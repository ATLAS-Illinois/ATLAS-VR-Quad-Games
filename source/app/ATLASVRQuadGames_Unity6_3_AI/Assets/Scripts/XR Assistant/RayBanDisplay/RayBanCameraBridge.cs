using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Reuses Quad's gameplay snapshot camera and local album; never opens a physical camera.</summary>
[DisallowMultipleComponent]
public sealed class RayBanCameraBridge : MonoBehaviour
{
    public VRCameraCapture capture;
    public const int SessionPhotoCapacity = GalleryDataManager.Capacity;
    private readonly Dictionary<GameObject, bool> activeStates = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Behaviour, bool> componentStates = new Dictionary<Behaviour, bool>();
    private readonly List<GameObject> ancestorChain = new List<GameObject>();
    private bool acquired, quitting, capturing;
    private Camera leasedCamera;
    private RenderTexture previousTarget;
    private float previousFov;
    private int renderedFrames;
    private float lastFrameAt = -100f;
    private Coroutine captureRoutine;
    private Coroutine countdownRoutine;
    private float countdownEndsAt;
    private string captureStatus;

    public IReadOnlyList<Texture2D> Photos => GalleryDataManager.SavedPhotos;
    public int PhotoCapacity => SessionPhotoCapacity;
    public int CapturedCount => GalleryDataManager.SavedPhotos.Count;
    public string GalleryStatus => GalleryDataManager.LastStatus;
    public bool DeletePhoto(int index, out string message)
    {
        var selected = index >= 0 && index < CapturedCount ? Photos[index] : null;
        bool removed = GalleryDataManager.TryDelete(index, out message);
        if (removed && capture != null && capture.recentPhotoPreview != null && capture.recentPhotoPreview.texture == selected)
            capture.recentPhotoPreview.texture = null;
        return removed;
    }
    public bool IsCapturing => capturing;
    public bool IsCountingDown => countdownRoutine != null;
    public int CountdownSeconds => IsCountingDown ? Mathf.Max(1, Mathf.CeilToInt(countdownEndsAt - Time.realtimeSinceStartup)) : 0;
    private bool HasFreshFrame => acquired && capture != null && leasedCamera != null &&
        leasedCamera.isActiveAndEnabled && capture.targetRT != null && capture.targetRT.IsCreated() &&
        renderedFrames > 0 && Time.realtimeSinceStartup - lastFrameAt < 1.5f;
    public Texture PreviewTexture => HasFreshFrame ? capture.targetRT : null;
    public bool CanCapture => HasFreshFrame && !capturing && !IsCountingDown && CapturedCount < PhotoCapacity;
    public string Status => capture == null || capture.snapshotCamera == null || capture.targetRT == null
        ? "Gameplay camera is not connected"
        : !acquired ? "Camera closed"
        : IsCountingDown ? "Photo in " + CountdownSeconds + "… · select Cancel to stop"
        : capturing ? "Capturing…"
        : CapturedCount >= PhotoCapacity ? "Album full · delete a photo in Photos"
        : !HasFreshFrame ? "Waiting for the gameplay camera…"
        : !string.IsNullOrEmpty(captureStatus) ? captureStatus : "Gameplay camera · select Capture";

    public void SetCameraVisible(bool visible)
    {
        if (!visible) { Release(!quitting); return; }
        if (acquired || quitting || !Application.isPlaying || capture == null ||
            capture.snapshotCamera == null || capture.targetRT == null) return;

        // The legacy capture script lives below an inactive Camera page. Lease only
        // its branch, stopping before a common ancestor of this display shell.
        ancestorChain.Clear();
        for (var node = capture.transform; node != null && !transform.IsChildOf(node); node = node.parent)
        {
            activeStates[node.gameObject] = node.gameObject.activeSelf;
            ancestorChain.Add(node.gameObject);
        }
        leasedCamera = capture.snapshotCamera;
        activeStates[leasedCamera.gameObject] = leasedCamera.gameObject.activeSelf;
        previousTarget = leasedCamera.targetTexture;
        previousFov = leasedCamera.fieldOfView;
        foreach (var component in capture.GetComponentsInChildren<Behaviour>(true))
        {
            componentStates[component] = component.enabled;
            // Old UI, selection/grab and zoom handlers must not run in parallel.
            component.enabled = component == capture || component == leasedCamera || component == capture.audioSource;
        }
        if (!componentStates.ContainsKey(leasedCamera)) componentStates[leasedCamera] = leasedCamera.enabled;
        leasedCamera.targetTexture = capture.targetRT;
        leasedCamera.enabled = true;
        acquired = true;
        renderedFrames = 0;
        lastFrameAt = -100f;
        captureStatus = null;
        Camera.onPostRender += OnCameraRendered;
        for (int i = ancestorChain.Count - 1; i >= 0; i--) ancestorChain[i].SetActive(true);
        leasedCamera.gameObject.SetActive(true);
    }

    private void OnCameraRendered(Camera camera)
    {
        if (!acquired || camera != leasedCamera || capture == null || camera.targetTexture != capture.targetRT) return;
        renderedFrames++;
        lastFrameAt = Time.realtimeSinceStartup;
    }

