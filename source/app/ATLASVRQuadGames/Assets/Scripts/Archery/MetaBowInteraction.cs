using System;
using UnityEngine;

public class BowController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The MetaStringInteraction script on your string")]
    [SerializeField]
    private MetaStringInteraction stringInteraction;

    [Tooltip("The empty GameObject where the arrow snaps to")]
    [SerializeField]
    private Transform nockPoint;

    [Tooltip("The same object used as the Direction Reference on the String (e.g., PullDirectionGuide)")]
    [SerializeField]
    private Transform fireDirectionGuide; // <--- NEW/UPDATED FOR LAUNCH FIX

    [Header("Bow Visuals")]
    [Tooltip("Top part of the bow that will bend")]
    [SerializeField]
    private Transform topLimb;

    [Tooltip("Bottom part of the bow that will bend")]
    [SerializeField]
    private Transform bottomLimb;

    [Tooltip("How much the limbs should bend at full pull")]
    [SerializeField]
    private float maxLimbBend = 30f; // Max rotation in degrees

    // Private State
    private Arrow currentArrow = null;
    private bool isArrowNocked = false;
    private Vector3 nockRestLocalPosition;

    private void Start()
    {
        if (stringInteraction == null)
        {
            Debug.LogError("BowController: String Interaction is not set!", this);
            return;
        }

        // Store the nock's starting position
        if (nockPoint != null)
        {
            nockRestLocalPosition = nockPoint.localPosition;
        }

        // Subscribe to the string's events
        stringInteraction.PullAmountChanged += UpdateBowTension;
        stringInteraction.OnStringReleased += FireArrow;
    }

    private void OnDestroy()
    {
        // Always unsubscribe from events
        if (stringInteraction != null)
        {
            stringInteraction.PullAmountChanged -= UpdateBowTension;
            stringInteraction.OnStringReleased -= FireArrow;
        }
    }

    // This is called by the NockSocket script
    public void NockArrow(Arrow arrow)
    {
        if (isArrowNocked) return;

        Debug.Log("Arrow Nocked!");
        currentArrow = arrow;
        isArrowNocked = true;

        // Tell the arrow it's nocked (disables its physics/grab)
        currentArrow.Nock(nockPoint);
    }

    // Called every frame the string's pull amount changes
    private void UpdateBowTension(float pullAmount)
    {
        // 1. Bend the limbs
        if (topLimb != null)
        {
            topLimb.localRotation = Quaternion.Euler(pullAmount * maxLimbBend, 0, 0);
        }
        if (bottomLimb != null)
        {
            bottomLimb.localRotation = Quaternion.Euler(pullAmount * -maxLimbBend, 0, 0);
        }

        // Note: Moving the nockPoint is handled by the String's constrained transform
        // if NockSocket is a child of the String. This code is generally now redundant/safe to remove.
        /*
        if (nockPoint != null)
        {
             nockPoint.localPosition = nockRestLocalPosition + Vector3.back * pullAmount * maxPullDistance;
        }
        */

    }

    // Called once when the string is released
    private void FireArrow(float finalPullAmount)
    {
        if (!isArrowNocked || currentArrow == null)
        {
            // No arrow to fire, just reset bow
            UpdateBowTension(0f);
            return;
        }

        // Determine the launch direction: 
        // Use the manually aligned guide's forward vector if set, otherwise fallback to nockPoint.
        Vector3 launchDirection = nockPoint.forward;

        if (fireDirectionGuide != null)
        {
            // *** CRITICAL FIX: Use the Direction Guide for launch direction ***
            launchDirection = fireDirectionGuide.forward;
        }

        Debug.Log("Firing arrow with power: " + finalPullAmount);

        // Tell the arrow to fly
        currentArrow.Fire(launchDirection, finalPullAmount);

        // Reset state
        isArrowNocked = false;
        currentArrow = null;

        // Reset bow visuals
        UpdateBowTension(0f);
    }
}