using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // Interesting...it may help

public class BallGravity : MonoBehaviour
{

    [SerializeField] private float customGravity = 0.5f; // Fall very slowly
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // FixedUpdate is used for all physics-related calculations
    void FixedUpdate()
    {
        Vector3 gravityForce = new Vector3(0, -customGravity, 0);
        rb.AddForce(gravityForce, ForceMode.Acceleration);
    }
}