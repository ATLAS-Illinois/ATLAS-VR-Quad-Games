using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Meta.WitAi.Json;
using Meta.WitAi;
using TMPro;
using UnityEngine.UI;
using Meta.WitAi.TTS.Utilities;
using ZXing;

public class AIManager : MonoBehaviour
{
    [Header("Behavior")]
    public bool activateVoiceAfterResponse = true;

    [Header("Gemini Settings")]
    [Tooltip("Loaded automatically from API_Secrets.asset")]
    private string geminiApiKey;

    [Tooltip("The AI will automatically rotate through these models if one hits a quota limit (429) or is retired (404).")]
    public string[] fallbackModels = new string[] { "gemini-2.5-flash", "gemini-3-flash", "gemini-2.5-flash-lite" };
    private int currentModelIndex = 0;

    [Tooltip("Check this to allow the AI to search the live internet for answers")]
    public bool enableGoogleSearch = true;

    [Tooltip("Paste your Quad Games persona and rules here")]
    [TextArea(10, 20)]
    public string systemInstructions;

    [Header("Scene References")]
    public WakeWordManager wakeWordManager;
    [Tooltip("Existing monoscopic gameplay snapshot camera, not the headset's stereo eye camera.")]
    public Camera centerEyeCamera;
    public Toggle micToggle;

    [Header("UI References")]
    public TextMeshProUGUI aiResponseText;
    public RawImage describePicture;

    [Header("Editor Debug")]
    public Texture2D debugPicture;

    [Header("Audio")]
    public TTSSpeaker ttsSpeaker;

    [Header("QR")]
    public bool scanForQRCodes = true;

    private Texture2D picture;
    private Texture2D capturePicture;
    private Transform snapshotHead;
    private Texture2D responsePicture;
    private CancellationTokenSource responseCancellation;
    private BufferedAssistantSpeech responseSpeech;
    private bool responseInProgress;
    private bool externalPresentation;
    private bool externalPresentationVisible;
    public bool IsResponding => responseInProgress;
    public bool IsInitialized { get; private set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(geminiApiKey);
    private bool IsAssistantVisible => externalPresentation ? externalPresentationVisible :
        aiResponseText == null || aiResponseText.gameObject.activeInHierarchy;

    public void SetExternalPresentation(bool enabled, bool visible)
    {
        externalPresentation = enabled;
        externalPresentationVisible = enabled && visible;
        if (!IsAssistantVisible) CancelCurrentResponse();
    }

    public void CancelResponse() => CancelCurrentResponse();

    // Memory bank
    private readonly List<string> conversationHistory = new List<string>();

    private void Start()
    {
        APIConfig config = Resources.Load<APIConfig>("API_Secrets");
        if (config != null)
        {
            geminiApiKey = config.geminiApiKey;
        }
        else
        {
            Debug.LogError("API_Secrets file not found! Did you run ATLAS VR -> API Setup?");
        }

        if (wakeWordManager != null)
        {
            wakeWordManager.OnResponseDetected.AddListener(HandleResponse);
            wakeWordManager.OnWakeWordDetected.AddListener(CancelCurrentResponse);
        }

        if (aiResponseText != null)
            aiResponseText.text = "";

        if (describePicture != null)
            describePicture.enabled = false;
        IsInitialized = true;
    }

    private void OnDestroy()
    {
        // OnDisable already stopped playback. SDK62 may destroy its speaker
        // first and clear the queue before this callback, even while Unity's
        // object reference still compares non-null. Only cancel managed work.
        CancelCurrentResponse(false);
        if (wakeWordManager != null)
        {
            wakeWordManager.OnResponseDetected.RemoveListener(HandleResponse);
            wakeWordManager.OnWakeWordDetected.RemoveListener(CancelCurrentResponse);
        }
        if (capturePicture != null) Destroy(capturePicture);
        if (responsePicture != null) Destroy(responsePicture);
    }

    public async void HandleResponse(WitResponseNode response)
    {
        // A new deliberate mic activation cancels the older request via its
        // OnWakeWordDetected event. Duplicate completion callbacks cannot speak
        // the same answer over itself while the current request is still active.
        if (response == null || responseInProgress || !IsAssistantVisible) return;
        string input = response.GetTranscription();
        if (string.IsNullOrWhiteSpace(input)) return;
        CancellationTokenSource owner = BeginResponse();
        CancellationToken token = owner.Token;
        bool answerDisplayed = false;
        try
        {
            if (micToggle != null) micToggle.SetIsOnWithoutNotify(false);
            if (wakeWordManager != null) wakeWordManager.DeActivate();
            UpdateResultUI("loading...", null);

            string rawResult = await DescribeGemini(input, token);
            CheckResponseActive(token);
            string spokenText = rawResult;
            string imagePrompt = "";
            int imageTagIndex = rawResult.IndexOf("[IMAGE:", StringComparison.Ordinal);
            if (imageTagIndex != -1)
            {
                spokenText = rawResult.Substring(0, imageTagIndex).Trim();
                int endBracketIndex = rawResult.IndexOf("]", imageTagIndex, StringComparison.Ordinal);
                if (endBracketIndex != -1)
                    imagePrompt = rawResult.Substring(imageTagIndex + 7, endBracketIndex - imageTagIndex - 7).Trim();
            }

            // The readable answer survives image or speech service failures.
            UpdateResultUI(spokenText, null);
            answerDisplayed = true;
            if (!string.IsNullOrEmpty(imagePrompt))
            {
                Texture2D image = null;
                try
                {
                    image = await FetchImageFromWeb(imagePrompt, token);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error)
                {
                    Debug.LogWarning("[AIManager] Answer illustration unavailable (" + error.GetType().Name + ").");
                }
                try
                {
                    CheckResponseActive(token);
                    if (responsePicture != null) Destroy(responsePicture);
                    responsePicture = image;
                    image = null; // Ownership transfers only while this request is current.
                    UpdateResultUI(spokenText, responsePicture);
                }
                finally
                {
                    if (image != null) Destroy(image);
                }
            }

            if (ttsSpeaker != null && !string.IsNullOrWhiteSpace(spokenText))
            {
                responseSpeech = new BufferedAssistantSpeech(ttsSpeaker);
                await responseSpeech.SpeakAsync(spokenText, () => CheckResponseActive(token));
            }
            CheckResponseActive(token);
            // Never listen to the Assistant's own audio. This is deliberately
            // after successful playback, not immediately after queuing speech.
            if (activateVoiceAfterResponse && wakeWordManager != null)
                wakeWordManager.Activate();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested && this != null && IsAssistantVisible)
            {
                if (aiResponseText != null)
                    aiResponseText.text = answerDisplayed
                        ? aiResponseText.text + "\n\nVoice playback is unavailable. You can read the response or ask again."
                        : "The response could not be completed. Please try again.";
                Debug.LogWarning("[AIManager] Assistant request stopped (" + error.GetType().Name + ").");
            }
        }
        finally
        {
            EndResponse(owner);
        }
    }

