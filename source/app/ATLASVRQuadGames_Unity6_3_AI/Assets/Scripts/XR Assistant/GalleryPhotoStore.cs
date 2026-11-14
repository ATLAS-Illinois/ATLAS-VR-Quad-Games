using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Local gameplay-photo album. Unity texture operations must stay on the main thread.
/// A successful save transfers texture ownership; a failed save leaves it with the caller.
/// </summary>
public sealed class GalleryPhotoStore : IDisposable
{
    public const int Capacity = 24;
    public const int MaxPhotoEdge = 1024;
    private const int MaxFileBytes = 8 * 1024 * 1024;
    private readonly string directory;
    private readonly Dictionary<Texture2D, string> paths = new Dictionary<Texture2D, string>();
    private bool loaded;
    private bool disposed;

    // Kept as a List for existing gallery readers. Mutations should use TrySave/TryDelete.
    public List<Texture2D> Photos { get; } = new List<Texture2D>();
    public string LastStatus { get; private set; } = "";
    public bool HasStorageWarning { get; private set; }
    public int UnreadableCount { get; private set; }
    public bool IsLoaded => loaded;

    public GalleryPhotoStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A gallery directory is required.", nameof(directory));
        this.directory = Path.GetFullPath(directory);
    }

    public bool Load()
    {
        if (disposed) return false;
        if (loaded) return true;
        string[] files;
        int preservedUnreadable;
        try
        {
            // A file occupying the directory is an IO error, not an empty album.
            if (File.Exists(directory)) throw new IOException("Gallery directory is unavailable.");
            if (Directory.Exists(directory))
            {
                // Only unfinished files from this store's atomic writes are disposable.
                foreach (string temporary in Directory.GetFiles(directory, "photo-*.tmp"))
                {
                    try { File.Delete(temporary); }
                    catch (Exception exception) when (IsStorageException(exception)) { /* Readable captures remain available. */ }
                }
            }
            files = Directory.Exists(directory) ? Directory.GetFiles(directory, "photo-*.png") : Array.Empty<string>();
            preservedUnreadable = Directory.Exists(directory) ? Directory.GetFiles(directory, "photo-*.unreadable").Length : 0;
            Array.Sort(files, StringComparer.Ordinal);
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            LastStatus = "Photos storage unavailable · try again later";
            HasStorageWarning = true;
            return false;
        }

        int unreadable = preservedUnreadable;
        bool storageUnavailable = false;
        foreach (string file in files)
        {
            if (Photos.Count >= Capacity) break;
            if (paths.ContainsValue(file)) continue; // Retry may follow a partial load after an IO error.
            Texture2D texture = null;
            bool invalidContent = false;
            try
            {
                byte[] bytes = ReadBoundedFile(file);
                if (bytes == null) { invalidContent = true; continue; }
                // Validate dimensions before letting the image decoder allocate a texture.
                if (!HasBoundedPngHeader(bytes)) { invalidContent = true; continue; }
                texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!ImageConversion.LoadImage(texture, bytes, false)) { invalidContent = true; continue; }
                texture.name = "Saved gameplay photo";
                Photos.Add(texture);
                paths.Add(texture, file);
                texture = null;
            }
            catch (Exception exception) when (IsStorageException(exception) || exception is UnityException || exception is ArgumentException)
            {
                if (IsStorageException(exception)) storageUnavailable = true;
                else invalidContent = true;
            }
            finally
            {
                DestroyTexture(texture);
                if (invalidContent)
                {
                    unreadable++;
                    // Keep the original bytes, but remove invalid entries from normal album capacity.
                    // A transient read/permission failure is never classified as invalid content.
                    if (!QuarantineUnreadable(file)) storageUnavailable = true;
                }
            }
        }
        UnreadableCount = unreadable;
        loaded = !storageUnavailable;
        HasStorageWarning = storageUnavailable || unreadable > 0;
        LastStatus = storageUnavailable ? "Some photos unavailable · reopen Photos to retry" : AlbumStatus;
        return loaded;
    }

    public bool TrySave(Texture2D photo, out string message)
    {
        message = "Photo could not be saved";
        if (disposed || (!loaded && !Load())) { message = LastStatus; return false; }
        if (photo == null || !photo.isReadable || photo.width < 1 || photo.height < 1 ||
            photo.width > MaxPhotoEdge || photo.height > MaxPhotoEdge)
            return Fail("Photo size or format is unavailable", out message);
        if (paths.ContainsKey(photo)) return Fail("This photo is already saved", out message);
        if (Photos.Count >= Capacity) return Fail("Album full · delete a photo to capture more", out message);

        string temporary = null;
        try
        {
            Directory.CreateDirectory(directory);
            // Invalid content is preserved separately during Load, leaving normal capture slots usable.
            if (Directory.GetFiles(directory, "photo-*.png").Length >= Capacity)
                return Fail("Album storage full · delete a saved photo", out message);
            byte[] bytes = ImageConversion.EncodeToPNG(photo);
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxFileBytes)
                return Fail("Photo could not be encoded", out message);
            string stem = "photo-" + DateTime.UtcNow.Ticks.ToString("D19", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            string destination = Path.Combine(directory, stem + ".png");
            temporary = Path.Combine(directory, stem + ".tmp");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            // Same-directory rename publishes only a completely written file; existing captures are never overwritten.
            File.Move(temporary, destination);
            temporary = null;
            paths.Add(photo, destination);
            Photos.Add(photo);
            message = "Saved on this device";
            LastStatus = AlbumStatus;
            HasStorageWarning = UnreadableCount > 0;
            return true;
        }
        catch (Exception exception) when (IsStorageException(exception) || exception is UnityException || exception is ArgumentException)
        {
            return Fail("Photo not saved · device storage unavailable", out message);
        }
        finally
        {
            if (temporary != null)
            {
                try { File.Delete(temporary); }
                catch (Exception exception) when (IsStorageException(exception)) { /* Retain prior captures when storage fails. */ }
            }
        }
    }

    public bool TryDelete(int index, out string message)
    {
        if (disposed || (!loaded && !Load())) { message = LastStatus; return false; }
        if (index < 0 || index >= Photos.Count) return Fail("Select a photo first", out message);
        Texture2D photo = Photos[index];
        if (photo == null || !paths.TryGetValue(photo, out string path))
            return Fail("This photo is not part of the saved album", out message);
        try { File.Delete(path); }
        catch (Exception exception) when (IsStorageException(exception))
        {
            // Never discard the in-memory image when deleting the disk copy failed.
            return Fail("Photo not deleted · device storage unavailable", out message);
        }
        Photos.RemoveAt(index);
        paths.Remove(photo);
        DestroyTexture(photo);
        message = "Photo deleted";
        LastStatus = UnreadableCount > 0 ? AlbumStatus : message;
        HasStorageWarning = UnreadableCount > 0;
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var photo in paths.Keys) DestroyTexture(photo);
        paths.Clear();
        Photos.Clear();
    }

    private bool Fail(string status, out string message)
    {
        LastStatus = message = status;
        HasStorageWarning = true;
        return false;
    }

    private string AlbumStatus => UnreadableCount > 0
        ? UnreadableCount + " unreadable photos kept separately" : "Saved on this device";

    private static bool QuarantineUnreadable(string file)
    {
        try
        {
            string destination = file + ".unreadable";
            if (File.Exists(destination)) destination = file + "." + Guid.NewGuid().ToString("N") + ".unreadable";
            File.Move(file, destination);
            return true;
        }
        catch (Exception exception) when (IsStorageException(exception)) { return false; }
    }

    private static bool HasBoundedPngHeader(byte[] data)
    {
        if (data.Length < 24 || data[0] != 137 || data[1] != 80 || data[2] != 78 || data[3] != 71 ||
            data[4] != 13 || data[5] != 10 || data[6] != 26 || data[7] != 10 ||
            data[12] != 73 || data[13] != 72 || data[14] != 68 || data[15] != 82) return false;
        uint width = ReadBigEndian(data, 16), height = ReadBigEndian(data, 20);
        return width > 0 && height > 0 && width <= MaxPhotoEdge && height <= MaxPhotoEdge;
    }

    private static uint ReadBigEndian(byte[] bytes, int at) =>
        ((uint)bytes[at] << 24) | ((uint)bytes[at + 1] << 16) | ((uint)bytes[at + 2] << 8) | bytes[at + 3];

    private static byte[] ReadBoundedFile(string file)
    {
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length < 24 || stream.Length > MaxFileBytes) return null;
            var bytes = new byte[(int)stream.Length];
            int read = 0;
            while (read < bytes.Length)
            {
                int count = stream.Read(bytes, read, bytes.Length - read);
                if (count == 0) return null;
                read += count;
            }
            return stream.ReadByte() < 0 ? bytes : null;
        }
    }

    private static bool IsStorageException(Exception exception) => exception is IOException ||
        exception is UnauthorizedAccessException || exception is System.Security.SecurityException || exception is NotSupportedException;

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
        else UnityEngine.Object.DestroyImmediate(texture);
    }
}

