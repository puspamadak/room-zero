// Deterministic local implementations of every brain center.
// These ARE the demo fallback (spec: "local deterministic fallback for every AI-dependent
// action"), and they are good enough to ship the core loop at gate G2 with no network.
// Owner: #5 (with #6 for game rules, #7 for the player model).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RoomZero.Zero
{
    public class LocalDirectorAgent : IDirectorAgent
    {
        public Task<MissionPlan> ProposeMissionAsync(DirectorInput input, CancellationToken ct)
            => Task.FromResult(Propose(input.profile, input.requestedTemplate));

        public static MissionPlan Propose(PlayerProfile profile, string requestedTemplate)
        {
            if (requestedTemplate == nameof(MissionTemplateId.TREASURE_HUNT)) return ProposeTreasureHunt(profile);
            if (requestedTemplate == nameof(MissionTemplateId.GREENHOUSE)) return ProposeGreenhouse(profile);
            if (requestedTemplate == nameof(MissionTemplateId.GENE_LAB)) return ProposeGeneLab(profile);
            return Propose(profile);
        }

        static int Difficulty(PlayerProfile profile) =>
            Mathf.Clamp(profile != null && profile.stats != null ? profile.stats.recentDifficulty : 1, 1, 3);

        /// ROOM 2 (GREENHOUSE): mix the seed's nutrient feed, then balance the grow lights so it sprouts.
        public static MissionPlan ProposeGreenhouse(PlayerProfile profile)
        {
            int difficulty = Difficulty(profile);
            var plan = new MissionPlan
            {
                missionId = $"greenhouse_{DateTime.UtcNow:HHmmss}",
                templateId = nameof(MissionTemplateId.GREENHOUSE),
                difficulty = difficulty,
                durationSeconds = 300 - (difficulty - 1) * 30,
                briefing = "Room 2, the Hydroponics Greenhouse. Mix the Harvest seed's nutrient feed to the exact recipe, " +
                           "then balance light, water and heat so the protein crop grows. Too much of anything scorches it.",
                npcBeatIds = new List<string> { "zero_greenhouse_intro", "guard_intro", "guard_suspicious", "guard_alert", "zero_hint", "zero_praise", "zero_fail" }
            };
            plan.objectives.Add(new ObjectiveSpec { id = "mix", type = nameof(ObjectiveType.NUTRIENT_MIX), steps = 3 });
            plan.objectives.Add(new ObjectiveSpec { id = "grow", type = nameof(ObjectiveType.GROW_LIGHT), steps = 3 });
            return plan;
        }

        /// ROOM 3 (GENE_LAB): find the healthy samples under the microscope, then repair the crop's gene.
        public static MissionPlan ProposeGeneLab(PlayerProfile profile)
        {
            int difficulty = Difficulty(profile);
            var plan = new MissionPlan
            {
                missionId = $"genelab_{DateTime.UtcNow:HHmmss}",
                templateId = nameof(MissionTemplateId.GENE_LAB),
                difficulty = difficulty,
                durationSeconds = 330 - (difficulty - 1) * 30,
                briefing = "Room 3, the Gene Lab. Focus the microscope and tag each sample healthy or diseased. " +
                           "Then splice the missing bases into the crop's gene (A pairs with T, G with C) to make it drought-proof.",
                npcBeatIds = new List<string> { "zero_genelab_intro", "guard_intro", "guard_suspicious", "guard_alert", "zero_hint", "zero_praise", "zero_fail" }
            };
            plan.objectives.Add(new ObjectiveSpec { id = "scope", type = nameof(ObjectiveType.MICROSCOPE_FOCUS), steps = 2 + Mathf.Min(difficulty, 2) });
            plan.objectives.Add(new ObjectiveSpec { id = "splice", type = nameof(ObjectiveType.DNA_SPLICE), steps = 2 + difficulty });
            return plan;
        }

        /// FORMULA HUNT (template TREASURE_HUNT): formula fragments hidden on real furniture form a clue chain;
        /// their numbers unlock the seed vault.
        public static MissionPlan ProposeTreasureHunt(PlayerProfile profile)
        {
            int difficulty = Mathf.Clamp(profile != null && profile.stats != null ? profile.stats.recentDifficulty : 1, 1, 3);
            int relics = difficulty == 1 ? 2 : 3;
            var plan = new MissionPlan
            {
                missionId = $"hunt_{DateTime.UtcNow:HHmmss}",
                templateId = nameof(MissionTemplateId.TREASURE_HUNT),
                difficulty = difficulty,
                durationSeconds = 300 - (difficulty - 1) * 30,
                // Keep under the validator's 280-char briefing limit (a self-test checks this).
                briefing = $"The Harvest Formula grows more food and protein with less water, for children who need it. " +
                           $"It's split into {relics} fragments hidden in your room. Scan your furniture: each fragment gives " +
                           "a number and the next clue. Together they unlock the seed vault.",
                npcBeatIds = new List<string> { "zero_treasure_intro", "guard_intro", "guard_suspicious", "guard_alert", "zero_hint", "zero_praise", "zero_fail" }
            };
            for (int i = 1; i <= relics; i++)
                plan.objectives.Add(new ObjectiveSpec { id = $"relic_{i}", type = nameof(ObjectiveType.HIDDEN_RELIC) });
            plan.objectives.Add(new ObjectiveSpec { id = "safe", type = nameof(ObjectiveType.SAFE_DIAL), steps = relics });
            return plan;
        }

        /// Pure function of the profile -> fully deterministic, also used as the
        /// last-resort template when everything else fails.
        public static MissionPlan Propose(PlayerProfile profile)
        {
            var last = profile != null && profile.HasLastSession ? profile.lastSession : null;
            int difficulty = Mathf.Clamp(profile != null && profile.stats != null ? profile.stats.recentDifficulty : 1, 1, 3);

            // Memory: alternate the template so a returning player gets a different run.
            var template = last != null && last.templateId == nameof(MissionTemplateId.DESK_VAULT)
                ? MissionTemplateId.SERVER_HEIST
                : MissionTemplateId.DESK_VAULT;

            int codeLength = difficulty >= 3 ? 5 : 4;
            if (difficulty == 1) codeLength = 3;

            var plan = new MissionPlan
            {
                missionId = $"heist_{DateTime.UtcNow:HHmmss}",
                templateId = template.ToString(),
                difficulty = difficulty,
                durationSeconds = 240 - (difficulty - 1) * 30,
                objectives = new List<ObjectiveSpec>
                {
                    new ObjectiveSpec { id = "obj_clue", type = nameof(ObjectiveType.FIND_CLUE) },
                    new ObjectiveSpec { id = "obj_code", type = nameof(ObjectiveType.ENTER_CODE), targetId = "obj_clue", steps = codeLength }
                },
                npcBeatIds = new List<string> { "zero_intro", "guard_intro", "guard_suspicious", "guard_alert", "zero_hint", "zero_praise", "zero_fail" }
            };

            // Memory: last choice visibly changes this mission.
            if (last != null && last.choice == nameof(RouteChoice.LOUD_ROUTE))
            {
                plan.objectives.Insert(1, new ObjectiveSpec { id = "obj_sensor", type = nameof(ObjectiveType.DISABLE_SENSOR), steps = 3 });
                plan.npcBeatIds.Add("zero_return_loud");
                plan.npcBeatIds.Add("guard_react_loud");
                plan.briefing = "They heard you last time. A sensor grid now guards the vault. Disable it, find the clue, open the panel.";
            }
            else if (last != null && last.choice == nameof(RouteChoice.SILENT_ROUTE))
            {
                plan.npcBeatIds.Add("zero_return_silent");
                plan.npcBeatIds.Add("guard_react_silent");
                plan.briefing = "Your last run was a ghost run. They moved the vault and changed the code. Do it again.";
            }
            else
            {
                plan.briefing = template == MissionTemplateId.DESK_VAULT
                    ? "A vault is hidden on your desk. Find the data shard, read the code, open the panel."
                    : "A server node is running in your room. Find its access shard and enter the key.";
            }
            return plan;
        }
    }

    public class LocalPuzzleArchitectAgent : IPuzzleArchitectAgent
    {
        readonly RoomZeroConfig cfg;
        public LocalPuzzleArchitectAgent(RoomZeroConfig cfg) { this.cfg = cfg; }

        public Task<PlacementList> PlacePuzzlesAsync(PuzzleInput input, CancellationToken ct)
            => Task.FromResult(Place(input.plan, input.room, cfg));

        public static PlacementList Place(MissionPlan plan, RoomMap room, RoomZeroConfig cfg)
        {
            var usable = room.surfaces
                .Where(s => MissionValidator.AnchorProblem(s, room, cfg) == null)
                .OrderBy(s => Spatial.RoomScout.HorizontalDistance(room.PlayerOrigin, s.Center))
                .ToList();

            var result = new PlacementList();
            if (usable.Count == 0) return result; // validator will reject -> fallback path

            // Keypad / vault get the nearest surface to themselves (they're wide and have buttons).
            // Everything else spreads over the OTHER surfaces, so nothing crowds the front object.
            // Only if the room has a single usable surface do they share it.
            var others = usable.Count > 1 ? usable.Skip(1).ToList() : usable;
            int next = 0;
            foreach (var o in plan.objectives)
            {
                bool front = o.type == nameof(ObjectiveType.ENTER_CODE) || o.type == nameof(ObjectiveType.SAFE_DIAL)
                          || o.type == nameof(ObjectiveType.GROW_LIGHT) || o.type == nameof(ObjectiveType.DNA_SPLICE);
                string anchor = front ? usable[0].id : others[(next++) % others.Count].id;
                result.placements.Add(new Placement { objectiveId = o.id, anchorId = anchor });
            }
            return result;
        }
    }

    public class LocalNpcAgent : INpcAgent
    {
        public Task<NpcLine> GetLineAsync(NpcInput input, CancellationToken ct)
            => Task.FromResult(new NpcLine
            {
                beatId = input.beatId,
                intent = NpcCatalog.DefaultIntent(input.beatId).ToString(),
                text = NpcCatalog.FallbackText(input.beatId)
            });
    }

    /// Dr. Asha's judgement as rules: encourage progress, hint when stuck, show one step when really stuck.
    public class LocalMentorAgent : IMentorAgent
    {
        public Task<MentorDecision> DecideAsync(MentorInput input, CancellationToken ct) => Task.FromResult(Decide(input));

        public static MentorDecision Decide(MentorInput i)
        {
            if (i.justSolvedOne) return new MentorDecision { action = "ENCOURAGE", reason = "player solved the last challenge alone" };
            if (i.secondsStuck >= 40f && i.hintsGiven >= 2) return new MentorDecision { action = "SHOW_ONE_STEP", reason = $"stuck {i.secondsStuck:0}s after {i.hintsGiven} hints" };
            if (i.failedAttempts >= 2) return new MentorDecision { action = "HINT", reason = $"{i.failedAttempts} wrong tries" };
            if (i.secondsStuck >= 20f) return new MentorDecision { action = "HINT", reason = $"no progress for {i.secondsStuck:0}s" };
            if (i.secondsLeft > 0 && i.secondsLeft < 45f && i.objectivesDone < i.objectivesTotal) return new MentorDecision { action = "HINT", reason = "time running out" };
            return new MentorDecision { action = "WAIT", reason = "player is making progress" };
        }

        /// Guardrail for ANY mentor (local or model): known action, and no "show" unless genuinely stuck.
        public static bool IsValid(MentorDecision d, MentorInput i)
        {
            if (d == null || !i.allowedActions.Contains(d.action)) return false;
            if (d.action == "SHOW_ONE_STEP" && i.secondsStuck < 20f) return false;
            return true;
        }
    }

    /// One bounded adjustment at a time. Never touches safety settings.
    public class LocalAdaptationAgent : IAdaptationAgent
    {
        public Task<AdaptationProposal> ProposeAsync(AdaptationInput input, CancellationToken ct)
            => Task.FromResult(Propose(input));

        public static AdaptationProposal Propose(AdaptationInput input)
        {
            var s = input.signals;
            float progress = s.objectivesTotal > 0 ? (float)s.objectivesComplete / s.objectivesTotal : 0f;
            float timeUsed = s.durationSeconds > 0 ? s.elapsedSeconds / s.durationSeconds : 0f;
            Func<AdaptationAction, bool> Applied = a => input.alreadyApplied.Contains(a.ToString());

            if (s.failedAttempts >= 2 && !Applied(AdaptationAction.OFFER_HINT))
                return P(AdaptationAction.OFFER_HINT, 0, $"{s.failedAttempts} failed attempts");

            if (s.guardAlertness >= 0.7f && !Applied(AdaptationAction.LOWER_GUARD_ALERTNESS))
                return P(AdaptationAction.LOWER_GUARD_ALERTNESS, 25, $"guard alertness {s.guardAlertness:0.00}");

            if (timeUsed > 0.6f && progress < 0.5f && !Applied(AdaptationAction.EXTEND_TIMER))
                return P(AdaptationAction.EXTEND_TIMER, 45, $"time {timeUsed:P0} used, progress {progress:P0}");

            if (timeUsed < 0.3f && progress >= 0.5f && s.failedAttempts == 0 && !Applied(AdaptationAction.RAISE_GUARD_ALERTNESS))
                return P(AdaptationAction.RAISE_GUARD_ALERTNESS, 20, "player is fast; add pressure");

            return P(AdaptationAction.NONE, 0, "no signal");
        }

        static AdaptationProposal P(AdaptationAction a, int amount, string reason) =>
            new AdaptationProposal { action = a.ToString(), amount = amount, reason = reason };
    }

    public class LocalPlayerModel : IPlayerModel
    {
        const int MaxHistory = 10;

        public PlayerProfile Apply(PlayerProfile p, SessionSummary s)
        {
            if (p == null) p = new PlayerProfile();
            p.history.Add(s);
            if (p.history.Count > MaxHistory) p.history.RemoveAt(0);
            p.lastSession = s;

            p.stats.missions++;
            if (s.completed) p.stats.completions++;
            p.stats.completionRate = p.stats.missions > 0 ? (float)p.stats.completions / p.stats.missions : 0f;

            var times = p.history.Where(h => h.completed).Select(h => h.seconds).OrderBy(t => t).ToList();
            p.stats.medianSeconds = times.Count == 0 ? 0f : times[times.Count / 2];
            p.stats.hintRate = p.history.Count == 0 ? 0f : (float)p.history.Count(h => h.hintsUsed > 0) / p.history.Count;

            // Difficulty: +1 after a clean win, -1 after a loss. Clamped.
            int d = s.difficulty;
            if (s.completed && s.hintsUsed == 0 && s.retries <= 1) d++;
            else if (!s.completed) d--;
            p.stats.recentDifficulty = Mathf.Clamp(d, 1, 3);

            // Preference: players who needed hints get observation puzzles, others sequences.
            p.preferences.puzzleStyle = p.stats.hintRate > 0.5f ? "observation" : "sequence";
            return p;
        }
    }
}
