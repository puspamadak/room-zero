// ZERO: one orchestrator, explicit states, timeouts, structured I/O (spec section 4,
// "Important engineering rule"). It invokes only the specialist the current step needs,
// validates every answer, and falls back to the local agent on any failure.
// Owner: #4 AI Orchestration Engineer.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using RoomZero.Game;
using RoomZero.Input;
using RoomZero.Memory;
using RoomZero.Spatial;
using RoomZero.UI;

namespace RoomZero.Zero
{
    public class ZeroOrchestrator : MonoBehaviour
    {
        // ---- wired by RoomZeroBootstrap ----
        [NonSerialized] public RoomZeroConfig cfg;
        [NonSerialized] public Transform head;
        [NonSerialized] public RoomScout roomScout;
        [NonSerialized] public MemoryStore memory;
        [NonSerialized] public GameRuntime runtime;
        [NonSerialized] public UiPanel panel;
        [NonSerialized] public MissionHud hud;
        [NonSerialized] public RoomDebugView roomDebug;

        [NonSerialized] public IDirectorAgent remoteDirector;
        [NonSerialized] public IPuzzleArchitectAgent remotePuzzle;
        [NonSerialized] public INpcAgent remoteNpc;
        [NonSerialized] public IAdaptationAgent remoteAdaptation;
        [NonSerialized] public IMentorAgent remoteMentor;
        readonly IPlayerModel playerModel = new LocalPlayerModel();

        public MissionStateMachine Machine { get; } = new MissionStateMachine();
        public RoomMap Room { get; private set; }

        PlayerProfile snapshot;          // profile as it was when the mission was requested
        string requestedTemplate;        // e.g. TREASURE_HUNT when the player asked for it
        MissionTemplateId lastTemplate;  // what was just played (campaign: which room comes next)
        bool entryPassed;                // Room 1 palm scan done for this request
        bool keepAsha;                   // Dr. Asha already walked in through the lab doors
        bool lastSuccess;
        ValidatedMission mission;
        string planSource;
        float nextAdaptationCheck, lastAdaptationAt = -999f;
        bool paused;
        string pauseReason;
        Action restorePanel;
        CancellationTokenSource lifetime;
        bool lastCallRemoteOk;

        // ================================================================== boot

        public void Begin()
        {
            lifetime = new CancellationTokenSource();
            PokeTarget.InputBlocked = false;   // a pause left over from a previous Play session must not block touches

            // State timers must outlast the agents they wait for, or a slow-but-valid AI answer is thrown away.
            if (cfg && cfg.aiMode == AiMode.RemoteWithFallback)
            {
                MissionStateMachine.Timeouts[MissionStateId.PLANNING] = Mathf.Max(10f, (TimeoutFor("Director") + TimeoutFor("PuzzleArchitect")) / 1000f + 3f);
                MissionStateMachine.Timeouts[MissionStateId.ADAPTING] = Mathf.Max(6f, TimeoutFor("Adaptation") / 1000f + 2f);
            }

            Machine.TimedOut += OnStateTimeout;
            runtime.MissionEnded += OnMissionEnded;
            runtime.BeatRequested += beat => Safe(SpeakAsync(beat));
            runtime.ObjectiveDone += OnObjectiveDone;

            EventBus.Subscribe<MissionRequested>(OnMissionRequested);
            EventBus.Subscribe<RouteChosen>(e => ChooseRoute(e.route));
            EventBus.Subscribe<HintRequested>(_ => { if (Machine.Is(MissionStateId.ACTIVE, MissionStateId.ADAPTING)) runtime.RequestHint(); });
            EventBus.Subscribe<PauseRequested>(e => SetPaused(e.paused, e.reason));
            EventBus.Subscribe<NextMissionRequested>(_ => { if (Machine.Is(MissionStateId.READY_FOR_NEXT_SESSION)) EventBus.Publish(new MissionRequested { source = InputSource.Ui, template = NextTemplate() }); });
            EventBus.Subscribe<HandTrackingChanged>(OnHandTracking);

            Safe(BootAsync());
        }

        void OnDestroy()
        {
            lifetime?.Cancel();
            EventBus.Clear();
        }