    private async Task<Texture2D> FetchImageFromWeb(string prompt, CancellationToken token)
    {
        string encodedPrompt = UnityWebRequest.EscapeURL(prompt);
        string searchUrl =
            $"https://en.wikipedia.org/w/api.php?action=query&generator=search&gsrsearch={encodedPrompt}&gsrlimit=1&prop=pageimages&pithumbsize=1000&format=json";

        string imageUrl = "";

        using (UnityWebRequest searchRequest = UnityWebRequest.Get(searchUrl))
        {
            searchRequest.SetRequestHeader("User-Agent", "ATLASVRQuadGames-XRAssistant/1.0");

            searchRequest.timeout = 8;
            await SendRequest(searchRequest, token);

            if (searchRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = searchRequest.downloadHandler.text;

                int sourceIndex = jsonResponse.IndexOf("\"thumbnail\":{\"source\":\"", StringComparison.Ordinal);
                if (sourceIndex != -1)
                {
                    int startIndex = sourceIndex + 23;
                    int endIndex = jsonResponse.IndexOf("\"", startIndex, StringComparison.Ordinal);
                    if (endIndex != -1)
                    {
                        imageUrl = jsonResponse.Substring(startIndex, endIndex - startIndex);
                        imageUrl = imageUrl.Replace("\\/", "/");
                    }
                }
            }
            else
            {
                Debug.LogError("Image search failed: " + searchRequest.error);
                return null;
            }
        }

        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogWarning("No related image found on the web for: " + prompt);
            return null;
        }

