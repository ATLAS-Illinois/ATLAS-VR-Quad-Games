using System.Collections.Generic;
using UnityEngine;

namespace Whacamole.Gameplay
{
    public enum WhacSound
    {
        Hit,        // wooden "tok" when a nut is whacked
        Explosion,  // bomb going off
        PowerUp,    // sparkly chime when an acorn power-up is collected
        PowerDown,  // falling two-note blip when a timed power-up wears off
        LevelUp,    // short rising fanfare on a level up
        Miss,       // low "bwomp" when a nut gets away
        Combo,      // quick bright arpeggio when the score multiplier goes up
        Firework,   // pop and crackle of a level-up firework
        SlowMo      // falling "whoosh" when slow-mo starts
    }

    /// <summary>
    /// Sound effects for whac-a-mole, generated in code the first time they're needed, so the game
    /// needs no audio files. Played in 3D at a world position (the headset's AudioListener hears them).
    /// To use real recordings instead, assign clips with Override(sound, clip).
    /// </summary>
    public static class WhacSounds
    {
        /// <summary>Master volume for all whac-a-mole sounds (0-1). Set from GameSettings.</summary>
        public static float Volume = 0.8f;

        private const int Rate = 44100;
        private static readonly Dictionary<WhacSound, AudioClip> clips = new Dictionary<WhacSound, AudioClip>();
        private static System.Random noise = new System.Random(1234);

        /// <summary>Plays a sound at a world position. volume is multiplied by the master Volume.</summary>
        public static void Play(WhacSound sound, Vector3 position, float volume = 1f)
        {
            if (Volume <= 0f || volume <= 0f) return;
            AudioClip clip = Get(sound);
            if (clip == null) return;

            var go = new GameObject("WhacSound_" + sound);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(Volume * volume);
            source.spatialBlend = 0.85f;         // mostly 3D, but never too quiet to hear
            source.minDistance = 1.5f;
            source.maxDistance = 30f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }

        /// <summary>Use a real audio clip for a sound instead of the generated one.</summary>
        public static void Override(WhacSound sound, AudioClip clip)
        {
            if (clip != null) clips[sound] = clip;
        }

        private static AudioClip Get(WhacSound sound)
        {
            if (clips.TryGetValue(sound, out AudioClip clip) && clip != null) return clip;

            float[] data;
            switch (sound)
            {
                case WhacSound.Hit: data = MakeHit(); break;
                case WhacSound.Explosion: data = MakeExplosion(); break;
                case WhacSound.PowerUp: data = MakeChime(new[] { 1046.5f, 1318.5f, 1568f, 2093f }, 0.065f, 0.35f); break;
                case WhacSound.PowerDown: data = MakeChime(new[] { 880f, 587.3f }, 0.11f, 0.18f); break;
                case WhacSound.LevelUp: data = MakeChime(new[] { 784f, 1046.5f, 1318.5f, 1568f }, 0.1f, 0.45f); break;
                case WhacSound.Miss: data = MakeMiss(); break;
                case WhacSound.Combo: data = MakeChime(new[] { 1318.5f, 1568f, 2093f, 2637f }, 0.045f, 0.3f); break;
                case WhacSound.Firework: data = MakeFirework(); break;
                case WhacSound.SlowMo: data = MakeSlowMo(); break;
                default: return null;
            }

            Normalize(data, 0.9f);
            clip = AudioClip.Create("Whac_" + sound, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            clips[sound] = clip;
            return clip;
        }

        // ---------- Synthesis ----------

        private static float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);

        // Wooden knock: a quickly falling pitch with a sharp decay, plus a tiny click.
        private static float[] MakeHit()
        {
            int n = (int)(Rate * 0.14f);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float freq = Mathf.Lerp(620f, 280f, Mathf.Clamp01(t / 0.05f));
                phase += 2f * Mathf.PI * freq / Rate;
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * 38f);
                float click = Noise() * Mathf.Exp(-t * 400f) * 0.5f;
                d[i] = body + click;
            }
            return d;
        }

        // Explosion: a bright crack, a low rumbling noise that darkens as it fades, and a deep thump.
        private static float[] MakeExplosion()
        {
            int n = (int)(Rate * 1.1f);
            var d = new float[n];
            float lp = 0f, lp2 = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;

                // Low-passed noise whose cutoff drops over time (rumble).
                float cutoff = Mathf.Lerp(0.35f, 0.02f, Mathf.Clamp01(t / 0.8f));
                lp += (Noise() - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                float rumble = lp2 * 3.5f * Mathf.Exp(-t * 3.2f);

                // Initial crack.
                float crack = Noise() * Mathf.Exp(-t * 60f) * 0.8f;

                // Deep thump sliding from 70 Hz down to 35 Hz.
                float freq = Mathf.Lerp(70f, 35f, Mathf.Clamp01(t / 0.4f));
                phase += 2f * Mathf.PI * freq / Rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 5f) * 0.9f;

                d[i] = rumble + crack + thump;
            }
            return d;
        }

        // Soft low "bwomp": a sine sliding down from 220 Hz to 90 Hz with a gentle fade.
        private static float[] MakeMiss()
        {
            int n = (int)(Rate * 0.35f);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float freq = Mathf.Lerp(220f, 90f, Mathf.Clamp01(t / 0.25f));
                phase += 2f * Mathf.PI * freq / Rate;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 9f);
                d[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(2f * phase)) * env;
            }
            return d;
        }

        // Firework: a sharp pop, then a sprinkle of random crackles that fade out.
        private static float[] MakeFirework()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise() - lp) * 0.3f;
                float pop = lp * 2.5f * Mathf.Exp(-t * 30f);
                float crackle = 0f;
                if (t > 0.08f && noise.NextDouble() < 0.004 * Mathf.Exp(-(t - 0.08f) * 3f))
                    crackle = Noise() * 0.9f;
                d[i] = pop + crackle;
            }
            // Smear each crackle click over a few samples so it's audible.
            for (int i = n - 1; i >= 4; i--)
                d[i] += 0.6f * d[i - 2] + 0.3f * d[i - 4];
            return d;
        }

        // Slow-mo "whoosh": airy noise plus a tone sliding down, like time winding down.
        private static float[] MakeSlowMo()
        {
            int n = (int)(Rate * 0.7f);
            var d = new float[n];
            float lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = t / 0.7f;
                float cutoff = Mathf.Lerp(0.25f, 0.03f, k);
                lp += (Noise() - lp) * cutoff;
                float freq = Mathf.Lerp(700f, 140f, k * k);
                phase += 2f * Mathf.PI * freq / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)); // swell in and out
                d[i] = (lp * 1.5f + 0.5f * Mathf.Sin(phase)) * env;
            }
            return d;
        }

        // A run of bell-like notes; the last one rings out longer.
        private static float[] MakeChime(float[] notes, float step, float tail)
        {
            float total = step * (notes.Length - 1) + tail;
            int n = (int)(Rate * total);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(Rate * step * k);
                float ring = k == notes.Length - 1 ? tail : step * 2.5f;
                int len = Mathf.Min(n - start, (int)(Rate * ring));
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 4f / ring);
                    float w = 2f * Mathf.PI * notes[k] * t;
                    // Fundamental plus a soft octave for a bell-ish tone.
                    d[start + i] += (Mathf.Sin(w) + 0.3f * Mathf.Sin(2f * w)) * env * 0.6f;
                }
            }
            return d;
        }

        private static void Normalize(float[] d, float peak)
        {
            float max = 0f;
            foreach (float v in d) max = Mathf.Max(max, Mathf.Abs(v));
            if (max <= 1e-5f) return;
            float g = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
        }
    }
}
