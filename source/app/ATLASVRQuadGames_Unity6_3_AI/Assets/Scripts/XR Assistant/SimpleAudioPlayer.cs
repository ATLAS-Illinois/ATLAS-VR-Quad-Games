using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class MusicTrack
{
    public string title;
    public string artist;
    public AudioClip clip;
    public Sprite image;
}

public class SimpleAudioPlayer : MonoBehaviour
{
    [Header("Playlist")]
    public List<MusicTrack> playlist = new List<MusicTrack>();
    [Tooltip("Index of the track to start with")]
    public int startIndex = 0;

    [Header("UI")]
    public Slider volumeSlider;
    public Slider progressSlider;
    public TextMeshProUGUI currentTimeText;
    public TextMeshProUGUI totalTimeText;
    public TextMeshProUGUI nameText;
    public Image artworkImage;

    [Header("Controls")]
    public Button playPauseButton;
    public Image playPauseIcon;
    public Sprite playIcon;
    public Sprite pauseIcon;
    public Button nextButton;
    public Button prevButton;

    [Header("Audio")]
    [Tooltip("If null, one will be added automatically")]
    public AudioSource audioSource;

    private int currentIndex = 0;
    private bool wasPlayingBeforeDisable = false;
    private bool userScrubbing = false;

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // Buttons
        if (playPauseButton) playPauseButton.onClick.AddListener(TogglePlayPause);
        if (nextButton) nextButton.onClick.AddListener(NextTrack);
        if (prevButton) prevButton.onClick.AddListener(PreviousTrack);

        // Sliders
        if (volumeSlider)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (progressSlider)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.onValueChanged.AddListener(SeekNormalized);
        }

        currentIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, playlist.Count - 1));
    }

    void OnEnable()
    {
        // Set initial volume from slider if any
        if (volumeSlider) audioSource.volume = volumeSlider.value;

        // Load and play current track if available
        if (playlist.Count > 0)
        {
            LoadTrack(currentIndex);
            Play();
        }
        else
        {
            UpdateUIStatic();
        }
    }

    void OnDisable()
    {
        wasPlayingBeforeDisable = audioSource.isPlaying;
        Stop();
    }

    void Update()
    {
        if (audioSource.clip)
        {
            // Update progress UI while playing
            if (!userScrubbing)
            {
                float t = Mathf.Clamp01(audioSource.time / audioSource.clip.length);
                if (progressSlider) progressSlider.SetValueWithoutNotify(t);
            }

            if (currentTimeText) currentTimeText.text = FormatTime(audioSource.time);

            // Auto advance when the song finishes
            // Use a small epsilon to avoid floating point edge cases
            if (!audioSource.isPlaying && audioSource.time > 0.01f && Mathf.Abs(audioSource.time - audioSource.clip.length) < 0.1f)
            {
                NextTrack();
            }
        }
    }

    // Public UI hooks for EventTrigger if you want scrub begin and end
    public void OnScrubBegin() { userScrubbing = true; }
    public void OnScrubEnd()
    {
        userScrubbing = false;
        // Ensure UI and audio are in sync after releasing the handle
        if (progressSlider && audioSource.clip)
        {
            audioSource.time = Mathf.Clamp01(progressSlider.value) * audioSource.clip.length;
        }
    }

    private void SetVolume(float v)
    {
        audioSource.volume = v;
    }

    private void SeekNormalized(float normalized)
    {
        if (!audioSource.clip) return;
        if (!userScrubbing)
        {
            audioSource.time = Mathf.Clamp01(normalized) * audioSource.clip.length;
        }
        else
        {
            // If scrubbing, just update the time text live
            float t = Mathf.Clamp01(normalized) * audioSource.clip.length;
            if (currentTimeText) currentTimeText.text = FormatTime(t);
        }
    }

    private void LoadTrack(int index)
    {
        if (playlist.Count == 0) return;

        currentIndex = ((index % playlist.Count) + playlist.Count) % playlist.Count;
        var track = playlist[currentIndex];

        audioSource.clip = track.clip;
        audioSource.time = 0f;

        // Update static UI
        if (nameText) nameText.text = track != null ? $"{track.title} - {track.artist}" : "No Track";
        if (artworkImage) artworkImage.sprite = track != null ? track.image : null;

        if (totalTimeText)
        {
            string len = track != null && track.clip ? FormatTime(track.clip.length) : "00:00";
            totalTimeText.text = len;
        }

        if (currentTimeText) currentTimeText.text = "00:00";
        if (progressSlider) progressSlider.SetValueWithoutNotify(0f);

        UpdatePlayPauseIcon();
    }

    public void TogglePlayPause()
    {
        if (!audioSource.clip)
        {
            if (playlist.Count > 0)
            {
                LoadTrack(currentIndex);
            }
            else
            {
                return;
            }
        }

        if (audioSource.isPlaying) Pause();
        else Play();
    }

    public void Play()
    {
        if (!audioSource.clip) return;
        audioSource.Play();
        UpdatePlayPauseIcon();
        Debug.LogWarning("PLAY");
    }

    public void Pause()
    {
        if (!audioSource.clip) return;
        audioSource.Pause();
        UpdatePlayPauseIcon();
        Debug.LogWarning("PAUSE");
    }

    public void Stop()
    {
        if (!audioSource.clip) return;
        audioSource.Stop();
        UpdatePlayPauseIcon();
    }

    public void NextTrack()
    {
        if (playlist.Count == 0) return;
        LoadTrack(currentIndex + 1);
        Play();
    }

    public void PreviousTrack()
    {
        if (playlist.Count == 0) return;
        LoadTrack(currentIndex - 1);
        Play();
    }

    private void UpdatePlayPauseIcon()
    {
        if (!playPauseIcon) return;

        if (audioSource.isPlaying)
        {
            if (pauseIcon) playPauseIcon.sprite = pauseIcon;
        }
        else
        {
            if (playIcon) playPauseIcon.sprite = playIcon;
        }
    }

    private void UpdateUIStatic()
    {
        if (nameText) nameText.text = "No Track";
        if (artworkImage) artworkImage.sprite = null;
        if (currentTimeText) currentTimeText.text = "00:00";
        if (totalTimeText) totalTimeText.text = "00:00";
        if (progressSlider) progressSlider.SetValueWithoutNotify(0f);
        UpdatePlayPauseIcon();
    }

    private string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }
}
