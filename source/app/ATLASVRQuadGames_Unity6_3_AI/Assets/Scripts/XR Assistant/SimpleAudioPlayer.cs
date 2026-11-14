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

public enum MusicRepeatMode { Off, All, One }

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
    private bool userScrubbing = false;
    private bool initialized;
    private bool externalControl;
    private bool paused;
    private bool playbackRequested;
    private bool observedPlayback;
    private bool applicationPaused;
    private bool resumeAfterApplicationPause;
    [SerializeField] private MusicRepeatMode repeatMode = MusicRepeatMode.All;

    public int CurrentIndex => currentIndex;
    public MusicTrack CurrentTrack => playlist != null && currentIndex >= 0 && currentIndex < playlist.Count
        ? playlist[currentIndex] : null;
    public float Elapsed => audioSource != null && audioSource.clip != null ? audioSource.time : 0f;
    public float Duration => audioSource != null && audioSource.clip != null ? audioSource.clip.length : 0f;
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
    public bool IsPaused => paused;
    // Each explicit request, including an already-paused Pause, supersedes an
    // interrupted playback session's permission to resume automatically.
    public int PlaybackRevision { get; private set; }
    public bool IsExternallyControlled => externalControl;
    public MusicRepeatMode RepeatMode => repeatMode;

    void Awake()
    {
        Initialize(!externalControl);
    }

    // The new shell leases this existing player before activating its legacy page.
    // Initializing while inactive prevents the legacy OnEnable autoplay path.
    public bool SetExternalControl(bool value)
    {
        bool previousControl = externalControl;
        externalControl = value;
        if (!Initialize(!value))
        {
            externalControl = previousControl;
            return false;
        }
        if (value)
        {
            Stop();
            LoadTrack(FindPlayableTrack(currentIndex, 1));
        }
        return true;
    }

    private bool Initialize(bool allowSourceCreation)
    {
        if (initialized) return audioSource != null;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && allowSourceCreation) audioSource = gameObject.AddComponent<AudioSource>();
        }
        if (audioSource == null) return false;

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

        currentIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, (playlist?.Count ?? 0) - 1));
        initialized = true;
        return true;
    }

    void OnEnable()
    {
        if (!Initialize(!externalControl)) return;
        if (externalControl)
        {
            if (audioSource.clip == null) LoadTrack(FindPlayableTrack(currentIndex, 1));
            return;
        }
        // Set initial volume from slider if any
        if (volumeSlider) audioSource.volume = volumeSlider.value;

        // Load and play current track if available
        if (playlist != null && playlist.Count > 0)
        {
            LoadTrack(FindPlayableTrack(currentIndex, 1));
            Play();
        }
        else
        {
            UpdateUIStatic();
        }
    }

    void OnDisable()
    {
        Stop();
        resumeAfterApplicationPause = false;
    }

    void Update()
    {
        if (audioSource != null && audioSource.clip)
        {
            // Update progress UI while playing
            if (!userScrubbing)
            {
                float t = Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 0f;
                if (progressSlider) progressSlider.SetValueWithoutNotify(t);
            }

            if (currentTimeText) currentTimeText.text = FormatTime(audioSource.time);

            // Unity can reset time to zero at the end. Observe playback instead
            // of depending on the final sample position surviving that reset.
            if (audioSource.isPlaying) observedPlayback = true;
            else if (playbackRequested && observedPlayback && !paused &&
                     !applicationPaused && !AudioListener.pause)
            {
                CompleteTrack();
            }
        }
    }

    void OnApplicationPause(bool value)
    {
        applicationPaused = value;
        if (value)
        {
            resumeAfterApplicationPause = IsPlaying;
            if (resumeAfterApplicationPause) Pause();
        }
        else if (resumeAfterApplicationPause)
        {
            resumeAfterApplicationPause = false;
            Play();
        }
    }

    // Public UI hooks for EventTrigger if you want scrub begin and end
    public void OnScrubBegin() { userScrubbing = true; }
    public void OnScrubEnd()
    {
        userScrubbing = false;
        // Ensure UI and audio are in sync after releasing the handle
        if (progressSlider && audioSource != null && audioSource.clip)
        {
            audioSource.time = Mathf.Clamp01(progressSlider.value) * audioSource.clip.length;
        }
    }

    private void SetVolume(float v)
    {
        if (audioSource != null) audioSource.volume = Mathf.Clamp01(v);
    }

    private void SeekNormalized(float normalized)
    {
        if (audioSource == null || !audioSource.clip) return;
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
        if (audioSource == null) return;
        Stop();
        if (playlist == null || playlist.Count == 0 || index < 0)
        {
            audioSource.clip = null;
            UpdateUIStatic();
            return;
        }

        currentIndex = ((index % playlist.Count) + playlist.Count) % playlist.Count;
        var track = playlist[currentIndex];

        audioSource.clip = track != null ? track.clip : null;
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
        if (!Initialize(!externalControl)) return;
        if (!audioSource.clip)
        {
            if (playlist != null && playlist.Count > 0)
            {
                LoadTrack(FindPlayableTrack(currentIndex, 1));
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
        PlaybackRevision++;
        if (audioSource == null || !audioSource.clip || !audioSource.isActiveAndEnabled || IsPlaying) return;
        if (paused) audioSource.UnPause();
        else audioSource.Play();
        paused = false;
        playbackRequested = true;
        observedPlayback = audioSource.isPlaying;
        UpdatePlayPauseIcon();
    }

    public void Pause()
    {
        PlaybackRevision++;
        if (audioSource == null || !audioSource.clip || (!IsPlaying && !paused)) return;
        audioSource.Pause();
        paused = true;
        playbackRequested = false;
        UpdatePlayPauseIcon();
    }

    public void Stop()
    {
        PlaybackRevision++;
        resumeAfterApplicationPause = false;
        playbackRequested = false;
        observedPlayback = false;
        paused = false;
        if (audioSource != null) audioSource.Stop();
        UpdatePlayPauseIcon();
    }

    public void NextTrack()
    {
        if (playlist == null || playlist.Count == 0) return;
        LoadTrack(FindPlayableTrack(currentIndex + 1, 1));
        Play();
    }

    public void PreviousTrack()
    {
        if (playlist == null || playlist.Count == 0) return;
        LoadTrack(FindPlayableTrack(currentIndex - 1, -1));
        Play();
    }

    /// <summary>Explicit timeline changes invalidate an interrupted session's auto-resume.</summary>
    public bool SeekSeconds(float seconds)
    {
        if (audioSource == null || audioSource.clip == null || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
        PlaybackRevision++;
        resumeAfterApplicationPause = false;
        // The final sample is not a valid seeking position on every decoder.
        audioSource.time = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, audioSource.clip.length - .01f));
        if (progressSlider) progressSlider.SetValueWithoutNotify(Duration > 0f ? Elapsed / Duration : 0f);
        return true;
    }

    public bool SelectTrack(int index)
    {
        if (!Initialize(!externalControl) || playlist == null || index < 0 || index >= playlist.Count ||
            playlist[index] == null || playlist[index].clip == null) return false;
        LoadTrack(index);
        Play();
        return true;
    }

    public void SetRepeatMode(MusicRepeatMode mode)
    {
        repeatMode = (int)mode >= (int)MusicRepeatMode.Off && (int)mode <= (int)MusicRepeatMode.One ? mode : MusicRepeatMode.All;
    }

    private void CompleteTrack()
    {
        if (repeatMode == MusicRepeatMode.One)
        {
            // Play starts at the beginning after Unity resets the completed source.
            audioSource.time = 0f;
            Play();
            return;
        }
        int next = FindPlayableTrack(currentIndex + 1, 1);
        if (repeatMode == MusicRepeatMode.Off && next <= currentIndex)
        {
            Stop();
            return;
        }
        LoadTrack(next);
        Play();
    }

    private int FindPlayableTrack(int index, int direction)
    {
        if (playlist == null || playlist.Count == 0) return -1;
        for (int i = 0; i < playlist.Count; i++)
        {
            int candidate = ((index % playlist.Count) + playlist.Count) % playlist.Count;
            if (playlist[candidate] != null && playlist[candidate].clip != null) return candidate;
            index += direction;
        }
        return -1;
    }

    private void UpdatePlayPauseIcon()
    {
        if (!playPauseIcon) return;

        if (IsPlaying)
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
