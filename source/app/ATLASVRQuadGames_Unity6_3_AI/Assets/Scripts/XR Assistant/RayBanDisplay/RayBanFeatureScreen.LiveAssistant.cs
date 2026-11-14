using UnityEngine;
using TMPro;
using UnityEngine.UI;

public partial class RayBanFeatureScreen
{
    private string lastAssistantResponse;
    private int assistantResponsePage;
    private int assistantResponsePages = 1;
    [SerializeField] private RawImage liveAssistantIllustration;

    private void BuildAssistantIllustration(RectTransform view)
    {
        liveAssistantIllustration = Rect("Answer illustration", view, 42, 292, 112, 112).gameObject.AddComponent<RawImage>();
        liveAssistantIllustration.raycastTarget = false;
        liveAssistantIllustration.color = Color.white;
        liveAssistantIllustration.enabled = false;
    }

    private bool HandleLiveAssistant(string action)
    {
        if (shell == null || !shell.UsesLiveFeatures || (action != "comm:assistant-listen" && action != "comm:assistant-question")) return false;
        var bridge = shell.assistantBridge;
        if (bridge == null) return true;
        if (action == "comm:assistant-listen") bridge.ToggleListening();
        else if (assistantResponsePages > 1)
            assistantResponsePage = (assistantResponsePage + 1) % assistantResponsePages;
        else bridge.Clear();
        RefreshLiveAssistant();
        return true;
    }

    private void RefreshLiveAssistant()
    {
        if (shell == null || !shell.UsesLiveFeatures || assistantResponse == null) return;
        assistantQuestion.richText = false;
        assistantResponse.richText = false;
        var bridge = shell.assistantBridge;
        string answer = bridge != null ? bridge.Response : "";
        bool layoutChanged = RefreshAssistantIllustration(bridge != null ? bridge.Illustration : null);
        bool changed = answer != lastAssistantResponse;
        if (changed) { lastAssistantResponse = answer; assistantResponsePage = 0; }
        assistantQuestion.text = bridge != null && !string.IsNullOrWhiteSpace(bridge.Question)
            ? bridge.Question : "What would you like to ask?";
        string displayedAnswer = string.IsNullOrWhiteSpace(answer)
            ? "Select Listen, then ask your question. Your response will appear here."
            : answer;
        if (layoutChanged || assistantResponse.overflowMode != TextOverflowModes.Page || assistantResponse.text != displayedAnswer)
        {
            // Layout, explicit newlines and font metrics decide page boundaries;
            // never cut characters or silently ellipsize the readable answer.
            assistantResponse.overflowMode = TextOverflowModes.Page;
            assistantResponse.text = displayedAnswer;
            assistantResponse.pageToDisplay = 1;
            assistantResponse.ForceMeshUpdate(true);
            assistantResponsePages = Mathf.Max(1, assistantResponse.textInfo.pageCount);
        }
        assistantResponsePage = Mathf.Clamp(assistantResponsePage, 0, assistantResponsePages - 1);
        assistantResponse.pageToDisplay = assistantResponsePage + 1;
        assistantStatus.text = bridge != null ? bridge.Status : "Assistant is not connected";
        if (assistantResponsePages > 1) assistantStatus.text += " · " + (assistantResponsePage + 1) + "/" + assistantResponsePages;
        bool busy = bridge != null && (bridge.IsBusy || bridge.IsListening);
        assistantStatusGlyph.Symbol = bridge != null && bridge.IsListening ? "mic" : "assistant";
        SetButtonLabel("comm:assistant-listen", busy ? "Stop" : "Listen");
        SetButtonSymbol("comm:assistant-listen", busy ? "close" : "mic");
        SetControlAvailable("comm:assistant-listen", bridge != null && (bridge.CanListen || busy));
        SetButtonLabel("comm:assistant-question", assistantResponsePages > 1 ? "Next page" : "Clear");
        SetButtonSymbol("comm:assistant-question", assistantResponsePages > 1 ? "next" : "close");
        SetControlAvailable("comm:assistant-question", bridge != null && (bridge.IsAcquired || assistantResponsePages > 1));
    }

    private bool RefreshAssistantIllustration(Texture picture)
    {
        if (liveAssistantIllustration == null) return false;
        bool visible = picture != null && picture.width > 0 && picture.height > 0;
        bool changed = liveAssistantIllustration.enabled != visible || liveAssistantIllustration.texture != picture;
        if (!changed) return false;
        liveAssistantIllustration.enabled = visible;
        liveAssistantIllustration.texture = visible ? picture : null;
        var answerRect = assistantResponse.rectTransform;
        answerRect.anchoredPosition = new Vector2(visible ? 174 : 42, -292);
        answerRect.sizeDelta = new Vector2(visible ? 384 : 516, 170);
        if (visible)
        {
            float ratio = picture.width / (float)picture.height;
            float width = ratio >= 1 ? 112 : 112 * ratio;
            float height = ratio >= 1 ? 112 / ratio : 112;
            liveAssistantIllustration.rectTransform.sizeDelta = new Vector2(width, height);
            liveAssistantIllustration.rectTransform.anchoredPosition = new Vector2(42 + (112 - width) * .5f, -292 - (112 - height) * .5f);
            liveAssistantIllustration.uvRect = new Rect(0, 0, 1, 1);
        }
        return changed;
    }

}
