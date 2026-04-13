using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRetrievalTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var arrowRetrival = other.GetComponentInChildren<ArrowRetrieval>()
            ?? other.GetComponentInParent<ArrowRetrieval>();
        var arrow = other.GetComponentInChildren<Arrow>() 
            ?? other.GetComponentInParent<Arrow>();

        if (arrow == null || arrowRetrival == null)
            return;

        if (arrow.HasLaunched && arrow.rb.isKinematic)
        {
            arrowRetrival.ReturnToQuiver();
        }
    }
}
