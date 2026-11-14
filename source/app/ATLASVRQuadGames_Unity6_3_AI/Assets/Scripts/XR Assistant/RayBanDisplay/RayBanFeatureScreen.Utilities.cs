using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class RayBanFeatureScreen
{
    [SerializeField] private TMP_Text mapInstruction, mapDistance, mapStatus;
    [SerializeField] private TMP_Text volumeValue, brightnessValue, settingsSummary, unavailableDescription;
    [SerializeField] private TMP_Text notificationSender, notificationMessage;
    [SerializeField] private RayBanGlyph mapTurn;
    [SerializeField] private RectTransform mapMarker;
    [SerializeField] private Image volumeFill, brightnessFill;
    [SerializeField] private int mapStep;
    private float previewVolume = 1f, previewBrightness = 1f;
    [SerializeField] private bool mapRunning;

    private void BuildUtilities()
    {
        var map = Page("maps", "Maps");
        BuildLiveNavigation(map);

        BuildTutorials();
        EnsureAudioOnlyScreen();
        EnsureExternalAppScreens();

        Page("settings", "App settings");
        EnsureExtendedSettings();
        BuildAdjustment("volume", "App volume", "volume", "Game audio, music and speech", out volumeValue, out volumeFill);
        BuildAdjustment("brightness", "Display brightness", "brightness", "Interface only · 25–100%", out brightnessValue, out brightnessFill);

        var unavailable = Page("unavailable", "Preview");
        Glyph(unavailable, "Not connected symbol", "settings", 252, 116, 96, 96);
        unavailableDescription = Text(unavailable, "Not connected message", "This control is not connected yet.", 44, 268, 512, 168, 28);
        unavailableDescription.alignment = TextAlignmentOptions.Center;
        RefreshSettings();

        var notification = Page("notifications", "Notification");
        var note = Panel(notification, "Sample notification card", 20, 110, 560, 348);
        Glyph(note.transform, "Notification icon", "messages", 28, 32, 56, 56);
        notificationSender = Text(note.transform, "Sender", "", 108, 34, 420, 48, 28, true);
        notificationMessage = Text(note.transform, "Notification body", "", 30, 132, 500, 144, 30);
        Text(note.transform, "Notification demo note", "Sample notification", 30, 296, 500, 32, 20);
        Button(notification, "Close notification", "notification.done", "Done", "check", 20, 500, 560, 80, 1, 0);
        EnsureLocalNotificationScreens();
    }

    private void HandleUtilities(string action)
    {
        if (shell != null && shell.UsesLiveFeatures && HandleLiveNavigation(action)) return;
        if (HandleTutorials(action)) return;
        if (HandleAudioOnly(action)) return;
        if (HandleExternalApps(action)) return;
        switch (action)
        {
            case "settings.volume": settingsParent = true; ShowView("volume"); break;
            case "settings.brightness": settingsParent = true; ShowView("brightness"); break;
            case "volume.less": AdjustSetting(true, -.1f); break;
            case "volume.more": AdjustSetting(true, .1f); break;
            case "brightness.less": AdjustSetting(false, -.1f); break;
            case "brightness.more": AdjustSetting(false, .1f); break;
            case "notification.done": shell.ReturnToLauncher(); break;
        }
    }

    private bool settingsParent;

    private void BuildAdjustment(string key, string title, string icon, string note, out TMP_Text value, out Image fill)
    {
        var page = Page(key, title);
        var card = Panel(page, title + " card", 20, 100, 560, 360);
        Glyph(card.transform, "Adjustment icon", icon, 232, 28, 96, 96);
        value = Text(card.transform, "Level", "100%", 30, 150, 500, 60, 44, true);
        value.alignment = TextAlignmentOptions.Center;
        Bar(card.transform, "Level track", 50, 250, 460, 8, new Color(1,1,1,.22f));
        fill = Bar(card.transform, "Level amount", 50, 250, 460, 8, new Color(.48f,.8f,1));
        Text(card.transform, "Adjustment scope", note, 30, 292, 500, 40, 22).alignment = TextAlignmentOptions.Center;
        Button(page, "Decrease", key + ".less", "Decrease", "minus", 20, 500, 274, 80, 1, 0);
        Button(page, "Increase", key + ".more", "Increase", "plus", 306, 500, 274, 80, 1, 1);
    }

    private void AdjustSetting(bool volume, float delta)
    {
        if (shell.UsesLiveFeatures && shell.appSettings != null)
        {
            if (volume) shell.appSettings.SetVolume(shell.appSettings.Volume + delta);
            else shell.appSettings.SetBrightness(shell.appSettings.Brightness + delta);
        }
        else if (volume) previewVolume = Mathf.Clamp01(previewVolume + delta);
        else previewBrightness = Mathf.Clamp(previewBrightness + delta, RayBanAppSettings.MinimumBrightness, 1f);
        RefreshSettings();
    }

    private void RefreshSettings()
    {
        bool live = shell != null && shell.UsesLiveFeatures && shell.appSettings != null;
        float volume = live ? shell.appSettings.Volume : previewVolume;
        float brightness = live ? shell.appSettings.Brightness : previewBrightness;
        if (volumeValue) volumeValue.text = Mathf.RoundToInt(volume * 100) + "%";
        if (brightnessValue) brightnessValue.text = Mathf.RoundToInt(brightness * 100) + "%";
        if (volumeFill) volumeFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 460 * volume);
        if (brightnessFill) brightnessFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 460 * brightness);
        if (settingsSummary) settingsSummary.text = "Volume  " + Mathf.RoundToInt(volume * 100) + "%\nBrightness  " + Mathf.RoundToInt(brightness * 100) + "%";
        RefreshExtendedSettings();
    }
}