        async Task BootAsync()
        {
            ZeroLog.SessionId = "boot";
            panel.Show("ROOM//ZERO", "Reading your room...");
            memory.Load();

            var scanned = await roomScout.ScanAsync(head, 8000, lifetime.Token);
            if (!this) return;
            if (Room == null) Room = scanned;
            if (!Machine.Is(MissionStateId.BOOT)) return; // boot timeout already moved on
            if (roomDebug) roomDebug.Show(Room, cfg.showRoomDebug);
            int usable = 0;
            foreach (var srf in Room.surfaces) if (MissionValidator.AnchorProblem(srf, Room, cfg) == null) usable++;
            AgentTrace.Log("RoomScout", false, $"{usable} of {Room.surfaces.Count} surfaces safe and within reach, {Room.excludedZoneIds.Count} no-go zone(s)");
            Machine.TransitionTo(MissionStateId.ROOM_READY, Room.source);
            if (DesktopSimulatorInput.Instance) DesktopSimulatorInput.Instance.FrameRoom(Room.PlayerOrigin, head.forward);
            ShowReady();
        }

        void ShowReady()
        {
            PlayMusic(RoomDebugView.Instance ? RoomDebugView.Instance.Theme : LabTheme.VaultLab, 0.7f);
            var p = memory.Profile;
            string body = p.HasLastSession
                ? $"Welcome back. Missions run: {p.stats.missions}. Last time you chose {Pretty(p.lastSession.choice)} and {(p.lastSession.completed ? "got out clean" : "got caught")}.\n\nSay \"ZERO, give me a mission\" (or \"formula hunt\" for the 3-room quest with Dr. Asha), or press a button."
                : "Your room is the vault. Stay seated; everything is within reach.\n3-ROOM QUEST: vault lab, greenhouse, gene lab. You play; Dr. Asha guides (J = she demos).\n\nSay \"ZERO, give me a mission\" (or \"formula hunt\"), or press a button.";
            ShowPanel(() => panel.Show("ROOM//ZERO", body,
                new UiAction("start", "START HEIST", Palette.Success * 0.6f + Color.black * 0.4f, () => EventBus.Publish(new MissionRequested { source = InputSource.Ui })),
                new UiAction("start_hunt", "3-ROOM QUEST", Palette.Accent * 0.6f + Color.black * 0.4f, StartTreasureHunt),
                new UiAction("sys_reset", "RESET MEMORY", Palette.Button, ResetMemory)));
        }

        // ================================================================== mission request -> plan -> validate

        void OnMissionRequested(MissionRequested e)
        {
            if (!Machine.Is(MissionStateId.ROOM_READY, MissionStateId.READY_FOR_NEXT_SESSION) || paused) return;
            if (Room == null) { ZeroLog.Warn("ZERO", "mission requested before room is ready"); return; }

            // Room 1 starts outside the lab: palm scan at the bio-secure doors first.
            if (e.template == nameof(MissionTemplateId.TREASURE_HUNT) && !entryPassed)
            {
                if (!EntryGate.Instance) StartEntry(e);
                return;
            }
            entryPassed = false;
            if (EntryGate.Instance && !EntryGate.Instance.Verified)
            {
                // Player chose something else at the door: drop the entrance.
                EntryGate.Close();
                if (RoomDebugView.Instance) RoomDebugView.Instance.HideEntrance();
            }
            if (!Machine.TransitionTo(MissionStateId.MISSION_REQUESTED, e.source.ToString())) return;

            ZeroLog.SessionId = $"s{DateTime.UtcNow:MMddHHmmss}";
            requestedTemplate = EnumParse.TryParse(e.template, out MissionTemplateId _) ? e.template : null;
            DesktopOverlay.ClearCelebration();
            // Each campaign room has its own look; the classic heists use the lab.
            if (!keepAsha) ScientistNpc.Despawn();
            keepAsha = false;
            var theme = requestedTemplate != null && EnumParse.TryParse(requestedTemplate, out MissionTemplateId want) ? Campaign.Theme(want) : LabTheme.VaultLab;
            if (RoomDebugView.Instance) RoomDebugView.Instance.SetTheme(theme);
            PlayMusic(theme, 1f);
            snapshot = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(memory.Profile));
            runtime.Cleanup();
            hud.Hide();
            Safe(PlanAsync());
        }

        async Task PlanAsync()
        {
            Machine.TransitionTo(MissionStateId.PLANNING);
            ShowPanel(() => panel.Show("ZERO", "Analyzing the room. Planning your heist..."));

            // Director: theme / objectives / difficulty.
            var directorInput = new DirectorInput { room = Room, profile = snapshot, requestedTemplate = requestedTemplate };
            var plan = await Call("Director",
                remoteDirector == null ? null : (Func<CancellationToken, Task<MissionPlan>>)(ct => remoteDirector.ProposeMissionAsync(directorInput, ct)),
                () => LocalDirectorAgent.Propose(snapshot, requestedTemplate),
                p => p.objectives != null && p.objectives.Count > 0);
            planSource = lastCallRemoteOk ? "remote_director" : "local_director";

            // Puzzle Architect: map objectives to valid anchor IDs (never coordinates).
            var puzzleInput = new PuzzleInput { room = Room, plan = plan, reachMeters = cfg.Reach };
            var placements = await Call("PuzzleArchitect",
                remotePuzzle == null ? null : (Func<CancellationToken, Task<PlacementList>>)(ct => remotePuzzle.PlacePuzzlesAsync(puzzleInput, ct)),
                () => LocalPuzzleArchitectAgent.Place(plan, Room, cfg),
                p => p.placements != null && p.placements.Count > 0);
            if (!this) return;
            foreach (var pl in placements.placements)
            {
                var o = plan.objectives.Find(x => x.id == pl.objectiveId);
                if (o != null) o.anchorId = pl.anchorId;
            }

            if (!Machine.Is(MissionStateId.PLANNING)) return; // timed out / cancelled meanwhile
            Validate(plan);
        }

