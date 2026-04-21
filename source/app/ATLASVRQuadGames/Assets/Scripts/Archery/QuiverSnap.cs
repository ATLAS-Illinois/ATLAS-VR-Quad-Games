using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using UnityEngine;

public class QuiverSnap : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Checks if object has quiver script in hierarchy, marks quiver in snap zone
        QuiverScript quiver = other.GetComponentInParent<QuiverScript>();
        if (quiver != null)
        {
            quiver.IsInSnapZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Checks if object has quiver script in hierarchy, marks quiver left snap zone
        QuiverScript quiver = other.GetComponentInParent<QuiverScript>();
        if (quiver != null)
        {
            quiver.IsInSnapZone = false;
        }
    }
}

