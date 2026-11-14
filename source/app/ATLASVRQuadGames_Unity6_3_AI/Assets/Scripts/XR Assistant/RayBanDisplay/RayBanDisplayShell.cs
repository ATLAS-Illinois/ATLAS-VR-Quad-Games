using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Editable display shell using the existing root and focus input. Selected live
/// features reuse legacy services, local activity and explicit browser handoffs.
/// </summary>
[DisallowMultipleComponent]
public partial class RayBanDisplayShell : MonoBehaviour
{
    [Tooltip("Use the redesigned display. To return to legacy pages, stop Play Mode and disable this.")]
    public bool previewEnabled = true;
    [Tooltip("Connect app features during Play, including local activity and explicit external browser handoffs.")]
    public bool liveFeaturesEnabled = true;
    public RayBanAppSettings appSettings;
    public RayBanCameraBridge cameraBridge;
    public RayBanMusicBridge musicBridge;
    public RayBanAssistantBridge assistantBridge;
    public RayBanCaptionBridge captionBridge;
    public RayBanMapBridge mapBridge;
    public TMP_FontAsset font;
    public Texture2D[] samplePhotos = Array.Empty<Texture2D>();
    public QuestDisplayUI router;
    public GameObject[] servicesToSuspend = Array.Empty<GameObject>();

    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private GameObject[] pages = new GameObject[3];
    [SerializeField] private List<RayBanTile> tiles = new List<RayBanTile>();
    [SerializeField] private List<RayBanSurface> dots = new List<RayBanSurface>();
    [SerializeField] private RectTransform appsContent;
    [SerializeField] private RectTransform appsViewport;
    [SerializeField] private ScrollRect appScroll;
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private RayBanFeatureScreen featureScreens;
    [SerializeField] private TMP_Text dateLabel;
    [SerializeField] private TMP_Text batteryLabel;
    [SerializeField] private int currentPage = 2;
    [SerializeField] private int selectedIndex;
    [SerializeField] private string currentDetail = "";

    private readonly Dictionary<GameObject, bool> suspended = new Dictionary<GameObject, bool>();
    private bool initialized;
    private bool startupTutorialChecked;
    private bool quitting;
    private bool selecting;
    private int revealedFrame = -1;
    private float nextBatteryRefresh;
    private const float Width = 600f;
    private static readonly Color Card = new Color(1, 1, 1, .92f);
    private static readonly Color TextColor = new Color(.98f, .99f, 1f, 1f);
    private static readonly string[] LegacyNames = { "Music", "Assistant", "Camera", "Photos", "Captions", "Maps" };
    private static readonly string[] AppNames = { "WhatsApp", "Instagram", "Messenger", "Messages", "Calls", "Camera", "Music", "Photos", "Captions", "Assistant", "Maps", "Tutorials" };
    private static readonly string[] AppSymbols = { "whatsapp", "instagram", "messenger", "messages", "phone", "camera", "music", "photos", "captions", "assistant", "map", "tutorials" };
    private static readonly Color[] AppColors = {
        new Color(.12f,.87f,.60f), new Color(.87f,.33f,.85f), new Color(.20f,.65f,1f), new Color(.72f,.85f,.96f),
        new Color(.41f,.86f,.62f), new Color(1f,.46f,.66f), new Color(.66f,.48f,1f), new Color(.99f,.47f,.79f),
        new Color(.90f,.78f,.35f), new Color(.48f,.79f,1f), new Color(.23f,.85f,.89f), new Color(1f,.66f,.30f)
    };

    // QuestDisplayUI initializes this from its early Awake, before Unity has
    // necessarily dispatched this component's OnEnable.
    public bool OwnsPreview => previewEnabled && enabled && gameObject.activeInHierarchy;
    public bool UsesLiveFeatures => OwnsPreview && liveFeaturesEnabled && Application.isPlaying;
    public bool IsVisible => visualRoot != null && visualRoot.gameObject.activeInHierarchy;
    public int CurrentPage => currentPage;
    public string CurrentDetail => currentDetail;
    public RectTransform VisualRoot => visualRoot;
    public RayBanFeatureScreen FeatureScreens => featureScreens;

