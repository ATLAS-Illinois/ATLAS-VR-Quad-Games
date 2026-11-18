using UnityEngine;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// A hammer that rests on a stand until a hand grabs it with the grip button (see HammerGrabber).
    /// While held it's parented to the controller so it follows the hand exactly; when let go it
    /// glides back to its stand. Only a held hammer can hit nuts.
    /// The head has a trigger collider that nuts use for hit detection.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Hammer : MonoBehaviour
    {
        [Tooltip("Which side of the table this hammer rests on.")]
        [SerializeField] private Hand hand = Hand.Right;
        [SerializeField] private Collider headCollider;

        [Header("Grabbing")]
        [Tooltip("Length from the grip point (this object's origin) to the far end of the head (m). Used to tell if a hand is close enough to grab.")]
        [SerializeField, Min(0.05f)] private float length = 0.33f;
        [Tooltip("Seconds the hammer takes to glide back to its stand when released.")]
        [SerializeField, Min(0f)] private float returnTime = 0.25f;
        [Tooltip("Seconds the hammer takes to grow or shrink for the big-hammer power-up.")]
        [SerializeField, Min(0f)] private float resizeTime = 0.3f;

        /// <summary>The hand currently holding this hammer, or its resting side if nobody holds it.</summary>
        public Hand Hand => IsHeld ? heldBy : hand;
        public Collider HeadCollider => headCollider;
        public bool IsHeld { get; private set; }

        private Hand heldBy;
        private Transform restParent;
        private Vector3 restLocalPos;
        private Quaternion restLocalRot;

        private Vector3 baseScale = Vector3.one;
        private float sizeMultiplier = 1f;
        private float targetSizeMultiplier = 1f;

        private bool returning;
        private float returnProgress;
        private Vector3 returnFromPos;
        private Quaternion returnFromRot;

        public void Setup(Hand newHand, Collider head)
        {
            hand = newHand;
            headCollider = head;
        }

        // ---------- Lifecycle ----------

        private void Awake()
        {
            restParent = transform.parent;
            restLocalPos = transform.localPosition;
            restLocalRot = transform.localRotation;
            baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (Core.GameManager.Instance != null) Core.GameManager.Instance.RegisterHammer(this);
        }

        private void Start()
        {
            // Covers the case where this hammer enabled before the GameManager's Awake ran.
            if (Core.GameManager.Instance != null) Core.GameManager.Instance.RegisterHammer(this);
        }

        private void OnDisable()
        {
            if (Core.GameManager.Instance != null) Core.GameManager.Instance.UnregisterHammer(this);
        }

        private void Reset()
        {
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        /// <summary>
        /// Grows or shrinks the hammer (1 = normal size). It scales from the grip point, so a bigger hammer
        /// reaches further and has a bigger head. Used by the big-hammer power-up.
        /// </summary>
        public void SetSizeMultiplier(float multiplier, bool instant = false)
        {
            targetSizeMultiplier = Mathf.Max(0.1f, multiplier);
            if (instant)
            {
                sizeMultiplier = targetSizeMultiplier;
                transform.localScale = baseScale * sizeMultiplier;
            }
        }

        private void Update()
        {
            if (!Mathf.Approximately(sizeMultiplier, targetSizeMultiplier))
            {
                float speed = resizeTime > 0f ? Mathf.Abs(targetSizeMultiplier - 1f) / resizeTime + 1f : 1000f;
                sizeMultiplier = Mathf.MoveTowards(sizeMultiplier, targetSizeMultiplier, speed * Time.deltaTime);
                transform.localScale = baseScale * sizeMultiplier;
            }

            if (!returning) return;

            returnProgress += returnTime > 0f ? Time.deltaTime / returnTime : 1f;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(returnProgress));
            transform.localPosition = Vector3.Lerp(returnFromPos, restLocalPos, t);
            transform.localRotation = Quaternion.Slerp(returnFromRot, restLocalRot, t);
            if (returnProgress >= 1f) returning = false;
        }

        // ---------- Grabbing ----------

        /// <summary>Distance from a point to the hammer (handle + head), for grab checks.</summary>
        public float DistanceTo(Vector3 point)
        {
            Vector3 a = transform.position;
            Vector3 b = transform.TransformPoint(new Vector3(0f, 0f, length));
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector3.Distance(point, a + ab * t);
        }

        /// <summary>Puts the hammer in the hand: grip point on the controller, head pointing forward.</summary>
        public void Grab(Transform handAnchor, Hand byHand)
        {
            if (handAnchor == null) return;
            IsHeld = true;
            heldBy = byHand;
            returning = false;

            transform.SetParent(handAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Lets go: the hammer glides back to its stand.</summary>
        public void Release()
        {
            if (!IsHeld) return;
            IsHeld = false;

            transform.SetParent(restParent, true);
            returnFromPos = transform.localPosition;
            returnFromRot = transform.localRotation;
            returnProgress = 0f;
            returning = true;
        }
    }
}
