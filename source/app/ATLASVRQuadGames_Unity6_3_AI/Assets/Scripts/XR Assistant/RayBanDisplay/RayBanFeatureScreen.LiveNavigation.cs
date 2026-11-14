using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class RayBanFeatureScreen
{
    [SerializeField] private RectTransform quadMapRoot;
    [SerializeField] private TMP_Text quadMapStatus, quadMapSelection, quadMapCount;
    [SerializeField] private TMP_Text quadMapDestination, quadMapDistance;
    [SerializeField] private QuadOverheadMapGraphic quadOverheadMap;

    private void BuildLiveNavigation(Transform page)
    {
        quadMapRoot = Rect("Quad landmark navigation", page, 0, 84, 600, 516);
        quadMapStatus = Text(quadMapRoot, "Local navigation status", "Choose a Quad landmark", 24, 0, 552, 30, 18);
        var selection = Panel(quadMapRoot, "Landmark selection", 20, 34, 560, 64);
        quadMapSelection = Text(selection.transform, "Selected landmark", "Choose a destination", 16, 5, 528, 33, 26, true);
        quadMapSelection.enableAutoSizing = true;
        quadMapSelection.fontSizeMin = 18;
        quadMapSelection.fontSizeMax = 26;
        quadMapSelection.textWrappingMode = TextWrappingModes.NoWrap;
        quadMapCount = Text(selection.transform, "Landmark count", "Quad landmark registry", 16, 36, 528, 26, 16);
        Button(quadMapRoot, "Previous landmark", "maps.previous", "Previous", "previous", 20, 108, 274, 46, 1, 0);
        Button(quadMapRoot, "Next landmark", "maps.next", "Next", "next", 306, 108, 274, 46, 1, 1);

        var mapFrame = Panel(quadMapRoot, "Overhead map frame", 20, 164, 560, 204, Color.white, 16);
        mapFrame.Flat = true;
        mapFrame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Bar(mapFrame.transform, "Map backing", 0, 0, 560, 204, new Color(.075f, .13f, .16f));
        var mapImage = Rect("Baked campus map", mapFrame.transform, 0, 0, 560, 204).gameObject.AddComponent<RawImage>();
        mapImage.raycastTarget = false;
        mapImage.enabled = false;
        quadOverheadMap = Rect("Player and waypoint route", mapFrame.transform, 0, 0, 560, 204)
            .gameObject.AddComponent<QuadOverheadMapGraphic>();
        quadOverheadMap.SetBackground(mapImage);
        quadOverheadMap.raycastTarget = false;
        var north = Panel(mapFrame.transform, "North indicator", 518, 8, 34, 50, new Color(.07f, .13f, .19f, .9f), 10);
        north.Flat = true;
        Text(north.transform, "North label", "N", 6, 0, 22, 30, 18, true).alignment = TextAlignmentOptions.Center;
        Glyph(north.transform, "North arrow", "up", 10, 28, 14, 14);

        quadMapDestination = Text(quadMapRoot, "Route instruction", "Select Navigate to find a pathway", 24, 374, 552, 30, 20);
        quadMapDestination.enableAutoSizing = true;
        quadMapDestination.fontSizeMin = 17;
        quadMapDestination.fontSizeMax = 20;
        quadMapDestination.textWrappingMode = TextWrappingModes.NoWrap;
        quadMapDistance = Text(quadMapRoot, "Remaining route distance", "", 24, 407, 552, 38, 25, true);
        Button(quadMapRoot, "Navigate to landmark", "maps.start", "Navigate", "map", 20, 452, 274, 56, 2, 0);
        Button(quadMapRoot, "End landmark navigation", "maps.end", "End", "close", 306, 452, 274, 56, 2, 1);
        foreach (var value in new[] { quadMapStatus, quadMapSelection, quadMapCount, quadMapDestination, quadMapDistance })
            value.richText = false;
    }

#if UNITY_EDITOR
    // Update only this authored feature branch; retain other screens and Back.
    public bool RebuildLiveNavigationView()
    {
        if (Application.isPlaying) return false;
        var view = views.Find(v => v.key == "maps");
        if (view == null || view.root == null) return false;
        var oldRoot = quadMapRoot != null ? quadMapRoot : view.root.Find("Quad landmark navigation");
        if (oldRoot != null)
        {
            controls.RemoveAll(control => control == null || control.transform.IsChildOf(oldRoot));
            DestroyImmediate(oldRoot.gameObject);
        }
        BuildLiveNavigation(view.root);
        return true;
    }
#endif

    private void ShowLiveNavigation(bool visible)
    {
        var bridge = shell != null ? shell.mapBridge : null;
        if (!visible)
        {
            if (bridge != null) bridge.Release();
            return;
        }
        if (bridge != null) bridge.Acquire(quadMapRoot);
        if (shell != null && shell.appSettings != null) shell.appSettings.BindVisuals(shell.VisualRoot);
        if (quadOverheadMap != null) quadOverheadMap.ResetViewport();
        TickLiveNavigation();
    }

    private bool HandleLiveNavigation(string action)
    {
        if (string.IsNullOrEmpty(action) || !action.StartsWith("maps.")) return false;
        if (shell != null && shell.mapBridge != null) shell.mapBridge.Execute(action);
        TickLiveNavigation();
        return true;
    }

    private void TickLiveNavigation()
    {
        if (shell == null || !shell.UsesLiveFeatures || CurrentView != "maps" || quadMapRoot == null) return;
        var bridge = shell.mapBridge;
        bool available = bridge != null && bridge.IsAcquired;
        bool navigating = available && bridge.IsNavigating;
        quadMapStatus.text = available && bridge.HasPlayer && bridge.MapData != null ? "Quad pathways · offline"
            : bridge != null ? bridge.Status : "Maps is not connected";
        quadMapSelection.text = available && bridge.LandmarkCount > 0 ? bridge.SelectedName : "No landmarks available";
        quadMapCount.text = available && bridge.LandmarkCount > 0
            ? (bridge.SelectedIndex + 1) + " of " + bridge.LandmarkCount + " · North-up campus map"
            : "Add destinations in the Landmark Registry.";
        quadMapDestination.text = available ? navigating ? bridge.Instruction : "Select Navigate to find a pathway" : "Map data unavailable";
        quadMapDistance.text = navigating && bridge.HasPlayer && bridge.HasRoute
            ? bridge.Arrived ? "Arrived at destination" : Mathf.CeilToInt(bridge.Distance) + " m remaining"
            : navigating ? bridge.HasPlayer ? "No route available" : "Position unavailable" : "";
        if (quadOverheadMap != null)
            quadOverheadMap.SetState(available ? bridge.MapData : null,
                available ? bridge.RoutePoints : null,
                available ? bridge.PlayerPosition : Vector3.zero, available && bridge.HasPlayer,
                available ? bridge.PlayerForward : Vector3.forward,
                available ? bridge.SelectedPosition : Vector3.zero, available && bridge.HasSelectedPosition,
                available && bridge.HasRoute, available ? bridge.SelectedIndex : -1);
        foreach (var control in controls)
            if (control != null && control.ActionId != null && control.ActionId.StartsWith("maps."))
                SetControlAvailable(control.ActionId, available && bridge.Available(control.ActionId));

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (available && bridge.LegacyRoot != null && selected != null && selected.transform.IsChildOf(bridge.LegacyRoot.transform))
        {
            FocusAction("back");
            FocusAction("maps.start");
        }
    }
}
