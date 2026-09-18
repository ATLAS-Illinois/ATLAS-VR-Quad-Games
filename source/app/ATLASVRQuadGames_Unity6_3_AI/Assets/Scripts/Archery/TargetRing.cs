using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetRing : MonoBehaviour
{ 
    // Fireworks
    public ParticleSystem fireworks;

    // The visual boundaries of the rings
    public Renderer bullseye;
    public Renderer redring;
    public Renderer bluering;
    public Renderer blackring;
    public Renderer whitering;

    // All the ring radii
    float bullseyer;
    float redr;
    float bluer;
    float blackr;
    float whiter;

    void Start()
    {
        // Assign ring boundaries, useable but not the most accurate though
        bullseyer = bullseye.bounds.extents.x;
        redr = redring.bounds.extents.x;
        bluer = bluering.bounds.extents.x;
        blackr = blackring.bounds.extents.x;
        whiter = whitering.bounds.extents.x;
    }

    // Determine if arrow scores and how much
    public void ProcessHit(Vector3 hitPoint)
    {
        // Get the hitpoint on target collider and get distance to 
        Vector3 localHit = transform.InverseTransformPoint(hitPoint);
        Vector2 flatHit = new Vector2(localHit.x, localHit.y);
        float distance = flatHit.magnitude;

        // Tolerance for arrows on the very edge of target
        float tolerance = 0.01f;
        int points = 0;
        
        // Point assignment based on ring boundaries, firework on bullseye
        if (distance <= bullseyer)
        {
            points = 10;
            if (fireworks != null)
                fireworks.Play();
        }
        else if (distance <= redr) points = 8;
        else if (distance <= bluer) points = 6;
        else if (distance <= blackr) points = 4;
        else if (distance <= whiter + tolerance) points = 2;
        else return; // Ignore if arrow enters box trigger but misses target, added tolerance distance for edge cases

        ScoreManager.instance.AddScore(points);
    }

}
