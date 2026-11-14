using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets the new display use the original music player without showing its old
/// Canvas or activating its old gesture handlers. The AudioSource stays in place.
/// </summary>
[DisallowMultipleComponent]
public sealed class RayBanMusicBridge : MonoBehaviour
{
    public SimpleAudioPlayer player;

    [Tooltip("The legacy Music menu entry (Audio Player), which contains AudioPlayerRoot.")]
    public GameObject legacyPageRoot;

    private readonly Dictionary<GameObject, bool> childStates = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Behaviour, bool> componentStates = new Dictionary<Behaviour, bool>();
    private GameObject leasedRoot;
    private bool originalActive;
    private bool acquired;
    private bool quitting;
    private bool applicationPaused;
    private bool assistantAudioActive;
    private bool resumeAfterAssistant;
    private AudioSource interruptedSource;
    private AudioClip interruptedClip;
    private int interruptedTrack;
    private int interruptedPlaybackRevision;

    public bool IsAcquired => acquired;
    public bool IsAssistantAudioActive => assistantAudioActive;
    public MusicTrack CurrentTrack => player != null ? player.CurrentTrack : null;
    public int CurrentIndex => player != null ? player.CurrentIndex : -1;
    public float Elapsed => player != null ? player.Elapsed : 0f;
    public float Duration => player != null ? player.Duration : 0f;
    public bool IsPlaying => acquired && player != null && player.IsPlaying;
    public float Volume => player != null && player.audioSource != null ? player.audioSource.volume : 0f;
    public int TrackCount => player != null && player.playlist != null ? player.playlist.Count : 0;
    public MusicRepeatMode RepeatMode => player != null ? player.RepeatMode : MusicRepeatMode.All;
    public MusicTrack GetTrack(int index) => player != null && player.playlist != null && index >= 0 && index < player.playlist.Count
        ? player.playlist[index] : null;

    public bool SelectTrack(int index)
    {
        if (assistantAudioActive || !Acquire()) return false;
        return player.SelectTrack(index);
    }

