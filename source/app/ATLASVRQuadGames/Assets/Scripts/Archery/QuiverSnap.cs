using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using UnityEngine;

public class QuiverSnap : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        QuiverScript quiver = other.GetComponentInParent<QuiverScript>();
        if (quiver != null)
        {
            quiver.IsInSnapZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        QuiverScript quiver = other.GetComponentInParent<QuiverScript>();
        if (quiver != null)
        {
            quiver.IsInSnapZone = false;
        }
    }
}

