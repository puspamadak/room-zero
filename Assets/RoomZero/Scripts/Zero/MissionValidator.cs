// Schema + spatial validation (spec section 3, step 05). AI proposes; this decides.
// Checks: schema version, required fields, enums, object count, difficulty/duration
// limits, anchor exists & allowed & confident, not in an excluded zone, within reach,
// sane height. Repairs only what is safe to repair (anchor reassignment, missing link);
// everything else is rejected.
// Owner: #4 AI Orchestration (with #6 for catalog limits).

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RoomZero.Spatial;

namespace RoomZero.Zero
{
    public class ValidatedObjective
    {
        public ObjectiveSpec spec;
        public ObjectiveType type;
        public SurfaceInfo anchor;
        public Vector3 position;     // world, where the runtime spawns it
        public Vector3 facing;       // direction the object faces (towards the player)
    }

    public class ValidatedMission
    {
        public MissionPlan plan;
        public MissionTemplateId template;
        public int difficulty;
        public int durationSeconds;
        public List<ValidatedObjective> objectives = new List<ValidatedObjective>();
        public List<string> npcBeatIds = new List<string>();
        public string MissionId => plan.missionId;
        public ValidatedObjective Find(string id) => objectives.Find(o => o.spec.id == id);
    }

    public class ValidationResult
    {
        public bool ok;
        public List<string> errors = new List<string>();
        public List<string> repairs = new List<string>();
        public ValidatedMission mission;
        public override string ToString() =>
            ok ? $"OK ({repairs.Count} repairs: {string.Join("; ", repairs)})" : $"REJECTED: {string.Join("; ", errors)}";
    }

