#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Local UI checks only. Does not start voice, cloud requests, or external apps.</summary>
public static class QuadOnboardingChecks
{
    private const string OutputDirectory = "Diagnostics/2026-11-01-QuestValidation";
    [Serializable] private class Result { public string name; public bool passed; }
    [Serializable] private class Report
    {
        public int passed, failed;
        public List<Result> checks = new List<Result>();
        public string limitation = "Synchronous Editor UI and source-state checks; physical input, audible continuity, startup lifecycle and Android persistence require separate validation.";
        public string error;
    }

    public static string Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in idle Play Mode.");
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(s => s.gameObject.scene.IsValid());
        if (!shell.UsesLiveFeatures || shell.FeatureScreens == null || shell.assistantBridge == null || shell.captionBridge == null)
            throw new InvalidOperationException("The live Quad display and its existing voice bridges must be available.");
        if (!VoiceIdle(shell) || (shell.musicBridge != null && shell.musicBridge.IsAssistantAudioActive))
            throw new InvalidOperationException("Close the user's active voice session before validation.");

        var screen = shell.FeatureScreens;
        var report = new Report();
        bool hadRead = PlayerPrefs.HasKey(RayBanFeatureScreen.TutorialReadKey);
        int oldRead = PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialReadKey);
        bool hadDismissed = PlayerPrefs.HasKey(RayBanFeatureScreen.TutorialDismissedKey);
        int oldDismissed = PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialDismissedKey);
        MusicState musicState = null;
        Action<bool, string> check = (pass, name) =>
        {
            report.checks.Add(new Result { name = name, passed = pass });
            if (pass) report.passed++; else report.failed++;
        };
        Func<string, RayBanTile> control = action => screen.Controls.First(t => t != null && t.ActionId == action);
        Action<string> press = action =>
        {
            var tile = control(action);
            if (!tile.gameObject.activeInHierarchy || !tile.IsInteractable())
                throw new InvalidOperationException("Test control is not available: " + action);
            screen.Activate(tile);
        };
        Action begin = () =>
        {
            PlayerPrefs.DeleteKey(RayBanFeatureScreen.TutorialDismissedKey);
            shell.OpenApp("Tutorials");
            screen.BeginFirstRunTutorial();
        };
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            screen.UpgradeLocalFeatures();
            if (shell.router != null) shell.router.OpenEntireUI();

            begin();
            check(screen.CurrentView == "tutorials" && screen.IsFirstRunTutorial && screen.TutorialStepIndex == 0,
                "First-run tour starts at guide one");
            check(!control("tutorials.previous").IsInteractable(), "Previous is disabled on first onboarding guide");
            check(control("tutorials.skip").gameObject.activeInHierarchy, "Onboarding offers Skip");
            check(!control("tutorials.open").gameObject.activeInHierarchy, "Onboarding does not launch another feature");
            check(Label(control("tutorials.next")) == "Next", "First onboarding guide offers Next");
            QuadRayBanDisplayPreview.Capture(shell, OutputDirectory + "/onboarding-first.png");

            press("tutorials.skip");
            CheckDismissal(shell, check, "Skip");
            shell.OpenApp("Tutorials");
            check(!screen.IsFirstRunTutorial && screen.CurrentView == "tutorials", "Manual Tutorials stays a reusable guide");
            check(!control("tutorials.skip").gameObject.activeInHierarchy && control("tutorials.open").gameObject.activeInHierarchy,
                "Manual guide offers feature shortcuts instead of Skip");

            begin();
            screen.Back();
            CheckDismissal(shell, check, "Back");

            begin();
            int readBefore = screen.TutorialReadCount;
            for (int i = 0; i < screen.TutorialStepCount; i++)
            {
                check(screen.TutorialStepIndex == i, "Onboarding guide " + (i + 1) + " follows sequence");
                CheckText(screen, check, "Onboarding guide " + (i + 1));
                check(VoiceIdle(shell), "Onboarding guide " + (i + 1) + " keeps voice idle");
                if (i < screen.TutorialStepCount - 1) press("tutorials.next");
            }
            check(Label(control("tutorials.next")) == "Finish", "Last onboarding guide replaces Next with Finish");
            check(control("tutorials.previous").IsInteractable(), "Previous becomes available after first guide");
            check(screen.TutorialReadCount == readBefore, "Tour navigation does not mark guides read");
            QuadRayBanDisplayPreview.Capture(shell, OutputDirectory + "/onboarding-last.png");
            press("tutorials.next");
            CheckDismissal(shell, check, "Finish");
            shell.OpenApp("Tutorials");
            check(!screen.IsFirstRunTutorial && Label(control("tutorials.next")) == "Next", "Manual last guide restores Next");
            press("tutorials.next");
            check(screen.TutorialStepIndex == 0 && control("tutorials.previous").IsInteractable(), "Manual guide still wraps without first-run restrictions");

            bool playingBeforeAudioOnly = shell.musicBridge != null && shell.musicBridge.IsPlaying;
            shell.OpenApp("Audio only");
            check(screen.CurrentView == "audio only", "Audio only opens its real app screen");
            check(control("audioonly.music").gameObject.activeInHierarchy && control("audioonly.hide").gameObject.activeInHierarchy,
                "Audio only exposes Music and Hide display controls");
            check((shell.musicBridge != null && shell.musicBridge.IsPlaying) == playingBeforeAudioOnly,
                "Opening Audio only does not change music playback");
            check(!screen.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Demo badge" && t.gameObject.activeInHierarchy),
                "Live Audio only is not marked as a demo");
            CheckText(screen, check, "Audio only");
            QuadRayBanDisplayPreview.Capture(shell, OutputDirectory + "/audio-only.png");

            var music = shell.musicBridge;
            if (MusicState.CanSafelyTest(music))
            {
                musicState = new MusicState(music);
                musicState.Source.mute = true;
                press("audioonly.music");
                check(screen.CurrentView == "music", "Audio only Music shortcut opens Music");
                check(music.IsPlaying == musicState.WasPlaying, "Music shortcut does not autoplay or stop existing playback");
                check(music.IsAcquired && music.player.IsExternallyControlled, "Music retains its existing app-owned player");
                if (!music.IsPlaying) music.player.Play();
                check(music.IsPlaying, "Muted continuity check starts the existing music source");
                var clip = music.player.audioSource.clip;
                int index = music.CurrentIndex;
                shell.OpenApp("Audio only");
                press("audioonly.hide");
                check(!shell.IsVisible, "Audio only Hide display hides the visual tree");
                check(music.IsPlaying && music.IsAcquired && music.player.audioSource == musicState.Source &&
                      music.CurrentIndex == index && music.player.audioSource.clip == clip,
                    "Hiding display preserves playing track and source ownership");
                if (shell.router != null) shell.router.OpenEntireUI(); else shell.ShowLauncher();
                check(shell.IsVisible && music.IsPlaying && music.player.audioSource == musicState.Source,
                    "Reopening display preserves music playback and source ownership");
                check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == musicState.SourceCount,
                    "Audio only adds no AudioSource");
            }
            else report.limitation += " Music continuity/shortcut playback checks skipped: no safely restorable app-owned source and playable current track.";
            check(VoiceIdle(shell), "Tutorial and Audio only checks leave microphone bridges idle");
        }
        catch (Exception exception)
        {
            report.error = exception.GetType().Name + ": " + exception.Message;
            check(false, "Harness completed without an exception");
            throw;
        }
        finally
        {
            try
            {
                shell.Hide();
                musicState?.Restore();
            }
            finally
            {
                RestorePreference(RayBanFeatureScreen.TutorialReadKey, hadRead, oldRead);
                RestorePreference(RayBanFeatureScreen.TutorialDismissedKey, hadDismissed, oldDismissed);
                PlayerPrefs.Save();
                Directory.CreateDirectory(OutputDirectory);
                File.WriteAllText(OutputDirectory + "/onboarding-checks.json", JsonUtility.ToJson(report, true));
            }
        }
        return "Onboarding checks: " + report.passed + " passed; " + report.failed + " failed.";
    }

    private static bool VoiceIdle(RayBanDisplayShell shell) =>
        !shell.assistantBridge.IsBusy && !shell.assistantBridge.IsListening && !shell.captionBridge.IsBusy && !shell.captionBridge.IsListening;

    private static string Label(RayBanTile tile) => tile.GetComponentsInChildren<TMP_Text>().First(t => t.name == "Control label").text;

    private static void CheckDismissal(RayBanDisplayShell shell, Action<bool, string> check, string action)
    {
        check(PlayerPrefs.GetInt(RayBanFeatureScreen.TutorialDismissedKey, 0) == 1 && !RayBanFeatureScreen.NeedsFirstRunTutorial,
            action + " persists welcome dismissal");
        check(!shell.FeatureScreens.IsFirstRunTutorial && string.IsNullOrEmpty(shell.CurrentDetail), action + " returns to launcher");
    }

    private static void CheckText(RayBanFeatureScreen screen, Action<bool, string> check, string context)
    {
        Canvas.ForceUpdateCanvases();
        var texts = screen.GetComponentsInChildren<TMP_Text>();
        foreach (var text in texts) text.ForceMeshUpdate();
        var overflowing = texts.Where(t => t.isTextOverflowing).Select(t => t.name).ToArray();
        check(overflowing.Length == 0, context + " text fits" + (overflowing.Length == 0 ? "" : ": " + string.Join(", ", overflowing)));
    }

    private static void RestorePreference(string key, bool existed, int value)
    {
        if (existed) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
    }

    private sealed class MusicState
    {
        private readonly RayBanMusicBridge bridge;
        private readonly bool wasAcquired, wasPaused, wasMuted;
        private readonly int index;
        private readonly AudioClip clip;
        private readonly float time, volume;
        public readonly AudioSource Source;
        public readonly bool WasPlaying;
        public readonly int SourceCount;

        public static bool CanSafelyTest(RayBanMusicBridge music) => music != null && music.IsAvailable &&
            !music.IsAssistantAudioActive && music.player != null && music.player.audioSource != null &&
            music.CurrentTrack != null && music.CurrentTrack.clip != null &&
            (music.IsAcquired || (!music.player.gameObject.activeInHierarchy && !music.player.IsPaused));

        public MusicState(RayBanMusicBridge music)
        {
            bridge = music;
            Source = music.player.audioSource;
            wasAcquired = music.IsAcquired;
            WasPlaying = music.player.IsPlaying;
            wasPaused = music.player.IsPaused;
            index = music.CurrentIndex;
            clip = Source.clip;
            time = clip != null ? Source.time : 0;
            volume = Source.volume;
            wasMuted = Source.mute;
            SourceCount = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        public void Restore()
        {
            if (Source == null || bridge == null || bridge.player == null) return;
            try
            {
                var player = bridge.player;
                player.Stop();
                // Acquisition can initialize an inactive player at its start index.
                // Restore through its public playlist API, still muted.
                for (int i = 0; player.CurrentIndex != index && i < player.playlist.Count; i++) player.NextTrack();
                player.Stop();
                if (!wasAcquired) bridge.Release();
                Source.clip = clip;
                if (WasPlaying || wasPaused)
                {
                    player.Play();
                    if (wasPaused) player.Pause();
                }
                else player.Stop();
                if (clip != null && Source.isActiveAndEnabled) Source.time = Mathf.Clamp(time, 0, Mathf.Max(0, clip.length - .001f));
            }
            finally
            {
                Source.volume = volume;
                Source.mute = wasMuted;
            }
        }
    }
}
#endif
