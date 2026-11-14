using System.Collections.Generic;
using UnityEngine;

/// <summary>Adapts the existing landmark registry to offline scene waypoint guidance.</summary>
[DisallowMultipleComponent]
public sealed class RayBanMapBridge : MonoBehaviour
{
    public VRMapNavigator navigator;
    private readonly Dictionary<GameObject, bool> activeStates = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Behaviour, bool> componentStates = new Dictionary<Behaviour, bool>();
    private readonly List<GameObject> ancestorChain = new List<GameObject>();
    private bool acquired, quitting;
    private bool arrivalNotified;
    private int selectedIndex;
    private readonly QuadWaypointRouter router = new QuadWaypointRouter();
    private QuadWaypointMap mapData;
    private Transform playerRoot;

    public bool IsAcquired => acquired;
    public GameObject LegacyRoot => navigator != null ? navigator.gameObject : null;
    public int LandmarkCount => navigator != null ? navigator.LandmarkCount : 0;
    public int SelectedIndex => selectedIndex;
    public string SelectedName => navigator != null ? navigator.GetLandmarkName(selectedIndex) : "No destination";
    public bool IsNavigating => acquired && navigator != null && navigator.IsNavigating;
    public string DestinationName => navigator != null ? navigator.DestinationName : "";
    public bool HasPlayer => acquired && navigator != null && navigator.playerCamera != null;
    public QuadWaypointMap MapData => mapData;
    public IReadOnlyList<Vector3> RoutePoints => router.RoutePoints;
    public bool HasRoute => acquired && router.HasRoute;
    public bool Arrived => HasRoute && router.Arrived;
    public Vector3 PlayerPosition => HasPlayer ? navigator.playerCamera.position : Vector3.zero;
    public Vector3 PlayerForward => HasPlayer ? navigator.playerCamera.forward : Vector3.forward;
    public bool HasSelectedPosition => TrySelectedPosition(out _);
    public Vector3 SelectedPosition => TrySelectedPosition(out Vector3 position) ? position : Vector3.zero;
    public float Distance => HasRoute ? router.RemainingDistance : 0f;
    public string Instruction => IsNavigating ? router.Instruction : "Choose a Quad landmark";
    public float HeadingAngle
    {
        get
        {
            if (!HasPlayer || !HasRoute || Arrived) return 0f;
            Vector3 forward = Vector3.ProjectOnPlane(PlayerForward, Vector3.up);
            Vector3 direction = Vector3.ProjectOnPlane(router.GuidancePosition - PlayerPosition, Vector3.up);
            return forward.sqrMagnitude > .0001f && direction.sqrMagnitude > .0001f
                ? Vector3.SignedAngle(forward, direction, Vector3.up) : 0f;
        }
    }
    public string Status => !acquired ? "Maps is not connected"
        : LandmarkCount == 0 ? "No local landmarks are configured"
        : !HasPlayer ? "Waiting for player position…"
        : IsNavigating ? (Arrived ? "Arrived at " + DestinationName : router.Instruction)
        : mapData == null ? "Waypoint map is unavailable" : "Choose a Quad landmark";

    private bool TrySelectedPosition(out Vector3 position)
    {
        if (mapData != null && mapData.TryGetDestination(SelectedName, out position)) return true;
        if (navigator != null) return navigator.TryGetLandmarkPosition(selectedIndex, out position);
        position = default;
        return false;
    }

