using UnityEngine;
using UnityEngine.UI;

public partial class RayBanFeatureScreen
{
    private bool HandleLiveMedia(string action)
    {
        var camera = shell.cameraBridge;
        var music = shell.musicBridge;
        switch (action)
        {
            case "photos.delete": BeginPhotoDelete(); return true;
            case "photos.cancel": CancelPhotoDelete(); return true;
            case "photos.confirm": ConfirmPhotoDelete(); return true;
            case "camera.capture":
                if (camera != null) camera.Capture(mediaCameraImage != null ? mediaCameraImage.uvRect : new Rect(0, 0, 1, 1));
                RefreshLiveCamera(); return true;
            case "camera.zoom":
                AdvanceCameraZoom();
                RefreshLiveCamera(); return true;
            case "camera.photos":
                mediaPhotoIndex = Mathf.Max(0, (camera != null ? camera.CapturedCount : 0) - 1);
                ShowView("photos"); FocusAction("photos.next"); return true;
            case "photos.previous":
            case "photos.next":
                photoActionMessage = null;
                int count = camera != null ? camera.CapturedCount : 0;
                if (count > 0) mediaPhotoIndex = (mediaPhotoIndex + (action == "photos.next" ? 1 : count - 1)) % count;
                RefreshLivePhotos(); return true;
            case "music.toggle": if (music != null) music.Toggle(); RefreshLiveMusic(); return true;
            case "music.previous": if (music != null) music.Previous(); RefreshLiveMusic(); return true;
            case "music.next": if (music != null) music.Next(); RefreshLiveMusic(); return true;
            default: return false;
        }
    }

    private void TickLiveMedia()
    {
        if (CurrentView == "camera") RefreshLiveCamera();
        else if (CurrentView == "music") RefreshLiveMusic();
    }

    private void RefreshLiveCamera()
    {
        var bridge = shell.cameraBridge;
        Texture frame = bridge != null ? bridge.PreviewTexture : null;
        mediaZoom = Mathf.Clamp(mediaZoom, 1f, 3f);
        if (mediaCameraImage != null)
        {
            mediaCameraImage.texture = frame;
            mediaCameraImage.enabled = frame != null;
            if (frame != null)
            {
                Rect crop = CoverCrop(frame, mediaCameraImage.rectTransform.rect.size);
                Vector2 size = crop.size / mediaZoom;
                mediaCameraImage.uvRect = new Rect(crop.center - size * .5f, size);
            }
        }
        if (mediaCameraZoom) mediaCameraZoom.text = mediaZoom.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "x";
        if (mediaCameraStatus) mediaCameraStatus.text = bridge != null ? bridge.Status : "Camera is not connected";
        SetControlAvailable("camera.capture", bridge != null && bridge.CanCapture);
        SetControlAvailable("camera.zoom", frame != null && (bridge == null || !bridge.IsCountingDown));
        SetControlAvailable("camera.timer", bridge != null && (bridge.CanCapture || bridge.IsCountingDown));
        SetButtonLabel("camera.timer", bridge != null && bridge.IsCountingDown ? "Cancel" : "3s timer");
    }

    private void RefreshLivePhotos()
    {
        var bridge = shell.cameraBridge;
        int count = bridge != null ? bridge.CapturedCount : 0;
        mediaPhotoIndex = Mathf.Clamp(mediaPhotoIndex, 0, Mathf.Max(0, count - 1));
        var photo = count > 0 ? bridge.Photos[mediaPhotoIndex] : null;
        SetContainedPicture(mediaPhotoImage, photo, new Rect(0, 0, 1, 1));
        if (mediaPhotoTitle) mediaPhotoTitle.text = count > 0 ? "Photo " + (mediaPhotoIndex + 1) : "No photos yet";
        if (mediaPhotoSubtitle) mediaPhotoSubtitle.text = !string.IsNullOrEmpty(photoActionMessage) ? photoActionMessage :
            GalleryDataManager.HasStorageWarning ? GalleryDataManager.LastStatus :
            count > 0 ? "On this device · " + count + " / " + bridge.PhotoCapacity :
            "Capture a photo in Camera.";
        if (mediaPhotoPosition) mediaPhotoPosition.text = count > 0 ? (mediaPhotoIndex + 1) + " of " + count : "0 photos";
        SetControlAvailable("photos.previous", count > 1);
        SetControlAvailable("photos.next", count > 1);
        SetControlAvailable("photos.delete", count > 0);
        SetControlAvailable("photos.view", count > 0);
    }