    public void Capture(Rect uvCrop)
    {
        if (!CanCapture) return;
        if (!Finite(uvCrop.x) || !Finite(uvCrop.y) || !Finite(uvCrop.width) || !Finite(uvCrop.height)) return;
        float left = Mathf.Clamp01(uvCrop.xMin), bottom = Mathf.Clamp01(uvCrop.yMin);
        float right = Mathf.Clamp01(uvCrop.xMax), top = Mathf.Clamp01(uvCrop.yMax);
        if (right <= left || top <= bottom) return;
        capturing = true;
        captureStatus = null;
        captureRoutine = StartCoroutine(CaptureAfterRender(Rect.MinMaxRect(left, bottom, right, top), renderedFrames));
    }

    public bool StartCountdown(Rect uvCrop)
    {
        if (!CanCapture || !Finite(uvCrop.x) || !Finite(uvCrop.y) || !Finite(uvCrop.width) || !Finite(uvCrop.height) ||
            uvCrop.width <= 0f || uvCrop.height <= 0f) return false;
        float left = Mathf.Clamp01(uvCrop.xMin), bottom = Mathf.Clamp01(uvCrop.yMin);
        float right = Mathf.Clamp01(uvCrop.xMax), top = Mathf.Clamp01(uvCrop.yMax);
        if (right <= left || top <= bottom) return false;
        countdownEndsAt = Time.realtimeSinceStartup + 3f;
        captureStatus = null;
        countdownRoutine = StartCoroutine(CaptureAfterCountdown(Rect.MinMaxRect(left, bottom, right, top)));
        return true;
    }

    public void CancelCountdown()
    {
        if (countdownRoutine == null) return;
        StopCoroutine(countdownRoutine);
        countdownRoutine = null;
        countdownEndsAt = 0f;
        captureStatus = "Timer cancelled";
    }

    private IEnumerator CaptureAfterCountdown(Rect crop)
    {
        while (acquired && Time.realtimeSinceStartup < countdownEndsAt) yield return null;
        countdownRoutine = null;
        countdownEndsAt = 0f;
        if (CanCapture) Capture(crop);
        else captureStatus = "Camera frame unavailable · try again";
    }

    private IEnumerator CaptureAfterRender(Rect crop, int requestedFrame)
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        while (acquired && renderedFrames <= requestedFrame && Time.realtimeSinceStartup < deadline)
            yield return new WaitForEndOfFrame();
        if (!HasFreshFrame || renderedFrames <= requestedFrame)
        {
            capturing = false;
            captureRoutine = null;
            captureStatus = "Camera frame unavailable · try again";
            yield break;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture cropped = null;
        Texture2D photo = null;
        try
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(capture.targetRT.width * crop.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(capture.targetRT.height * crop.height));
            float scale = Mathf.Min(1f, (float)GalleryDataManager.MaxPhotoEdge / Mathf.Max(width, height));
            width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            cropped = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(capture.targetRT, cropped, crop.size, crop.position);
            RenderTexture.active = cropped;
            photo = new Texture2D(width, height, TextureFormat.RGB24, false);
            photo.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            photo.Apply(false, false);
            if (GalleryDataManager.TrySave(photo, out string saveMessage))
            {
                if (capture.recentPhotoPreview != null) capture.recentPhotoPreview.texture = photo;
                photo = null; // The local gallery owns the captured texture after a successful save.
                if (capture.audioSource != null && capture.shutterSound != null)
                    capture.audioSource.PlayOneShot(capture.shutterSound);
                captureStatus = saveMessage;
                RayBanLocalNotifications.Publish(QuadNotificationKind.PhotoSaved, "Photo saved", "Your capture is available in Photos.");
            }
            else captureStatus = saveMessage;
        }
        catch (System.Exception exception)
        {
            captureStatus = "Capture unavailable · try again";
            Debug.LogWarning("[RayBanCameraBridge] Gameplay capture failed (" + exception.GetType().Name + ").");
        }
        finally
        {
            RenderTexture.active = previous;
            if (cropped != null) RenderTexture.ReleaseTemporary(cropped);
            if (photo != null) Destroy(photo);
            capturing = false;
            captureRoutine = null;
        }
    }

    private void Release(bool restore)
    {
        CancelCountdown();
        Camera.onPostRender -= OnCameraRendered;
        if (captureRoutine != null) StopCoroutine(captureRoutine);
        captureRoutine = null;
        capturing = false;
        if (!acquired) return;
        acquired = false;
        renderedFrames = 0;
        if (capture != null) capture.gameObject.SetActive(false);
        foreach (var state in componentStates) if (state.Key != null) state.Key.enabled = state.Value;
        componentStates.Clear();
        if (restore)
            foreach (var state in activeStates) if (state.Key != null) state.Key.SetActive(state.Value);
        // Restored OnEnable callbacks may reset the legacy zoom slider, so restore
        // camera properties after restoring activation as well.
        if (leasedCamera != null)
        {
            leasedCamera.targetTexture = previousTarget;
            leasedCamera.fieldOfView = previousFov;
        }
        activeStates.Clear();
        ancestorChain.Clear();
        leasedCamera = null;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        CancelCountdown();
        if (captureRoutine != null) StopCoroutine(captureRoutine);
        captureRoutine = null;
        capturing = false;
        renderedFrames = 0;
        lastFrameAt = -100f;
    }
    private void OnDisable() => Release(!quitting);
    private void OnApplicationQuit() { quitting = true; Release(false); }
}
