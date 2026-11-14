using System;
using TMPro;
using UnityEngine;

public partial class RayBanFeatureScreen
{
    private const int ExtendedSettingsVersion = 1;
    [SerializeField] private int extendedSettingsVersion;
    [SerializeField] private TMP_Text accessibilityStatus, resetDisplayStatus;
    private bool previewReducedMotion, previewStrongContrast;

    public static bool IsExtendedSettingsView(string key) =>
        key == "settings.accessibility" || key == "settings.reset";

    /// <summary>Upgrade only settings-owned content; keep existing page identity and input wiring.</summary>
    public void EnsureExtendedSettings()
    {
        if (extendedSettingsVersion == ExtendedSettingsVersion && accessibilityStatus != null && resetDisplayStatus != null)
        { RefreshExtendedSettings(); return; }
        var settings = views.Find(view => view.key == "settings");
        if (settings == null || settings.root == null) return;
        ClearSettingsPageContent(settings.root);
        settings.heading.text = "App settings";
        settingsSummary = Text(settings.root, "Saved settings summary", "", 28, 84, 544, 36, 20);
        settingsSummary.alignment = TextAlignmentOptions.Center;
        Button(settings.root, "App volume", "settings.volume", "Volume", "volume", 20, 130, 560, 58, 1, 0);
        Button(settings.root, "Interface brightness", "settings.brightness", "Brightness", "brightness", 20, 194, 560, 58, 2, 0);
        Button(settings.root, "Accessibility preferences", "settings.accessibility", "Accessibility", "settings", 20, 258, 560, 58, 3, 0);
        Button(settings.root, "App order", "settings_launcher", "App order", "next", 20, 322, 560, 58, 4, 0);
        Button(settings.root, "App activity settings", "settings.activity", "App activity", "bell", 20, 386, 560, 58, 5, 0);
        Button(settings.root, "Replay welcome guide", "settings.welcome", "Replay welcome guide", "tutorials", 20, 450, 560, 58, 6, 0);
        Button(settings.root, "Reset display preferences", "settings.reset", "Reset display preferences", "settings", 20, 514, 560, 58, 7, 0);

        var accessibility = SettingsPage("settings.accessibility", "Accessibility");
        var card = Panel(accessibility, "Accessibility information", 20, 100, 560, 194);
        Text(card.transform, "Reduced motion explanation", "Reduced motion keeps focus still.\nStronger contrast makes cards and outlines clearer.", 26, 24, 508, 118, 25);
        accessibilityStatus = Text(card.transform, "Accessibility saved status", "Saved on this device", 26, 150, 508, 32, 19);
        Button(accessibility, "Reduced motion preference", "settings.motion.toggle", "Reduced motion: Off", "check", 20, 318, 560, 90, 1, 0);
        Button(accessibility, "Stronger contrast preference", "settings.contrast.toggle", "Stronger contrast: Off", "brightness", 20, 426, 560, 90, 2, 0);
        Text(accessibility, "Accessibility scope", "Applies to the Assistant display only.", 30, 542, 540, 32, 20).alignment = TextAlignmentOptions.Center;

        var reset = SettingsPage("settings.reset", "Reset display settings?");
        Text(reset, "Reset display explanation", "Restore volume and brightness to 100%, and turn off Reduced motion and Stronger contrast.\n\nPhotos, activity, app order, tutorials and game progress are kept.", 36, 132, 528, 264, 26);
        resetDisplayStatus = Text(reset, "Reset display status", "", 36, 418, 528, 42, 22);
        resetDisplayStatus.alignment = TextAlignmentOptions.Center;
        Button(reset, "Keep display preferences", "settings.reset.keep", "Keep settings", "back", 20, 500, 274, 80, 1, 0);
        Button(reset, "Confirm display preferences reset", "settings.reset.confirm", "Reset display", "check", 306, 500, 274, 80, 1, 1);
        accessibility.gameObject.SetActive(false);
        reset.gameObject.SetActive(false);
        extendedSettingsVersion = ExtendedSettingsVersion;
        RefreshSettings();
        if (shell != null && shell.UsesLiveFeatures && shell.appSettings != null)
            shell.appSettings.BindVisuals(shell.VisualRoot);
    }

