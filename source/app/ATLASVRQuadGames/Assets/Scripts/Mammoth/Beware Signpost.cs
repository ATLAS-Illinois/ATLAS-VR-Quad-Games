using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BewareSignpost : MonoBehaviour
{
    private TMP_Text dialogue;
    // Start is called before the first frame update
    void Start()
    {
        dialogue = GetComponent<TMP_Text>();
        dialogue.text = "do not take the golden donut\r\n\r\n-mammoth";
        dialogue.fontSize = 20;
    }

    public void Anger()
    {
        dialogue.text = "RUN!!!!!";
        dialogue.fontSize = 36;
    }
}
