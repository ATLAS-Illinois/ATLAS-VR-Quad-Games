using UnityEngine;

public class SmoothFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Usually the HMD or a rig child that tracks head pose")]
    public Transform target;

    [Header("Placement")]
    [Tooltip("Meters in front of the target, projected on the horizontal plane")]
    public float distance = 1.5f;
    [Tooltip("Vertical offset from the target position")]
    public float heightOffset = 0.0f;

    [Header("Smoothing")]
    [Tooltip("SmoothDamp time for position. 0 means snap")]
    public float positionSmoothTime = 0.12f;
    [Tooltip("Rotation interpolation speed in 1 per second. 0 means snap")]
    public float rotationLerpSpeed = 12f;

    [Header("Rotation")]
    [Tooltip("Fixed local X euler tilt added after yaw so you can lean the canvas up or down")]
    public float localXRotationOffset = 0.0f;

    [Header("Safety")]
    [Tooltip("If true, movement is disabled when no OVR headset is present")]
    public bool requireOVRHeadset = true;
    public bool onlyUseYRotation = true;

    // internal state
    Vector3 _vel;                 // SmoothDamp velocity
    Vector3 _lastFlatForward = Vector3.forward;

    private void Start()
    {
        // Assuming your variable is named 'target'
        if (target == null)
        {
            GameObject centerEye = GameObject.Find("CenterEyeAnchor");
            if (centerEye != null) target = centerEye.transform;
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        if (requireOVRHeadset && !IsOvrHeadsetPresent())
        {
            return; // do not move or rotate
        }

        // Compute horizontal forward from target
        Vector3 flatForward = Vector3.forward;
        
        if(onlyUseYRotation)
        {
            flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        }
        else
        {
            flatForward = target.forward;
        }

        if (flatForward.sqrMagnitude < 1e-6f)
        {
            flatForward = _lastFlatForward;
        }
        else
        {
            flatForward.Normalize();
            _lastFlatForward = flatForward;
        }

        // Desired position: in front on the horizontal plane, with height offset
        Vector3 basePos = target.position;
        Vector3 desiredPos = basePos + flatForward * distance;
        desiredPos.y = basePos.y + heightOffset;

        // Smooth position
        if (positionSmoothTime > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _vel, positionSmoothTime);
        else
            transform.position = desiredPos;

        // Yaw-only look at target
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 1e-6f)
        {
            // Fallback to face along flatForward opposite direction
            toTarget = -flatForward;
        }

        Quaternion yaw = Quaternion.LookRotation(-toTarget.normalized, Vector3.up);

        // Apply local X tilt after yaw
        Quaternion targetRot = Quaternion.identity;
        if (onlyUseYRotation)
        {
            targetRot = yaw * Quaternion.Euler(localXRotationOffset, 0f, 0f);
        }
        else
        {
            targetRot = Quaternion.LookRotation(-toTarget.normalized, target.up);
        }

        // Smooth rotation
        if (rotationLerpSpeed > 0f)
        {
            float t = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }
        else
        {
            transform.rotation = targetRot;
        }
    }

    // Basic OVR presence check
    bool IsOvrHeadsetPresent()
    {
        // OVRManager is part of Meta SDK - this returns false if headset not detected
        // If your project uses OpenXR with OVR backend, this still reports presence.
        return OVRManager.isHmdPresent;
    }
}
