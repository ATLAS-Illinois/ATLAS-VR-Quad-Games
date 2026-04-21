using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowRetrieval : MonoBehaviour
{
    public Transform quiver;
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private Rigidbody rb;
    private void Awake()
    {
        // Store arrow original transform relative to the quiver for resetting
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;
        rb = GetComponent<Rigidbody>(); 
    }

    void Update()
    {
        // Return arrow to quiver if arrow falls distance below initial location (below floor)
        if (transform.position.y < transform.parent.TransformPoint(initialLocalPos).y - 15f)
        {
            ReturnToQuiver();
        }     
    }

    public void ReturnToQuiver()
    {
        // Reset arrow state
        var arrow = GetComponent<Arrow>();
        if (arrow != null)
        {
            arrow.DisableTracer();
            arrow.HasScored = false;
            arrow.HasLaunched = false;
            arrow.trail.emitting = false;
        }
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Teleport the arrow back to the quiver and parent it to the quiver
        transform.SetParent(quiver);
        transform.position = quiver.TransformPoint(initialLocalPos);
        transform.rotation = quiver.rotation * initialLocalRot;
    }
}
