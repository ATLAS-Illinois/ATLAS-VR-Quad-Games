using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class RayBanFeatureScreen
{
    [SerializeField] private int localNotificationUiVersion;
    [SerializeField] private TMP_Text localInboxStatus, localInboxPosition, localInboxEmpty;
    [SerializeField] private TMP_Text localActivityTitle, localActivityBody, localActivityTime, localDndStatus;
    [SerializeField] private RayBanTile[] localInboxRows = new RayBanTile[4];
    [SerializeField] private TMP_Text[] localInboxRowTitles = new TMP_Text[4];
    [SerializeField] private TMP_Text[] localInboxRowMessages = new TMP_Text[4];
    private readonly string[] localInboxIds = new string[4];
    private int localInboxPage, localActivityRevision = -1;
    private string localActivityId;
    private bool CanUseLocalActivity => !renderingCopy && shell != null && shell.UsesLiveFeatures;
    public static bool IsLocalNotificationView(string key) => key == "do not disturb" ||
        key == "notifications" || key == "notifications.detail" || key == "notifications.clear";

    /// <summary>Replace only the old sample notification page and add local settings.</summary>
    public void EnsureLocalNotificationScreens()
    {
        if (localNotificationUiVersion == 1 && localInboxStatus != null && localDndStatus != null)
        { ApplyLocalNotificationTextLayout(); EnsureActivityFollowThrough(); return; }
        var view = views.Find(item => item.key == "notifications");
        if (view == null || view.root == null) return;
        for (int i = view.root.childCount - 1; i >= 0; i--)
        {
            var child = view.root.GetChild(i);
            if (child.name == "Heading" || child.name == "Back") continue;
            controls.RemoveAll(control => control == null || control.transform.IsChildOf(child));
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        view.heading.text = "App activity";
        localInboxStatus = Text(view.root, "Activity status", "", 24, 86, 552, 44, 18);
        localInboxEmpty = Text(view.root, "Empty activity", "No activity yet.\nSaved photos and route arrivals appear here.", 40, 205, 520, 170, 27);
        localInboxEmpty.alignment = TextAlignmentOptions.Center;
        for (int i = 0; i < 4; i++)
        {
            var tile = Button(view.root, "Activity " + i, "activity.open." + i, "", "bell", 20, 136 + i * 76, 560, 68, i + 1, 0, true);
            tile.IconGlyph.gameObject.SetActive(false);
            localInboxRows[i] = tile;
            localInboxRowTitles[i] = Text(tile.transform, "Activity title", "", 18, 6, 524, 29, 22, true);
            localInboxRowMessages[i] = Text(tile.transform, "Activity summary", "", 18, 35, 524, 26, 18);
            localInboxRowTitles[i].richText = localInboxRowMessages[i].richText = false;
            localInboxRowTitles[i].overflowMode = localInboxRowMessages[i].overflowMode = TextOverflowModes.Ellipsis;
        }
        Button(view.root, "Older activity", "activity.previous", "Previous", "previous", 20, 450, 170, 58, 5, 0);
        localInboxPosition = Text(view.root, "Activity page", "1 / 1", 200, 463, 200, 30, 20);
        localInboxPosition.alignment = TextAlignmentOptions.Center;
        Button(view.root, "Newer activity", "activity.next", "Next", "next", 410, 450, 170, 58, 5, 2);
        Button(view.root, "Clear activity", "activity.clear", "Clear history", "close", 20, 522, 274, 58, 6, 0);
        Button(view.root, "Activity settings", "activity.settings", "Quiet mode", "donotdisturb", 306, 522, 274, 58, 6, 2);

        var detail = Page("notifications.detail", "App activity");
        var card = Panel(detail, "Activity details", 20, 108, 560, 350);
        localActivityTitle = Text(card.transform, "Activity heading", "", 28, 26, 504, 88, 30, true);
        localActivityBody = Text(card.transform, "Activity body", "", 28, 132, 504, 142, 25);
        localActivityTime = Text(card.transform, "Activity date", "", 28, 296, 504, 34, 19);
        localActivityTitle.richText = localActivityBody.richText = localActivityTime.richText = false;
        localActivityTitle.enableAutoSizing = localActivityBody.enableAutoSizing = true;
        localActivityTitle.fontSizeMin = 22; localActivityTitle.fontSizeMax = 30;
        localActivityBody.fontSizeMin = 18; localActivityBody.fontSizeMax = 25;
        localActivityTitle.overflowMode = localActivityBody.overflowMode = TextOverflowModes.Ellipsis;
        Button(detail, "Activity inbox", "activity.inbox", "All activity", "back", 20, 500, 274, 80, 1, 0);
        Button(detail, "Delete activity", "activity.delete", "Delete", "close", 306, 500, 274, 80, 1, 1);

        var clear = Page("notifications.clear", "Clear app history?");
        Text(clear, "Clear history explanation", "Remove the activity history from this device?\n\nYour photos, routes and app settings are kept.", 36, 165, 528, 230, 29);
        Button(clear, "Keep activity", "activity.inbox", "Keep", "back", 20, 500, 274, 80, 1, 0);
        Button(clear, "Confirm clear activity", "activity.clear.confirm", "Clear history", "close", 306, 500, 274, 80, 1, 1);

        var dnd = Page("do not disturb", "Do not disturb");
        Glyph(dnd, "Quiet activity symbol", "donotdisturb", 252, 106, 96, 96);
        localDndStatus = Text(dnd, "Quiet activity status", "", 32, 222, 536, 62, 30, true);
        localDndStatus.alignment = TextAlignmentOptions.Center;
        Text(dnd, "Quiet activity scope", "Hides this app's unread indicator.\nActivity history is still kept.\n\nHeadset notifications, sound and other apps are unchanged.", 38, 306, 524, 154, 23).alignment = TextAlignmentOptions.Center;
        Button(dnd, "Toggle quiet activity", "activity.dnd.toggle", "Turn on", "donotdisturb", 20, 500, 274, 80, 1, 0);
        Button(dnd, "Open app activity", "activity.inbox", "App activity", "bell", 306, 500, 274, 80, 1, 1);
        detail.gameObject.SetActive(false); clear.gameObject.SetActive(false); dnd.gameObject.SetActive(false);
        localNotificationUiVersion = 1;
        ApplyLocalNotificationTextLayout();
        EnsureActivityFollowThrough();
        RefreshLocalNotificationView(true);
    }

    private void EnsureActivityFollowThrough()
    {
        var detail = views.Find(view => view.key == "notifications.detail");
        if (detail == null || detail.root == null) return;
        if (!controls.Exists(control => control != null && control.ActionId == "activity.feature"))
            Button(detail.root, "Open activity feature", "activity.feature", "Open feature", "next", 20, 454, 560, 58, 1, 0);
        var card = detail.root.Find("Activity details") as RectTransform;
        if (card != null) card.sizeDelta = new Vector2(560, 326);
        if (localActivityTime != null) localActivityTime.rectTransform.anchoredPosition = new Vector2(28, -282);
        foreach (var control in controls)
        {
            if (control == null || !control.transform.IsChildOf(detail.root)) continue;
            if (control.ActionId != "activity.inbox" && control.ActionId != "activity.delete") continue;
            control.Row = 2;
            var rect = control.transform as RectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -522);
            rect.sizeDelta = new Vector2(274, 58);
        }
    }

    // Apply to existing authored pages as well as newly constructed pages.
    // These changes do not require rebuilding any page or changing its identity.
    private void ApplyLocalNotificationTextLayout()
    {
        for (int i = 0; i < localInboxRowTitles.Length; i++)
        {
            var title = localInboxRowTitles[i];
            if (title != null)
            {
                title.rectTransform.anchoredPosition = new Vector2(18, -4);
                title.rectTransform.sizeDelta = new Vector2(524, 36);
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.enableAutoSizing = true;
                title.fontSizeMin = 18; title.fontSizeMax = 22;
            }
            if (i < localInboxRowMessages.Length && localInboxRowMessages[i] != null)
            {
                localInboxRowMessages[i].rectTransform.anchoredPosition = new Vector2(18, -38);
                localInboxRowMessages[i].rectTransform.sizeDelta = new Vector2(524, 26);
                localInboxRowMessages[i].textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
        var quiet = views.Find(view => view.key == "do not disturb");
        var scopeTransform = quiet != null && quiet.root != null ? quiet.root.Find("Quiet activity scope") : null;
        var scope = scopeTransform != null ? scopeTransform.GetComponent<TMP_Text>() : null;
        if (scope == null) return;
        scope.text = "Hides the app's unread indicator.\nKeeps your activity history.\n\nNo change to headset alerts,\nsound or other apps.";
        scope.rectTransform.anchoredPosition = new Vector2(38, -300);
        scope.rectTransform.sizeDelta = new Vector2(524, 180);
        scope.enableAutoSizing = true;
        scope.fontSizeMin = 20; scope.fontSizeMax = 23;
    }

    public void OpenLocalNotification(string id)
    {
        if (!CanUseLocalActivity) return;
        localActivityId = id;
        RayBanLocalNotifications.Store.MarkRead(id);
        ShowView("notifications.detail");
        RefreshLocalNotificationView(true);
    }

    public bool TryHandleLocalNotification(string action)
    {
        if (string.IsNullOrEmpty(action) || !action.StartsWith("activity.", StringComparison.Ordinal)) return false;
        if (renderingCopy) return true;
        var store = CanUseLocalActivity ? RayBanLocalNotifications.Store : null;
        if (action.StartsWith("activity.open.", StringComparison.Ordinal))
        {
            if (int.TryParse(action.Substring("activity.open.".Length), out int index) && index >= 0 && index < localInboxIds.Length)
                OpenLocalNotification(localInboxIds[index]);
            return true;
        }
        switch (action)
        {
            case "activity.previous": localInboxPage--; break;
            case "activity.next": localInboxPage++; break;
            case "activity.inbox": ShowView("notifications"); break;
            case "activity.settings": ShowView("do not disturb"); break;
            case "activity.clear": ShowView("notifications.clear"); FocusAction("activity.inbox"); break;
            case "activity.clear.confirm":
                if (store != null) store.Clear();
                localInboxPage = 0; ShowView("notifications"); break;
            case "activity.delete":
                if (store != null) store.Delete(localActivityId);
                localActivityId = null; ShowView("notifications"); break;
            case "activity.dnd.toggle": if (store != null) store.DoNotDisturb = !store.DoNotDisturb; break;
            case "activity.feature":
                var entry = store != null ? store.Find(localActivityId) : null;
                // Route only typed local events. Stored text cannot launch URLs
                // or select a destination. The album opens without deleting or
                // pretending that an older photo still exists.
                if (entry != null && entry.Kind == QuadNotificationKind.PhotoSaved) shell.OpenApp("Photos");
                else if (entry != null && entry.Kind == QuadNotificationKind.RouteArrived) shell.OpenApp("Maps");
                return true;
        }
        RefreshLocalNotificationView(true);
        return true;
    }

    public bool HandleLocalNotificationBack()
    {
        if (currentView != "notifications.detail" && currentView != "notifications.clear") return false;
        ShowView("notifications"); RefreshLocalNotificationView(true); return true;
    }

    public void RefreshLocalNotificationView(bool force = false)
    {
        if (renderingCopy || localInboxStatus == null) return;
        var store = CanUseLocalActivity ? RayBanLocalNotifications.Store : null;
        int revision = store != null ? store.Revision : 0;
        if (!force && localActivityRevision == revision) return;
        localActivityRevision = revision;
        // Disabling a selected uGUI Button clears EventSystem selection. Keep
        // the focus input path alive when a pagination button reaches its end.
        var selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var previousFocus = selectedObject != null ? selectedObject.GetComponent<RayBanTile>() : null;
        var inbox = views.Find(view => view.key == "notifications");
        bool hadInboxFocus = currentView == "notifications" && previousFocus != null && inbox != null &&
            previousFocus.transform.IsChildOf(inbox.root);
        int count = store != null ? store.Items.Count : 0;
        int pageCount = Math.Max(1, (count + 3) / 4);
        localInboxPage = Mathf.Clamp(localInboxPage, 0, pageCount - 1);
        bool quiet = store != null && store.DoNotDisturb;
        localInboxStatus.text = store != null && !string.IsNullOrEmpty(store.LastStorageError) ? store.LastStorageError :
            (quiet ? "Quiet mode on · history is kept" : (store != null ? store.UnreadCount : 0) + " unread · stored on this device");
        localInboxEmpty.gameObject.SetActive(count == 0);
        localInboxPosition.text = (localInboxPage + 1) + " / " + pageCount;
        for (int i = 0; i < 4; i++)
        {
            int index = localInboxPage * 4 + i;
            bool exists = index < count;
            localInboxRows[i].gameObject.SetActive(exists);
            localInboxIds[i] = exists ? store.Items[index].Id : null;
            if (!exists) continue;
            var item = store.Items[index];
            localInboxRowTitles[i].text = (item.IsRead ? "" : "New: ") + item.Title;
            localInboxRowMessages[i].text = item.Message;
        }
        SetControlAvailable("activity.previous", localInboxPage > 0);
        SetControlAvailable("activity.next", localInboxPage < pageCount - 1);
        SetControlAvailable("activity.clear", count > 0);
        SetControlAvailable("activity.clear.confirm", count > 0);
        SetControlAvailable("activity.dnd.toggle", store != null);
        localDndStatus.text = quiet ? "App quiet mode is on" : "App quiet mode is off";
        SetButtonLabel("activity.dnd.toggle", quiet ? "Turn off" : "Turn on");
        var selected = store != null ? store.Find(localActivityId) : null;
        localActivityTitle.text = selected != null ? selected.Title : "Activity unavailable";
        localActivityBody.text = selected != null ? selected.Message : "This entry is no longer in the local history.";
        localActivityTime.text = selected != null ? new DateTime(selected.CreatedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MMM d · h:mm tt") : "";
        SetControlAvailable("activity.delete", selected != null);
        bool canOpen = selected != null && (selected.Kind == QuadNotificationKind.PhotoSaved || selected.Kind == QuadNotificationKind.RouteArrived);
        SetControlAvailable("activity.feature", canOpen);
        SetButtonLabel("activity.feature", selected != null && selected.Kind == QuadNotificationKind.PhotoSaved ? "Open Photos" :
            selected != null && selected.Kind == QuadNotificationKind.RouteArrived ? "Open Maps" : "Open feature");
        foreach (var view in views)
        {
            if (view.key != "do not disturb" && !view.key.StartsWith("notifications", StringComparison.Ordinal)) continue;
            var badge = view.root.Find("Demo badge");
            if (badge != null) badge.gameObject.SetActive(false);
        }
        if (hadInboxFocus) RestoreLocalInboxFocus(previousFocus);
    }

    private void RestoreLocalInboxFocus(RayBanTile previousFocus)
    {
        if (!Application.isPlaying || EventSystem.current == null) return;
        var currentObject = EventSystem.current.currentSelectedGameObject;
        var currentControl = currentObject != null ? currentObject.GetComponent<UnityEngine.UI.Selectable>() : null;
        if (currentControl != null && currentControl.isActiveAndEnabled && currentControl.IsInteractable()) return;
        if (previousFocus != null && previousFocus.isActiveAndEnabled && previousFocus.IsInteractable())
        { Focus(previousFocus); return; }
        foreach (var row in localInboxRows)
            if (row != null && row.isActiveAndEnabled && row.IsInteractable())
            { Focus(row); return; }
        FocusAction("back");
    }
}
