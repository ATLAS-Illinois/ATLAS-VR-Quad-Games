using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class YorB : MonoBehaviour
{
    [SerializeField] private DetectIfGrabbed dig;

    private void Awake()
    {
        gameObject.GetComponent<TMP_Text>().text = "B";
    }

    void Update()
    {
        TMP_Text button = gameObject.GetComponent<TMP_Text>();

        if (dig.IsGrabbedLeft && dig.IsGrabbedRight)
            button.text = "F"; // Code has an error
        else if (dig.IsGrabbedLeft)
            button.text = "Y";
        else if (dig.IsGrabbedRight)
            button.text = "B";
    }
}
