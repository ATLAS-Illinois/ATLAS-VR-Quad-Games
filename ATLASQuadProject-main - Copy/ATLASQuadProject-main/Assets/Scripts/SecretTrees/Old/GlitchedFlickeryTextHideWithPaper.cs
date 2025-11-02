using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using System.Xml;

public class GlitchedFlickeryTextHideWithPaper : MonoBehaviour
{
    [SerializeField] private GameObject paper;

    // Sets the gameObject's opacity to 0.
    private void Awake()
    {
        TextMeshPro tmp = gameObject.GetComponent<TextMeshPro>();
        tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 0f);
    }

    // Make text transparent until paper is rotated rightside up.
    // Exclusively used with children of the Secret Paper family.
    void Update()
    {
        // Update the text's opacity based on the paper's z-rotation value
        // (more specifically, how much the paper is tilted. The value for flat is 0 degrees).

        float tilt = paper.transform.eulerAngles.z;
        tilt = Math.Min(tilt, 360 - tilt);

        TextMeshPro tmp = GetComponent<TextMeshPro>();

        float alpha = 1f;
        if (tilt >= 15 && tilt <= 25)
            alpha = 1f - (tilt - 15) * 0.1f;
        else if (tilt > 25)
            alpha = 0f; // transparent

        tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);
    }
}
