using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Whacamole.Controls
{
    /// <summary>
    /// Tracks the position and velocity of both VR controllers for hit detection.
    ///
    /// Velocity comes from the controller Transforms (world space, smoothed over a few frames).
    /// If a controller Transform isn't assigned or found, it falls back to the velocity the
    /// XR device reports (tracking space).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Controllers")]
        [Tooltip("Left controller transform (e.g. OVRCameraRig > LeftHandAnchor).")]
        [SerializeField] private Transform leftController;
        [Tooltip("Right controller transform (e.g. OVRCameraRig > RightHandAnchor).")]
        [SerializeField] private Transform rightController;

        [Tooltip("If a controller isn't assigned, search the scene for these names (Meta OVRCameraRig and XR Interaction Toolkit defaults).")]
        [SerializeField] private bool autoFindControllers = true;
        [SerializeField] private string[] leftControllerNames = { "LeftHandAnchor", "LeftControllerAnchor", "Left Controller" };
        [SerializeField] private string[] rightControllerNames = { "RightHandAnchor", "RightControllerAnchor", "Right Controller" };

        [Header("Velocity")]
        [Tooltip("Number of frames velocity is averaged over. Higher = smoother but laggier.")]
        [SerializeField, Range(1, 20)] private int smoothingFrames = 5;
        [Tooltip("Minimum controller speed (m/s) for a contact to count as a hit.")]
        [SerializeField, Min(0f)] private float minHitSpeed = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool drawVelocityGizmos = true;

        public float MinHitSpeed => minHitSpeed;

        private readonly ControllerTracker[] trackers = { new ControllerTracker(), new ControllerTracker() };
        private float nextSearchTime;
        private const float SearchInterval = 1f;

        // ---------- Lifecycle ----------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InputManager] Another InputManager exists in the scene. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ResizeTrackers();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            if (Application.isPlaying) ResizeTrackers();
        }

        // LateUpdate so controller poses from the rig have already been applied this frame.
        private void LateUpdate()
        {
            if (autoFindControllers && (leftController == null || rightController == null) && Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + SearchInterval;
                FindControllers();
            }

            float t = Time.time;
            Sample(Hand.Left, leftController, XRNode.LeftHand, t);
            Sample(Hand.Right, rightController, XRNode.RightHand, t);
        }

        // ---------- Public API ----------

        /// <summary>Smoothed controller velocity in m/s.</summary>
        public Vector3 GetVelocity(Hand hand) => trackers[(int)hand].Velocity;

        /// <summary>Smoothed controller speed in m/s.</summary>
        public float GetSpeed(Hand hand) => trackers[(int)hand].Velocity.magnitude;

        /// <summary>Latest controller position.</summary>
        public Vector3 GetPosition(Hand hand) => trackers[(int)hand].Position;

        /// <summary>True if the controller has reported a pose recently.</summary>
        public bool IsTracked(Hand hand) => trackers[(int)hand].IsTracked;

        /// <summary>True if the controller is moving fast enough to count as a hit.</summary>
        public bool IsFastEnoughToHit(Hand hand) => GetSpeed(hand) >= minHitSpeed;

        /// <summary>The Transform used for this controller, or null if none.</summary>
        public Transform GetControllerTransform(Hand hand) => hand == Hand.Left ? leftController : rightController;

        /// <summary>
        /// How hard the grip button (middle-finger button on the side of the controller) is squeezed, 0-1.
        /// Reads Meta's OVRInput hand trigger, and falls back to the generic XR grip value.
        /// </summary>
        public float GetGrip(Hand hand)
        {
            float ovr = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger,
                hand == Hand.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);

            float xr = 0f;
            InputDevice device = InputDevices.GetDeviceAtXRNode(hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid) device.TryGetFeatureValue(CommonUsages.grip, out xr);

            return Mathf.Max(ovr, xr);
        }

        /// <summary>
        /// Works out which controller a collider belongs to (the collider must be on the
        /// controller or one of its children, e.g. a mallet). Use this in OnTriggerEnter/OnCollisionEnter.
        /// </summary>
        public bool TryGetHand(Component other, out Hand hand)
        {
            hand = Hand.Left;
            if (other == null) return false;

            Transform t = other.transform;
            if (leftController != null && t.IsChildOf(leftController)) { hand = Hand.Left; return true; }
            if (rightController != null && t.IsChildOf(rightController)) { hand = Hand.Right; return true; }
            return false;
        }

        /// <summary>Plays a vibration on the controller (amplitude 0-1, duration in seconds).</summary>
        public void SendHaptic(Hand hand, float amplitude, float duration)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
        }

        // ---------- Internals ----------

        private void Sample(Hand hand, Transform controller, XRNode node, float time)
        {
            ControllerTracker tracker = trackers[(int)hand];

            if (controller != null && controller.gameObject.activeInHierarchy)
            {
                tracker.AddSample(controller.position, time);
                return;
            }

            // Fallback: ask the XR device directly.
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid &&
                device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos) &&
                device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 vel))
            {
                tracker.SetDirect(pos, vel);
            }
            else
            {
                tracker.Clear();
            }
        }

        private void FindControllers()
        {
            if (leftController == null) leftController = FindFirst(leftControllerNames);
            if (rightController == null) rightController = FindFirst(rightControllerNames);
        }

        private static Transform FindFirst(IEnumerable<string> names)
        {
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                GameObject go = GameObject.Find(n);
                if (go != null) return go.transform;
            }
            return null;
        }

        private void ResizeTrackers()
        {
            foreach (ControllerTracker t in trackers) t.Resize(smoothingFrames + 1);
        }

        private void OnDrawGizmos()
        {
            if (!drawVelocityGizmos || !Application.isPlaying) return;

            for (int i = 0; i < trackers.Length; i++)
            {
                ControllerTracker t = trackers[i];
                if (!t.IsTracked) continue;
                Gizmos.color = t.Velocity.magnitude >= minHitSpeed ? Color.green : Color.yellow;
                Gizmos.DrawLine(t.Position, t.Position + t.Velocity * 0.1f);
                Gizmos.DrawWireSphere(t.Position, 0.02f);
            }
        }

        /// <summary>Ring buffer of recent positions; velocity = distance over time across the window.</summary>
        private class ControllerTracker
        {
            private Vector3[] positions = new Vector3[2];
            private float[] times = new float[2];
            private int head;
            private int count;

            public Vector3 Position { get; private set; }
            public Vector3 Velocity { get; private set; }
            public bool IsTracked { get; private set; }

            public void Resize(int size)
            {
                size = Mathf.Max(2, size);
                positions = new Vector3[size];
                times = new float[size];
                head = 0;
                count = 0;
            }

            public void AddSample(Vector3 position, float time)
            {
                int len = positions.Length;
                positions[head] = position;
                times[head] = time;
                head = (head + 1) % len;
                if (count < len) count++;

                Position = position;
                IsTracked = true;

                if (count < 2)
                {
                    Velocity = Vector3.zero;
                    return;
                }

                int newest = (head - 1 + len) % len;
                int oldest = (head - count + len) % len;
                float dt = times[newest] - times[oldest];
                Velocity = dt > 1e-5f ? (positions[newest] - positions[oldest]) / dt : Vector3.zero;
            }

            public void SetDirect(Vector3 position, Vector3 velocity)
            {
                count = 0;
                Position = position;
                Velocity = velocity;
                IsTracked = true;
            }

            public void Clear()
            {
                count = 0;
                Velocity = Vector3.zero;
                IsTracked = false;
            }
        }
    }
}
