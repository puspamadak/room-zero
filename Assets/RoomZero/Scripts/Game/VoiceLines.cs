// Every line that has a recorded voice. tools/voice/make_voices.py reads this list (via the
// "dump" self-test) and generates one clip per line, so if you add or change a spoken line,
// add it here and re-run the generator. Lines not listed stay text-only (that's fine).
// Owner: #6 narrative / #3 audio.

using System.Collections.Generic;
using RoomZero.Zero;

namespace RoomZero.Game
{
    public static class VoiceLines
    {
        /// What Dr. Asha says when she arrives at a challenge (you play, she guides).
        public static string AshaTurn(ObjectiveType t)
        {
            switch (t)
            {
                case ObjectiveType.HIDDEN_RELIC: return "Your turn. Sweep your hand over the furniture to scan for the hidden fragment.";
                case ObjectiveType.SAFE_DIAL: return "Your turn. Set the vault dial to each fragment number, one digit at a time.";
                case ObjectiveType.FIND_CLUE: return "Your turn. Touch the glowing orange shard to reveal the code.";
                case ObjectiveType.ENTER_CODE: return "Your turn. Type the code from the shard into the keypad.";
                case ObjectiveType.DISABLE_SENSOR: return "Your turn. Watch the sensor lights flash, then touch them in the same order.";
                case ObjectiveType.NUTRIENT_MIX: return "Your turn. Add drops until the beaker matches the recipe, then press mix.";
                case ObjectiveType.GROW_LIGHT: return "Your turn. Push light, water and heat into the green bands. Careful, lamps add heat.";
                case ObjectiveType.MICROSCOPE_FOCUS: return "Your turn. Focus the microscope, then tell me if the cells are healthy or diseased.";
                case ObjectiveType.DNA_SPLICE: return "Your turn. Fill each gap in the gene. A pairs with T, G pairs with C.";
                default: return "Your turn. I'll guide you.";
            }
        }

        public const string LookFirst = "Let me take a look.";
        public const string HintLine = "Here's a hint: look at the device, I've marked the next step.";
        public const string HintLine2 = "Look closely. The answer is right there on the device.";
        public const string ShowStep = "Let me show you just one step.";
        public const string AfterShowStep = "There. Now you try the rest.";
        public static readonly string[] Encourage = { "Brilliant, you got it! On to the next one.", "Nice work. You're a natural.", "Great job. Keep going." };
        public static readonly string[] Thinking = { "Hmm, let me think.", "Okay, I see it.", "Right, next step." };
        public static readonly string[] NutrientNames = { "NITROGEN", "PHOSPHORUS", "POTASSIUM" };

        public static IEnumerable<KeyValuePair<string, string>> All()
        {
            // ---- Dr. Asha ----
            foreach (var t in new[] { MissionTemplateId.TREASURE_HUNT, MissionTemplateId.GREENHOUSE, MissionTemplateId.GENE_LAB, MissionTemplateId.DESK_VAULT })
                yield return A(Campaign.AshaIntro(t));
            yield return A("Good morning! I'm Dr. Asha. Come in, the seed vault is this way.");
            foreach (ObjectiveType t in System.Enum.GetValues(typeof(ObjectiveType))) yield return A(AshaTurn(t));
            yield return A("Your turn! I'll guide you.");
            yield return A("Your turn. I'll guide you.");
            yield return A("Demo mode: watch me solve it. Touch anything to take over.");
            yield return A("We did it! That's one step closer to feeding those kids.");
            yield return A("Not this time. We'll be back.");
            yield return A(HintLine);
            yield return A(HintLine2);
            yield return A(ShowStep);
            yield return A(AfterShowStep);
            foreach (var e in Encourage) yield return A(e);
            yield return A(LookFirst);
            foreach (var s in Thinking) yield return A(s);
            // her moves while demonstrating
            yield return A("The code is on this shard.");
            yield return A("Let me clear that.");
            yield return A("And... enter.");
            for (int d = 0; d <= 9; d++) yield return A($"{d}...");
            yield return A("I memorised the flash order.");
            yield return A("My scanner picked something up here.");
            for (int i = 1; i <= 3; i++) for (int d = 0; d <= 9; d++) yield return A($"Fragment {i}: number {d}.");
            yield return A("That's the last digit. Opening!");
            for (int i = 1; i <= 3; i++) for (int d = 0; d <= 9; d++) yield return A($"Digit {i}: {d}.");
            yield return A("Too much. Starting over.");
            foreach (var n in NutrientNames) for (int k = 1; k <= 4; k++) yield return A($"{n}: {k} drops, done.");
            yield return A("Exact recipe. Mixing!");
            yield return A("Cooling first, so the lamp won't scorch it.");
            foreach (var s in new[] { "More light. That warms it too.", "Dimming the lamp.", "A little water. That cools it.", "Less water.", "Bit warmer.", "Cooling down." }) yield return A(s);
            yield return A("There. In focus.");
            yield return A("Round, green, one nucleus: healthy.");
            yield return A("Purple and spotty: diseased.");
            foreach (var b in new[] { 'A', 'T', 'G', 'C' }) yield return A($"{b} pairs with {DnaSpliceObjective.Pair(b)}.");

            // ---- ZERO ----
            foreach (var beat in NpcCatalog.Beats)
                if (!beat.StartsWith("guard_")) yield return Z(NpcCatalog.FallbackText(beat));
            yield return Z("This is the bio-secure seed lab. Scan your palm and the doors will open.");
            foreach (var s in new[] { "You're circling. I've highlighted what matters.", "I bought you extra time on their clock.", "I looped the camera feed. The drone is calming down.", "You're fast. Let's see you handle a nervous drone." }) yield return Z(s);
            for (int i = 1; i <= 3; i++) for (int d = 0; d <= 9; d++) for (int left = 1; left <= 2; left++)
                yield return Z($"Fragment {i} of the Harvest Formula: number {d}. {left} to go. Follow the clue.");

            // ---- guard drone ----
            foreach (var beat in NpcCatalog.Beats)
                if (beat.StartsWith("guard_")) yield return new KeyValuePair<string, string>("drone", NpcCatalog.FallbackText(beat));
        }

        static KeyValuePair<string, string> A(string t) => new KeyValuePair<string, string>("asha", t);
        static KeyValuePair<string, string> Z(string t) => new KeyValuePair<string, string>("zero", t);
    }
}
