using UnityEngine;

public class OnEnableActivateAI : MonoBehaviour
{
    public WakeWordManager wakeWordManager;

    private void OnEnable()
    {
        wakeWordManager.Activate();
    }

    private void OnDisable()
    {
        wakeWordManager.DeActivate();
    }
}
