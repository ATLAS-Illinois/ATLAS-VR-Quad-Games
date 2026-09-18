using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HideWithPaper : MonoBehaviour
{
    [SerializeField] private GameObject paper;
    private int framesToEase = 60;
    private float easingAnimationProgress = 0f;

    // Sets the gameObject's opacity to 0.
    private void Awake()
    {
        Color c = gameObject.GetComponent<MeshRenderer>().material.color;
        c.a = 0f;
    }

    // Fade in object (with an easing function) when paper is rotated rightside up.
    // Exclusively used with children of the Secret Paper family.
    void Update()
    {
        float tilt = paper.transform.eulerAngles.z;
        tilt = Math.Min(tilt, 360 - tilt);

        if (tilt <= 15)
            easingAnimationProgress = Mathf.Min(easingAnimationProgress + 1f / framesToEase, 1f);
        else
            easingAnimationProgress = Mathf.Max(easingAnimationProgress - 1f / framesToEase, 0f);

        Color c = gameObject.GetComponent<MeshRenderer>().material.color;
        c.a = EasingFunction.EaseInOutCubic(0, 1, easingAnimationProgress);

        gameObject.GetComponent<MeshRenderer>().material.color = c;
    }
}
