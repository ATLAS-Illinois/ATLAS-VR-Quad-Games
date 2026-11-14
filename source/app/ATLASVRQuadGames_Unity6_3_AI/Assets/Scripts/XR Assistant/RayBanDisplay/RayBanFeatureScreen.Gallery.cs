using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class RayBanFeatureScreen
{
    [SerializeField] private RawImage photoDeletePreview;
    [SerializeField] private TMP_Text photoDeleteStatus;
    private Texture2D pendingPhotoDelete;
    private string photoActionMessage;

    // Upgrade existing authored instances in place. The Canvas, Meta pointer
    // surfaces, rig, focus adapter and other feature pages retain their identity.
    public void UpgradeLocalFeatures()
    {
        EnsurePhotoControls();
        EnsureTutorialGuide();
        EnsureAudioOnlyScreen();
        EnsureExternalAppScreens();
        EnsureLocalNotificationScreens();
        EnsureExtendedMedia();
        EnsureExtendedSettings();
        EnsureLauncherSettings();
        EnsureCaptionPaging();
        EnsureQuadGameGuide();
    }

    private void EnsurePhotoControls()
    {
        var photos = views.Find(v => v.key == "photos");
        if (photos == null || photos.root == null || views.Exists(v => v.key == "photos.delete")) return;
        if (mediaPhotoTitle) mediaPhotoTitle.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 382);
        if (mediaPhotoSubtitle)
        {
            mediaPhotoSubtitle.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 382);
            mediaPhotoSubtitle.fontSize = 19;
        }
        foreach (var tile in controls)
            if (tile && (tile.ActionId == "photos.previous" || tile.ActionId == "photos.next")) tile.Row = 2;
        Button(photos.root, "Delete photo", "photos.delete", "Delete", "close", 426, 408, 154, 80, 1, 2, accent: MediaPink);

        var page = Page("photos.delete", "Delete photo?");
        photoDeletePreview = Picture(page, "Selected photo", 0, 90, 88, 420, 238);
        Text(page, "Delete explanation", "Remove this photo from this device?\nThis cannot be undone.", 28, 346, 544, 82, 25);
        photoDeleteStatus = Text(page, "Delete status", "", 28, 440, 544, 40, 21, color: MediaSecondary);
        Button(page, "Keep photo", "photos.cancel", "Keep", "back", 20, 500, 274, 80, 1, 0);
        Button(page, "Confirm delete", "photos.confirm", "Delete", "close", 306, 500, 274, 80, 1, 1, accent: MediaPink);
        page.gameObject.SetActive(false);
    }

    private void BeginPhotoDelete()
    {
        var bridge = shell.cameraBridge;
        if (bridge == null || mediaPhotoIndex < 0 || mediaPhotoIndex >= bridge.CapturedCount) return;
        pendingPhotoDelete = bridge.Photos[mediaPhotoIndex];
        photoActionMessage = null;
        ShowView("photos.delete");
        SetContainedPicture(photoDeletePreview, pendingPhotoDelete, new Rect(0, 0, 1, 1));
        if (photoDeleteStatus) photoDeleteStatus.text = "";
        FocusAction("photos.cancel");
    }

    private void CancelPhotoDelete()
    {
        ClearPhotoDelete();
        ShowView("photos");
        FocusAction("photos.delete");
    }

    private void ConfirmPhotoDelete()
    {
        var bridge = shell.cameraBridge;
        int index = pendingPhotoDelete != null ? GalleryDataManager.SavedPhotos.IndexOf(pendingPhotoDelete) : -1;
        if (bridge == null || index < 0) { CancelPhotoDelete(); return; }
        if (!bridge.DeletePhoto(index, out string message))
        {
            if (photoDeleteStatus) photoDeleteStatus.text = message;
            FocusAction("photos.cancel");
            return;
        }
        ClearPhotoDelete();
        photoActionMessage = "Photo deleted";
        ShowView("photos");
        FocusAction(bridge.CapturedCount > 1 ? "photos.next" : "back");
    }

    private void ClearPhotoDelete()
    {
        pendingPhotoDelete = null;
        if (photoDeletePreview) photoDeletePreview.texture = null;
    }
}