    public static class MissionValidator
    {
        public static ValidationResult Validate(MissionPlan plan, RoomMap room, RoomZeroConfig cfg, bool allowRepair = true)
        {
            var r = new ValidationResult();
            if (plan == null) { r.errors.Add("plan is null"); return r; }
            if (room == null) { r.errors.Add("room is null"); return r; }

            // ---- schema & scalar fields ----
            if (plan.schemaVersion != Schema.Version) r.errors.Add($"schemaVersion {plan.schemaVersion} != {Schema.Version}");
            if (string.IsNullOrWhiteSpace(plan.missionId) || plan.missionId.Length > 64) r.errors.Add("missionId missing/too long");
            if (!EnumParse.TryParse(plan.templateId, out MissionTemplateId template)) r.errors.Add($"unknown templateId '{plan.templateId}'");
            if (plan.difficulty < 1 || plan.difficulty > 3) r.errors.Add($"difficulty {plan.difficulty} outside 1..3");
            if (plan.durationSeconds < cfg.durationRangeSeconds.x || plan.durationSeconds > cfg.durationRangeSeconds.y)
                r.errors.Add($"durationSeconds {plan.durationSeconds} outside {cfg.durationRangeSeconds}");
            if (plan.successCondition != "ALL_OBJECTIVES_COMPLETE") r.errors.Add($"unsupported successCondition '{plan.successCondition}'");
            if (plan.briefing != null && plan.briefing.Length > 280) r.errors.Add("briefing longer than 280 chars");
            if (plan.objectives == null || plan.objectives.Count == 0) r.errors.Add("no objectives");
            else if (plan.objectives.Count > cfg.maxObjectives) r.errors.Add($"{plan.objectives.Count} objectives > max {cfg.maxObjectives}");
            if (r.errors.Count > 0) return r;

            var vm = new ValidatedMission
            {
                plan = plan, template = template, difficulty = plan.difficulty, durationSeconds = plan.durationSeconds
            };

            // ---- NPC beats: catalog only ----
            foreach (var beat in plan.npcBeatIds ?? new List<string>())
            {
                if (NpcCatalog.IsKnownBeat(beat)) vm.npcBeatIds.Add(beat);
                else if (allowRepair) r.repairs.Add($"dropped unknown npc beat '{beat}'");
                else r.errors.Add($"unknown npc beat '{beat}'");
            }

            // ---- objectives ----
            var ids = new HashSet<string>();
            var usage = new Dictionary<string, int>();
            foreach (var o in plan.objectives)
            {
                if (o == null || string.IsNullOrWhiteSpace(o.id)) { r.errors.Add("objective without id"); continue; }
                if (!ids.Add(o.id)) { r.errors.Add($"duplicate objective id '{o.id}'"); continue; }
                if (!EnumParse.TryParse(o.type, out ObjectiveType type)) { r.errors.Add($"{o.id}: unknown type '{o.type}'"); continue; }

                bool lab = type == ObjectiveType.NUTRIENT_MIX || type == ObjectiveType.GROW_LIGHT || type == ObjectiveType.MICROSCOPE_FOCUS || type == ObjectiveType.DNA_SPLICE;
                int minSteps = type == ObjectiveType.SAFE_DIAL ? 1 : lab ? 2 : 3;
                int maxSteps = type == ObjectiveType.NUTRIENT_MIX || type == ObjectiveType.GROW_LIGHT ? 3 : 5;
                bool usesSteps = lab || type == ObjectiveType.ENTER_CODE || type == ObjectiveType.DISABLE_SENSOR || type == ObjectiveType.SAFE_DIAL;
                if (usesSteps && (o.steps < minSteps || o.steps > maxSteps))
                {
                    if (!allowRepair) { r.errors.Add($"{o.id}: steps {o.steps} outside {minSteps}..{maxSteps}"); continue; }
                    r.repairs.Add($"{o.id}: steps {o.steps} clamped");
                    o.steps = Mathf.Clamp(o.steps, minSteps, maxSteps);
                }

                var anchor = room.FindSurface(o.anchorId);
                string why = AnchorProblem(anchor, room, cfg);
                if (why != null)
                {
                    if (!allowRepair) { r.errors.Add($"{o.id}: anchor '{o.anchorId}' {why}"); continue; }
                    var replacement = BestSurface(room, cfg, usage);
                    if (replacement == null) { r.errors.Add($"{o.id}: anchor '{o.anchorId}' {why}, no safe replacement"); continue; }
                    r.repairs.Add($"{o.id}: anchor '{o.anchorId}' {why} -> '{replacement.id}'");
                    o.anchorId = replacement.id;
                    anchor = replacement;
                }
                usage[anchor.id] = usage.TryGetValue(anchor.id, out var n) ? n + 1 : 1;
                vm.objectives.Add(new ValidatedObjective { spec = o, type = type, anchor = anchor });
            }

            // ---- cross-links: every ENTER_CODE needs a FIND_CLUE ----
            foreach (var vo in vm.objectives.Where(v => v.type == ObjectiveType.ENTER_CODE))
            {
                var target = vm.Find(vo.spec.targetId ?? "");
                if (target != null && target.type == ObjectiveType.FIND_CLUE) continue;
                var anyClue = vm.objectives.FirstOrDefault(v => v.type == ObjectiveType.FIND_CLUE);
                if (anyClue != null && allowRepair)
                {
                    r.repairs.Add($"{vo.spec.id}: targetId '{vo.spec.targetId}' -> '{anyClue.spec.id}'");
                    vo.spec.targetId = anyClue.spec.id;
                }
                else r.errors.Add($"{vo.spec.id}: ENTER_CODE has no FIND_CLUE to link to");
            }

            // ---- every SAFE_DIAL needs at least one HIDDEN_RELIC (the relics are its combination) ----
            int relicCount = vm.objectives.Count(v => v.type == ObjectiveType.HIDDEN_RELIC);
            foreach (var safe in vm.objectives.Where(v => v.type == ObjectiveType.SAFE_DIAL))
            {
                if (relicCount == 0) r.errors.Add($"{safe.spec.id}: SAFE_DIAL has no HIDDEN_RELIC to take digits from");
                else if (safe.spec.steps != relicCount)
                {
                    if (allowRepair) { r.repairs.Add($"{safe.spec.id}: steps {safe.spec.steps} -> {relicCount} (relic count)"); safe.spec.steps = relicCount; }
                    else r.errors.Add($"{safe.spec.id}: steps {safe.spec.steps} != relic count {relicCount}");
                }
            }

            if (r.errors.Count > 0) return r;

            LayoutOnSurfaces(vm, room);

            // ---- final reach check on the actual spawn points ----
            foreach (var vo in vm.objectives)
            {
                float d = RoomScout.HorizontalDistance(room.PlayerOrigin, vo.position);
                if (d > cfg.Reach + 0.05f) r.errors.Add($"{vo.spec.id}: spawn point {d:0.00}m away > reach {cfg.Reach}m");
            }

            r.ok = r.errors.Count == 0;
            if (r.ok) r.mission = vm;
            return r;
        }