    // Called after the router's startup visibility decision, not on every menu reopen.
    public bool ShowStartupTutorialIfNeeded()
    {
        if (startupTutorialChecked || !UsesLiveFeatures || featureScreens == null) return false;
        startupTutorialChecked = true;
        if (!RayBanFeatureScreen.NeedsFirstRunTutorial) return false;
        OpenApp("Tutorials");
        featureScreens.BeginFirstRunTutorial();
        return true;
    }

    public bool InitializePreview()
    {
        if (!OwnsPreview)
        {
            Hide();
            if (Application.isPlaying) RestoreServices();
            return false;
        }
        if (router == null) router = GetComponent<QuestDisplayUI>();
        if (router != null && (!Application.isPlaying || !initialized))
        {
            if (router.mainMenuRoot != null) router.mainMenuRoot.SetActive(false);
            foreach (var entry in router.entries)
                if (entry != null && entry.page != null) entry.page.SetActive(false);
        }
        if (Application.isPlaying && !initialized)
        {
            foreach (var service in servicesToSuspend)
            {
                if (service == null || transform.IsChildOf(service.transform) || suspended.ContainsKey(service)) continue;
                suspended.Add(service, service.activeSelf);
                service.SetActive(false);
            }
            initialized = true;
        }
        EnsureTree();
        EnsureLauncherOrder();
        if (featureScreens != null) featureScreens.UpgradeLocalFeatures();
        EnsureLocalNotificationDashboard();
        RefreshLocalNotifications();
        BindScroll();
        if (UsesLiveFeatures && appSettings != null) appSettings.Activate(visualRoot);
        return true;
    }

    private void OnApplicationQuit() => quitting = true;

    private void Update()
    {
        RefreshLocalNotifications();
        if (IsVisible && Time.unscaledTime >= nextBatteryRefresh)
        {
            RefreshBattery();
            nextBatteryRefresh = Time.unscaledTime + 5f;
        }
    }

    private void RefreshBattery()
    {
        if (batteryLabel != null) batteryLabel.text = RayBanAppSettings.BatteryLabel;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        Hide();
        if (cameraBridge != null) cameraBridge.SetCameraVisible(false);
        if (musicBridge != null) musicBridge.Release(!quitting);
        if (assistantBridge != null) assistantBridge.Release(!quitting);
        if (captionBridge != null) captionBridge.Release(!quitting);
        if (mapBridge != null) mapBridge.Release(!quitting);
        if (appSettings != null) appSettings.Deactivate();
        // Do not wake voice/camera services while Unity is tearing down Play Mode.
        // The Editor restores their serialized states when the scene is reloaded.
        if (!quitting) RestoreServices();
        else { suspended.Clear(); initialized = false; }
    }

    private void RestoreServices()
    {
        foreach (var state in suspended)
            if (state.Key != null) state.Key.SetActive(state.Value);
        suspended.Clear();
        initialized = false;
    }

    private void EnsureTree()
    {
        if (visualRoot == null || pages == null || pages.Length != 3 || pages[2] == null)
            BuildVisualTree();
    }

    /// <summary>Explicit, idempotent authoring operation; no continuously-running edit-mode builder.</summary>
    public void BuildVisualTree()
    {
        if (visualRoot == null)
        {
            var existing = transform.Find("Ray-Ban Display Preview");
            if (existing != null) visualRoot = existing as RectTransform;
        }
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(visualRoot.gameObject);
            else DestroyImmediate(visualRoot.gameObject);
        }
        tiles.Clear();
        dots.Clear();
        pages = new GameObject[3];
        visualRoot = Rect("Ray-Ban Display Preview", transform, 0, 0, Width, Width);
        visualRoot.anchorMin = visualRoot.anchorMax = new Vector2(.5f, .5f);
        visualRoot.pivot = new Vector2(.5f, .5f);
        visualRoot.anchoredPosition = Vector2.zero;
        visualRoot.localScale = Vector3.one * .000396f;
        var canvas = visualRoot.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        visualRoot.gameObject.AddComponent<GraphicRaycaster>();

