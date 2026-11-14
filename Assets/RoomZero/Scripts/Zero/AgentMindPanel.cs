// ZERO'S MIND in the headset: a small floating panel (lower-left of your view, lazily following
// your head) with the agents' latest decisions and whether each came from the AI model or the
// local rules. Poke "MIND" to fold it away; Z does the same in the Editor / simulator.
// The desktop build draws the same log in DesktopOverlay instead.
// Owner: #5 Agent / LLM Engineer + #3 VR UX.

using UnityEngine;
using RoomZero.UI;

namespace RoomZero.Zero
{
    public class AgentMindPanel : MonoBehaviour
    {
        const int Rows = 5;
        const float RowH = 0.034f;
        TextMesh header;
        readonly TextMesh[] rows = new TextMesh[Rows];
        Transform body;
        bool folded;
        int lastCount = -1;
        float lastTime = -1f;

        public static AgentMindPanel Create(Transform head)
        {
            var go = new GameObject("AgentMindPanel");
            var p = go.AddComponent<AgentMindPanel>();
            var follow = go.AddComponent<LazyFollow>();
            follow.head = head;
            follow.distance = 0.85f;
            follow.verticalOffset = -0.32f;
            follow.horizontalOffset = -0.42f;
            follow.maxAngle = 40f;
            follow.speed = 2.5f;
            p.Build();
            follow.SnapNow();
            return p;
        }

        void Build()
        {
            header = UiFactory.BackedText(transform, "ZERO'S MIND · agent decisions", new Vector3(0, RowH * 1.2f, 0), 0.022f, Palette.Accent, faceCamera: false);
            body = new GameObject("Rows").transform;
            body.SetParent(transform, false);
            for (int i = 0; i < Rows; i++)
                rows[i] = UiFactory.BackedText(body, "", new Vector3(0, -RowH * i, 0), 0.016f, Color.white, faceCamera: false);
            UiFactory.Button(transform, "sys_mind", "MIND", new Vector3(0.24f, RowH * 1.2f, 0), new Vector2(0.07f, 0.03f),
                Palette.Button, () => { folded = !folded; lastCount = -1; });
        }

        void LateUpdate()
        {
            bool show = AgentTrace.Visible && !folded;
            if (body.gameObject.activeSelf != show) body.gameObject.SetActive(show);
            if (!show) return;

            var e = AgentTrace.Entries;
            float newest = e.Count > 0 ? e[e.Count - 1].time : -1f;
            if (e.Count == lastCount && Mathf.Approximately(newest, lastTime)) return;
            lastCount = e.Count; lastTime = newest;

            int aiCount = 0;
            for (int i = 0; i < e.Count; i++) if (e[i].ai) aiCount++;
            header.text = e.Count == 0 ? "ZERO'S MIND · waiting for the first decision"
                                       : $"ZERO'S MIND · {aiCount}/{e.Count} from AI model";

            // Newest first.
            for (int i = 0; i < Rows; i++)
            {
                int k = e.Count - 1 - i;
                if (k < 0) { rows[i].text = ""; continue; }
                var x = e[k];
                string line = $"{(x.ai ? "AI" : "rules")} · {x.agent}: {x.text}";
                if (line.Length > 64) line = line.Substring(0, 61) + "...";
                rows[i].text = line;
                rows[i].color = x.ai ? Palette.Holo : new Color(0.8f, 0.82f, 0.86f);
            }
        }
    }
}