        void Validate(MissionPlan plan)
        {
            Machine.TransitionTo(MissionStateId.VALIDATING);
            var sw = Stopwatch.StartNew();
            var result = MissionValidator.Validate(plan, Room, cfg, allowRepair: true);
            ZeroLog.Info("Validator", result.ToString(), sw.ElapsedMilliseconds);
            AgentTrace.Log("Validator", false, result.ok
                ? (result.repairs.Count > 0 ? $"approved after {result.repairs.Count} fix(es): {result.repairs[0]}" : "approved: safe, reachable, rule-valid")
                : $"rejected ({(result.errors.Count > 0 ? result.errors[0] : "invalid")}) -> safe template");

            if (!result.ok)
            {
                // Deterministic template, built only from local agents. Spec: "use a
                // deterministic template after repeated failure".
                var fallback = LocalDirectorAgent.Propose(snapshot, requestedTemplate);
                foreach (var pl in LocalPuzzleArchitectAgent.Place(fallback, Room, cfg).placements)
                {
                    var o = fallback.objectives.Find(x => x.id == pl.objectiveId);
                    if (o != null) o.anchorId = pl.anchorId;
                }
                result = MissionValidator.Validate(fallback, Room, cfg, allowRepair: true);
                planSource = "fallback_template";
                ZeroLog.Warn("Validator", $"template: {result}", -1, "plan_rejected");
            }

            if (!result.ok)
            {
                Machine.TransitionTo(MissionStateId.ERROR, "no valid plan");
                ShowPanel(() => panel.Show("ZERO", "I can't find a safe spot within reach. Clear your desk area or rescan your room, then try again.",
                    new UiAction("start", "TRY AGAIN", Palette.Button, () => { Machine.TransitionTo(MissionStateId.ROOM_READY); ShowReady(); })));
                return;
            }

            mission = result.mission;
            Safe(BriefAsync());
        }

        // ================================================================== briefing

        async Task BriefAsync()
        {
            Machine.TransitionTo(MissionStateId.BRIEFING, planSource);
            string beat = mission.npcBeatIds.Contains("zero_treasure_intro") ? "zero_treasure_intro"
                        : mission.npcBeatIds.Contains("zero_greenhouse_intro") ? "zero_greenhouse_intro"
                        : mission.npcBeatIds.Contains("zero_genelab_intro") ? "zero_genelab_intro"
                        : mission.npcBeatIds.Contains("zero_return_loud") ? "zero_return_loud"
                        : mission.npcBeatIds.Contains("zero_return_silent") ? "zero_return_silent" : "zero_intro";
            var line = await NpcLineAsync(beat);
            RoomZero.Audio.Voice.Say("zero", line.text);
            if (!this || !Machine.Is(MissionStateId.BRIEFING)) return;

            int room = Campaign.RoomNumber(mission.template);
            string title = room > 0
                ? $"ROOM {room} OF {Campaign.Rooms.Length}  ·  {Campaign.RoomName(mission.template)}  ·  LEVEL {mission.difficulty}"
                : $"{mission.template.ToString().Replace('_', ' ')}  ·  LEVEL {mission.difficulty}";
            string body = $"ZERO: \"{line.text}\"\n\n{mission.plan.briefing}\n\n" +
                          "You play, Dr. Asha guides you and gives hints. Press J for a demo where she solves it.\n" +
                          "SILENT: full timer, calmer guard.   LOUD: less time, angry guard.";
            ShowPanel(() => panel.Show(title, body,
                new UiAction("route_silent", "SILENT", Palette.Button, () => ChooseRoute(RouteChoice.SILENT_ROUTE)),
                new UiAction("route_loud", "LOUD", Palette.Danger * 0.55f + Color.black * 0.45f, () => ChooseRoute(RouteChoice.LOUD_ROUTE))));
        }

