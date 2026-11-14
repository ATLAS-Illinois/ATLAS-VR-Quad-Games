using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

/// <summary>A native focused button using the project's existing uGUI navigator.</summary>
public class RayBanTile : Button
{
    public RayBanDisplayShell Shell;
    public RayBanSurface Surface;
    public int PageId;
    public int ItemIndex;
    public int Row;
    public int Column;
    public string Title;
    public string Symbol;
    public string ActionId;
    public Graphic IconBadge;
    public RectTransform IconGlyph;
    public RectTransform LabelRect;
    [SerializeField] private Vector2 labelRest;
    private Coroutine focusRoutine;
    private bool hasVisualFocus;
    private bool ReducedMotion => Shell != null && Shell.appSettings != null &&
        Shell.appSettings.IsActive && Shell.appSettings.ReducedMotion;

    public override void OnMove(AxisEventData eventData)
    {
        if (Shell != null && Shell.OwnsPreview)
        {
            Shell.NavigateFrom(this, eventData.moveDir);
            eventData.Use();
            return;
        }
        base.OnMove(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (!IsActive() || !IsInteractable()) return;
        base.OnSubmit(eventData);
        if (Shell != null) Shell.ActivateTile(this);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        // A hand-ray click is released by the same pinch used for adjustment.
        // Consuming that release prevents accidental Capture/Next/Back actions.
        if (Shell != null && Shell.FeatureScreens != null && Shell.FeatureScreens.IsAdjustingMediaWithTwist) return;
        if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
        base.OnPointerClick(eventData);
        if (Shell != null) Shell.ActivateTile(this);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        SetVisualFocus(true);
        if (Shell != null) Shell.ObserveSelection(this);
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        SetVisualFocus(false);
    }

    public void SetVisualFocus(bool focused)
    {
        hasVisualFocus = focused;
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = null;
        if (Application.isPlaying && isActiveAndEnabled && !ReducedMotion)
            focusRoutine = StartCoroutine(AnimateFocus(focused ? 1f : 0f));
        else ApplyFocus(focused ? 1f : 0f);
    }

    public void RefreshAccessibility()
    {
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = null;
        ApplyFocus(hasVisualFocus ? 1f : 0f);
    }

    public void ConfigureVisuals(Graphic badge, RectTransform glyph, RectTransform label)
    {
        IconBadge = badge;
        IconGlyph = glyph;
        LabelRect = label;
        if (label != null) labelRest = label.anchoredPosition;
        ApplyFocus(0);
    }

    protected override void OnDisable()
    {
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = null;
        hasVisualFocus = false;
        ApplyFocus(0);
        base.OnDisable();
    }

    private IEnumerator AnimateFocus(float target)
    {
        float from = Surface != null ? Surface.Focus : 0;
        float elapsed = 0;
        while (elapsed < .2f)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyFocus(Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, elapsed / .2f)));
            yield return null;
        }
        ApplyFocus(target);
        focusRoutine = null;
    }

    private void ApplyFocus(float value)
    {
        if (Surface != null) Surface.Focus = value;
        if (IconBadge != null) IconBadge.color = new Color(1, 1, 1, 1 - value);
        if (IconGlyph != null) IconGlyph.localScale = Vector3.one * (ReducedMotion ? .8f : Mathf.Lerp(.663f, 1, value));
        if (LabelRect != null) LabelRect.anchoredPosition = labelRest + Vector2.left * (ReducedMotion ? 0 : 4 * value);
        transform.localScale = Vector3.one * (ReducedMotion ? 1 : Mathf.Lerp(.943f, 1, value));
    }
}
