#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class QuadLocalFeatureChecks
{
    [Serializable] private class Result { public string name; public bool passed; }
    [Serializable] private class Report
    {
        public int passed, failed;
        public List<Result> checks = new List<Result>();
        public string limitation = "Editor controls and local file storage; physical tracking and Android persistence require device validation.";
    }
    public static string Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Run in idle Play Mode.");
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(s => s.gameObject.scene.IsValid());
        if (shell.assistantBridge.IsBusy || shell.assistantBridge.IsListening || shell.captionBridge.IsBusy || shell.captionBridge.IsListening)
            throw new InvalidOperationException("Close the user's active voice session before validation.");
        var report = new Report();
        var screen = shell.FeatureScreens;
        bool hadPrefs = PlayerPrefs.HasKey(RayBanFeatureScreen.TutorialReadKey);
        int oldPrefs = PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialReadKey);
        int countBefore = GalleryDataManager.SavedPhotos.Count;
        Texture2D testPhoto = null;
        Action<bool, string> check = (pass, name) => { report.checks.Add(new Result { name = name, passed = pass }); if (pass) report.passed++; else report.failed++; };
        Action<string> press = action => screen.Activate(screen.Controls.First(t => t != null && t.ActionId == action && t.gameObject.activeInHierarchy));
        try
        {
            int controlsBefore = screen.Controls.Count;
            screen.UpgradeLocalFeatures(); screen.UpgradeLocalFeatures();
            check(screen.Controls.Count == controlsBefore, "Feature upgrade is idempotent");
            check(screen.Controls.Count(t => t && t.ActionId == "photos.delete") == 1, "One photo delete control");
            shell.OpenApp("Tutorials");
            check(screen.TutorialStepCount == 8, "Eight feature guides");
            int readBefore = screen.TutorialReadCount;
            press("tutorials.next"); press("tutorials.previous");
            check(screen.TutorialReadCount == readBefore, "Reading navigation does not claim completion");
            press("tutorials.read"); int marked = screen.TutorialReadCount; press("tutorials.read");
            check(screen.TutorialReadCount == marked, "Mark read is idempotent");
            check((PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialReadKey) & (1 << screen.TutorialStepIndex)) != 0, "Read progress persisted");
            shell.ReturnToLauncher(); shell.OpenApp("Tutorials");
            check(screen.TutorialReadCount == marked, "Read progress survives feature re-entry");
            while (screen.TutorialStepIndex != 0) press("tutorials.next");
            string[] expected = { "", "camera", "photos", "maps", "music", "assistant", "captions", "" };
            for (int i = 0; i < 8; i++)
            {
                if (i > 0) { shell.OpenApp("Tutorials"); press("tutorials.next"); }
                Canvas.ForceUpdateCanvases();
                foreach (var text in screen.GetComponentsInChildren<TMPro.TMP_Text>()) text.ForceMeshUpdate();
                check(!screen.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.isTextOverflowing), "Guide " + i + " text fits");
                if (i == 2) QuadRayBanDisplayPreview.Capture(shell, "Diagnostics/2026-11-01-QuestValidation/feature-guide.png");
                press("tutorials.open");
                check(expected[i] == "" ? string.IsNullOrEmpty(shell.CurrentDetail) : screen.CurrentView == expected[i], "Guide shortcut " + i + " opens expected view");
                check(!shell.assistantBridge.IsListening && !shell.assistantBridge.IsBusy && !shell.captionBridge.IsListening && !shell.captionBridge.IsBusy,
                    "Guide shortcut " + i + " does not start cloud or microphone");
                if (i == 4) check(!shell.musicBridge.IsPlaying, "Music shortcut does not autoplay");
            }
            shell.OpenApp("Tutorials"); press("tutorials.next");
            check(screen.TutorialStepIndex == 0, "Guide Next wraps");
            press("tutorials.previous"); check(screen.TutorialStepIndex == 7, "Guide Previous wraps");
            if (countBefore < GalleryDataManager.Capacity)
            {
                testPhoto = new Texture2D(16, 16, TextureFormat.RGB24, false);
                testPhoto.SetPixels(Enumerable.Repeat(new Color(.12f, .55f, .8f), 256).ToArray()); testPhoto.Apply();
                if (!GalleryDataManager.TrySave(testPhoto, out string error)) throw new InvalidOperationException(error);
                shell.OpenApp("Photos");
                check(screen.Controls.First(t => t && t.ActionId == "photos.delete").IsInteractable(), "Saved photo can be selected for deletion");
                QuadRayBanDisplayPreview.Capture(shell, "Diagnostics/2026-11-01-QuestValidation/persistent-photos.png");
                press("photos.delete");
                check(screen.CurrentView == "photos.delete" && GalleryDataManager.SavedPhotos.Contains(testPhoto), "Delete requires confirmation");
                check(EventSystem.current.currentSelectedGameObject.GetComponent<RayBanTile>().ActionId == "photos.cancel", "Delete defaults to Keep");
                QuadRayBanDisplayPreview.Capture(shell, "Diagnostics/2026-11-01-QuestValidation/photo-delete-confirmation.png");
                screen.Back();
                check(screen.CurrentView == "photos" && GalleryDataManager.SavedPhotos.Contains(testPhoto), "Back cancels deletion");
                press("photos.delete"); press("photos.cancel");
                check(GalleryDataManager.SavedPhotos.Contains(testPhoto), "Keep preserves photo");
                press("photos.delete"); press("photos.confirm");
                check(GalleryDataManager.SavedPhotos.Count == countBefore && !GalleryDataManager.SavedPhotos.Contains(testPhoto), "Confirm removes only selected photo");
                testPhoto = null;
                if (countBefore == 0) check(!screen.Controls.First(t => t && t.ActionId == "photos.delete").IsInteractable(), "Empty album disables delete");
            }
            else report.limitation += " Photo UI mutation checks skipped: existing album is full; user photos were preserved.";
        }
        finally
        {
            shell.Hide();
            if (testPhoto != null)
            {
                int index = GalleryDataManager.SavedPhotos.IndexOf(testPhoto);
                if (index >= 0) GalleryDataManager.TryDelete(index, out _); else UnityEngine.Object.Destroy(testPhoto);
            }
            if (hadPrefs) PlayerPrefs.SetInt(RayBanFeatureScreen.TutorialReadKey, oldPrefs); else PlayerPrefs.DeleteKey(RayBanFeatureScreen.TutorialReadKey);
            PlayerPrefs.Save();
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText("Diagnostics/2026-11-01-QuestValidation/local-feature-checks.json", json);
        }
        return "Local feature checks: " + report.passed + " passed; " + report.failed + " failed.";
    }
}
#endif
