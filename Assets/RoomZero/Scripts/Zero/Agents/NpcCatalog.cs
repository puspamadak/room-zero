// Enumerated NPC beats + fallback dialogue. The NPC agent may phrase a line differently,
// but it can only speak on these beats, with these intents, in short lines.
// Owner: #6 Game Systems (beats) + #5 (phrasing).

using System.Collections.Generic;

namespace RoomZero.Zero
{
    public static class NpcCatalog
    {
        public const int MaxLineLength = 120;

        public static readonly string[] Beats =
        {
            "zero_intro", "zero_treasure_intro", "zero_greenhouse_intro", "zero_genelab_intro", "zero_return_silent", "zero_return_loud", "zero_hint", "zero_praise", "zero_fail",
            "guard_intro", "guard_suspicious", "guard_alert", "guard_calm", "guard_react_silent", "guard_react_loud"
        };

        static readonly HashSet<string> known = new HashSet<string>(Beats);
        public static bool IsKnownBeat(string id) => id != null && known.Contains(id);

        public static NpcIntent DefaultIntent(string beat)
        {
            switch (beat)
            {
                case "zero_intro": case "zero_treasure_intro": case "zero_greenhouse_intro": case "zero_genelab_intro": case "guard_intro": return NpcIntent.INTRO;
                case "zero_hint": return NpcIntent.HINT;
                case "zero_praise": return NpcIntent.PRAISE;
                case "guard_suspicious": return NpcIntent.SUSPICIOUS;
                case "guard_alert": case "guard_react_loud": return NpcIntent.ALERT;
                case "guard_calm": case "guard_react_silent": return NpcIntent.CALM;
                case "zero_fail": return NpcIntent.TAUNT;
                default: return NpcIntent.IDLE;
            }
        }

        /// Deterministic fallback text. Always safe, always short.
        public static string FallbackText(string beat)
        {
            switch (beat)
            {
                case "zero_intro": return "This room is your vault. Find the clue, crack the panel, stay quiet.";
                case "zero_treasure_intro": return "The Harvest Formula is hidden in your room, in pieces. Recover it and we can feed a lot of kids.";
                case "zero_greenhouse_intro": return "The seed is safe. Now it has to grow. Dr. Asha is heading to the greenhouse.";
                case "zero_genelab_intro": return "Last room. One gene stands between this crop and a drought. Dr. Asha is on her way.";
                case "zero_return_silent": return "Welcome back. They never knew you were here, so they moved the vault.";
                case "zero_return_loud": return "Welcome back. After your alarm, they installed a sensor grid.";
                case "zero_hint": return "Look at the glowing shard. The code is written on it.";
                case "zero_praise": return "Clean work. I'm logging this one.";
                case "zero_fail": return "They spotted you. We regroup and try again.";
                case "guard_intro": return "Sector quiet. Patrol drone online.";
                case "guard_suspicious": return "Hm? Did something move?";
                case "guard_alert": return "Intruder! Locking down!";
                case "guard_calm": return "...Probably nothing.";
                case "guard_react_silent": return "Systems nominal. Nothing to report.";
                case "guard_react_loud": return "Who's making that noise?!";
                default: return "...";
            }
        }
    }
}
