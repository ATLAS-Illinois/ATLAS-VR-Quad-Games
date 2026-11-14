using TMPro;
using UnityEngine;

public partial class RayBanFeatureScreen
{
    [SerializeField] private TMP_FontAsset captionChineseFont;
    [SerializeField] private TMP_FontAsset captionJapaneseFont;
    [SerializeField] private TMP_Text captionPageLabel;
    private bool captionFollowLatest = true;

    public void EnsureCaptionPaging()
    {
        var view = views.Find(v => v.key == "captions");
        if (view == null || view.root == null || captionTranscript == null) return;
        captionTranscript.rectTransform.anchoredPosition = new Vector2(40, -252);
        captionTranscript.rectTransform.sizeDelta = new Vector2(520, 168);
        var panel = view.root.Find("Transcript panel") as RectTransform;
        if (panel != null) panel.sizeDelta = new Vector2(568, 210);
        if (captionPageLabel == null)
        {
            Button(view.root, "Earlier captions", "captions.earlier", "Earlier", "previous", 16, 438, 174, 54, 2, 0);
            Button(view.root, "Latest captions", "captions.latest", "Latest", "captions", 210, 438, 180, 54, 2, 1);
            Button(view.root, "Later captions", "captions.later", "Later", "next", 410, 438, 174, 54, 2, 2);
            captionPageLabel = Text(view.root, "Transcript position", "Live · 1 / 1", 300, 222, 260, 24, 16);
            captionPageLabel.alignment = TextAlignmentOptions.Right;
        }
        captionPageLabel.rectTransform.anchoredPosition = new Vector2(300, -222);
        captionPageLabel.rectTransform.sizeDelta = new Vector2(260, 24);
        foreach (var control in controls)
            if (control != null && (control.ActionId == "comm:captions-pause" || control.ActionId == "comm:captions-language")) control.Row = 3;
        RefreshCaptionPaging();
    }

    private bool HandleCaptionPaging(string action)
    {
        if (action != "captions.earlier" && action != "captions.later" && action != "captions.latest") return false;
        if (captionTranscript == null) return true;
        int count = Mathf.Max(1, captionTranscript.textInfo.pageCount);
        int page = Mathf.Clamp(captionTranscript.pageToDisplay, 1, count);
        captionFollowLatest = action == "captions.latest";
        if (action == "captions.earlier") page--;
        if (action == "captions.later") page++;
        captionTranscript.pageToDisplay = captionFollowLatest ? count : Mathf.Clamp(page, 1, count);
        RefreshCaptionPaging();
        if (focused != null && !focused.IsInteractable()) FocusAction("captions.latest");
        return true;
    }

    private void RefreshCaptionPaging()
    {
        if (captionTranscript == null || captionPageLabel == null) return;
        int count = Mathf.Max(1, captionTranscript.textInfo.pageCount);
        int page = Mathf.Clamp(captionTranscript.pageToDisplay, 1, count);
        captionPageLabel.text = (captionFollowLatest ? "Live" : "Reading") + " · " + page + " / " + count;
        SetControlAvailable("captions.earlier", page > 1);
        SetControlAvailable("captions.later", page < count);
        // Keep Latest focusable even when already following: it is a stable
        // destination if streaming updates change the number of pages.
        SetControlAvailable("captions.latest", true);
    }

    private void BuildLiveCaptions()
    {
        var view = views.Find(v => v.key == "captions");
        if (view == null) return;
        var panel = view.root.Find("Transcript panel") as RectTransform;
        if (panel != null) { panel.anchoredPosition = new Vector2(16, -220); panel.sizeDelta = new Vector2(568, 268); }
        var speaker = view.root.Find("Speaker");
        if (speaker != null) speaker.gameObject.SetActive(false);
        if (captionTranscript != null)
        { captionTranscript.rectTransform.anchoredPosition = new Vector2(40, -242); captionTranscript.rectTransform.sizeDelta = new Vector2(520, 222); captionTranscript.fontSize = 27; captionTranscript.richText = false; }
        Button(view.root, "Source language", "comm:captions-source", "Speak: English", "mic", 16, 154, 282, 52, 1, 0);
        Button(view.root, "Target language", "comm:captions-target", "To: Spanish", "captions", 302, 154, 282, 52, 1, 1);
        foreach (var control in controls)
            if (control != null && (control.ActionId == "comm:captions-pause" || control.ActionId == "comm:captions-language")) control.Row = 2;
    }

