// Procedural sound effects: no audio files needed. Clips are synthesised once and cached.
// Owner: #3 Interaction + VR UX (replace with real recorded sounds when the team has them).

using System.Collections.Generic;
using UnityEngine;

namespace RoomZero.Audio
{
    public static class Sfx
    {
        public static bool Enabled = true;
        public static float MasterVolume = 0.6f;

        static AudioSource source;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        const int Rate = 44100;

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (!Enabled) return;
            if (!source)
            {
                var go = new GameObject("RZ_Audio");
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            if (!clips.TryGetValue(name, out var clip) || !clip) clips[name] = clip = Make(name);
            if (!clip) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * MasterVolume);
        }

        static AudioClip Make(string name)
        {
            switch (name)
            {
                case "click":   return Notes(name, new[] { 1500f }, 0.035f, 0.5f, Wave.Sine);
                case "scan":    return Notes(name, new[] { 820f }, 0.05f, 0.25f, Wave.Sine);
                case "chime":   return Notes(name, new[] { 1046f, 1568f }, 0.16f, 0.5f, Wave.Bell);
                case "success": return Notes(name, new[] { 659f, 880f, 1318f }, 0.09f, 0.5f, Wave.Bell);
                case "error":   return Notes(name, new[] { 196f, 165f }, 0.14f, 0.8f, Wave.Square);
                case "alarm":   return Notes(name, new[] { 740f, 523f, 740f, 523f }, 0.13f, 0.7f, Wave.Square);
                case "fanfare": return Notes(name, new[] { 523f, 659f, 784f, 1046f, 1046f }, 0.12f, 0.5f, Wave.Bell);
                case "grow":    return Sweep(name, 300f, 900f, 0.6f, 0.35f);
                default: return null;
            }
        }

        enum Wave { Sine, Square, Bell }

        static AudioClip Notes(string name, float[] freqs, float noteSeconds, float amp, Wave wave)
        {
            int perNote = Mathf.RoundToInt(noteSeconds * Rate);
            int tail = wave == Wave.Bell ? Mathf.RoundToInt(0.25f * Rate) : 0;
            var data = new float[perNote * freqs.Length + tail];
            for (int n = 0; n < freqs.Length; n++)
            {
                int len = perNote + (n == freqs.Length - 1 ? tail : 0);
                for (int i = 0; i < len && n * perNote + i < data.Length; i++)
                {
                    float t = (float)i / Rate;
                    float env = Mathf.Min(1f, i / (0.004f * Rate)) * Mathf.Exp(-t * (wave == Wave.Bell ? 6f : 14f));
                    float ph = 2f * Mathf.PI * freqs[n] * t;
                    float v = wave == Wave.Square ? Mathf.Sign(Mathf.Sin(ph)) * 0.5f
                            : wave == Wave.Bell ? Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 2.01f)
                            : Mathf.Sin(ph);
                    data[n * perNote + i] += v * env * amp;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Sweep(string name, float from, float to, float seconds, float amp)
        {
            int len = Mathf.RoundToInt(seconds * Rate);
            var data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float k = (float)i / len;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, k) / Rate;
                data[i] = Mathf.Sin(phase) * amp * Mathf.Sin(Mathf.PI * k);
            }
            var clip = AudioClip.Create(name, len, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
