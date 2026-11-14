using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public enum QuadNotificationKind { PhotoSaved = 1, RouteArrived = 2 }

[Serializable]
public sealed class QuadLocalNotification
{
    [SerializeField] private string id, title, message;
    [SerializeField] private long createdUtcTicks;
    [SerializeField] private bool isRead;
    [SerializeField] private QuadNotificationKind kind;
    public string Id => id;
    public string Title => title;
    public string Message => message;
    public long CreatedUtcTicks => createdUtcTicks;
    public bool IsRead => isRead;
    public QuadNotificationKind Kind => kind;

    internal QuadLocalNotification(string entryId, QuadNotificationKind entryKind, string heading,
        string body, long ticks, bool read)
    { id = entryId; kind = entryKind; title = heading; message = body; createdUtcTicks = ticks; isRead = read; }
    internal void Read() => isRead = true;
}

/// <summary>
/// Local app activity only. Callers supply authored copy, never transcripts,
/// responses, credentials, downloaded text or arbitrary navigation actions.
/// The injectable directory keeps checks separate from the user's history.
/// </summary>
public sealed class QuadLocalNotificationStore
{
    public const int MaximumItems = 20;
    public const int MaximumFileBytes = 32768;
    public const string StorageFileName = "activity-v1.json";
    [Serializable] private sealed class SavedState
    {
        public int version = 1;
        public bool doNotDisturb;
        public List<QuadLocalNotification> entries = new List<QuadLocalNotification>();
    }

    private readonly List<QuadLocalNotification> entries = new List<QuadLocalNotification>();
    private readonly IReadOnlyList<QuadLocalNotification> readOnlyEntries;
    private readonly string directory, path;
    private bool doNotDisturb;
    public IReadOnlyList<QuadLocalNotification> Items => readOnlyEntries;
    public int Revision { get; private set; }
    public string LastStorageError { get; private set; }
    public int UnreadCount
    {
        get { int count = 0; foreach (var item in entries) if (!item.IsRead) count++; return count; }
    }
    public bool DoNotDisturb
    {
        get => doNotDisturb;
        set { if (doNotDisturb == value) return; doNotDisturb = value; Changed(); }
    }

    public QuadLocalNotificationStore(string storageDirectory)
    {
        if (string.IsNullOrWhiteSpace(storageDirectory)) throw new ArgumentException("A storage directory is required.", nameof(storageDirectory));
        directory = Path.GetFullPath(storageDirectory);
        path = Path.Combine(directory, StorageFileName);
        readOnlyEntries = entries.AsReadOnly();
        Load();
    }

    public string Publish(QuadNotificationKind kind, string title, string message)
    {
        if (!KnownKind(kind)) return null;
        title = SafeText(title, 72);
        message = SafeText(message, 180);
        if (string.IsNullOrEmpty(title)) return null;
        string id = Guid.NewGuid().ToString("N");
        long ticks = DateTime.UtcNow.Ticks;
        // Preserve newest-first order even when the platform clock has a coarse
        // resolution and two app events happen within the same clock tick.
        if (entries.Count > 0 && entries[0].CreatedUtcTicks >= ticks && entries[0].CreatedUtcTicks < DateTime.MaxValue.Ticks)
            ticks = entries[0].CreatedUtcTicks + 1;
        entries.Insert(0, new QuadLocalNotification(id, kind, title, message, ticks, false));
        if (entries.Count > MaximumItems) entries.RemoveRange(MaximumItems, entries.Count - MaximumItems);
        Changed();
        return id;
    }

    public bool MarkRead(string id)
    {
        var entry = Find(id);
        if (entry == null || entry.IsRead) return false;
        entry.Read(); Changed(); return true;
    }

    public bool Delete(string id)
    {
        var entry = Find(id);
        if (entry == null) return false;
        entries.Remove(entry); Changed(); return true;
    }

    public bool Clear()
    {
        if (entries.Count == 0) return false;
        entries.Clear(); Changed(); return true;
    }

    public QuadLocalNotification Find(string id) => entries.Find(item => item.Id == id);
    private static bool KnownKind(QuadNotificationKind kind) => kind == QuadNotificationKind.PhotoSaved || kind == QuadNotificationKind.RouteArrived;

    private void Changed() { Revision++; Save(); }

