using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BewareSignpost : MonoBehaviour
{
    private const string WarningText = "do not take the golden donut\r\n\r\n-mammoth";
    private const float WarningFontSize = 20;

    private TMP_Text dialogue;
    // Start is called before the first frame update
    void Start()
    {
        dialogue = GetComponent<TMP_Text>();
        Calm();
    }

    public void Anger()
    {
        ShowMessage("RUN!!!!!", 36);
    }

    // Back to the original warning (used when the mammoth game resets).
    public void Calm()
    {
        ShowMessage(WarningText, WarningFontSize);
    }

    public void ShowMessage(string message, float fontSize)
    {
        if (dialogue == null) dialogue = GetComponent<TMP_Text>();
        if (dialogue == null) return;

        dialogue.text = message;
        dialogue.fontSize = fontSize;
    }
}
