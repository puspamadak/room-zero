// Desktop-only, screen-space UI: draws the main panel and mission HUD as crisp 2D text at the
// monitor's native resolution (3D text always looks soft on a flat screen). In the headset the
// 3D panels are used instead. Uses IMGUI, so it needs no extra packages on any Unity 6 version.
// Owner: #3 Interaction + VR UX.

using System.Collections.Generic;
using UnityEngine;
using RoomZero.Input;

namespace RoomZero.UI
{
    public class DesktopOverlay : MonoBehaviour
    {
        public UiPanel panel;
        public MissionHud hud;

        static readonly List<Rect> blocking = new List<Rect>();

        // Celebration banner
        static string celebrateTitle, celebrateSub;
        static float celebrateStart = -100f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { celebrateStart = -100f; blocking.Clear(); }
        GUIStyle bannerStyle, bannerSub;

        public const float CelebrationSeconds = 3.6f;

        /// Big animated "CONGRATULATIONS!" banner, shown on its own (results appear after it).
        public static void Celebrate(string title, string subtitle)
        {
            celebrateTitle = title;
            celebrateSub = subtitle;
            celebrateStart = Time.unscaledTime;
        }

        public static void ClearCelebration() { celebrateStart = -100f; }

        GUIStyle title, body, button, status, guide, subtitle, small, box, boxAccent, boxLine, boxGreen, cardLine;
        Texture2D texPanel, texAccent, texBarBack, texBar, texLine;
        float builtFor = -1f;

        /// True when the mouse is over overlay UI, so a click doesn't also "touch" the 3D scene.
        public static bool PointerOverUI(Vector2 mouseScreen)
        {
            var p = new Vector2(mouseScreen.x, Screen.height - mouseScreen.y);
            foreach (var r in blocking) if (r.Contains(p)) return true;
            return false;
        }

        float Scale => Mathf.Clamp(Screen.height / 900f, 0.6f, 3f);
        int Px(float v) => Mathf.RoundToInt(v * Scale);

        void BuildStyles()
        {
            if (Mathf.Approximately(builtFor, Scale) && title != null) return;
            builtFor = Scale;
            texPanel = Tex(new Color(0.03f, 0.11f, 0.10f, 0.93f));
            texAccent = Tex(Palette.Holo);
            texBarBack = Tex(new Color(0.18f, 0.18f, 0.24f, 1f));
            texBar = Tex(Palette.Danger);
            texLine = Tex(Palette.Accent);

            box = new GUIStyle { normal = { background = texPanel } };
            boxAccent = new GUIStyle { normal = { background = texAccent } };
            boxLine = new GUIStyle { normal = { background = texLine } };
            boxGreen = new GUIStyle { normal = { background = Tex(Palette.Success) } };

            title = Label(Px(30), Palette.Holo, true);
            body = Label(Px(19), new Color(0.92f, 0.95f, 1f), false);
            status = Label(Px(20), Palette.Holo, true);
            guide = Label(Px(23), Palette.Accent, true);
            subtitle = Label(Px(18), Color.white, false);
            small = Label(Px(15), Palette.Muted, true);
            cardLine = Label(Px(18), Color.white, true);
            cardLine.wordWrap = false;

            button = new GUIStyle(GUI.skin.button) { fontSize = Px(19), fontStyle = FontStyle.Bold };
            button.normal.textColor = Color.white;
            button.hover.textColor = Palette.Accent;
        }