        var dotPill = Surface("Page indicator", visualRoot, 246, 8, 108, 64, new Color(1,1,1,.12f), 32);
        for (int i = 0; i < 3; i++)
        {
            var dot = Surface("Page " + (i + 1), dotPill.transform, 32 + i * 18, 26, 8, 8, Color.white, 6);
            dot.Circle = true;
            dot.Flat = true;
            dots.Add(dot);
        }
        // The Quad keeps its existing ray/poke interaction. Make the page
        // indicators directly selectable as well as reachable by focus input.
        for (int i = 0; i < 3; i++)
        {
            var hit = Rect("Select dashboard " + i, dotPill.transform, i * 36, 0, 36, 64);
            var target = hit.gameObject.AddComponent<Image>();
            target.color = Color.clear;
            target.raycastTarget = true;
            var tab = hit.gameObject.AddComponent<RayBanPageTab>();
            tab.shell = this;
            tab.page = i;
        }
        for (int i = 0; i < 3; i++) pages[i] = Rect("Dashboard " + i, visualRoot, 0, 72, 600, 528).gameObject;
        BuildQuickSettings(pages[0].transform);
        BuildNotifications(pages[1].transform);
        BuildApps(pages[2].transform);
        BuildDetail();
        EnsureLocalNotificationDashboard();
        currentDetail = "";
        currentPage = Mathf.Clamp(currentPage, 0, 2);
        for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == currentPage);
        BindScroll();
        if (UsesLiveFeatures && appSettings != null) appSettings.BindVisuals(visualRoot);
        if (OwnsPreview) ShowLauncher(); else Hide();
    }

    public void ShowLauncher()
    {
        if (!OwnsPreview) return;
        EnsureTree();
        if (!IsVisible && Application.isPlaying) revealedFrame = Time.frameCount;
        visualRoot.gameObject.SetActive(true);
        if (router != null && router.canvasGroup != null) router.canvasGroup.alpha = 1;
        currentDetail = "";
        detailRoot.SetActive(false);
        for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == currentPage);
        foreach (var dot in dots) dot.transform.parent.gameObject.SetActive(true);
        UpdateDots();
        RefreshLocalNotifications(true);
        RefreshBattery();
        if (dateLabel != null) dateLabel.text = DateTime.Now.ToString("ddd, MMM d  ·  h:mm tt");
        RestoreDashboardState();
        SelectItem(selectedIndex);
        UpdateScrollFade();
    }

    public void Hide()
    {
        if (visualRoot == null) return;
        CaptureDashboardState();
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (Application.isPlaying && selected != null && selected.transform.IsChildOf(visualRoot))
            EventSystem.current.SetSelectedGameObject(null);
        visualRoot.gameObject.SetActive(false);
    }

    public void ShowLegacyPage(int index)
    {
        if (index >= 0 && index < LegacyNames.Length) OpenApp(LegacyNames[index]);
    }

    public void SetPage(int page)
    {
        CaptureDashboardState();
        currentPage = Mathf.Clamp(page, 0, 2);
        ShowLauncher();
    }

    public void ReturnToLauncher() => ShowLauncher();
    public void Back()
    {
        if (!string.IsNullOrEmpty(currentDetail) && featureScreens != null) featureScreens.Back();
        else ReturnToLauncher();
    }

    public void OpenApp(string appName)
    {
        if (!OwnsPreview) return;
        EnsureTree();
        CaptureDashboardState();
        visualRoot.gameObject.SetActive(true);
        if (router != null && router.canvasGroup != null) router.canvasGroup.alpha = 1;
        currentDetail = string.IsNullOrEmpty(appName) ? "App" : appName;
        foreach (var page in pages) page.SetActive(false);
        foreach (var dot in dots) dot.transform.parent.gameObject.SetActive(false);
        detailRoot.SetActive(true);
        featureScreens.Open(currentDetail);
        RefreshLocalNotifications(true);
    }

    public void Navigate(MoveDirection direction)
    {
        if (!string.IsNullOrEmpty(currentDetail)) { featureScreens.Navigate(direction); return; }
        var tile = FindItem(currentPage, selectedIndex);
        if (tile != null) NavigateFrom(tile, direction);
    }

    public void NavigateFrom(RayBanTile from, MoveDirection direction)
    {
        if (!OwnsPreview || !IsVisible || from == null) return;
        if (!string.IsNullOrEmpty(currentDetail))
        {
            featureScreens.NavigateFrom(from, direction);
            return;
        }
        if (from.PageId != currentPage) return;
        RayBanTile target = null;
        foreach (var tile in tiles)
        {
            if (tile == null || tile.PageId != currentPage || tile == from || !tile.gameObject.activeInHierarchy || !tile.IsInteractable()) continue;
            bool match = direction == MoveDirection.Left ? tile.Row == from.Row && tile.Column < from.Column :
                         direction == MoveDirection.Right ? tile.Row == from.Row && tile.Column > from.Column :
                         direction == MoveDirection.Up ? tile.Row < from.Row :
                         direction == MoveDirection.Down && tile.Row > from.Row;
            if (!match) continue;
            if (target == null || NavigationDistance(tile, from, direction) < NavigationDistance(target, from, direction)) target = tile;
        }
        if (target != null) SelectTile(target);
        else if (direction == MoveDirection.Left && currentPage > 0) SetPage(currentPage - 1);
        else if (direction == MoveDirection.Right && currentPage < 2) SetPage(currentPage + 1);
    }

    private static int NavigationDistance(RayBanTile tile, RayBanTile from, MoveDirection direction)
    {
        bool vertical = direction == MoveDirection.Up || direction == MoveDirection.Down;
        return vertical ? Math.Abs(tile.Row - from.Row) * 10 + Math.Abs(tile.Column - from.Column) : Math.Abs(tile.Column - from.Column);
    }

    public void ActivateTile(RayBanTile tile)
    {
        if (!OwnsPreview || !IsVisible || tile == null) return;
        // Reveal and submit share the same optical thumb-tap event source.
        if (Application.isPlaying && revealedFrame == Time.frameCount) return;
        if (tile.PageId == -2) { featureScreens.Activate(tile); return; }
        if (tile.PageId == 1)
        {
            OpenLocalNotification(tile.ItemIndex);
            return;
        }
        if (tile.ActionId == "back") ReturnToLauncher();
        else if (tile.ActionId == "hide")
        {
            if (router != null) router.HideMainMenu(); else Hide();
        }
        else OpenApp(tile.Title);
    }

    public void ObserveSelection(RayBanTile tile)
    {
        if (selecting || tile == null || tile.PageId != currentPage || !string.IsNullOrEmpty(currentDetail)) return;
        selectedIndex = tile.ItemIndex;
        EnsureVisible(tile);
    }

    private RayBanTile FindItem(int page, int index)
    {
        RayBanTile first = null;
        foreach (var tile in tiles)
        {
            if (tile == null || tile.PageId != page || !tile.gameObject.activeSelf || !tile.IsInteractable()) continue;
            if (first == null) first = tile;
            if (tile.ItemIndex == index) return tile;
        }
        return first;
    }

    private void SelectItem(int index) => SelectTile(FindItem(currentPage, index));

    private void SelectTile(RayBanTile tile)
    {
        if (tile == null) return;
        foreach (var candidate in tiles) if (candidate != null) candidate.SetVisualFocus(candidate == tile);
        if (tile.PageId == currentPage && string.IsNullOrEmpty(currentDetail)) selectedIndex = tile.ItemIndex;
        EnsureVisible(tile);
        if (Application.isPlaying && EventSystem.current != null && tile.gameObject.activeInHierarchy)
        {
            selecting = true;
            EventSystem.current.SetSelectedGameObject(tile.gameObject);
            selecting = false;
        }
    }

    private void EnsureVisible(RayBanTile tile)
    {
        if (tile.PageId != 2 || appsContent == null || appsViewport == null || !string.IsNullOrEmpty(currentDetail)) return;
        float top = -((RectTransform)tile.transform).anchoredPosition.y - ((RectTransform)tile.transform).sizeDelta.y * .5f;
        float bottom = top + ((RectTransform)tile.transform).sizeDelta.y;
        float offset = appsContent.anchoredPosition.y;
        float height = appsViewport.rect.height;
        if (top < offset + 10) offset = top - 10;
        if (bottom > offset + height - 24) offset = bottom - height + 24;
        offset = Mathf.Clamp(offset, 0, Mathf.Max(0, appsContent.rect.height - height));
        appsContent.anchoredPosition = new Vector2(0, offset);
        if (appScroll != null) appScroll.StopMovement();
        UpdateScrollFade();
    }

    private void BindScroll()
    {
        if (appScroll == null) return;
        appScroll.onValueChanged.RemoveListener(OnScrollChanged);
        appScroll.onValueChanged.AddListener(OnScrollChanged);
    }

    private void OnScrollChanged(Vector2 _) => UpdateScrollFade();

    private void UpdateScrollFade()
    {
        if (appsContent == null || appsViewport == null) return;
        float offset = appsContent.anchoredPosition.y;
        float height = appsViewport.rect.height;
        foreach (var tile in tiles)
        {
            if (tile == null || tile.PageId != 2) continue;
            var rect = (RectTransform)tile.transform;
            float top = -rect.anchoredPosition.y - rect.sizeDelta.y * .5f - offset;
            float bottom = top + rect.sizeDelta.y;
            float shown = Mathf.Max(0, Mathf.Min(bottom, height) - Mathf.Max(top, 0));
            float alpha = Mathf.Clamp01(shown / Mathf.Max(1, rect.sizeDelta.y));
            tile.GetComponent<CanvasGroup>().alpha = alpha;
        }
    }

    private void UpdateDots()
    {
        for (int i = 0; i < dots.Count; i++)
        {
            bool active = i == currentPage;
            dots[i].color = new Color(1, 1, 1, active ? .85f : .35f);
            dots[i].rectTransform.sizeDelta = Vector2.one * (active ? 12 : 8);
            dots[i].rectTransform.anchoredPosition = new Vector2(32 + i * 18 - (active ? 2 : 0), active ? -26 : -28);
        }
    }

    private void BuildApps(Transform parent)
    {
        appsViewport = Rect("App viewport", parent, 0, 11, 600, 517);
        appsViewport.gameObject.AddComponent<RectMask2D>();
        appsContent = Rect("App grid", appsViewport, 0, 0, 600, 792);
        appScroll = appsViewport.gameObject.AddComponent<ScrollRect>();
        appScroll.viewport = appsViewport;
        appScroll.content = appsContent;
        appScroll.horizontal = false;
        appScroll.vertical = true;
        appScroll.movementType = ScrollRect.MovementType.Clamped;
        appScroll.inertia = true;
        appScroll.scrollSensitivity = 30;
        for (int i = 0; i < AppNames.Length; i++)
        {
            var tile = Tile(AppNames[i], AppSymbols[i], appsContent, 2, i, i / 2, i % 2, 16 + i % 2 * 286, i / 2 * 132, 282, 120, AppColors[i]);
            tile.gameObject.AddComponent<CanvasGroup>();
        }
        EnsureLauncherOrder();
    }

    private void BuildQuickSettings(Transform parent)
    {
        Glyph("Headset battery", parent, "battery", 30, 7, 32, 24);
        batteryLabel = Label("Quest battery", parent, RayBanAppSettings.BatteryLabel, 70, 2, 400, 34, 20, FontStyles.Normal);
        Label("Settings scope", parent, "APP", 501, 7, 66, 24, 14, FontStyles.Bold, new Color(1,1,1,.55f));
        Tile("Captions", "captions", parent, 0, 0, 0, 0, 18, 55, 132, 105, AppColors[8], false);
        Tile("Camera", "camera", parent, 0, 1, 0, 1, 162, 55, 132, 105, AppColors[5], false);
        Tile("Music", "music", parent, 0, 2, 0, 2, 306, 55, 276, 105, AppColors[6]);
        Tile("Volume", "volume", parent, 0, 3, 1, 0, 18, 172, 276, 105, new Color(.35f,.64f,.80f));
        Tile("Brightness", "brightness", parent, 0, 4, 1, 2, 306, 172, 276, 105, new Color(.35f,.64f,.80f));
        Tile("Audio only", "audioonly", parent, 0, 5, 2, 0, 18, 289, 276, 105, new Color(.35f,.64f,.80f));
        Tile("Do not\ndisturb", "donotdisturb", parent, 0, 6, 2, 2, 306, 289, 276, 105, new Color(.35f,.64f,.80f));
        Tile("Settings", "settings", parent, 0, 7, 3, 0, 18, 406, 276, 100, new Color(.35f,.64f,.80f));
        Tile("Close display", "close", parent, 0, 8, 3, 2, 306, 406, 276, 100, new Color(.35f,.64f,.80f)).ActionId = "hide";
    }

    private void BuildNotifications(Transform parent)
    {
        dateLabel = Label("Date and time", parent, "", 110, 0, 380, 38, 22, FontStyles.Normal, new Color(1,1,1,.83f));
        dateLabel.alignment = TextAlignmentOptions.Center;
        string[] names = { "Barbara Johnson", "Alicia Dayton", "Carl Rogers", "Reminder" };
        string[] messages = { "Yes! Let’s do it!", "5 messages", "Let’s meet at 3pm", "Check up with Sheena" };
        string[] times = { "1m", "2m", "7m", "12m" };
        for (int i = 0; i < names.Length; i++)
        {
            var tile = Tile(names[i], i == 3 ? "calendar" : "person", parent, 1, i, i, 0, 30, 58 + i * 112, 540, 96, AppColors[(i * 2) % AppColors.Length], false);
            var icon = tile.transform.Find("Icon") as RectTransform;
            icon.anchoredPosition = new Vector2(20, -18);
            tile.IconGlyph.anchoredPosition = new Vector2(51, -49);
            Label("Sender", tile.transform, names[i], 100, 9, 335, 40, 25, FontStyles.Normal);
            Label("Message", tile.transform, messages[i], 100, 46, 355, 36, 21, FontStyles.Normal, new Color(1,1,1,.87f));
            var time = Label("Time", tile.transform, times[i], 450, 13, 65, 36, 21, FontStyles.Normal, new Color(1,1,1,.80f));
            time.alignment = TextAlignmentOptions.Right;
        }
        Label("Sample notification caption", parent, "Sample notifications", 30, 509, 540, 18, 13, FontStyles.Normal, new Color(1,1,1,.45f)).alignment = TextAlignmentOptions.Center;
    }

    private void BuildDetail()
    {
        detailRoot = Rect("App preview", visualRoot, 0, 0, 600, 600).gameObject;
        featureScreens = detailRoot.AddComponent<RayBanFeatureScreen>();
        featureScreens.Build(this, font, samplePhotos);
        detailRoot.SetActive(false);
    }

    private RayBanTile Tile(string title, string symbol, Transform parent, int page, int index, int row, int column, float x, float y, float width, float height, Color accent, bool showLabel = true)
    {
        var surface = Surface(title + " tile", parent, x, y, width, height, Card, 30);
        surface.Tint = accent;
        var tileRect = (RectTransform)surface.transform;
        tileRect.pivot = new Vector2(.5f, .5f);
        tileRect.anchoredPosition = new Vector2(x + width * .5f, -y - height * .5f);
        var tile = surface.gameObject.AddComponent<RayBanTile>();
        tile.Shell = this;
        tile.Surface = surface;
        tile.PageId = page;
        tile.ItemIndex = index;
        tile.Row = row;
        tile.Column = column;
        tile.Title = title;
        tile.Symbol = symbol;
        tile.ActionId = "app";
        tile.targetGraphic = surface;
        tile.transition = Selectable.Transition.None;
        tile.navigation = new Navigation { mode = Navigation.Mode.None };
        float iconX = showLabel ? 20 : (width - 62) / 2;
        var icon = Surface("Icon", tile.transform, iconX, (height - 62) / 2, 62, 62, Color.white, 31);
        icon.Circle = true;
        icon.Tint = accent;
        icon.Focus = .78f;
        var glyph = Glyph("Glyph", tile.transform, symbol, iconX - 1, (height - 64) / 2, 64, 64);
        var glyphRect = (RectTransform)glyph.transform;
        glyphRect.pivot = new Vector2(.5f, .5f);
        glyphRect.anchoredPosition += new Vector2(32, -32);
        RectTransform labelRect = null;
        if (showLabel)
        {
            var label = Label("Label", tile.transform, title, 96, 12, width - 110, height - 24, 24, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            labelRect = (RectTransform)label.transform;
        }
        tile.ConfigureVisuals(icon, glyphRect, labelRect);
        if (page >= 0) tiles.Add(tile);
        return tile;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RayBanSurface Surface(string name, Transform parent, float x, float y, float width, float height, Color color, float radius)
    {
        var surface = Rect(name, parent, x, y, width, height).gameObject.AddComponent<RayBanSurface>();
        surface.color = color;
        surface.Tint = color;
        surface.Radius = radius;
        surface.raycastTarget = name.EndsWith(" tile", StringComparison.Ordinal);
        return surface;
    }

    private static RayBanGlyph Glyph(string name, Transform parent, string symbol, float x, float y, float width, float height)
    {
        var glyph = Rect(name, parent, x, y, width, height).gameObject.AddComponent<RayBanGlyph>();
        glyph.Symbol = symbol;
        glyph.color = TextColor;
        glyph.raycastTarget = false;
        return glyph;
    }

    private TMP_Text Label(string name, Transform parent, string text, float x, float y, float width, float height, float size, FontStyles style, Color? color = null)
    {
        var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color ?? TextColor;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
}
