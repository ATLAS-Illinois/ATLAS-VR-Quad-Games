using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>App-owned settings. Never changes the Quest system volume, display, or passthrough.</summary>
[DisallowMultipleComponent]
public sealed class RayBanAppSettings : MonoBehaviour
{
    public const string VolumeKey = "ATLASVRQuadGames.Display.AppVolume.v1";
    public const string BrightnessKey = "ATLASVRQuadGames.Display.UiBrightness.v1";
    public const string ReducedMotionKey = "ATLASVRQuadGames.Display.ReducedMotion.v1";
    public const string StrongContrastKey = "ATLASVRQuadGames.Display.StrongContrast.v1";
    public const float MinimumBrightness = .25f;
    public float Volume { get; private set; } = 1f;
    public float Brightness { get; private set; } = 1f;
    public bool ReducedMotion { get; private set; }
    public bool StrongContrast { get; private set; }
    public bool IsActive { get; private set; }
    public event Action Changed;
    private readonly List<Graphic> graphics = new List<Graphic>();
    private readonly HashSet<Graphic> knownGraphics = new HashSet<Graphic>();
    private readonly List<Graphic> discoveredGraphics = new List<Graphic>();
    private Transform boundRoot;
    private float previousVolume;

    public void Activate(Transform visualRoot)
    {
        if (!Application.isPlaying) return;
        if (!IsActive)
        {
            previousVolume = AudioListener.volume;
            Volume = SafeLevel(PlayerPrefs.GetFloat(VolumeKey, 1f), 0f);
            Brightness = SafeLevel(PlayerPrefs.GetFloat(BrightnessKey, 1f), MinimumBrightness);
            ReducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;
            StrongContrast = PlayerPrefs.GetInt(StrongContrastKey, 0) == 1;
            IsActive = true;
            Canvas.willRenderCanvases += ApplyBrightness;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }
        BindVisuals(visualRoot);
        AudioListener.volume = Volume;
    }

    public void BindVisuals(Transform visualRoot)
    {
        ResetTint();
        graphics.Clear(); knownGraphics.Clear();
        boundRoot = visualRoot;
        TrackVisuals(visualRoot);
        if (boundRoot != null)
            foreach (var tile in boundRoot.GetComponentsInChildren<RayBanTile>(true)) tile.RefreshAccessibility();
    }

    // Fallback-font submeshes can be created after the authored tree is bound.
    private void OnTextChanged(UnityEngine.Object changed)
    {
        var text = changed as TMP_Text;
        if (IsActive && boundRoot != null && text != null && text.transform.IsChildOf(boundRoot)) TrackVisuals(text.transform);
    }

    private void TrackVisuals(Transform root)
    {
        discoveredGraphics.Clear();
        if (root != null) root.GetComponentsInChildren(true, discoveredGraphics);
        foreach (var graphic in discoveredGraphics)
            if (graphic != null && knownGraphics.Add(graphic))
            {
                graphics.Add(graphic);
                if (graphic is RayBanSurface surface) surface.StrongContrast = StrongContrast;
            }
        ApplyBrightness();
    }

    public void SetVolume(float value)
    {
        if (!IsActive) return;
        Volume = SafeLevel(value, 0f);
        AudioListener.volume = Volume;
        PlayerPrefs.SetFloat(VolumeKey, Volume);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public void SetBrightness(float value)
    {
        if (!IsActive) return;
        Brightness = SafeLevel(value, MinimumBrightness);
        ApplyBrightness();
        PlayerPrefs.SetFloat(BrightnessKey, Brightness);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public void SetReducedMotion(bool value)
    {
        if (!IsActive || ReducedMotion == value) return;
        ReducedMotion = value;
        PlayerPrefs.SetInt(ReducedMotionKey, value ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAccessibility();
        Changed?.Invoke();
    }

    public void SetStrongContrast(bool value)
    {
        if (!IsActive || StrongContrast == value) return;
        StrongContrast = value;
        PlayerPrefs.SetInt(StrongContrastKey, value ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAccessibility();
        Changed?.Invoke();
    }

    /// <summary>Reset only this display's four preferences; never clear other app data.</summary>
    public void ResetDisplayPreferences()
    {
        if (!IsActive) return;
        Volume = Brightness = 1f;
        ReducedMotion = StrongContrast = false;
        PlayerPrefs.DeleteKey(VolumeKey);
        PlayerPrefs.DeleteKey(BrightnessKey);
        PlayerPrefs.DeleteKey(ReducedMotionKey);
        PlayerPrefs.DeleteKey(StrongContrastKey);
        PlayerPrefs.Save();
        AudioListener.volume = Volume;
        ApplyAccessibility();
        ApplyBrightness();
        Changed?.Invoke();
    }

    private void ApplyAccessibility()
    {
        TrackVisuals(boundRoot);
        foreach (var graphic in graphics)
            if (graphic is RayBanSurface surface) surface.StrongContrast = StrongContrast;
        if (boundRoot != null)
            foreach (var tile in boundRoot.GetComponentsInChildren<RayBanTile>(true)) tile.RefreshAccessibility();
    }

    private static float SafeLevel(float value, float minimum) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, minimum, 1f);

    // CanvasRenderer tint multiplies the authored mesh RGB without reducing alpha
    // or modifying shared materials. It also covers TMP and focused glass meshes.
    private void ApplyBrightness()
    {
        if (!IsActive) return;
        var tint = new Color(Brightness, Brightness, Brightness, 1f);
        foreach (var graphic in graphics)
            if (graphic) graphic.canvasRenderer.SetColor(tint);
    }

    private void ResetTint()
    {
        foreach (var graphic in graphics)
            if (graphic)
            {
                graphic.canvasRenderer.SetColor(Color.white);
                if (graphic is RayBanSurface surface) surface.StrongContrast = false;
            }
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        Canvas.willRenderCanvases -= ApplyBrightness;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        ResetTint();
        graphics.Clear(); knownGraphics.Clear(); discoveredGraphics.Clear(); boundRoot = null;
        AudioListener.volume = previousVolume;
        IsActive = false;
    }

    private void OnDisable() => Deactivate();
    private void OnDestroy() => Deactivate();

    public static string BatteryLabel
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return FormatBattery(SystemInfo.batteryLevel, SystemInfo.batteryStatus);
#else
            // SystemInfo in Link/Editor reports the host PC, not the Quest.
            return "Quest battery · on headset";
#endif
        }
    }

    public static string FormatBattery(float level, BatteryStatus status)
    {
        if (level < 0f || level > 1f || float.IsNaN(level)) return "Quest battery · unavailable";
        string suffix = status == BatteryStatus.Charging ? " · charging" : status == BatteryStatus.Full ? " · full" : "";
        return "Quest · " + Mathf.RoundToInt(level * 100f) + "%" + suffix;
    }
}
