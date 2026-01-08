using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClampingBlock : MonoBehaviour
{
    [Tooltip("Tag used for other blocks that can connect to this one.")]
    public string connectTag = "LogoPiece";

    [Tooltip("How close two blocks need to be before clamping.")]
    public float clampForceThreshold = 2.0f;

    private void OnCollisionEnter(Collision collision)
    {
        // Only connect with other tagged blocks
        if (collision.gameObject.CompareTag(connectTag))
        {
            Rigidbody otherRb = collision.rigidbody;
            Rigidbody myRb = GetComponent<Rigidbody>();

            // Make sure both objects have Rigidbodies and no existing joint
            if (otherRb != null && myRb != null && GetComponent<FixedJoint>() == null)
            {
                // Add a joint to "lock" them together
                FixedJoint joint = gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = otherRb;

                // Optional: prevent accidental separation
                joint.breakForce = Mathf.Infinity;
                joint.breakTorque = Mathf.Infinity;
            }
        }
    }
}
