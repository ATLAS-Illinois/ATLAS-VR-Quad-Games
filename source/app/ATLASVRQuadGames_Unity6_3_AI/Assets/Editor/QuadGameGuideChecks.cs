#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Exercises the authored guide and existing navigation without starting mini-games or voice services.</summary>
public static class QuadGameGuideChecks
{
    [Serializable] private class Check { public string name; public bool passed; public string detail; }
    [Serializable] private class Report
    {
        public int passed, failed;
        public List<Check> checks = new List<Check>();
        public List<string> unavailableRoutes = new List<string>();
        public string scope = "Editor UI and route selection only. No player movement, mini-game state, microphone, camera capture, or cloud request. Physical controller/hand use and standalone Quest require separate testing.";
    }
    private const string Output = "Diagnostics/2026-11-08-QuadGameGuide/guide-checks.json";

    public static string Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in idle Play Mode.");
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(s => s.gameObject.scene.path == "Assets/Scenes/QuadProjectAlexanderLau.unity" && s.OwnsPreview);
        var screen = shell.FeatureScreens;
        var map = shell.mapBridge;
        if (shell.assistantBridge.IsBusy || shell.assistantBridge.IsListening || shell.captionBridge.IsBusy || shell.captionBridge.IsListening || map.IsNavigating)
            throw new InvalidOperationException("Close the user's voice or navigation session before these checks.");
        if (screen.IsFirstRunTutorial)
            throw new InvalidOperationException("Leave the first-run tour before these checks; its progress must be preserved.");

