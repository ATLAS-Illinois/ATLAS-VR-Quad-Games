using UnityEngine;
using Oculus.Voice;
using Meta.WitAi.Json;
using Meta.WitAi;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

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


    [Header("Settings")]
    [SerializeField] private float baseScale = 1f;       // Default resting scale
    [SerializeField] private float maxScale = 2f;        // Maximum scale when loudest
    [SerializeField] private float sensitivity = 1f;     // Mic value multiplier
    [SerializeField] private float smoothSpeed = 8f;     // Higher = snappier, lower = smoother

    private Vector3 currentVolumeScale;

    private void Awake()
    {

        currentVolumeScale = Vector3.one * baseScale;
    }

    public void UpdateVolumeUI(float micValue)
    {
        if (!gameObject.activeInHierarchy) 
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
        appVoice.VoiceEvents.OnResponse.AddListener(HandleResponse);

        appVoice.VoiceEvents.OnMicAudioLevelChanged.AddListener(UpdateVolumeUI);
        appVoice.VoiceEvents.OnPartialTranscription.AddListener(UpdateTranscriptionUI);


        if(!onlyActivateFromAction)
            appVoice.Activate();
    }

    public void UpdateTranscriptionUI(string transcriptionValue)
    {
        if(voiceActivated)
            transcriptionText.text = transcriptionValue;
    }

    public void Activate()
    {
        voiceActivated = true;
        Debug.Log("Wake word detected");
        transcriptionText.text = "";
        appVoice.Activate();
        OnWakeWordDetected.Invoke();
    }

    public void DeActivate()
    {
        voiceActivated = false;
        transcriptionText.text = "";
        appVoice.Deactivate();
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
        Debug.Log("CHAT GPT ASKING : " + witResponse.GetTranscription() + "-" + witResponse.GetIntentName());

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
                transcriptionText.text = "";
                OnWakeWordDetected.Invoke();
            }
        }

        if (!onlyActivateFromAction)
            appVoice.Activate();
    }
}
