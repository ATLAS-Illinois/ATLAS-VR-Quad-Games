using UnityEngine;
using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Animations;

[RequireComponent(typeof(Grabbable), typeof(Rigidbody), typeof(Collider))]
public class Arrow : MonoBehaviour
{
    [SerializeField] private float firePowerMultiplier = 20f;
    [HideInInspector] public Rigidbody rb;
    private Grabbable grabbable;
    private Collider col;

    public Transform bowTransform = null;
    public Transform nockTransform = null;
    private Transform fletchingPoint; // Point on arrow where arrow tracer appears at

    [HideInInspector] public TrailRenderer trail; // White arrow trail
    public GameObject arrowTracer; // Actual tracer prefab
    private GameObject activeTracer; // Used to deactivate tracer on retrieval

    public bool IsHeldByHand { get; private set; } = false;
    public bool IsNocked { get; private set; } = false;
    [HideInInspector] public bool HasLaunched = false;
    [HideInInspector] public bool HasScored = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        grabbable = GetComponent<Grabbable>();
        col = GetComponent<Collider>();

        // Turn arrow trail off until fired from bow
        trail = GetComponentInChildren<TrailRenderer>();
        trail.emitting = false;

        fletchingPoint = transform.Find("Fletching");

        rb.angularDrag = 1.0f; // Some angular drag for stability
        rb.drag = 0.1f; // Some linear drag to simulate air resistance
    }

    private void OnEnable()
    {
        // Listening for grab/release pointer events
        grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        // Stop listening for grab/release pointer events
        grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void HandlePointerEvent(PointerEvent pointerEvent)
    {
        if (pointerEvent.Type == PointerEventType.Select)
        {
            IsHeldByHand = true;
            rb.isKinematic = false;
            rb.useGravity = true;

            // If we grab the arrow while it's nocked, we "Un-Nock" it
            if (IsNocked)
            {
                UnNock();
            }

            // Detach from any parent (quiver, bow) when grabbed
            transform.SetParent(null, true);
        }
        else if (pointerEvent.Type == PointerEventType.Unselect)
        {
            IsHeldByHand = false;
        }
    }

    // Called by BowController when arrow is nocked
    public void Nock(Transform nockPoint, Transform bowObject)
    {
        IsHeldByHand = false;
        IsNocked = true;

        rb.isKinematic = true;
        rb.useGravity = false;

        bowTransform = bowObject;
        nockTransform = nockPoint;

        transform.SetParent(nockPoint);
        transform.localPosition = Vector3.zero;
    }

    // New function to handle taking the arrow OFF the string
    public void UnNock()
    {
        IsNocked = false;
        transform.SetParent(null); // Detach from bow

        bowTransform = null;
        nockTransform = null;

        // Reset physics
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void Fire(Vector3 fireDirection, float pullValue)
    {
        IsNocked = false;
        HasLaunched = true;
        transform.SetParent(null);
        bowTransform = null;
        nockTransform = null;

        rb.isKinematic = false;
        rb.useGravity = true;

        float fireForce = pullValue * firePowerMultiplier;
        rb.AddForce(fireDirection * fireForce, ForceMode.Impulse);

        trail.Clear();
        trail.emitting = true;

        // Briefly disable arrow collider after firing
        StartCoroutine(BrieflyDisableCollider(0.1f));
    }

    private void OnCollisionEnter(Collision collision)
    {

        if (rb.velocity.magnitude > 0.5f && !IsNocked && !rb.isKinematic)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            trail.emitting = false;

            // Raycast backwards along the arrow's flight path
            Vector3 dir = transform.forward;
            RaycastHit hit;

            int layerMask = LayerMask.GetMask("Default"); // Ensure it only hits the environment and targets, not the arrow itself

            if (Physics.Raycast(transform.position - dir * 0.2f, dir, out hit, 1f, layerMask))
            {
                float penetrationDepth = 0.55f;

                transform.position = hit.point - dir * penetrationDepth;
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

                // Find parent with no scaling and rotation
                MovingTarget mover = hit.collider.GetComponentInParent<MovingTarget>();
                if (mover != null)
                {
                    // Parent to the clean parent
                    transform.SetParent(mover.transform, true);
                }
            }
            // Start the blue/orange tracer if the arrow hits the target
            if (collision.collider.CompareTag("TargetRing"))
            {
                activeTracer = Instantiate(arrowTracer, fletchingPoint.position, fletchingPoint.rotation, fletchingPoint);
            }
        }
    }

    // Disable tracer (for arrow retrieval)
    public void DisableTracer()
    {
        if (activeTracer != null)
        {
            activeTracer.SetActive(false);
        }
    }

    // For point calculation
    private void OnTriggerEnter(Collider other)
    {
        if (HasScored) return;

        if (other.CompareTag("TargetRing"))
        {
            TargetRing ring = other.GetComponent<TargetRing>();
            if (ring != null)
            {
                ScoreManager.instance.AddScore(ring.points);
                HasScored = true; // Prevent multiple scoring from the same arrow
            }
        }
    }

    private IEnumerator BrieflyDisableCollider(float duration)
    {
        col.enabled = false;
        yield return new WaitForSeconds(duration);
        col.enabled = true;
    }

    private void FixedUpdate()
    {
        // Only run if the arrow has been launched and is not resting on something
        if (HasLaunched && !rb.isKinematic && rb.velocity.sqrMagnitude > 0.01f)
        {
            // Create a rotation that looks in the direction of the current velocity
            Quaternion lookRotation = Quaternion.LookRotation(rb.velocity);

            transform.Rotate(180f * Time.fixedDeltaTime * Vector3.forward);

            // Smoothly apply the rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.fixedDeltaTime * 15f);
        }
    }

    private void LateUpdate()
    {
        if (nockTransform && bowTransform)
        {
            transform.position = nockTransform.position;
            transform.rotation = bowTransform.rotation * Quaternion.Euler(0, 270, 0);
        }   
    }
}