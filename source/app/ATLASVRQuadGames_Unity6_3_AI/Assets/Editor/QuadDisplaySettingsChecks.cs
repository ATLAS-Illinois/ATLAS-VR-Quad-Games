#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Exercises real settings controls, saves/reloads, and reset boundaries; restores user preferences.</summary>
public static class QuadDisplaySettingsChecks
{
    [Serializable] private class Check { public string name; public bool passed; }
    [Serializable] private class Report
    {
        public int passed, failed;
        public List<Check> checks = new List<Check>();
        public string limitation = "Editor UI and preference persistence only. Headset readability and physical input require device validation.";
    }

    public static string Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in idle Play Mode.");
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(s => s.gameObject.scene.IsValid() && s.UsesLiveFeatures && s.appSettings != null && s.appSettings.IsActive);
        if ((shell.assistantBridge != null && (shell.assistantBridge.IsBusy || shell.assistantBridge.IsListening)) ||
            (shell.captionBridge != null && (shell.captionBridge.IsBusy || shell.captionBridge.IsListening)))
            throw new InvalidOperationException("Close the active voice session before validation.");
        var screen = shell.FeatureScreens;
        var settings = shell.appSettings;
        var report = new Report();
        string[] keys = { RayBanAppSettings.VolumeKey, RayBanAppSettings.BrightnessKey,
            RayBanAppSettings.ReducedMotionKey, RayBanAppSettings.StrongContrastKey };
        bool[] existed = keys.Select(PlayerPrefs.HasKey).ToArray();
        float volumeBefore = PlayerPrefs.GetFloat(keys[0], 1f), brightnessBefore = PlayerPrefs.GetFloat(keys[1], 1f);
        int motionBefore = PlayerPrefs.GetInt(keys[2]), contrastBefore = PlayerPrefs.GetInt(keys[3]);
        float listenerBefore = AudioListener.volume;
        var photosBefore = GalleryDataManager.SavedPhotos.ToArray();
        int activityRevision = RayBanLocalNotifications.Store.Revision;
        bool quietBefore = RayBanLocalNotifications.Store.DoNotDisturb;
        int tutorialBefore = PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialReadKey, -1);
        int welcomeBefore = PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialDismissedKey, -1);
        string appOrderBefore = PlayerPrefs.GetString("ATLASVRQuadGames.Display.LauncherOrder.v1", "");
        string sentinel = "ATLASVRQuadGames.SettingsCheck." + Guid.NewGuid().ToString("N");
        Action<bool, string> check = (passed, name) =>
        {
            report.checks.Add(new Check { name = name, passed = passed });
            if (passed) report.passed++; else report.failed++;
        };
        Action<string> press = action => screen.Activate(screen.Controls.First(t => t != null && t.ActionId == action && t.gameObject.activeInHierarchy));
        Action<string> checkText = name =>
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in screen.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            check(!screen.GetComponentsInChildren<TMP_Text>().Any(t => t.isTextOverflowing), name);
        };
        try
        {
            PlayerPrefs.SetString(sentinel, "keep");
            screen.EnsureExtendedSettings();
            int controlsBefore = screen.Controls.Count;
            screen.EnsureExtendedSettings();
            check(screen.Controls.Count == controlsBefore, "Repeated settings upgrade keeps one set of controls");
            shell.OpenApp("Settings");
            check(screen.Controls.Count(t => t != null && t.ActionId.StartsWith("settings", StringComparison.Ordinal) && t.gameObject.activeInHierarchy) == 7,
                "Settings hub exposes seven destinations");
            checkText("Settings hub labels fit");
            press("settings.accessibility");
            check(screen.CurrentView == "settings.accessibility", "Accessibility opens through settings control");
            settings.SetReducedMotion(false); settings.SetStrongContrast(false);
            press("settings.motion.toggle"); press("settings.contrast.toggle");
            check(settings.ReducedMotion && settings.StrongContrast, "Accessibility toggles change live settings");
            var focusTile = screen.Controls.First(t => t != null && t.ActionId == "settings.motion.toggle");
            focusTile.SetVisualFocus(false);
            var scale = focusTile.transform.localScale;
            var label = focusTile.LabelRect.anchoredPosition;
            focusTile.SetVisualFocus(true);
            check(focusTile.transform.localScale == scale && focusTile.LabelRect.anchoredPosition == label && focusTile.Surface.Focus == 1f,
                "Reduced motion retains focus color without zoom or label movement");
            check(shell.VisualRoot.GetComponentsInChildren<RayBanSurface>(true).All(s => s.StrongContrast),
                "Stronger contrast reaches visible and hidden display surfaces");
            settings.Deactivate(); settings.Activate(shell.VisualRoot);
            check(settings.ReducedMotion && settings.StrongContrast, "Accessibility survives settings deactivation and reload");
            screen.RefreshExtendedSettings();
            checkText("Accessibility labels and explanation fit");
            screen.Back();
            check(screen.CurrentView == "settings", "Accessibility Back returns to settings");
            press("settings_launcher");
            check(screen.CurrentView == "launcher_settings", "App order opens from settings");
            screen.Back();
            check(screen.CurrentView == "settings", "App order Back returns to settings");
            press("settings.activity"); press("activity.settings"); screen.Back();
            check(screen.CurrentView == "notifications", "Quiet mode Back returns to app activity");
            screen.Back();
            check(screen.CurrentView == "settings", "App activity Back returns to settings");
            settings.SetVolume(.4f); settings.SetBrightness(.65f);
            press("settings.reset");
            check(screen.CurrentView == "settings.reset" && settings.ReducedMotion && Mathf.Approximately(settings.Volume, .4f),
                "Opening reset leaves preferences intact");
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            check(selected != null && selected.GetComponent<RayBanTile>().ActionId == "settings.reset.keep", "Reset defaults to Keep settings");
            checkText("Reset confirmation explanation fits");
            press("settings.reset.keep");
            check(settings.ReducedMotion && settings.StrongContrast && Mathf.Approximately(settings.Brightness, .65f), "Keep settings cancels reset");
            press("settings.reset"); press("settings.reset.confirm");
            check(screen.CurrentView == "settings" && settings.Volume == 1f && settings.Brightness == 1f &&
                !settings.ReducedMotion && !settings.StrongContrast, "Confirmed reset restores four display defaults");
            check(keys.All(key => !PlayerPrefs.HasKey(key)) && PlayerPrefs.GetString(sentinel) == "keep",
                "Reset clears only the intended preference keys");
            check(GalleryDataManager.SavedPhotos.SequenceEqual(photosBefore) && RayBanLocalNotifications.Store.Revision == activityRevision &&
                RayBanLocalNotifications.Store.DoNotDisturb == quietBefore &&
                PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialReadKey, -1) == tutorialBefore &&
                PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialDismissedKey, -1) == welcomeBefore &&
                PlayerPrefs.GetString("ATLASVRQuadGames.Display.LauncherOrder.v1", "") == appOrderBefore,
                "Reset preserves photos, activity, quiet mode, tutorial progress and app order");
        }
        finally
        {
            PlayerPrefs.SetFloat(keys[0], volumeBefore); PlayerPrefs.SetFloat(keys[1], brightnessBefore);
            PlayerPrefs.SetInt(keys[2], motionBefore); PlayerPrefs.SetInt(keys[3], contrastBefore);
            for (int i = 0; i < keys.Length; i++) if (!existed[i]) PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.DeleteKey(sentinel); PlayerPrefs.Save();
            settings.Deactivate(); settings.Activate(shell.VisualRoot);
            AudioListener.volume = listenerBefore;
            shell.OpenApp("Settings");
            string directory = "Diagnostics/2026-11-08-DisplayCompletion";
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "settings-checks.json"), JsonUtility.ToJson(report, true));
        }
        return "Display settings checks: " + report.passed + " passed; " + report.failed + " failed.";
    }
}
#endif
