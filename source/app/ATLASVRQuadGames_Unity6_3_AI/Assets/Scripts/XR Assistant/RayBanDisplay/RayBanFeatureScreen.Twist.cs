using UnityEngine;

public partial class RayBanFeatureScreen
{
    [SerializeField] private PinchAndTwistEventSource mediaTwistSource;
    private PinchAndTwistEventSource subscribedMediaTwist;
    private string mediaTwistView;
    private bool mediaTwistActive, mediaTwistPaused;
    private float lastMediaTwist, mediaTwistCooldown, nextTwistSourceLookup;

    private bool MediaTwistContext => Application.isPlaying && !renderingCopy && !mediaTwistPaused &&
        isActiveAndEnabled && shell != null && shell.UsesLiveFeatures && shell.IsVisible &&
        (CurrentView == "camera" || CurrentView == "music");

    // Suppress incidental optical navigation during adjustment and release.
    // Keyboard Back/Escape remains available for desktop debugging.
    public bool IsAdjustingMediaWithTwist => MediaTwistContext &&
        (mediaTwistActive || Time.unscaledTime < mediaTwistCooldown ||
            (CanAdjustMedia && subscribedMediaTwist.HasTwistIntent));

    private bool CanAdjustMedia => MediaTwistContext && subscribedMediaTwist != null &&
        subscribedMediaTwist.isActiveAndEnabled && (CurrentView == "camera"
            ? shell.cameraBridge != null && shell.cameraBridge.PreviewTexture != null && !shell.cameraBridge.IsCountingDown
            : shell.musicBridge != null && shell.musicBridge.IsAcquired && shell.musicBridge.IsAvailable);

    private void BindMediaTwist()
    {
        if (!MediaTwistContext || subscribedMediaTwist != null) return;
        if (mediaTwistSource == null)
        {
            // Reuse the scene's existing optical source, never create a rig or detector.
            // A serialized assignment can disambiguate projects with several sources.
            if (Time.unscaledTime < nextTwistSourceLookup) return;
            nextTwistSourceLookup = Time.unscaledTime + 1f;
            PinchAndTwistEventSource found = null;
            foreach (var source in FindObjectsByType<PinchAndTwistEventSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.gameObject.scene != gameObject.scene) continue;
                if (found != null) return;
                found = source;
            }
            mediaTwistSource = found;
        }
        if (mediaTwistSource == null) return;
        subscribedMediaTwist = mediaTwistSource;
        subscribedMediaTwist.OnStartPinchAndTwist.AddListener(BeginMediaTwist);
        subscribedMediaTwist.OnPinchAndTwist.AddListener(ApplyMediaTwist);
        subscribedMediaTwist.OnEndPinchAndTwist.AddListener(EndMediaTwist);
    }

    private void UnbindMediaTwist()
    {
        if (subscribedMediaTwist != null)
        {
            subscribedMediaTwist.OnStartPinchAndTwist.RemoveListener(BeginMediaTwist);
            subscribedMediaTwist.OnPinchAndTwist.RemoveListener(ApplyMediaTwist);
            subscribedMediaTwist.OnEndPinchAndTwist.RemoveListener(EndMediaTwist);
        }
        subscribedMediaTwist = null;
        mediaTwistActive = false;
        mediaTwistView = null;
        mediaTwistCooldown = 0f;
    }

    private void TickMediaTwist()
    {
        if (!MediaTwistContext) { UnbindMediaTwist(); return; }
        BindMediaTwist();
        if (mediaTwistActive && !CanAdjustMedia) EndMediaTwist();
    }

    private void BeginMediaTwist()
    {
        mediaTwistActive = CanAdjustMedia;
        mediaTwistView = mediaTwistActive ? CurrentView : null;
        lastMediaTwist = 0f;
    }

    private void ApplyMediaTwist(float value)
    {
        if (!mediaTwistActive) return;
        if (!CanAdjustMedia || mediaTwistView != CurrentView || float.IsNaN(value) || float.IsInfinity(value))
        { EndMediaTwist(); return; }
        value = Mathf.Clamp(value, -1f, 1f);
        float delta = value - lastMediaTwist;
        lastMediaTwist = value;
        if (Mathf.Abs(delta) < .0025f) return;
        if (mediaTwistView == "camera")
        {
            mediaZoom = Mathf.Clamp(mediaZoom + delta * 2f, 1f, 3f);
            RefreshLiveCamera();
        }
        else
        {
            shell.musicBridge.SetVolume(shell.musicBridge.Volume + delta);
            RefreshLiveMusic();
        }
    }

    private void EndMediaTwist()
    {
        if (mediaTwistActive) mediaTwistCooldown = Time.unscaledTime + .15f;
        mediaTwistActive = false;
        mediaTwistView = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) CancelExtendedMedia();
        mediaTwistPaused = paused;
        UnbindMediaTwist();
        if (!paused) BindMediaTwist();
    }
}
