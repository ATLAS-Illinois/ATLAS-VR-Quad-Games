using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public partial class RayBanFeatureScreen
{
    // Retain the serialized shape so older prefabs can be upgraded in place.
    // The rejected browser handoff pages are no longer created or reachable.
    [Serializable] private class ExternalAppScreen
    {
        public string key;
        public TMP_Text status;
    }
    [SerializeField] private List<ExternalAppScreen> externalAppScreens = new List<ExternalAppScreen>();
#if UNITY_EDITOR
    // Kept for regression checks that verify no legacy action calls an opener.
    public Action<string> ExternalBrowserOpenForEditorChecks { private get; set; }
#endif

    /// <summary>Retire legacy browser pages and refresh the existing in-app social previews.</summary>
    public void EnsureExternalAppScreens()
    {
        foreach (var view in views)
            if (RayBanExternalAppCatalog.TryGetByKey(view.key, out _) && view.root != null)
                view.root.gameObject.SetActive(false);

        foreach (var control in controls)
            if (control != null && control.ActionId != null &&
                control.ActionId.StartsWith(RayBanExternalAppCatalog.OpenActionPrefix, StringComparison.Ordinal))
                control.interactable = false;

        EnsureInAppSocialScreens();
    }

    private void RefreshExternalApp(string key)
    {
        // Only updates old serialized status text; there is no browser side effect.
        var screen = externalAppScreens.Find(item => item.key == key);
        if (screen != null && screen.status != null)
            screen.status.text = "Use the in-app sample screen. Account not connected.";
    }

    private bool HandleExternalApps(string action)
    {
        // Consume stale UnityEvent/action identifiers without opening any URL.
        return action != null && action.StartsWith(RayBanExternalAppCatalog.OpenActionPrefix, StringComparison.Ordinal);
    }
}
