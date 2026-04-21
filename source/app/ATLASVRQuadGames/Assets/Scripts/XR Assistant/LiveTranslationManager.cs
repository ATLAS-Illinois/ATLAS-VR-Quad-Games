using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Translation;
using System.Threading.Tasks;

public class LiveTranslationManager : MonoBehaviour
{
    [Header("Azure Credentials")]
    private string subscriptionKey;
    private string region;

    [Header("UI References")]
    public Toggle micToggle;
    public TextMeshProUGUI liveTranscriptionText;
    public TextMeshProUGUI captionHistoryText;
    public ScrollRect historyScrollView;

    [Header("Translation Controls")]
    public TMP_Dropdown sourceLanguageDropdown; // What you speak
    public TMP_Dropdown targetLanguageDropdown; // What it translates to
    public Toggle viewModeToggle; // ON = Show Translation, OFF = Show Original

    private TranslationRecognizer recognizer;
    private bool isListening = false;

    private string originalHistory = "";
    private string translatedHistory = "";

    // What Azure LISTENS for
    private string[] sourceLanguageCodes = new string[] { "es-ES", "en-US", "zh-CN", "ko-KR", "ja-JP" };

    // What Azure TRANSLATES into
    private string[] targetLanguageCodes = new string[] { "es", "en", "zh-Hans", "ko", "ja" };

    // Thread-safe variables
    private readonly object threadLocker = new object();
    private string currentTargetLanguageCode = "en"; // Cached for background thread
    private string currentOriginal = "";
    private string currentTranslation = "";
    private bool needsHistoryUpdate = false;
    private string newOriginalSentence = "";
    private string newTranslatedSentence = "";

    private void Awake()
    {
        APIConfig config = Resources.Load<APIConfig>("API_Secrets");
        if (config != null)
        {
            // FIX: Using the correct variable names that exist in your script
            subscriptionKey = config.azureSubscriptionKey;
            region = config.azureRegion;
        }
        else
        {
            Debug.LogError("[LiveTranslationManager] API_Secrets file not found! Run ATLAS VR -> API Setup.");
        }
    }

    private void Start()
    {
        liveTranscriptionText.text = "Waiting for speech...";
        captionHistoryText.text = "";

        if (micToggle != null) micToggle.onValueChanged.AddListener(OnMicToggle);
        if (viewModeToggle != null) viewModeToggle.onValueChanged.AddListener(OnViewModeChanged);
        if (sourceLanguageDropdown != null) sourceLanguageDropdown.onValueChanged.AddListener(OnDropdownChanged);
        if (targetLanguageDropdown != null) targetLanguageDropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    private async void OnMicToggle(bool isOn)
    {
        if (isOn) await StartTranslation();
        else await StopTranslation();
    }

    private async void OnDropdownChanged(int index)
    {
        // Restart the mic stream if they change languages while speaking
        if (isListening)
        {
            await StopTranslation();
            await StartTranslation();
        }
    }

    private async Task StartTranslation()
    {
        if (isListening) return;

        var config = SpeechTranslationConfig.FromSubscription(subscriptionKey, region);

        string sourceLang = sourceLanguageCodes[sourceLanguageDropdown.value];
        currentTargetLanguageCode = targetLanguageCodes[targetLanguageDropdown.value];

        config.SpeechRecognitionLanguage = sourceLang;
        config.AddTargetLanguage(currentTargetLanguageCode);

        recognizer = new TranslationRecognizer(config);
        recognizer.Recognizing += OnRecognizing;
        recognizer.Recognized += OnRecognized;
        recognizer.Canceled += OnCanceled;

        await recognizer.StartContinuousRecognitionAsync();
        isListening = true;

        lock (threadLocker) currentOriginal = "";
        liveTranscriptionText.text = $"Listening in {sourceLanguageDropdown.options[sourceLanguageDropdown.value].text}...";
    }

    private async Task StopTranslation()
    {
        if (!isListening || recognizer == null) return;

        await recognizer.StopContinuousRecognitionAsync();
        recognizer.Dispose();
        recognizer = null;
        isListening = false;
        liveTranscriptionText.text = "Captions Paused.";
    }

    private void OnRecognizing(object sender, TranslationRecognitionEventArgs e)
    {
        // Safely pull the translation using our cached target code
        string translated = e.Result.Translations.ContainsKey(currentTargetLanguageCode) ? e.Result.Translations[currentTargetLanguageCode] : "";
        lock (threadLocker)
        {
            currentOriginal = e.Result.Text;
            currentTranslation = translated;
        }
    }

    private void OnRecognized(object sender, TranslationRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.TranslatedSpeech)
        {
            lock (threadLocker)
            {
                newOriginalSentence = e.Result.Text;
                newTranslatedSentence = e.Result.Translations.ContainsKey(currentTargetLanguageCode) ? e.Result.Translations[currentTargetLanguageCode] : "";
                needsHistoryUpdate = true;
            }
        }
    }

    private void OnCanceled(object sender, TranslationRecognitionCanceledEventArgs e)
    {
        Debug.LogWarning($"Azure Translation Canceled: {e.Reason}");
    }

    private void Update()
    {
        lock (threadLocker)
        {
            if (!string.IsNullOrEmpty(currentOriginal) && !needsHistoryUpdate)
            {
                liveTranscriptionText.text = viewModeToggle.isOn ? currentTranslation : currentOriginal;
            }

            if (needsHistoryUpdate)
            {
                originalHistory += newOriginalSentence + "\n\n";
                translatedHistory += newTranslatedSentence + "\n\n";

                currentOriginal = "";
                currentTranslation = "";
                needsHistoryUpdate = false;

                UpdateHistoryText();
                liveTranscriptionText.text = isListening ? "Listening..." : "Captions Paused.";

                if (gameObject.activeInHierarchy && historyScrollView != null && historyScrollView.verticalNormalizedPosition < 0.05f)
                {
                    StartCoroutine(ScrollToBottom());
                }
            }
        }
    }

    private void OnViewModeChanged(bool isTranslationMode)
    {
        UpdateHistoryText();
        lock (threadLocker)
        {
            if (!string.IsNullOrEmpty(currentOriginal))
            {
                liveTranscriptionText.text = viewModeToggle.isOn ? currentTranslation : currentOriginal;
            }
        }
    }

    private void UpdateHistoryText()
    {
        captionHistoryText.text = viewModeToggle.isOn ? translatedHistory : originalHistory;
    }

    private System.Collections.IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        if (historyScrollView != null) historyScrollView.verticalNormalizedPosition = 0f;
    }

    private void OnDestroy()
    {
        if (isListening) _ = StopTranslation();
    }
}