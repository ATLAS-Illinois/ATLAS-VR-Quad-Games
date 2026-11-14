using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The original Azure caption backend, shared by the legacy page and display adapter.
public class LiveTranslationManager : MonoBehaviour
{
    private string subscriptionKey, region;
    [Header("UI References")]
    public Toggle micToggle;
    public TextMeshProUGUI liveTranscriptionText, captionHistoryText;
    public ScrollRect historyScrollView;
    [Header("Translation Controls")]
    public TMP_Dropdown sourceLanguageDropdown, targetLanguageDropdown;
    public Toggle viewModeToggle;

    public static readonly string[] LanguageNames = { "Spanish", "English", "Chinese", "Korean", "Japanese" };
    private static readonly string[] SourceCodes = { "es-ES", "en-US", "zh-CN", "ko-KR", "ja-JP" };
    private static readonly string[] TargetCodes = { "es", "en", "zh-Hans", "ko", "ja" };
    public bool IsListening { get; private set; }
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "Choose Start captions.";
    public int SourceIndex => sourceLanguageDropdown != null ? Mathf.Clamp(sourceLanguageDropdown.value, 0, 4) : sourceIndex;
    public int TargetIndex => targetLanguageDropdown != null ? Mathf.Clamp(targetLanguageDropdown.value, 0, 4) : targetIndex;
    public bool ShowTranslation => viewModeToggle == null ? translationView : viewModeToggle.isOn;
    public string CurrentText { get { lock (threadLocker) return ShowTranslation ? currentTranslation : currentOriginal; } }
    public string History => ShowTranslation ? translatedHistory : originalHistory;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(subscriptionKey) && !string.IsNullOrWhiteSpace(region);

    private volatile TranslationRecognizer recognizer;
    private readonly object threadLocker = new object();
    private readonly SemaphoreSlim transition = new SemaphoreSlim(1, 1);
    private readonly Queue<KeyValuePair<string, string>> pending = new Queue<KeyValuePair<string, string>>();
    private string currentOriginal = "", currentTranslation = "", originalHistory = "", translatedHistory = "";
    private string callbackTarget = "en", pendingFailure;
    private int generation, sourceIndex = 1, targetIndex;
    private volatile bool wantsListening, destroyed;
    private bool translationView = true;

    private void Awake()
    {
        var config = Resources.Load<APIConfig>("API_Secrets");
        if (config != null) { subscriptionKey = config.azureSubscriptionKey; region = config.azureRegion; }
    }

    private void Start()
    {
        // A bridge can receive a deliberate start before Unity dispatches Start.
        if (micToggle != null) { micToggle.SetIsOnWithoutNotify(wantsListening); micToggle.onValueChanged.AddListener(OnMicToggle); }
        if (viewModeToggle != null) viewModeToggle.onValueChanged.AddListener(OnViewModeChanged);
        if (sourceLanguageDropdown != null) sourceLanguageDropdown.onValueChanged.AddListener(OnDropdownChanged);
        if (targetLanguageDropdown != null) targetLanguageDropdown.onValueChanged.AddListener(OnDropdownChanged);
        Render();
    }

    private void OnMicToggle(bool value) => SetListening(value);
    private void OnViewModeChanged(bool value) => Render();
    private void OnDropdownChanged(int value)
    {
        // Changes while stopped never open the microphone.
        if (wantsListening) SetListening(true);
    }

    public void SetLanguages(int source, int target)
    {
        sourceIndex = Mathf.Clamp(source, 0, 4); targetIndex = Mathf.Clamp(target, 0, 4);
        if (sourceLanguageDropdown != null) sourceLanguageDropdown.SetValueWithoutNotify(sourceIndex);
        if (targetLanguageDropdown != null) targetLanguageDropdown.SetValueWithoutNotify(targetIndex);
        OnDropdownChanged(0);
    }

    public void SetTranslationView(bool translated)
    {
        translationView = translated;
        if (viewModeToggle != null) viewModeToggle.SetIsOnWithoutNotify(translated);
        Render();
    }

    public void ClearHistory()
    {
        originalHistory = translatedHistory = "";
        lock (threadLocker) { currentOriginal = currentTranslation = ""; pending.Clear(); }
        Render();
    }

