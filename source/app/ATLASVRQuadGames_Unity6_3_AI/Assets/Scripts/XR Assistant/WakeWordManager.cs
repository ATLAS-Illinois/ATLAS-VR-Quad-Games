using UnityEngine;
using Oculus.Voice;
using Meta.WitAi.Json;
using Meta.WitAi;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using Meta.WitAi.Configuration;
using Meta.WitAi.Requests;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

[DefaultExecutionOrder(-1000)]
public class WakeWordManager : MonoBehaviour
{
    public bool onlyActivateFromAction = true;
    public bool useKeyboardShortCutActivation = true;

    public AppVoiceExperience appVoice;
    public Transform micVolume;
    public float micVolumeMultiplier = 5;
    public TextMeshProUGUI transcriptionText;

    public UnityEvent OnWakeWordDetected;
    public UnityEvent<WitResponseNode> OnResponseDetected;

    private bool voiceActivated = false;
    public bool IsVoiceActivated => voiceActivated;
    public bool IsInitialized { get; private set; }
    public string ActivationFailure { get; private set; }
    private bool hasStarted;
#if UNITY_ANDROID && !UNITY_EDITOR
    private AssistantWitVoice androidVoice;
    public VoiceService ActiveVoice => androidVoice;
    public bool IsVoiceServiceReady => androidVoice != null && androidVoice.IsInitialized && androidVoice.isActiveAndEnabled;
    // Listen also performs lazy initialization after a permission grant.
    public bool CanRequestVoiceInput => appVoice != null;
    private bool HasInitializedVoice => androidVoice != null && androidVoice.IsInitialized;
#else
    public VoiceService ActiveVoice => appVoice;
    public bool IsVoiceServiceReady => ActiveVoice != null && ActiveVoice.isActiveAndEnabled;
    public bool CanRequestVoiceInput => IsVoiceServiceReady;
    private bool HasInitializedVoice => appVoice != null;
#endif
    public bool IsMicActive => IsVoiceServiceReady && ActiveVoice.MicActive;


    [Header("Settings")]
    [SerializeField] private float baseScale = 1f;       // Default resting scale
    [SerializeField] private float maxScale = 2f;        // Maximum scale when loudest
    [SerializeField] private float sensitivity = 1f;     // Mic value multiplier
    [SerializeField] private float smoothSpeed = 8f;     // Higher = snappier, lower = smoother

    private Vector3 currentVolumeScale;

    private void Awake()
    {

        currentVolumeScale = Vector3.one * baseScale;
#if UNITY_ANDROID && !UNITY_EDITOR
        PrepareVoiceService();
#endif
        InitializeListeners();
    }

