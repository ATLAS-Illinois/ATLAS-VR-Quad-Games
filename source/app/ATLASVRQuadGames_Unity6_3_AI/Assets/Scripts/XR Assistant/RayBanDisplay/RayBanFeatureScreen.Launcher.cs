using TMPro;
using UnityEngine;

public partial class RayBanFeatureScreen
{
    [SerializeField] private TMP_Text launcherSelectedName, launcherSelectedPosition, launcherOrderStatus;
    [SerializeField] private RayBanGlyph launcherSelectedGlyph;
    private int launcherEditPosition;

    public void EnsureLauncherSettings()
    {
        if (views.Exists(v => v.key == "launcher_settings")) return;
        var page = Page("launcher_settings", "App order");
        Text(page, "Launcher help", "Choose an app, then move it in your launcher.", 28, 88, 544, 64, 23);
        var card = Panel(page, "Selected launcher app", 20, 160, 560, 116);
        launcherSelectedGlyph = Glyph(card.transform, "Selected app symbol", "assistant", 16, 22, 72, 72);
        launcherSelectedName = Text(card.transform, "Selected app name", "", 106, 16, 432, 48, 29, true);
        launcherSelectedPosition = Text(card.transform, "App position", "", 106, 68, 432, 30, 21);
        Button(page, "Previous app", "launcher.previous", "Previous", "back", 20, 292, 274, 66, 1, 0);
        Button(page, "Next app", "launcher.next", "Next", "next", 306, 292, 274, 66, 1, 1);
        Button(page, "Move app earlier", "launcher.earlier", "Move earlier", "minus", 20, 370, 274, 66, 2, 0);
        Button(page, "Move app later", "launcher.later", "Move later", "plus", 306, 370, 274, 66, 2, 1);
        Button(page, "Move app to first position", "launcher.first", "Move to first", "check", 20, 448, 274, 66, 3, 0);
        Button(page, "Restore original app order", "launcher.reset", "Reset order", "settings", 306, 448, 274, 66, 3, 1);
        launcherOrderStatus = Text(page, "Launcher saved status", "Saved on this device", 28, 538, 544, 36, 20);
        launcherOrderStatus.alignment = TextAlignmentOptions.Center;
        page.gameObject.SetActive(false);

        var confirm = Page("launcher_reset", "Reset app order?");
        Text(confirm, "Reset launcher explanation", "Restore the original order of all 12 apps?\nYour photos, settings and app data stay as they are.", 36, 180, 528, 200, 28).alignment = TextAlignmentOptions.Center;
        Button(confirm, "Keep current app order", "launcher.cancel", "Keep order", "back", 20, 500, 274, 80, 1, 0);
        Button(confirm, "Confirm original app order", "launcher.confirm", "Reset", "check", 306, 500, 274, 80, 1, 1);
        confirm.gameObject.SetActive(false);
        RefreshLauncherSettings();
    }

    private bool IsLauncherSettingsView(string key) => key == "launcher_settings" || key == "launcher_reset";

    private bool HandleLauncherSettingsBack()
    {
        if (currentView == "launcher_reset") { ShowView("launcher_settings"); return true; }
        if (currentView == "launcher_settings") { ShowView("settings"); return true; }
        return false;
    }

    private bool HandleLauncherSettings(string action)
    {
        if (shell == null || action == null || !action.StartsWith("launcher.", System.StringComparison.Ordinal)) return false;
        switch (action)
        {
            case "launcher.previous": launcherEditPosition = Mathf.Max(0, launcherEditPosition - 1); break;
            case "launcher.next": launcherEditPosition = Mathf.Min(shell.LauncherAppCount - 1, launcherEditPosition + 1); break;
            case "launcher.earlier":
                if (shell.MoveLauncherApp(launcherEditPosition, launcherEditPosition - 1)) launcherEditPosition--;
                break;
            case "launcher.later":
                if (shell.MoveLauncherApp(launcherEditPosition, launcherEditPosition + 1)) launcherEditPosition++;
                break;
            case "launcher.first":
                if (shell.MoveLauncherApp(launcherEditPosition, 0)) launcherEditPosition = 0;
                break;
            case "launcher.reset":
                ShowView("launcher_reset");
                FocusAction("launcher.cancel");
                return true;
            case "launcher.cancel": ShowView("launcher_settings"); return true;
            case "launcher.confirm":
                shell.ResetLauncherOrder();
                launcherEditPosition = 0;
                ShowView("launcher_settings");
                return true;
            default: return false;
        }
        RefreshLauncherSettings();
        // A boundary can disable the button that was just used; keep focus usable.
        if (focused != null && !focused.IsInteractable()) FocusAction(launcherEditPosition == 0 ? "launcher.next" : "launcher.previous");
        return true;
    }

    private void RefreshLauncherSettings()
    {
        if (shell == null || launcherSelectedName == null) return;
        launcherEditPosition = Mathf.Clamp(launcherEditPosition, 0, shell.LauncherAppCount - 1);
        launcherSelectedName.text = shell.GetLauncherApp(launcherEditPosition);
        launcherSelectedPosition.text = "Position " + (launcherEditPosition + 1) + " of " + shell.LauncherAppCount;
        launcherSelectedGlyph.Symbol = shell.GetLauncherAppSymbol(launcherEditPosition);
        launcherOrderStatus.text = shell.HasCustomLauncherOrder ? "Custom order · Saved on this device" : "Original order · Two-column launcher";
        SetLauncherControlEnabled("launcher.previous", launcherEditPosition > 0);
        SetLauncherControlEnabled("launcher.next", launcherEditPosition < shell.LauncherAppCount - 1);
        SetLauncherControlEnabled("launcher.earlier", launcherEditPosition > 0);
        SetLauncherControlEnabled("launcher.later", launcherEditPosition < shell.LauncherAppCount - 1);
        SetLauncherControlEnabled("launcher.first", launcherEditPosition > 0);
        SetLauncherControlEnabled("launcher.reset", shell.HasCustomLauncherOrder);
    }

    private void SetLauncherControlEnabled(string action, bool enabled)
    {
        var button = controls.Find(c => c != null && c.ActionId == action);
        if (button != null) button.interactable = enabled;
    }
}