    public bool Acquire(RectTransform unusedHost)
    {
        if (acquired) return true;
        if (!Application.isPlaying || quitting || navigator == null) return false;
        ancestorChain.Clear();
        for (var node = navigator.transform; node != null && !transform.IsChildOf(node); node = node.parent)
        {
            activeStates[node.gameObject] = node.gameObject.activeSelf;
            ancestorChain.Add(node.gameObject);
        }
        // Keep backend data and its existing hidden labels; suppress old visuals
        // and focus/input handlers while the redesigned screen owns interaction.
        foreach (var component in navigator.GetComponentsInChildren<Behaviour>(true))
        {
            componentStates[component] = component.enabled;
            component.enabled = component == navigator;
        }
        acquired = true;
        for (int i = ancestorChain.Count - 1; i >= 0; i--) ancestorChain[i].SetActive(true);
        navigator.EnsureInitialized();
        mapData = Resources.Load<QuadWaypointMap>("Quad_WaypointMap");
        if (mapData != null && mapData.scenePath != navigator.gameObject.scene.path) mapData = null;
        ResolvePlayerRoot();
        if (navigator.SelectedLandmark >= 0) selectedIndex = navigator.SelectedLandmark;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, LandmarkCount - 1));
        navigator.StopNavigation();
        router.Stop();
        arrivalNotified = false;
        return true;
    }

    public bool Available(string action)
    {
        if (!acquired || navigator == null) return false;
        switch (action)
        {
            case "maps.previous":
            case "maps.next": return LandmarkCount > 1;
            case "maps.start": return LandmarkCount > 0 && HasPlayer;
            case "maps.end": return IsNavigating;
            case "maps.recalculate": return IsNavigating && HasPlayer;
            default: return false;
        }
    }

    /// <summary>Select an authored destination without starting a route or changing a failed selection.</summary>
    public bool SelectLandmark(string name)
    {
        if (!acquired || navigator == null || string.IsNullOrEmpty(name)) return false;
        for (int i = 0; i < LandmarkCount; i++)
        {
            if (!string.Equals(navigator.GetLandmarkName(i), name, System.StringComparison.Ordinal)) continue;
            StopGuidance();
            selectedIndex = i;
            return true;
        }
        return false;
    }

    public void Execute(string action)
    {
        if (!Available(action)) return;
        switch (action)
        {
            case "maps.previous":
                StopGuidance();
                selectedIndex = (selectedIndex + LandmarkCount - 1) % LandmarkCount;
                break;
            case "maps.next":
                StopGuidance();
                selectedIndex = (selectedIndex + 1) % LandmarkCount;
                break;
            case "maps.start":
            case "maps.recalculate":
                if (action == "maps.start") arrivalNotified = false;
                ResolvePlayerRoot();
                if (navigator.SelectDestination(selectedIndex))
                    router.Build(mapData, navigator.gameObject.scene.path, PlayerPosition, playerRoot, SelectedName);
                break;
            case "maps.end": StopGuidance(); break;
        }
    }

    private void Update()
    {
        if (IsNavigating && HasPlayer) router.Update(PlayerPosition, Time.unscaledTime);
        if (IsNavigating && Arrived && !arrivalNotified)
        {
            arrivalNotified = true;
            RayBanLocalNotifications.Publish(QuadNotificationKind.RouteArrived, "Destination reached", "Arrived at " + DestinationName + " in the virtual Quad.");
        }
    }

    private void ResolvePlayerRoot()
    {
        // Ignore only the locomotion/player hierarchy in physics connectors.
        // The assistant shell can belong to a completely separate scene root.
        if (navigator == null || navigator.playerCamera == null) { playerRoot = null; return; }
        var controller = navigator.playerCamera.GetComponentInParent<CharacterController>();
        playerRoot = controller != null ? controller.transform : navigator.playerCamera.root;
    }

    private void StopGuidance()
    {
        arrivalNotified = false;
        router.Stop();
        if (navigator != null) navigator.StopNavigation();
    }

    public void Release(bool restore = true)
    {
        if (!acquired) return;
        StopGuidance();
        acquired = false;
        playerRoot = null;
        if (navigator != null) navigator.gameObject.SetActive(false);
        foreach (var state in componentStates) if (state.Key != null) state.Key.enabled = state.Value;
        componentStates.Clear();
        if (restore)
            foreach (var state in activeStates) if (state.Key != null) state.Key.SetActive(state.Value);
        activeStates.Clear();
        ancestorChain.Clear();
    }

    private void OnDisable() => Release(!quitting);
    private void OnApplicationQuit() { quitting = true; Release(false); }
}