        void ChooseRoute(RouteChoice route)
        {
            if (!Machine.Is(MissionStateId.BRIEFING) || paused) return;
            if (!Machine.TransitionTo(MissionStateId.ACTIVE, route.ToString())) return;
            PokeTarget.InputBlocked = false;   // the mission starts unpaused: touches must work

            float startAlert = snapshot.HasLastSession && snapshot.lastSession.choice == nameof(RouteChoice.LOUD_ROUTE) ? 0.15f : 0f;
            panel.Hide();
            restorePanel = null;
            runtime.Begin(mission, Room, route, startAlert);
            hud.Show();
            if (ScientistNpc.Instance) ScientistNpc.Instance.Greet(Campaign.AshaIntro(mission.template));
            else ScientistNpc.Spawn(runtime, Room, Campaign.AshaIntro(mission.template), (who, text) => hud.Say(who, text));
            if (ScientistNpc.Instance) ScientistNpc.Instance.Mentor = DecideMentorAsync;
            nextAdaptationCheck = Time.time + cfg.adaptationInterval;
            lastAdaptationAt = -999f;

            Safe(SpeakAsync("guard_intro"));
            // The guard reacts to the choice the player just made (demo beat 1:00–1:35).
            Safe(SpeakAsync(route == RouteChoice.LOUD_ROUTE ? "guard_react_loud" : "guard_react_silent", 3000));
        }

        // ================================================================== active + adaptation

        void Update()
        {
            Machine.Tick();
            if (!runtime || !runtime.Running) return;
            hud.SetStatus(runtime.SecondsLeft, runtime.ObjectivesComplete, runtime.Mission.objectives.Count, runtime.Guard ? runtime.Guard.Alertness : 0);
            hud.SetGuide(runtime.GuideText(head));

            if (Machine.Is(MissionStateId.ACTIVE) && !paused && Time.time >= nextAdaptationCheck)
            {
                nextAdaptationCheck = Time.time + cfg.adaptationInterval;
                if (Time.time - lastAdaptationAt >= cfg.adaptationCooldown && runtime.AppliedAdaptations.Count < cfg.maxAdaptationsPerMission)
                    Safe(AdaptAsync());
            }
        }

        async Task AdaptAsync()
        {
            Machine.TransitionTo(MissionStateId.ADAPTING);
            var input = new AdaptationInput { signals = runtime.Signals };
            input.alreadyApplied.AddRange(runtime.AppliedAdaptations);

            var proposal = await Call("Adaptation",
                remoteAdaptation == null ? null : (Func<CancellationToken, Task<AdaptationProposal>>)(ct => remoteAdaptation.ProposeAsync(input, ct)),
                () => LocalAdaptationAgent.Propose(input),
                p => EnumParse.TryParse(p.action, out AdaptationAction _));

            if (!this || !Machine.Is(MissionStateId.ADAPTING)) return; // mission ended while we waited

            EnumParse.TryParse(proposal.action, out AdaptationAction action);
            if (action != AdaptationAction.NONE && !input.alreadyApplied.Contains(action.ToString()))
            {
                var (ok, why) = runtime.ApplyCommand(new GameCommand
                {
                    type = GameCommandType.APPLY_ADAPTATION, missionId = mission.MissionId, adaptation = action, amount = proposal.amount
                });
                if (ok)
                {
                    lastAdaptationAt = Time.time;
                    ZeroLog.Info("Adaptation", $"{action} amount={proposal.amount} reason={proposal.reason}");
                    hud.Say("ZERO", Describe(action));
                }
                else ZeroLog.Warn("Adaptation", $"{action} rejected by runtime: {why}");
            }
            Machine.TransitionTo(MissionStateId.ACTIVE, "adapted");
        }

        static string Describe(AdaptationAction a)
        {
            switch (a)
            {
                case AdaptationAction.OFFER_HINT: return "You're circling. I've highlighted what matters.";
                case AdaptationAction.EXTEND_TIMER: return "I bought you extra time on their clock.";
                case AdaptationAction.LOWER_GUARD_ALERTNESS: return "I looped the camera feed. The drone is calming down.";
                case AdaptationAction.RAISE_GUARD_ALERTNESS: return "You're fast. Let's see you handle a nervous drone.";
                default: return "";
            }
        }

        // ================================================================== end -> save -> next

