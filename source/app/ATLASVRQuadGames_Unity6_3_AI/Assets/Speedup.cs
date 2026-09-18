using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Speedup : MonoBehaviour
{
    [SerializeField] private OVRPlayerController controller;
    [SerializeField] private DetectIfGrabbed dig;
    [SerializeField] private EnableCollision ghostifyTopPlane;
    private bool isSpeedy = false;

    private void Start()
    {
        ghostifyTopPlane.DisableCollision();
    }

    // Update is called once per frame
    void Update()
    {
        if (!isSpeedy && dig.IsGrabbed)
        {
            isSpeedy = true;    
            controller.Acceleration = 10f;
            ghostifyTopPlane.TurnOnCollision();
        } else if (isSpeedy && !dig.IsGrabbed) {
            isSpeedy = false;   
            controller.Acceleration = 0.25f;
            ghostifyTopPlane.DisableCollision();
        }
    }
}
