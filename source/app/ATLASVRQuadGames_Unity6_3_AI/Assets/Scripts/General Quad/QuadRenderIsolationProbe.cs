using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Injected only into an explicitly requested diagnostic build. Applies bounded,
/// reversible render exclusions without changing the rig, input or project settings.
/// </summary>
public sealed class QuadRenderIsolationProbe : MonoBehaviour
{
    private const string Prefix = "[QuadRenderProbe] ";
    private const float PhaseDuration = 30f;
    private const string OverheadPath = "New Mystery Assets/Part 5 (Library)/Observatory/The Top";
    private sealed class CameraState
    {
        public Camera camera;
        public CameraClearFlags clearFlags;
        public Color background;
        public bool modified;
        public int cullingMask;
        public bool maskModified;
    }
    private sealed class RendererState
    {
        public Renderer renderer;
        public bool enabled;
        public bool modified;
    }

    private readonly List<CameraState> cameras = new List<CameraState>();
    private readonly List<RendererState> trees = new List<RendererState>();
    private readonly List<RendererState> overhead = new List<RendererState>();
    private bool captured;
    private string commandPath = "";
#if !UNITY_EDITOR
    private DateTime commandWriteTime;
    private string commandText;
    private float nextPoll;
    private bool commandErrorLogged;
#endif
    private float restoreAt, phaseStarted;
    private int framesAtStart;

    public int CurrentPhase { get; private set; }
    public int CameraCount => cameras.Count;
    public int TreeRendererCount => trees.Count;
    public int OverheadRendererCount => overhead.Count;
    public string CommandPath => commandPath;

    private void Start()
    {
        CaptureBaseline();
#if !UNITY_EDITOR
        // Reset only this diagnostic's own command file: an old phase must not
        // activate automatically on the next launch.
        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "RenderIsolation");
            Directory.CreateDirectory(directory);
            commandPath = Path.Combine(directory, "phase.txt");
            File.WriteAllText(commandPath, "0");
            commandWriteTime = File.GetLastWriteTimeUtc(commandPath);
            commandText = "0";
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            commandPath = null;
            Debug.LogWarning(Prefix + "Command file unavailable: " + exception.GetType().Name);
        }
#endif
        LogState("ready", 0f);
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        if (CurrentPhase != 0 && now >= restoreAt)
        {
            float elapsed = now - phaseStarted;
            float fps = elapsed > 0f ? (Time.frameCount - framesAtStart) / elapsed : 0f;
            RestoreBaseline();
            LogState("automatic restore", fps);
        }
#if !UNITY_EDITOR
        if (now < nextPoll || string.IsNullOrEmpty(commandPath)) return;
        nextPoll = now + 1f;
        try
        {
            var file = new FileInfo(commandPath);
            if (!file.Exists || file.Length > 32) return;
            DateTime writeTime = file.LastWriteTimeUtc;
            string text = File.ReadAllText(commandPath).Trim();
            if (writeTime == commandWriteTime && text == commandText) return;
            commandWriteTime = writeTime;
            commandText = text;
            commandErrorLogged = false;
            if (int.TryParse(text, out int phase) && phase >= 0 && phase <= 5)
                ApplyPhase(phase);
            else
                Debug.LogWarning(Prefix + "Ignored invalid phase command.");
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            if (!commandErrorLogged)
                Debug.LogWarning(Prefix + "Command read failed: " + exception.GetType().Name);
            commandErrorLogged = true;
        }
