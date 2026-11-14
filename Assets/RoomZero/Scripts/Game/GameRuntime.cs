// The authoritative game runtime (spec: "the runtime—not the LLM—decides whether a puzzle
// is solved, whether a guard notices, and whether the mission succeeds").
// Everything that changes the room goes through ApplyCommand, which re-validates.
// Owner: #6 Game Systems (rules) + #1 Tech Lead (command interface).

using System;
using System.Collections.Generic;
using UnityEngine;
using RoomZero.Spatial;
using RoomZero.Zero;

namespace RoomZero.Game
{
    public class GameRuntime : MonoBehaviour
    {
        public ValidatedMission Mission { get; private set; }
        public RouteChoice Route { get; private set; }
        public bool Running { get; private set; }
        public bool Paused { get; set; }
        public int Seed { get; private set; }
        public float SecondsLeft { get; private set; }
        public float Elapsed { get; private set; }
        public int FailedAttempts { get; private set; }
        public int HintsUsed { get; private set; }
        public int ObjectivesComplete { get; private set; }
        public GuardDrone Guard { get; private set; }
        public readonly List<string> AppliedAdaptations = new List<string>();

        public event Action<MissionOutcome> MissionEnded;
        public event Action<string> BeatRequested;      // NPC beat id -> orchestrator asks the NPC agent
        public event Action<string> ObjectiveDone;

        RoomZeroConfig cfg;
        RoomMap room;
        Transform root;
        readonly Dictionary<string, ObjectiveBehaviour> spawned = new Dictionary<string, ObjectiveBehaviour>();
        readonly Dictionary<string, string> codes = new Dictionary<string, string>();
        Transform marker;

        /// First unfinished objective in plan order: what the player should do next.
        public ObjectiveBehaviour CurrentTarget
        {
            get
            {
                if (Mission == null) return null;
                foreach (var o in Mission.objectives)
                {
                    var b = Objective(o.spec.id);
                    if (b && !b.Completed) return b;
                }
                return null;
            }
        }

        /// Text for the HUD "NEXT:" line, with a direction relative to the player's view.
        public string GuideText(Transform head)
        {
            var t = CurrentTarget;
            if (!t) return Running ? "all objectives done!" : "";
            return t.Guide(Direction(head, t.transform.position));
        }

        /// Lines for the desktop side card, one per objective in plan order.
        public List<string> StatusLines()
        {
            var lines = new List<string>();
            if (Mission == null) return lines;
            foreach (var o in Mission.objectives)
            {
                var b = Objective(o.spec.id);
                if (b) lines.Add(b.StatusLine());
            }
            return lines;
        }

        public bool IsFormulaHunt => Mission != null && Mission.template == MissionTemplateId.TREASURE_HUNT;

        public static string Direction(Transform head, Vector3 target)
        {
            if (!head) return "nearby";
            Vector3 to = target - head.position; to.y = 0;
            Vector3 fwd = head.forward; fwd.y = 0;
            if (to.sqrMagnitude < 0.0001f || fwd.sqrMagnitude < 0.0001f) return "right in front of you";
            float angle = Vector3.SignedAngle(fwd.normalized, to.normalized, Vector3.up);
            if (Mathf.Abs(angle) < 25f) return "in front of you";
            if (Mathf.Abs(angle) > 120f) return "behind you";
            return angle > 0 ? "on your right" : "on your left";
        }

        public static GameRuntime Instance { get; private set; }

        public void Init(RoomZeroConfig config) { cfg = config; Instance = this; }

        public MissionSignals Signals => new MissionSignals
        {
            elapsedSeconds = Elapsed,
            durationSeconds = Mission != null ? Mission.durationSeconds : 0,
            failedAttempts = FailedAttempts,
            hintsUsed = HintsUsed,
            objectivesComplete = ObjectivesComplete,
            objectivesTotal = Mission != null ? Mission.objectives.Count : 0,
            guardAlertness = Guard ? Guard.Alertness : 0,
            difficulty = Mission != null ? Mission.difficulty : 1
        };

        // ------------------------------------------------------------------ lifecycle