    public void SetListening(bool value)
    {
        if (value && SourceIndex == TargetIndex)
        {
            SetListening(false);
            Status = "Choose a different target language, then start captions.";
            Render();
            return;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (value && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            Status = "Allow microphone access, then choose Start captions.";
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return;
        }
#endif
        wantsListening = value && !destroyed && isActiveAndEnabled;
        int version = ++generation;
        if (micToggle != null) micToggle.SetIsOnWithoutNotify(wantsListening);
        if (!wantsListening)
        {
            IsListening = false; Status = "Captions paused.";
            // Closing the page must release the microphone even while Azure's start is pending.
            var closing = recognizer;
            recognizer = null;
            DisposeRecognizer(closing);
            lock (threadLocker) { currentOriginal = currentTranslation = ""; pending.Clear(); pendingFailure = null; }
        }
        _ = Reconcile(version, SourceIndex, TargetIndex);
    }

    private async Task Reconcile(int version, int source, int target)
    {
        await transition.WaitAsync();
        try
        {
            if (version != generation) return;
            IsBusy = true;
            var old = recognizer;
            recognizer = null;
            IsListening = false;
            DisposeRecognizer(old);
            if (version != generation || !wantsListening || destroyed || !isActiveAndEnabled) return;
            if (!IsConfigured) { wantsListening = false; Status = "Caption service is not configured."; return; }
            Status = "Starting captions…";
            var config = SpeechTranslationConfig.FromSubscription(subscriptionKey, region);
            config.SpeechRecognitionLanguage = SourceCodes[source];
            config.AddTargetLanguage(TargetCodes[target]);
            var next = new TranslationRecognizer(config);
            lock (threadLocker) { callbackTarget = TargetCodes[target]; currentOriginal = currentTranslation = ""; pending.Clear(); pendingFailure = null; }
            recognizer = next;
            next.Recognizing += OnRecognizing; next.Recognized += OnRecognized; next.Canceled += OnCanceled;
            Task start = next.StartContinuousRecognitionAsync();
            if (await Task.WhenAny(start, Task.Delay(10000)) != start)
            {
                _ = start.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException();
            }
            await start;
            if (version == generation && wantsListening && !destroyed && isActiveAndEnabled)
            { IsListening = true; Status = "Listening in " + LanguageNames[source] + "…"; }
            // A newer queued transition owns cancellation if this start completed late.
        }
        catch
        {
            if (version == generation)
            { wantsListening = false; IsListening = false; Status = "Captions unavailable. Check microphone, internet and speech service."; }
            var failed = recognizer; recognizer = null;
            DisposeRecognizer(failed);
        }
        finally
        {
            IsBusy = false;
            transition.Release();
            if (!destroyed) { if (micToggle != null) micToggle.SetIsOnWithoutNotify(wantsListening); Render(); }
        }
    }

    private void DisposeRecognizer(TranslationRecognizer value)
    {
        if (value == null) return;
        value.Recognizing -= OnRecognizing; value.Recognized -= OnRecognized; value.Canceled -= OnCanceled;
        try { value.Dispose(); } catch { }
    }

    private void OnRecognizing(object sender, TranslationRecognitionEventArgs args)
    {
        lock (threadLocker)
        {
            if (!ReferenceEquals(sender, recognizer) || !wantsListening) return;
            currentOriginal = args.Result.Text;
            currentTranslation = args.Result.Translations.TryGetValue(callbackTarget, out var value) ? value : "";
        }
    }

    private void OnRecognized(object sender, TranslationRecognitionEventArgs args)
    {
        lock (threadLocker)
        {
            if (!ReferenceEquals(sender, recognizer) || !wantsListening || args.Result.Reason != ResultReason.TranslatedSpeech) return;
            string translated = args.Result.Translations.TryGetValue(callbackTarget, out var value) ? value : "";
            if (pending.Count >= 16) pending.Dequeue();
            pending.Enqueue(new KeyValuePair<string, string>(args.Result.Text, translated));
            currentOriginal = currentTranslation = "";
        }
    }

    private void OnCanceled(object sender, TranslationRecognitionCanceledEventArgs args)
    {
        lock (threadLocker)
            if (ReferenceEquals(sender, recognizer) && wantsListening) pendingFailure = "Caption service stopped. Choose Start to retry.";
    }

    private void Update()
    {
        string failure;
        lock (threadLocker)
        {
            while (pending.Count > 0)
            {
                var sentence = pending.Dequeue();
                originalHistory = AppendBounded(originalHistory, sentence.Key);
                translatedHistory = AppendBounded(translatedHistory, sentence.Value);
            }
            failure = pendingFailure; pendingFailure = null;
        }
        if (failure != null) { SetListening(false); Status = failure; }
        Render();
    }

    private static string AppendBounded(string history, string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence)) return history;
        string result = history + sentence + "\n\n";
        if (result.Length <= 6000) return result;
        int start = result.Length - 6000;
        if (char.IsLowSurrogate(result[start])) start++;
        return result.Substring(start);
    }

    private void Render()
    {
        if (destroyed) return;
        string current = CurrentText;
        if (liveTranscriptionText != null) { liveTranscriptionText.richText = false; liveTranscriptionText.text = string.IsNullOrEmpty(current) ? Status : current; }
        if (captionHistoryText != null) { captionHistoryText.richText = false; captionHistoryText.text = History; }
    }

    private void OnApplicationPause(bool paused) { if (paused) SetListening(false); }
    private void OnDisable() => SetListening(false);
    private void OnDestroy()
    {
        destroyed = true;
        SetListening(false);
        if (micToggle != null) micToggle.onValueChanged.RemoveListener(OnMicToggle);
        if (viewModeToggle != null) viewModeToggle.onValueChanged.RemoveListener(OnViewModeChanged);
        if (sourceLanguageDropdown != null) sourceLanguageDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        if (targetLanguageDropdown != null) targetLanguageDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
    }
}
