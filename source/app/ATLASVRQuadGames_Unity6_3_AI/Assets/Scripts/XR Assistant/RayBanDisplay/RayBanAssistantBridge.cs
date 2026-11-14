using Meta.WitAi;
using Meta.WitAi.Json;
using UnityEngine;

/// <summary>Uses the original Assistant services while their legacy page stays hidden.</summary>
[DisallowMultipleComponent]
public sealed class RayBanAssistantBridge : MonoBehaviour
{
    public AIManager manager;
    public GameObject serviceRoot;

    private bool acquired;
    private bool rootWasActive;
    private bool managerWasEnabled;
    private bool wakeWasEnabled;
    private bool voiceWasEnabled;
    private bool previousAutoListen;
    private bool previousActionOnly;
    private bool previousKeyboard;
    private bool requested;
    private bool paused;
    private bool quitting;
    private bool hasPresented;
    private float requestedAt;
    private string question = "";
    private string failure;
    private WakeWordManager wake;
    private RayBanMusicBridge music;

    public bool IsAcquired => acquired;
    public bool IsListening => acquired && wake != null && wake.IsMicActive;
    public bool IsBusy => acquired && ((manager != null && manager.IsResponding) || requested);
    public bool CanListen => acquired && !paused && manager != null && manager.IsInitialized &&
        manager.IsConfigured && wake != null && wake.IsInitialized && wake.isActiveAndEnabled &&
        wake.CanRequestVoiceInput;
    public string Question => question;
    public string Response => manager != null && manager.aiResponseText != null ? manager.aiResponseText.text : "";
    public Texture Illustration => manager != null && manager.describePicture != null && manager.describePicture.enabled
        ? manager.describePicture.texture : null;

    public string Status
    {
        get
        {
            if (!HasReferences) return "Assistant is not connected";
            if (paused) return "Paused · select Listen to continue";
            if (!acquired) return "Assistant closed";
            if (!string.IsNullOrEmpty(failure)) return failure;
            if (!manager.IsInitialized || !wake.IsInitialized) return "Preparing Assistant…";
            if (!manager.IsConfigured) return "Assistant API configuration unavailable";
            if (IsListening) return "Listening…";
            if (requested) return "Processing speech…";
            if (manager.IsResponding) return "Preparing or speaking response…";
            return "Select Listen to ask a question";
        }
    }

    private bool HasReferences => manager != null && manager.wakeWordManager != null &&
        manager.wakeWordManager.appVoice != null && serviceRoot != null &&
        manager.transform.IsChildOf(serviceRoot.transform) &&
        manager.wakeWordManager.transform.IsChildOf(serviceRoot.transform);

    // Opening prepares existing components (including SDK microphone prewarming).
    // Recognition and web requests require the user's explicit Listen action.
    public bool Acquire()
    {
        if (!Application.isPlaying || quitting || !HasReferences) return false;
        if (acquired) return true;
        wake = manager.wakeWordManager;
        music = GetComponent<RayBanMusicBridge>();
        rootWasActive = serviceRoot.activeSelf;
        managerWasEnabled = manager.enabled;
        wakeWasEnabled = wake.enabled;
        voiceWasEnabled = wake.appVoice.enabled;
        previousAutoListen = manager.activateVoiceAfterResponse;
        previousActionOnly = wake.onlyActivateFromAction;
        previousKeyboard = wake.useKeyboardShortCutActivation;
        manager.activateVoiceAfterResponse = false;
        wake.onlyActivateFromAction = true;
        wake.useKeyboardShortCutActivation = false;
        manager.SetExternalPresentation(true, true);
        if (!hasPresented) { manager.ResetUI(); hasPresented = true; }
        wake.PrepareVoiceService();
        manager.enabled = wake.enabled = true;
        acquired = true;
        requested = false;
        failure = null;
        wake.OnResponseDetected.AddListener(OnRecognized);
        wake.appVoice.VoiceEvents.OnPartialTranscription.AddListener(OnPartial);
        wake.appVoice.VoiceEvents.OnError.AddListener(OnVoiceError);
        wake.appVoice.VoiceEvents.OnRequestCompleted.AddListener(OnRecognitionComplete);
        serviceRoot.SetActive(true);
        AssistantMicrophoneLifetime.PrepareForAssistant();
        return true;
    }

    public void ToggleListening()
    {
        if (!acquired || paused) return;
        if (IsBusy || IsListening) { Stop(); return; }
        if (!CanListen) return;
        failure = null;
        question = "";
        manager.ResetUI();
        requested = true;
        requestedAt = Time.realtimeSinceStartup;
        UpdateMusicInterruption();
        string activationStage = "prepare-input";
        try
        {
            AssistantMicrophoneLifetime.PrepareForAssistant();
            activationStage = "activate-voice";
            if (!wake.TryActivateAssistant())
            {
                Stop();
                failure = wake.ActivationFailure ?? "Voice input did not start · try again";
            }
        }
        catch (System.Exception exception)
        {
            LogStartupException(activationStage, exception);
            try { Stop(); }
            catch (System.Exception cleanupException)
            {
                LogStartupException("cleanup-after-failed-start", cleanupException);
            }
            failure = "Voice startup failed · try again";
        }
    }

