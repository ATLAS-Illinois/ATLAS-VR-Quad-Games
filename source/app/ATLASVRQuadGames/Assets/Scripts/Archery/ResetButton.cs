using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetButton : MonoBehaviour
{
    public QuiverScript quiver;
    private bool handInside = false;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("GameController"))
        {
            handInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("GameController"))
        {
            handInside = false;
        }
    }

    private void Update()
    {
        if (!handInside)
            return;
        
        // Require a controller button press while hand is in trigger
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            Debug.Log("Reset button pressed, resetting quiver and arrows.");
            quiver.ResetQuiverArrows();
        }
    }
}