    public bool SeekBy(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || !Acquire()) return false;
        return player.SeekSeconds(player.Elapsed + seconds);
    }

    public void CycleRepeatMode()
    {
        if (!Acquire()) return;
        player.SetRepeatMode((MusicRepeatMode)(((int)player.RepeatMode + 1) % 3));
    }

    /// <summary>Music-only gain, matching the original slider; app/system volume is separate.</summary>
    public void SetVolume(float value)
    {
        if (!acquired || !IsAvailable || float.IsNaN(value) || float.IsInfinity(value)) return;
        value = Mathf.Clamp01(value);
        player.audioSource.volume = value;
        if (player.volumeSlider != null) player.volumeSlider.SetValueWithoutNotify(value);
    }

    public bool IsAvailable
    {
        get
        {
            if (player == null || player.playlist == null) return false;
            var source = player.audioSource != null ? player.audioSource : player.GetComponent<AudioSource>();
            // The current project keeps both components on the legacy page root.
            // Do not silently add a duplicate source for another configuration.
            if (source == null || source.gameObject != player.gameObject) return false;
            foreach (var track in player.playlist)
                if (track != null && track.clip != null) return true;
            return false;
        }
    }

    public string Status => !IsAvailable ? "No music available" : IsPlaying ? "Playing" : "Paused";

    /// <summary>
    /// Temporarily pauses this player's music during Assistant listening and
    /// speech. Only the unchanged playback session may resume afterward.
    /// </summary>
    public void SetAssistantAudioActive(bool active, bool allowResume = true)
    {
        if (active)
        {
            if (assistantAudioActive || quitting || applicationPaused || !isActiveAndEnabled) return;
            assistantAudioActive = true;
            ClearAssistantResume();
            if (!acquired || player == null || !player.isActiveAndEnabled || !player.IsPlaying) return;

            interruptedSource = player.audioSource;
            interruptedClip = interruptedSource != null ? interruptedSource.clip : null;
            interruptedTrack = player.CurrentIndex;
            player.Pause();
            interruptedPlaybackRevision = player.PlaybackRevision;
            resumeAfterAssistant = player.IsPaused && interruptedClip != null;
            return;
        }

        bool resume = assistantAudioActive && resumeAfterAssistant && allowResume &&
            acquired && isActiveAndEnabled && !quitting && !applicationPaused &&
            player != null && player.isActiveAndEnabled && player.IsPaused &&
            player.audioSource == interruptedSource && interruptedSource != null &&
            interruptedSource.isActiveAndEnabled && interruptedSource.clip == interruptedClip &&
            player.CurrentIndex == interruptedTrack && player.PlaybackRevision == interruptedPlaybackRevision;
        // Clear before invoking the backend so duplicate cancellation/close
        // callbacks cannot resume a second time or retain a stale intent.
        assistantAudioActive = false;
        ClearAssistantResume();
        if (resume) player.Play();
    }

    private void ClearAssistantResume()
    {
        resumeAfterAssistant = false;
        interruptedSource = null;
        interruptedClip = null;
        interruptedTrack = -1;
        interruptedPlaybackRevision = 0;
    }

    /// <summary>Prepare the existing player, without starting audio.</summary>
    public bool Acquire()
    {
        if (!Application.isPlaying || quitting || !IsAvailable) return false;
        if (acquired) return true;

        leasedRoot = legacyPageRoot != null ? legacyPageRoot : player.gameObject;
        // Never activate a broad rig or unrelated root from a mistaken assignment.
        if (!player.transform.IsChildOf(leasedRoot.transform) || transform.IsChildOf(leasedRoot.transform))
        { leasedRoot = null; return false; }
        originalActive = leasedRoot.activeSelf;
        leasedRoot.SetActive(false);

        // Keep only the ancestor chain to the player active. Canvas/poke/ray
        // components live on AudioPlayerRoot in Quad, while its page parent is
        // inactive. Hiding the children alone would leave that whole branch off.
        Transform pathChild = null;
        for (Transform current = player.transform; current != null; current = current.parent)
        {
            childStates[current.gameObject] = current.gameObject == leasedRoot ? originalActive : current.gameObject.activeSelf;
            foreach (Transform child in current)
            {
                if (child == pathChild) continue;
                childStates[child.gameObject] = child.gameObject.activeSelf;
                child.gameObject.SetActive(false);
            }
            foreach (var component in current.GetComponents<Behaviour>())
            {
                componentStates[component] = component.enabled;
                if (component != player && component != player.audioSource) component.enabled = false;
            }
            if (current.gameObject == leasedRoot) break;
            current.gameObject.SetActive(true);
            pathChild = current;
        }

        if (!player.SetExternalControl(true))
        {
            RestorePresentation(true);
            return false;
        }

        player.enabled = true;
        player.audioSource.enabled = true;
        acquired = true;
        leasedRoot.SetActive(true);
        if (!player.gameObject.activeInHierarchy)
        {
            Release();
            return false;
        }
        return true;
    }

    public void Toggle()
    {
        if (Acquire()) player.TogglePlayPause();
    }

    public void Next()
    {
        if (Acquire()) player.NextTrack();
    }

    public void Previous()
    {
        if (Acquire()) player.PreviousTrack();
    }

    /// <summary>
    /// Normal release restores the original presentation. On application quit,
    /// pass false so no legacy handlers are reactivated during teardown.
    /// </summary>
    public void Release(bool restoreOriginalState = true)
    {
        SetAssistantAudioActive(false, false);
        if (!acquired && componentStates.Count == 0) return;
        if (player != null)
        {
            player.Stop();
            if (leasedRoot != null) leasedRoot.SetActive(false);
            player.SetExternalControl(false);
        }
        acquired = false;
        RestorePresentation(restoreOriginalState && !quitting);
    }

    private void RestorePresentation(bool restoreOriginalState)
    {
        // The root is inactive while restoring its children and UI/input flags.
        foreach (var pair in childStates)
            if (pair.Key != null && pair.Key != leasedRoot) pair.Key.SetActive(pair.Value);
        foreach (var pair in componentStates)
            if (pair.Key != null) pair.Key.enabled = pair.Value;
        childStates.Clear();
        componentStates.Clear();
        if (leasedRoot != null && restoreOriginalState) leasedRoot.SetActive(originalActive);
        leasedRoot = null;
    }

    private void OnApplicationQuit()
    {
        quitting = true;
        Release(false);
    }

    private void OnApplicationPause(bool value)
    {
        applicationPaused = value;
        if (value) SetAssistantAudioActive(false, false);
    }

    private void OnDisable() => Release(!quitting);
}