    private void Load()
    {
        LastStorageError = null;
        try
        {
            if (!File.Exists(path)) return;
            // Bound allocation before deserialization, including a final byte
            // count for a file changed between the size check and read.
            if (new FileInfo(path).Length > MaximumFileBytes) throw new InvalidDataException();
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaximumFileBytes) throw new InvalidDataException();
                bytes = new byte[(int)stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0) throw new EndOfStreamException();
                    read += count;
                }
            }
            var state = JsonUtility.FromJson<SavedState>(Encoding.UTF8.GetString(bytes));
            if (state == null || state.version != 1 || state.entries == null) throw new InvalidDataException();
            doNotDisturb = state.doNotDisturb;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in state.entries)
            {
                if (item == null || !KnownKind(item.Kind) || !Guid.TryParseExact(item.Id, "N", out _) ||
                    item.CreatedUtcTicks < DateTime.MinValue.Ticks || item.CreatedUtcTicks > DateTime.MaxValue.Ticks || !seen.Add(item.Id)) continue;
                string title = SafeText(item.Title, 72);
                if (string.IsNullOrEmpty(title)) continue;
                entries.Add(new QuadLocalNotification(item.Id, item.Kind, title, SafeText(item.Message, 180), item.CreatedUtcTicks, item.IsRead));
            }
            entries.Sort((left, right) => right.CreatedUtcTicks.CompareTo(left.CreatedUtcTicks));
            if (entries.Count > MaximumItems) entries.RemoveRange(MaximumItems, entries.Count - MaximumItems);
        }
        catch (Exception error) when (IsStorageFailure(error))
        {
            entries.Clear(); doNotDisturb = false;
            LastStorageError = "Saved activity could not be read. New activity is available for this session.";
        }
    }

    private void Save()
    {
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            var state = new SavedState { doNotDisturb = doNotDisturb, entries = entries };
            string json = JsonUtility.ToJson(state);
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new InvalidDataException();
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            // Replace is atomic and retains the previous valid file if the
            // replacement fails. Never delete it before moving the new state.
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            LastStorageError = null;
        }
        catch (Exception error) when (IsStorageFailure(error))
        {
            LastStorageError = "Activity changes could not be saved. They remain available for this session.";
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (IsStorageFailure(error)) { }
        }
    }

    private static bool IsStorageFailure(Exception error) => error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
        error is ArgumentException || error is NotSupportedException || error is System.Security.SecurityException;

    private static string SafeText(string text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var result = new StringBuilder(Math.Min(maximum, text.Length));
        for (int i = 0; i < text.Length && result.Length < maximum; i++)
        {
            char c = text[i];
            if (c == '<' || c == '>' || char.IsSurrogate(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format) continue;
            if (char.IsWhiteSpace(c)) c = ' ';
            else if (char.IsControl(c)) continue;
            result.Append(c);
        }
        return result.ToString().Trim();
    }
}

/// <summary>One device-local store; no server, phone notifications, or OS settings.</summary>
public static class RayBanLocalNotifications
{
    private static QuadLocalNotificationStore store;
    private static string initializationError;
    public static QuadLocalNotificationStore Store => store ?? (store = new QuadLocalNotificationStore(Path.Combine(Application.persistentDataPath, "QuadDisplayActivity")));
    public static string LastStorageError => initializationError ?? (store != null ? store.LastStorageError : null);
    /// <summary>Never let optional activity recording fail an already successful app action.</summary>
    public static bool Publish(QuadNotificationKind kind, string title, string message)
    {
        try
        {
            initializationError = null;
            return Store.Publish(kind, title, message) != null;
        }
        catch (Exception)
        {
            initializationError = "Activity could not be recorded. Your completed action is unaffected.";
            return false;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { store = null; initializationError = null; }

#if UNITY_EDITOR
    public static IDisposable UseStoreForEditorChecks(QuadLocalNotificationStore replacement)
    {
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));
        var previous = store; store = replacement;
        return new RestoreStore(previous);
    }
    private sealed class RestoreStore : IDisposable
    {
        private readonly QuadLocalNotificationStore previous;
        private bool disposed;
        public RestoreStore(QuadLocalNotificationStore original) => previous = original;
        public void Dispose() { if (disposed) return; disposed = true; store = previous; }
    }
#endif
}
