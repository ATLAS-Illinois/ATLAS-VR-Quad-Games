using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MammothHitPlayer : MonoBehaviour
{
    [SerializeField] private MammothEatDonut med;
    [SerializeField] private IsGoldenDonutGrabbed igdg;

    void OnTriggerEnter(Collider obj)
    {
        if (obj.gameObject.name == "Mammoth Model" && igdg.donutIsGrabbed)
        {
            Debug.Log("OOOOH NO");
            med.MammothGameOver();
        }
    }
}