    private RectTransform SettingsPage(string key, string title)
    {
        var existing = views.Find(view => view.key == key);
        if (existing == null || existing.root == null) return Page(key, title);
        ClearSettingsPageContent(existing.root);
        existing.heading.text = title;
        return existing.root;
    }

    private void ClearSettingsPageContent(Transform page)
    {
        for (int i = page.childCount - 1; i >= 0; i--)
        {
            var child = page.GetChild(i);
            if (child.name == "Heading" || child.name == "Back" || child.name == "Demo badge") continue;
            controls.RemoveAll(control => control == null || control.transform.IsChildOf(child));
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
    }

    public bool HandleExtendedSettings(string action)
    {
        if (renderingCopy || string.IsNullOrEmpty(action)) return false;
        var settings = shell != null && shell.UsesLiveFeatures ? shell.appSettings : null;
        switch (action)
        {
            case "settings.accessibility": ShowView("settings.accessibility"); break;
            case "settings_launcher": ShowView("launcher_settings"); break;
            case "settings.activity": ShowView("notifications"); break;
            case "settings.welcome":
                shell.OpenApp("Tutorials");
                BeginFirstRunTutorial();
                return true;
            case "settings.motion.toggle":
                if (settings != null) settings.SetReducedMotion(!settings.ReducedMotion);
                else previewReducedMotion = !previewReducedMotion;
                break;
            case "settings.contrast.toggle":
                if (settings != null) settings.SetStrongContrast(!settings.StrongContrast);
                else previewStrongContrast = !previewStrongContrast;
                break;
            case "settings.reset":
                if (resetDisplayStatus != null) resetDisplayStatus.text = "";
                ShowView("settings.reset");
                FocusAction("settings.reset.keep");
                return true;
            case "settings.reset.keep": ShowView("settings"); return true;
            case "settings.reset.confirm":
                if (settings != null) settings.ResetDisplayPreferences();
                else { previewVolume = previewBrightness = 1f; previewReducedMotion = previewStrongContrast = false; }
                ShowView("settings");
                return true;
            default: return false;
        }
        RefreshSettings();
        return true;
    }

    public bool HandleExtendedSettingsBack()
    {
        if (currentView == "do not disturb" && shell != null &&
            string.Equals(shell.CurrentDetail, "Settings", StringComparison.OrdinalIgnoreCase))
        {
            ShowView("notifications");
            return true;
        }
        if (IsExtendedSettingsView(currentView) || (currentView == "notifications" && shell != null &&
            string.Equals(shell.CurrentDetail, "Settings", StringComparison.OrdinalIgnoreCase)))
        {
            ShowView("settings");
            return true;
        }
        return false;
    }

    public void RefreshExtendedSettings()
    {
        var settings = shell != null && shell.UsesLiveFeatures ? shell.appSettings : null;
        bool motion = settings != null ? settings.ReducedMotion : previewReducedMotion;
        bool contrast = settings != null ? settings.StrongContrast : previewStrongContrast;
        SetButtonLabel("settings.motion.toggle", "Reduced motion: " + (motion ? "On" : "Off"));
        SetButtonLabel("settings.contrast.toggle", "Stronger contrast: " + (contrast ? "On" : "Off"));
        if (accessibilityStatus != null)
            accessibilityStatus.text = settings != null ? "Saved on this device" : "Preview · not saved";
        if (extendedSettingsVersion == ExtendedSettingsVersion && settingsSummary != null)
        {
            float volume = settings != null ? settings.Volume : previewVolume;
            float brightness = settings != null ? settings.Brightness : previewBrightness;
            settingsSummary.text = "Volume " + Mathf.RoundToInt(volume * 100) + "%  ·  Brightness " + Mathf.RoundToInt(brightness * 100) + "%";
        }
    }
}
