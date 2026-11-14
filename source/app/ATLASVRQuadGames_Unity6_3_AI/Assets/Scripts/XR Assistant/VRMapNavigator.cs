using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class VRMapNavigator : MonoBehaviour
{
    [Header("Core References")]
    public Transform playerCamera;

    [Header("UI Elements")]
    public TextMeshProUGUI distanceText;
    public RectTransform arrowIcon;
    public TMP_Dropdown destinationDropdown;

    private bool isNavigating;
    private Vector3 currentTargetPosition;
    private List<Landmark> activeLandmarks = new List<Landmark>();
    private bool initialized;
    private int selectedLandmark = -1;

    // The display adapter reads the existing local landmark data. This is a
    // direction and straight-line distance indicator, not pedestrian pathfinding.
    public bool IsInitialized => initialized;
    public bool IsNavigating => isNavigating;
    public int LandmarkCount => activeLandmarks.Count;
    public int SelectedLandmark => selectedLandmark;
    public string DestinationName => GetLandmarkName(selectedLandmark);
    public float DistanceToDestination => isNavigating && playerCamera != null
        ? Vector3.Distance(playerCamera.position, currentTargetPosition) : 0f;
    public float HeadingAngle
    {
        get
        {
            if (!isNavigating || playerCamera == null) return 0f;
            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.forward, Vector3.up);
            Vector3 direction = Vector3.ProjectOnPlane(currentTargetPosition - playerCamera.position, Vector3.up);
            return forward.sqrMagnitude > .0001f && direction.sqrMagnitude > .0001f
                ? Vector3.SignedAngle(forward, direction, Vector3.up) : 0f;
        }
    }

    private void Start() => EnsureInitialized();

    public void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        ResolvePlayerCamera();
        MapConfig config = Resources.Load<MapConfig>("Map_Settings");
        if (config != null && config.landmarks != null)
            activeLandmarks = config.landmarks;
        else
            Debug.LogWarning("[VRMapNavigator] Local landmark registry is unavailable.");

        if (destinationDropdown != null)
        {
            destinationDropdown.ClearOptions();
            List<string> options = new List<string> { "Select a building..." };
            foreach (var landmark in activeLandmarks) options.Add(landmark.name);
            destinationDropdown.AddOptions(options);
            destinationDropdown.onValueChanged.AddListener(OnDestinationSelected);
        }
        if (distanceText != null) distanceText.text = "Select destination";
        if (arrowIcon != null) arrowIcon.gameObject.SetActive(false);
    }

    private void ResolvePlayerCamera()
    {
        if (playerCamera != null) return;
        if (Camera.main != null) playerCamera = Camera.main.transform;
        else
        {
            GameObject centerEye = GameObject.Find("CenterEyeAnchor");
            if (centerEye != null) playerCamera = centerEye.transform;
        }
    }

    public string GetLandmarkName(int index) => index >= 0 && index < activeLandmarks.Count
        ? activeLandmarks[index].name : "Choose a destination";

    public bool TryGetLandmarkPosition(int index, out Vector3 position)
    {
        if (index >= 0 && index < activeLandmarks.Count)
        {
            position = activeLandmarks[index].position;
            return true;
        }
        position = default;
        return false;
    }

    public bool SelectDestination(int index)
    {
        EnsureInitialized();
        if (index < 0 || index >= activeLandmarks.Count) return false;
        selectedLandmark = index;
        currentTargetPosition = activeLandmarks[index].position;
        isNavigating = true;
        if (arrowIcon != null) arrowIcon.gameObject.SetActive(true);
        if (destinationDropdown != null) destinationDropdown.SetValueWithoutNotify(index + 1);
        UpdatePresentation();
        return true;
    }

    private void OnDestinationSelected(int index)
    {
        if (index == 0) StopNavigation();
        else SelectDestination(index - 1);
    }

    public void StopNavigation()
    {
        isNavigating = false;
        selectedLandmark = -1;
        if (arrowIcon != null) arrowIcon.gameObject.SetActive(false);
        if (distanceText != null) distanceText.text = "Select destination";
        if (destinationDropdown != null) destinationDropdown.SetValueWithoutNotify(0);
    }

    private void Update()
    {
        if (playerCamera == null) ResolvePlayerCamera();
        UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        if (!isNavigating || playerCamera == null) return;
        if (distanceText != null) distanceText.text = Mathf.RoundToInt(DistanceToDestination) + "m";
        if (arrowIcon != null) arrowIcon.localEulerAngles = new Vector3(0, 0, -HeadingAngle);
    }

    private void OnDestroy()
    {
        if (initialized && destinationDropdown != null)
            destinationDropdown.onValueChanged.RemoveListener(OnDestinationSelected);
    }
}