        var report = new Report();
        bool hadRead = PlayerPrefs.HasKey(RayBanFeatureScreen.TutorialReadKey);
        bool hadDismissed = PlayerPrefs.HasKey(RayBanFeatureScreen.TutorialDismissedKey);
        int read = PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialReadKey);
        int dismissed = PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialDismissedKey);
        int oldStep = screen.TutorialStepIndex;
        int oldReadCount = screen.TutorialReadCount;
        string oldLandmark = map.SelectedName;
        bool musicPlaying = shell.musicBridge.IsPlaying;
        int photos = GalleryDataManager.SavedPhotos.Count;
        bool visible = shell.IsVisible;
        int dashboardPage = shell.CurrentPage;
        Action<bool, string, string> check = (ok, name, detail) =>
        {
            report.checks.Add(new Check { name = name, passed = ok, detail = detail });
            if (ok) report.passed++; else report.failed++;
        };
        try
        {
            int count = screen.Controls.Count;
            screen.UpgradeLocalFeatures(); screen.UpgradeLocalFeatures();
            check(screen.Controls.Count == count, "Authored guide upgrade is idempotent", "");
            check(screen.Controls.Count(t => t && t.ActionId == "tutorials.games") == 1,
                "One Quad games entry on existing Tutorials", "");
            check(screen.QuadGameCount == QuadGameGuideCatalog.Guides.Count && screen.QuadGameCount == 6,
                "Six scene-based game guides", "");
            check(QuadGameGuideCatalog.Guides.Select(g => g.Id).Distinct().Count() == screen.QuadGameCount,
                "Guide identifiers are unique", "");
            check(QuadGameGuideCatalog.Guides.All(g => !string.IsNullOrEmpty(g.Title) && !string.IsNullOrEmpty(g.Landmark) &&
                g.Steps.Count >= 3 && g.Steps.All(s => !string.IsNullOrEmpty(s.Title) && !string.IsNullOrEmpty(s.Body))),
                "Every game has a destination and readable instruction sections", "");

            shell.OpenApp("Tutorials");
            check(screen.CurrentView == "tutorials" && screen.TutorialStepCount == 8,
                "Original eight Assistant guides remain available", "");
            check(Reachable(screen), "Quad games header entry is reachable with directional focus", "");
            Fit(screen, check, "Existing tutorial with new entry");
            Press(screen, "tutorials.games");
            check(screen.CurrentView == "tutorials.games", "Tutorials opens the game catalog", "");
            check(Reachable(screen), "Every catalog card is reachable with directional focus", "");
            Fit(screen, check, "Game catalog");

            for (int gameIndex = 0; gameIndex < screen.QuadGameCount; gameIndex++)
            {
                var game = QuadGameGuideCatalog.Guides[gameIndex];
                Press(screen, "games.open." + gameIndex);
                check(screen.CurrentView == "tutorials.game" && screen.QuadGameIndex == gameIndex &&
                    screen.QuadGameTitle == game.Title && screen.QuadGameLandmark == game.Landmark,
                    game.Title + ": opens correct instructions", "");
                while (screen.QuadGameStepIndex > 0) Press(screen, "games.previous");
                check(!Active(screen, "games.previous").IsInteractable(), game.Title + ": Previous stops at first section", "");
                for (int step = 0; step < screen.QuadGameStepCount; step++)
                {
                    check(screen.QuadGameStepIndex == step, game.Title + ": section " + (step + 1) + " selected", "");
                    Fit(screen, check, game.Title + " section " + (step + 1));
                    check(Reachable(screen), game.Title + ": section " + (step + 1) + " controls reachable", "");
                    if (step + 1 < screen.QuadGameStepCount) Press(screen, "games.next");
                }
                int lastStep = screen.QuadGameStepIndex;
                Press(screen, "games.map");
                check(screen.CurrentView == "maps" && map.IsAcquired && map.SelectedName == game.Landmark,
                    game.Title + ": map selects exact registry destination", "");
                check(!map.IsNavigating && !map.HasRoute, game.Title + ": map shortcut waits for Navigate", "");
                check(map.MapData != null && map.MapData.TryGetDestination(game.Landmark, out _),
                    game.Title + ": waypoint map contains destination", "");
                Press(screen, "maps.start");
                check(map.IsNavigating && map.DestinationName == game.Landmark,
                    game.Title + ": explicit Navigate uses chosen game destination", "");
                if (map.HasRoute)
                {
                    check(map.RoutePoints.Count > 0 && Vector3.Distance(map.RoutePoints[map.RoutePoints.Count - 1], map.SelectedPosition) < .05f,
                        game.Title + ": route ends at authored approach", "");
                }
                else report.unavailableRoutes.Add(game.Title + ": current player pose could not connect to the waypoint network; no player movement was performed.");
                string selected = map.SelectedName;
                bool navigating = map.IsNavigating;
                check(!map.SelectLandmark("__missing_quad_guide_landmark__") && map.SelectedName == selected && map.IsNavigating == navigating,
                    game.Title + ": missing destination leaves selection and route intact", "");
                screen.Back();
                check(screen.CurrentView == "tutorials.game" && screen.QuadGameIndex == gameIndex &&
                    screen.QuadGameStepIndex == lastStep && !map.IsAcquired && !map.IsNavigating,
                    game.Title + ": Back returns to instructions and releases navigation", "");
                screen.Back();
                check(screen.CurrentView == "tutorials.games", game.Title + ": Back returns to game catalog", "");
                Press(screen, "games.open." + gameIndex);
                check(screen.QuadGameStepIndex == lastStep, game.Title + ": reopening retains current section", "");
                Press(screen, "games.next");
                check(screen.CurrentView == "tutorials.games", game.Title + ": final All games returns to catalog", "");
                check(Idle(shell) && shell.musicBridge.IsPlaying == musicPlaying && GalleryDataManager.SavedPhotos.Count == photos,
                    game.Title + ": no voice, music or photo side effects", "");
            }
            screen.Back();
            check(screen.CurrentView == "tutorials" && screen.TutorialStepIndex == oldStep && screen.TutorialReadCount == oldReadCount,
                "Game catalog Back preserves Assistant tutorial progress", "");
            screen.BeginFirstRunTutorial();
            check(screen.IsFirstRunTutorial && screen.TutorialStepCount == 8 &&
                screen.Controls.Where(t => t && t.ActionId == "tutorials.games").All(t => !t.gameObject.activeInHierarchy),
                "First-run Assistant onboarding stays focused on its existing eight steps", "");
            shell.OpenApp("Tutorials");
            check(!screen.IsFirstRunTutorial && Active(screen, "tutorials.games").IsInteractable(),
                "Quad games entry returns outside onboarding", "");
            check(PlayerPrefs.HasKey(RayBanFeatureScreen.TutorialReadKey) == hadRead &&
                PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialReadKey) == read &&
                PlayerPrefs.HasKey(RayBanFeatureScreen.TutorialDismissedKey) == hadDismissed &&
                PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialDismissedKey) == dismissed,
                "Game guide does not alter onboarding completion or read preferences", "");
        }
        catch (Exception exception)
        {
            check(false, "Unexpected check exception", exception.ToString());
        }
        finally
        {
            shell.OpenApp("Tutorials");
            for (int i = 0; i < screen.TutorialStepCount && screen.TutorialStepIndex != oldStep; i++) Press(screen, "tutorials.next");
            if (!string.IsNullOrEmpty(oldLandmark))
            {
                shell.OpenApp("Maps");
                map.SelectLandmark(oldLandmark);
            }
            shell.SetPage(dashboardPage);
            if (!visible) shell.Hide();
            RestorePreference(RayBanFeatureScreen.TutorialReadKey, hadRead, read);
            RestorePreference(RayBanFeatureScreen.TutorialDismissedKey, hadDismissed, dismissed);
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        }
        return "Quad game guide: " + report.passed + " passed; " + report.failed + " failed; " + report.unavailableRoutes.Count + " routes unavailable from current player pose.";
    }

    private static bool Idle(RayBanDisplayShell shell) => !shell.assistantBridge.IsListening && !shell.assistantBridge.IsBusy &&
        !shell.captionBridge.IsListening && !shell.captionBridge.IsBusy;
    private static RayBanTile Active(RayBanFeatureScreen screen, string action) =>
        screen.Controls.First(t => t && t.ActionId == action && t.gameObject.activeInHierarchy);
    private static void Press(RayBanFeatureScreen screen, string action)
    {
        var tile = Active(screen, action);
        if (!tile.IsInteractable()) throw new InvalidOperationException("Control is unavailable: " + action);
        screen.Activate(tile);
    }
    private static void Fit(RayBanFeatureScreen screen, Action<bool, string, string> check, string name)
    {
        Canvas.ForceUpdateCanvases();
        var text = screen.GetComponentsInChildren<TMP_Text>();
        foreach (var label in text) label.ForceMeshUpdate();
        var overflowing = text.Where(t => t.isTextOverflowing).Select(t => t.name).ToArray();
        check(overflowing.Length == 0, name + ": all text fits", string.Join(", ", overflowing));
    }
    private static bool Reachable(RayBanFeatureScreen screen)
    {
        if (EventSystem.current == null) return false;
        var available = screen.Controls.Where(t => t && t.gameObject.activeInHierarchy && t.IsInteractable()).ToArray();
        var first = available.FirstOrDefault(t => t.ActionId == "back");
        if (first == null) return false;
        var visited = new HashSet<RayBanTile> { first };
        var pending = new Queue<RayBanTile>(); pending.Enqueue(first);
        while (pending.Count > 0)
        {
            var source = pending.Dequeue();
            foreach (MoveDirection direction in new[] { MoveDirection.Up, MoveDirection.Down, MoveDirection.Left, MoveDirection.Right })
            {
                EventSystem.current.SetSelectedGameObject(source.gameObject);
                screen.NavigateFrom(source, direction);
                var selected = EventSystem.current.currentSelectedGameObject;
                var target = selected != null ? selected.GetComponent<RayBanTile>() : null;
                if (target != null && available.Contains(target) && visited.Add(target)) pending.Enqueue(target);
            }
        }
        return available.All(visited.Contains);
    }
    private static void RestorePreference(string key, bool existed, int value)
    {
        if (PlayerPrefs.HasKey(key) == existed && (!existed || PlayerPrefs.GetInt(key) == value)) return;
        if (existed) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
#endif
