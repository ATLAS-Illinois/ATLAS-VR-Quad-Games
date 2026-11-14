using System.Collections.Generic;
using UnityEngine;

/// <summary>Reuses the original Azure backend without activating its legacy input UI.</summary>
[DisallowMultipleComponent]
public sealed class RayBanCaptionBridge : MonoBehaviour
{
    public LiveTranslationManager manager;
    [Tooltip("The legacy Captions Page menu entry containing CaptionsRoot.")]
    public GameObject legacyPageRoot;
    private readonly Dictionary<GameObject, bool> children = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Behaviour, bool> components = new Dictionary<Behaviour, bool>();
    private GameObject leasedRoot;
    private bool acquired, originalActive, quitting;
    public bool IsAcquired => acquired;
    public bool IsListening => acquired && manager != null && manager.IsListening;
    public bool IsBusy => manager != null && manager.IsBusy;
    public string Status => manager == null ? "Caption service is not connected." : manager.Status;

    public bool SetVisible(bool visible)
    {
        if (!visible) { Release(); return false; }
        if (!Application.isPlaying || quitting || manager == null) return false;
        if (acquired) return true;
        leasedRoot = legacyPageRoot != null ? legacyPageRoot : manager.gameObject;
        // The manager is below an inactive menu-page parent in Quad. Activate
        // only its ancestor chain, never a containing display or player rig.
        if (!manager.transform.IsChildOf(leasedRoot.transform) || transform.IsChildOf(leasedRoot.transform))
        { leasedRoot = null; return false; }
        originalActive = leasedRoot.activeSelf;
        leasedRoot.SetActive(false);
        Transform pathChild = null;
        for (Transform current = manager.transform; current != null; current = current.parent)
        {
            children[current.gameObject] = current.gameObject == leasedRoot ? originalActive : current.gameObject.activeSelf;
            foreach (Transform child in current)
            {
                if (child == pathChild) continue;
                children[child.gameObject] = child.gameObject.activeSelf;
                child.gameObject.SetActive(false);
            }
            foreach (var component in current.GetComponents<Behaviour>())
            {
                components[component] = component.enabled;
                if (component != manager) component.enabled = false;
            }
            if (current.gameObject == leasedRoot) break;
            current.gameObject.SetActive(true);
            pathChild = current;
        }
        manager.enabled = true;
        acquired = true;
        leasedRoot.SetActive(true);
        if (!manager.isActiveAndEnabled)
        { Release(); return false; }
        manager.SetListening(false);
        return true;
    }

    public void Toggle()
    {
        if (SetVisible(true) && !manager.IsBusy) manager.SetListening(!manager.IsListening);
    }
    public void CycleSource()
    {
        if (!acquired || manager == null) return;
        manager.SetLanguages((manager.SourceIndex + 1) % 5, manager.TargetIndex);
    }
    public void CycleTarget()
    {
        if (!acquired || manager == null) return;
        manager.SetLanguages(manager.SourceIndex, (manager.TargetIndex + 1) % 5);
    }
    public void ToggleView() { if (acquired && manager != null) manager.SetTranslationView(!manager.ShowTranslation); }

    public void Release(bool restore = true)
    {
        if (!acquired && components.Count == 0) return;
        if (manager != null) manager.SetListening(false);
        if (leasedRoot != null) leasedRoot.SetActive(false);
        // Restore while the page is inactive to avoid awakening old UI input.
        foreach (var state in children) if (state.Key != null && state.Key != leasedRoot) state.Key.SetActive(state.Value);
        foreach (var state in components) if (state.Key != null) state.Key.enabled = state.Value;
        children.Clear(); components.Clear(); acquired = false;
        if (leasedRoot != null && restore && !quitting) leasedRoot.SetActive(originalActive);
        leasedRoot = null;
    }
    private void OnDisable() => Release(!quitting);
    private void OnApplicationQuit() { quitting = true; Release(false); }
}
