// The guard: a patrol drone hologram with an alertness meter (0..1).
// Rules are deterministic: failed attempts raise alertness, time lowers it, 1.0 = caught.
// The NPC agent only supplies what the drone SAYS when thresholds are crossed.
// Owner: #6 Game Systems.

using System;
using UnityEngine;
using RoomZero.UI;

namespace RoomZero.Game
{
    public class GuardDrone : MonoBehaviour
    {
        public float Alertness { get; private set; }
        public float gainPerFailure = 0.2f;
        public float decayPerSecond = 0.008f;

        /// Fired with an NPC beat id when a threshold is crossed.
        public event Action<string> BeatTriggered;
        public event Action Caught;

        Transform body, eye;
        TextMesh subtitle;
        float subtitleUntil;
        int band; // 0 calm, 1 suspicious, 2 alert
        bool caught;

        public static GuardDrone Create(Vector3 position, Vector3 lookAt, float startAlertness)
        {
            var go = new GameObject("GuardDrone");
            var g = go.AddComponent<GuardDrone>();
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(position - lookAt, Vector3.up);

            g.body = UiFactory.Primitive(PrimitiveType.Sphere, "Body", go.transform, Vector3.zero, Vector3.one * 0.12f, Palette.Muted, lit: true).transform;
            UiFactory.Primitive(PrimitiveType.Cylinder, "Ring", g.body, Vector3.zero, new Vector3(1.6f, 0.04f, 1.6f), Palette.Holo);
            g.eye = UiFactory.Primitive(PrimitiveType.Sphere, "Eye", g.body, new Vector3(0, 0, -0.42f), Vector3.one * 0.35f, Palette.Success).transform;
            UiFactory.ApplyModel(UiFactory.Config ? UiFactory.Config.droneModel : null, g.body.gameObject, 0.22f);
            g.subtitle = UiFactory.BackedText(go.transform, "", new Vector3(0, 0.14f, 0), 0.014f, Color.white);
            g.Alertness = Mathf.Clamp01(startAlertness);
            g.band = g.BandFor(g.Alertness);
            g.Refresh();
            return g;
        }

        public void Raise(float amount) => Set(Alertness + amount);
        public void Lower(float amount) => Set(Alertness - amount);

        public void Say(string line, float seconds = 4f)
        {
            subtitle.text = UiFactory.Wrap(line, 34);
            RoomZero.Audio.Voice.Say("drone", line);
            subtitleUntil = Time.time + seconds;
        }

        public void Tick(float dt)
        {
            if (caught) return;
            if (decayPerSecond > 0) Set(Alertness - decayPerSecond * dt);
        }

        void Set(float value)
        {
            if (caught) return;
            Alertness = Mathf.Clamp01(value);
            int b = BandFor(Alertness);
            if (b != band)
            {
                if (b == 2) { RoomZero.Audio.Sfx.Play("alarm"); BeatTriggered?.Invoke("guard_alert"); }
                else if (b == 1 && band == 0) BeatTriggered?.Invoke("guard_suspicious");
                else if (b == 0) BeatTriggered?.Invoke("guard_calm");
                band = b;
            }
            Refresh();
            if (Alertness >= 1f) { caught = true; Caught?.Invoke(); }
        }

        int BandFor(float a) => a >= 0.75f ? 2 : a >= 0.4f ? 1 : 0;

        void Refresh()
        {
            var c = Color.Lerp(Palette.Success, Palette.Danger, Alertness);
            eye.GetComponent<Renderer>().sharedMaterial = UiFactory.Mat(new Color(Mathf.Round(c.r * 10) / 10, Mathf.Round(c.g * 10) / 10, Mathf.Round(c.b * 10) / 10));
        }

        void Update()
        {
            float t = Time.time;
            body.localPosition = new Vector3(Mathf.Sin(t * 0.7f) * 0.05f, Mathf.Sin(t * 1.6f) * 0.015f, 0);
            body.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.5f) * (20f + 40f * Alertness), 0);
            if (subtitle.text.Length > 0 && Time.time > subtitleUntil) subtitle.text = "";
        }
    }
}
