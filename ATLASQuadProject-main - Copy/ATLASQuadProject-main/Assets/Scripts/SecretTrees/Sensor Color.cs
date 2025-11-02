using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SensorColor : MonoBehaviour
{
    [SerializeField] private Material foundMat;

    // Changes sensor material from unfound (red) to found (green).
    public void FoundSensor()
    {
        gameObject.GetComponent<Renderer>().material = foundMat;
    }
}