    private void RefreshLiveMusic()
    {
        var bridge = shell.musicBridge;
        var track = bridge != null ? bridge.CurrentTrack : null;
        bool available = bridge != null && bridge.IsAvailable;
        Sprite artwork = track != null ? track.image : null;
        Rect uv = new Rect(0, 0, 1, 1);
        if (artwork != null)
        {
            Rect rect = artwork.textureRect;
            uv = new Rect(rect.x / artwork.texture.width, rect.y / artwork.texture.height,
                rect.width / artwork.texture.width, rect.height / artwork.texture.height);
        }
        SetContainedPicture(mediaAlbumImage, artwork != null ? artwork.texture : null, uv);
        if (mediaTrackTitle) mediaTrackTitle.text = track != null ? track.title : "No music available";
        if (mediaTrackArtist) mediaTrackArtist.text = track != null ? track.artist : "Add tracks to the existing playlist";
        float elapsed = bridge != null ? bridge.Elapsed : 0f;
        float duration = bridge != null ? bridge.Duration : 0f;
        if (mediaElapsed) mediaElapsed.text = MediaTimestamp(elapsed);
        if (mediaDuration) mediaDuration.text = MediaTimestamp(duration);
        if (mediaPlaybackStatus) mediaPlaybackStatus.text = available
            ? bridge.Status + " · Volume " + Mathf.RoundToInt(bridge.Volume * 100f) + "%"
            : "Music is not connected";
        bool playing = bridge != null && bridge.IsPlaying;
        SetButtonLabel("music.toggle", playing ? "Pause" : "Play");
        SetButtonSymbol("music.toggle", playing ? "pause" : "play");
        float width = duration > 0 ? 472 * Mathf.Clamp01(elapsed / duration) : 0f;
        if (mediaProgressFill) mediaProgressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        if (mediaProgressThumb)
        {
            var position = mediaProgressThumb.rectTransform.anchoredPosition;
            position.x = 64 + width - 7.5f;
            mediaProgressThumb.rectTransform.anchoredPosition = position;
        }
        SetControlAvailable("music.toggle", available);
        SetControlAvailable("music.previous", available);
        SetControlAvailable("music.next", available);
    }

    private void SetControlAvailable(string action, bool available)
    {
        foreach (var control in controls)
        {
            if (!control || control.ActionId != action) continue;
            control.interactable = available;
            // Preserve RGB and focus styling; use authored alpha for unavailable controls.
            if (control.LabelRect)
            {
                var text = control.LabelRect.GetComponent<TMPro.TMP_Text>();
                Color c = text.color; c.a = available ? 1f : .45f; text.color = c;
            }
        }
    }

    private static Rect CoverCrop(Texture texture, Vector2 target)
    {
        if (!texture || texture.height <= 0 || target.y <= 0) return new Rect(0, 0, 1, 1);
        float sourceAspect = (float)texture.width / texture.height;
        float targetAspect = target.x / target.y;
        Vector2 size = sourceAspect > targetAspect ? new Vector2(targetAspect / sourceAspect, 1f) : new Vector2(1f, sourceAspect / targetAspect);
        return new Rect((Vector2.one - size) * .5f, size);
    }

    private static void SetContainedPicture(RawImage image, Texture texture, Rect uv)
    {
        if (!image) return;
        image.texture = texture;
        image.enabled = texture != null;
        if (!texture) return;
        image.uvRect = uv;
        var frame = image.transform.parent as RectTransform;
        if (!frame) return;
        Vector2 bounds = frame.rect.size;
        float width = texture.width * uv.width, height = texture.height * uv.height;
        if (width <= 0 || height <= 0) return;
        float scale = Mathf.Min(bounds.x / width, bounds.y / height);
        Vector2 size = new Vector2(width, height) * scale;
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = new Vector2((bounds.x - size.x) * .5f, -(bounds.y - size.y) * .5f);
    }
}