        public void Begin(ValidatedMission mission, RoomMap roomMap, RouteChoice route, float startAlertness)
        {
            Cleanup();
            Mission = mission;
            room = roomMap;
            Route = route;
            Seed = Mathf.Abs((mission.MissionId ?? "x").GetHashCode());
            Elapsed = 0; FailedAttempts = 0; HintsUsed = 0; ObjectivesComplete = 0;
            AppliedAdaptations.Clear();
            root = new GameObject($"Mission_{mission.MissionId}").transform;

            // Route = the player's choice; deterministic, bounded consequences.
            float duration = mission.durationSeconds;
            float gain = 0.15f;
            if (route == RouteChoice.LOUD_ROUTE)
            {
                duration = Mathf.Max(cfg.durationRangeSeconds.x, duration - 30);
                gain = 0.25f;
                startAlertness += 0.2f;
                foreach (var o in mission.objectives)
                    if (o.type == ObjectiveType.ENTER_CODE) o.spec.steps = Mathf.Max(3, o.spec.steps - 1); // smash: shorter code
            }
            SecondsLeft = duration;

            GenerateCodes();
            SpawnGuard(Mathf.Clamp(startAlertness, 0f, 0.6f), gain);

            foreach (var o in mission.objectives)
            {
                var (ok, reason) = ApplyCommand(new GameCommand { type = GameCommandType.SPAWN_OBJECTIVE, missionId = mission.MissionId, objectiveId = o.spec.id });
                if (!ok) ZeroLog.Warn("Runtime", $"spawn {o.spec.id} rejected: {reason}");
            }
            CreateMarker();
            Running = true;
            ZeroLog.Info("Runtime", $"mission {mission.MissionId} running: {mission.objectives.Count} objectives, {duration}s, route={route}");
        }

        public void Cleanup()
        {
            Running = false;
            spawned.Clear();
            codes.Clear();
            if (root) Destroy(root.gameObject);
            if (Guard) Destroy(Guard.gameObject);
            if (marker) Destroy(marker.gameObject);
            Mission = null;
        }

        void Update()
        {
            UpdateMarker();
            if (!Running || Paused) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            SecondsLeft -= dt;
            if (Guard) Guard.Tick(dt);
            if (SecondsLeft <= 0) End(MissionOutcome.FAILED_TIMEOUT);
        }

        void End(MissionOutcome outcome)
        {
            if (!Running) return;
            Running = false;
            ZeroLog.Info("Runtime", $"mission ended: {outcome} after {Elapsed:0}s");
            MissionEnded?.Invoke(outcome);
            StartCoroutine(HideMissionObjects(3f)); // let the win moment play, then clear the view for results
        }

