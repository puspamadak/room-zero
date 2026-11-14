// Spoken lines for Dr. Asha, ZERO and the guard drone.
// Every line the game can say was pre-generated as an audio clip (see tools/voice/make_voices.py,
// voices from the Piper LibriTTS model, CC BY 4.0). A clip is found by its speaker + exact text,
// so any line without a clip (e.g. live LLM dialogue) simply stays text-only.
// Owner: #3 audio.

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RoomZero.Audio
{
    /// One voice channel: lines from ZERO, Dr. Asha and the drone queue up and never talk over
    /// each other. Lines that waited too long are dropped (they'd be out of date).
    public static class Voice
    {
        public static float Volume = 1f;
        const float MaxWait = 6f;
        static Dictionary<string, AudioClip> bank;
        static AudioSource source;
        static float playingUntil;
        struct Pending { public string speaker; public AudioClip clip; public float queuedAt; }
        static readonly List<Pending> queue = new List<Pending>();

        // Time.time restarts every Play session; a leftover "speaking until" would make everyone wait.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { queue.Clear(); playingUntil = 0f; source = null; bank = null; }

        /// Stable 32-bit FNV-1a hash of "speaker|text" (the generator script uses the same function).
        public static string Key(string speaker, string text)
        {
            uint h = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(speaker + "|" + text)) { h ^= b; h *= 16777619; }
            return speaker + "_" + h.ToString("x8");
        }

        public static string SpeakerId(string displayName)
        {
            string n = (displayName ?? "").ToUpperInvariant();
            if (n.Contains("ASHA")) return "asha";
            if (n.Contains("DRONE") || n.Contains("GUARD")) return "drone";
            return "zero";
        }

        static void EnsureBank()
        {
            if (bank != null) return;
            bank = new Dictionary<string, AudioClip>();
            var cfg = RoomZero.UI.UiFactory.Config;
            if (cfg && cfg.voiceClips != null)
                foreach (var c in cfg.voiceClips) if (c) bank[c.name] = c;
        }

        /// Queue a line. Returns its length in seconds (0 when there is no recording for it).
        public static float Say(string speaker, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            EnsureBank();
            if (!bank.TryGetValue(Key(speaker, text), out var clip)) return 0f;
            EnsureSource();
            // A newer line from the same speaker replaces their older, not-yet-spoken one.
            queue.RemoveAll(p => p.speaker == speaker);
            queue.Add(new Pending { speaker = speaker, clip = clip, queuedAt = Time.time });
            Pump();
            return clip.length;
        }

        static void EnsureSource()
        {
            if (source) return;
            var go = new GameObject("RZ_Voice");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            go.AddComponent<VoicePump>();
        }

        internal static void Pump()
        {
            if (!source || Time.time < playingUntil) return;
            while (queue.Count > 0)
            {
                var p = queue[0];
                queue.RemoveAt(0);
                if (Time.time - p.queuedAt > MaxWait) continue;   // stale: skip
                source.clip = p.clip;
                source.volume = Volume * (p.speaker == "drone" ? 0.8f : 1f);
                source.Play();
                playingUntil = Time.time + p.clip.length + 0.25f; // small breath between speakers
                return;
            }
        }

        /// True while any line is being spoken or waiting to be spoken.
        public static bool Busy => Time.time < playingUntil || queue.Count > 0;

        /// Seconds until everything queued so far has been said (used to pace Dr. Asha).
        public static float Remaining(string speaker)
        {
            float t = Mathf.Max(0f, playingUntil - Time.time);
            foreach (var p in queue) t += p.clip.length + 0.25f;
            return t;
        }

        class VoicePump : MonoBehaviour { void Update() => Pump(); }
    }

    /// Background music, one loop per room, cross-faded.
    public class Music : MonoBehaviour
    {
        static Music instance;
        AudioSource a, b;
        AudioClip want;
        float targetVolume = 0.22f;

        public static void Play(AudioClip clip, float volume = 0.22f)
        {
            if (!instance)
            {
                var go = new GameObject("RZ_Music");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Music>();
                instance.a = go.AddComponent<AudioSource>();
                instance.b = go.AddComponent<AudioSource>();
                foreach (var s in new[] { instance.a, instance.b }) { s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f; }
            }
            instance.targetVolume = volume;
            if (!clip || clip == instance.want) return;
            instance.want = clip;
            var t = instance.a; instance.a = instance.b; instance.b = t;   // a = incoming, b = outgoing
            instance.a.clip = clip;
            instance.a.volume = 0f;
            instance.a.Play();
        }

        public static void SetVolume(float v) { if (instance) instance.targetVolume = v; }

        void Update()
        {
            float k = Time.unscaledDeltaTime / 1.5f;   // 1.5 s cross-fade
            float duck = Voice.Busy ? 0.3f : 1f;                    // music dips while someone is talking
            a.volume = Mathf.MoveTowards(a.volume, targetVolume * duck, k * 2f);
            b.volume = Mathf.MoveTowards(b.volume, 0f, k);
            if (b.isPlaying && b.volume <= 0f) b.Stop();
        }
    }
}
