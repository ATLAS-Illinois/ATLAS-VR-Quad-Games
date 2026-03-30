using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    public Vector3 offset = new Vector3(3f, 0f, 0f);
    public float speed = 2f;
    
    private Vector3 startPosition;
    private Vector3 endPosition;
    void Start()
    {
        startPosition = transform.position;
        endPosition = startPosition + offset;
    }
    void Update()
    {
        float time = Mathf.PingPong(Time.time * speed, 1f);
        transform.position = Vector3.Lerp(startPosition, endPosition, time);      
    }
}
