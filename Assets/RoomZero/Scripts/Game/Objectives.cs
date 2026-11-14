// Objective catalog (spec: "two puzzle interactions"). Each objective decides its OWN
// success deterministically; nothing here listens to an AI.
// Owner: #6 Game Systems + Puzzle Designer.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RoomZero.Input;
using RoomZero.UI;
using RoomZero.Zero;

namespace RoomZero.Game
{
    /// Objectives the desktop keyboard can type into (0-9, "OK", "C").
    public interface IKeyboardTarget { void TypeKey(string key); }

    public abstract class ObjectiveBehaviour : MonoBehaviour
    {
        /// Should the bouncing "go here" arrow point at this objective? (Hidden relics: only after a hint.)
        public virtual bool ShowMarker => true;

        /// How high above the object the "go here" arrow floats (clear of its labels).
        public virtual float MarkerHeight => 0.33f;

        public ValidatedObjective Data { get; private set; }
        public bool Completed { get; private set; }
        public string Id => Data.spec.id;
        protected GameRuntime runtime;

        public virtual void Init(GameRuntime rt, ValidatedObjective data)
        {
            runtime = rt;
            Data = data;
            transform.position = data.position;
            // Face the player: our "front" (-Z for TextMesh readability) points at them.
            transform.rotation = Quaternion.LookRotation(-data.facing, Vector3.up);
            Build();
        }

        protected abstract void Build();
        public abstract void ShowHint();

        // ---- Dr. Asha (scientist NPC) can solve objectives one correct move at a time ----

        /// True while the scientist (not the player) is acting, so her moves don't count as a player take-over.
        public static bool AutoActing { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAutoActing() { AutoActing = false; }

        /// Make ONE correct move toward solving this objective. Returns a short line she says, or null if nothing to do.
        protected virtual string AutoStep() => null;

        /// Where her hand reaches when she works on this objective.
        public virtual Vector3 WorkPoint => transform.position + Vector3.up * 0.08f;

        /// Seconds she waits between moves (puzzles with many small moves go faster).
        public virtual float AutoInterval => 1.3f;

        public string RunAutoStep()
        {
            if (Completed || PokeTarget.InputBlocked) return null;
            AutoActing = true;
            try { return AutoStep(); }
            finally { AutoActing = false; }
        }

        /// One short instruction for the HUD "NEXT:" line. `where` is e.g. "on your right".
        public abstract string Guide(string where);

        /// One line for the desktop side card (progress + any numbers found). Never hides behind 3D.
        public abstract string StatusLine();

        protected void Complete()
        {
            if (Completed) return;
            Completed = true;
            Fx.Burst(transform.position, Palette.Success);
            RoomZero.Audio.Sfx.Play("success");
            OnCompleted();
            runtime.ReportObjectiveCompleted(Id);
        }

        protected virtual void OnCompleted() { }
        protected void FailAttempt()
        {
            RoomZero.Audio.Sfx.Play("error");
            runtime.ReportFailedAttempt(Id);
        }
    }

    /// FIND_CLUE: a floating data shard. Touch it to reveal the code.
    public class ClueObjective : ObjectiveBehaviour
    {
        Transform shard;
        TextMesh label;
        float pulse;

        protected override void Build()
        {
            UiFactory.Primitive(PrimitiveType.Cylinder, "Base", transform, Vector3.zero, new Vector3(0.08f, 0.004f, 0.08f), Palette.Muted, lit: true);
            var s = UiFactory.Primitive(PrimitiveType.Cube, "Shard", transform, new Vector3(0, 0.09f, 0), Vector3.one * 0.05f, Palette.Accent, keepCollider: true);
            s.transform.localRotation = Quaternion.Euler(45, 0, 45);
            shard = s.transform;
            gameObject.AddComponent<Sparkles>().color = Palette.Accent;
            UiFactory.ApplyModel(UiFactory.Config ? UiFactory.Config.shardModel : null, s, 0.08f);
            var poke = s.AddComponent<PokeTarget>();
            poke.targetId = Id;
            poke.Pressed += _ => Reveal();
            // Generous trigger so a fingertip near the shard counts.
            ((BoxCollider)s.GetComponent<Collider>()).size = Vector3.one * 1.4f;

            label = UiFactory.BackedText(transform, "DATA SHARD", new Vector3(0, 0.17f, 0), 0.016f, Palette.Accent);
        }

