using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Narration : MonoBehaviour
{
    [SerializeField] private TMP_Text tmp;
    [SerializeField] private float delay = 3f;
    private bool activated = false;

    // Start is called before the first frame update
    void Start()
    {
        tmp.maxVisibleLines = 0;
    }

    // Update is called once per frame
    void Update() { }

    public void EnableText()
    {
        if (!activated)
        {
            activated = true;
            StartCoroutine(Narrate());
        }
    }

    IEnumerator Narrate()
    {
        tmp.ForceMeshUpdate();
        int totalLines = tmp.textInfo.lineCount;

        for (int i = 0; i <= totalLines; i++)
        {
            tmp.maxVisibleLines = i;
            yield return new WaitForSeconds(delay);
        }
    }
}
