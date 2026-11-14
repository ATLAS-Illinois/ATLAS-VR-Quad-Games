using TMPro;
using UnityEngine;

public partial class RayBanFeatureScreen
{
    public const string TutorialReadKey = "ATLASVRQuadGames.Display.FeatureGuideRead.v1";
    public const string TutorialDismissedKey = "ATLASVRQuadGames.Display.WelcomeDismissed.v1";
    private const int CurrentTutorialGuideVersion = 2;
    [SerializeField] private TMP_Text tutorialTitle, tutorialBody, tutorialCount, tutorialProgress;
    [SerializeField] private RayBanGlyph tutorialGlyph;
    [SerializeField] private RectTransform tutorialGuideRoot;
    [SerializeField] private int tutorialStep, tutorialGuideVersion;
    private int tutorialReadMask;
    private bool tutorialProgressLoaded;
    private bool tutorialOnboarding;
    public bool IsFirstRunTutorial => tutorialOnboarding;
    public static bool NeedsFirstRunTutorial => PlayerPrefs.GetInt(TutorialDismissedKey, 0) == 0;

    private sealed class FeatureGuide
    {
        public readonly string Title, Body, Symbol, Shortcut, Destination;
        public FeatureGuide(string title, string body, string symbol, string shortcut, string destination)
        { Title = title; Body = body; Symbol = symbol; Shortcut = shortcut; Destination = destination; }
    }

    // These instructions describe the Quad's existing controls and services.
    // Opening a shortcut does not start listening, translation, capture or music.
    private static readonly FeatureGuide[] FeatureGuides = {
        new FeatureGuide("Explore the display",
            "Point or touch to select a tile. Drag the launcher to see all twelve apps. The three dots switch pages. Menu hides or reopens the display.",
            "tutorials", "Open launcher", "$launcher"),
        new FeatureGuide("Capture the virtual Quad",
            "Aim your view, then select Capture. Pinch and twist your right hand to zoom. The camera records the virtual campus, not your physical surroundings.",
            "camera", "Open Camera", "Camera"),
        new FeatureGuide("Browse your photos",
            "Photos keeps your captures on this device between visits. Previous and Next browse the album. Delete asks you to confirm. Start in Camera if the album is empty.",
            "photos", "Open Photos", "Photos"),
        new FeatureGuide("Follow a campus pathway",
            "Choose a landmark with Previous or Next, then select Navigate. Follow the map and remaining distance. End stops guidance. These routes stay inside the virtual Quad.",
            "map", "Open Maps", "Maps"),
        new FeatureGuide("Listen while exploring",
            "Choose Play to start music. Previous and Next change tracks; pinch and twist adjusts music volume. Assistant pauses music while listening and speaking.",
            "music", "Open Music", "Music"),
        new FeatureGuide("Ask about the Quad",
            "Select Listen, then ask your question. Assistant can use the virtual campus view. Stop cancels a response; Back closes the session and releases the microphone.",
            "assistant", "Open Assistant", "Assistant"),
        new FeatureGuide("Read live captions",
            "Choose the spoken and target languages, then select Start captions. Switch between original and translated text. Pause ends capture. This feature needs an internet connection.",
            "captions", "Open Captions", "Captions"),
        new FeatureGuide("Adjust the app",
            "Volume changes game audio, music and speech. Brightness changes this display only. Your levels are saved on this device; headset system settings stay separate.",
            "settings", "Open Quick Settings", "$settings")
    };

    public int TutorialStepIndex => tutorialStep;
    public int TutorialStepCount => FeatureGuides.Length;
    public int TutorialReadCount
    {
        get
        {
            LoadTutorialProgress();
            int count = 0;
            for (int i = 0; i < FeatureGuides.Length; i++) if ((tutorialReadMask & (1 << i)) != 0) count++;
            return count;
        }
    }

    private bool CanSaveTutorialProgress => !renderingCopy && shell != null && shell.UsesLiveFeatures;

    private void LoadTutorialProgress()
    {
        if (!CanSaveTutorialProgress || tutorialProgressLoaded) return;
        tutorialReadMask = PlayerPrefs.GetInt(TutorialReadKey, 0) & ((1 << FeatureGuides.Length) - 1);
        tutorialProgressLoaded = true;
    }

    private void BuildTutorials() => BuildTutorialContent(Page("tutorials", "Tutorials"));

    private void BuildTutorialContent(Transform page)
    {
        tutorialGuideRoot = Rect("Feature guide", page, 0, 84, 600, 516);
        var card = Panel(tutorialGuideRoot, "Feature guide card", 20, 0, 560, 354);
        tutorialGlyph = Glyph(card.transform, "Tutorial symbol", "tutorials", 28, 24, 60, 60, new Color(1, .76f, .44f));
        tutorialCount = Text(card.transform, "Guide position", "", 110, 28, 420, 34, 22, true);
        tutorialProgress = Text(card.transform, "Read progress", "", 110, 62, 420, 32, 18, false, new Color(.82f, .89f, .94f));
        tutorialTitle = Text(card.transform, "Tutorial title", "", 28, 112, 504, 44, 28, true);
        tutorialBody = Text(card.transform, "Tutorial text", "", 28, 170, 504, 164, 23);
        Button(tutorialGuideRoot, "Open guided feature", "tutorials.open", "Open feature", "next", 20, 366, 560, 58, 1, 0);
        Button(tutorialGuideRoot, "Previous guide", "tutorials.previous", "Previous", "previous", 20, 436, 172, 66, 2, 0);
        Button(tutorialGuideRoot, "Mark guide read", "tutorials.read", "Mark read", "check", 202, 436, 196, 66, 2, 1);
        Button(tutorialGuideRoot, "Next guide", "tutorials.next", "Next", "next", 408, 436, 172, 66, 2, 2);
        Button(tutorialGuideRoot, "Skip first tour", "tutorials.skip", "Skip", "close", 430, -68, 150, 56, 0, 2);
        tutorialGuideVersion = CurrentTutorialGuideVersion;
        RefreshTutorial();
    }