        System.Collections.IEnumerator HideMissionObjects(float delay)
        {
            var r = root; var g = Guard;
            yield return new WaitForSeconds(delay);
            if (Running) yield break;           // a new mission already started
            if (r) r.gameObject.SetActive(false);
            if (g) g.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ commands

        /// The single entry point for changing the world. Rejects anything stale,
        /// unknown, duplicated or out of bounds, and says why.
        public (bool ok, string reason) ApplyCommand(GameCommand c)
        {
            if (c == null) return (false, "null command");
            if (Mission == null || c.missionId != Mission.MissionId) return (false, $"stale mission id '{c.missionId}'");

            switch (c.type)
            {
                case GameCommandType.SPAWN_OBJECTIVE:
                {
                    var o = Mission.Find(c.objectiveId);
                    if (o == null) return (false, $"objective '{c.objectiveId}' not in validated mission");
                    if (spawned.ContainsKey(o.spec.id)) return (false, "already spawned");
                    if (RoomScout.HorizontalDistance(room.PlayerOrigin, o.position) > cfg.Reach + 0.05f) return (false, "out of reach");
                    Spawn(o);
                    return (true, null);
                }
                case GameCommandType.PLAY_NPC_BEAT:
                    if (!NpcCatalog.IsKnownBeat(c.beatId)) return (false, $"unknown beat '{c.beatId}'");
                    BeatRequested?.Invoke(c.beatId);
                    return (true, null);

                case GameCommandType.APPLY_ADAPTATION:
                    return ApplyAdaptation(c.adaptation, c.amount);

                case GameCommandType.END_MISSION:
                    End(MissionOutcome.ABANDONED);
                    return (true, null);
            }
            return (false, $"unsupported command {c.type}");
        }

        (bool, string) ApplyAdaptation(AdaptationAction a, int amount)
        {
            if (!Running) return (false, "not running");
            switch (a)
            {
                case AdaptationAction.OFFER_HINT:
                    ShowHint();
                    break;
                case AdaptationAction.EXTEND_TIMER:
                    SecondsLeft += Mathf.Clamp(amount, 10, 60);
                    break;
                case AdaptationAction.LOWER_GUARD_ALERTNESS:
                    if (Guard) Guard.Lower(Mathf.Clamp(amount, 5, 40) / 100f);
                    break;
                case AdaptationAction.RAISE_GUARD_ALERTNESS:
                    // Pressure, never a cheap loss: cannot push the guard past 0.6 this way.
                    if (Guard) Guard.Raise(Mathf.Max(0, Mathf.Min(Mathf.Clamp(amount, 5, 25) / 100f, 0.6f - Guard.Alertness)));
                    break;
                default:
                    return (false, $"adaptation {a} not applicable");
            }
            AppliedAdaptations.Add(a.ToString());
            return (true, null);
        }

        // ------------------------------------------------------------------ objectives

        void Spawn(ValidatedObjective o)
        {
            var go = new GameObject($"Objective_{o.spec.id}_{o.type}");
            go.transform.SetParent(root, true);
            ObjectiveBehaviour b;
            switch (o.type)
            {
                case ObjectiveType.FIND_CLUE: b = go.AddComponent<ClueObjective>(); break;
                case ObjectiveType.ENTER_CODE: b = go.AddComponent<CodePanelObjective>(); break;
                case ObjectiveType.HIDDEN_RELIC: b = go.AddComponent<HiddenRelicObjective>(); break;
                case ObjectiveType.SAFE_DIAL: b = go.AddComponent<SafeDialObjective>(); break;
                case ObjectiveType.NUTRIENT_MIX: b = go.AddComponent<NutrientMixObjective>(); break;
                case ObjectiveType.GROW_LIGHT: b = go.AddComponent<GrowLightObjective>(); break;
                case ObjectiveType.MICROSCOPE_FOCUS: b = go.AddComponent<MicroscopeObjective>(); break;
                case ObjectiveType.DNA_SPLICE: b = go.AddComponent<DnaSpliceObjective>(); break;
                default: b = go.AddComponent<SensorObjective>(); break;
            }
            spawned[o.spec.id] = b;
            b.Init(this, o);
        }

        void GenerateCodes()
        {
            var rng = new System.Random(Seed);
            foreach (var o in Mission.objectives)
                if (o.type == ObjectiveType.HIDDEN_RELIC) codes[o.spec.id] = rng.Next(10).ToString(); // one digit per relic
            foreach (var o in Mission.objectives)
            {
                if (o.type != ObjectiveType.ENTER_CODE) continue;
                int len = Mathf.Clamp(o.spec.steps, 3, 5);
                var chars = new char[len];
                for (int i = 0; i < len; i++) chars[i] = (char)('0' + rng.Next(10));
                codes[o.spec.targetId] = new string(chars); // keyed by the clue that reveals it
            }
        }

        // ---- treasure hunt helpers ----
        public List<string> RelicIds()
        {
            var ids = new List<string>();
            if (Mission == null) return ids;
            foreach (var o in Mission.objectives) if (o.type == ObjectiveType.HIDDEN_RELIC) ids.Add(o.spec.id);
            return ids;
        }

        public int RelicIndex(string relicId) => RelicIds().IndexOf(relicId) + 1;
        public int RelicDigit(string relicId) => codes.TryGetValue(relicId, out var d) ? d[0] - '0' : 0;

        /// Safe combination = relic digits in chain order.
        public string SafeCombo()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var id in RelicIds()) sb.Append(RelicDigit(id));
            return sb.ToString();
        }

