using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixPapertoBackground : MonoBehaviour
{
    void Start()
    {
        gameObject.GetComponent<Material>().renderQueue = 2000;
    }

}