#endif
    }

    public void CaptureBaseline()
    {
        if (captured) return;
        captured = true;
        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.gameObject.scene != gameObject.scene || camera.targetTexture != null ||
                camera.stereoTargetEye == StereoTargetEyeMask.None) continue;
            cameras.Add(new CameraState { camera = camera, clearFlags = camera.clearFlags, background = camera.backgroundColor });
        }
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer.gameObject.scene != gameObject.scene) continue;
            bool tree = false;
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.shader != null && material.shader.name == "SyntyStudios/Trees")
                { tree = true; break; }
            if (tree) trees.Add(new RendererState { renderer = renderer, enabled = renderer.enabled });

            // Exact known scene candidates only. Other objects named Cube and
            // gameplay surfaces must not be hidden by this diagnostic.
            bool tallRootCube = renderer.transform.parent == null && renderer.name == "Cube" && renderer.bounds.size.y > 4000f;
            if (tallRootCube || HierarchyPath(renderer.transform) == OverheadPath)
                overhead.Add(new RendererState { renderer = renderer, enabled = renderer.enabled });
        }
    }

    /// <summary>0 baseline; 1 sky; 2 Synty trees; 3 two large overhead candidates; 4 all three exclusions; 5 solid blue with scene culling disabled.</summary>
    public void ApplyPhase(int phase)
    {
        if (phase < 0 || phase > 5) return;
        CaptureBaseline();
        float elapsed = Time.realtimeSinceStartup - phaseStarted;
        float previousFps = CurrentPhase != 0 && elapsed > 0f ? (Time.frameCount - framesAtStart) / elapsed : 0f;
        RestoreBaseline();
        if (phase == 1 || phase == 4 || phase == 5)
            foreach (CameraState state in cameras)
                if (state.camera != null && state.camera.enabled && state.camera.gameObject.activeInHierarchy &&
                    state.camera.targetTexture == null && state.camera.stereoTargetEye != StereoTargetEyeMask.None)
                {
                    state.clearFlags = state.camera.clearFlags;
                    state.background = state.camera.backgroundColor;
                    state.modified = true;
                    state.camera.clearFlags = CameraClearFlags.SolidColor;
                    state.camera.backgroundColor = new Color(.08f, .32f, .65f, 1f);
                    if (phase == 5)
                    {
                        state.cullingMask = state.camera.cullingMask;
                        state.maskModified = true;
                        state.camera.cullingMask = 0;
                    }
                }
        if (phase == 2 || phase == 4) SetHidden(trees);
        if (phase == 3 || phase == 4) SetHidden(overhead);
        CurrentPhase = phase;
        phaseStarted = Time.realtimeSinceStartup;
        framesAtStart = Time.frameCount;
        restoreAt = phaseStarted + PhaseDuration;
        LogState("phase changed", previousFps);
    }

    public void RestoreBaseline()
    {
        foreach (CameraState state in cameras)
            if (state.camera != null && state.modified)
            {
                state.camera.clearFlags = state.clearFlags;
                state.camera.backgroundColor = state.background;
                state.modified = false;
                if (state.maskModified)
                {
                    state.camera.cullingMask = state.cullingMask;
                    state.maskModified = false;
                }
            }
        RestoreRenderers(trees);
        RestoreRenderers(overhead);
        CurrentPhase = 0;
        restoreAt = 0f;
    }

    private static void SetHidden(List<RendererState> states)
    {
        foreach (RendererState state in states)
            if (state.renderer != null)
            {
                state.enabled = state.renderer.enabled;
                state.modified = true;
                state.renderer.enabled = false;
            }
    }
    private static void RestoreRenderers(List<RendererState> states)
    {
        foreach (RendererState state in states)
            if (state.renderer != null && state.modified)
            {
                state.renderer.enabled = state.enabled;
                state.modified = false;
            }
    }
    private void OnDisable() => RestoreBaseline();
    private void OnDestroy() => RestoreBaseline();
    private void OnApplicationPause(bool paused)
    {
        if (paused && CurrentPhase != 0)
        {
            RestoreBaseline();
            LogState("paused; baseline restored", 0f);
        }
    }

    private void LogState(string reason, float previousFps)
    {
        Camera eye = null;
        foreach (CameraState state in cameras)
            if (state.camera != null && state.camera.enabled && state.camera.gameObject.activeInHierarchy)
            { eye = state.camera; break; }
        string pose = eye != null ? eye.transform.localEulerAngles.ToString("F1") : "unavailable";
        Debug.Log(Prefix + reason + "; phase=" + CurrentPhase + "; expiresSeconds=" + (CurrentPhase == 0 ? 0 : PhaseDuration) +
            "; api=" + SystemInfo.graphicsDeviceType + "; msaa=" + QualitySettings.antiAliasing +
            "; stereo=" + (eye != null && eye.stereoEnabled) + "; eyeLocalEuler=" + pose +
            "; eyeWorldPosition=" + (eye != null ? eye.transform.position.ToString("F2") : "unavailable") +
            "; eyeWorldEuler=" + (eye != null ? eye.transform.eulerAngles.ToString("F1") : "unavailable") +
            "; cullingMask=" + (eye != null ? eye.cullingMask.ToString() : "unavailable") +
            "; focused=" + Application.isFocused +
            "; near=" + (eye != null ? eye.nearClipPlane : 0f) + "; far=" + (eye != null ? eye.farClipPlane : 0f) +
            "; verticalFov=" + (eye != null ? eye.fieldOfView : 0f) +
            "; cameras=" + cameras.Count + "; trees=" + trees.Count + "; overhead=" + overhead.Count +
            "; previousPhaseAverageFps=" + previousFps.ToString("F1"));
    }
    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }
    private static bool IsStorageFailure(Exception exception) => exception is IOException ||
        exception is UnauthorizedAccessException || exception is System.Security.SecurityException;
}
