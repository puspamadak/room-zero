// Dr. Asha: the scientist who walks into each room and solves its challenges, one correct move at a time,
// explaining what she does. The player can take over at ANY moment just by touching a control
// (hands-first: the experience stays fully playable end to end with hands alone):
//   - player touches a control  -> she steps back ("Your turn!") and watches that objective
//   - player idle for 15 s       -> she offers to carry on and resumes
//   - desktop J key              -> toggle her help on/off
// She only ever calls ObjectiveBehaviour.RunAutoStep(), so every move goes through the same
// deterministic game rules as the player's moves. Built entirely from code-generated shapes.
// Owner: #6 Game Systems (behaviour) + #3 art (look).

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using RoomZero.UI;
using RoomZero.Zero;

namespace RoomZero.Game
{
    public class ScientistNpc : MonoBehaviour
    {
        public static ScientistNpc Instance { get; private set; }

        enum Mode { Walking, Guiding, Working, Watching, Celebrating, Leaving }

        const float WalkSpeed = 0.8f;

        GameRuntime runtime;
        Action<string, string> say;
        Mode mode = Mode.Walking, afterWalk = Mode.Guiding;
        readonly List<Vector3> path = new List<Vector3>();
        ObjectiveBehaviour working;
        float nextMove, walkPhase, celebrateUntil, bubbleUntil, tap, waveUntil;
        /// Demo mode (J): she solves the challenges herself. Off by default: the player plays, she guides.
        public static bool DemoMode;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { DemoMode = false; Instance = null; }

        /// Her judgement: the Mentor agent (AI model if connected, rules otherwise), set by ZERO.
        public Func<MentorInput, Task<MentorDecision>> Mentor;
        float lastProgress, nextDecision, lastHelpAt = -99f, showStepAt = -1f;
        int hintsGiven, failsAtStart;
        bool deciding, justSolved;
        Vector3 origin, fwd, right, door;

        Transform body, head, shoulderL, shoulderR, elbowL, elbowR, legL, legR;
        TextMesh bubble;

        // ------------------------------------------------------------------ spawning

        public static ScientistNpc Spawn(GameRuntime rt, RoomMap room, string introLine, Action<string, string> say)
        {
            Despawn();
            var go = new GameObject("DrAsha");
            var npc = go.AddComponent<ScientistNpc>();
            npc.runtime = rt;
            npc.say = say;
            npc.SetupFrame(room);
            go.transform.position = npc.door;
            go.transform.rotation = Quaternion.LookRotation(npc.right, Vector3.up);
            if (!npc.BuildModel()) npc.BuildBody();
            go.transform.rotation = Quaternion.LookRotation(npc.right, Vector3.up);
            var win = RoomZero.Spatial.LabWindow.Current;
            if (win)
            {
                // Step out of the window towards you, stopping at a comfortable distance.
                go.transform.rotation = Quaternion.LookRotation(win.Normal, Vector3.up);
                Vector3 to = npc.origin - npc.door; to.y = 0;
                float stop = Mathf.Max(0.3f, to.magnitude - 1.1f);
                npc.path.Add(npc.door + to.normalized * stop);
            }
            else npc.path.Add(npc.P(-0.95f, 0.92f));
            npc.Speak(introLine);
            RoomZero.Audio.Sfx.Play("chime", 0.6f);
            Instance = npc;
            return npc;
        }

        /// Room 1: she walks in through the bio-secure doors as they open, then waits by the desk.
        public static ScientistNpc SpawnThroughDoors(GameRuntime rt, RoomMap room, string line, Action<string, string> say)
        {
            var npc = Spawn(rt, room, line, say);
            if (RoomZero.Spatial.LabWindow.Current) { npc.waveUntil = Time.time + 6f; return npc; }   // MR: from the window
            npc.transform.position = npc.P(0f, 1.43f);
            npc.transform.rotation = Quaternion.LookRotation(-npc.fwd, Vector3.up);
            npc.path.Clear();
            npc.path.Add(npc.P(0.05f, 1.1f));
            npc.path.Add(npc.P(0.42f, 1.02f));
            npc.waveUntil = Time.time + 6f;
            return npc;
        }

        /// Say hello again (she's already in the room when the mission starts).
        public void Greet(string line)
        {
            Speak(line);
            waveUntil = Time.time + 2.5f;
        }

        public static void Despawn()
        {
            if (Instance) Destroy(Instance.gameObject);
            Instance = null;
        }

        void OnEnable() => EventBus.Subscribe<HandAction>(OnHandAction);
        void OnDisable() => EventBus.Unsubscribe<HandAction>(OnHandAction);
        void OnDestroy() { if (Instance == this) Instance = null; }

