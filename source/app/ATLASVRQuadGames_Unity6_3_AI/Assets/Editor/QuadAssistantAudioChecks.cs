#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit audio regression checks; never records the microphone or calls Gemini.</summary>
public static class QuadAssistantAudioChecks
{
    private const string Output = "Diagnostics/2026-10-25-RayBanUI/audio-coexistence-after.txt";
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;
    private static readonly List<string> lines = new List<string>();

    [MenuItem("Tools/Ray-Ban Display/Validate Assistant music coexistence")]
    public static void StartChecks()
    {
        if (!Application.isPlaying || running) throw new InvalidOperationException("Use idle Play Mode.");
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsSortMode.None).Single();
        if (shell.assistantBridge.IsBusy || shell.assistantBridge.IsListening || shell.musicBridge.IsAcquired)
            throw new InvalidOperationException("Use a fresh Play session to preserve the user's active or paused Music session.");
        running = true;
        _ = Run(shell);
    }

    private static void Check(bool condition, string label)
    {
        lines.Add((condition ? "PASS " : "FAIL ") + label);
        File.WriteAllLines(Output, lines);
        if (!condition) throw new InvalidOperationException(label);
    }

    private static void Responding(RayBanAssistantBridge assistant, bool value)
    {
        typeof(AIManager).GetField("responseInProgress", PrivateInstance).SetValue(assistant.manager, value);
        typeof(RayBanAssistantBridge).GetMethod("UpdateMusicInterruption", PrivateInstance).Invoke(assistant, null);
    }

    private static void Suspension(MonoBehaviour component, bool value) =>
        component.SendMessage("OnApplicationPause", value, SendMessageOptions.DontRequireReceiver);

    private static async Task Run(RayBanDisplayShell shell)
    {
        var assistant = shell.assistantBridge;
        var music = shell.musicBridge;
        var player = music.player;
        float originalVolume = player.audioSource.volume;
        float originalSliderVolume = player.volumeSlider != null ? player.volumeSlider.value : originalVolume;
        string originalDetail = shell.CurrentDetail;
        object speech = null;
        Type speechType = typeof(AIManager).Assembly.GetType("BufferedAssistantSpeech");
        lines.Clear();
        lines.Add("Started UTC=" + DateTime.UtcNow.ToString("O"));
        lines.Add("Editor checks use simulated response/listening flags, real BufferedAssistantSpeech + Wit TTS, and the existing music source. No microphone, camera, Gemini or translation requests.");
        try
        {
            shell.OpenApp("Assistant");
            await Task.Yield(); await Task.Yield();
            var speaker = assistant.manager.ttsSpeaker;
            Check(speaker.AudioSource != player.audioSource, "Speech and Music have distinct AudioSources");
            Check(!music.IsAssistantAudioActive, "Opening idle Assistant does not interrupt Music");
            for (int scenario = 0; scenario < 3; scenario++)
            {
                if (scenario == 1) { music.Acquire(); music.SetVolume(.37f); player.Play(); await Task.Delay(300); }
                if (scenario == 2) player.Pause();
                bool wasPlaying = music.IsPlaying;
                float position = player.Elapsed;
                var musicClip = player.audioSource.clip;
                Responding(assistant, true);
                Check(music.IsAssistantAudioActive && !music.IsPlaying, "Scenario " + scenario + ": Music quiet during Assistant response");
                speech = Activator.CreateInstance(speechType, PrivateInstance, null, new object[] { speaker }, null);
                typeof(AIManager).GetField("responseSpeech", PrivateInstance).SetValue(assistant.manager, speech);
                Action checkActive = () => { if (!Application.isPlaying || !assistant.IsAcquired || !assistant.manager.IsResponding) throw new OperationCanceledException(); };
                var task = (Task)speechType.GetMethod("SpeakAsync", PrivateInstance).Invoke(speech,
                    new object[] { "The Assistant is speaking while music waits. This test checks that every phrase finishes clearly. When the answer ends, previously playing music should continue from the same position.", checkActive });
                int advances = 0, phrases = 0, lastSample = -1;
                object lastPhrase = null;
                bool virtualized = false;
                float deadline = Time.realtimeSinceStartup + 60f;
                while (!task.IsCompleted)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Buffered speech test deadline");
                    if (speaker.AudioSource.isPlaying)
                    {
                        virtualized |= speaker.AudioSource.isVirtual;
                        if (!ReferenceEquals(lastPhrase, speaker.SpeakingClip)) { phrases++; lastPhrase = speaker.SpeakingClip; lastSample = -1; }
                        if (speaker.AudioSource.timeSamples > lastSample) { advances++; lastSample = speaker.AudioSource.timeSamples; }
                    }
                    if (music.IsPlaying) throw new InvalidOperationException("Music resumed during speech");
                    await Task.Delay(50);
                }
                await task;
                Check(phrases >= 3 && advances > 5 && !virtualized, "Scenario " + scenario + ": buffered speech completed all chunks, phrases=" + phrases + ", progress=" + advances);
                Check(player.audioSource.clip == musicClip && Mathf.Abs(player.Elapsed-position) < .2f, "Scenario " + scenario + ": Music clip and position preserved during speech");
                Responding(assistant, false);
                Check(music.IsPlaying == wasPlaying, "Scenario " + scenario + ": only previously playing Music resumes");
                if (scenario > 0) Check(Mathf.Approximately(music.Volume, .37f), "Scenario " + scenario + ": Music volume preserved");
                typeof(AIManager).GetField("responseSpeech", PrivateInstance).SetValue(assistant.manager, null);
                speech = null;
            }

            player.Play();
            typeof(RayBanAssistantBridge).GetField("requested", PrivateInstance).SetValue(assistant, true);
            typeof(RayBanAssistantBridge).GetField("requestedAt", PrivateInstance).SetValue(assistant, Time.realtimeSinceStartup);
            Responding(assistant, false);
            Check(!music.IsPlaying && music.IsAssistantAudioActive, "Pending recognition interrupts Music before microphone activation");
            assistant.Stop();
            Check(music.IsPlaying, "Cancelled recognition restores previously playing Music");

            player.Play(); Responding(assistant, true); player.Pause(); Responding(assistant, false);
            Check(!music.IsPlaying, "User Pause during interruption prevents automatic resume");
            player.Play(); Responding(assistant, true); player.Stop(); Responding(assistant, false);
            Check(!music.IsPlaying, "User Stop during interruption prevents automatic resume");

            player.Play(); Responding(assistant, true); assistant.Stop();
            Check(music.IsPlaying && !music.IsAssistantAudioActive, "Assistant Stop cancels ownership and resumes Music once");
            assistant.Stop(); Check(music.IsPlaying, "Repeated Stop leaves playback state intact");

            Responding(assistant, true); shell.ShowLauncher();
            Check(music.IsPlaying && !music.IsAssistantAudioActive, "Closing Assistant resumes interrupted Music");
            shell.OpenApp("Assistant"); await Task.Yield();

            foreach (bool assistantFirst in new[] { true, false })
            {
                player.Play(); Responding(assistant, true);
                if (assistantFirst) { Suspension(assistant, true); Suspension(music, true); Suspension(player, true); }
                else { Suspension(player, true); Suspension(music, true); Suspension(assistant, true); }
                Check(!music.IsPlaying && !music.IsAssistantAudioActive, "Suspension stays silent; Assistant callback first=" + assistantFirst);
                Suspension(player, false); Suspension(music, false); Suspension(assistant, false);
                Check(!music.IsPlaying, "Resume from suspension does not restore stale Assistant lease; order=" + assistantFirst);
            }
            player.Play(); Responding(assistant, true); music.Release(); Responding(assistant, false);
            Check(!music.IsPlaying && !music.IsAcquired, "Releasing Music prevents stale resume");
            lines.Add("SUCCESS; source progression and lifecycle checks only. Audible Quest output and a fresh Android build remain unverified.");
        }
        catch (Exception error)
        {
            lines.Add("FAILED " + error.GetType().Name);
        }
        finally
        {
            if (Application.isPlaying && shell != null)
            {
                assistant.Stop();
                if (speech != null) speechType.GetMethod("Stop", PrivateInstance).Invoke(speech, new object[] { true });
                Suspension(assistant, false); Suspension(music, false); Suspension(player, false);
                music.Release();
                player.audioSource.volume = originalVolume;
                if (player.volumeSlider != null) player.volumeSlider.SetValueWithoutNotify(originalSliderVolume);
                if (string.IsNullOrEmpty(originalDetail)) shell.ShowLauncher(); else shell.OpenApp(originalDetail);
            }
            running = false;
            lines.Add("Finished UTC=" + DateTime.UtcNow.ToString("O"));
            File.WriteAllLines(Output, lines);
        }
    }
}
#endif
