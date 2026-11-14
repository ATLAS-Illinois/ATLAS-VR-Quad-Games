using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class RayBanFeatureScreen
{
    // Original native compositions informed by the public toolkit's CarouselPage,
    // CardPage, MediaWrapperPage, ButtonRailPage and ScrubberPage examples.
    // Live adapters supply gameplay capture, a local album and the existing music player.
    [SerializeField] private RawImage mediaCameraImage;
    [SerializeField] private TMP_Text mediaCameraStatus;
    [SerializeField] private TMP_Text mediaCameraZoom;
    [SerializeField] private Rect mediaCameraBaseUv = new Rect(0, 0, 1, 1);
    [SerializeField] private int mediaCaptureCount;
    [SerializeField, Range(1f, 3f)] private float mediaZoom = 1f;
    [SerializeField] private RawImage mediaPhotoImage;
    [SerializeField] private TMP_Text mediaPhotoTitle;
    [SerializeField] private TMP_Text mediaPhotoSubtitle;
    [SerializeField] private TMP_Text mediaPhotoPosition;
    [SerializeField] private int mediaPhotoIndex;
    [SerializeField] private RawImage mediaAlbumImage;
    [SerializeField] private TMP_Text mediaTrackTitle;
    [SerializeField] private TMP_Text mediaTrackArtist;
    [SerializeField] private TMP_Text mediaElapsed;
    [SerializeField] private TMP_Text mediaDuration;
    [SerializeField] private TMP_Text mediaPlaybackStatus;
    [SerializeField] private Image mediaProgressFill;
    [SerializeField] private RayBanSurface mediaProgressThumb;
    [SerializeField] private int mediaTrackIndex;
    [SerializeField] private bool mediaPlaying;
    [SerializeField] private float mediaElapsedSeconds = 72f;
    [SerializeField] private int mediaDisplayedSecond = -1;

    private static readonly string[] MediaPhotoTitles = { "Coastal sunrise", "Forest light", "Above the clouds" };
    private static readonly string[] MediaPhotoDetails = { "Coast", "Forest", "Mountain overlook" };
    private static readonly string[] MediaTrackTitles = { "A little farther", "The quiet hours", "Open skies" };
    private static readonly string[] MediaTrackArtists = { "Sample track · Coast", "Sample track · Forest", "Sample track · Overlook" };
    private static readonly int[] MediaTrackDurations = { 218, 194, 242 };
    private static readonly Color MediaSecondary = new Color(.81f, .83f, .86f, 1f);
    private static readonly Color MediaPink = new Color(.97f, .35f, .62f, 1f);
    private static readonly Color MediaPurple = new Color(.58f, .42f, .96f, 1f);

    private void BuildMedia()
    {
        BuildMediaCamera();
        BuildMediaPhotos();
        BuildMediaMusic();
    }

    private void BuildMediaCamera()
    {
        var page = Page("camera", "Camera");
        mediaCameraImage = Picture(page, "Sample viewfinder", 0, 20, 90, 560, 386);
        mediaCameraBaseUv = mediaCameraImage.uvRect;

        // Discreet framing brackets remain visible without a heavy viewfinder border.
        MediaCorner(page, "Top left", 42, 112, false, false);
        MediaCorner(page, "Top right", 558, 112, true, false);
        MediaCorner(page, "Bottom left", 42, 454, false, true);
        MediaCorner(page, "Bottom right", 558, 454, true, true);
        var zoomPill = Panel(page, "Zoom badge", 474, 106, 72, 42,
            new Color(.04f, .06f, .09f, .72f), 21);
        zoomPill.Flat = true;
        mediaCameraZoom = Text(page, "Zoom value", "1x", 474, 106, 72, 42, 22, true);
        mediaCameraZoom.alignment = TextAlignmentOptions.Center;
        var statusPill = Panel(page, "Viewfinder status", 54, 415, 432, 40,
            new Color(.025f, .04f, .06f, .8f), 20);
        statusPill.Flat = true;
        mediaCameraStatus = Text(page, "Capture status", "", 70, 420, 406, 30, 20);

        Button(page, "Browse samples", "camera.photos", "Photos", "photos", 20, 500, 148, 80, 1, 0,
            accent: MediaPink);
        Button(page, "Sample shutter", "camera.capture", "Capture", "camera", 180, 500, 240, 80, 1, 1,
            accent: MediaPink);
        Button(page, "Change zoom", "camera.zoom", "Zoom", "plus", 432, 500, 148, 80, 1, 2,
            accent: MediaPink);
        RefreshMediaCamera();
    }

    private void MediaCorner(RectTransform parent, string name, float x, float y, bool right, bool bottom)
    {
        var white = new Color(1, 1, 1, .83f);
        Bar(parent, name + " horizontal", right ? x - 25 : x, bottom ? y - 3 : y, 25, 3, white);
        Bar(parent, name + " vertical", right ? x - 3 : x, bottom ? y - 25 : y, 3, 25, white);
    }

    private void BuildMediaPhotos()
    {
        var page = Page("photos", "Photos");
        mediaPhotoImage = Picture(page, "Current sample photo", mediaPhotoIndex, 20, 90, 560, 310);
        mediaPhotoTitle = Text(page, "Photo title", "", 28, 408, 544, 48, 30, true);
        mediaPhotoSubtitle = Text(page, "Photo details", "", 28, 458, 544, 30, 21, color: MediaSecondary);
        Button(page, "Previous photo", "photos.previous", "Previous", "previous", 20, 500, 182, 80, 1, 0,
            accent: MediaPink);
        mediaPhotoPosition = Text(page, "Photo position", "", 212, 516, 176, 48, 24, true);
        mediaPhotoPosition.alignment = TextAlignmentOptions.Center;
        Button(page, "Next photo", "photos.next", "Next", "next", 398, 500, 182, 80, 1, 2,
            accent: MediaPink);
        RefreshMediaPhotos();
    }

    private void BuildMediaMusic()
    {
        var page = Page("music", "Music");
        mediaAlbumImage = Picture(page, "Sample album artwork", mediaTrackIndex, 190, 88, 220, 196);
        var badge = Panel(page, "Artwork music badge", 360, 232, 64, 64, Color.white, 32);
        badge.Tint = MediaPurple;
        badge.Circle = true;
        Glyph(page, "Artwork music mark", "music", 375, 247, 34, 34);
        mediaTrackTitle = Text(page, "Track title", "", 24, 298, 552, 52, 32, true);
        mediaTrackTitle.alignment = TextAlignmentOptions.Center;
        mediaTrackArtist = Text(page, "Track artist", "", 24, 352, 552, 34, 23, color: MediaSecondary);
        mediaTrackArtist.alignment = TextAlignmentOptions.Center;
        Bar(page, "Timeline background", 64, 410, 472, 5, new Color(1, 1, 1, .23f));
        mediaProgressFill = Bar(page, "Timeline elapsed", 64, 410, 1, 5, Color.white);
        mediaProgressThumb = Panel(page, "Timeline position", 64, 405, 15, 15, Color.white, 8);
        mediaProgressThumb.Circle = true;
        mediaProgressThumb.Flat = true;
        mediaElapsed = Text(page, "Elapsed time", "", 64, 428, 104, 28, 20, color: MediaSecondary);
        mediaDuration = Text(page, "Track duration", "", 432, 428, 104, 28, 20, color: MediaSecondary);
        mediaDuration.alignment = TextAlignmentOptions.TopRight;
        mediaPlaybackStatus = Text(page, "Playback preview status", "", 24, 466, 552, 26, 19, color: MediaSecondary);
        mediaPlaybackStatus.alignment = TextAlignmentOptions.Center;
        Button(page, "Previous track", "music.previous", "Previous", "previous", 20, 500, 160, 80, 1, 0,
            accent: MediaPurple);
        Button(page, "Toggle playback", "music.toggle", "Play", "play", 192, 500, 216, 80, 1, 1,
            accent: MediaPurple);
        Button(page, "Next track", "music.next", "Next", "next", 420, 500, 160, 80, 1, 2,
            accent: MediaPurple);
        RefreshMediaMusic();
    }

    private bool HandleMedia(string action)
    {
        if (shell.UsesLiveFeatures) return HandleLiveMedia(action);
        switch (action)
        {
            case "camera.capture":
                mediaCaptureCount++;
                RefreshMediaCamera();
                return true;
            case "camera.zoom":
                AdvanceCameraZoom();
                RefreshMediaCamera();
                return true;
            case "camera.photos":
                ShowView("photos");
                FocusAction("photos.next");
                return true;
            case "photos.previous":
                mediaPhotoIndex = (mediaPhotoIndex + MediaPhotoCount - 1) % MediaPhotoCount;
                RefreshMediaPhotos();
                return true;
            case "photos.next":
                mediaPhotoIndex = (mediaPhotoIndex + 1) % MediaPhotoCount;
                RefreshMediaPhotos();
                return true;
            case "music.previous":
                mediaTrackIndex = (mediaTrackIndex + MediaTrackTitles.Length - 1) % MediaTrackTitles.Length;
                mediaElapsedSeconds = 0;
                RefreshMediaMusic();
                return true;
            case "music.next":
                mediaTrackIndex = (mediaTrackIndex + 1) % MediaTrackTitles.Length;
                mediaElapsedSeconds = 0;
                RefreshMediaMusic();
                return true;
            case "music.toggle":
                mediaPlaying = !mediaPlaying;
                if (mediaPlaying && mediaElapsedSeconds >= MediaTrackDurations[mediaTrackIndex])
                    mediaElapsedSeconds = 0;
                RefreshMediaMusic();
                return true;
            default:
                return false;
        }
    }

    private int MediaPhotoCount => samplePhotos != null && samplePhotos.Length > 0 ? samplePhotos.Length : 1;

    private void RefreshMediaCamera()
    {
        if (shell.UsesLiveFeatures) { RefreshLiveCamera(); return; }
        mediaZoom = Mathf.Clamp(mediaZoom, 1f, 3f);
        if (mediaCameraImage != null)
        {
            Vector2 size = mediaCameraBaseUv.size / mediaZoom;
            mediaCameraImage.uvRect = new Rect(mediaCameraBaseUv.center - size * .5f, size);
        }
        if (mediaCameraZoom != null) mediaCameraZoom.text = mediaZoom.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "x";
        if (mediaCameraStatus != null)
            mediaCameraStatus.text = mediaCaptureCount == 0
                ? "Sample view · nothing is saved"
                : "Sample capture " + mediaCaptureCount + " · nothing saved";
    }

    private void AdvanceCameraZoom()
    {
        // Preserve the button's 1x/2x/3x stops after a continuous twist.
        mediaZoom = mediaZoom >= 2.99f ? 1f : Mathf.Min(3f, Mathf.Floor(mediaZoom + .001f) + 1f);
    }

    private void RefreshMediaPhotos()
    {
        if (shell.UsesLiveFeatures) { RefreshLivePhotos(); return; }
        mediaPhotoIndex = Mathf.Clamp(mediaPhotoIndex, 0, MediaPhotoCount - 1);
        int titleIndex = mediaPhotoIndex % MediaPhotoTitles.Length;
        if (mediaPhotoImage != null) SetPhoto(mediaPhotoImage, mediaPhotoIndex);
        if (mediaPhotoTitle != null) mediaPhotoTitle.text = MediaPhotoTitles[titleIndex];
        if (mediaPhotoSubtitle != null) mediaPhotoSubtitle.text = "Oct 18 · " + MediaPhotoDetails[titleIndex] + " · Sample date";
        if (mediaPhotoPosition != null) mediaPhotoPosition.text = (mediaPhotoIndex + 1) + " of " + MediaPhotoCount;
        SetControlAvailable("photos.delete", false); // Preview images are never written or deleted.
    }

    private void RefreshMediaMusic()
    {
        if (shell.UsesLiveFeatures) { RefreshLiveMusic(); return; }
        mediaTrackIndex = Mathf.Clamp(mediaTrackIndex, 0, MediaTrackTitles.Length - 1);
        int duration = MediaTrackDurations[mediaTrackIndex];
        mediaElapsedSeconds = Mathf.Clamp(mediaElapsedSeconds, 0, duration);
        if (mediaAlbumImage != null) SetPhoto(mediaAlbumImage, mediaTrackIndex % MediaPhotoCount);
        if (mediaTrackTitle != null) mediaTrackTitle.text = MediaTrackTitles[mediaTrackIndex];
        if (mediaTrackArtist != null) mediaTrackArtist.text = MediaTrackArtists[mediaTrackIndex];
        mediaDisplayedSecond = -1;
        if (mediaDuration != null) mediaDuration.text = MediaTimestamp(duration);
        RefreshMediaTimeline();
        if (mediaPlaybackStatus != null)
            mediaPlaybackStatus.text = (mediaPlaying ? "Playing" : "Paused") + " preview · no audio";
        SetButtonLabel("music.toggle", mediaPlaying ? "Pause" : "Play");
        SetButtonSymbol("music.toggle", mediaPlaying ? "pause" : "play");
    }

    // Called by the shared Update. Time advances only for this visible sample;
    // the real music feature, AudioSource components and clip playback are untouched.
    private void TickMedia()
    {
        if (shell != null && shell.UsesLiveFeatures) { TickLiveMedia(); return; }
        if (!Application.isPlaying || !isActiveAndEnabled || CurrentView != "music" || !mediaPlaying) return;
        mediaTrackIndex = Mathf.Clamp(mediaTrackIndex, 0, MediaTrackTitles.Length - 1);
        int duration = MediaTrackDurations[mediaTrackIndex];
        mediaElapsedSeconds = Mathf.Min(duration, mediaElapsedSeconds + Time.unscaledDeltaTime);
        if (mediaElapsedSeconds >= duration)
        {
            if (previewRepeatMode == MusicRepeatMode.One) mediaElapsedSeconds = 0f;
            else if (previewRepeatMode == MusicRepeatMode.All || mediaTrackIndex + 1 < MediaTrackTitles.Length)
            { mediaTrackIndex = (mediaTrackIndex + 1) % MediaTrackTitles.Length; mediaElapsedSeconds = 0f; }
            else mediaPlaying = false;
            RefreshMediaMusic();
            return;
        }
        RefreshMediaTimeline();
    }

    private void RefreshMediaTimeline()
    {
        int second = Mathf.FloorToInt(mediaElapsedSeconds);
        if (second != mediaDisplayedSecond)
        {
            mediaDisplayedSecond = second;
            if (mediaElapsed != null) mediaElapsed.text = MediaTimestamp(mediaElapsedSeconds);
        }
        float width = 472 * mediaElapsedSeconds / MediaTrackDurations[mediaTrackIndex];
        if (mediaProgressFill != null) mediaProgressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        if (mediaProgressThumb != null)
        {
            var position = mediaProgressThumb.rectTransform.anchoredPosition;
            position.x = 64 + width - 7.5f;
            mediaProgressThumb.rectTransform.anchoredPosition = position;
        }
    }

    private static string MediaTimestamp(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60) + ":" + (total % 60).ToString("00");
    }
}