        void SetupFrame(RoomMap room)
        {
            origin = room.PlayerOrigin; origin.y = 0f;
            var desk = room.FindSurface("surface-desk");
            var rot = Quaternion.Euler(0, desk != null ? desk.yawDegrees : 0f, 0);
            fwd = rot * Vector3.forward; right = rot * Vector3.right;
            door = P(-1.72f, 0.5f);   // the drawn door in every desktop room
            if (room.source != null && room.source.StartsWith("MOCK")) return;
            foreach (var zid in room.excludedZoneIds)   // real room: use the scanned door if there is one
            {
                var z = room.FindZone(zid);
                if (z != null && zid.Contains("door")) { door = new Vector3(z.Bounds.center.x, 0, z.Bounds.center.z); break; }
            }
            // Mixed reality: she steps out of the lab window on your wall.
            var win = RoomZero.Spatial.LabWindow.Current;
            if (win) door = win.EntryPoint;
        }

        Vector3 P(float x, float z) => origin + right * x + fwd * z;   // plan (x right, z forward) -> floor point

        // ------------------------------------------------------------------ player take-over

        void OnHandAction(HandAction a)
        {
            if (a.targetId != null && a.targetId.StartsWith("sys_")) return;
            if (runtime && runtime.Running) PlayerActed();
        }

        /// Called for any player input on a challenge (hand poke, mouse click, desktop keyboard).
        public void PlayerActed()
        {
            lastProgress = Time.time;
            if (mode == Mode.Working)
            {
                DemoMode = false;
                mode = Mode.Guiding;
                Speak("Your turn! I'll guide you.");
            }
            else if (mode == Mode.Walking && afterWalk == Mode.Working) { DemoMode = false; afterWalk = Mode.Guiding; }
        }

        /// Desktop J: demo mode on/off (she solves it for you / you play and she guides).
        public void ToggleHelp()
        {
            DemoMode = !DemoMode;
            if (DemoMode) { if (mode == Mode.Guiding || mode == Mode.Watching) { mode = Mode.Working; nextMove = Time.time + 0.5f; } else if (mode == Mode.Walking) afterWalk = Mode.Working; Speak("Demo mode: watch me solve it. Touch anything to take over."); }
            else { if (mode == Mode.Working) mode = Mode.Guiding; else if (mode == Mode.Walking) afterWalk = Mode.Guiding; Speak("Your turn. I'll guide you."); lastProgress = Time.time; }
        }

        public void OnMissionEnded(bool success)
        {
            mode = Mode.Celebrating;
            celebrateUntil = Time.time + (success ? 3f : 1.5f);
            Speak(success ? "We did it! That's one step closer to feeding those kids." : "Not this time. We'll be back.");
        }

        // ------------------------------------------------------------------ behaviour

        bool reported, explained, inspected, thoughtThisStep;
        int steps;

        void Update()
        {
            try { Tick(); }
            catch (Exception e)
            {
                // Dr. Asha must never freeze the game: log once as a warning and keep going.
                if (!reported) { reported = true; Debug.LogWarning("[RZ] Dr. Asha skipped a step: " + e.Message); }
            }
        }

