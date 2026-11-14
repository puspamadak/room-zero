// Diegetic panels: main panel (onboarding, briefing, results, pause), mission HUD, debug log.
// All buttons are PokeTargets, so every action works by hand, mouse, or voice.
// Owner: #3 Interaction + VR UX.

using System;
using System.Collections.Generic;
using UnityEngine;
using RoomZero.Input;

namespace RoomZero.UI
{
    public struct UiAction
    {
        public string id, label;
        public Color color;
        public Action onPress;
        public UiAction(string id, string label, Color color, Action onPress)
        { this.id = id; this.label = label; this.color = color; this.onPress = onPress; }
    }

    public class UiPanel : MonoBehaviour
    {
        /// Desktop: panels are drawn by DesktopOverlay as crisp 2D UI instead of 3D text.
        public static bool ScreenSpace;

        public const float Width = 0.48f;
        public string TitleText { get; private set; } = "";
        public string BodyText { get; private set; } = "";
        public UiAction[] Actions { get; private set; } = new UiAction[0];
        public bool Visible => gameObject.activeSelf;

        TextMesh title, body;
        Transform buttonRow;
        LazyFollow follow;
        readonly List<GameObject> buttons = new List<GameObject>();

        public static UiPanel Create(string name, Transform head, float distance, float vOffset, float hOffset = 0f, float height = 0.34f)
        {
            var root = new GameObject(name);
            var p = root.AddComponent<UiPanel>();
            if (ScreenSpace) return p;
            p.follow = root.AddComponent<LazyFollow>();
            p.follow.head = head;
            p.follow.distance = distance;
            p.follow.verticalOffset = vOffset;
            p.follow.horizontalOffset = hOffset;

            UiFactory.Primitive(PrimitiveType.Cube, "Background", root.transform, new Vector3(0, 0, 0.006f), new Vector3(Width, height, 0.004f), Palette.Panel);
            UiFactory.Primitive(PrimitiveType.Cube, "Edge", root.transform, new Vector3(0, height / 2f, 0.004f), new Vector3(Width, 0.004f, 0.004f), Palette.Holo);
            p.title = UiFactory.Text(root.transform, "", new Vector3(-Width / 2 + 0.02f, height / 2 - 0.027f, -0.002f), 0.026f, Palette.Holo, TextAnchor.MiddleLeft);
            p.body = UiFactory.Text(root.transform, "", new Vector3(-Width / 2 + 0.02f, height / 2 - 0.055f, -0.002f), 0.016f, Color.white, TextAnchor.UpperLeft);
            p.buttonRow = new GameObject("Buttons").transform;
            p.buttonRow.SetParent(root.transform, false);
            p.buttonRow.localPosition = new Vector3(0, -height / 2 + 0.035f, -0.004f);
            return p;
        }

        public void Show(string titleText, string bodyText, params UiAction[] actions)
        {
            TitleText = titleText ?? "";
            BodyText = bodyText ?? "";
            Actions = actions ?? new UiAction[0];
            gameObject.SetActive(true);
            if (ScreenSpace) return;
            title.text = titleText;
            body.text = UiFactory.Wrap(bodyText, 50);
            foreach (var b in buttons) Destroy(b);
            buttons.Clear();

            int n = actions.Length;
            float w = n == 0 ? 0 : Mathf.Min(0.16f, (Width - 0.03f) / n - 0.01f);
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * (w + 0.012f);
                var a = actions[i];
                var poke = UiFactory.Button(buttonRow, a.id, a.label, new Vector3(x, 0, 0), new Vector2(w, 0.05f), a.color, a.onPress);
                poke.ignoreInputBlock = a.id.StartsWith("sys_");
                buttons.Add(poke.gameObject);
            }
            follow.SnapNow();
        }

        public void SetBody(string text)
        {
            BodyText = text ?? "";
            if (body) body.text = UiFactory.Wrap(text, 50);
        }