        using (UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            imageRequest.timeout = 8;
            await SendRequest(imageRequest, token);

            if (imageRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D downloadedTex = DownloadHandlerTexture.GetContent(imageRequest);
                downloadedTex.filterMode = FilterMode.Bilinear;
                downloadedTex.anisoLevel = 8;
                downloadedTex.Apply();
                return downloadedTex;
            }
            else
            {
                Debug.LogError("Failed to download image from URL: " + imageRequest.error);
                return null;
            }
        }
    }

    private string DecodeQRCode(Color32[] pixels, int width, int height)
    {
        try
        {
            var barcodeReader = new BarcodeReader
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
                }
            };

            var result = barcodeReader.Decode(pixels, width, height);
            if (result != null)
                return result.Text;
        }
        catch (Exception e)
        {
            Debug.LogWarning("QR scanning error: " + e.Message);
        }

        return null;
    }

    private Texture2D CaptureCenterEyeSnapshot()
    {
        if (centerEyeCamera == null)
            return null;

        if (snapshotHead == null)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null) snapshotHead = rig.centerEyeAnchor;
        }
        if (snapshotHead == null || centerEyeCamera.transform == snapshotHead)
            return null;

        int width = centerEyeCamera.pixelWidth > 0 ? centerEyeCamera.pixelWidth : Screen.width;
        int height = centerEyeCamera.pixelHeight > 0 ? centerEyeCamera.pixelHeight : Screen.height;

        if (width <= 0 || height <= 0)
            return null;

        RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = centerEyeCamera.targetTexture;
        Vector3 previousPosition = centerEyeCamera.transform.position;
        Quaternion previousRotation = centerEyeCamera.transform.rotation;
        float previousFov = centerEyeCamera.fieldOfView;
        StereoTargetEyeMask previousStereo = centerEyeCamera.stereoTargetEye;

        try
        {
            // The Camera screen may be closed or zoomed. Capture the current
            // head view without opening that screen or redirecting an XR eye.
            centerEyeCamera.transform.SetPositionAndRotation(snapshotHead.position, snapshotHead.rotation);
            centerEyeCamera.fieldOfView = 60f;
            centerEyeCamera.stereoTargetEye = StereoTargetEyeMask.None;
            centerEyeCamera.targetTexture = tempRT;
            centerEyeCamera.Render();

            RenderTexture.active = tempRT;

            if (capturePicture == null || capturePicture.width != width || capturePicture.height != height)
            {
                if (capturePicture != null) Destroy(capturePicture);
                capturePicture = new Texture2D(width, height, TextureFormat.RGB24, false);
            }

            capturePicture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            capturePicture.Apply(false, false);

            return capturePicture;
        }
        catch (Exception e)
        {
            Debug.LogError("Center-eye snapshot failed: " + e.Message);
            return null;
        }
        finally
        {
            centerEyeCamera.targetTexture = previousTarget;
            centerEyeCamera.stereoTargetEye = previousStereo;
            centerEyeCamera.fieldOfView = previousFov;
            centerEyeCamera.transform.SetPositionAndRotation(previousPosition, previousRotation);
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(tempRT);
        }
    }

    public void ResetUI()
    {
        CancelCurrentResponse();
        if (describePicture != null)
            describePicture.enabled = false;

        if (aiResponseText != null)
            aiResponseText.text = "";

        if (wakeWordManager != null && wakeWordManager.transcriptionText != null)
            wakeWordManager.transcriptionText.text = "";
    }

    public void ClearMemory()
    {
        CancelCurrentResponse();
        conversationHistory.Clear();
        Debug.Log("AI memory cleared.");
    }

    public void UpdateResultUI(string resultText, Texture2D resultTexture)
    {
        if (aiResponseText != null)
            aiResponseText.text = resultText;

        if (describePicture == null)
            return;

        if (resultTexture != null)
        {
            describePicture.enabled = true;
            describePicture.texture = resultTexture;

            RectTransform rectTransform = describePicture.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                float imageAspect = (float)resultTexture.width / resultTexture.height;
                float uiAspect = rectTransform.rect.width / rectTransform.rect.height;

                if (imageAspect > uiAspect)
                {
                    float cropWidth = uiAspect / imageAspect;
                    describePicture.uvRect = new Rect((1f - cropWidth) / 2f, 0f, cropWidth, 1f);
                }
                else
                {
                    float cropHeight = imageAspect / uiAspect;
                    describePicture.uvRect = new Rect(0f, (1f - cropHeight) / 2f, 1f, cropHeight);
                }
            }
            else
            {
                describePicture.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }
        else
        {
            describePicture.enabled = false;
            describePicture.texture = null;
            describePicture.uvRect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    public async Task<string> AskGemini(string input)
    {
        return await DescribeGemini(input);
    }

    public Task<string> DescribeGemini(string input) => DescribeGemini(input, CancellationToken.None);

    private async Task<string> DescribeGemini(string input, CancellationToken token)
    {
        CheckResponseActive(token);
        picture = null; // Never reuse an earlier image when capture is unavailable.
        Color32[] pixels = null;
        int imgWidth = 0;
        int imgHeight = 0;
        string cameraDiagnosticNote = "";

        if (Application.isEditor && debugPicture != null)
        {
            picture = debugPicture;
            imgWidth = picture.width;
            imgHeight = picture.height;

            try
            {
                pixels = picture.GetPixels32();
            }
            catch (UnityException e)
            {
                Debug.LogError("Enable Read/Write on debugPicture. " + e.Message);
            }
        }
        else
        {
            Texture2D captured = CaptureCenterEyeSnapshot();
            if (captured != null)
            {
                picture = captured;
                imgWidth = picture.width;
                imgHeight = picture.height;
                pixels = picture.GetPixels32();
            }
            else
            {
                cameraDiagnosticNote =
                    " [SYSTEM NOTE: The camera snapshot is currently unavailable. If the user asks what you see, explicitly mention that your camera seems to not be showing anything.]";
            }
        }

        if (scanForQRCodes && pixels != null)
        {
            string qrData = DecodeQRCode(pixels, imgWidth, imgHeight);
            if (!string.IsNullOrEmpty(qrData))
            {
                // QR contents are external input and must not be logged.
                input +=
                    $" \n[IMPORTANT SYSTEM COMMAND: The user is looking at a QR code containing this exact URL/Data: '{qrData}'. You MUST use your Google Search tool to identify exactly what it is. Do NOT guess.]";
            }
        }

        string base64Image = "";
        if (picture != null)
        {
            try
            {
                byte[] imageBytes = picture.EncodeToJPG();
                base64Image = Convert.ToBase64String(imageBytes);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to convert captured image: " + e.Message);
            }
        }

        string safeInput = EscapeString(input + cameraDiagnosticNote);

        string systemInstructionJson = string.IsNullOrEmpty(systemInstructions)
            ? ""
            : $@"
        ""system_instruction"": {{
            ""parts"": [
                {{""text"": ""{EscapeString(systemInstructions)}""}}
            ]
        }},";

        string toolsJson = enableGoogleSearch ? @", ""tools"": [{""googleSearch"": {}}]" : "";

        string imagePart = "";
        if (!string.IsNullOrEmpty(base64Image))
        {
            imagePart = $@", {{ ""inline_data"": {{ ""mime_type"": ""image/jpeg"", ""data"": ""{base64Image}"" }} }}";
        }

        string currentUserMsgWithImage = $@"{{
            ""role"": ""user"",
            ""parts"": [
                {{ ""text"": ""User: {safeInput}"" }}{imagePart}
            ]
        }}";

        string memoryPrefix = string.IsNullOrEmpty(base64Image) ? "" : "[User sent an image] ";
        string currentUserMsgTextOnly =
            $@"{{""role"": ""user"", ""parts"": [{{""text"": ""{memoryPrefix}User: {safeInput}""}}]}}";

        string historyJson = string.Join(",", conversationHistory);
        if (!string.IsNullOrEmpty(historyJson))
            historyJson += ",";

        string jsonPayload = $@"{{
            {systemInstructionJson}
            ""contents"": [
                {historyJson}
                {currentUserMsgWithImage}
            ]{toolsJson}
        }}";

        string responseText = await SendGeminiRequest(jsonPayload, token);
        CheckResponseActive(token);

        if (!responseText.StartsWith("Sorry") &&
            !responseText.StartsWith("Could not") &&
            !responseText.StartsWith("API Key"))
        {
            conversationHistory.Add(currentUserMsgTextOnly);
            conversationHistory.Add(
                $@"{{""role"": ""model"", ""parts"": [{{""text"": ""{EscapeString(responseText)}""}}]}}");
        }

        return responseText;
    }

    private async Task<string> SendGeminiRequest(string jsonPayload, CancellationToken token, int retryCount = 0)
    {
        if (string.IsNullOrEmpty(geminiApiKey))
        {
            Debug.LogError("Gemini API key is missing.");
            return "API Key is missing.";
        }

        if (fallbackModels == null || fallbackModels.Length == 0)
        {
            Debug.LogError("No Gemini fallback models configured.");
            return "No Gemini models configured.";
        }

        CheckResponseActive(token);
        currentModelIndex = Mathf.Clamp(currentModelIndex, 0, fallbackModels.Length - 1);
        string currentModel = fallbackModels[currentModelIndex];
        string url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{currentModel}:generateContent?key={geminiApiKey}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            request.timeout = 30;
            await SendRequest(request, token);

            if (request.result == UnityWebRequest.Result.Success)
            {
                GeminiResponseData response = JsonUtility.FromJson<GeminiResponseData>(request.downloadHandler.text);
                if (response != null &&
                    response.candidates != null &&
                    response.candidates.Length > 0 &&
                    response.candidates[0] != null &&
                    response.candidates[0].content != null &&
                    response.candidates[0].content.parts != null &&
                    response.candidates[0].content.parts.Length > 0)
                {
                    return response.candidates[0].content.parts[0].text;
                }

                Debug.LogWarning("Gemini returned success but response text could not be parsed.");
                return "Could not parse the response from Gemini.";
            }
            else
            {
                if (request.responseCode == 429 || request.responseCode == 404 || request.responseCode == 503)
                {
                    if (retryCount < fallbackModels.Length - 1)
                    {
                        string oldModel = fallbackModels[currentModelIndex];
                        currentModelIndex = (currentModelIndex + 1) % fallbackModels.Length;
                        Debug.LogWarning(
                            $"[AIManager] {oldModel} failed (Error {request.responseCode}). Switching to {fallbackModels[currentModelIndex]} and retrying...");
                        return await SendGeminiRequest(jsonPayload, token, retryCount + 1);
                    }

                    Debug.LogError("[AIManager] All fallback models failed.");
                    return "Sorry, I have run out of available models or requests right now.";
                }

                Debug.LogWarning($"[AIManager] Gemini request failed (HTTP {request.responseCode}).");
                return "Sorry, I encountered a network error.";
            }
        }
    }

    private CancellationTokenSource BeginResponse()
    {
        CancelCurrentResponse();
        responseCancellation = new CancellationTokenSource();
        responseInProgress = true;
        return responseCancellation;
    }

    private void EndResponse(CancellationTokenSource owner)
    {
        if (!ReferenceEquals(responseCancellation, owner)) return;
        responseSpeech?.Stop();
        responseSpeech = null;
        responseInProgress = false;
        responseCancellation = null;
        owner.Dispose();
    }

    private void CancelCurrentResponse() => CancelCurrentResponse(true);

    private void CancelCurrentResponse(bool releaseSpeech)
    {
        CancellationTokenSource previous = responseCancellation;
        responseCancellation = null;
        responseInProgress = false;
        previous?.Cancel();
        responseSpeech?.Stop(releaseSpeech);
        responseSpeech = null;
        previous?.Dispose();
        if (releaseSpeech && ttsSpeaker != null && ttsSpeaker.isActiveAndEnabled) ttsSpeaker.Stop();
    }

    private void CheckResponseActive(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (this == null || !isActiveAndEnabled || !Application.isPlaying || !IsAssistantVisible)
            throw new OperationCanceledException();
    }

    private async Task SendRequest(UnityWebRequest request, CancellationToken token)
    {
        CheckResponseActive(token);
        var operation = request.SendWebRequest();
        float deadline = Time.realtimeSinceStartup + request.timeout + 2f;
        try
        {
            while (!operation.isDone)
            {
                CheckResponseActive(token);
                if (Time.realtimeSinceStartup >= deadline)
                    throw new TimeoutException("Assistant web request timed out.");
                await Task.Yield();
            }
            CheckResponseActive(token);
        }
        catch
        {
            request.Abort();
            throw;
        }
    }

    private void Update()
    {
        if (responseInProgress && !IsAssistantVisible) CancelCurrentResponse();
    }

    private void OnDisable() => CancelCurrentResponse();

    private void OnApplicationPause(bool paused)
    {
        if (paused) CancelCurrentResponse();
    }

    private string EscapeString(string str)
    {
        if (string.IsNullOrEmpty(str))
            return "";

        return str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }
}

[Serializable]
public class GeminiResponseData
{
    public GeminiCandidate[] candidates;
}

[Serializable]
public class GeminiCandidate
{
    public GeminiContent content;
}

[Serializable]
public class GeminiContent
{
    public GeminiPart[] parts;
}

[Serializable]
public class GeminiPart
{
    public string text;
}