        void Tick()
        {
            if (!runtime) return;
            float dt = Time.deltaTime;
            if (bubble && Time.time > bubbleUntil) bubble.text = "";

            switch (mode)
            {
                case Mode.Celebrating:
                    if (Time.time > celebrateUntil)
                    {
                        mode = Mode.Leaving;
                        path.Clear(); path.Add(P(-0.95f, 0.92f)); path.Add(door);
                    }
                    break;

                case Mode.Leaving:
                    if (FollowPath(dt)) { Destroy(gameObject); return; }
                    break;

                case Mode.Walking:
                    if (FollowPath(dt)) { mode = afterWalk; nextMove = Time.time + 0.6f; }
                    break;

                default:
                    if (!runtime.Running) break;
                    var target = runtime.CurrentTarget;
                    if (target != working)
                    {
                        // The player finished the previous one themselves -> worth a word of praise.
                        if (working && working.Completed && (mode == Mode.Guiding || mode == Mode.Watching)) justSolved = true;
                        working = target;
                        if (!target) break;
                        GoTo(StandPoint(target));
                        afterWalk = DemoMode ? Mode.Working : Mode.Guiding;
                        explained = false;
                        inspected = false;
                        steps = 0;
                        break;
                    }
                    if (!target) break;
                    Face(target.transform.position, dt);
                    if (mode == Mode.Guiding || mode == Mode.Watching)
                    {
                        // Explain each challenge once, then hint if the player seems stuck.
                        if (!explained)
                        {
                            explained = true;
                            hintsGiven = 0;
                            failsAtStart = runtime.FailedAttempts;
                            lastProgress = Time.time;
                            nextDecision = Time.time + 6f;
                            if (justSolved) { justSolved = false; Speak(VoiceLines.Encourage[UnityEngine.Random.Range(0, VoiceLines.Encourage.Length)]); }
                            Speak(VoiceLines.AshaTurn(target.Data.type));
                        }
                        else if (showStepAt > 0f && Time.time >= showStepAt && RoomZero.Audio.Voice.Remaining("asha") < 0.15f)
                        {
                            // SHOW_ONE_STEP: demonstrate exactly one move, then hand it back.
                            showStepAt = -1f;
                            target.RunAutoStep();
                            tap = 1f;
                            Speak(VoiceLines.AfterShowStep);
                            lastProgress = Time.time;
                        }
                        else if (Time.time >= nextDecision && !deciding && RoomZero.Audio.Voice.Remaining("asha") < 0.15f)
                            AskMentor(target);
                    }
                    if (mode == Mode.Working && !runtime.Paused && Time.time >= nextMove && RoomZero.Audio.Voice.Remaining("asha") < 0.15f)
                    {
                        // Like a person: look first, think now and then, act, then let the result sink in.
                        if (!inspected)
                        {
                            inspected = true;
                            Speak(VoiceLines.LookFirst);
                            nextMove = Time.time + 2.4f;
                        }
                        else if (steps > 0 && steps % 4 == 0 && !thoughtThisStep)
                        {
                            thoughtThisStep = true;
                            Speak(VoiceLines.Thinking[(steps / 4) % VoiceLines.Thinking.Length]);
                            nextMove = Time.time + 1.6f;
                        }
                        else
                        {
                            string line = target.RunAutoStep();
                            tap = 1f;
                            steps++;
                            thoughtThisStep = false;
                            if (!string.IsNullOrEmpty(line)) Speak(line);
                            float voice = RoomZero.Audio.Voice.Remaining("asha");
                            nextMove = Time.time + Mathf.Max(target.AutoInterval * 2.2f, voice + 0.5f) + UnityEngine.Random.Range(0.2f, 0.9f);
                        }
                    }
                    break;
            }
            Animate(dt);
        }

        Vector3 StandPoint(ObjectiveBehaviour t)
        {
            Vector3 d = t.transform.position - origin;
            float tx = Vector3.Dot(d, right), tz = Vector3.Dot(d, fwd);
            float side = tx > 0.15f ? 1f : -1f;                   // stand beside, never in front of the puzzle
            return P(tx + side * 0.3f, Mathf.Min(tz + 0.48f, 1.3f));
        }

        void GoTo(Vector3 p)
        {
            path.Clear();
            path.Add(p);
            mode = Mode.Walking;
        }

        bool FollowPath(float dt)
        {
            if (path.Count == 0) return true;
            Vector3 pos = transform.position, goal = path[0];
            Vector3 to = goal - pos; to.y = 0;
            float step = WalkSpeed * dt;
            if (to.magnitude <= step)
            {
                transform.position = new Vector3(goal.x, pos.y, goal.z);
                path.RemoveAt(0);
                return path.Count == 0;
            }
            transform.position = pos + to.normalized * step;
            Face(goal, dt * 2f);
            walkPhase += dt * 7f;
            return false;
        }

