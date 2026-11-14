// Menu: RoomZero > Run Self-Tests   (or CI: -batchmode -executeMethod RoomZero.EditorTools.RoomZeroSelfTests.RunBatch)
// Contract + safety tests against the JSON fixtures in Assets/RoomZero/Fixtures.
// These cover the spec's acceptance tests that don't need a headset:
// malformed agent output, out-of-bounds anchor, persistence, adaptation, voice fallback.
// Owner: #8 QA (with #4/#7 adding cases as contracts evolve).

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RoomZero.Input;
using RoomZero.Game;
using RoomZero.Memory;
using RoomZero.Spatial;
using RoomZero.Zero;

namespace RoomZero.EditorTools
{
    public static class RoomZeroSelfTests
    {
        public static string FixtureDir = Path.Combine(Application.dataPath, "RoomZero", "Fixtures");

#if UNITY_EDITOR
        [UnityEditor.MenuItem("RoomZero/Run Self-Tests", priority = 20)]
        public static void RunMenu()
        {
            var (pass, fail, log) = Run();
            Debug.Log(log);
            if (fail > 0) Debug.LogError($"[RZ] Self-tests: {fail} FAILED, {pass} passed");
            else Debug.Log($"[RZ] Self-tests: all {pass} passed");
        }

        public static void RunBatch()
        {
            var r = Run();
            Console.WriteLine(r.log);
            UnityEditor.EditorApplication.Exit(r.fail > 0 ? 1 : 0);
        }
#endif

