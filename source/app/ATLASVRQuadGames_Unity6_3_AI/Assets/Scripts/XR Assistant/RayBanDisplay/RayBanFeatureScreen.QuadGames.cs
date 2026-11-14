using TMPro;
using UnityEngine;

public partial class RayBanFeatureScreen
{
    [SerializeField] private TMP_Text quadGuidePosition, quadGuideTitle, quadGuideBody, quadGuideLocation;
    [SerializeField] private RayBanGlyph quadGuideGlyph;
    private int quadGameIndex, quadGameStep;
    private bool quadGuideMapReturn;
    public int QuadGameCount => QuadGameGuideCatalog.Guides.Count;
    public int QuadGameIndex => quadGameIndex;
    public int QuadGameStepIndex => quadGameStep;
    public int QuadGameStepCount => CurrentQuadGame.Steps.Count;
    public string QuadGameTitle => CurrentQuadGame.Title;
    public string QuadGameLandmark => CurrentQuadGame.Landmark;
    private QuadGameGuide CurrentQuadGame => QuadGameGuideCatalog.Guides[Mathf.Clamp(quadGameIndex, 0, QuadGameCount - 1)];
    private static bool IsQuadGameGuideView(string key) => key == "tutorials.games" || key == "tutorials.game";

    // Add only guide pages and a link on the existing tutorial; all backend and input references stay intact.
    public void EnsureQuadGameGuide()
    {
        var tutorial = views.Find(v => v.key == "tutorials");
        if (tutorial == null || tutorial.root == null) return;
        if (!controls.Exists(t => t != null && t.ActionId == "tutorials.games"))
        {
            tutorial.heading.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 290);
            Button(tutorial.root, "Quad games guide", "tutorials.games", "Quad games", "map", 400, 16, 180, 56, 0, 2);
        }
        var entry = controls.Find(t => t != null && t.ActionId == "tutorials.games");
        if (entry != null && entry.LabelRect != null)
        {
            var label = entry.LabelRect.GetComponent<TMP_Text>();
            label.fontSize = 18;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        if (!views.Exists(v => v.key == "tutorials.games"))
        {
            var page = Page("tutorials.games", "Quad games");
            Text(page, "Game guide introduction", "Learn how to play, then find the starting point.", 28, 92, 544, 60, 23);
            for (int i = 0; i < QuadGameCount; i++)
            {
                var game = QuadGameGuideCatalog.Guides[i];
                Button(page, game.Title + " guide", "games.open." + i, game.Title, game.Symbol,
                    20 + (i % 2) * 286, 166 + (i / 2) * 114, 274, 100, 1 + i / 2, i % 2);
            }
            Text(page, "Game guide scope", "Use your usual game controls. Display gestures and Back still work here.", 28, 520, 544, 66, 20);
            page.gameObject.SetActive(false);
        }
        if (!views.Exists(v => v.key == "tutorials.game"))
        {
            var page = Page("tutorials.game", "Game guide");
            var card = Panel(page, "Game instructions", 20, 94, 560, 320);
            quadGuideGlyph = Glyph(card.transform, "Game symbol", "tutorials", 24, 20, 46, 46, new Color(1, .76f, .44f));
            quadGuidePosition = Text(card.transform, "Game step count", "", 88, 26, 444, 34, 21, true);
            quadGuideTitle = Text(card.transform, "Game step title", "", 24, 82, 512, 44, 27, true);
            quadGuideBody = Text(card.transform, "Game instructions text", "", 24, 136, 512, 166, 22);
            quadGuideLocation = Text(page, "Game starting location", "", 28, 426, 544, 44, 19);
            foreach (var text in new[] { quadGuidePosition, quadGuideTitle, quadGuideBody, quadGuideLocation }) text.richText = false;
            Button(page, "Show game on map", "games.map", "Show on map", "map", 20, 478, 560, 46, 1, 0);
            Button(page, "Previous game instruction", "games.previous", "Previous", "previous", 20, 536, 274, 50, 2, 0);
            Button(page, "Next game instruction", "games.next", "Next", "next", 306, 536, 274, 50, 2, 1);
            page.gameObject.SetActive(false);
        }
        RefreshTutorial();
    }

    private bool HandleQuadGameGuide(string action)
    {
        if (action == "tutorials.games")
        {
            if (tutorialOnboarding) return true;
            ShowView("tutorials.games");
            FocusAction("games.open." + quadGameIndex);
            return true;
        }
        if (string.IsNullOrEmpty(action) || !action.StartsWith("games.", System.StringComparison.Ordinal)) return false;
        if (action.StartsWith("games.open.", System.StringComparison.Ordinal))
        {
            if (int.TryParse(action.Substring(11), out int index) && index >= 0 && index < QuadGameCount)
            {
                if (quadGameIndex != index) quadGameStep = 0;
                quadGameIndex = index;
                ShowView("tutorials.game");
                FocusAction("games.next");
            }
        }
        else if (action == "games.map") OpenQuadGameMap();
        else
        {
            if (action == "games.previous") quadGameStep = Mathf.Max(0, quadGameStep - 1);
            if (action == "games.next")
            {
                if (quadGameStep == QuadGameStepCount - 1)
                { ShowView("tutorials.games"); FocusAction("games.open." + quadGameIndex); return true; }
                quadGameStep++;
            }
            RefreshQuadGameGuide();
            // The first step disables Previous; never strand gesture focus on it.
            if (quadGameStep == 0 && action == "games.previous") FocusAction("games.next");
        }
        return true;
    }

    private void RefreshQuadGameGuide()
    {
        var game = CurrentQuadGame;
        quadGameStep = Mathf.Clamp(quadGameStep, 0, game.Steps.Count - 1);
        var step = game.Steps[quadGameStep];
        SetHeader("tutorials.game", game.Title);
        if (quadGuidePosition) quadGuidePosition.text = "Step " + (quadGameStep + 1) + " of " + game.Steps.Count;
        if (quadGuideTitle) quadGuideTitle.text = step.Title;
        if (quadGuideBody) quadGuideBody.text = step.Body;
        if (quadGuideGlyph) quadGuideGlyph.Symbol = game.Symbol;
        if (quadGuideLocation) quadGuideLocation.text = "Start: " + game.Landmark;
        SetButtonLabel("games.next", quadGameStep == game.Steps.Count - 1 ? "All games" : "Next");
        SetControlAvailable("games.previous", quadGameStep > 0);
    }

    private void OpenQuadGameMap()
    {
        if (shell == null || !shell.UsesLiveFeatures || shell.mapBridge == null)
        { if (quadGuideLocation) quadGuideLocation.text = "Maps is unavailable. Start: " + QuadGameLandmark; return; }
        ShowView("maps");
        if (!shell.mapBridge.SelectLandmark(QuadGameLandmark))
        {
            ShowView("tutorials.game");
            if (quadGuideLocation) quadGuideLocation.text = "Starting point unavailable: " + QuadGameLandmark;
            FocusAction("games.map");
            return;
        }
        quadGuideMapReturn = true;
        TickLiveNavigation();
        FocusAction("maps.start");
    }

    private bool HandleQuadGameGuideBack()
    {
        if (currentView == "maps" && quadGuideMapReturn)
        {
            quadGuideMapReturn = false;
            ShowView("tutorials.game");
            FocusAction("games.map");
            return true;
        }
        if (currentView == "tutorials.game")
        { ShowView("tutorials.games"); FocusAction("games.open." + quadGameIndex); return true; }
        if (currentView == "tutorials.games")
        { ShowView("tutorials"); FocusAction("tutorials.games"); return true; }
        return false;
    }
}
