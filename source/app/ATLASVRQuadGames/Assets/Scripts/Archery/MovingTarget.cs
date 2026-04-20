using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    public Vector3 offset = new Vector3(3f, 0f, 0f); // How far target moves
    public float speed = 2f; // How fast target moves
    private Vector3 startPosition;
    private Vector3 endPosition;
    void Start()
    {
        // Record starting position and get end position
        startPosition = transform.position;
        endPosition = startPosition + offset;
    }
    void Update()
    {
        // Value that moves between 0 and 1 over time
        float time = Mathf.PingPong(Time.time * speed, 1f);
        // Smoothly moves target
        transform.position = Vector3.Lerp(startPosition, endPosition, time);      
    }
}
