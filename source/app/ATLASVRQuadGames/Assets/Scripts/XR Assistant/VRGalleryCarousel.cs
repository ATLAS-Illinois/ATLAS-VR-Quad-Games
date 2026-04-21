using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VRGalleryCarousel : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The main image display")]
    public RawImage photoDisplay;

    [Tooltip("Navigation Buttons")]
    public Button nextButton;
    public Button prevButton;

    [Tooltip("Optional: Shows '1 / 5' etc.")]
    public TextMeshProUGUI counterText;

    private int currentIndex = 0;

    private void Start()
    {
        // Bind the buttons
        if (nextButton != null) nextButton.onClick.AddListener(NextPhoto);
        if (prevButton != null) prevButton.onClick.AddListener(PreviousPhoto);
    }

    // Every time the Gallery Page is opened, refresh the display
    private void OnEnable()
    {
        // Default to the newest photo (the last one in the list)
        if (GalleryDataManager.SavedPhotos.Count > 0)
        {
            currentIndex = GalleryDataManager.SavedPhotos.Count - 1;
            UpdateDisplay();
        }
        else
        {
            // If there are no photos, hide the display or show a default color
            photoDisplay.texture = null;
            photoDisplay.color = Color.black;

            if (counterText != null) counterText.text = "0 / 0";
            if (nextButton != null) nextButton.interactable = false;
            if (prevButton != null) prevButton.interactable = false;
        }
    }

    private void NextPhoto()
    {
        if (GalleryDataManager.SavedPhotos.Count == 0) return;

        currentIndex++;

        // Loop back to the first photo if we go past the end
        if (currentIndex >= GalleryDataManager.SavedPhotos.Count)
        {
            currentIndex = 0;
        }

        UpdateDisplay();
    }

    private void PreviousPhoto()
    {
        if (GalleryDataManager.SavedPhotos.Count == 0) return;

        currentIndex--;

        // Loop back to the last photo if we go past the beginning
        if (currentIndex < 0)
        {
            currentIndex = GalleryDataManager.SavedPhotos.Count - 1;
        }

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (GalleryDataManager.SavedPhotos.Count == 0) return;

        // Ensure the display is visible
        photoDisplay.color = Color.white;

        // Set the texture to the current index in our saved photos list
        photoDisplay.texture = GalleryDataManager.SavedPhotos[currentIndex];

        // Update the counter text
        if (counterText != null)
        {
            counterText.text = (currentIndex + 1) + " / " + GalleryDataManager.SavedPhotos.Count;
        }

        // Enable buttons if we have more than one photo
        bool canNavigate = GalleryDataManager.SavedPhotos.Count > 1;
        if (nextButton != null) nextButton.interactable = canNavigate;
        if (prevButton != null) prevButton.interactable = canNavigate;
    }
}