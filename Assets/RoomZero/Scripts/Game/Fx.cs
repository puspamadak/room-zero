// Small code-only visual effects (no particle module or assets needed).
// Owner: #6 Game Systems (swap for real VFX later).

using System.Collections.Generic;
using UnityEngine;
using RoomZero.UI;

namespace RoomZero.Game
{
    public static class Fx
    {
        /// Expanding glow ring on the surface: "something unlocked here".
        public static void Burst(Vector3 position, Color color, float size = 0.35f)
        {
            var go = UiFactory.Primitive(PrimitiveType.Cylinder, "FxBurst", null, Vector3.zero, Vector3.zero, color);
            go.transform.position = position + Vector3.up * 0.01f;
            var b = go.AddComponent<BurstRing>();
            b.maxSize = size;
        }
    }

    /// Colourful confetti burst (code-only, no particle module).
    public class Confetti : MonoBehaviour
    {
        static readonly Color[] Colors =
        {
            new Color(1f, 0.3f, 0.75f), new Color(0.2f, 0.88f, 1f), new Color(0.6f, 1f, 0.2f),
            new Color(1f, 0.8f, 0.2f), new Color(0.62f, 0.42f, 1f), new Color(1f, 0.45f, 0.3f)
        };
        readonly List<Transform> bits = new List<Transform>();
        readonly List<Vector3> vel = new List<Vector3>();
        readonly List<Vector3> spin = new List<Vector3>();
        float age;

        public static void Spawn(Vector3 at, int count = 70)
        {
            var go = new GameObject("FxConfetti");
            go.transform.position = at;
            var c = go.AddComponent<Confetti>();
            for (int i = 0; i < count; i++)
            {
                var b = UiFactory.Primitive(PrimitiveType.Cube, "Bit", go.transform, Vector3.zero, new Vector3(0.018f, 0.004f, 0.012f), Colors[i % Colors.Length]).transform;
                c.bits.Add(b);
                c.vel.Add(new Vector3(Random.Range(-1f, 1f), Random.Range(1.2f, 2.6f), Random.Range(-1f, 1f)));
                c.spin.Add(new Vector3(Random.Range(-720f, 720f), Random.Range(-720f, 720f), Random.Range(-720f, 720f)));
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            for (int i = 0; i < bits.Count; i++)
            {
                var v = vel[i];
                v.y -= 3.2f * Time.deltaTime;                 // gentle gravity
                v *= 1f - 0.9f * Time.deltaTime;              // air drag: slow flutter
                vel[i] = v;
                bits[i].position += v * Time.deltaTime;
                bits[i].Rotate(spin[i] * Time.deltaTime);
            }
            if (age > 4f) Destroy(gameObject);
        }
    }

    public class BurstRing : MonoBehaviour
    {
        public float maxSize = 0.35f, duration = 0.6f;
        float t;
        void Update()
        {
            t += Time.deltaTime / duration;
            float s = Mathf.Lerp(0.02f, maxSize, 1f - (1f - t) * (1f - t));
            float thin = Mathf.Lerp(0.004f, 0.0005f, t);
            transform.localScale = new Vector3(s, thin, s);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// Tiny glowing motes drifting upwards around an object.
    public class Sparkles : MonoBehaviour
    {
        public Color color = Palette.Success;
        public int count = 10;
        public float radius = 0.07f, height = 0.16f;
        readonly List<Transform> motes = new List<Transform>();
        readonly List<float> seeds = new List<float>();

        void Start()
        {
            for (int i = 0; i < count; i++)
            {
                var m = UiFactory.Primitive(PrimitiveType.Sphere, "Mote", transform, Vector3.zero, Vector3.one * 0.007f, color).transform;
                motes.Add(m);
                seeds.Add(Random.value * 10f);
            }
        }

        void Update()
        {
            for (int i = 0; i < motes.Count; i++)
            {
                float k = (Time.time * 0.35f + seeds[i]) % 1f;
                float ang = seeds[i] * 6.28f + Time.time * 0.8f;
                motes[i].localPosition = new Vector3(Mathf.Cos(ang) * radius, k * height, Mathf.Sin(ang) * radius);
                motes[i].localScale = Vector3.one * 0.008f * Mathf.Sin(k * Mathf.PI);
            }
        }
    }

    /// Gentle breathing scale for holograms.
    public class HoloPulse : MonoBehaviour
    {
        public float amount = 0.06f, speed = 2.2f;
        Vector3 baseScale;
        void Start() { baseScale = transform.localScale; }
        void Update() { transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * speed) * amount); }
    }

    /// Grow from nothing with a little overshoot (sprouts, revealed items).
    public class GrowIn : MonoBehaviour
    {
        public float duration = 0.8f;
        Vector3 target;
        float t;
        void Awake() { target = transform.localScale; transform.localScale = Vector3.zero; }
        void Update()
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            float overshoot = 1f + Mathf.Sin(k * Mathf.PI) * 0.15f;
            transform.localScale = target * (k * overshoot);
            if (t >= 1f) { transform.localScale = target; Destroy(this); }
        }
    }

    /// Slow constant rotation (holograms, centrifuges).
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 30f;
        public Vector3 axis = Vector3.up;
        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.World);
    }
}