        public static (int pass, int fail, string log) Run()
        {
            var t = new Results();

            var cfg = ScriptableObject.CreateInstance<RoomZeroConfig>();
            cfg.seatedMode = true;

            Func<RoomMap> Room = () => new RoomScout(new MockRoomProvider(), cfg).Normalize(Load<RoomMap>("roommap_sample.json"), null);

            try
            {
                // ---- Room Scout ----
                var room = Room();
                t.Check("RoomScout rejects low-confidence surface", !room.FindSurface("surface-blurry").allowed);
                t.Check("RoomScout rejects surface inside door zone", !room.FindSurface("surface-by-door").allowed);
                t.Check("RoomScout keeps good table", room.FindSurface("surface-01").allowed);

                // ---- Validator ----
                var valid = MissionValidator.Validate(Load<MissionPlan>("missionplan_valid.json"), Room(), cfg, allowRepair: false);
                t.Check("Valid plan accepted strictly", valid.ok, valid.ToString());
                t.Check("Valid plan spawn points within seated reach", valid.ok && valid.mission.objectives.TrueForAll(o =>
                    RoomScout.HorizontalDistance(new Vector3(0, 1.2f, 0), o.position) <= cfg.Reach + 0.05f));

                var spec = MissionValidator.Validate(Load<MissionPlan>("missionplan_spec_example.json"), Room(), cfg, allowRepair: true);
                t.Check("Spec example plan accepted after repair", spec.ok && spec.repairs.Count >= 2, spec.ToString());

                var badEnum = MissionValidator.Validate(Load<MissionPlan>("missionplan_bad_enum.json"), Room(), cfg, allowRepair: true);
                t.Check("Unknown objective enum rejected (no repair possible)", !badEnum.ok, badEnum.ToString());

                var oobStrict = MissionValidator.Validate(Load<MissionPlan>("missionplan_out_of_bounds.json"), Room(), cfg, allowRepair: false);
                t.Check("Out-of-bounds / excluded anchors rejected (strict)", !oobStrict.ok, oobStrict.ToString());
                var oobRepair = MissionValidator.Validate(Load<MissionPlan>("missionplan_out_of_bounds.json"), Room(), cfg, allowRepair: true);
                t.Check("Out-of-bounds anchors repaired to safe surfaces", oobRepair.ok && oobRepair.mission.objectives.TrueForAll(o => o.anchor.id == "surface-01" || o.anchor.id == "surface-02"), oobRepair.ToString());

                var tooHard = Load<MissionPlan>("missionplan_valid.json"); tooHard.difficulty = 9;
                t.Check("Difficulty outside 1..3 rejected", !MissionValidator.Validate(tooHard, Room(), cfg).ok);
                var tooMany = Load<MissionPlan>("missionplan_valid.json");
                for (int i = 0; i < 5; i++) tooMany.objectives.Add(new ObjectiveSpec { id = "x" + i, type = "FIND_CLUE", anchorId = "surface-01" });
                t.Check("Too many objectives rejected", !MissionValidator.Validate(tooMany, Room(), cfg).ok);
                var oldSchema = Load<MissionPlan>("missionplan_valid.json"); oldSchema.schemaVersion = 0;
                t.Check("Wrong schemaVersion rejected", !MissionValidator.Validate(oldSchema, Room(), cfg).ok);

                // ---- Local agents produce valid plans (the demo fallback must always work) ----
                var fresh = LocalDirectorAgent.Propose(new PlayerProfile());
                foreach (var p in LocalPuzzleArchitectAgent.Place(fresh, Room(), cfg).placements)
                    fresh.objectives.Find(o => o.id == p.objectiveId).anchorId = p.anchorId;
                var freshResult = MissionValidator.Validate(fresh, Room(), cfg, allowRepair: false);
                t.Check("Local fallback plan validates strictly", freshResult.ok, freshResult.ToString());

                var loudProfile = Load<PlayerProfile>("profile_sample.json");
                var loudPlan = LocalDirectorAgent.Propose(loudProfile);
                t.Check("Memory: last LOUD run adds a sensor grid", loudPlan.objectives.Exists(o => o.type == "DISABLE_SENSOR"));
                t.Check("Memory: template alternates after DESK_VAULT", loudPlan.templateId == "SERVER_HEIST");

                // ---- Desktop mock room + empty-room fallback ----
                var scout = new RoomScout(new MockRoomProvider(), cfg);
                var mock = scout.Normalize(new MockRoomProvider().LoadAsync(null, default(System.Threading.CancellationToken)).Result.map, null);
                scout.EnsureReachableSurface(mock, null);
                var loud3 = LocalDirectorAgent.Propose(Load<PlayerProfile>("profile_sample.json"));
                foreach (var p in LocalPuzzleArchitectAgent.Place(loud3, mock, cfg).placements)
                    loud3.objectives.Find(o => o.id == p.objectiveId).anchorId = p.anchorId;
                var mockResult = MissionValidator.Validate(loud3, mock, cfg, allowRepair: false);
                t.Check("Mock room hosts a 3-objective mission strictly", mockResult.ok && mockResult.mission.objectives.Count == 3, mockResult.ToString());

                var empty = scout.EmptyMap(null);
                scout.EnsureReachableSurface(empty, null);
                var emptyPlan = LocalDirectorAgent.Propose(new PlayerProfile());
                foreach (var p in LocalPuzzleArchitectAgent.Place(emptyPlan, empty, cfg).placements)
                    emptyPlan.objectives.Find(o => o.id == p.objectiveId).anchorId = p.anchorId;
                var emptyResult = MissionValidator.Validate(emptyPlan, empty, cfg, allowRepair: false);
                t.Check("No scan data -> virtual desk -> mission still valid", empty.FindSurface("virtual-desk") != null && emptyResult.ok, emptyResult.ToString());

                var none = new RoomMap();
                var noneResult = MissionValidator.Validate(Load<MissionPlan>("missionplan_valid.json"), none, cfg, allowRepair: true);
                t.Check("Room with no surfaces -> plan rejected, nothing spawns", !noneResult.ok, noneResult.ToString());

                // ---- Treasure hunt ----
                var hunt = LocalDirectorAgent.Propose(Load<PlayerProfile>("profile_sample.json"), "TREASURE_HUNT");
                foreach (var p in LocalPuzzleArchitectAgent.Place(hunt, mock, cfg).placements)
                    hunt.objectives.Find(o => o.id == p.objectiveId).anchorId = p.anchorId;
                var huntResult = MissionValidator.Validate(hunt, mock, cfg, allowRepair: false);
                t.Check("Formula hunt validates strictly in the mock room", huntResult.ok && hunt.templateId == "TREASURE_HUNT", huntResult.ToString());
                if (huntResult.ok)
                {
                    var relicSurfaces = new HashSet<string>();
                    foreach (var o in huntResult.mission.objectives) if (o.type == ObjectiveType.HIDDEN_RELIC) relicSurfaces.Add(o.anchor.id);
                    t.Check("Formula fragments spread over 2+ pieces of furniture", relicSurfaces.Count >= 2, string.Join(",", relicSurfaces));
                    t.Check("Seed vault is on the nearest surface", huntResult.mission.objectives.Find(o => o.type == ObjectiveType.SAFE_DIAL).anchor.id == "surface-desk");
                    t.Check("Seed vault has its surface to itself (no crowding)",
                        huntResult.mission.objectives.FindAll(o => o.anchor.id == "surface-desk").Count == 1);
                    bool noOverlap = true;
                    foreach (var a in huntResult.mission.objectives)
                        foreach (var b in huntResult.mission.objectives)
                            if (a != b && a.anchor.id == b.anchor.id &&
                                Vector3.Distance(a.position, b.position) < (MissionValidator.Footprint(a) + MissionValidator.Footprint(b)) * 0.5f * 0.8f)
                                noOverlap = false;
                    t.Check("Objects sharing a surface don't overlap", noOverlap);
                }

                var noRelics = new MissionPlan { missionId = "x", templateId = "TREASURE_HUNT", durationSeconds = 200,
                    objectives = new List<ObjectiveSpec> { new ObjectiveSpec { id = "safe", type = "SAFE_DIAL", anchorId = "surface-01", steps = 3 } } };
                t.Check("SAFE_DIAL without relics rejected", !MissionValidator.Validate(noRelics, Room(), cfg).ok);

                var badSteps = LocalDirectorAgent.ProposeTreasureHunt(new PlayerProfile());
                foreach (var p in LocalPuzzleArchitectAgent.Place(badSteps, Room(), cfg).placements)
                    badSteps.objectives.Find(o => o.id == p.objectiveId).anchorId = p.anchorId;
                badSteps.objectives.Find(o => o.type == "SAFE_DIAL").steps = 5;
                var badStepsResult = MissionValidator.Validate(badSteps, Room(), cfg, allowRepair: true);
                t.Check("Vault dial length repaired to fragment count", badStepsResult.ok && badSteps.objectives.Find(o => o.type == "SAFE_DIAL").steps == 2, badStepsResult.ToString());

                t.Check("Voice: 'start the formula hunt'", VoiceIntentRouter.Parse("ZERO, start the formula hunt") == VoiceIntentId.START_MISSION);
                t.Check("Every mission briefing fits the 280-char limit",
                    LocalDirectorAgent.ProposeTreasureHunt(new PlayerProfile { stats = new ProfileStats { recentDifficulty = 3 } }).briefing.Length <= 280
                    && LocalDirectorAgent.Propose(Load<PlayerProfile>("profile_sample.json")).briefing.Length <= 280);
                t.Check("Riddles use real furniture types", RoomZero.Game.HiddenRelicObjective.Riddle("table").Contains("cup") && RoomZero.Game.HiddenRelicObjective.Riddle("couch").Contains("rest"));
                var scanRay = new Ray(Vector3.zero, Vector3.forward);
                t.Check("Scanner ray distance math", Mathf.Abs(ScanSources.DistanceToRay(scanRay, new Vector3(0.05f, 0, 1f)) - 0.05f) < 0.001f
                                                     && Mathf.Abs(ScanSources.DistanceToRay(scanRay, new Vector3(0, 0, -1f)) - 1f) < 0.001f);

                // ---- Adaptation ----
                var sig = new MissionSignals { failedAttempts = 2, durationSeconds = 180, elapsedSeconds = 40, objectivesTotal = 2 };
                t.Check("Adaptation: 2 failures -> OFFER_HINT", LocalAdaptationAgent.Propose(new AdaptationInput { signals = sig }).action == "OFFER_HINT");
                var again = new AdaptationInput { signals = sig }; again.alreadyApplied.Add("OFFER_HINT");
                t.Check("Adaptation: same action not repeated", LocalAdaptationAgent.Propose(again).action != "OFFER_HINT");
                var calm = new MissionSignals { durationSeconds = 180, elapsedSeconds = 20, objectivesTotal = 2 };
                t.Check("Adaptation: no signal -> NONE", LocalAdaptationAgent.Propose(new AdaptationInput { signals = calm }).action == "NONE");

                // ---- Player model + persistence ----
                var model = new LocalPlayerModel();
                var prof = model.Apply(new PlayerProfile(), new SessionSummary { sessionId = "t1", completed = true, difficulty = 1, choice = "SILENT_ROUTE", seconds = 100 });
                t.Check("Player model: clean win raises difficulty", prof.stats.recentDifficulty == 2);
                prof = model.Apply(prof, new SessionSummary { sessionId = "t2", completed = false, difficulty = 2, choice = "LOUD_ROUTE", seconds = 180 });
                t.Check("Player model: loss lowers difficulty", prof.stats.recentDifficulty == 1 && prof.stats.completionRate == 0.5f);

                var dir = Path.Combine(Path.GetTempPath(), "rz_selftest_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                var store = new MemoryStore(dir);
                store.Save(prof);
                var reloaded = new MemoryStore(dir).Load();
                t.Check("Persistence: save -> reload keeps last choice", reloaded.HasLastSession && reloaded.lastSession.choice == "LOUD_ROUTE" && reloaded.stats.missions == 2);
                File.WriteAllText(Path.Combine(dir, MemoryStore.FileName), "{ not json");
                t.Check("Persistence: corrupt file -> fresh profile, no crash", !new MemoryStore(dir).Load().HasLastSession);
                Directory.Delete(dir, true);

                // ---- Voice ----
                t.Check("Voice: 'ZERO, give me a mission'", VoiceIntentRouter.Parse("ZERO, give me a mission") == VoiceIntentId.START_MISSION);
                t.Check("Voice: 'go silent'", VoiceIntentRouter.Parse("go silent") == VoiceIntentId.CHOOSE_SILENT);
                t.Check("Voice: noise -> UNKNOWN", VoiceIntentRouter.Parse("uh what") == VoiceIntentId.UNKNOWN);

                // ---- NPC catalog ----
                t.Check("NPC fallback lines are short", Array.TrueForAll(NpcCatalog.Beats, b => NpcCatalog.FallbackText(b).Length <= NpcCatalog.MaxLineLength));

                // ---- 3-room campaign (rooms 2 and 3 + Dr. Asha's solvers) ----
                foreach (var tmpl in new[] { "GREENHOUSE", "GENE_LAB" })
                {
                    for (int diff = 1; diff <= 3; diff++)
                    {
                        var camProf = Load<PlayerProfile>("profile_sample.json");
                        camProf.stats.recentDifficulty = diff;
                        var roomPlan = LocalDirectorAgent.Propose(camProf, tmpl);
                        foreach (var pl in LocalPuzzleArchitectAgent.Place(roomPlan, mock, cfg).placements)
                            roomPlan.objectives.Find(o => o.id == pl.objectiveId).anchorId = pl.anchorId;
                        var rr = MissionValidator.Validate(roomPlan, mock, cfg, allowRepair: false);
                        t.Check($"{tmpl} (level {diff}) validates strictly in the mock room", rr.ok && roomPlan.templateId == tmpl, rr.ToString());
                        if (rr.ok && diff == 1)
                        {
                            var anchors = new HashSet<string>();
                            foreach (var o in rr.mission.objectives) anchors.Add(o.anchor.id);
                            t.Check($"{tmpl}: its two challenges sit on different furniture (no overlap)", anchors.Count == rr.mission.objectives.Count);
                        }
                    }
                }
                t.Check("Campaign order: vault lab -> greenhouse -> gene lab",
                    Campaign.TryNext(MissionTemplateId.TREASURE_HUNT, out var r2) && r2 == MissionTemplateId.GREENHOUSE &&
                    Campaign.TryNext(MissionTemplateId.GREENHOUSE, out var r3) && r3 == MissionTemplateId.GENE_LAB &&
                    !Campaign.TryNext(MissionTemplateId.GENE_LAB, out _) && Campaign.IsFinal(MissionTemplateId.GENE_LAB));
                t.Check("Classic heists are not campaign rooms", !Campaign.IsCampaign(MissionTemplateId.DESK_VAULT));
                t.Check("DNA pairing A-T, G-C", DnaSpliceObjective.Pair('A') == 'T' && DnaSpliceObjective.Pair('T') == 'A' && DnaSpliceObjective.Pair('G') == 'C' && DnaSpliceObjective.Pair('C') == 'G');

                bool growOk = true; string growWhy = "";
                for (int a = 2; a <= 4; a++) for (int b = 2; b <= 4; b++) for (int c = 2; c <= 4; c++)
                {
                    int[] lv = { 1, 1, 1 }, tg = { a, b, c };
                    int moves = 0;
                    while (GrowLightObjective.NextAutoMove(lv, tg, out int ctl, out int mv) && moves < 60)
                    {
                        GrowLightObjective.ApplyChange(lv, ctl, mv);
                        moves++;
                        if (GrowLightObjective.Scorched(lv, tg)) { growOk = false; growWhy = $"scorched at targets {a}{b}{c}"; }
                    }
                    if (moves >= 60) { growOk = false; growWhy = $"no convergence at targets {a}{b}{c}"; }
                }
                t.Check("Dr. Asha balances every grow-light target without scorching", growOk, growWhy);

                // ---- Mentor agent (Dr. Asha's judgement) ----
                var stuck = new MentorInput { objectiveType = "DNA_SPLICE", secondsStuck = 25f };
                t.Check("Mentor: stuck 25s -> HINT", LocalMentorAgent.Decide(stuck).action == "HINT");
                var veryStuck = new MentorInput { objectiveType = "DNA_SPLICE", secondsStuck = 45f, hintsGiven = 2 };
                t.Check("Mentor: stuck 45s after 2 hints -> SHOW_ONE_STEP", LocalMentorAgent.Decide(veryStuck).action == "SHOW_ONE_STEP");
                var going = new MentorInput { objectiveType = "DNA_SPLICE", secondsStuck = 4f, secondsLeft = 200f, objectivesTotal = 2 };
                t.Check("Mentor: player making progress -> WAIT", LocalMentorAgent.Decide(going).action == "WAIT");
                t.Check("Mentor guardrail: unknown action rejected", !LocalMentorAgent.IsValid(new MentorDecision { action = "SOLVE_EVERYTHING" }, going));
                t.Check("Mentor guardrail: no 'show' unless really stuck", !LocalMentorAgent.IsValid(new MentorDecision { action = "SHOW_ONE_STEP" }, going));

                // ---- State machine ----
                var sm = new MissionStateMachine();
                t.Check("State machine refuses BOOT -> ACTIVE", !sm.TransitionTo(MissionStateId.ACTIVE, "test"));
                t.Check("State machine allows BOOT -> ROOM_READY", sm.TransitionTo(MissionStateId.ROOM_READY, "test"));

                // MR lab window: off-axis view through the window rectangle.
                var c0 = RoomZero.Spatial.LabWindow.FrustumEdges(new Vector3(0, 0, -1f), 0.05f);
                t.Check("Lab window: centred eye gives a symmetric view", Mathf.Abs(c0[0] + c0[1]) < 1e-5f && Mathf.Abs(c0[2] + c0[3]) < 1e-5f);
                var c1 = RoomZero.Spatial.LabWindow.FrustumEdges(new Vector3(0.3f, 0, -1f), 0.05f);
                t.Check("Lab window: stepping right shifts the view left (parallax)", c1[0] < c0[0] && c1[1] < c0[1]);
                var c2 = RoomZero.Spatial.LabWindow.FrustumEdges(new Vector3(0, 0, -2f), 0.05f);
                t.Check("Lab window: stepping back narrows the view", c2[1] - c2[0] < c0[1] - c0[0]);
            }
            catch (Exception e)
            {
                t.Check("no exceptions", false, e.ToString());
            }

            UnityEngine.Object.DestroyImmediate(cfg);
            t.lines.Add($"{t.pass} passed, {t.fail} failed");
            return (t.pass, t.fail, string.Join("\n", t.lines));
        }

        class Results
        {
            public int pass, fail;
            public readonly List<string> lines = new List<string> { "ROOM//ZERO self-tests" };
            public void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++; else fail++;
                lines.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
            }
        }

        static T Load<T>(string file) => JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(FixtureDir, file)));
    }
}
