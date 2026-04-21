using UnityEngine;
using UnityEngine.UI;

public class ToggleVoiceActivation : MonoBehaviour
{
    public Toggle toggle;
    public WakeWordManager wakeMic;

    private void OnEnable()
    {
        toggle.onValueChanged.AddListener(ActivateMic);
    }

    private void OnDisable()
    {
        toggle.onValueChanged.RemoveListener(ActivateMic);
    }

    public void ActivateMic(bool yes)
    {
        if(yes)
        {
            wakeMic.Activate();
        }
        else
        {
            wakeMic.DeActivate();
        }
    }
}
