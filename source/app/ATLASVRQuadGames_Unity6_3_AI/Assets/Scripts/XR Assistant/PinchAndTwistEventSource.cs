using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Emits events when the user pinches with the index finger and twists while pinched.
/// Requires an OVRHand on the same GameObject and optionally an OVRSkeleton to auto resolve finger tips.
/// Outputs:
/// - OnStartPinchAndTwist
/// - OnPinchAndTwistNormalized (0..1 absolute from start)
/// - OnPinchAndTwistSignedNormalized (-1..1, right positive, left negative)
/// - OnEndPinchAndTwist
/// </summary>
[DefaultExecutionOrder(50)]
public class PinchAndTwistEventSource : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("OVRHand providing pinch state and tracking confidence")]
    public OVRHand hand;

    [Tooltip("Optional. If present, the script will try to auto find thumb and index tips")]
    public OVRSkeleton skeleton;

    [Header("Pinch gating")]
    [Range(0f, 1f)]
    [Tooltip("Pinch strength required to start tracking a twist")]
    public float pinchStartStrength = 0.7f;

    [Range(0f, 1f)]
    [Tooltip("Pinch strength below which the gesture ends")]
    public float pinchReleaseStrength = 0.3f;

    [Tooltip("Require high confidence tracking before firing any events")]
    public bool requireHighConfidence = true;

    [Header("Twist settings")]
    [Tooltip("Degrees of twist that maps to 1.0 normalized")]
    public float maxTwistAngle = 90f;

    [Tooltip("Minimum absolute degrees from the start before we consider this a twist and fire OnStart")]
    public float minAngleToTwist = 5f;

    [Tooltip("Only fire normalized events when change exceeds this amount")]
    public float minDeltaNormalizedToFire = 0.01f;

    [Tooltip("Optional deadband in degrees to ignore tiny jitter")]
    public float angleDeadband = 1.0f;

    [Header("Events")]
    public UnityEvent OnStartPinchAndTwist = new UnityEvent();
    [Tooltip("Signed normalized -1..1. Right positive, left negative")]
    public UnityEvent<float> OnPinchAndTwist = new UnityEvent<float>();
    public UnityEvent OnEndPinchAndTwist = new UnityEvent();

    enum State { Idle, Pinching, ActiveTwist }
    State _state = State.Idle;

    private Transform thumbTip;
    private Transform indexTip;

    float _lastReportedSigned;
    bool _raisedStart;
    bool _triedResolveBones;
    bool _applicationPaused;
    Vector3 startPinchUpVector;

    // Meta's microgesture callbacks can run before this component's Update.
    // Suppress navigation only after real rotation intent, not for ordinary taps.
    public bool HasTwistIntent => isActiveAndEnabled && !_applicationPaused && hand != null &&
        hand.IsDataValid && hand.IsPointerPoseValid && hand.GetPointerRayTransform() != null &&
        (!requireHighConfidence || hand.IsDataHighConfidence) &&
        hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= pinchReleaseStrength &&
        (_state == State.ActiveTwist || (_state == State.Pinching &&
            Mathf.Abs(GetLocalFingerVectorAngleDeg()) >= minAngleToTwist));

    void Reset()
    {
        hand = GetComponent<OVRHand>();
        skeleton = GetComponent<OVRSkeleton>();
    }

    void Awake()
    {
        if (!hand) hand = GetComponent<OVRHand>();
        if (!skeleton) skeleton = GetComponent<OVRSkeleton>();
    }

    void Update()
    {
        if (_applicationPaused || !hand || !hand.IsValid()) { EndGestureIfNeeded(); return; }

        if ((!thumbTip || !indexTip) && !_triedResolveBones)
        {
            TryResolveTipsFromSkeleton();
        }

        if (!hand.IsDataValid || !hand.IsPointerPoseValid || hand.GetPointerRayTransform() == null ||
            (requireHighConfidence && !hand.IsDataHighConfidence))
        {
            EndGestureIfNeeded();
            return;
        }

        float pinch = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

        switch (_state)
        {
            case State.Idle:
                if (pinch >= pinchStartStrength && TipsAvailable())
                {
                    _lastReportedSigned = 0f;
                    _raisedStart = false;
                    _state = State.Pinching;
                    startPinchUpVector = hand.GetPointerRayTransform().up;
                }
                break;

            case State.Pinching:
                if (pinch < pinchReleaseStrength)
                {
                    EndGestureIfNeeded();
                    break;
                }

                {
                    float deltaAbs = GetLocalFingerVectorAngleDeg();
                    if (Mathf.Abs(deltaAbs) >= minAngleToTwist)
                    {
                        if (!_raisedStart)
                        {
                            _raisedStart = true;
                            SafeInvoke(OnStartPinchAndTwist);
                        }
                        _state = State.ActiveTwist;
                        goto case State.ActiveTwist;
                    }
                }
                break;

            case State.ActiveTwist:
                if (pinch < pinchReleaseStrength)
                {
                    EndGestureIfNeeded();
                    break;
                }

                {
                    float deltaDeg = GetLocalFingerVectorAngleDeg();

                    if (Mathf.Abs(deltaDeg) < angleDeadband) deltaDeg = 0f;

                    float absNorm = Mathf.Clamp01(Mathf.Abs(deltaDeg) / Mathf.Max(1e-3f, maxTwistAngle));
                    float sign = Mathf.Sign(deltaDeg);
                    float signedNorm = Mathf.Clamp(sign * absNorm, -1f, 1f);

                    if (Mathf.Abs(signedNorm - _lastReportedSigned) >= minDeltaNormalizedToFire)
                    {
                        _lastReportedSigned = signedNorm;
                        SafeInvoke(OnPinchAndTwist, signedNorm);
                    }
                }
                break;
        }
    }

    void OnDisable() => EndGestureIfNeeded();
    void OnApplicationPause(bool paused)
    {
        _applicationPaused = paused;
        if (paused) EndGestureIfNeeded();
    }

    void EndGestureIfNeeded()
    {
        if (_state == State.ActiveTwist && _raisedStart)
        {
            SafeInvoke(OnEndPinchAndTwist);
        }
        _state = State.Idle;
        _raisedStart = false;
        _lastReportedSigned = 0f;
    }

    bool TipsAvailable() => thumbTip && indexTip;

    float GetLocalFingerVectorAngleDeg()
    {
        if (!TipsAvailable()) return 0f;

        Transform rayTransform = hand.GetPointerRayTransform();
        if (rayTransform == null) return 0f;
        return Vector3.SignedAngle(startPinchUpVector, rayTransform.up, -rayTransform.forward);
    }

    void TryResolveTipsFromSkeleton()
    {
        _triedResolveBones = true;
        if (!skeleton) return;

        var bones = skeleton.Bones;
        if (bones == null || bones.Count == 0)
        {
            _triedResolveBones = false;
            return;
        }

        Transform foundThumb = null;
        Transform foundIndex = null;

        foreach (var b in bones)
        {
            if (b == null || !b.Transform) continue;
            string idName = b.Id.ToString();
            string tName = b.Transform.name;

            if (!foundThumb && (idName.Contains("ThumbTip") || tName.Contains("ThumbTip")))
                foundThumb = b.Transform;

            if (!foundIndex && (idName.Contains("IndexTip") || tName.Contains("IndexTip")))
                foundIndex = b.Transform;

            if (foundThumb && foundIndex) break;
        }

        if (foundThumb) thumbTip = foundThumb;
        if (foundIndex) indexTip = foundIndex;

#if UNITY_EDITOR
        if (!thumbTip || !indexTip)
        {
            Debug.LogWarning("[PinchAndTwistEventSource] Could not auto resolve finger tips. Assign them manually.");
        }
#endif
    }

    static void SafeInvoke(UnityEvent evt)
    {
        try { evt?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    static void SafeInvoke(UnityEvent<float> evt, float value)
    {
        try { evt?.Invoke(value); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