        /// e.g. "7-2-?" : digits of collected relics, ? for the rest.
        public string KnownComboText()
        {
            var parts = new List<string>();
            foreach (var id in RelicIds())
            {
                var o = Objective(id);
                parts.Add(o && o.Completed ? RelicDigit(id).ToString() : "?");
            }
            return string.Join("-", parts);
        }

        public string CodeFor(string clueId) => clueId != null && codes.TryGetValue(clueId, out var c) ? c : "000";
        public ObjectiveBehaviour Objective(string id) => id != null && spawned.TryGetValue(id, out var o) ? o : null;

        public void ReportObjectiveCompleted(string id)
        {
            if (!Running) return;
            ObjectivesComplete++;
            ObjectiveDone?.Invoke(id);
            ZeroLog.Info("Runtime", $"objective {id} complete ({ObjectivesComplete}/{Mission.objectives.Count})");
            if (ObjectivesComplete >= Mission.objectives.Count) End(MissionOutcome.SUCCESS);
        }

        public void ReportFailedAttempt(string id)
        {
            if (!Running) return;
            FailedAttempts++;
            if (Guard) Guard.Raise(Guard.gainPerFailure);
            ZeroLog.Info("Runtime", $"failed attempt on {id} (total {FailedAttempts}), alert={(Guard ? Guard.Alertness : 0f):0.00}");
        }

        /// Player-requested hint (counts toward hint rate).
        public void RequestHint()
        {
            if (!Running) return;
            HintsUsed++;
            ShowHint();
        }

        void ShowHint()
        {
            // Hint the first incomplete objective in plan order (sensor -> clue -> code).
            foreach (var o in Mission.objectives)
            {
                var b = Objective(o.spec.id);
                if (b && !b.Completed) { b.ShowHint(); BeatRequested?.Invoke("zero_hint"); return; }
            }
        }

        // ------------------------------------------------------------------ "go here" marker

        void CreateMarker()
        {
            marker = new GameObject("NextTargetMarker").transform;
            var diamond = UI.UiFactory.Primitive(PrimitiveType.Cube, "Diamond", marker, Vector3.zero, Vector3.one * 0.035f, UI.Palette.Accent);
            diamond.transform.localRotation = Quaternion.Euler(45, 0, 45);
            diamond.AddComponent<HoloPulse>();
            UI.UiFactory.Primitive(PrimitiveType.Cube, "Stem", marker, new Vector3(0, -0.05f, 0), new Vector3(0.006f, 0.06f, 0.006f), UI.Palette.Accent);
        }

        void UpdateMarker()
        {
            if (!marker) return;
            var t = Running ? CurrentTarget : null;
            bool show = t != null && t.ShowMarker;
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (!show) return;
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 0.02f;
            marker.position = t.transform.position + Vector3.up * (t.MarkerHeight + bob);
            marker.Rotate(Vector3.up, 120f * Time.unscaledDeltaTime, Space.World);
        }

        // ------------------------------------------------------------------ guard

        void SpawnGuard(float startAlertness, float gain)
        {
            Vector3 origin = room.PlayerOrigin;
            Vector3 pos = Vector3.zero;
            bool found = false;
            float best = float.MaxValue;
            foreach (var s in room.surfaces)
            {
                if (!s.allowed || s.IsHorizontal) continue; // walls only
                float d = RoomScout.HorizontalDistance(origin, s.Center);
                if (d < 0.8f || d > 3.5f || d >= best) continue;
                best = d;
                pos = s.Center + s.Normal * 0.75f;   // clear of furniture against the wall
                pos.y = origin.y + 0.25f;
                found = true;
            }
            if (!found)
            {
                // No wall: hover beyond the objectives, above eye level, out of reach.
                Vector3 fwd = Vector3.forward;
                if (Mission.objectives.Count > 0) { fwd = Mission.objectives[0].position - origin; fwd.y = 0; fwd.Normalize(); }
                pos = origin + fwd * 1.4f + Vector3.up * 0.25f;
            }

            Guard = GuardDrone.Create(pos, origin, startAlertness);
            Guard.gainPerFailure = gain;
            Guard.BeatTriggered += beat => BeatRequested?.Invoke(beat);
            Guard.Caught += () => End(MissionOutcome.FAILED_CAUGHT);
        }
    }
}
