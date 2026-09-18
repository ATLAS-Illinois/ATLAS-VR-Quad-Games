using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class MenuEntry
{
    public string name;            // label for clarity in the Inspector
    public GameObject page;        // The UI page to show
    public Button button;          // The button that opens this page
}

public class QuestDisplayUI : MonoBehaviour
{
    [Header("References")]
    public bool showMainMenuOnStart = true;
    public GameObject mainMenuRoot;
    public List<MenuEntry> entries = new List<MenuEntry>();
    public CanvasGroup canvasGroup; // assign the parent CanvasGroup here in Inspector

    [Header("Fade Settings")]
    [Tooltip("Duration of fade transitions in seconds.")]
    public float fadeDuration = 0.25f;

    private Coroutine fadeRoutine;

    // Tracks if the global UI is visible or hidden
    private bool isUIVisible = true;

    void Start()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (showMainMenuOnStart)
        {
            isUIVisible = true;
            ShowMainMenuInstant();
        }
        else
        {
            isUIVisible = false;
            HideMainMenuInstant();
        }

        for (int i = 0; i < entries.Count; i++)
        {
            int index = i;
            var entry = entries[index];
            if (entry.button != null)
                entry.button.onClick.AddListener(() => ShowPage(index));
        }
    }

    // --- NEW: Global Toggle Logic ---
    void Update()
    {
        // Listen for the physical controller Menu button
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            ToggleEntireUI();
        }
    }

    public void ToggleEntireUI()
    {
        if (isUIVisible) CloseEntireUI();
        else OpenEntireUI();
    }

    public void CloseEntireUI()
    {
        if (!isUIVisible) return;
        isUIVisible = false;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeEntireUI(0f));
    }

    public void OpenEntireUI()
    {
        if (isUIVisible) return;
        isUIVisible = true;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeEntireUI(1f));
    }

    private IEnumerator FadeEntireUI(float targetAlpha)
    {
        // Instantly disable clicking when fading out
        if (targetAlpha == 0f && canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / fadeDuration);
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = targetAlpha;

            // Re-enable clicking only if we faded completely back in
            bool isVisible = (targetAlpha > 0f);
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }
    }
    // --------------------------------

    public void ShowPage(int index)
    {
        if (index < 0 || index >= entries.Count)
        {
            Debug.LogWarning($"QuestDisplayUI: ShowPage index {index} is out of range.");
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTransition(() =>
        {
            mainMenuRoot.SetActive(false);
            DeactivateAllPages();

            var selected = entries[index]?.page;
            if (selected != null)
                selected.SetActive(true);
        }));
    }

    public void ShowMainMenuIfHidden()
    {
        bool isShowing = false;
        for (int i = 0; i < entries.Count; i++)
        {
            var page = entries[i]?.page;
            if (page.activeSelf)
                isShowing = true;
        }

        if (!isShowing && !mainMenuRoot.activeSelf)
            ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTransition(() =>
        {
            DeactivateAllPages();
            mainMenuRoot.SetActive(true);
        }));
    }

    public void HideMainMenu()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTransition(() =>
        {
            DeactivateAllPages();
            mainMenuRoot.SetActive(false);
        }));
    }

    private void HideMainMenuInstant()
    {
        DeactivateAllPages();
        if (mainMenuRoot != null) mainMenuRoot.SetActive(false);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void ShowMainMenuInstant()
    {
        DeactivateAllPages();
        if (mainMenuRoot != null) mainMenuRoot.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void DeactivateAllPages()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var page = entries[i]?.page;
            if (page != null) page.SetActive(false);
        }
    }

    private IEnumerator FadeTransition(Action onMidTransition)
    {
        if (canvasGroup != null) yield return Fade(1f, 0f);

        onMidTransition?.Invoke();

        if (canvasGroup != null) yield return Fade(0f, 1f);
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        canvasGroup.alpha = from;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}