        void OnMissionEnded(MissionOutcome outcome)
        {
            if (!Machine.TransitionTo(MissionStateId.COMPLETE, outcome.ToString())) return;
            hud.Hide();
            RoomZero.Audio.Sfx.Play(outcome == MissionOutcome.SUCCESS ? "fanfare" : "error");
            lastTemplate = mission.template;
            lastSuccess = outcome == MissionOutcome.SUCCESS;
            if (ScientistNpc.Instance) ScientistNpc.Instance.OnMissionEnded(lastSuccess);
            if (outcome == MissionOutcome.SUCCESS)
            {
                RoomZero.Game.Confetti.Spawn(head.position + Flat(head.forward) * 0.9f + Vector3.up * 0.1f);
                DesktopOverlay.Celebrate(Campaign.IsFinal(mission.template) ? "CAMPAIGN COMPLETE!" : "CONGRATULATIONS!", Campaign.WinLine(mission.template));
            }
            Safe(SpeakAsync(outcome == MissionOutcome.SUCCESS ? "zero_praise" : "zero_fail"));

            var summary = new SessionSummary
            {
                sessionId = ZeroLog.SessionId,
                missionId = mission.MissionId,
                templateId = mission.template.ToString(),
                choice = runtime.Route.ToString(),
                outcome = outcome.ToString(),
                completed = outcome == MissionOutcome.SUCCESS,
                retries = runtime.FailedAttempts,
                hintsUsed = runtime.HintsUsed,
                seconds = Mathf.Round(runtime.Elapsed),
                difficulty = mission.difficulty,
                adaptations = new System.Collections.Generic.List<string>(runtime.AppliedAdaptations),
                timestampUtc = DateTime.UtcNow.ToString("o")
            };
            summary.summary = $"{Pretty(summary.choice)} run, {summary.outcome.ToLowerInvariant()}, {summary.seconds}s, {summary.retries} retries, {summary.hintsUsed} hints";

            Machine.TransitionTo(MissionStateId.SAVING);
            var updated = playerModel.Apply(memory.Profile, summary);
            bool saved = memory.Save(updated);        // never throws; never blocks mission end
            AgentTrace.Log("Memory", false, $"remembered: {summary.summary}; next level {updated.stats.recentDifficulty}");
            Machine.TransitionTo(MissionStateId.READY_FOR_NEXT_SESSION, saved ? "saved" : "save_failed");
            // Win: let the CONGRATULATIONS moment play on its own first, then show the results.
            if (outcome == MissionOutcome.SUCCESS) Safe(ShowResultsAfter(DesktopOverlay.CelebrationSeconds, outcome, summary, updated, saved));
            else ShowResults(outcome, summary, updated, saved);
        }

        async Task ShowResultsAfter(float seconds, MissionOutcome outcome, SessionSummary s, PlayerProfile p, bool saved)
        {
            await Task.Delay(Mathf.RoundToInt(seconds * 1000));
            if (!this || !Machine.Is(MissionStateId.READY_FOR_NEXT_SESSION)) return;   // player already moved on
            ShowResults(outcome, s, p, saved);
        }

        void ShowResults(MissionOutcome outcome, SessionSummary s, PlayerProfile p, bool saved)
        {
            bool hunt = s.templateId == nameof(MissionTemplateId.TREASURE_HUNT);
            EnumParse.TryParse(s.templateId, out MissionTemplateId played);
            int room = Campaign.RoomNumber(played);
            bool win = outcome == MissionOutcome.SUCCESS;
            if (room > 0)
            {
                ShowCampaignResults(played, room, win, s, p, saved);
                return;
            }
            string title = outcome == MissionOutcome.SUCCESS ? (hunt ? "FORMULA RECOVERED" : "MISSION COMPLETE") : outcome == MissionOutcome.FAILED_CAUGHT ? "CAUGHT" : outcome == MissionOutcome.ABANDONED ? "MISSION ABORTED" : "OUT OF TIME";
            string memoryLine = s.choice == nameof(RouteChoice.LOUD_ROUTE)
                ? "ZERO will remember you went LOUD: next run they install a sensor grid and the drone starts nervous."
                : s.choice == nameof(RouteChoice.SILENT_ROUTE)
                    ? "ZERO will remember your SILENT run: next run they move the vault and change the code."
                    : "";
            if (hunt)
                memoryLine = outcome == MissionOutcome.SUCCESS
                    ? "The Harvest Formula is safe: a crop with a bigger, more sustainable harvest and more protein, for children who need it. ZERO will hide the next set somewhere new.\n" + memoryLine
                    : "The formula is still out there. Children are waiting on this harvest. Try again.\n" + memoryLine;
            string body = $"{s.summary}.\nAdaptations: {(s.adaptations.Count == 0 ? "none" : string.Join(", ", s.adaptations))}\n" +
                          $"{memoryLine}\nNext difficulty: {p.stats.recentDifficulty}.  {(saved ? "Saved." : "Save failed: kept for this session.")}";
            ShowPanel(() => panel.Show(title, body,
                new UiAction("next", "NEXT HEIST", Palette.Button, () => EventBus.Publish(new NextMissionRequested())),
                new UiAction("next_hunt", "FORMULA HUNT", Palette.Accent * 0.6f + Color.black * 0.4f, StartTreasureHunt),
                new UiAction("sys_reset", "RESET MEMORY", Palette.Muted * 0.5f + Color.black * 0.5f, ResetMemory)));
        }

