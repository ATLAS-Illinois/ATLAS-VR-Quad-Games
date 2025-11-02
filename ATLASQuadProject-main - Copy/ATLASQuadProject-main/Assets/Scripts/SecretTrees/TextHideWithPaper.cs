using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using System.Xml;

// Very similar script to HideWithPaper, except for TextMeshPro objects rather than gameObjects with mesh renderers.
public class TextHideWithPaper : MonoBehaviour
{
    [SerializeField] private GameObject paper;
    private int framesToEase = 60;
    private float easingAnimationProgress = 0f;

    // Sets the gameObject's opacity to 0.
    private void Awake()
    {
        TextMeshPro tmp = gameObject.GetComponent<TextMeshPro>();
        tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 0f);
    }

    // Fade in text (with an easing function) when paper is rotated rightside up.
    // Exclusively used with children of the Secret Paper family.
    void Update()
    {
        float tilt = paper.transform.eulerAngles.z;
        tilt = Math.Min(tilt, 360 - tilt);

        if (tilt <= 15)
            easingAnimationProgress = Mathf.Min(easingAnimationProgress + 1f / framesToEase, 1f);
        else
            easingAnimationProgress = Mathf.Max(easingAnimationProgress - 1f / framesToEase, 0f);

        float alpha = EasingFunction.EaseInOutCubic(0, 1, easingAnimationProgress);
        TextMeshPro tmp = GetComponent<TextMeshPro>();
        tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);
    }
}
