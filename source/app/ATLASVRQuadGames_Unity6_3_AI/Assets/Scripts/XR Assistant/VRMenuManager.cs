using UnityEngine;

public class VRMenuManager : MonoBehaviour
{
    [Header("Core Reference")]
    [Tooltip("Drag your main Menu Canvas or UI Root here")]
    public GameObject uiCanvasRoot;

    private void Update()
    {
        // OVRInput.Button.Start maps to the physical 'Menu' button on the Left Quest Controller
        // OVRInput.GetDown ensures it only triggers once per press, not continuously
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            ToggleMenu();
        }
    }
     
    // This flips the canvas on and off based on its current state
    public void ToggleMenu()
    {
        if (uiCanvasRoot != null)
        {
            bool isCurrentlyActive = uiCanvasRoot.activeSelf;
            uiCanvasRoot.SetActive(!isCurrentlyActive);
        }
    }

    // Your UI 'X' Button will call this specific function
    public void CloseMenu()
    {
        if (uiCanvasRoot != null)
        {
            uiCanvasRoot.SetActive(false);
        }
    }
}