        /// Dr. Asha asks this when deciding how to help (AI model if connected, rules otherwise; always validated).
        public async Task<MentorDecision> DecideMentorAsync(MentorInput input)
        {
            return await Call("Mentor",
                remoteMentor == null ? null : (Func<CancellationToken, Task<MentorDecision>>)(ct => remoteMentor.DecideAsync(input, ct)),
                () => LocalMentorAgent.Decide(input),
                d => LocalMentorAgent.IsValid(d, input));
        }

        /// Room music (quieter while outside the lab doors).
        void PlayMusic(LabTheme theme, float loudness)
        {
            if (!cfg) return;
            var clip = theme == LabTheme.Greenhouse ? cfg.musicGreenhouse : theme == LabTheme.GeneLab ? cfg.musicGeneLab : cfg.musicVaultLab;
            RoomZero.Audio.Music.Play(clip, cfg.musicVolume * loudness);
        }

        /// Room 1 entrance: lab dark behind bio-secure doors, palm scanner on the desk.
        void StartEntry(MissionRequested e)
        {
            PlayMusic(LabTheme.VaultLab, 0.35f);
            ScientistNpc.Despawn();
            runtime.Cleanup();
            if (RoomDebugView.Instance) RoomDebugView.Instance.SetTheme(LabTheme.VaultLab);
            panel.Hide();
            restorePanel = null;
            hud.Show();
            hud.SetStatusText("ROOM 1 · BIO-SECURE ENTRY");
            hud.SetGuide("place your hand on the glowing PALM SCANNER in front of you.");
            hud.Say("ZERO", "This is the bio-secure seed lab. Scan your palm and the doors will open.");
            var source = e.source;
            EntryGate.Show(Room, () =>
            {
                hud.Hide();
                PlayMusic(LabTheme.VaultLab, 1f);
                // Dr. Asha steps through the opening doors while ZERO plans the mission.
                ScientistNpc.SpawnThroughDoors(runtime, Room, "Good morning! I'm Dr. Asha. Come in, the seed vault is this way.", (who, text) => hud.Say(who, text));
                keepAsha = true;
                entryPassed = true;
                EventBus.Publish(new MissionRequested { source = source, template = nameof(MissionTemplateId.TREASURE_HUNT) });
            });
        }

        /// Campaign results: room cleared -> NEXT ROOM; last room -> the whole story's payoff.
        void ShowCampaignResults(MissionTemplateId played, int room, bool win, SessionSummary s, PlayerProfile p, bool saved)
        {
            string stats = $"{s.summary}.  {(saved ? "Saved." : "Save failed: kept for this session.")}";
            if (!win)
            {
                ShowPanel(() => panel.Show($"ROOM {room}: TRY AGAIN", $"The crop still needs you.\n{stats}",
                    new UiAction("retry_room", "RETRY ROOM", Palette.Accent * 0.6f + Color.black * 0.4f, () => StartRoom(played)),
                    new UiAction("next_hunt", "ROOM 1", Palette.Button, StartTreasureHunt),
                    new UiAction("sys_reset", "RESET MEMORY", Palette.Muted * 0.5f + Color.black * 0.5f, ResetMemory)));
                return;
            }
            if (Campaign.TryNext(played, out var next))
            {
                ShowPanel(() => panel.Show($"ROOM {room} CLEARED", $"{Campaign.WinLine(played)}\nNext: Room {room + 1}, the {Campaign.RoomName(next)}. Dr. Asha is heading there now.\n{stats}",
                    new UiAction("next_room", $"NEXT ROOM", Palette.Success * 0.6f + Color.black * 0.4f, () => StartRoom(next)),
                    new UiAction("retry_room", "REPLAY", Palette.Button, () => StartRoom(played)),
                    new UiAction("sys_reset", "RESET MEMORY", Palette.Muted * 0.5f + Color.black * 0.5f, ResetMemory)));
                return;
            }
            ShowPanel(() => panel.Show("HARVEST FORMULA COMPLETE",
                "Seed recovered, crop grown, gene repaired. This drought-proof, high-protein crop can feed children where food is scarce.\n" +
                $"Next difficulty: {p.stats.recentDifficulty}.  {stats}",
                new UiAction("next_hunt", "PLAY AGAIN", Palette.Accent * 0.6f + Color.black * 0.4f, StartTreasureHunt),
                new UiAction("next", "CLASSIC HEIST", Palette.Button, () => EventBus.Publish(new MissionRequested { source = InputSource.Ui })),
                new UiAction("sys_reset", "RESET MEMORY", Palette.Muted * 0.5f + Color.black * 0.5f, ResetMemory)));
        }

