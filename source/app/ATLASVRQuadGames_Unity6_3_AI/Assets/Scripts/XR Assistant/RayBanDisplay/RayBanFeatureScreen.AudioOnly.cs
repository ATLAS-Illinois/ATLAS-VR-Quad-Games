using TMPro;
using UnityEngine;

public partial class RayBanFeatureScreen
{
    [SerializeField] private TMP_Text audioOnlyPlayback;
    private float nextAudioOnlyRefresh;

    /// <summary>Add only the app-owned audio-only presentation to older authored trees.</summary>
    public void EnsureAudioOnlyScreen()
    {
        if (views.Exists(view => view.key == "audio only")) return;
        var page = Page("audio only", "Audio only");
        var card = Panel(page, "Audio only information", 20, 100, 560, 360);
        Glyph(card.transform, "Audio only symbol", "headphones", 28, 26, 64, 64, new Color(.51f, .8f, 1f));
        Text(card.transform, "Audio only title", "Keep listening", 116, 34, 416, 48, 30, true);
        Text(card.transform, "Audio only scope",
            "Hide the display and keep listening. The campus stays visible.\n\nPress Menu to reopen. Leaving Assistant or Captions stops microphone capture.",
            28, 116, 504, 176, 23);
        audioOnlyPlayback = Text(card.transform, "Audio only playback", "", 28, 302, 504, 40, 21,
            color: new Color(.81f, .88f, .94f));
        Button(page, "Open music controls", "audioonly.music", "Open Music", "music", 20, 500, 274, 80, 1, 0);
        Button(page, "Hide display for audio", "audioonly.hide", "Hide display", "audioonly", 306, 500, 274, 80, 1, 1);
        RefreshAudioOnly();
        page.gameObject.SetActive(false);
    }

    private bool HandleAudioOnly(string action)
    {
        if (action == "audioonly.music")
        {
            shell.OpenApp("Music"); // The existing Music lease never auto-plays.
            return true;
        }
        if (action != "audioonly.hide") return false;
        // Hide only the existing display. Its OnDisable releases voice/camera
        // leases, while the music player and the campus retain their ownership.
        if (shell.router != null) shell.router.CloseEntireUI();
        else shell.Hide();
        return true;
    }

    private void RefreshAudioOnly()
    {
        nextAudioOnlyRefresh = Time.unscaledTime + .5f;
        if (audioOnlyPlayback == null) return;
        bool live = shell != null && shell.UsesLiveFeatures;
        var music = live ? shell.musicBridge : null;
        if (music != null && music.IsPlaying)
        {
            var track = music.CurrentTrack;
            audioOnlyPlayback.text = "Playing · " + (track != null && !string.IsNullOrEmpty(track.title) ? track.title : "Music");
        }
        else if (!live) audioOnlyPlayback.text = "Music status appears in the running app";
        else if (music == null || !music.IsAvailable) audioOnlyPlayback.text = "No music available";
        else audioOnlyPlayback.text = "Music paused · Open Music to play";
    }
}
