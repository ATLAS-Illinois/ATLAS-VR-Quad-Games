using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class RayBanFeatureScreen
{
    [SerializeField] private TMP_Text extendedMusicTrack, extendedMusicStatus, extendedTrackPage;
    [SerializeField] private RawImage extendedPhotoImage;
    [SerializeField] private TMP_Text extendedPhotoDetails;
    [SerializeField] private int musicTrackPage;
    private float photoViewZoom = 1f, previewMusicVolume = 1f;
    private MusicRepeatMode previewRepeatMode = MusicRepeatMode.All;
    private Texture inspectedPhoto;
    private float nextExtendedMediaRefresh;
    private const int TracksPerPage = 4;
    private static readonly string[] ExtendedMusicActions = { "music.seek.back", "music.seek.forward", "music.volume.less", "music.volume.more", "music.repeat", "music.tracks" };

    public void EnsureExtendedMedia()
    {
        EnsureMediaEntry("music", "Music controls", "music.controls", "Controls", "settings");
        EnsureMediaEntry("camera", "Camera timer", "camera.timer", "3s timer", "camera");
        EnsureMediaEntry("photos", "Inspect photo", "photos.view", "View", "photos");
        if (!views.Exists(v => v.key == "music.controls"))
        {
            var page = Page("music.controls", "Music controls");
            extendedMusicTrack = Text(page, "Current track", "", 28, 90, 544, 58, 26, true);
            Button(page, "Rewind ten seconds", "music.seek.back", "Back 10s", "previous", 20, 160, 274, 74, 1, 0, accent: MediaPurple);
            Button(page, "Forward ten seconds", "music.seek.forward", "Forward 10s", "next", 306, 160, 274, 74, 1, 1, accent: MediaPurple);
            Button(page, "Lower music volume", "music.volume.less", "Volume −", "minus", 20, 248, 274, 74, 2, 0, accent: MediaPurple);
            Button(page, "Raise music volume", "music.volume.more", "Volume +", "plus", 306, 248, 274, 74, 2, 1, accent: MediaPurple);
            Button(page, "Repeat mode", "music.repeat", "Repeat all", "music", 20, 336, 560, 72, 3, 0, accent: MediaPurple);
            Button(page, "Choose track", "music.tracks", "Choose track", "music", 20, 424, 560, 72, 4, 0, accent: MediaPurple);
            extendedMusicStatus = Text(page, "Playback details", "", 28, 514, 544, 65, 22, color: MediaSecondary);
            page.gameObject.SetActive(false);
        }
        if (!views.Exists(v => v.key == "music.tracks"))
        {
            var page = Page("music.tracks", "Choose track");
            for (int i = 0; i < TracksPerPage; i++)
                Button(page, "Track " + i, "music.select." + i, "", "music", 20, 92 + i * 90, 560, 78, i + 1, 0, accent: MediaPurple);
            Button(page, "Previous tracks", "music.tracks.previous", "Previous", "previous", 20, 500, 182, 80, 5, 0, accent: MediaPurple);
            extendedTrackPage = Text(page, "Track page", "", 210, 520, 180, 44, 23);
            extendedTrackPage.alignment = TextAlignmentOptions.Center;
            Button(page, "Next tracks", "music.tracks.next", "Next", "next", 398, 500, 182, 80, 5, 2, accent: MediaPurple);
            page.gameObject.SetActive(false);
        }
        if (!views.Exists(v => v.key == "photos.view"))
        {
            var page = Page("photos.view", "View photo");
            extendedPhotoImage = Picture(page, "Full photo", 0, 20, 90, 560, 324);
            extendedPhotoDetails = Text(page, "Photo details", "", 28, 426, 544, 62, 22, color: MediaSecondary);
            Button(page, "Zoom photo out", "photos.zoom.less", "Zoom −", "minus", 20, 500, 182, 80, 1, 0, accent: MediaPink);
            Button(page, "Reset photo zoom", "photos.zoom.reset", "Reset", "photos", 210, 500, 180, 80, 1, 1, accent: MediaPink);
            Button(page, "Zoom photo in", "photos.zoom.more", "Zoom +", "plus", 398, 500, 182, 80, 1, 2, accent: MediaPink);
            page.gameObject.SetActive(false);
        }
    }

    private void EnsureMediaEntry(string key, string name, string action, string title, string symbol)
    {
        var view = views.Find(v => v.key == key);
        if (view == null || view.root == null || controls.Exists(t => t && t.ActionId == action)) return;
        Button(view.root, name, action, title, symbol, 426, 18, 154, 52, 0, 4, accent: key == "music" ? MediaPurple : MediaPink);
        if (view.heading) view.heading.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 222);
        var badge = view.root.Find("Demo badge") as RectTransform;
        if (badge) badge.anchoredPosition = new Vector2(326, -31);
    }

    public bool HandleExtendedMedia(string action)
    {
        if (renderingCopy || string.IsNullOrEmpty(action)) return false;
        bool live = shell != null && shell.UsesLiveFeatures;
        var music = shell != null ? shell.musicBridge : null;
        var camera = shell != null ? shell.cameraBridge : null;
        if (action.StartsWith("music.select.", StringComparison.Ordinal))
        {
            if (!int.TryParse(action.Substring("music.select.".Length), out int slot) || slot < 0 || slot >= TracksPerPage) return true;
            int index = musicTrackPage * TracksPerPage + slot;
            if (live)
            {
                if (music == null || !music.SelectTrack(index)) return true;
            }
            else if (index >= 0 && index < MediaTrackTitles.Length)
            { mediaTrackIndex = index; mediaElapsedSeconds = 0; mediaPlaying = true; }
            else return true;
            ShowView("music");
            FocusAction("music.toggle");
            return true;
        }
        switch (action)
        {
            case "camera.timer":
                if (live && camera != null)
                {
                    if (camera.IsCountingDown) camera.CancelCountdown();
                    else camera.StartCountdown(mediaCameraImage ? mediaCameraImage.uvRect : new Rect(0, 0, 1, 1));
                    RefreshLiveCamera();
                }
                // Sample camera deliberately performs no delayed capture or save.
                else if (mediaCameraStatus) mediaCameraStatus.text = "3-second timer · available with gameplay camera";
                return true;
            case "music.controls": ShowView("music.controls"); RefreshExtendedMedia(true); FocusAction("music.seek.back"); return true;
            case "music.tracks":
                musicTrackPage = Mathf.Max(0, live && music != null ? music.CurrentIndex : mediaTrackIndex) / TracksPerPage;
                ShowView("music.tracks"); RefreshExtendedMedia(true); FocusAction("music.select.0"); return true;
            case "music.tracks.previous": musicTrackPage--; RefreshExtendedMedia(true); return true;
            case "music.tracks.next": musicTrackPage++; RefreshExtendedMedia(true); return true;
            case "music.seek.back":
            case "music.seek.forward":
                float delta = action == "music.seek.back" ? -10f : 10f;
                if (live) { if (music != null) music.SeekBy(delta); }
                else mediaElapsedSeconds = Mathf.Clamp(mediaElapsedSeconds + delta, 0f, MediaTrackDurations[mediaTrackIndex]);
                RefreshExtendedMedia(true); return true;
            case "music.volume.less":
            case "music.volume.more":
                float gain = action == "music.volume.less" ? -.1f : .1f;
                if (live) { if (music != null && music.Acquire()) music.SetVolume(music.Volume + gain); }
                else previewMusicVolume = Mathf.Clamp01(previewMusicVolume + gain);
                RefreshExtendedMedia(true); return true;
            case "music.repeat":
                if (live) { if (music != null) music.CycleRepeatMode(); }
                else previewRepeatMode = (MusicRepeatMode)(((int)previewRepeatMode + 1) % 3);
                RefreshExtendedMedia(true); return true;
            case "photos.view":
                inspectedPhoto = null; photoViewZoom = 1f;
                ShowView("photos.view"); RefreshExtendedMedia(true); FocusAction("photos.zoom.more"); return true;
            case "photos.zoom.less": photoViewZoom = Mathf.Max(1f, photoViewZoom - .5f); RefreshExtendedMedia(true); return true;
            case "photos.zoom.more": photoViewZoom = Mathf.Min(3f, photoViewZoom + .5f); RefreshExtendedMedia(true); return true;
            case "photos.zoom.reset": photoViewZoom = 1f; RefreshExtendedMedia(true); return true;
            default: return false;
        }
    }

    public bool HandleExtendedMediaBack()
    {
        if (CurrentView == "music.controls") { ShowView("music"); FocusAction("music.controls"); return true; }
        if (CurrentView == "music.tracks") { ShowView("music.controls"); RefreshExtendedMedia(true); FocusAction("music.tracks"); return true; }
        if (CurrentView == "photos.view") { ShowView("photos"); FocusAction("photos.view"); return true; }
        return false;
    }

    public void OnExtendedMediaViewChanging(string next)
    {
        if (next != "camera") CancelExtendedMedia();
        if (next != "photos.view")
        {
            inspectedPhoto = null;
            if (extendedPhotoImage) extendedPhotoImage.texture = null;
        }
        nextExtendedMediaRefresh = 0f;
    }

    public void CancelExtendedMedia()
    {
        // Preview canvases retain references to the live shell, but never own its timer.
        if (renderingCopy) return;
        if (shell != null && shell.cameraBridge != null) shell.cameraBridge.CancelCountdown();
    }

    public bool IsExtendedMediaView(string key) => key == "music.controls" || key == "music.tracks" || key == "photos.view";

    public void RefreshExtendedMedia() => RefreshExtendedMedia(false);

    private void RefreshExtendedMedia(bool force)
    {
        if (renderingCopy || !IsExtendedMediaView(CurrentView) || shell == null) return;
        if (!force && Time.unscaledTime < nextExtendedMediaRefresh) return;
        nextExtendedMediaRefresh = Time.unscaledTime + .15f;
        bool live = shell.UsesLiveFeatures;
        var music = shell.musicBridge;
        bool available = !live || music != null && music.IsAvailable;
        if (CurrentView == "music.controls")
        {
            var track = music != null ? music.CurrentTrack : null;
            if (extendedMusicTrack) extendedMusicTrack.text = live ? track != null ? track.title : "No music available" : MediaTrackTitles[mediaTrackIndex];
            MusicRepeatMode repeat = live && music != null ? music.RepeatMode : previewRepeatMode;
            SetButtonLabel("music.repeat", repeat == MusicRepeatMode.Off ? "Repeat off" : repeat == MusicRepeatMode.One ? "Repeat this track" : "Repeat all tracks");
            float volume = live && music != null ? music.Volume : previewMusicVolume;
            float elapsed = live && music != null ? music.Elapsed : mediaElapsedSeconds;
            float duration = live && music != null ? music.Duration : MediaTrackDurations[mediaTrackIndex];
            if (extendedMusicStatus) extendedMusicStatus.text = "Music volume " + Mathf.RoundToInt(volume * 100) + "% · " + MediaTimestamp(elapsed) + " / " + MediaTimestamp(duration) +
                (live ? "\nOn-device playlist" : "\nSample controls · no audio");
            foreach (string action in ExtendedMusicActions)
                SetControlAvailable(action, available);
            SetControlAvailable("music.volume.less", available && volume > .001f);
            SetControlAvailable("music.volume.more", available && volume < .999f);
        }
        else if (CurrentView == "music.tracks")
        {
            int count = live ? music != null ? music.TrackCount : 0 : MediaTrackTitles.Length;
            int pages = Mathf.Max(1, (count + TracksPerPage - 1) / TracksPerPage);
            musicTrackPage = Mathf.Clamp(musicTrackPage, 0, pages - 1);
            for (int i = 0; i < TracksPerPage; i++)
            {
                int index = musicTrackPage * TracksPerPage + i;
                var track = live && music != null ? music.GetTrack(index) : null;
                bool playable = index < count && (!live || track != null && track.clip != null);
                SetButtonLabel("music.select." + i, index >= count ? "—" : live ? track != null ? track.title : "Unavailable track" : MediaTrackTitles[index]);
                SetControlAvailable("music.select." + i, playable);
            }
            if (extendedTrackPage) extendedTrackPage.text = count == 0 ? "No tracks" : (musicTrackPage + 1) + " / " + pages;
            SetControlAvailable("music.tracks.previous", musicTrackPage > 0);
            SetControlAvailable("music.tracks.next", musicTrackPage + 1 < pages);
        }
        else RefreshInspectedPhoto(live);
        EnsureExtendedMediaFocus();
    }

    private void EnsureExtendedMediaFocus()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var tile = selected != null ? selected.GetComponent<RayBanTile>() : focused;
        if (tile == null || !tile.gameObject.activeInHierarchy || tile.IsInteractable()) return;
        string alternate = tile.ActionId == "music.volume.more" ? "music.volume.less" :
            tile.ActionId == "music.volume.less" ? "music.volume.more" :
            tile.ActionId == "photos.zoom.more" ? "photos.zoom.less" :
            tile.ActionId == "photos.zoom.less" || tile.ActionId == "photos.zoom.reset" ? "photos.zoom.more" :
            tile.ActionId == "music.tracks.next" || tile.ActionId == "music.tracks.previous" ? "music.select.0" : "back";
        var target = controls.Find(t => t && t.ActionId == alternate && t.gameObject.activeInHierarchy && t.IsInteractable());
        if (target) FocusAction(alternate); else FocusAction("back");
    }

    private void RefreshInspectedPhoto(bool live)
    {
        var camera = shell.cameraBridge;
        Texture photo = live ? camera != null && mediaPhotoIndex >= 0 && mediaPhotoIndex < camera.CapturedCount ? camera.Photos[mediaPhotoIndex] : null :
            samplePhotos != null && samplePhotos.Length > 0 ? samplePhotos[Mathf.Clamp(mediaPhotoIndex, 0, samplePhotos.Length - 1)] : null;
        if (photo != inspectedPhoto) { inspectedPhoto = photo; photoViewZoom = 1f; }
        if (extendedPhotoImage)
        {
            SetContainedPicture(extendedPhotoImage, photo, new Rect(0, 0, 1, 1));
            if (photo != null)
            {
                var frame = extendedPhotoImage.transform.parent as RectTransform;
                Vector2 size = extendedPhotoImage.rectTransform.sizeDelta * photoViewZoom;
                extendedPhotoImage.rectTransform.sizeDelta = size;
                extendedPhotoImage.rectTransform.anchoredPosition = new Vector2((frame.rect.width - size.x) * .5f, -(frame.rect.height - size.y) * .5f);
            }
        }
        if (extendedPhotoDetails) extendedPhotoDetails.text = photo == null ? "No photo available" :
            photo.width + " × " + photo.height + " pixels · " + photoViewZoom.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "×\n" +
            (live ? "Saved on this device · Photo " + (mediaPhotoIndex + 1) : "Sample photo · not saved");
        SetControlAvailable("photos.zoom.less", photo != null && photoViewZoom > 1f);
        SetControlAvailable("photos.zoom.more", photo != null && photoViewZoom < 3f);
        SetControlAvailable("photos.zoom.reset", photo != null && photoViewZoom > 1f);
    }
}
