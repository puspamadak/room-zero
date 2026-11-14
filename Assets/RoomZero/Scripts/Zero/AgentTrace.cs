// "ZERO's mind": a short, human-readable log of what each agent just decided and why,
// and whether it came from the AI model or the local fallback. Shown on screen (Z toggles)
// so players and judges can SEE the agentic loop: perceive -> plan -> place -> validate ->
// act -> observe -> adapt -> remember.
// Owner: #5 Agent / LLM Engineer.

using System.Collections.Generic;
using UnityEngine;

namespace RoomZero.Zero
{
    public static class AgentTrace
    {
        public struct Entry { public float time; public string agent; public bool ai; public string text; }

        public const int Max = 6;
        public static bool Visible = true;
        static readonly List<Entry> entries = new List<Entry>();
        public static IReadOnlyList<Entry> Entries => entries;

        public static void Log(string agent, bool fromAiModel, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (agent == "Mentor" && text.StartsWith("wait")) return;   // "let them work" every few seconds is noise
            if (text.Length > 110) text = text.Substring(0, 107) + "...";
            entries.Add(new Entry { time = Time.unscaledTime, agent = agent, ai = fromAiModel, text = text });
            while (entries.Count > Max) entries.RemoveAt(0);
        }

        public static void Clear() => entries.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { entries.Clear(); Visible = true; }

        /// One-line summary of any agent's output, for the log.
        public static string Summarize(object value)
        {
            var p = value as MissionPlan;
            if (p != null) return $"planned {p.templateId.Replace('_', ' ').ToLowerInvariant()}, level {p.difficulty}, {p.objectives.Count} challenges, {p.durationSeconds}s";
            var l = value as PlacementList;
            if (l != null)
            {
                var parts = new List<string>();
                foreach (var x in l.placements) parts.Add($"{x.objectiveId}->{(x.anchorId ?? "").Replace("surface-", "")}");
                return "placed " + string.Join(", ", parts);
            }
            var a = value as AdaptationProposal;
            if (a != null) return a.action == "NONE" ? "no change needed" : $"{a.action.Replace('_', ' ').ToLowerInvariant()} ({a.reason})";
            var n = value as NpcLine;
            if (n != null) return $"line: \"{n.text}\"";
            var m = value as MentorDecision;
            if (m != null) return $"{m.action.Replace('_', ' ').ToLowerInvariant()} ({m.reason})";
            return value != null ? value.ToString() : "";
        }
    }
}