        void Update()
        {
            if (!shard) return;
            shard.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
            if (pulse > 0)
            {
                pulse -= Time.deltaTime;
                shard.localScale = Vector3.one * 0.05f * (1f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 6f)));
            }
        }

        void Reveal()
        {
            if (Completed) return;
            label.text = $"CODE  {Spaced(runtime.CodeFor(Id))}";
            label.color = Palette.Success;
            shard.GetComponent<PokeTarget>().SetTint(Palette.Success);
            Complete();
        }

        public override void ShowHint()
        {
            pulse = 6f;
            if (!Completed) label.text = "TOUCH THE SHARD";
        }

        public override string Guide(string where) => $"touch the glowing ORANGE SHARD ({where}) to reveal the code.";

        protected override string AutoStep() { Reveal(); return "The code is on this shard."; }

        public override string StatusLine() => Completed ? $"Code shard:  {runtime.CodeFor(Id)}  (done)" : "Code shard:  not found yet";

        static string Spaced(string s) => string.Join(" ", s.ToCharArray());
    }

    /// ENTER_CODE: upright keypad. Code comes from the linked FIND_CLUE objective.
    public class CodePanelObjective : ObjectiveBehaviour, IKeyboardTarget
    {
        public override float MarkerHeight => 0.62f;
        /// The keypad currently in play (desktop sim types into it with the keyboard).
        public static CodePanelObjective Active { get; private set; }

        TextMesh display;
        string entered = "";
        string Code => runtime.CodeFor(Data.spec.targetId);

        void OnEnable() { Active = this; }
        void OnDestroy() { if (Active == this) Active = null; }

        /// "0"-"9", "C" (clear) or "OK". Same path as poking the on-panel keys.
        public void TypeKey(string k)
        {
            if (PokeTarget.InputBlocked) return;
            Key(k);
        }

        public override string StatusLine() =>
            Completed ? "Keypad:  OPEN  (done)" : $"Keypad:  {(entered.Length == 0 ? "-" : entered)}  ({Code.Length} digits)";

        public override string Guide(string where)
        {
            var clue = runtime.Objective(Data.spec.targetId);
            if (clue != null && !clue.Completed) return "find the code first (orange shard).";
            string keys = RoomZeroBootstrap.DesktopMode ? " (or number keys + Enter)" : "";
            return $"code is {Code}. Type it on the KEYPAD ({where}){keys}, then OK.";
        }

        protected override void Build()
        {
            var face = new GameObject("Face").transform;
            face.SetParent(transform, false);
            face.localPosition = new Vector3(0, 0.22f, 0);
            face.localRotation = Quaternion.Euler(15f, 0, 0); // tilt the face up towards a seated player
            face.localScale = Vector3.one * 1.4f;             // big enough to read and hit easily

            var keypadBody = UiFactory.Primitive(PrimitiveType.Cube, "Body", face, new Vector3(0, 0, 0.012f), new Vector3(0.2f, 0.27f, 0.015f), Palette.Panel, lit: true);
            UiFactory.ApplyModel(UiFactory.Config ? UiFactory.Config.keypadModel : null, keypadBody, 0.3f);
            UiFactory.Primitive(PrimitiveType.Cube, "Stand", transform, new Vector3(0, 0.05f, 0.03f), new Vector3(0.05f, 0.1f, 0.05f), Palette.Muted, lit: true);
            display = UiFactory.Text(face, "LOCKED", new Vector3(0, 0.105f, -0.002f), 0.022f, Palette.Accent);

            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
            for (int i = 0; i < keys.Length; i++)
            {
                int row = i / 3, col = i % 3;
                string k = keys[i];
                var color = k == "OK" ? Palette.Success * 0.6f : k == "C" ? Palette.Danger * 0.6f : Palette.Button;
                color.a = 1f;
                var b = UiFactory.Button(face, $"{Id}_{k}", k, new Vector3((col - 1) * 0.058f, 0.06f - row * 0.05f, -0.004f), new Vector2(0.05f, 0.042f), color, () => Key(k));
                b.cooldownSeconds = 0.25f;
            }
        }

        protected override string AutoStep()
        {
            if (entered.Length >= Code.Length || !Code.StartsWith(entered)) { Key("C"); return "Let me clear that."; }
            string d = Code[entered.Length].ToString();
            Key(d);
            if (entered.Length == Code.Length) { Key("OK"); return "And... enter."; }
            return $"{d}...";
        }

        public override float AutoInterval => 0.8f;

        void Key(string k)
        {
            if (Completed) return;
            if (k == "C") entered = "";
            else if (k == "OK")
            {
                if (entered == Code) { display.text = "OPEN"; display.color = Palette.Success; Complete(); return; }
                display.text = "DENIED"; display.color = Palette.Danger;
                entered = "";
                FailAttempt();
                StartCoroutine(ResetDisplay());
                return;
            }
            else if (entered.Length < Code.Length) entered += k;
            display.color = Palette.Holo;
            display.text = entered.PadRight(Code.Length, '_');
        }

        IEnumerator ResetDisplay()
        {
            yield return new WaitForSeconds(1f);
            if (!Completed) { display.color = Palette.Accent; display.text = new string('_', Code.Length); }
        }

        public override void ShowHint()
        {
            if (Completed) return;
            var clue = runtime.Objective(Data.spec.targetId);
            display.color = Palette.Accent;
            display.text = clue != null && !clue.Completed ? "FIND THE SHARD" : $"{Code[0]}{new string('_', Code.Length - 1)}";
        }
    }

    /// DISABLE_SENSOR: nodes flash a sequence; touch them in the same order.
    public class SensorObjective : ObjectiveBehaviour
    {
        readonly List<PokeTarget> nodes = new List<PokeTarget>();
        readonly List<int> sequence = new List<int>();
        int progress;
        TextMesh label;
        Coroutine playing;
        bool slowHint;

        protected override void Build()
        {
            int n = Mathf.Clamp(Data.spec.steps, 3, 5);
            UiFactory.Primitive(PrimitiveType.Cube, "Rail", transform, new Vector3(0, 0.01f, 0), new Vector3(0.06f * n, 0.01f, 0.05f), Palette.Muted, lit: true);
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var go = UiFactory.Primitive(PrimitiveType.Sphere, $"Node{i}", transform, new Vector3((i - (n - 1) * 0.5f) * 0.06f, 0.045f, 0), Vector3.one * 0.035f, Palette.Danger, keepCollider: true);
                var p = go.AddComponent<PokeTarget>();
                p.targetId = $"{Id}_node{i}";
                p.cooldownSeconds = 0.2f;
                p.Pressed += _ => Touch(idx);
                nodes.Add(p);
            }
            var rng = new System.Random(runtime.Seed + Id.GetHashCode());
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Insert(rng.Next(order.Count + 1), i);
            sequence.AddRange(order);
            label = UiFactory.BackedText(transform, "SENSOR GRID: REPEAT THE PATTERN", new Vector3(0, 0.12f, 0), 0.013f, Palette.Danger);
            playing = StartCoroutine(PlayLoop());
        }

        IEnumerator PlayLoop()
        {
            while (!Completed)
            {
                if (progress == 0)
                {
                    float on = slowHint ? 0.7f : 0.4f;
                    for (int i = 0; i < sequence.Count && progress == 0; i++)
                    {
                        nodes[sequence[i]].SetTint(Palette.Accent);
                        yield return new WaitForSeconds(on);
                        nodes[sequence[i]].SetTint(Palette.Danger);
                        yield return new WaitForSeconds(0.15f);
                    }
                }
                yield return new WaitForSeconds(2.5f);
            }
        }

        protected override string AutoStep()
        {
            if (progress >= sequence.Count) return null;
            Touch(sequence[progress]);
            return progress == 1 ? "I memorised the flash order." : null;
        }

        void Touch(int idx)
        {
            if (Completed) return;
            if (sequence[progress] == idx)
            {
                nodes[idx].SetTint(Palette.Success);
                progress++;
                if (progress >= sequence.Count) { label.text = "SENSOR GRID OFFLINE"; label.color = Palette.Success; Complete(); }
            }
            else
            {
                progress = 0;
                foreach (var n in nodes) n.SetTint(Palette.Danger);
                label.text = "WRONG ORDER - WATCH AGAIN";
                FailAttempt();
            }
        }

        public override void ShowHint()
        {
            slowHint = true;
            label.text = $"FIRST NODE IS #{sequence[0] + 1} FROM THE LEFT";
        }

        public override string Guide(string where) =>
            $"watch the red SENSOR nodes ({where}) flash, then touch them in the same order.";

        public override string StatusLine() => Completed ? "Sensor grid:  OFFLINE  (done)" : $"Sensor grid:  {progress}/{sequence.Count} in order";

        protected override void OnCompleted()
        {
            if (playing != null) StopCoroutine(playing);
            foreach (var n in nodes) { n.SetTint(Palette.Success); n.interactable = false; }
        }
    }
}