        void Face(Vector3 p, float dt)
        {
            Vector3 d = p - transform.position; d.y = 0;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), Mathf.Clamp01(dt * 5f));
        }

        void Speak(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            say?.Invoke("DR. ASHA", line);
            if (bubble) { bubble.text = UiFactory.Wrap(line, 34); bubbleUntil = Time.time + 3.5f; }
        }

        // ------------------------------------------------------------------ animation

        void Animate(float dt)
        {
            if (anim) { AnimateModel(); return; }
            float k = Mathf.Clamp01(dt * 8f);
            bool walking = mode == Mode.Walking || mode == Mode.Leaving;
            float swing = walking ? Mathf.Sin(walkPhase) : 0f;
            tap = Mathf.Max(0, tap - dt * 3f);

            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(swing * 26f, 0, 0), k);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-swing * 26f, 0, 0), k);
            float bob = walking ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.02f : Mathf.Sin(Time.time * 1.6f) * 0.004f;

            Quaternion sl, sr, el, er;
            if (mode == Mode.Celebrating)
            {
                float hop = Mathf.Abs(Mathf.Sin(Time.time * 7f));
                bob = hop * 0.06f;
                sl = Quaternion.Euler(0, 0, -150f - hop * 15f); sr = Quaternion.Euler(0, 0, 150f + hop * 15f);
                el = er = Quaternion.Euler(-10, 0, 0);
            }
            else if (mode == Mode.Working && working)
            {
                // Right hand reaches for the control she's using; left hand holds her tablet.
                Vector3 dir = (working.WorkPoint - shoulderR.position).normalized;
                sr = Quaternion.Inverse(transform.rotation) * Quaternion.FromToRotation(Vector3.down, dir);
                er = Quaternion.Euler(-12f - tap * 25f, 0, 0);
                sl = Quaternion.Euler(-40, 0, -8); el = Quaternion.Euler(-75, 0, 0);
            }
            else if (mode == Mode.Watching || mode == Mode.Guiding)
            {
                sl = Quaternion.Euler(-35, 0, -12); el = Quaternion.Euler(-95, 25, 0);   // arms folded
                sr = Quaternion.Euler(-35, 0, 12); er = Quaternion.Euler(-95, -25, 0);
            }
            else
            {
                sl = Quaternion.Euler(-swing * 22f, 0, -4); sr = Quaternion.Euler(swing * 22f, 0, 4);
                el = er = Quaternion.Euler(-12, 0, 0);
            }
            shoulderL.localRotation = Quaternion.Slerp(shoulderL.localRotation, sl, k);
            shoulderR.localRotation = Quaternion.Slerp(shoulderR.localRotation, sr, k);
            elbowL.localRotation = Quaternion.Slerp(elbowL.localRotation, el, k);
            elbowR.localRotation = Quaternion.Slerp(elbowR.localRotation, er, k);
            body.localPosition = new Vector3(0, bob, 0);

            // Head: look at the work while working; nod while speaking.
            float nod = Time.time < bubbleUntil ? Mathf.Sin(Time.time * 6f) * 4f : 0f;
            float look = mode == Mode.Working ? 18f : mode == Mode.Watching || mode == Mode.Guiding ? 8f : 0f;
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(look + nod, 0, 0), k);
        }

        // ------------------------------------------------------------------ real character (Rocketbox model)

        Animation anim;
        string clipNow;

        void AnimateModel()
        {
            bool walking = mode == Mode.Walking || mode == Mode.Leaving;
            bool speaking = Time.time < bubbleUntil;
            string want;
            if (walking) want = "walk";
            else if (mode == Mode.Celebrating) want = "cheer";
            else if (Time.time < waveUntil) want = "wave";
            else if (mode == Mode.Working && working && runtime.Running) want = "work";
            else if (mode == Mode.Watching || mode == Mode.Guiding) want = speaking ? "talk" : "listen";
            else want = speaking ? "talk" : "idle";
            if (!anim.GetClip(want)) want = anim.GetClip("idle") ? "idle" : clipNow;
            if (want != null && want != clipNow)
            {
                anim.CrossFade(want, 0.35f);
                clipNow = want;
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r) return r; }
            return null;
        }

        /// Real human model with real textures and motion-captured animation. False -> use the built-in figure.
        bool BuildModel()
        {
            var cfg = UiFactory.Config;
            if (!cfg || !cfg.scientistModel) return false;

            var model = Instantiate(cfg.scientistModel, transform, false).transform;
            model.name = "DrAshaModel";
            model.localPosition = Vector3.zero;
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) { Destroy(model.gameObject); return false; }

            // Face the same way as this NPC (whatever axis the file was authored in): eyes are in front of the head.
            var headBone = FindDeep(model, "Bip01 Head");
            var leftEye = FindDeep(model, "Bip01 LEye");
            var rightEye = FindDeep(model, "Bip01 REye");
            if (headBone && leftEye && rightEye)
            {
                Vector3 f = (leftEye.position + rightEye.position) * 0.5f - headBone.position; f.y = 0;
                if (f.sqrMagnitude > 1e-8f) model.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(f, transform.forward, Vector3.up), Vector3.up) * model.rotation;
            }

            // Real-world height (~1.65 m) with feet on the floor.
            var b = renderers[0].bounds;
            foreach (var r in renderers) { b.Encapsulate(r.bounds); r.updateWhenOffscreen = true; }
            if (b.size.y < 1.3f || b.size.y > 2.1f) { model.localScale *= 1.65f / Mathf.Max(0.01f, b.size.y); b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); }
            model.position += Vector3.up * (transform.position.y - b.min.y);

            // Textures: lab coat, face (skin + eyes), hair (alpha-cut), stethoscope.
            // Match by material name; if Unity renamed the materials, use the file's slot order instead.
            if (!cfg.scientistBody || !cfg.scientistHead)
                Debug.LogWarning("[RZ] Dr. Asha's textures are not assigned: run RoomZero > Set Up Dr. Asha.");
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    int slot = SlotByName(mats[i] ? mats[i].name : "");
                    if (slot < 0) slot = mats.Length == 4 ? i : 0;   // file order: body, head, hair, stethoscope
                    if (slot == 3) { RemoveSubmesh(r, i); continue; } // a scientist, not a doctor: no stethoscope
                    switch (slot)
                    {
                        case 1: mats[i] = SkinMat(cfg.scientistHead, cfg.scientistHeadNormal, 0.38f, false); break;
                        case 2: mats[i] = SkinMat(cfg.scientistHair, null, 0.3f, true); break;
                        case 3: mats[i] = SkinMat(cfg.scientistExtra, cfg.scientistExtraNormal, 0.55f, false); break;
                        default: mats[i] = SkinMat(cfg.scientistBody, cfg.scientistBodyNormal, 0.25f, false); break;
                    }
                }
                r.sharedMaterials = mats;
            }

            if (headBone && leftEye && rightEye) BuildGlasses(headBone, leftEye.position, rightEye.position);
            var leftHand = FindDeep(model, "Bip01 L Hand");
            var leftUpper = FindDeep(model, "Bip01 L UpperArm");
            if (leftHand && leftUpper) BuildTablet(leftHand, (leftHand.position - leftUpper.position).normalized);

            anim = model.GetComponent<Animation>();
            if (!anim) anim = model.gameObject.AddComponent<Animation>();
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            AddClip("idle", cfg.scientistIdle); AddClip("walk", cfg.scientistWalk); AddClip("talk", cfg.scientistTalk);
            AddClip("work", cfg.scientistWork); AddClip("listen", cfg.scientistListen); AddClip("cheer", cfg.scientistCheer); AddClip("wave", cfg.scientistWave);
            if (anim.GetClip("walk")) anim["walk"].speed = WalkSpeed / 1.05f;
            if (anim.GetClipCount() == 0)
            {
                // No animations imported: fall back to the built-in figure rather than a frozen T-pose.
                Destroy(model.gameObject);
                anim = null;
                return false;
            }
            if (anim.GetClip("idle")) { anim.Play("idle"); clipNow = "idle"; }

            Shapes.ContactShadow(transform, transform.position, 0.55f, 0.45f, transform.rotation, 0.4f);
            bubble = UiFactory.BackedText(transform, "", new Vector3(0, 1.9f, 0), 0.028f, Color.white);
            return true;
        }

        /// Hide one part of the model (its triangles) without touching the shared asset.
        static void RemoveSubmesh(SkinnedMeshRenderer r, int index)
        {
            if (!r.sharedMesh || index >= r.sharedMesh.subMeshCount) return;
            var copy = Instantiate(r.sharedMesh);
            copy.SetTriangles(new int[0], index);
            r.sharedMesh = copy;
        }

        /// A lab tablet carried in her left hand (built in the bind pose, so it follows the hand in every animation).
        void BuildTablet(Transform hand, Vector3 armDir)
        {
            var tablet = new GameObject("LabTablet").transform;
            tablet.position = hand.position + armDir * 0.07f + Vector3.down * 0.035f;
            tablet.rotation = Quaternion.LookRotation(armDir, Vector3.up);
            var body = Shapes.Make("TabletBody", Shapes.RoundedBox(new Vector3(0.17f, 0.012f, 0.23f), 0.01f), Shapes.BlackPlastic, tablet, tablet.position);
            body.transform.localRotation = Quaternion.identity;
            var screen = Shapes.Make("TabletScreen", Shapes.RoundedBox(new Vector3(0.15f, 0.002f, 0.2f), 0.004f), Shapes.ScreenMatFront, tablet, tablet.position);
            screen.transform.localPosition = new Vector3(0, 0.007f, 0);
            screen.transform.localRotation = Quaternion.identity;
            tablet.SetParent(hand, true);
        }

        async void AskMentor(ObjectiveBehaviour target)
        {
            deciding = true;
            nextDecision = Time.time + 6f;
            var input = new MentorInput
            {
                objectiveType = target.Data.type.ToString(),
                secondsStuck = Time.time - lastProgress,
                failedAttempts = runtime.FailedAttempts - failsAtStart,
                hintsGiven = hintsGiven,
                justSolvedOne = false,
                objectivesDone = runtime.ObjectivesComplete,
                objectivesTotal = runtime.Mission != null ? runtime.Mission.objectives.Count : 0,
                secondsLeft = runtime.SecondsLeft
            };
            MentorDecision d;
            try { d = Mentor != null ? await Mentor(input) : LocalMentorAgent.Decide(input); }
            catch (Exception) { d = LocalMentorAgent.Decide(input); }
            deciding = false;
            if (!this || !target || target.Completed || mode == Mode.Working || runtime == null || !runtime.Running) return;
            if (d == null || d.action == "WAIT") return;
            if (Time.time - lastHelpAt < 10f) return;               // never nag: at most one helping move every 10 s
            lastHelpAt = Time.time;
            switch (d.action)
            {
                case "ENCOURAGE":
                    Speak(VoiceLines.Encourage[UnityEngine.Random.Range(0, VoiceLines.Encourage.Length)]);
                    break;
                case "HINT":
                    target.ShowHint();
                    hintsGiven++;
                    Speak(hintsGiven % 2 == 1 ? VoiceLines.HintLine : VoiceLines.HintLine2);
                    break;
                case "SHOW_ONE_STEP":
                    Speak(VoiceLines.ShowStep);
                    showStepAt = Time.time + 0.3f;
                    break;
            }
        }

        static int SlotByName(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("head") || n.Contains("face")) return 1;
            if (n.Contains("opacity") || n.Contains("hair")) return 2;
            if (n.Contains("stetoskop") || n.Contains("stetho")) return 3;
            if (n.Contains("body")) return 0;
            return -1;
        }

        void AddClip(string key, AnimationClip clip)
        {
            if (!clip || !anim) return;
            clip.legacy = true;
            anim.AddClip(clip, key);
            anim[key].wrapMode = WrapMode.Loop;
        }

        Material SkinMat(Texture2D tex, Texture2D normal, float smoothness, bool cutout)
        {
            var m = new Material(UiFactory.LitMat(Color.white));
            if (tex)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (normal && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (cutout)
            {
                if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 1f);            // Built-in Standard: Cutout
                if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);  // URP Lit: alpha clipping
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.35f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);           // hair cards: both sides
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = 2450;
            }
            return m;
        }

        /// Thin black-rimmed spectacles, placed on her eyes and fixed to her head so they move with it.
        void BuildGlasses(Transform headBone, Vector3 leftEye, Vector3 rightEye)
        {
            Vector3 fwdDir = transform.forward, up = Vector3.up;
            Vector3 mid = (leftEye + rightEye) * 0.5f;
            float sep = Mathf.Clamp(Vector3.Distance(leftEye, rightEye), 0.05f, 0.075f);
            float lensR = sep * 0.36f;
            var glasses = new GameObject("Spectacles").transform;
            glasses.position = mid + fwdDir * 0.022f + up * 0.003f;
            glasses.rotation = Quaternion.LookRotation(fwdDir, up);
            var rim = Shapes.Surface(new Color(0.06f, 0.06f, 0.07f), 0.75f, 0.3f);
            // A torus for each rim (turned profile of a small circle).
            var ringPts = new Vector2[13];
            for (int i = 0; i < ringPts.Length; i++)
            {
                float a = i * Mathf.PI * 2f / (ringPts.Length - 1);
                ringPts[i] = new Vector2(lensR + Mathf.Cos(a) * 0.0012f, Mathf.Sin(a) * 0.0012f);
            }
            var ringMesh = Shapes.Lathe($"specrim_{lensR:F4}", ringPts, 24);
            var faceAxis = Quaternion.FromToRotation(Vector3.up, Vector3.forward);   // lathe axis -> looking direction
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 c = new Vector3(s * sep * 0.5f, 0, 0);
                var ring = Shapes.Make("Rim", ringMesh, rim, glasses, glasses.position);
                ring.transform.localPosition = c; ring.transform.localRotation = faceAxis;
                // Temple arm back to the ear.
                var arm = Shapes.Make("Temple", Shapes.Rod(0.0011f, 0.1f, 6), rim, glasses, glasses.position);
                arm.transform.localPosition = new Vector3(s * (sep * 0.5f + lensR), 0.004f, 0);
                arm.transform.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(s * 0.12f, -0.05f, -1f).normalized);
            }
            var bridge = Shapes.Make("Bridge", Shapes.Rod(0.0012f, sep - lensR * 2f + 0.002f, 6), rim, glasses, glasses.position);
            bridge.transform.localPosition = new Vector3(-(sep * 0.5f - lensR) - 0.001f, 0.003f, 0);
            bridge.transform.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.right);
            glasses.SetParent(headBone, true);
        }

        // ------------------------------------------------------------------ body (code-generated)

        static readonly Color Skin = new Color(0.6f, 0.4f, 0.29f);
        static readonly Color Hair = new Color(0.1f, 0.07f, 0.06f);
        static readonly Color Trousers = new Color(0.16f, 0.2f, 0.3f);
        static readonly Color Glove = new Color(0.35f, 0.55f, 0.95f);

        Transform Pivot(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        GameObject Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 local, Quaternion localRot, Vector3? scale = null)
        {
            var go = Shapes.Make(name, mesh, mat, parent, parent.position);
            go.transform.localPosition = local;
            go.transform.localRotation = localRot;
            if (scale.HasValue) go.transform.localScale = scale.Value;
            return go;
        }

        GameObject Ball(string name, Transform parent, Vector3 local, Vector3 scale, Material mat)
        {
            var go = UiFactory.Primitive(PrimitiveType.Sphere, name, parent, local, scale, Color.white);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        void BuildBody()
        {
            var coatMat = Shapes.Surface(new Color(0.95f, 0.96f, 0.97f), 0.25f, 0f);
            var skin = Shapes.Surface(Skin, 0.35f, 0f);
            var hair = Shapes.Surface(Hair, 0.45f, 0f);
            var trousers = Shapes.Surface(Trousers, 0.2f, 0f);
            var shoes = Shapes.Surface(new Color(0.08f, 0.08f, 0.09f), 0.6f, 0f);
            var glove = Shapes.Surface(Glove, 0.55f, 0f);
            var down = Quaternion.Euler(180, 0, 0);

            body = Pivot("Body", transform, Vector3.zero);
            Shapes.ContactShadow(transform, transform.position, 0.5f, 0.4f, transform.rotation, 0.4f);

            // Legs (swing from the hips) with shoes.
            legL = Pivot("LegL", body, new Vector3(-0.085f, 0.88f, 0));
            legR = Pivot("LegR", body, new Vector3(0.085f, 0.88f, 0));
            foreach (var leg in new[] { legL, legR })
            {
                Part("Leg", Shapes.Rod(0.058f, 0.83f, 14), trousers, leg, Vector3.zero, down);
                Part("Shoe", Shapes.RoundedBox(new Vector3(0.095f, 0.06f, 0.24f), 0.03f), shoes, leg, new Vector3(0, -0.85f, 0.04f), Quaternion.identity);
            }

            // Lab coat: a turned, flared shape from the knees to the shoulders, slightly flattened front-to-back.
            var coat = Part("LabCoat", Shapes.Lathe("labcoat", new[]
            {
                new Vector2(0, 0.5f), new Vector2(0.2f, 0.5f), new Vector2(0.19f, 0.68f), new Vector2(0.168f, 0.88f), new Vector2(0.165f, 0.98f),
                new Vector2(0.18f, 1.12f), new Vector2(0.192f, 1.24f), new Vector2(0.19f, 1.32f), new Vector2(0.155f, 1.385f), new Vector2(0.07f, 1.43f), new Vector2(0, 1.44f)
            }, 28), coatMat, body, Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 0.72f));
            coat.name = "LabCoat";
            float front = 0.192f * 0.72f;
            Part("Shirt", Shapes.RoundedBox(new Vector3(0.1f, 0.13f, 0.01f), 0.004f), Shapes.Surface(new Color(0.3f, 0.5f, 0.75f), 0.3f, 0f), body, new Vector3(0, 1.33f, front - 0.012f), Quaternion.Euler(-8, 0, 0));
            Part("CoatSeam", Shapes.RoundedBox(new Vector3(0.006f, 0.78f, 0.004f), 0.002f), Shapes.Surface(new Color(0.82f, 0.84f, 0.86f), 0.2f, 0f), body, new Vector3(0, 0.9f, 0.123f), Quaternion.Euler(-3, 0, 0));
            for (int i = 0; i < 3; i++)
                Ball("Button", body, new Vector3(0.018f, 0.95f + i * 0.11f, 0.124f + i * 0.004f), Vector3.one * 0.014f, Shapes.Surface(new Color(0.7f, 0.72f, 0.75f), 0.6f, 0.4f));
            Part("Pocket", Shapes.RoundedBox(new Vector3(0.09f, 0.08f, 0.006f), 0.003f), Shapes.Surface(new Color(0.9f, 0.91f, 0.93f), 0.25f, 0f), body, new Vector3(-0.085f, 1.2f, 0.132f), Quaternion.Euler(-6, 0, 0));
            Part("Pen", Shapes.Rod(0.005f, 0.06f, 8), Shapes.Surface(new Color(0.15f, 0.35f, 0.8f), 0.6f, 0f), body, new Vector3(-0.065f, 1.2f, 0.136f), Quaternion.identity);
            Part("Badge", Shapes.RoundedBox(new Vector3(0.05f, 0.065f, 0.004f), 0.003f), Shapes.LabelMat, body, new Vector3(0.09f, 1.22f, 0.134f), Quaternion.Euler(-6, 0, 0));
            var badge = UiFactory.Text(body, "ASHA", new Vector3(0.09f, 1.205f, 0.131f), 0.008f, new Color(0.1f, 0.2f, 0.3f));
            badge.transform.localRotation = Quaternion.Euler(-6, 180, 0);

            // Neck + head (glasses, hair bun).
            Part("Neck", Shapes.Rod(0.042f, 0.08f, 12), skin, body, new Vector3(0, 1.41f, 0), Quaternion.identity);
            head = Pivot("Head", body, new Vector3(0, 1.48f, 0));
            Ball("Face", head, new Vector3(0, 0.07f, 0.005f), new Vector3(0.165f, 0.205f, 0.185f), skin);
            Part("Hair", Shapes.Dome(0.092f, 0.11f), hair, head, new Vector3(0, 0.085f, -0.01f), Quaternion.Euler(-12, 0, 0), new Vector3(1f, 1f, 1.08f));
            Ball("HairBack", head, new Vector3(0, 0.07f, -0.035f), new Vector3(0.175f, 0.2f, 0.15f), hair);
            Ball("Bun", head, new Vector3(0, 0.17f, -0.075f), Vector3.one * 0.075f, hair);
            var eyeMat = Shapes.Surface(new Color(0.06f, 0.05f, 0.05f), 0.8f, 0f);
            var rimMat = Shapes.Surface(new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.4f);
            foreach (int s in new[] { -1, 1 })
            {
                Ball("Eye", head, new Vector3(s * 0.034f, 0.085f, 0.088f), Vector3.one * 0.016f, eyeMat);
                Part("Brow", Shapes.RoundedBox(new Vector3(0.03f, 0.005f, 0.006f), 0.002f), hair, head, new Vector3(s * 0.034f, 0.108f, 0.09f), Quaternion.Euler(0, 0, s * -6f));
                Part("GlassRim", Shapes.Rod(0.022f, 0.003f, 18), rimMat, head, new Vector3(s * 0.035f, 0.085f, 0.098f), Quaternion.Euler(90, 0, 0));
                Part("Ear", Shapes.RoundedBox(new Vector3(0.012f, 0.04f, 0.025f), 0.006f), skin, head, new Vector3(s * 0.083f, 0.07f, 0f), Quaternion.identity);
            }
            Part("Bridge", Shapes.RoundedBox(new Vector3(0.024f, 0.004f, 0.004f), 0.0015f), rimMat, head, new Vector3(0, 0.088f, 0.1f), Quaternion.identity);
            Part("Nose", Shapes.RoundedBox(new Vector3(0.018f, 0.03f, 0.02f), 0.008f), skin, head, new Vector3(0, 0.065f, 0.095f), Quaternion.identity);
            Part("Smile", Shapes.RoundedBox(new Vector3(0.03f, 0.005f, 0.005f), 0.002f), Shapes.Surface(new Color(0.45f, 0.2f, 0.18f), 0.4f, 0f), head, new Vector3(0, 0.035f, 0.09f), Quaternion.identity);

            // Arms: shoulder -> elbow -> blue gloved hand. Left hand carries a tablet.
            shoulderL = Pivot("ShoulderL", body, new Vector3(-0.2f, 1.34f, 0));
            shoulderR = Pivot("ShoulderR", body, new Vector3(0.2f, 1.34f, 0));
            elbowL = BuildArm(shoulderL, coatMat, glove);
            elbowR = BuildArm(shoulderR, coatMat, glove);
            var tablet = Part("Tablet", Shapes.RoundedBox(new Vector3(0.16f, 0.22f, 0.012f), 0.01f), Shapes.BlackPlastic, elbowL, new Vector3(0.04f, -0.27f, 0.05f), Quaternion.Euler(70, 0, 20));
            Part("TabletScreen", Shapes.RoundedBox(new Vector3(0.14f, 0.19f, 0.002f), 0.004f), Shapes.ScreenMatFront, tablet.transform, new Vector3(0, 0, 0.007f), Quaternion.identity);

            // Speech bubble above her head (readable from the player's seat).
            bubble = UiFactory.BackedText(transform, "", new Vector3(0, 1.85f, 0), 0.028f, Color.white);
        }

        Transform BuildArm(Transform shoulder, Material sleeve, Material glove)
        {
            var down = Quaternion.Euler(180, 0, 0);
            Ball("Shoulder", shoulder, Vector3.zero, Vector3.one * 0.1f, sleeve);
            Part("UpperArm", Shapes.Rod(0.048f, 0.29f, 14), sleeve, shoulder, Vector3.zero, down);
            var elbow = Pivot("Elbow", shoulder, new Vector3(0, -0.29f, 0));
            Part("Forearm", Shapes.Rod(0.042f, 0.24f, 14), sleeve, elbow, Vector3.zero, down);
            Part("Cuff", Shapes.Rod(0.044f, 0.02f, 14), Shapes.Surface(new Color(0.85f, 0.86f, 0.88f), 0.25f, 0f), elbow, new Vector3(0, -0.22f, 0), down);
            Ball("Hand", elbow, new Vector3(0, -0.285f, 0.005f), new Vector3(0.065f, 0.09f, 0.045f), glove);
            return elbow;
        }
    }
}
