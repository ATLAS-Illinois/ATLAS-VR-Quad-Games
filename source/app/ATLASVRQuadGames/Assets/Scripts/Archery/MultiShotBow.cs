using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

public class MultiShotBow : MonoBehaviour
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
    private List<Arrow> currentArrows = new List<Arrow> { };
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

        Debug.Log("Arrow Nocked!");
        // Tell the arrow it's nocked (disables its physics/grab)
        arrow.Nock(nockPoint);

        currentArrows.Add(arrow);
        isArrowNocked = true;
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
        if (!isArrowNocked || currentArrows.Count == 0)
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

        // Find an arrow to fly
        int ind = UnityEngine.Random.Range(0, currentArrows.Count);
        Arrow arrow = currentArrows[ind];
        currentArrows.RemoveAt(ind);
        if (arrow != null) arrow.Fire(launchDirection, finalPullAmount);

        // Reset state
        if (currentArrows.Count == 0) isArrowNocked = false;

        // Reset bow visuals
        UpdateBowTension(0f);
    }
}