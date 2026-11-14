// Hand-first interaction primitive. Anything the player can "press" is a PokeTarget.
// Pressed by: a Fingertip collider (real hands / Meta XR Simulator hands), a mouse click
// (desktop sim), or code (voice / accessibility fallback). One code path for all.
// Owner: #3 Interaction + VR UX.

using System;
using System.Collections;
using UnityEngine;

namespace RoomZero.Input
{
    [RequireComponent(typeof(Collider))]
    public class PokeTarget : MonoBehaviour
    {
        /// Set while paused or while hand tracking is lost. Prevents accidental completion.
        public static bool InputBlocked;

        /// Static state survives between Play sessions when Unity's domain reload is off
        /// (Enter Play Mode Settings). Start every session unblocked.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { InputBlocked = false; lastGlobalPress = -10f; }

        public string targetId;
        public bool interactable = true;
        [Tooltip("System buttons (pause menu) keep working while InputBlocked.")]
        public bool ignoreInputBlock;
        public float cooldownSeconds = 0.35f;

        public event Action<InputSource> Pressed;

        float lastPress = -10f;
        // Global debounce: a press that swaps a panel must not instantly press the new
        // button that appears under the same fingertip.
        static float lastGlobalPress = -10f;
        const float GlobalDebounce = 0.3f;
        Vector3 baseScale;
        Renderer rend;
        Color baseColor;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock mpb;

        void Awake()
        {
            baseScale = transform.localScale;
            rend = GetComponent<Renderer>();
            if (rend && rend.sharedMaterial)
            {
                var m = rend.sharedMaterial;
                baseColor = m.HasProperty(ColorId) ? m.GetColor(ColorId) : m.HasProperty(LegacyColorId) ? m.GetColor(LegacyColorId) : Color.white;
            }
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Fingertip>() != null) Press(InputSource.Hand);
        }

        public void Press(InputSource source)
        {
            if (!interactable || !isActiveAndEnabled) return;
            if (InputBlocked && !ignoreInputBlock) return;
            if (Time.unscaledTime - lastPress < cooldownSeconds) return;
            if (Time.unscaledTime - lastGlobalPress < GlobalDebounce) return;
            lastPress = lastGlobalPress = Time.unscaledTime;

            StartCoroutine(Feedback());
            RoomZero.Audio.Sfx.Play("click", 0.6f);
            EventBus.Publish(new HandAction { targetId = targetId, action = "poke", source = source });
            Pressed?.Invoke(source);
        }

        bool hovering;

        /// Desktop mouse-over feedback: grow slightly so it's obvious what a click will hit.
        public void SetHover(bool on)
        {
            if (hovering == on) return;
            hovering = on;
            transform.localScale = on ? baseScale * 1.12f : baseScale;
        }

        public void SetTint(Color c)
        {
            if (!rend) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            mpb.SetColor(LegacyColorId, c);
            rend.SetPropertyBlock(mpb);
            baseColor = c;
        }

        IEnumerator Feedback()
        {
            transform.localScale = baseScale * 0.85f;
            if (rend)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(mpb);
                mpb.SetColor(ColorId, Color.white);
                mpb.SetColor(LegacyColorId, Color.white);
                rend.SetPropertyBlock(mpb);
            }
            yield return new WaitForSecondsRealtime(0.12f);
            transform.localScale = hovering ? baseScale * 1.12f : baseScale;
            if (rend)
            {
                mpb.SetColor(ColorId, baseColor);
                mpb.SetColor(LegacyColorId, baseColor);
                rend.SetPropertyBlock(mpb);
            }
        }
    }

    /// Put this on any fingertip transform. It becomes a small trigger sphere that presses PokeTargets.
    [RequireComponent(typeof(SphereCollider))]
    public class Fingertip : MonoBehaviour
    {
        public float radius = 0.012f;

        void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            var rb = GetComponent<Rigidbody>();
            if (!rb) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void OnEnable() => ScanSources.AddPoint(transform);
        void OnDisable() => ScanSources.RemovePoint(transform);
    }
}
