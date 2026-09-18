using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchorFollow : MonoBehaviour
{
    public Transform camera; // Use CenterEyeAnchor to read yaw rotation
    public Transform rigRoot; // Use TrackingSpace for player body reference
    public Vector3 localOffset; // Position offset for the quiver anchor spot
    void LateUpdate()
    {
        if (camera == null || rigRoot == null) return;
        {
        // Get camera yaw only
        float yaw = camera.eulerAngles.y;
        Quaternion yawRotation = Quaternion.Euler(0, yaw, 0);

        // Anchor point rotates with player/controller head turning (yaw) only, stays at stable height
        transform.position = rigRoot.position + yawRotation * localOffset;
        transform.rotation = yawRotation;
        }
    }
}