    /// <summary>Upgrade only the authored tutorial branch; retain the existing page and Back control.</summary>
    public bool EnsureTutorialGuide()
    {
        if (tutorialGuideVersion == CurrentTutorialGuideVersion && tutorialGuideRoot != null && tutorialProgress != null)
            return false;
        var view = views.Find(v => v.key == "tutorials");
        if (view == null || view.root == null) return false;
        for (int i = view.root.childCount - 1; i >= 0; i--)
        {
            var child = view.root.GetChild(i);
            if (child.name == "Heading" || child.name == "Demo badge" || child.name == "Back") continue;
            controls.RemoveAll(control => control == null || control.transform.IsChildOf(child));
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        BuildTutorialContent(view.root);
        if (CurrentView == "tutorials" && view.root.gameObject.activeInHierarchy) FocusAction("back");
        return true;
    }

    private bool HandleTutorials(string action)
    {
        if (string.IsNullOrEmpty(action) || !action.StartsWith("tutorials.")) return false;
        LoadTutorialProgress();
        switch (action)
        {
            case "tutorials.skip": if (tutorialOnboarding) DismissFirstRunTutorial(); return true;
            case "tutorials.previous": tutorialStep = tutorialOnboarding ? Mathf.Max(0, tutorialStep - 1) : (tutorialStep + FeatureGuides.Length - 1) % FeatureGuides.Length; break;
            case "tutorials.next":
                if (tutorialOnboarding && tutorialStep == FeatureGuides.Length - 1) { DismissFirstRunTutorial(); return true; }
                tutorialStep = (tutorialStep + 1) % FeatureGuides.Length; break;
            case "tutorials.read":
                tutorialReadMask |= 1 << Mathf.Clamp(tutorialStep, 0, FeatureGuides.Length - 1);
                if (CanSaveTutorialProgress)
                {
                    PlayerPrefs.SetInt(TutorialReadKey, tutorialReadMask);
                    PlayerPrefs.Save();
                }
                break;
            case "tutorials.open":
                var guide = FeatureGuides[Mathf.Clamp(tutorialStep, 0, FeatureGuides.Length - 1)];
                if (guide.Destination == "$launcher") shell.SetPage(2);
                else if (guide.Destination == "$settings") shell.SetPage(0);
                else shell.OpenApp(guide.Destination);
                return true;
        }
        RefreshTutorial();
        return true;
    }

    private void RefreshTutorial()
    {
        LoadTutorialProgress();
        tutorialStep = Mathf.Clamp(tutorialStep, 0, FeatureGuides.Length - 1);
        var guide = FeatureGuides[tutorialStep];
        bool read = (tutorialReadMask & (1 << tutorialStep)) != 0;
        if (tutorialTitle != null) tutorialTitle.text = guide.Title;
        if (tutorialBody != null) tutorialBody.text = guide.Body;
        if (tutorialCount != null) tutorialCount.text = "Guide " + (tutorialStep + 1) + " of " + FeatureGuides.Length;
        if (tutorialProgress != null) tutorialProgress.text = TutorialReadCount + " of " + FeatureGuides.Length + " read · " + (read ? "This guide is read" : "Mark when read");
        if (tutorialGlyph != null) tutorialGlyph.Symbol = guide.Symbol;
        SetButtonLabel("tutorials.open", guide.Shortcut);
        SetButtonSymbol("tutorials.open", guide.Symbol);
        SetButtonLabel("tutorials.read", read ? "Read" : "Mark read");
        SetButtonLabel("tutorials.next", tutorialOnboarding && tutorialStep == FeatureGuides.Length - 1 ? "Finish" : "Next");
        SetControlAvailable("tutorials.previous", !tutorialOnboarding || tutorialStep > 0);
        foreach (var control in controls)
        {
            if (!control) continue;
            if (control.ActionId == "tutorials.skip") control.gameObject.SetActive(tutorialOnboarding);
            if (control.ActionId == "tutorials.games") control.gameObject.SetActive(!tutorialOnboarding);
            if (control.ActionId == "tutorials.open") control.gameObject.SetActive(!tutorialOnboarding);
        }
    }

    public void BeginFirstRunTutorial()
    {
        tutorialOnboarding = true;
        tutorialStep = 0;
        ShowView("tutorials");
        FocusAction("tutorials.next");
    }

    private bool CloseFirstRunTutorial()
    {
        if (currentView != "tutorials" || !tutorialOnboarding) return false;
        DismissFirstRunTutorial();
        return true;
    }

    private void DismissFirstRunTutorial()
    {
        if (CanSaveTutorialProgress)
        {
            PlayerPrefs.SetInt(TutorialDismissedKey, 1);
            PlayerPrefs.Save();
        }
        tutorialOnboarding = false;
        shell.ReturnToLauncher();
    }
}