        GUIStyle Label(int size, Color c, bool bold)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, richText = true };
            s.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            s.normal.textColor = c;
            return s;
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.Layout) blocking.Clear();
            BuildStyles();
            if (hud && hud.Visible) DrawHud();
            if (panel && panel.Visible) DrawPanel();
            else DrawCelebration();
            DrawAgentTrace();   // the banner never sits on top of a panel
        }

        void DrawCelebration()
        {
            float t = Time.unscaledTime - celebrateStart;
            if (t > CelebrationSeconds || string.IsNullOrEmpty(celebrateTitle)) return;
            if (bannerStyle == null || bannerStyle.fontSize != Px(64))
            {
                bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = Px(64), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                bannerSub = new GUIStyle(GUI.skin.label) { fontSize = Px(24), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            float alpha = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((CelebrationSeconds - t) * 2f);
            float pop = 1f + 0.12f * Mathf.Sin(Mathf.Clamp01(t * 2f) * Mathf.PI);   // quick bounce in
            float hue = (t * 0.25f) % 1f;
            var c = Color.HSVToRGB(hue, 0.55f, 1f); c.a = alpha;
            bannerStyle.normal.textColor = c;
            var sub = Palette.Success; sub.a = alpha;
            bannerSub.normal.textColor = sub;

            float w = Screen.width, h = Px(96) * pop;
            var shadow = new Color(0, 0, 0, 0.55f * alpha);
            // Centred on a dark band so it reads over any background; nothing else is on screen now.
            float bandH = Px(200);
            var band = new Rect(0, Screen.height * 0.36f, w, bandH);
            var bandCol = new Color(0.02f, 0.08f, 0.07f, 0.8f * alpha);
            GUI.color = bandCol;
            GUI.DrawTexture(band, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var r = new Rect(0, band.y + Px(24), w, h);
            var old = GUI.color;
            GUI.color = shadow;
            GUI.Label(new Rect(r.x + Px(3), r.y + Px(3), r.width, r.height), celebrateTitle, bannerStyle);
            GUI.color = old;
            GUI.Label(r, celebrateTitle, bannerStyle);
            GUI.Label(new Rect(Px(40), r.yMax, w - Px(80), Px(70)), celebrateSub, bannerSub);
        }

        // ------------------------------------------------------------------ main panel

        void DrawPanel()
        {
            float pad = Px(26), gap = Px(14);
            float w = Mathf.Min(Px(760), Screen.width - Px(40));
            float inner = w - pad * 2;
            float titleH = title.CalcHeight(new GUIContent(panel.TitleText), inner);
            float bodyH = body.CalcHeight(new GUIContent(panel.BodyText), inner);
            var actions = panel.Actions;
            float buttonsH = actions.Length > 0 ? Px(54) : 0;
            float h = pad + titleH + gap + bodyH + (buttonsH > 0 ? gap * 1.6f + buttonsH : 0) + pad;

            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f - Px(30), w, h);
            blocking.Add(r);
            GUI.Box(r, GUIContent.none, box);
            GUI.Box(new Rect(r.x, r.y, r.width, Px(4)), GUIContent.none, boxAccent);

            float y = r.y + pad;
            GUI.Label(new Rect(r.x + pad, y, inner, titleH), panel.TitleText, title);
            y += titleH + gap;
            GUI.Label(new Rect(r.x + pad, y, inner, bodyH), panel.BodyText, body);
            y += bodyH + gap * 1.6f;

            if (actions.Length == 0) return;
            float bw = (inner - gap * (actions.Length - 1)) / actions.Length;
            var copy = (UiAction[])actions.Clone(); // a button may replace the panel's actions
            for (int i = 0; i < copy.Length; i++)
            {
                var a = copy[i];
                var br = new Rect(r.x + pad + i * (bw + gap), y, bw, buttonsH);
                if (GUI.Button(br, a.label, button))
                {
                    bool system = a.id != null && a.id.StartsWith("sys_");
                    if (!PokeTarget.InputBlocked || system) a.onPress?.Invoke();
                }
            }
        }

        // ------------------------------------------------------------------ mission HUD

        // Screen layout (no overlaps):
        //   top-left    : status bar (timer, objectives, alert, HINT/PAUSE inline, NEXT, ZERO line)
        //   right side  : mission card (every number you've found, dial / keypad state)
        //   bottom-left : controls help (DesktopSimulatorInput)
        void DrawHud()
        {
            float pad = Px(16);
            float cardW = Mathf.Min(Px(400), Screen.width * 0.3f);
            float w = Mathf.Min(Px(980), Screen.width - cardW - Px(60));
            float inner = w - pad * 2;
            string guideText = string.IsNullOrEmpty(hud.GuideText) ? "" : "NEXT:  " + hud.GuideText;
            float guideH = guide.CalcHeight(new GUIContent(guideText), inner);
            float subH = string.IsNullOrEmpty(hud.SubtitleText) ? 0 : subtitle.CalcHeight(new GUIContent(hud.SubtitleText), inner);
            float rowH = Px(30);
            float h = pad + rowH + Px(6) + guideH + (subH > 0 ? Px(6) + subH : 0) + pad;

            var r = new Rect(Px(20), Px(14), w, h);
            blocking.Add(r);
            GUI.Box(r, GUIContent.none, box);
            GUI.Box(new Rect(r.x, r.y + r.height - Px(3), r.width, Px(3)), GUIContent.none, boxLine);

            float y = r.y + pad;
            var st = new GUIStyle(status);
            st.normal.textColor = hud.LowTime ? Palette.Danger : Palette.Holo;
            GUI.Label(new Rect(r.x + pad, y, inner * 0.45f, rowH), hud.StatusText, st);

            // Row 1 (inline): timer/objectives ... ALERT [bar]  [HINT] [PAUSE]
            float bw = Px(118), bh = Px(36), gapB = Px(8);
            var pause = new Rect(r.x + r.width - pad - bw, y - Px(3), bw, bh);
            var hint = new Rect(pause.x - gapB - bw, pause.y, bw, bh);
            if (GUI.Button(hint, "HINT (H)", button) && !PokeTarget.InputBlocked) hud.OnHint?.Invoke();
            if (GUI.Button(pause, "PAUSE (P)", button)) hud.OnPause?.Invoke();

            float barW = Px(150), barH = Px(14);
            float barX = hint.x - Px(18) - barW;
            GUI.Label(new Rect(barX - Px(64), y + Px(2), Px(64), rowH), "ALERT", small);
            GUI.DrawTexture(new Rect(barX, y + Px(8), barW, barH), texBarBack);
            GUI.DrawTexture(new Rect(barX, y + Px(8), barW * hud.Alert, barH), texBar);
            y += rowH + Px(6);

            GUI.Label(new Rect(r.x + pad, y, inner, guideH), guideText, guide);
            y += guideH;
            if (subH > 0) GUI.Label(new Rect(r.x + pad, y + Px(6), inner, subH), hud.SubtitleText, subtitle);

            DrawMissionCard(cardW);


        }

        /// Bottom-right: "ZERO'S MIND", the latest agent decisions (Z toggles).
        void DrawAgentTrace()
        {
            var list = RoomZero.Zero.AgentTrace.Entries;
            if (!RoomZero.Zero.AgentTrace.Visible || list.Count == 0) return;
            float w = Mathf.Min(Px(520), Screen.width * 0.38f), pad = Px(12), lineH = Px(22);
            var line = new GUIStyle(cardLine) { fontSize = Px(14), wordWrap = false, clipping = TextClipping.Clip };
            float h = pad + Px(26) + list.Count * lineH + pad;
            var r = new Rect(Screen.width - w - Px(20), Screen.height - h - Px(14), w, h);
            blocking.Add(r);
            GUI.Box(r, GUIContent.none, box);
            GUI.Box(new Rect(r.x, r.y, r.width, Px(3)), GUIContent.none, boxAccent);
            var head = new GUIStyle(title) { fontSize = Px(15) };
            head.normal.textColor = Palette.Holo;
            GUI.Label(new Rect(r.x + pad, r.y + pad - Px(4), w - pad * 2, Px(24)), "ZERO'S MIND  ·  agent decisions  (Z hides)", head);
            float y = r.y + pad + Px(24);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                float age = Time.unscaledTime - e.time;
                var badge = new GUIStyle(line) { fontStyle = FontStyle.Bold };
                badge.normal.textColor = e.ai ? Palette.Accent : Palette.Muted;
                GUI.Label(new Rect(r.x + pad, y, Px(64), lineH), e.ai ? "AI" : "rules", badge);
                var st = new GUIStyle(line);
                st.normal.textColor = age < 3f ? Color.white : new Color(0.8f, 0.88f, 0.86f);
                GUI.Label(new Rect(r.x + pad + Px(52), y, w - pad * 2 - Px(52), lineH), $"<b>{e.agent}</b>  {e.text}", st);
                y += lineH;
            }
        }

        /// Right-hand card: every number the player has found, so nothing in 3D can hide it.
        void DrawMissionCard(float w)
        {
            var rt = Game.GameRuntime.Instance;
            if (!rt || !rt.Running) return;
            var lines = rt.StatusLines();
            if (lines.Count == 0) return;

            float pad = Px(16), lineH = Px(28);
            string header = rt.IsFormulaHunt ? "HARVEST FORMULA" : "MISSION";
            float h = pad + Px(34) + lines.Count * lineH + pad;
            var r = new Rect(Screen.width - w - Px(20), Px(14), w, h);
            blocking.Add(r);
            GUI.Box(r, GUIContent.none, box);
            GUI.Box(new Rect(r.x, r.y, r.width, Px(4)), GUIContent.none, rt.IsFormulaHunt ? boxGreen : boxAccent);

            var head = new GUIStyle(title) { fontSize = Px(20) };
            head.normal.textColor = rt.IsFormulaHunt ? Palette.Success : Palette.Holo;
            GUI.Label(new Rect(r.x + pad, r.y + pad - Px(4), w - pad * 2, Px(32)), header, head);

            float y = r.y + pad + Px(34);
            foreach (var line in lines)
            {
                bool done = line.EndsWith("(done)");
                var st = new GUIStyle(cardLine);
                st.normal.textColor = done ? Palette.Success : Color.white;
                GUI.Label(new Rect(r.x + pad, y, w - pad * 2, lineH), line, st);
                y += lineH;
            }
        }
    }
}