        public void Hide() => gameObject.SetActive(false);
    }

    public class MissionHud : MonoBehaviour
    {
        // Plain values, read by DesktopOverlay in screen-space mode.
        public string StatusText { get; private set; } = "";
        public bool LowTime { get; private set; }
        public float Alert { get; private set; }
        public string SubtitleText { get; private set; } = "";
        public string GuideText { get; private set; } = "";
        public Action OnHint { get; private set; }
        public Action OnPause { get; private set; }
        public bool Visible => gameObject.activeSelf;

        TextMesh status, subtitle, guide;
        Transform alertFill;
        LazyFollow follow;

        public static MissionHud Create(Transform head, Action onHint, Action onPause)
        {
            var root = new GameObject("MissionHud");
            var h = root.AddComponent<MissionHud>();
            h.OnHint = onHint;
            h.OnPause = onPause;
            if (UiPanel.ScreenSpace) return h;
            h.follow = root.AddComponent<LazyFollow>();
            h.follow.head = head;
            h.follow.distance = 0.55f;
            h.follow.verticalOffset = -0.18f;
            h.follow.maxAngle = 35f;

            // Layout (top to bottom): NEXT STEP guide, timer/objectives, ZERO/guard subtitle, alert + buttons.
            UiFactory.Primitive(PrimitiveType.Cube, "Background", root.transform, new Vector3(0, 0, 0.006f), new Vector3(0.46f, 0.18f, 0.004f), Palette.Panel);
            UiFactory.Primitive(PrimitiveType.Cube, "Edge", root.transform, new Vector3(0, 0.09f, 0.004f), new Vector3(0.46f, 0.004f, 0.004f), Palette.Accent);
            h.guide = UiFactory.Text(root.transform, "", new Vector3(-0.215f, 0.078f, -0.002f), 0.018f, Palette.Accent, TextAnchor.UpperLeft);
            h.status = UiFactory.Text(root.transform, "", new Vector3(-0.215f, 0.012f, -0.002f), 0.016f, Palette.Holo, TextAnchor.MiddleLeft);
            h.subtitle = UiFactory.Text(root.transform, "", new Vector3(-0.215f, -0.018f, -0.002f), 0.013f, Color.white, TextAnchor.MiddleLeft);

            UiFactory.Text(root.transform, "ALERT", new Vector3(-0.215f, -0.06f, -0.002f), 0.012f, Palette.Muted, TextAnchor.MiddleLeft);
            UiFactory.Primitive(PrimitiveType.Cube, "AlertBack", root.transform, new Vector3(-0.1f, -0.06f, 0.002f), new Vector3(0.12f, 0.014f, 0.002f), new Color(0.15f, 0.15f, 0.2f));
            var fillPivot = new GameObject("AlertPivot").transform;
            fillPivot.SetParent(root.transform, false);
            fillPivot.localPosition = new Vector3(-0.16f, -0.06f, 0f);
            UiFactory.Primitive(PrimitiveType.Cube, "AlertFill", fillPivot, new Vector3(0.06f, 0, 0), new Vector3(0.12f, 0.014f, 0.003f), Palette.Danger);
            h.alertFill = fillPivot;

            var hint = UiFactory.Button(root.transform, "hud_hint", "HINT", new Vector3(0.09f, -0.06f, -0.004f), new Vector2(0.08f, 0.04f), Palette.Button, onHint);
            var pause = UiFactory.Button(root.transform, "sys_pause", "PAUSE", new Vector3(0.18f, -0.06f, -0.004f), new Vector2(0.08f, 0.04f), Palette.Button, onPause);
            pause.ignoreInputBlock = true;
            hint.cooldownSeconds = 1f;
            return h;
        }

        /// Free text in the status row (e.g. "ROOM 1 · BIO-SECURE ENTRY" before a mission starts).
        public void SetStatusText(string text)
        {
            StatusText = text ?? "";
            LowTime = false;
            Alert = 0f;
            if (!status) return;
            status.text = StatusText;
            status.color = Palette.Holo;
            alertFill.localScale = new Vector3(0, 1, 1);
        }

        public void SetStatus(float secondsLeft, int done, int total, float alertness)
        {
            int sec = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            StatusText = $"{sec / 60}:{sec % 60:00}   OBJECTIVES {done}/{total}";
            LowTime = secondsLeft < 30;
            Alert = Mathf.Clamp01(alertness);
            if (!status) return;
            status.text = StatusText;
            status.color = LowTime ? Palette.Danger : Palette.Holo;
            alertFill.localScale = new Vector3(Alert, 1, 1);
        }

        public void Say(string speaker, string line)
        {
            // Spoken too (the drone voices itself from its own body, so not here).
            if (speaker != "DRONE") RoomZero.Audio.Voice.Say(RoomZero.Audio.Voice.SpeakerId(speaker), line);
            SubtitleText = $"{speaker}: {line}";
            if (subtitle) subtitle.text = UiFactory.Wrap(SubtitleText, 62);
        }

        /// The always-visible "what do I do now" line.
        public void SetGuide(string text)
        {
            GuideText = text ?? "";
            if (!guide) return;
            string wrapped = UiFactory.Wrap("NEXT: " + text, 44);
            if (guide.text != wrapped) guide.text = wrapped;
        }
        public void Show() { gameObject.SetActive(true); if (follow) follow.SnapNow(); }
        public void Hide() => gameObject.SetActive(false);
    }

    /// In-headset log of the last ZeroLog lines (session/state/agent/duration/fallback).
    public class DebugPanel : MonoBehaviour
    {
        public static DebugPanel Instance { get; private set; }
        TextMesh text;
        float next;

        public static void Toggle()
        {
            if (Instance) Instance.gameObject.SetActive(!Instance.gameObject.activeSelf);
        }

        public static DebugPanel Create(Transform head)
        {
            var root = new GameObject("DebugPanel");
            var d = root.AddComponent<DebugPanel>();
            Instance = d;
            var f = root.AddComponent<LazyFollow>();
            f.head = head; f.distance = 0.8f; f.verticalOffset = 0.05f; f.horizontalOffset = -0.55f; f.maxAngle = 60f;
            UiFactory.Primitive(PrimitiveType.Cube, "Background", root.transform, new Vector3(0, 0, 0.006f), new Vector3(0.5f, 0.3f, 0.004f), new Color(0, 0, 0, 1));
            d.text = UiFactory.Text(root.transform, "", new Vector3(-0.24f, 0.14f, -0.002f), 0.009f, Palette.Success, TextAnchor.UpperLeft);
            f.SnapNow();
            return d;
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            var sb = new System.Text.StringBuilder("ZERO TRACE\n");
            foreach (var l in ZeroLog.Recent) sb.AppendLine(l.Length > 95 ? l.Substring(0, 95) + "…" : l);
            text.text = sb.ToString();
        }
    }
}
