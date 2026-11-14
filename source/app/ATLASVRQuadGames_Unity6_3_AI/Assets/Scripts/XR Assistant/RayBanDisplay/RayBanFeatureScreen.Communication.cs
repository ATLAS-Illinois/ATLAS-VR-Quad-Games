using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class RayBanFeatureScreen
{
    [SerializeField] private TMP_Text assistantQuestion;
    [SerializeField] private TMP_Text assistantResponse;
    [SerializeField] private TMP_Text assistantStatus;
    [SerializeField] private RayBanGlyph assistantStatusGlyph;
    [SerializeField] private int assistantQuestionIndex;
    [SerializeField] private bool assistantListening;
    [SerializeField] private TMP_Text captionTranscript;
    [SerializeField] private TMP_Text captionStatus;
    [SerializeField] private TMP_Text captionLanguage;
    [SerializeField] private bool captionsPaused;
    [SerializeField] private bool captionsSpanish;
    [SerializeField] private RectTransform messageThreadView;
    [SerializeField] private TMP_Text messageIncoming;
    [SerializeField] private TMP_Text messageOutgoing;
    [SerializeField] private TMP_Text messageReplyStatus;
    [SerializeField] private TMP_Text[] inboxPreviews = new TMP_Text[3];
    [SerializeField] private bool[] messageReplied = new bool[3];
    [SerializeField] private int messageContact;
    [SerializeField] private string messagingBrand = "Messages";
    [SerializeField] private RectTransform callPreviewView;
    [SerializeField] private TMP_Text callContactLabel;
    [SerializeField] private TMP_Text callPreviewStatus;
    [SerializeField] private bool callPreviewMuted;
    [SerializeField] private int callContact;
    [SerializeField] private RawImage instagramPhoto;
    [SerializeField] private TMP_Text instagramCaption;
    [SerializeField] private TMP_Text instagramPostNumber;
    [SerializeField] private int instagramPost;
    [SerializeField] private bool[] instagramLiked = new bool[3];

    private static readonly string[] CommunicationContacts = { "Avery Chen", "Jordan Lee", "Sam Rivera" };
    private static readonly string[] CommunicationSnippets = { "Meet by the Union?", "The prototype looks great!", "See you at the team meeting." };
    private static readonly string[] CommunicationIncoming = {
        "Want to meet by the Union after class? I will be at the south entrance.",
        "The new display looks great! Can we try the gesture navigation next?",
        "Our team meeting starts at 3. Bring the headset when you come over."
    };
    private static readonly Color CommunicationSecondary = new Color(.78f, .83f, .89f, 1);
    private static readonly Color CommunicationAccent = new Color(.35f, .82f, 1f, 1);
    private const string SocialAccountNote = "Account not connected · Sample only";

    // Applies to already authored prefabs as well as newly built feature trees.
    private void EnsureInAppSocialScreens()
    {
        foreach (var view in views)
        {
            if (view.root == null || !(view.key == "messages" || view.key == "message-thread" ||
                view.key == "calls" || view.key == "call-preview" || view.key == "instagram")) continue;
            var badge = view.root.Find("Demo badge");
            if (badge != null && badge.TryGetComponent<TMP_Text>(out var badgeText)) badgeText.text = "SAMPLE";
            SetSocialText(view.root, "Inbox subtitle", "SAMPLE CONVERSATIONS");
            SetSocialText(view.root, "Messaging sample note", SocialAccountNote);
            SetSocialText(view.root, "Calls heading", "SAMPLE CALLS");
            SetSocialText(view.root, "Call sample note", SocialAccountNote);
            SetSocialText(view.root, "Call preview note", "Microphone and calling are inactive.");
        }
        if (messageIncoming != null && messageOutgoing != null && messageReplyStatus != null) RefreshMessagePreview();
        if (callContactLabel != null && callPreviewStatus != null) RefreshCallPreview();
        if (instagramPhoto != null && instagramCaption != null && instagramPostNumber != null) RefreshInstagramPreview();
    }

    private static void SetSocialText(Transform root, string childName, string value)
    {
        var child = root.Find(childName);
        if (child != null && child.TryGetComponent<TMP_Text>(out var text)) text.text = value;
    }

    private void BuildCommunication()
    {
        if (messageReplied == null || messageReplied.Length != 3) messageReplied = new bool[3];
        if (instagramLiked == null || instagramLiked.Length != 3) instagramLiked = new bool[3];
        inboxPreviews = new TMP_Text[3];

        var assistant = Page("assistant", "Assistant");
        Panel(assistant, "Assistant status", 20, 94, 560, 54, new Color(1, 1, 1, .32f), 27);
        assistantStatusGlyph = Glyph(assistant, "Assistant status glyph", "assistant", 38, 105, 32, 32, CommunicationAccent);
        assistantStatus = Text(assistant, "Assistant status text", "Sample answer", 82, 104, 466, 34, 22, false, CommunicationSecondary);
        Panel(assistant, "Question bubble", 76, 166, 504, 90, new Color(.66f, .83f, 1, .78f), 24);
        assistantQuestion = Text(assistant, "Question", "", 98, 174, 458, 74, 26, true);
        Panel(assistant, "Answer bubble", 20, 274, 560, 208, new Color(1, 1, 1, .82f), 28);
        assistantResponse = Text(assistant, "Sample response", "", 42, 292, 516, 170, 27);
        BuildAssistantIllustration(assistant);
        Button(assistant, "Listen preview", "comm:assistant-listen", "Listen", "mic", 16, 506, 282, 78, 1, 0, false, CommunicationAccent);
        Button(assistant, "Another question", "comm:assistant-question", "Try another", "next", 302, 506, 282, 78, 1, 1);
        RefreshAssistantPreview();

        var captions = Page("captions", "Captions");
        Glyph(captions, "Caption glyph", "captions", 24, 100, 42, 42, new Color(.97f, .83f, .39f));
        captionStatus = Text(captions, "Caption playback state", "Playing sample", 80, 105, 300, 34, 23, true);
        captionLanguage = Text(captions, "Caption language", "ENGLISH", 390, 109, 186, 30, 18, true, CommunicationSecondary);
        Panel(captions, "Transcript panel", 16, 164, 568, 304, new Color(1, 1, 1, .72f), 32);
        Text(captions, "Speaker", "SPEAKER 1", 40, 186, 500, 32, 19, true, CommunicationAccent);
        captionTranscript = Text(captions, "Transcript", "", 40, 236, 520, 208, 32);
        Button(captions, "Pause captions", "comm:captions-pause", "Pause", "pause", 16, 506, 282, 78, 1, 0);
        Button(captions, "Caption language", "comm:captions-language", "Spanish", "captions", 302, 506, 282, 78, 1, 1);
        RefreshCaptionPreview();

        var inbox = Page("messages", messagingBrand);
        Text(inbox, "Inbox subtitle", "SAMPLE CONVERSATIONS", 24, 99, 550, 32, 18, true, CommunicationSecondary);
        for (int i = 0; i < CommunicationContacts.Length; i++)
        {
            float y = 146 + i * 112;
            var item = Button(inbox, "Conversation " + CommunicationContacts[i], "comm:message-" + i,
                "", "person", 16, y, 568, 100, i + 1, 0, false, CommunicationAccent);
            item.Title = CommunicationContacts[i];
            Text(item.transform, "Contact name", CommunicationContacts[i], 92, 10, 390, 42, 26, true);
            inboxPreviews[i] = Text(item.transform, "Message preview", "", 92, 53, 426, 30, 21, false, CommunicationSecondary);
            Text(item.transform, "Time", (i + 1) + "m", 496, 16, 48, 28, 18, false, CommunicationSecondary);
        }
        Text(inbox, "Messaging sample note", SocialAccountNote, 28, 520, 544, 54, 21, false, CommunicationSecondary);

        messageThreadView = Page("message-thread", CommunicationContacts[0]);
        Text(messageThreadView, "Thread timestamp", "TODAY  ·  2:30 PM", 28, 101, 540, 30, 18, false, CommunicationSecondary);
        Panel(messageThreadView, "Incoming bubble", 20, 152, 510, 146, new Color(1, 1, 1, .8f), 24);
        messageIncoming = Text(messageThreadView, "Incoming message", "", 42, 170, 466, 112, 26);
        Panel(messageThreadView, "Outgoing bubble", 100, 322, 480, 100, new Color(.51f, .77f, 1f, .85f), 24);
        messageOutgoing = Text(messageThreadView, "Outgoing message", "", 124, 339, 430, 68, 26);
        messageReplyStatus = Text(messageThreadView, "Reply state", "", 102, 436, 472, 42, 19, false, CommunicationSecondary);
        Button(messageThreadView, "Quick reply", "comm:message-reply", "Preview reply", "send", 16, 506, 568, 78, 1, 0, false, CommunicationAccent);
        RefreshMessagePreview();

        var calls = Page("calls", "Calls");
        Text(calls, "Calls heading", "SAMPLE CALLS", 24, 99, 550, 32, 18, true, CommunicationSecondary);
        string[] callTimes = { "Outgoing  ·  Today, 12:42 PM", "Incoming  ·  Today, 10:16 AM", "Missed  ·  Yesterday" };
        for (int i = 0; i < CommunicationContacts.Length; i++)
        {
            var item = Button(calls, "Recent " + CommunicationContacts[i], "comm:call-" + i,
                "", "phone", 16, 146 + i * 112, 568, 100, i + 1, 0, false, new Color(.42f, .89f, .65f));
            item.Title = CommunicationContacts[i];
            Text(item.transform, "Contact name", CommunicationContacts[i], 92, 10, 446, 42, 26, true);
            Text(item.transform, "Call time", callTimes[i], 92, 54, 446, 30, 20, false, CommunicationSecondary);
        }
        Text(calls, "Call sample note", SocialAccountNote, 28, 520, 544, 54, 21, false, CommunicationSecondary);
        callPreviewView = Page("call-preview", "Call preview");
        Panel(callPreviewView, "Caller avatar", 238, 113, 124, 124, new Color(.64f, .89f, .79f, .86f), 62);
        Glyph(callPreviewView, "Caller glyph", "person", 266, 141, 68, 68);
        callContactLabel = Text(callPreviewView, "Caller name", "", 28, 266, 544, 62, 38, true);
        callContactLabel.alignment = TextAlignmentOptions.Center;
        callPreviewStatus = Text(callPreviewView, "Call state", "Preview only  ·  No connection", 28, 346, 544, 74, 24, false, CommunicationSecondary);
        callPreviewStatus.alignment = TextAlignmentOptions.Center;
        Text(callPreviewView, "Call preview note", "Microphone and calling are inactive.", 28, 436, 544, 38, 20, false, CommunicationSecondary).alignment = TextAlignmentOptions.Center;
        Button(callPreviewView, "Mute preview", "comm:call-mute", "Mute preview", "mic", 16, 506, 282, 78, 1, 0);
        Button(callPreviewView, "End preview", "comm:call-end", "End preview", "close", 302, 506, 282, 78, 1, 1, false, new Color(1f, .35f, .39f));
        RefreshCallPreview();

        var instagram = Page("instagram", "Instagram");
        Panel(instagram, "Post avatar", 22, 92, 46, 46, new Color(.9f, .61f, .86f, .9f), 23);
        Glyph(instagram, "Post avatar glyph", "person", 32, 102, 26, 26);
        Text(instagram, "Post user", "campus.stories", 82, 98, 348, 34, 24, true);
        instagramPostNumber = Text(instagram, "Post number", "", 450, 102, 126, 28, 18, false, CommunicationSecondary);
        Panel(instagram, "Post image frame", 16, 150, 568, 266, new Color(1, 1, 1, .55f), 24);
        instagramPhoto = Picture(instagram, "Sample post photo", 0, 24, 158, 552, 250);
        instagramCaption = Text(instagram, "Post caption", "", 24, 430, 552, 60, 22);
        Button(instagram, "Like sample post", "comm:instagram-like", "Like", "heart", 16, 506, 282, 78, 1, 0, false, new Color(1f, .41f, .65f));
        Button(instagram, "Next sample post", "comm:instagram-next", "Next post", "next", 302, 506, 282, 78, 1, 1);
        RefreshInstagramPreview();
    }

    private bool HandleCommunication(string action)
    {
        if (HandleLiveAssistant(action)) return true;
        if (shell != null && shell.UsesLiveFeatures && HandleLiveCaptions(action)) return true;
        if (string.IsNullOrEmpty(action) || !action.StartsWith("comm:")) return false;
        switch (action)
        {
            case "comm:assistant-listen":
                assistantListening = !assistantListening;
                RefreshAssistantPreview();
                return true;
            case "comm:assistant-question":
                assistantQuestionIndex = (assistantQuestionIndex + 1) % 2;
                assistantListening = false;
                RefreshAssistantPreview();
                return true;
            case "comm:captions-pause":
                captionsPaused = !captionsPaused;
                RefreshCaptionPreview();
                return true;
            case "comm:captions-language":
                captionsSpanish = !captionsSpanish;
                RefreshCaptionPreview();
                return true;
            case "comm:message-reply":
                messageReplied[messageContact] = !messageReplied[messageContact];
                RefreshMessagePreview();
                return true;
            case "comm:call-mute":
                callPreviewMuted = !callPreviewMuted;
                RefreshCallPreview();
                return true;
            case "comm:call-end":
                ShowView("calls");
                FocusAction("comm:call-" + callContact);
                return true;
            case "comm:instagram-like":
                instagramLiked[instagramPost] = !instagramLiked[instagramPost];
                RefreshInstagramPreview();
                return true;
            case "comm:instagram-next":
                instagramPost = (instagramPost + 1) % 3;
                RefreshInstagramPreview();
                return true;
        }
        if (action.StartsWith("comm:message-") && int.TryParse(action.Substring("comm:message-".Length), out int contact) && contact >= 0 && contact < 3)
        {
            messageContact = contact;
            RefreshMessagePreview();
            ShowView("message-thread");
            FocusAction("comm:message-reply");
            return true;
        }
        if (action.StartsWith("comm:call-") && int.TryParse(action.Substring("comm:call-".Length), out int caller) && caller >= 0 && caller < 3)
        {
            callContact = caller;
            callPreviewMuted = false;
            RefreshCallPreview();
            ShowView("call-preview");
            FocusAction("comm:call-mute");
            return true;
        }
        return false;
    }

    public void SetMessagingBrand(string brand)
    {
        messagingBrand = string.IsNullOrEmpty(brand) ? "Messages" : brand;
        SetHeader("messages", messagingBrand);
    }

    public bool CommunicationBack()
    {
        if (messageThreadView != null && messageThreadView.gameObject.activeSelf)
        {
            ShowView("messages");
            FocusAction("comm:message-" + messageContact);
            return true;
        }
        if (callPreviewView != null && callPreviewView.gameObject.activeSelf)
        {
            ShowView("calls");
            FocusAction("comm:call-" + callContact);
            return true;
        }
        return false;
    }

    private void RefreshAssistantPreview()
    {
        if (shell != null && shell.UsesLiveFeatures) { RefreshLiveAssistant(); return; }
        assistantQuestionIndex = Mathf.Clamp(assistantQuestionIndex, 0, 1);
        assistantQuestion.text = assistantQuestionIndex == 0 ? "What do you see in front of me?" : "Where can I meet my team?";
        assistantResponse.text = assistantListening
            ? "Listening preview\nYour question would appear here.\nSelect Show answer to continue."
            : assistantQuestionIndex == 0
                ? "A tree-lined campus path with the Illini Union ahead. This is a sample response for the display preview."
                : "The Illini Union south entrance is an easy meeting spot. Open Maps to preview the campus route screen.";
        assistantStatus.text = assistantListening ? "Listening preview  ·  Mic inactive" : "Sample answer  ·  AI inactive";
        assistantStatusGlyph.Symbol = assistantListening ? "mic" : "assistant";
        SetButtonLabel("comm:assistant-listen", assistantListening ? "Show answer" : "Listen");
        SetButtonSymbol("comm:assistant-listen", assistantListening ? "check" : "mic");
    }

    private void RefreshCaptionPreview()
    {
        if (shell != null && shell.UsesLiveFeatures) { RefreshLiveCaptions(); return; }
        captionTranscript.text = captionsSpanish
            ? "El recorrido empieza en el patio.\n\nSigan el camino hasta la entrada principal."
            : "The tour starts in the courtyard.\n\nFollow the path to the main entrance.";
        captionLanguage.text = captionsSpanish ? "SPANISH" : "ENGLISH";
        captionStatus.text = captionsPaused ? "Sample paused" : "Playing sample";
        SetButtonLabel("comm:captions-pause", captionsPaused ? "Resume" : "Pause");
        SetButtonSymbol("comm:captions-pause", captionsPaused ? "play" : "pause");
        SetButtonLabel("comm:captions-language", captionsSpanish ? "English" : "Spanish");
    }

    private void RefreshMessagePreview()
    {
        messageContact = Mathf.Clamp(messageContact, 0, 2);
        SetHeader("message-thread", CommunicationContacts[messageContact]);
        messageIncoming.text = CommunicationIncoming[messageContact];
        messageOutgoing.text = messageReplied[messageContact] ? "Sounds good. See you there!" : "Thanks for the update!";
        messageReplyStatus.text = messageReplied[messageContact] ? "Preview reply added locally  ·  Not sent" : "Sample conversation  ·  No messages sent";
        SetButtonLabel("comm:message-reply", messageReplied[messageContact] ? "Reset preview" : "Preview reply");
        for (int i = 0; i < inboxPreviews.Length; i++)
            if (inboxPreviews[i] != null) inboxPreviews[i].text = messageReplied[i] ? "You: Sounds good. See you there!" : CommunicationSnippets[i];
    }

    private void RefreshCallPreview()
    {
        callContact = Mathf.Clamp(callContact, 0, 2);
        callContactLabel.text = CommunicationContacts[callContact];
        callPreviewStatus.text = callPreviewMuted ? "Muted preview  ·  No connection" : "Preview only  ·  No connection";
        SetButtonLabel("comm:call-mute", callPreviewMuted ? "Unmute preview" : "Mute preview");
        SetButtonSymbol("comm:call-mute", callPreviewMuted ? "close" : "mic");
    }

    private void RefreshInstagramPreview()
    {
        instagramPost = Mathf.Clamp(instagramPost, 0, 2);
        SetPhoto(instagramPhoto, instagramPost);
        instagramPostNumber.text = (instagramPost + 1) + " / 3";
        string[] captions = { "A little fresh air between classes.", "Finding a new perspective on campus.", "An afternoon worth remembering." };
        instagramCaption.text = captions[instagramPost] + "\nSample post · Account not connected";
        SetButtonLabel("comm:instagram-like", instagramLiked[instagramPost] ? "Sample liked" : "Like sample");
        SetButtonSymbol("comm:instagram-like", instagramLiked[instagramPost] ? "check" : "heart");
    }
}
