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
        CheckForCatch(obj);
    }

    // Enter alone can be missed (e.g. the player was already overlapping the mammoth
    // when they grabbed the donut), so keep checking while the two overlap.
    void OnTriggerStay(Collider obj)
    {
        CheckForCatch(obj);
    }

    private void CheckForCatch(Collider obj)
    {
        if (med == null || igdg == null || !igdg.donutIsGrabbed) return;

        bool isMammoth = obj.gameObject.name == "Mammoth Model"
                         || obj.GetComponentInParent<MammothEatDonut>() != null;

        if (isMammoth)
        {
            Debug.Log("OOOOH NO");
            med.MammothGameOver();
        }
    }
}