        void StartRoom(MissionTemplateId t) =>
            EventBus.Publish(new MissionRequested { source = InputSource.Ui, template = t.ToString() });

        /// "Next" (N key / voice): next campaign room after a win, retry after a loss, else a classic heist.
        string NextTemplate()
        {
            if (!Campaign.IsCampaign(lastTemplate)) return null;
            if (!lastSuccess) return lastTemplate.ToString();
            return Campaign.TryNext(lastTemplate, out var next) ? next.ToString() : nameof(MissionTemplateId.TREASURE_HUNT);
        }

        void StartTreasureHunt() =>
            EventBus.Publish(new MissionRequested { source = InputSource.Ui, template = nameof(MissionTemplateId.TREASURE_HUNT) });

        /// ZERO comments when a relic is collected: its number, how many remain.
        void OnObjectiveDone(string id)
        {
            var o = mission?.Find(id);
            if (o == null || o.type != ObjectiveType.HIDDEN_RELIC) return;
            int left = runtime.RelicIds().FindAll(r => { var b = runtime.Objective(r); return !(b && b.Completed); }).Count;
            string msg = left > 0
                ? $"Fragment {runtime.RelicIndex(id)} of the Harvest Formula: number {runtime.RelicDigit(id)}. {left} to go. Follow the clue."
                : $"All fragments recovered. Vault code {runtime.KnownComboText()}. Unlock the seed vault.";
            hud.Say("ZERO", msg);
        }

        void ResetMemory()
        {
            ScientistNpc.Despawn();
            EntryGate.Close();
            if (RoomDebugView.Instance) RoomDebugView.Instance.HideEntrance();
            memory.Reset();
            if (Machine.Is(MissionStateId.READY_FOR_NEXT_SESSION)) Machine.TransitionTo(MissionStateId.ROOM_READY, "reset");
            if (Machine.Is(MissionStateId.ROOM_READY)) ShowReady();
        }

        // ================================================================== pause / tracking loss

        void SetPaused(bool value, string reason)
        {
            // Keep the touch block in step with the real pause state even if it got out of sync.
            if (value == paused) { PokeTarget.InputBlocked = value; return; }
            paused = value;
            pauseReason = reason;
            runtime.Paused = value;
            PokeTarget.InputBlocked = value;
            Machine.Hold(value);
            ZeroLog.Info("Pause", value ? $"paused ({reason})" : "resumed");

            if (value)
            {
                bool inMission = runtime.Running;
                if (inMission) hud.Hide();
                string body = reason == "tracking_lost"
                    ? "Hands not visible. Bring them back into view and the game resumes by itself. You can also say \"resume\"."
                    : "Take a break. Everything stays where it is.";
                var buttons = inMission
                    ? new UiAction[] { new UiAction("sys_resume", "RESUME", Palette.Button, Resume), new UiAction("sys_end", "END MISSION", Palette.Danger * 0.5f + Color.black * 0.5f, AbortMission) }
                    : new UiAction[] { new UiAction("sys_resume", "RESUME", Palette.Button, Resume) };
                panel.Show("PAUSED", body, buttons);
            }
            else
            {
                if (runtime.Running) { panel.Hide(); hud.Show(); }
                else if (restorePanel != null) restorePanel();
                else panel.Hide();
            }
        }

        void Resume() => SetPaused(false, "ui");

        void AbortMission()
        {
            SetPaused(false, "abort");
            runtime.ApplyCommand(new GameCommand { type = GameCommandType.END_MISSION, missionId = mission?.MissionId });
        }

        void OnHandTracking(HandTrackingChanged e)
        {
            if (!e.tracked && !paused) SetPaused(true, "tracking_lost");
            else if (e.tracked && paused && pauseReason == "tracking_lost") SetPaused(false, "tracking_back");
        }

        // ================================================================== timeouts

        void OnStateTimeout(MissionStateId state)
        {
            switch (state)
            {
                case MissionStateId.BOOT:
                    Room = roomScout.Current ?? Room;
                    if (Room == null) { Machine.TransitionTo(MissionStateId.ERROR, "boot timeout"); return; }
                    Machine.TransitionTo(MissionStateId.ROOM_READY, "boot timeout");
                    ShowReady();
                    break;
                case MissionStateId.PLANNING:
                    // Agents too slow: go straight to the deterministic template.
                    Validate(new MissionPlan { schemaVersion = -1 }); // forces the template path
                    break;
                case MissionStateId.BRIEFING:
                    ChooseRoute(RouteChoice.SILENT_ROUTE);
                    break;
                case MissionStateId.ADAPTING:
                    Machine.TransitionTo(MissionStateId.ACTIVE, "adapt timeout");
                    break;
                case MissionStateId.ERROR:
                    Machine.TransitionTo(MissionStateId.ROOM_READY, "recover");
                    ShowReady();
                    break;
            }
        }

