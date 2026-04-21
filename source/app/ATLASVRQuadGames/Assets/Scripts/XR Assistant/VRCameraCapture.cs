using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class GalleryDataManager
{
    public static List<Texture2D> SavedPhotos = new List<Texture2D>();
}

public class VRCameraCapture : MonoBehaviour
{
    [Header("Camera Components")]
    public Camera snapshotCamera;
    public RenderTexture targetRT;

    [Header("UI Elements")]
    public RawImage recentPhotoPreview;
    public Image flashPanel;
    public Button captureButton;

    [Tooltip("The Slider used to zoom the camera")]
    public Slider zoomSlider;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shutterSound;

    private Transform centerEyeAnchor;
    private float baseFOV = 60f; // Default starting FOV

    private void Start()
    {
        if (captureButton != null) captureButton.onClick.AddListener(TakeSnapshot);

        if (snapshotCamera != null)
        {
            baseFOV = snapshotCamera.fieldOfView; // Remember our starting FOV
        }

        if (zoomSlider != null)
        {
            zoomSlider.onValueChanged.AddListener(UpdateZoom);
            zoomSlider.minValue = 1f;
            zoomSlider.maxValue = 3f; // 3x zoom
            zoomSlider.value = 1f;
        }

        if (flashPanel != null) flashPanel.color = new Color(1, 1, 1, 0);

        FindPlayerHead();
    }

    private void OnEnable()
    {
        if (snapshotCamera != null) snapshotCamera.gameObject.SetActive(true);
        if (zoomSlider != null) zoomSlider.value = 1f; // Reset zoom on open
    }

    private void OnDisable()
    {
        if (snapshotCamera != null) snapshotCamera.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (snapshotCamera.gameObject.activeInHierarchy && centerEyeAnchor != null)
        {
            snapshotCamera.transform.position = centerEyeAnchor.position;
            snapshotCamera.transform.rotation = centerEyeAnchor.rotation;
        }
        else if (centerEyeAnchor == null)
        {
            FindPlayerHead();
        }
    }

    private void FindPlayerHead()
    {
        if (centerEyeAnchor == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null) centerEyeAnchor = rig.centerEyeAnchor;
        }
    }

    // THE MAGIC: Changing the actual camera FOV instead of UI scale!
    private void UpdateZoom(float zoomValue)
    {
        if (snapshotCamera != null)
        {
            // If zoomValue goes from 1 to 3, FOV shrinks from 60 to 20 (zooming in)
            snapshotCamera.fieldOfView = baseFOV / zoomValue;
        }
    }

    private void TakeSnapshot()
    {
        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        yield return new WaitForEndOfFrame();

        RenderTexture.active = targetRT;
        Texture2D photo = new Texture2D(targetRT.width, targetRT.height, TextureFormat.RGB24, false);
        photo.ReadPixels(new Rect(0, 0, targetRT.width, targetRT.height), 0, 0);
        photo.Apply();
        RenderTexture.active = null;

        GalleryDataManager.SavedPhotos.Add(photo);

        if (recentPhotoPreview != null) recentPhotoPreview.texture = photo;
        if (audioSource != null && shutterSound != null) audioSource.PlayOneShot(shutterSound);
        if (flashPanel != null) StartCoroutine(FlashEffect());
    }

    private IEnumerator FlashEffect()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        flashPanel.color = new Color(1, 1, 1, 1);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            flashPanel.color = new Color(1, 1, 1, alpha);
            yield return null;
        }

        flashPanel.color = new Color(1, 1, 1, 0);
    }
}