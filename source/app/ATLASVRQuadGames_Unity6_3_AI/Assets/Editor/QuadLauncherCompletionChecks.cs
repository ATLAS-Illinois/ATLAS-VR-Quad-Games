#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Controlled Play Mode checks; restores local preference and dashboard state.</summary>
public static class QuadLauncherCompletionChecks
{
    private const string PreferenceKey = "ATLASVRQuadGames.Display.LauncherOrder.v1";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] private class Check { public string name; public bool passed; public string detail; }
    [Serializable] private class Report { public string scope = "Editor Play Mode only; no device or service calls"; public bool passed; public List<Check> checks = new List<Check>(); }

    public static string Run()
    {
        var report = new Report();
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(s => s.OwnsPreview);
        if (!Application.isPlaying || shell == null || shell.FeatureScreens == null || !string.IsNullOrEmpty(shell.CurrentDetail))
        {
            Add(report, "precondition", false, "Start Play Mode with the live display on a dashboard, not inside an app.");
            return Write(report);
        }

        var feature = shell.FeatureScreens;
        bool hadPreference = PlayerPrefs.HasKey(PreferenceKey);
        string preference = PlayerPrefs.GetString(PreferenceKey, "");
        var originalOrder = Order(shell);
        int originalPage = shell.CurrentPage;
        int originalSelection = Get<int>(shell, "selectedIndex");
        var originalDashboardSelections = (int[])Get<int[]>(shell, "dashboardSelections").Clone();
        bool originalStateInitialized = Get<bool>(shell, "dashboardStateInitialized");
        string originalSelectedApp = Get<string>(shell, "selectedLauncherApp");
        float originalSavedScroll = Get<float>(shell, "savedLauncherScroll");
        var content = Get<RectTransform>(shell, "appsContent");
        Vector2 originalScroll = content.anchoredPosition;
        bool originalVisible = shell.IsVisible;
        var originalEventSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        string originalView = feature.CurrentView;
        var originalFocused = Get<RayBanTile>(feature, "focused");
        var remembered = Get<Dictionary<string, string>>(feature, "rememberedViewFocus");
        var originalRemembered = new Dictionary<string, string>(remembered);
        var pageActivity = new Dictionary<GameObject, bool>();
        foreach (Transform child in feature.transform) pageActivity[child.gameObject] = child.gameObject.activeSelf;

        try
        {
            feature.EnsureLauncherSettings();
            shell.ResetLauncherOrder();
            var defaultOrder = Order(shell);
            shell.MoveLauncherApp(10, 0);
            var movedOrder = Order(shell);
            Add(report, "move keeps all twelve unique apps", movedOrder.Length == 12 && movedOrder.Distinct().Count() == 12 &&
                movedOrder[0] == defaultOrder[10] && movedOrder.OrderBy(n => n).SequenceEqual(defaultOrder.OrderBy(n => n)));
            Add(report, "order is persisted", PlayerPrefs.GetString(PreferenceKey, "") == string.Join("|", movedOrder));
            var tiles = Get<List<RayBanTile>>(shell, "tiles").Where(t => t != null && t.PageId == 2).ToArray();
            Add(report, "tile indices rows and columns follow order", tiles.Length == 12 && tiles.All(t =>
                t.ItemIndex == Array.IndexOf(movedOrder, t.Title) && t.Row == t.ItemIndex / 2 && t.Column == t.ItemIndex % 2));
            Add(report, "out of range move leaves order unchanged", !shell.MoveLauncherApp(-1, 0) && !shell.MoveLauncherApp(0, 12) && Order(shell).SequenceEqual(movedOrder));

            shell.OpenApp("Settings");
            Call(feature, "ShowView", "launcher_settings");
            Activate(feature, "launcher.reset");
            Add(report, "reset requires confirmation with safe focus", feature.CurrentView == "launcher_reset" && FocusedAction(feature) == "launcher.cancel");
            Activate(feature, "launcher.cancel");
            Add(report, "cancel preserves custom order", feature.CurrentView == "launcher_settings" && Order(shell).SequenceEqual(movedOrder));
            Activate(feature, "launcher.reset");
            Call(feature, "FocusAction", "launcher.confirm");
            feature.Back();
            Activate(feature, "launcher.reset");
            Add(report, "reopening confirmation does not restore destructive focus", FocusedAction(feature) == "launcher.cancel");
            Activate(feature, "launcher.confirm");
            Add(report, "confirmed reset restores catalog order", feature.CurrentView == "launcher_settings" && Order(shell).SequenceEqual(defaultOrder));

            shell.ReturnToLauncher();
            shell.SetPage(2);
            var lastTile = tiles.First(t => t.ItemIndex == 11);
            Call(shell, "SelectTile", lastTile);
            float beforeScroll = content.anchoredPosition.y;
            shell.OpenApp("Tutorials");
            feature.Back();
            Add(report, "return restores focused app and scroll", string.IsNullOrEmpty(shell.CurrentDetail) &&
                Get<int>(shell, "selectedIndex") == 11 && Mathf.Abs(content.anchoredPosition.y - beforeScroll) < .1f &&
                (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == lastTile.gameObject));

            shell.SetPage(0);
            var settingsTile = Get<List<RayBanTile>>(shell, "tiles").First(t => t != null && t.PageId == 0 && t.ItemIndex == 7);
            Call(shell, "SelectTile", settingsTile);
            shell.SetPage(2);
            Add(report, "app dashboard keeps previous selection", Get<int>(shell, "selectedIndex") == 11 && Mathf.Abs(content.anchoredPosition.y - beforeScroll) < .1f);
            shell.SetPage(0);
            Add(report, "settings dashboard keeps previous selection", Get<int>(shell, "selectedIndex") == 7);
        }
        catch (Exception exception)
        {
            Add(report, "unexpected exception", false, (exception.InnerException ?? exception).ToString());
        }
        finally
        {
            var order = Get<List<string>>(shell, "launcherOrder");
            order.Clear(); order.AddRange(originalOrder);
            Call(shell, "ApplyLauncherOrder");
            if (hadPreference) PlayerPrefs.SetString(PreferenceKey, preference); else PlayerPrefs.DeleteKey(PreferenceKey);
            PlayerPrefs.Save();
            shell.ReturnToLauncher();
            Set(shell, "currentPage", originalPage);
            Set(shell, "selectedIndex", originalSelection);
            Array.Copy(originalDashboardSelections, Get<int[]>(shell, "dashboardSelections"), originalDashboardSelections.Length);
            Set(shell, "dashboardStateInitialized", originalStateInitialized);
            Set(shell, "selectedLauncherApp", originalSelectedApp);
            Set(shell, "savedLauncherScroll", originalSavedScroll);
            shell.ShowLauncher();
            content.anchoredPosition = originalScroll;
            Call(shell, "UpdateScrollFade");
            foreach (Transform child in feature.transform) child.gameObject.SetActive(pageActivity.TryGetValue(child.gameObject, out bool active) && active);
            Set(feature, "currentView", originalView);
            Set(feature, "focused", originalFocused);
            remembered.Clear(); foreach (var entry in originalRemembered) remembered.Add(entry.Key, entry.Value);
            if (!originalVisible) shell.Hide();
            if (EventSystem.current != null && (originalEventSelection == null || originalEventSelection.activeInHierarchy))
                EventSystem.current.SetSelectedGameObject(originalEventSelection);
            Add(report, "original preference restored", PlayerPrefs.HasKey(PreferenceKey) == hadPreference &&
                (!hadPreference || PlayerPrefs.GetString(PreferenceKey, "") == preference) && Order(shell).SequenceEqual(originalOrder));
        }
        return Write(report);
    }

    private static string[] Order(RayBanDisplayShell shell) => Enumerable.Range(0, shell.LauncherAppCount).Select(shell.GetLauncherApp).ToArray();
    private static string FocusedAction(RayBanFeatureScreen feature) => Get<RayBanTile>(feature, "focused")?.ActionId;
    private static void Activate(RayBanFeatureScreen feature, string action)
    {
        var button = feature.Controls.First(c => c != null && c.ActionId == action && c.gameObject.activeInHierarchy && c.IsInteractable());
        feature.Activate(button);
    }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static void Add(Report report, string name, bool passed, string detail = "") => report.checks.Add(new Check { name = name, passed = passed, detail = detail });
    private static string Write(Report report)
    {
        report.passed = report.checks.Count > 0 && report.checks.All(c => c.passed);
        string json = JsonUtility.ToJson(report, true);
        string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Diagnostics/2026-11-08-DisplayCompletion");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "launcher-checks.json"), json);
        return json;
    }
}
#endif
