using Meta.WitAi.Data;
using Meta.WitAi.Interfaces;
using UnityEngine;

/// <summary>Releases Voice SDK 62's prewarmed input after the last recorder stops.</summary>
[DisallowMultipleComponent]
public sealed class AssistantMicrophoneLifetime : MonoBehaviour
{
    private AudioBuffer buffer;
    private IAudioInputSource mic;
    private Behaviour micBehaviour;
    private bool suspendWhenIdle;
    private bool suspended;
    private float nextCheck;

    public static void PrepareForAssistant()
    {
        // Do not use AudioBuffer.Instance: its getter creates an SDK service.
        foreach (var lifetime in FindObjectsByType<AssistantMicrophoneLifetime>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            lifetime.suspendWhenIdle = false;
            lifetime.ResumeInput();
        }
    }

    public static void ReleaseIdleMicrophone()
    {
        foreach (var existing in FindObjectsByType<AudioBuffer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var lifetime = existing.GetComponent<AssistantMicrophoneLifetime>();
            if (lifetime == null) lifetime = existing.gameObject.AddComponent<AssistantMicrophoneLifetime>();
            lifetime.suspendWhenIdle = true;
            lifetime.SuspendIdleInput();
        }
    }

    private void Awake()
    {
        buffer = GetComponent<AudioBuffer>();
        // An enabled AudioBuffer has already resolved its input in OnEnable.
        var input = buffer != null ? buffer.MicInput : null;
        // The concrete SDK Mic lives in a platform-specific assembly in v62.
        // Use its common public interface, but never suspend another input type.
        if (input == null || input.GetType().FullName != "Meta.WitAi.Lib.Mic") return;
        micBehaviour = input as Behaviour;
        if (micBehaviour == null) return;
        mic = input;
        mic.OnStartRecording += OnRecordingStarted;
        mic.OnStopRecording += SuspendIdleInput;
    }

    private void OnRecordingStarted()
    {
        // A different SDK consumer may start through AudioBuffer directly.
        suspendWhenIdle = false;
        ResumeInput();
    }

    private void ResumeInput()
    {
        if (suspended && micBehaviour != null) micBehaviour.enabled = true;
        suspended = false;
    }

    private void SuspendIdleInput()
    {
        if (!suspendWhenIdle || buffer == null || mic == null || micBehaviour == null || !micBehaviour.enabled || mic.IsRecording) return;
        // SDK 62 has no aggregate AudioState. Its public per-consumer query
        // includes waiting consumers, so do not interrupt one still starting.
        foreach (var consumer in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (buffer.IsRecording(consumer)) return;
        suspended = true;
        micBehaviour.enabled = false; // SDK OnDisable releases its prewarmed hardware clip.
    }

    private void LateUpdate()
    {
        // Aborting a pending request may remove a waiting recorder without the
        // stopped event. Finish cleanup after that transition has completed.
        if (!suspendWhenIdle || mic == null || micBehaviour == null || !micBehaviour.enabled || mic.IsRecording || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .2f;
        SuspendIdleInput();
    }

    private void OnDestroy()
    {
        if (mic == null) return;
        mic.OnStartRecording -= OnRecordingStarted;
        mic.OnStopRecording -= SuspendIdleInput;
    }
}
