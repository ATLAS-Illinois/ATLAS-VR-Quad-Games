#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Isolated, silent playback checks. Never touches the user's playlist, camera or album.</summary>
public static class QuadExtendedMediaChecks
{
    [Serializable] private sealed class Check { public string name; public bool passed; }
    [Serializable] private sealed class Report
    {
        public int passed, failed;
        public string limitation = "Editor playback logic with silent temporary clips. Hardware audio, timer capture, photo appearance and optical gestures require separate runtime validation.";
        public List<Check> checks = new List<Check>();
    }

    [MenuItem("Tools/Ray-Ban Display/Validate extended media backend")]
    public static string Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || AudioListener.pause)
            throw new InvalidOperationException("Run in idle Play Mode with audio unpaused.");
        var report = new Report();
        GameObject root = null;
        AudioClip first = null, last = null;
        Action<bool, string> check = (pass, name) =>
        { report.checks.Add(new Check { name = name, passed = pass }); if (pass) report.passed++; else report.failed++; };
        try
        {
            root = new GameObject("Isolated media validation");
            var legacy = new GameObject("Temporary playlist"); legacy.transform.SetParent(root.transform); legacy.SetActive(false);
            var source = legacy.AddComponent<AudioSource>(); source.playOnAwake = false; source.mute = true;
            var player = legacy.AddComponent<SimpleAudioPlayer>(); player.audioSource = source;
            first = AudioClip.Create("Silent first track", 32 * 8000, 1, 8000, false);
            last = AudioClip.Create("Silent last track", 32 * 8000, 1, 8000, false);
            player.playlist = new List<MusicTrack> {
                new MusicTrack { title = "First", clip = first }, null,
                new MusicTrack { title = "Last", clip = last } };
            var bridge = root.AddComponent<RayBanMusicBridge>(); bridge.player = player; bridge.legacyPageRoot = legacy;
            check(bridge.Acquire() && !player.IsPlaying, "Acquire prepares the existing AudioSource without autoplay");
            check(legacy.GetComponents<AudioSource>().Length == 1, "Acquire does not create another AudioSource");
            check(bridge.TrackCount == 3 && bridge.GetTrack(-1) == null && bridge.GetTrack(3) == null, "Track lookup handles bounds and sparse playlists");
            check(bridge.SelectTrack(0) && source.clip == first, "Selecting a valid track uses its clip");
            player.Pause();
            int revision = player.PlaybackRevision;
            check(!bridge.SelectTrack(1) && !bridge.SelectTrack(100) && source.clip == first && revision == player.PlaybackRevision,
                "Invalid selections preserve the paused clip and ownership revision");
            check(player.SeekSeconds(15f) && Mathf.Abs(player.Elapsed - 15f) < .05f && player.IsPaused && !player.IsPlaying,
                "Seek preserves paused state");
            check(player.PlaybackRevision > revision, "Seeking supersedes automatic-resume permission");
            bridge.SeekBy(10f); check(Mathf.Abs(player.Elapsed - 25f) < .05f, "Forward seek advances ten seconds");
            bridge.SeekBy(-10f); check(Mathf.Abs(player.Elapsed - 15f) < .05f, "Backward seek moves ten seconds");
            player.SeekSeconds(-100f); check(player.Elapsed < .05f, "Seek clamps before the start");
            player.SeekSeconds(999f); check(player.Elapsed <= player.Duration && player.Elapsed > player.Duration - .05f, "Seek clamps below the final sample");
            revision = player.PlaybackRevision;
            check(!player.SeekSeconds(float.NaN) && !bridge.SeekBy(float.PositiveInfinity) && revision == player.PlaybackRevision,
                "Nonfinite seeks leave playback state unchanged");
            player.SetRepeatMode(MusicRepeatMode.Off); bridge.CycleRepeatMode();
            check(player.RepeatMode == MusicRepeatMode.All, "Repeat cycles from Off to All");
            bridge.CycleRepeatMode(); check(player.RepeatMode == MusicRepeatMode.One, "Repeat cycles from All to One");
            bridge.CycleRepeatMode(); check(player.RepeatMode == MusicRepeatMode.Off, "Repeat cycles from One to Off");
            var complete = typeof(SimpleAudioPlayer).GetMethod("CompleteTrack", BindingFlags.Instance | BindingFlags.NonPublic);
            player.SelectTrack(0); source.Stop(); complete.Invoke(player, null);
            check(player.CurrentIndex == 2, "Repeat Off advances past missing clips");
            source.Stop(); complete.Invoke(player, null);
            check(player.CurrentIndex == 2 && !player.IsPlaying && !player.IsPaused, "Repeat Off stops after the final playable clip");
            player.SetRepeatMode(MusicRepeatMode.All); source.Stop(); complete.Invoke(player, null);
            check(player.CurrentIndex == 0, "Repeat All wraps to the first playable clip");
            player.SetRepeatMode(MusicRepeatMode.One); player.SelectTrack(2); source.Stop(); complete.Invoke(player, null);
            check(player.CurrentIndex == 2 && player.Elapsed < .1f, "Repeat One restarts the same clip");
            bridge.SetAssistantAudioActive(true);
            check(player.IsPaused && !bridge.IsPlaying, "Assistant interruption still pauses Music");
            bridge.SeekBy(10f); bridge.SetAssistantAudioActive(false);
            check(player.IsPaused && !bridge.IsPlaying, "Seek during interruption prevents stale automatic resume");
            player.Play(); bridge.SetAssistantAudioActive(true);
            check(!bridge.SelectTrack(0), "Track picker does not start Music over an active Assistant");
            bridge.SetAssistantAudioActive(false); check(bridge.IsPlaying, "An unchanged interrupted session still resumes");
            bridge.SetVolume(2f); check(Mathf.Approximately(bridge.Volume, 1f), "Music volume clamps to maximum");
            bridge.SetVolume(-1f); check(Mathf.Approximately(bridge.Volume, 0f), "Music volume clamps to minimum");
            bridge.Release(); check(!legacy.activeSelf && !source.isPlaying, "Release restores inactive legacy page and stops audio");
        }
        finally
        {
            if (root) UnityEngine.Object.DestroyImmediate(root);
            if (first) UnityEngine.Object.DestroyImmediate(first);
            if (last) UnityEngine.Object.DestroyImmediate(last);
            const string directory = "Diagnostics/2026-11-08-DisplayCompletion";
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "extended-media-checks.json"), JsonUtility.ToJson(report, true));
        }
        return "Extended media checks: " + report.passed + " passed; " + report.failed + " failed.";
    }
}
#endif