        /// Returns null if the surface can host an interactive objective, else the reason.
        public static string AnchorProblem(SurfaceInfo s, RoomMap room, RoomZeroConfig cfg)
        {
            if (s == null) return "does not exist";
            if (!s.allowed) return "not allowed by Room Scout";
            if (s.confidence < cfg.minSurfaceConfidence) return $"confidence {s.confidence:0.00} too low";
            if (!s.IsHorizontal) return "not a horizontal surface";
            if (s.Center.y < cfg.minObjectHeight || s.Center.y > cfg.maxObjectHeight) return $"height {s.Center.y:0.00}m out of range";
            if (RoomScout.HorizontalDistance(room.PlayerOrigin, s.Center) > cfg.Reach) return "out of reach";
            foreach (var zid in room.excludedZoneIds)
            {
                var z = room.FindZone(zid);
                if (z != null && z.Bounds.Contains(s.Center)) return $"inside excluded zone '{zid}'";
            }
            return null;
        }

        static SurfaceInfo BestSurface(RoomMap room, RoomZeroConfig cfg, Dictionary<string, int> usage)
        {
            return room.surfaces
                .Where(s => AnchorProblem(s, room, cfg) == null)
                .OrderBy(s => usage.TryGetValue(s.id, out var n) ? n : 0)
                .ThenBy(s => RoomScout.HorizontalDistance(room.PlayerOrigin, s.Center))
                .FirstOrDefault();
        }

        /// Spread objectives that share a surface along the edge nearest the player.
        /// Approximate width (m) each objective needs on a surface, including its labels/buttons.
        public static float Footprint(ValidatedObjective o)
        {
            switch (o.type)
            {
                case ObjectiveType.SAFE_DIAL: return 0.59f;   // 1.4x vault incl. side buttons
                case ObjectiveType.ENTER_CODE: return 0.34f;  // 1.4x keypad
                case ObjectiveType.GROW_LIGHT: return 0.5f;   // plant + three gauges
                case ObjectiveType.DNA_SPLICE: return 0.56f;  // gene strand + base buttons
                case ObjectiveType.NUTRIENT_MIX: return 0.36f;
                case ObjectiveType.MICROSCOPE_FOCUS: return 0.36f;
                case ObjectiveType.DISABLE_SENSOR: return 0.06f * Mathf.Clamp(o.spec.steps, 3, 5) + 0.06f;
                default: return 0.16f;   // clue shard / formula fragment (+ its label)
            }
        }

        static void LayoutOnSurfaces(ValidatedMission vm, RoomMap room)
        {
            Vector3 origin = room.PlayerOrigin;
            foreach (var group in vm.objectives.GroupBy(o => o.anchor.id))
            {
                var list = group.ToList();
                var s = list[0].anchor;
                Vector3 toPlayer = origin - s.Center; toPlayer.y = 0;
                if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector3.back;
                toPlayer.Normalize();
                Vector3 along = Vector3.Cross(Vector3.up, toPlayer);

                // Side by side using each object's real width (vault with its buttons is ~0.42 m).
                float usable = Mathf.Max(0.2f, Mathf.Max(s.Size.x, s.Size.z) - 0.05f);
                const float gap = 0.04f;
                float total = list.Sum(o => Footprint(o)) + gap * (list.Count - 1);
                float squeeze = total > usable ? usable / total : 1f;   // tiny surfaces: compress rather than fail
                float nearEdge = Mathf.Min(s.Size.x, s.Size.z) * 0.25f; // pull towards the player side

                float cursor = -total * squeeze * 0.5f;
                foreach (var o in list)
                {
                    float w = Footprint(o) * squeeze;
                    float offset = cursor + w * 0.5f;
                    cursor += w + gap * squeeze;
                    o.position = s.Center + along * offset + toPlayer * nearEdge + Vector3.up * 0.02f;
                    o.facing = toPlayer;
                }
            }
        }
    }
}
