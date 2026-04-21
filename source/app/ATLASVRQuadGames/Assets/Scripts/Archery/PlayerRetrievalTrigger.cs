using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRetrievalTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Get ArrowRetrieval/Arrow component on object/hierarchy, ignore objects without it
        var arrowRetrival = other.GetComponentInChildren<ArrowRetrieval>()
            ?? other.GetComponentInParent<ArrowRetrieval>();
        var arrow = other.GetComponentInChildren<Arrow>() 
            ?? other.GetComponentInParent<Arrow>();

        if (arrow == null || arrowRetrival == null)
            return;

        // Returns arrow if at rest
        if (!arrow.IsHeldByHand && !arrow.IsNocked && arrow.rb.velocity.magnitude < 0.5f)
        {
            arrowRetrival.ReturnToQuiver();
        }
    }
}
