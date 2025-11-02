using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class XorA : MonoBehaviour
{
    [SerializeField] private DetectIfGrabbed dig;

    private void Awake()
    {
        gameObject.GetComponent<TMP_Text>().text = "A";
    }

    void Update()
    {
        TMP_Text button = gameObject.GetComponent<TMP_Text>();

        if (dig.IsGrabbedLeft && dig.IsGrabbedRight)
            button.text = "F"; // Code has an error
        else if (dig.IsGrabbedLeft)
            button.text = "X";
        else if (dig.IsGrabbedRight)
            button.text = "A";
    }
}