    // The serialized AppVoiceExperience remains the configuration/event owner.
    // SDK 62's Android platform logger cannot attach in the current player;
    // direct Wit uses the same recognition API without that platform binding.
    public void PrepareVoiceService()
    {
        if (appVoice == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        appVoice.enabled = false;
        if (androidVoice == null)
        {
            var voiceObject = new GameObject("Assistant Managed Wit");
            voiceObject.SetActive(false);
            voiceObject.transform.SetParent(appVoice.transform, false);
            androidVoice = voiceObject.AddComponent<AssistantWitVoice>();
            androidVoice.RuntimeConfiguration = appVoice.RuntimeConfiguration;
            androidVoice.VoiceEvents = appVoice.VoiceEvents;
            androidVoice.TelemetryEvents = appVoice.TelemetryEvents;
            androidVoice.ShouldWrap = true;
            androidVoice.ShouldLog = false;
        }
        // Wit.Awake creates AudioBuffer and prewarms Unity's input. Defer that
        // lifecycle entirely until Android microphone permission is granted.
        androidVoice.gameObject.SetActive(Permission.HasUserAuthorizedPermission(Permission.Microphone));
#else
        appVoice.enabled = true;
#endif
    }

    public void RestoreVoiceServiceEnabled(bool wasEnabled)
    {
        if (appVoice == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        // Never restart the failed Android platform binding during close/reopen.
        appVoice.enabled = false;
#else
        appVoice.enabled = wasEnabled;
#endif
    }

    private void OnEnable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        PrepareVoiceService();
#endif
        if (hasStarted && !onlyActivateFromAction) TryActivateVoice();
    }

    public void UpdateVolumeUI(float micValue)
    {
        if (!gameObject.activeInHierarchy || micVolume == null)
            return;

        // Target scale based on mic value
        float targetScaleValue = baseScale + Mathf.Clamp01(micValue * sensitivity) * (maxScale - baseScale);
        Vector3 targetScale = Vector3.one * targetScaleValue;

        // Smoothly interpolate
        currentVolumeScale = Vector3.Lerp(currentVolumeScale, targetScale, Time.deltaTime * smoothSpeed);

        micVolume.localScale = currentVolumeScale;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitializeListeners();
        hasStarted = true;
        if (!onlyActivateFromAction && !voiceActivated) TryActivateVoice();
    }

    private void InitializeListeners()
    {
        if (IsInitialized || appVoice == null) return;
        appVoice.VoiceEvents.OnResponse.AddListener(HandleResponse);

        appVoice.VoiceEvents.OnMicAudioLevelChanged.AddListener(UpdateVolumeUI);
        appVoice.VoiceEvents.OnPartialTranscription.AddListener(UpdateTranscriptionUI);


        IsInitialized = true;
    }

    public void UpdateTranscriptionUI(string transcriptionValue)
    {
        if(voiceActivated && transcriptionText != null)
            transcriptionText.text = transcriptionValue;
    }

    // Keep the void signature used by serialized UnityEvent callbacks.
    public void Activate() => TryActivateAssistant();

    public bool TryActivateAssistant()
    {
        if (!isActiveAndEnabled || appVoice == null) return false;
        InitializeListeners();
        voiceActivated = true;
        if (transcriptionText != null) transcriptionText.text = "";
        if (!TryActivateVoice())
        {
            voiceActivated = false;
            return false;
        }
        OnWakeWordDetected?.Invoke();
        return true;
    }

    private bool TryActivateVoice()
    {
        ActivationFailure = null;
        PrepareVoiceService();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            ActivationFailure = "Allow microphone access, then select Listen";
            return false;
        }
#endif
        AssistantMicrophoneLifetime.PrepareForAssistant();
        var voice = ActiveVoice;
        if (!IsVoiceServiceReady || !voice.CanActivateAudio())
        {
            ActivationFailure = "Voice input is not ready · try again";
            return false;
        }
        if (!voice.CanSend())
        {
            ActivationFailure = "Voice service unavailable · check connection and configuration";
            return false;
        }
        var request = voice.Activate(new WitRequestOptions(), new VoiceServiceRequestEvents());
        if (request != null) return true;
        ActivationFailure = "Voice input did not start · try again";
        return false;
    }

    public void DeActivate()
    {
        voiceActivated = false;
        if (transcriptionText != null) transcriptionText.text = "";
        if (HasInitializedVoice) ActiveVoice.Deactivate();
    }

    // Leaving the new Assistant screen must also cancel pending recognition so
    // an older response cannot be accepted after the screen is reopened.
    public void CancelListening()
    {
        voiceActivated = false;
        if (HasInitializedVoice) ActiveVoice.DeactivateAndAbortRequest();
    }

    private void OnDisable()
    {
        CancelListening();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (androidVoice != null) androidVoice.gameObject.SetActive(false);
#endif
        AssistantMicrophoneLifetime.ReleaseIdleMicrophone();
    }

    private void OnDestroy()
    {
        if (appVoice == null) return;
        appVoice.VoiceEvents.OnResponse.RemoveListener(HandleResponse);
        appVoice.VoiceEvents.OnMicAudioLevelChanged.RemoveListener(UpdateVolumeUI);
        appVoice.VoiceEvents.OnPartialTranscription.RemoveListener(UpdateTranscriptionUI);
    }

    private void Update()
    {
        if(useKeyboardShortCutActivation)
        {
            if(Input.GetKeyDown(KeyCode.L))
            {
                Activate();
            }
        }
    }


    public void HandleResponse(WitResponseNode witResponse)
    {
        if (!isActiveAndEnabled || witResponse == null) return;
        // Transcriptions can contain private speech; keep them out of logs.

        if (voiceActivated)
        {
            Debug.Log("Voice Activated : ");
            voiceActivated = false;
            OnResponseDetected.Invoke(witResponse);
        }
        else
        {
            if (onlyActivateFromAction)
                return;

            if(witResponse.GetIntentName() == "wake_word")
            {
                voiceActivated = true;
                Debug.Log("Wake word detected");
                if (transcriptionText != null) transcriptionText.text = "";
                OnWakeWordDetected.Invoke();
            }
        }

        if (!onlyActivateFromAction)
            TryActivateVoice();
    }
}
