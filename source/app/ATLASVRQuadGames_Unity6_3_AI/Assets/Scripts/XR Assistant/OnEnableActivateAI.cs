using UnityEngine;

public class OnEnableActivateAI : MonoBehaviour
{
    public WakeWordManager wakeWordManager;

    private void OnEnable()
    {
        if (wakeWordManager != null) wakeWordManager.Activate();
    }

    private void OnDisable()
    {
        if (wakeWordManager != null) wakeWordManager.CancelListening();
        AssistantMicrophoneLifetime.ReleaseIdleMicrophone();
    }
}
