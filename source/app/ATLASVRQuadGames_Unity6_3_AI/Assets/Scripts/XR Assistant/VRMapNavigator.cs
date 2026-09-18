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

    private bool isNavigating = false;
    private Vector3 currentTargetPosition;

    // We cache the list here when the game starts
    private List<Landmark> activeLandmarks = new List<Landmark>();

    private void Start()
    {
        // Auto-link the camera if the Inspector slot got cleared!
        if (playerCamera == null)
        {
            // First try the standard Unity Main Camera
            if (Camera.main != null)
            {
                playerCamera = Camera.main.transform;
            }
            else
            {
                // Fallback: specifically hunt for the Meta Quest CenterEyeAnchor
                GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) playerCamera = centerEye.transform;
            }
        }
        // 1. Fetch the global Map Settings file we created with the Editor tool
        MapConfig config = Resources.Load<MapConfig>("Map_Settings");
        if (config != null)
        {
            activeLandmarks = config.landmarks;
        }
        else
        {
            Debug.LogError("[VRMapNavigator] Map_Settings not found! Open ATLAS VR -> Landmark Registry to generate it.");
        }

        // 2. Populate the dropdown
        destinationDropdown.ClearOptions();
        List<string> options = new List<string> { "Select a building..." };

        foreach (var landmark in activeLandmarks)
        {
            options.Add(landmark.name);
        }
        destinationDropdown.AddOptions(options);

        // 3. Hook up events and set defaults
        destinationDropdown.onValueChanged.AddListener(OnDestinationSelected);
        distanceText.text = "Select destination";
        arrowIcon.gameObject.SetActive(false);
    }

    private void OnDestinationSelected(int index)
    {
        if (index == 0)
        {
            StopNavigation();
            return;
        }

        currentTargetPosition = activeLandmarks[index - 1].position;
        isNavigating = true;
        arrowIcon.gameObject.SetActive(true);
    }

    public void StopNavigation()
    {
        isNavigating = false;
        arrowIcon.gameObject.SetActive(false);
        distanceText.text = "Select destination";
        destinationDropdown.value = 0;
    }

    private void Update()
    {
        if (!isNavigating || playerCamera == null || arrowIcon == null) return;

        float distance = Vector3.Distance(playerCamera.position, currentTargetPosition);
        distanceText.text = Mathf.RoundToInt(distance).ToString() + "m";

        Vector3 playerForward = playerCamera.forward;
        playerForward.y = 0;
        playerForward.Normalize();

        Vector3 directionToTarget = currentTargetPosition - playerCamera.position;
        directionToTarget.y = 0;
        directionToTarget.Normalize();

        float angle = Vector3.SignedAngle(playerForward, directionToTarget, Vector3.up);
        arrowIcon.localEulerAngles = new Vector3(0, 0, -angle);
    }
}