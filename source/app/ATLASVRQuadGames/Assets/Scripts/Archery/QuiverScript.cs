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

    public ArrowRetrieval[] arrowRetrievals;
    public List<HandGrabInteractable> arrows;
    public Transform holsterPoint;
    
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
        gameObject.layer = LayerMask.NameToLayer("Arrow");

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
        if (!IsHeldByHand)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;

            gameObject.layer = LayerMask.NameToLayer("Holstered");

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

            rb.isKinematic = false;
            rb.useGravity = true;

            gameObject.layer = LayerMask.NameToLayer("Arrow");

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
