#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class QuadDisplayReadingChecks
{
    [Serializable] private class Check { public string name; public bool passed; }
    [Serializable] private class Report
    {
        public int passed, failed;
        public List<Check> checks = new List<Check>();
        public string scope = "Editor UI flow with synthetic captions and isolated local activity. No speech recognition, cloud request, or real photo mutation.";
    }
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string Output = "Diagnostics/2026-11-08-DisplayCompletion";

    public static string Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Use idle Play Mode.");
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(s => s.gameObject.scene.IsValid());
        if (shell.assistantBridge.IsBusy || shell.assistantBridge.IsListening || shell.captionBridge.IsBusy || shell.captionBridge.IsListening)
            throw new InvalidOperationException("A user voice session is active.");
        var screen = shell.FeatureScreens;
        var manager = shell.captionBridge.manager;
        var report = new Report();
        string[] fields = { "originalHistory", "translatedHistory", "currentOriginal", "currentTranslation" };
        var original = fields.ToDictionary(n => n, n => typeof(LiveTranslationManager).GetField(n, Private).GetValue(manager));
        var followField = typeof(RayBanFeatureScreen).GetField("captionFollowLatest", Private);
        bool originalFollow = (bool)followField.GetValue(screen);
        var transcript = (TMP_Text)typeof(RayBanFeatureScreen).GetField("captionTranscript", Private).GetValue(screen);
        int originalPage = transcript.pageToDisplay;
        var store = new QuadLocalNotificationStore(Path.Combine(Output, "reading-check-store-" + Guid.NewGuid().ToString("N")));
        Action<bool, string> check = (ok, name) => { report.checks.Add(new Check { name = name, passed = ok }); if (ok) report.passed++; else report.failed++; };
        Action<string> press = action => screen.Activate(screen.Controls.First(t => t != null && t.ActionId == action && t.gameObject.activeInHierarchy));
        Action refresh = () => typeof(RayBanFeatureScreen).GetMethod("RefreshLiveCaptions", Private).Invoke(screen, null);
        try
        {
            using (RayBanLocalNotifications.UseStoreForEditorChecks(store))
            {
                int before = screen.Controls.Count;
                screen.UpgradeLocalFeatures(); screen.UpgradeLocalFeatures();
                check(screen.Controls.Count == before, "Repeated feature upgrade adds no duplicate controls");
                shell.OpenApp("Captions");
                string sample = string.Join("\n\n", Enumerable.Range(1, 18).Select(i => "Sample caption " + i + ": Explore the campus and follow the marked path."));
                foreach (string name in fields) typeof(LiveTranslationManager).GetField(name, Private).SetValue(manager, name.EndsWith("History", StringComparison.Ordinal) ? sample : "");
                followField.SetValue(screen, true); refresh();
                int pages = transcript.textInfo.pageCount;
                check(pages > 2 && transcript.pageToDisplay == pages, "Live captions follow newest page");
                press("captions.earlier"); int readingPage = transcript.pageToDisplay;
                check(readingPage == pages - 1 && !(bool)followField.GetValue(screen), "Earlier enters reading mode");
                foreach (string name in new[] { "originalHistory", "translatedHistory" }) typeof(LiveTranslationManager).GetField(name, Private).SetValue(manager, sample + "\n\nOne more sample caption.");
                refresh();
                check(transcript.pageToDisplay == readingPage, "Incoming text does not steal the page while reading");
                for (int i = 0; i < pages + 2; i++) press("captions.earlier");
                check(transcript.pageToDisplay == 1, "Earlier is bounded at first page");
                check(EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<RayBanTile>().IsInteractable(), "Caption boundary keeps a usable focus");
                QuadRayBanDisplayPreview.Capture(shell, Output + "/captions-reading.png");
                press("captions.latest");
                check(transcript.pageToDisplay == transcript.textInfo.pageCount && (bool)followField.GetValue(screen), "Latest resumes live following");
                screen.NavigateFrom(screen.Controls.First(t => t.ActionId == "captions.latest"), MoveDirection.Down);
                var retained = EventSystem.current.currentSelectedGameObject.GetComponent<RayBanTile>();
                string retainedAction = retained.ActionId;
                shell.ReturnToLauncher(); shell.OpenApp("Captions");
                check(EventSystem.current.currentSelectedGameObject.GetComponent<RayBanTile>().ActionId == retainedAction, "Feature focus returns to the previous control");
                check(!shell.captionBridge.IsListening && !shell.captionBridge.IsBusy, "Browsing captions does not start recording");

                string photo = store.Publish(QuadNotificationKind.PhotoSaved, "Sample photo saved", "Test event in an isolated history.");
                string route = store.Publish(QuadNotificationKind.RouteArrived, "Sample route arrived", "Test event in an isolated history.");
                shell.OpenApp("Notifications"); screen.OpenLocalNotification(photo);
                check(screen.Controls.First(t => t.ActionId == "activity.feature").IsInteractable(), "Photo activity has an available destination");
                QuadRayBanDisplayPreview.Capture(shell, Output + "/activity-detail.png");
                int photos = GalleryDataManager.SavedPhotos.Count;
                press("activity.feature");
                check(screen.CurrentView == "photos" && photos == GalleryDataManager.SavedPhotos.Count, "Photo event opens album without changing photos");
                shell.OpenApp("Notifications"); screen.OpenLocalNotification(route); press("activity.feature");
                check(screen.CurrentView == "maps", "Arrival event opens Maps");
                shell.OpenApp("Notifications"); screen.OpenLocalNotification("missing");
                check(!screen.Controls.First(t => t.ActionId == "activity.feature").IsInteractable(), "Missing activity cannot launch a feature");
                check(store.Items.Count == 2, "Opening events preserves local history");
                check(!shell.assistantBridge.IsListening && !shell.captionBridge.IsListening, "No voice session started by tested flows");
            }
        }
        finally
        {
            shell.ReturnToLauncher();
            foreach (var item in original) typeof(LiveTranslationManager).GetField(item.Key, Private).SetValue(manager, item.Value);
            followField.SetValue(screen, originalFollow);
            transcript.pageToDisplay = originalPage;
            screen.RefreshLocalNotificationView(true);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/reading-checks.json", JsonUtility.ToJson(report, true));
        }
        return "Reading/activity: " + report.passed + " passed; " + report.failed + " failed.";
    }
}
#endif
