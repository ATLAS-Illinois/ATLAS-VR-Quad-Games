using UnityEngine;

public class SmoothFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Usually the HMD or a rig child that tracks head pose")]
    public Transform target;

    [Header("Placement")]
    [Tooltip("Meters in front of the target; horizontal when Only Use Y Rotation is enabled")]
    public float distance = 1.5f;
    [Tooltip("Offset along the target's up direction, or world up in yaw-only mode")]
    public float heightOffset = 0.0f;

    [Header("Smoothing")]
    [Tooltip("SmoothDamp time for position. 0 means snap")]
    public float positionSmoothTime = 0.12f;
    [Tooltip("Rotation interpolation speed in 1 per second. 0 means snap")]
    public float rotationLerpSpeed = 12f;

    [Header("Rotation")]
    [Tooltip("Fixed local X tilt added after the followed rotation")]
    public float localXRotationOffset = 0.0f;

    [Header("Safety")]
    [Tooltip("If true, movement is disabled when no OVR headset is present")]
    public bool requireOVRHeadset = true;
    [Tooltip("Follow heading on the horizontal plane. Disable to keep the display in the same head-relative position while looking up or down.")]
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

        Vector3 flatForward = onlyUseYRotation ? StableFlatForward() : Vector3.zero;
        // Full-pose mode must retain both pitch and roll in the offset. Replacing
        // world Y here collapses the panel into the user's head near vertical gaze.
        Vector3 desiredPos = onlyUseYRotation
            ? target.position + flatForward * distance + Vector3.up * heightOffset
            : target.position + target.forward * distance + target.up * heightOffset;

        // Smooth position
        if (positionSmoothTime > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _vel, positionSmoothTime);
        else
            transform.position = desiredPos;

        Quaternion targetRot;
        if (onlyUseYRotation)
        {
            Vector3 fromTarget = transform.position - target.position;
            fromTarget.y = 0f;
            if (fromTarget.sqrMagnitude < 1e-6f) fromTarget = flatForward;
            targetRot = Quaternion.LookRotation(fromTarget.normalized, Vector3.up)
                * Quaternion.Euler(localXRotationOffset, 0f, 0f);
        }
        else
        {
            // The authored tilt already accounts for the low display placement.
            // Looking at the offset and adding that tilt again would double it.
            targetRot = target.rotation * Quaternion.Euler(localXRotationOffset, 0f, 0f);
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

    Vector3 StableFlatForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        float horizontalLength = forward.magnitude;
        Vector3 flatRight = Vector3.ProjectOnPlane(target.right, Vector3.up);
        Vector3 sideForward = flatRight.sqrMagnitude > 1e-6f
            ? Vector3.Cross(flatRight.normalized, Vector3.up)
            : _lastFlatForward;

        // At vertical gaze, projected forward vanishes and then reverses. The
        // headset's lateral axis remains usable. Blend before the singularity,
        // preserving the legacy heading everywhere below a steep viewing angle.
        if (horizontalLength < 0.1f)
            forward = sideForward;
        else
        {
            forward /= horizontalLength;
            float blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.25f, horizontalLength));
            if (blend > 0f)
            {
                float angle = Vector3.SignedAngle(forward, sideForward, Vector3.up);
                forward = Quaternion.AngleAxis(angle * blend, Vector3.up) * forward;
            }
        }
        if (forward.sqrMagnitude < 1e-6f) return _lastFlatForward;
        _lastFlatForward = forward.normalized;
        return _lastFlatForward;
    }

    // Basic OVR presence check
    bool IsOvrHeadsetPresent()
    {
        // OVRManager is part of Meta SDK - this returns false if headset not detected
        // If your project uses OpenXR with OVR backend, this still reports presence.
        return OVRManager.isHmdPresent;
    }
}