    private static void LogStartupException(string stage, System.Exception exception)
    {
        // Provider exception messages can contain speech, URLs or credentials.
        // Record only bounded CLR type/method identifiers, never exception text
        // or the raw stack trace (which can include file paths and native text).
        var diagnostic = new System.Text.StringBuilder("[AssistantStartup] stage=");
        diagnostic.Append(stage).Append(" exception=").Append(SafeDiagnosticIdentifier(exception.GetType().FullName));
        diagnostic.Append(" frames=");
        try
        {
            var frames = new System.Diagnostics.StackTrace(exception, false).GetFrames();
            if (frames == null || frames.Length == 0) diagnostic.Append("unavailable");
            else
            {
                for (int index = 0; index < frames.Length && index < 16; index++)
                {
                    if (index > 0) diagnostic.Append(" -> ");
                    var method = frames[index].GetMethod();
                    diagnostic.Append(SafeDiagnosticIdentifier(method?.DeclaringType?.FullName));
                    diagnostic.Append('.').Append(SafeDiagnosticIdentifier(method?.Name));
                }
            }
        }
        catch
        {
            // Some IL2CPP builds omit managed stack metadata.
            diagnostic.Append("unavailable");
        }
        Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", diagnostic.ToString());
    }

    private static string SafeDiagnosticIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value)) return "unknown";
        var result = new System.Text.StringBuilder();
        foreach (char character in value)
        {
            if (result.Length >= 120) break;
            bool letterOrDigit = character >= 'a' && character <= 'z' ||
                character >= 'A' && character <= 'Z' || character >= '0' && character <= '9';
            result.Append(letterOrDigit || character == '.' || character == '_' || character == '+' ||
                character == '<' || character == '>' || character == '`' ? character : '_');
        }
        return result.ToString();
    }

    public void Stop()
    {
        requested = false;
        if (wake != null) wake.CancelListening();
        if (manager != null) manager.CancelResponse();
        if (acquired) AssistantMicrophoneLifetime.ReleaseIdleMicrophone();
        if (music != null) music.SetAssistantAudioActive(false, !paused && !quitting && isActiveAndEnabled);
    }

    public void Clear()
    {
        if (!acquired) return;
        Stop();
        manager.ResetUI();
        manager.ClearMemory();
        question = "";
        failure = null;
    }

    private void OnPartial(string text)
    {
        if (acquired && requested) question = text ?? "";
    }

    private void OnRecognized(WitResponseNode response)
    {
        if (!acquired || !requested) return;
        question = response != null ? response.GetTranscription() ?? "" : "";
        requested = false;
        if (string.IsNullOrWhiteSpace(question)) failure = "No speech recognized · try again";
    }

    private void OnVoiceError(string error, string message)
    {
        if (!acquired || !requested) return;
        Stop();
        // Provider messages can include sensitive data; show a bounded local message.
        failure = "Voice recognition unavailable · check microphone and connection";
    }

    private void OnRecognitionComplete()
    {
        // This SDK event follows all response/error callbacks. A completed
        // request without a recognized response must not remain busy forever.
        if (!acquired || !requested) return;
        requested = false;
        if (wake != null) wake.CancelListening();
        failure = "No speech recognized · try again";
    }

    private void Update()
    {
        if (acquired && requested && Time.realtimeSinceStartup - requestedAt > 45f)
        {
            Stop();
            failure = "Voice recognition timed out · try again";
        }
        UpdateMusicInterruption();
    }

    private void UpdateMusicInterruption()
    {
        // Hold one interruption through recognition, response loading and all
        // TTS chunks. Music must not resume between phrases or echo into the mic.
        bool needsAudio = acquired && !paused && (requested || IsListening ||
            (manager != null && manager.IsResponding));
        if (music != null)
            music.SetAssistantAudioActive(needsAudio, !paused && !quitting && isActiveAndEnabled);
    }

    public void Release(bool restoreOriginalState = true)
    {
        if (!acquired) return;
        Stop();
        acquired = false;
        if (wake != null)
        {
            wake.OnResponseDetected.RemoveListener(OnRecognized);
            if (wake.appVoice != null)
            {
                wake.appVoice.VoiceEvents.OnPartialTranscription.RemoveListener(OnPartial);
                wake.appVoice.VoiceEvents.OnError.RemoveListener(OnVoiceError);
                wake.appVoice.VoiceEvents.OnRequestCompleted.RemoveListener(OnRecognitionComplete);
            }
        }
        // Restore component flags while the shared group is inactive. The legacy
        // UI and all its gesture adapters remain untouched throughout the lease.
        if (serviceRoot != null) serviceRoot.SetActive(false);
        AssistantMicrophoneLifetime.ReleaseIdleMicrophone();
        if (manager != null)
        {
            manager.SetExternalPresentation(false, false);
            manager.activateVoiceAfterResponse = previousAutoListen;
            manager.enabled = managerWasEnabled;
        }
        if (wake != null)
        {
            wake.onlyActivateFromAction = previousActionOnly;
            wake.useKeyboardShortCutActivation = previousKeyboard;
            wake.enabled = wakeWasEnabled;
            wake.RestoreVoiceServiceEnabled(voiceWasEnabled);
        }
        if (serviceRoot != null && restoreOriginalState && !quitting) serviceRoot.SetActive(rootWasActive);
    }

    private void OnApplicationPause(bool value)
    {
        paused = value;
        if (value && acquired) Stop();
    }
    private void OnApplicationQuit() { quitting = true; Release(false); }
    private void OnDisable() => Release(!quitting);
    private void OnDestroy() => Release(!quitting);
}