    private bool HandleLiveCaptions(string action)
    {
        var bridge = shell != null ? shell.captionBridge : null;
        switch (action)
        {
            case "comm:captions-pause": if (bridge != null) bridge.Toggle(); break;
            case "comm:captions-language": if (bridge != null) bridge.ToggleView(); break;
            case "comm:captions-source": if (bridge != null) bridge.CycleSource(); break;
            case "comm:captions-target": if (bridge != null) bridge.CycleTarget(); break;
            default: return false;
        }
        if (action == "comm:captions-language" || action == "comm:captions-source" || action == "comm:captions-target") captionFollowLatest = true;
        RefreshLiveCaptions();
        return true;
    }

    private void RefreshLiveCaptions()
    {
        if (shell == null || !shell.UsesLiveFeatures) return;
        var bridge = shell.captionBridge;
        var manager = bridge != null ? bridge.manager : null;
        string detail = bridge != null ? bridge.Status : "Caption service is not connected.";
        if (captionStatus != null)
        {
            captionStatus.fontSize = 20;
            captionStatus.text = manager == null ? "Captions not connected" : bridge.IsBusy ? "Preparing captions…" :
                bridge.IsListening ? "Listening…" : "Captions paused";
        }
        if (captionLanguage != null) captionLanguage.text = manager != null && manager.ShowTranslation ? "TRANSLATED" : "ORIGINAL";
        if (captionTranscript != null)
        {
            // Han characters share Unicode values but have regional glyph forms.
            // Original mode follows the source language; translation follows target.
            int language = manager != null ? (manager.ShowTranslation ? manager.TargetIndex : manager.SourceIndex) : 1;
            var typeface = language == 2 && captionChineseFont != null ? captionChineseFont :
                language == 4 && captionJapaneseFont != null ? captionJapaneseFont : font;
            bool fontChanged = typeface != null && captionTranscript.font != typeface;
            if (fontChanged) captionTranscript.font = typeface;
            string history = manager != null ? manager.History : "";
            string current = manager != null ? manager.CurrentText : "";
            string value = (history + current).Trim();
            if (manager != null && !bridge.IsListening && !bridge.IsBusy && value.Length > 0 &&
                detail != "Captions paused." && detail != "Choose Start captions.") value += "\n\n" + detail;
            captionTranscript.richText = false;
            value = value.Length > 0 ? value : manager == null ? detail + "\n\nStart captions is unavailable." :
                detail == "Captions paused." || detail == "Choose Start captions."
                    ? "Choose languages, then select Start captions.\n\nMicrophone and internet access required."
                    : detail;
            if (fontChanged || captionTranscript.text != value || captionTranscript.overflowMode != TextOverflowModes.Page)
            {
                int previousPage = captionTranscript.pageToDisplay;
                captionTranscript.overflowMode = TextOverflowModes.Page;
                captionTranscript.text = value;
                captionTranscript.ForceMeshUpdate();
                int count = Mathf.Max(1, captionTranscript.textInfo.pageCount);
                captionTranscript.pageToDisplay = captionFollowLatest ? count : Mathf.Clamp(previousPage, 1, count);
            }
        }
        RefreshCaptionPaging();
        bool listening = bridge != null && bridge.IsListening;
        SetButtonLabel("comm:captions-pause", listening ? "Pause" : "Start captions");
        SetButtonSymbol("comm:captions-pause", listening ? "pause" : "mic");
        SetButtonLabel("comm:captions-language", manager != null && manager.ShowTranslation ? "Show original" : "Translate");
        SetButtonLabel("comm:captions-source", "Speak: " + (manager != null ? LiveTranslationManager.LanguageNames[manager.SourceIndex] : "English"));
        SetButtonLabel("comm:captions-target", "To: " + (manager != null ? LiveTranslationManager.LanguageNames[manager.TargetIndex] : "Spanish"));
        SetControlAvailable("comm:captions-pause", manager != null && bridge.IsAcquired && !bridge.IsBusy);
        SetControlAvailable("comm:captions-source", manager != null && !bridge.IsBusy);
        SetControlAvailable("comm:captions-target", manager != null && !bridge.IsBusy);
        SetControlAvailable("comm:captions-language", manager != null);
    }
}
