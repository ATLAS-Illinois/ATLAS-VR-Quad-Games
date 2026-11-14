using System;
using TMPro;
using UnityEngine;

public partial class RayBanDisplayShell
{
    [SerializeField] private RectTransform localNotificationDashboard;
    [SerializeField] private TMP_Text localNotificationEmpty, localNotificationFooter, localNotificationBadge;
    [SerializeField] private RayBanTile[] localNotificationTiles = new RayBanTile[4];
    [SerializeField] private TMP_Text[] localNotificationTitles = new TMP_Text[4];
    [SerializeField] private TMP_Text[] localNotificationMessages = new TMP_Text[4];
    [SerializeField] private TMP_Text[] localNotificationTimes = new TMP_Text[4];
    private readonly string[] localNotificationIds = new string[4];
    private int localNotificationRevision = -1;
    private bool localNotificationWasLive;
    private string localNotificationDetail;
    private float nextLocalNotificationRefresh;
    public bool LocalNotificationAttentionVisible => localNotificationBadge != null && localNotificationBadge.gameObject.activeSelf;

    /// <summary>Upgrade only the former sample-notification dashboard.</summary>
    public void EnsureLocalNotificationDashboard()
    {
        if (pages == null || pages.Length < 2 || pages[1] == null || visualRoot == null) return;
        if (localNotificationDashboard != null && localNotificationBadge != null &&
            localNotificationDashboard.IsChildOf(pages[1].transform) && localNotificationBadge.transform.IsChildOf(visualRoot))
        { ApplyLocalNotificationDashboardTextLayout(); return; }
        var parent = pages[1].transform;
        tiles.RemoveAll(tile => tile == null || tile.PageId == 1);
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (dateLabel != null && child == dateLabel.transform) continue;
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        localNotificationDashboard = Rect("Local activity dashboard", parent, 0, 42, 600, 486);
        localNotificationEmpty = Label("Empty local activity", localNotificationDashboard,
            "No app activity yet\nSaved photos and route arrivals appear here.", 44, 120, 512, 190, 26, FontStyles.Normal);
        localNotificationEmpty.alignment = TextAlignmentOptions.Center;
        localNotificationEmpty.textWrappingMode = TextWrappingModes.Normal;
        for (int i = 0; i < 4; i++)
        {
            var tile = Tile("App activity", "bell", localNotificationDashboard, 1, i, i, 0,
                28, 10 + i * 96, 544, 84, new Color(.35f, .70f, .87f), false);
            tile.IconBadge.gameObject.SetActive(false);
            tile.IconGlyph.gameObject.SetActive(false);
            localNotificationTiles[i] = tile;
            localNotificationTitles[i] = Label("Activity title", tile.transform, "", 20, 8, 396, 32, 24, FontStyles.Bold);
            localNotificationMessages[i] = Label("Activity summary", tile.transform, "", 20, 44, 504, 28, 19, FontStyles.Normal);
            localNotificationTimes[i] = Label("Activity time", tile.transform, "", 434, 11, 90, 26, 17, FontStyles.Normal);
            localNotificationTimes[i].alignment = TextAlignmentOptions.Right;
            localNotificationTitles[i].richText = localNotificationMessages[i].richText = localNotificationTimes[i].richText = false;
        }
        Tile("All activity", "bell", localNotificationDashboard, 1, 4, 4, 0, 28, 402, 544, 64,
            new Color(.35f, .64f, .80f));
        localNotificationFooter = Label("Local activity scope", localNotificationDashboard, "App activity · stored on this device",
            24, 470, 552, 16, 13, FontStyles.Normal);
        localNotificationFooter.alignment = TextAlignmentOptions.Center;
        localNotificationBadge = Label("Unread app activity", visualRoot, "", 365, 24, 196, 32, 18, FontStyles.Bold,
            new Color(.69f, .90f, 1f));
        ApplyLocalNotificationDashboardTextLayout();
        localNotificationRevision = -1;
        RefreshLocalNotifications(true);
    }

