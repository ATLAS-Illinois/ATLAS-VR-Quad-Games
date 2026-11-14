#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Exercises real PNG/file operations in a disposable isolated album, never the user's saved photos.</summary>
public static class QuadGalleryStorageChecks
{
    [Serializable] private sealed class Check { public string name; public bool passed; }
    [Serializable] private sealed class Report
    {
        public string finishedUtc;
        public int passed, failed;
        public string limitation = "Editor file/texture checks in an isolated temporary folder. Does not establish Android storage or physical camera/gesture behavior.";
        public List<Check> checks = new List<Check>();
    }

    [MenuItem("Tools/Ray-Ban Display/Validate persistent photo storage")]
    public static string RunChecks()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Run storage checks while the Editor is idle outside Play Mode.");
        var report = new Report();
        string root = Path.Combine(Path.GetTempPath(), "QuadGalleryChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var callerOwned = new List<Texture2D>();
        GalleryPhotoStore store = null;
        try
        {
            string album = Path.Combine(root, "album");
            store = new GalleryPhotoStore(album);
            Assert(report, "Missing album loads empty", store.Load() && store.Photos.Count == 0);
            Texture2D red = Photo(Color.red); callerOwned.Add(red);
            Assert(report, "Capture writes PNG and transfers ownership", store.TrySave(red, out _) && store.Photos.Count == 1);
            callerOwned.Remove(red);
            Texture2D blue = Photo(Color.blue); callerOwned.Add(blue);
            Assert(report, "Second capture appends without replacing first", store.TrySave(blue, out _) && store.Photos[0] == red && store.Photos[1] == blue);
            callerOwned.Remove(blue);
            Assert(report, "Only complete files published", Directory.GetFiles(album, "photo-*.png").Length == 2 && Directory.GetFiles(album, "*.tmp").Length == 0);
            Assert(report, "Duplicate texture rejected", !store.TrySave(red, out _) && store.Photos.Count == 2);
            store.Dispose(); store = new GalleryPhotoStore(album);
            Assert(report, "Fresh session restores captures", store.Load() && store.Photos.Count == 2);
            Assert(report, "Capture order and image pixels survive reload", store.Photos[0].GetPixel(0, 0).r > .9f && store.Photos[1].GetPixel(0, 0).b > .9f);
            Assert(report, "Repeated load does not duplicate photos", store.Load() && store.Photos.Count == 2);
            Assert(report, "Invalid delete leaves album intact", !store.TryDelete(-1, out _) && !store.TryDelete(2, out _) && store.Photos.Count == 2);
            Assert(report, "Selected photo removed from memory and storage", store.TryDelete(0, out _) && store.Photos.Count == 1 && Directory.GetFiles(album, "*.png").Length == 1);
            store.Dispose(); store = new GalleryPhotoStore(album);
            Assert(report, "Deleted capture stays deleted after reload", store.Load() && store.Photos.Count == 1 && store.Photos[0].GetPixel(0, 0).b > .9f);
            store.Dispose(); store = null;

            File.WriteAllText(Path.Combine(album, "photo-corrupt.png"), "damaged capture");
            byte[] oversizedHeader = new byte[24];
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            Array.Copy(signature, oversizedHeader, signature.Length);
            oversizedHeader[12] = 73; oversizedHeader[13] = 72; oversizedHeader[14] = 68; oversizedHeader[15] = 82;
            oversizedHeader[16] = 127; oversizedHeader[20] = 127;
            File.WriteAllBytes(Path.Combine(album, "photo-oversized.png"), oversizedHeader);
            File.WriteAllText(Path.Combine(album, "unfinished.tmp"), "incomplete file");
            File.WriteAllText(Path.Combine(album, "photo-incomplete.tmp"), "interrupted capture");
            store = new GalleryPhotoStore(album);
            Assert(report, "Corrupt and oversized files preserved separately", store.Load() && store.Photos.Count == 1 && store.UnreadableCount == 2 && store.HasStorageWarning);
            Assert(report, "Temporary files never appear as photos", store.Photos[0].GetPixel(0, 0).b > .9f);
            Assert(report, "Interrupted capture is cleaned without touching unrelated files", !File.Exists(Path.Combine(album, "photo-incomplete.tmp")) && File.Exists(Path.Combine(album, "unfinished.tmp")));
            store.Dispose(); store = null;

            string damagedAlbum = Path.Combine(root, "all-damaged");
            Directory.CreateDirectory(damagedAlbum);
            for (int i = 0; i < GalleryPhotoStore.Capacity; i++)
                File.WriteAllText(Path.Combine(damagedAlbum, "photo-" + i.ToString("D2") + ".png"), "damaged capture " + i);
            store = new GalleryPhotoStore(damagedAlbum);
            Assert(report, "24 damaged files do not occupy normal capture slots", store.Load() && store.Photos.Count == 0 && store.UnreadableCount == 24 && Directory.GetFiles(damagedAlbum, "*.png").Length == 0);
            bool originalsPreserved = true;
            for (int i = 0; i < GalleryPhotoStore.Capacity; i++)
                originalsPreserved &= File.ReadAllText(Path.Combine(damagedAlbum, "photo-" + i.ToString("D2") + ".png.unreadable")) == "damaged capture " + i;
            Assert(report, "Quarantine preserves every damaged file byte for byte", originalsPreserved);
            Texture2D recovered = Photo(Color.cyan); callerOwned.Add(recovered);
            Assert(report, "New capture succeeds after fully corrupt album recovery", store.TrySave(recovered, out _) && store.Photos.Count == 1 && Directory.GetFiles(damagedAlbum, "*.png").Length == 1);
            callerOwned.Remove(recovered);
            store.Dispose(); store = new GalleryPhotoStore(damagedAlbum);
            Assert(report, "Recovered album and warning survive reload", store.Load() && store.Photos.Count == 1 && store.UnreadableCount == 24 && store.HasStorageWarning && store.Photos[0].GetPixel(0, 0).b > .9f);
            store.Dispose(); store = null;

            string fullAlbum = Path.Combine(root, "capacity");
            store = new GalleryPhotoStore(fullAlbum);
            bool filled = store.Load();
            for (int i = 0; i < GalleryPhotoStore.Capacity; i++)
            {
                Texture2D photo = Photo(Color.green);
                if (!store.TrySave(photo, out _)) { filled = false; callerOwned.Add(photo); break; }
            }
            Assert(report, "Album accepts exactly 24 captures", filled && store.Photos.Count == 24 && Directory.GetFiles(fullAlbum, "*.png").Length == 24);
            Texture2D overflow = Photo(Color.yellow); callerOwned.Add(overflow);
            Assert(report, "Full album rejects new photo without eviction", !store.TrySave(overflow, out _) && overflow != null && store.Photos.Count == 24 && Directory.GetFiles(fullAlbum, "*.png").Length == 24);
            Assert(report, "Delete frees a slot for next capture", store.TryDelete(0, out _) && store.TrySave(overflow, out _) && store.Photos.Count == 24);
            callerOwned.Remove(overflow);
            store.Dispose(); store = null;

            string blocked = Path.Combine(root, "blocked");
            store = new GalleryPhotoStore(blocked);
            store.Load();
            File.WriteAllText(blocked, "a file prevents creating the album folder");
            Texture2D rejected = Photo(Color.white); callerOwned.Add(rejected);
            Assert(report, "Storage failure does not claim capture success", !store.TrySave(rejected, out string status) && status.Contains("unavailable"));
            Assert(report, "Storage failure preserves caller ownership and existing files", rejected != null && store.Photos.Count == 0 && File.Exists(blocked) && Directory.GetFiles(fullAlbum, "*.png").Length == 24);
            store.Dispose(); store = null;
            store = new GalleryPhotoStore(blocked);
            Assert(report, "Unavailable album is not silently treated as empty", !store.Load() && !store.IsLoaded);
            File.Delete(blocked);
            Assert(report, "Failed load can retry after storage recovers", store.Load() && store.IsLoaded && store.Photos.Count == 0);
            store.Dispose(); store = null;

            // On Windows an exclusive open models an unavailable disk file for deletion.
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                store = new GalleryPhotoStore(fullAlbum); store.Load();
                string[] ordered = Directory.GetFiles(fullAlbum, "photo-*.png"); Array.Sort(ordered, StringComparer.Ordinal);
                using (var locked = new FileStream(ordered[0], FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Assert(report, "Failed deletion keeps the selected photo", !store.TryDelete(0, out _) && store.Photos.Count == 24);
                Assert(report, "Deletion succeeds after storage recovers", store.TryDelete(0, out _) && store.Photos.Count == 23);
            }
        }
        catch (Exception exception)
        {
            Assert(report, "Unexpected test exception: " + exception.GetType().Name, false);
        }
        finally
        {
            store?.Dispose();
            foreach (Texture2D photo in callerOwned) if (photo != null) UnityEngine.Object.DestroyImmediate(photo);
            // This exact unique directory was created by this test, and contains only test captures.
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        report.finishedUtc = DateTime.UtcNow.ToString("o");
        string output = "Diagnostics/2026-11-01-QuestValidation/gallery-storage-checks.json";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        string json = JsonUtility.ToJson(report, true);
        File.WriteAllText(output, json);
        Debug.Log("[QuadGalleryStorageChecks] " + report.passed + " passed, " + report.failed + " failed.");
        return json;
    }

    private static Texture2D Photo(Color color)
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGB24, false);
        var pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        texture.SetPixels(pixels); texture.Apply();
        return texture;
    }

    private static void Assert(Report report, string name, bool passed)
    {
        report.checks.Add(new Check { name = name, passed = passed });
        if (passed) report.passed++; else report.failed++;
    }
}
#endif