/// <summary>Shared persistent album behind the existing Camera and Photos presentations.</summary>
public static class GalleryDataManager
{
    public const int Capacity = GalleryPhotoStore.Capacity;
    public const int MaxPhotoEdge = GalleryPhotoStore.MaxPhotoEdge;
    private static GalleryPhotoStore store;
    public static List<Texture2D> SavedPhotos { get { EnsureLoaded(); return store.Photos; } }
    public static string LastStatus { get { EnsureLoaded(); return store.LastStatus; } }
    public static bool HasStorageWarning { get { EnsureLoaded(); return store.HasStorageWarning; } }
    public static int UnreadableCount { get { EnsureLoaded(); return store.UnreadableCount; } }

    public static void EnsureLoaded()
    {
        if (store != null) return;
        store = new GalleryPhotoStore(Path.Combine(Application.persistentDataPath, "QuadGameplayPhotos"));
        store.Load();
    }

    // Call on opening Photos, not every render/update. This also retries a partially loaded album.
    public static bool RetryLoad()
    {
        if (store == null) { EnsureLoaded(); return store.IsLoaded; }
        return store.Load();
    }

    public static bool TrySave(Texture2D photo, out string message)
    {
        EnsureLoaded();
        return store.TrySave(photo, out message);
    }

    public static bool TryDelete(int index, out string message)
    {
        EnsureLoaded();
        return store.TryDelete(index, out message);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        store?.Dispose();
        store = null;
    }
}
