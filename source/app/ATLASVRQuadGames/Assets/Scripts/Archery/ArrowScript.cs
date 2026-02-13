using UnityEngine;
using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Grabbable), typeof(Rigidbody), typeof(Collider))]
public class Arrow : MonoBehaviour
{
    [SerializeField] private float firePowerMultiplier = 20f;

    private Rigidbody rb;
    private Grabbable grabbable;
    private Collider col;
    private Vector3 respawnPoint;

    public bool IsHeldByHand { get; private set; } = false;
    public bool IsNocked { get; private set; } = false;
    public bool HasLaunched { get; private set; } = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
        col = GetComponent<Collider>();

        respawnPoint = transform.position;

        rb.angularDrag = 1.0f; // Some angular drag for stability
        rb.drag = 0.1f; // Some linear drag to simulate air resistance
    }

    private void OnEnable()
    {
        grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void HandlePointerEvent(PointerEvent pointerEvent)
    {
        if (pointerEvent.Type == PointerEventType.Select)
        {
            IsHeldByHand = true;

            // If we grab the arrow while it's nocked, we "Un-Nock" it
            if (IsNocked)
            {
                UnNock();
            }

            // Ensure physics are ready for holding
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        else if (pointerEvent.Type == PointerEventType.Unselect)
        {
            IsHeldByHand = false;
        }
    }

    // Called by BowController when arrow is nocked
    public void Nock(Transform nockPoint)
    {
        IsHeldByHand = false;
        IsNocked = true;

        rb.isKinematic = true;
        rb.useGravity = false;

        transform.SetParent(nockPoint);


        // The code below is half working, and is meant to fix slight misalignments when nocking.
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

    }

    // New function to handle taking the arrow OFF the string
    public void UnNock()
    {
        IsNocked = false;
        transform.SetParent(null); // Detach from bow

        // Reset physics
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void Fire(Vector3 fireDirection, float pullValue)
    {
        IsNocked = false;
        transform.SetParent(null);

        rb.isKinematic = false;
        rb.useGravity = true;

        float fireForce = pullValue * firePowerMultiplier;
        rb.AddForce(fireDirection * fireForce, ForceMode.Impulse);

        HasLaunched = true;

        StartCoroutine(BrieflyDisableCollider(0.1f));
    }

    // ... (Collision and IEnumerator code stays the same) ...
    private void OnCollisionEnter(Collision collision)
    {
        if (rb.velocity.magnitude > 0.5f && !IsNocked && !rb.isKinematic)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            transform.SetParent(collision.transform);
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

            // Smoothly apply the rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.fixedDeltaTime * 15f);
        }

        if (transform.position.y <= -20f)
        {
            transform.SetPositionAndRotation(respawnPoint, Quaternion.identity);
            GetComponent<Rigidbody>().velocity = Vector3.zero;


            HasLaunched = false;
            IsHeldByHand = false;
            IsNocked = false;
            rb.useGravity = false;
            rb.isKinematic = false;

            Awake(); // can call to show what to do upon awakening
        }
    }
}