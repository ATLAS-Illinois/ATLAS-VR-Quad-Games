using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEditor.Callbacks;
using UnityEngine;

public class QuiverScript : MonoBehaviour
{
    private Rigidbody rb;
    private Grabbable grabbable;
    public GameObject snapPointMarker;

    private Vector3 quiverInitialPos;
    private Quaternion quiverInitialRot;
    private Transform quiverParent;

    public ArrowRetrieval[] arrowRetrievals; // This list  is used for teleporting the arrows on retrieval
    public List<HandGrabInteractable> arrows; // This list is for disabling arrow interactions when grabbing quiver
    public Transform holsterPoint; // Quiver anchor point
    
    private int handsInQuiver = 0;
    [HideInInspector] public bool IsHeldByHand = false;
    [HideInInspector] public bool IsInSnapZone = false;

    private void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        rb = GetComponent<Rigidbody>();
        snapPointMarker.SetActive(false); // Hide snap marker by default

        // Store quiver original transform for resetting
        quiverInitialPos = transform.localPosition;
        quiverInitialRot = transform.localRotation;
        quiverParent = transform.parent;
    }

    public void ResetQuiverArrows()
    {
        // Reset quiver physics and layer
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reset quiver layer
        gameObject.layer = LayerMask.NameToLayer("Quiver");

        // Reset quiver transform
        transform.SetParent(quiverParent);
        transform.localPosition = quiverInitialPos;
        transform.localRotation = quiverInitialRot;

        // Reset each arrow in the quiver
        foreach (var arrow in arrowRetrievals)
        {
            arrow.ReturnToQuiver();
        }
    }

    private void OnEnable()
    {
        grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Collider:" + other);
        var interactor = other.GetComponentInParent<IInteractor>();
        if (interactor != null)
        {
            handsInQuiver++;

            if (handsInQuiver == 1)
            {
                foreach (var arrow in arrows)
                {
                    arrow.enabled = false;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var interactor = other.GetComponentInParent<IInteractor>();
        if (interactor != null)
        {
            handsInQuiver--;

            if (handsInQuiver <= 0)
            {
                handsInQuiver = 0;

                foreach (var arrow in arrows)
                {
                    arrow.enabled = true;
                }
            }
        }
    }

    public void HolsterSnap(Transform holster)
    {
        // Quiver snaps only when released
        if (!IsHeldByHand)
        {
            // Stopping quiver drift/jitter
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;

            // This layer can be grabbed but doesn't have collisions with other objects
            gameObject.layer = LayerMask.NameToLayer("Anchor");

            // Attach quiver to holster anchor, reset local transform for clean snap
            transform.SetParent(holster, true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }

    private void HandlePointerEvent(PointerEvent pointerEvent)
    {
        if (pointerEvent.Type == PointerEventType.Select)
        {
            IsHeldByHand = true;

            // Show snap marker when grabbing quiver
            if (snapPointMarker != null)
            {
                snapPointMarker.SetActive(true);
            }

            // Enable physics when held, stays true if dropped outside of snap zone
            rb.isKinematic = false;
            rb.useGravity = true;

            // Back to normal interaction layer
            gameObject.layer = LayerMask.NameToLayer("Quiver");

            // Detached from anchor/parent so quiver moves freely
            transform.SetParent(null, true);
        }
        else if (pointerEvent.Type == PointerEventType.Unselect)
        {
            IsHeldByHand = false;

            // Hide snap marker when quiver is released
            if (snapPointMarker != null)
            {
                snapPointMarker.SetActive(false);
            }

            if (IsInSnapZone)
            {
                HolsterSnap(holsterPoint);
            }
        }
    }
}