    private void ApplyLocalNotificationDashboardTextLayout()
    {
        // The project's font metrics need more than a 32px line box at 24pt.
        // Upgrade the existing serialized labels without rebuilding the cards.
        foreach (var title in localNotificationTitles)
        {
            if (title == null) continue;
            title.rectTransform.anchoredPosition = new Vector2(20, -2);
            title.rectTransform.sizeDelta = new Vector2(396, 40);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.enableAutoSizing = true;
            title.fontSizeMin = 18; title.fontSizeMax = 24;
        }
        if (localNotificationFooter != null)
        {
            localNotificationFooter.rectTransform.anchoredPosition = new Vector2(24, -468);
            localNotificationFooter.rectTransform.sizeDelta = new Vector2(552, 18);
            localNotificationFooter.enableAutoSizing = true;
            localNotificationFooter.fontSizeMin = 10; localNotificationFooter.fontSizeMax = 12;
        }
    }

    public void RefreshLocalNotifications(bool force = false)
    {
        if (localNotificationDashboard == null || localNotificationBadge == null) return;
        bool live = UsesLiveFeatures;
        // Authoring and isolated preview rendering never open the user's store.
        var store = live ? RayBanLocalNotifications.Store : null;
        int revision = store != null ? store.Revision : 0;
        if (!force && localNotificationWasLive == live && localNotificationRevision == revision && localNotificationDetail == currentDetail &&
            (!Application.isPlaying || Time.unscaledTime < nextLocalNotificationRefresh)) return;
        localNotificationRevision = revision;
        localNotificationWasLive = live;
        localNotificationDetail = currentDetail;
        nextLocalNotificationRefresh = Time.unscaledTime + 15f;
        int count = store != null ? Math.Min(4, store.Items.Count) : 0;
        localNotificationEmpty.gameObject.SetActive(count == 0);
        for (int i = 0; i < 4; i++)
        {
            bool exists = i < count;
            localNotificationTiles[i].gameObject.SetActive(exists);
            localNotificationIds[i] = exists ? store.Items[i].Id : null;
            if (!exists) continue;
            var item = store.Items[i];
            localNotificationTitles[i].text = item.Title;
            localNotificationMessages[i].text = item.Message;
            localNotificationTimes[i].text = Age(item.CreatedUtcTicks);
            // DND suppresses dashboard attention. Read state stays visible in
            // the deliberately opened inbox and history is never discarded.
            localNotificationTitles[i].fontStyle = !item.IsRead && !store.DoNotDisturb ? FontStyles.Bold : FontStyles.Normal;
        }
        bool attention = live && !store.DoNotDisturb && store.UnreadCount > 0;
        localNotificationBadge.text = attention ? store.UnreadCount + " unread" : "";
        localNotificationBadge.gameObject.SetActive(attention && string.IsNullOrEmpty(currentDetail));
        localNotificationFooter.text = store != null && !string.IsNullOrEmpty(store.LastStorageError) ? "History could not be saved · open All activity" :
            store != null && store.DoNotDisturb ? "Quiet mode on · history is kept" : "App activity · stored on this device";
    }

    public void OpenLocalNotification(int dashboardIndex)
    {
        if (!UsesLiveFeatures) return;
        string id = dashboardIndex >= 0 && dashboardIndex < localNotificationIds.Length ? localNotificationIds[dashboardIndex] : null;
        OpenApp("Notifications");
        if (!string.IsNullOrEmpty(id)) featureScreens.OpenLocalNotification(id);
        RefreshLocalNotifications(true);
    }

    private static string Age(long utcTicks)
    {
        double seconds = Math.Max(0, (DateTime.UtcNow - new DateTime(utcTicks, DateTimeKind.Utc)).TotalSeconds);
        if (seconds < 60) return "now";
        if (seconds < 3600) return ((int)(seconds / 60)) + "m";
        if (seconds < 86400) return ((int)(seconds / 3600)) + "h";
        return ((int)(seconds / 86400)) + "d";
    }
}