        // ================================================================== NPC

        async Task SpeakAsync(string beat, int delayMs = 0)
        {
            if (delayMs > 0) await Task.Delay(delayMs);
            if (!this) return;
            var line = await NpcLineAsync(beat);
            if (!this || !runtime) return;
            if (beat.StartsWith("guard_") && runtime.Guard)
            {
                runtime.Guard.Say(line.text);
                if (RoomZeroBootstrap.DesktopMode) hud.Say("DRONE", line.text); // readable copy on the 2D HUD
            }
            else if (runtime.Running) hud.Say("ZERO", line.text);
        }

        async Task<NpcLine> NpcLineAsync(string beat)
        {
            var input = new NpcInput
            {
                beatId = beat,
                missionState = Machine.State.ToString(),
                route = runtime ? runtime.Route.ToString() : "NONE",
                guardAlertness = runtime && runtime.Guard ? runtime.Guard.Alertness : 0f,
                profile = snapshot
            };
            return await Call("NPC",
                remoteNpc == null ? null : (Func<CancellationToken, Task<NpcLine>>)(ct => remoteNpc.GetLineAsync(input, ct)),
                () => new NpcLine { beatId = beat, intent = NpcCatalog.DefaultIntent(beat).ToString(), text = NpcCatalog.FallbackText(beat) },
                l => IsSafeLine(l, beat));
        }

        static bool IsSafeLine(NpcLine l, string beat)
        {
            if (l == null || l.beatId != beat || string.IsNullOrWhiteSpace(l.text)) return false;
            if (l.text.Length > NpcCatalog.MaxLineLength) return false;
            if (!EnumParse.TryParse(l.intent, out NpcIntent _)) return false;
            foreach (char c in l.text) if (char.IsControl(c)) return false;
            return true;
        }

        // ================================================================== agent call w/ timeout + fallback

        async Task<T> Call<T>(string agent, Func<CancellationToken, Task<T>> remote, Func<T> local, Func<T, bool> isValid) where T : class
        {
            var sw = Stopwatch.StartNew();
            string reason = "local_mode";
            lastCallRemoteOk = false;
            if (remote != null)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                try
                {
                    var task = remote(cts.Token);
                    var done = await Task.WhenAny(task, Task.Delay(TimeoutFor(agent)));
                    if (done == task)
                    {
                        var value = await task;
                        if (value != null && isValid(value))
                        {
                            ZeroLog.Info(agent, "remote ok", sw.ElapsedMilliseconds);
                            lastCallRemoteOk = true;
                            AgentTrace.Log(agent, true, AgentTrace.Summarize(value));
                            return value;
                        }
                        reason = "invalid_output";
                    }
                    else { cts.Cancel(); reason = "timeout"; }
                }
                catch (Exception e) { reason = "error:" + e.Message; }
                finally { cts.Dispose(); }
            }
            var fallback = local();
            AgentTrace.Log(agent, false, AgentTrace.Summarize(fallback) + (remote != null ? $"  [AI {reason.Split(':')[0]}: local fallback]" : ""));
            if (remote != null) ZeroLog.Warn(agent, "using local agent", sw.ElapsedMilliseconds, reason);
            else ZeroLog.Info(agent, "local", sw.ElapsedMilliseconds);
            return fallback;
        }

        /// Planning happens once, behind the "Planning your heist" panel, so a local model may take longer there.
        /// In-play decisions (mentor, NPC, adaptation) keep the configured, snappier limit.
        int TimeoutFor(string agent)
        {
            if (agent == "Director" || agent == "PuzzleArchitect") return Mathf.Max(cfg.agentTimeoutMs, 15000);
            return cfg.agentTimeoutMs;
        }

        // ================================================================== helpers

        /// Fire-and-forget with logging: an exception in an async step must never vanish.
        async void Safe(Task task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception e) { ZeroLog.Error("ZERO", e.ToString()); }
        }

        void ShowPanel(Action show)
        {
            restorePanel = show;
            if (!paused) show();
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized; }

        static string Pretty(string route) =>
            route == nameof(RouteChoice.LOUD_ROUTE) ? "LOUD" : route == nameof(RouteChoice.SILENT_ROUTE) ? "SILENT" : "no route";
    }
}
