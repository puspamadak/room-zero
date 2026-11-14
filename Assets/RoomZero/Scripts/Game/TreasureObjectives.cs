// FORMULA HUNT objectives (owner: #6 Game Systems + Puzzle Designer).
// Story: fragments of the lost "Harvest Formula" (a crop that gives a bigger, more sustainable
// harvest with more protein, to help children who don't get enough) are hidden in the room.
// Internally the template is still called TREASURE_HUNT and the objectives HIDDEN_RELIC / SAFE_DIAL.
//
// HIDDEN_RELIC: invisible until the player sweeps a scanner over it (hand on Quest, controller
//   laser in the simulator, mouse on desktop). Collect it to get a number and a riddle that
//   points at the next relic's real furniture. Only the current relic can be scanned, so the
//   relics form a clue chain.
// SAFE_DIAL: dial in the collected numbers in order (< > to turn, SET to lock each digit) to
//   open the treasure safe. Wrong digit = failed attempt (guard alertness rises), sequence resets.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RoomZero.Input;
using RoomZero.UI;

namespace RoomZero.Game
{
    public class HiddenRelicObjective : ObjectiveBehaviour
    {
        public const float ScanSeconds = 0.9f;

        Transform relic, sparkle, ring;
        TextMesh label;
        PokeTarget poke;
        float scan;          // 0..1 reveal progress
        float nextScanBeep;
        bool revealed, hinted;

        public int Index => runtime.RelicIndex(Id);           // 1-based position in the chain
        public bool IsCurrent => runtime.CurrentTarget == this;
        public override bool ShowMarker => revealed || hinted;
        public override float MarkerHeight => 0.36f;   // above the FRAGMENT label

        protected override void Build()
        {
            // Relic: a gold gem on a small plinth, scaled to 0 until scanned.
            relic = new GameObject("Relic").transform;
            relic.SetParent(transform, false);
            relic.localPosition = new Vector3(0, 0.07f, 0);
            relic.localScale = Vector3.zero;
            // Formula fragment: a real glass vial of glowing green serum, black cap, leaf label.
            var gem = new GameObject("FormulaVial");
            gem.transform.SetParent(relic, false);
            var capsule = gem.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 0.0f, 0); capsule.radius = 0.035f; capsule.height = 0.12f;   // generous touch/click area
            var vialRoot = new GameObject("VialMesh").transform;
            vialRoot.SetParent(gem.transform, false);
            vialRoot.localPosition = new Vector3(0, -0.045f, 0);
            Shapes.Make("Serum", Shapes.VialLiquid(), UiFactory.Mat(new Color(0.35f, 1f, 0.45f)), vialRoot, vialRoot.position).transform.localPosition = Vector3.zero;
            var glassGo = Shapes.NoShadow(Shapes.Make("Glass", Shapes.Vial(), Shapes.ClearGlass, vialRoot, vialRoot.position));
            glassGo.transform.localPosition = Vector3.zero;
            var cap = Shapes.Make("Cap", Shapes.Rod(0.0095f, 0.016f, 18), Shapes.BlackPlastic, vialRoot, vialRoot.position);
            cap.transform.localPosition = new Vector3(0, 0.078f, 0);
            var tag = Shapes.RoundCube("Label", vialRoot, new Vector3(0, 0.03f, -0.0165f), new Vector3(0.022f, 0.026f, 0.001f), new Color(0.95f, 0.97f, 0.92f), 0.0008f);
            var leaf = Shapes.Make("LabelLeaf", Shapes.Leaf(1f, 0.45f, 0f), UiFactory.Mat(new Color(0.2f, 0.65f, 0.3f)), tag.transform, tag.transform.position);
            leaf.transform.localPosition = new Vector3(0, -0.008f, -0.0008f);
            leaf.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            leaf.transform.localScale = Vector3.one * 0.016f;
            var plinth = Shapes.Make("Plinth", Shapes.Rod(0.035f, 0.008f), Shapes.Steel, relic, relic.position);
            plinth.transform.localPosition = new Vector3(0, -0.054f, 0);
            UiFactory.ApplyModel(UiFactory.Config ? UiFactory.Config.fragmentModel : null, gem, 0.09f);
            poke = gem.AddComponent<PokeTarget>();
            poke.targetId = Id;
            poke.Pressed += _ => Collect();
            gem.GetComponent<Collider>().enabled = false; // can't be clicked until revealed

            // Faint shimmer: the only visible sign before scanning.
            sparkle = UiFactory.Primitive(PrimitiveType.Sphere, "Shimmer", transform, new Vector3(0, 0.05f, 0), Vector3.one * 0.012f, new Color(0.7f, 1f, 0.75f)).transform;

            // Scan ring grows while scanning.
            ring = UiFactory.Primitive(PrimitiveType.Cylinder, "ScanRing", transform, new Vector3(0, 0.005f, 0), new Vector3(0f, 0.002f, 0f), Palette.Holo).transform;

            label = UiFactory.BackedText(transform, "", new Vector3(0, 0.19f, 0), 0.017f, Palette.Accent);
        }

        void Update()
        {
            if (!runtime) return;
            if (Completed) { relic.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World); return; }

            float t = Time.time;
            bool canScan = IsCurrent && !revealed && !PokeTarget.InputBlocked;
            sparkle.gameObject.SetActive(!revealed && IsCurrent);
            sparkle.localScale = Vector3.one * (hinted ? 0.03f : 0.012f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 5f)));

            if (canScan && ScanSources.IsScanning(transform.position + Vector3.up * 0.06f))
            {
                scan = Mathf.Min(1f, scan + Time.deltaTime / ScanSeconds);
                if (Time.time >= nextScanBeep) { nextScanBeep = Time.time + 0.12f; RoomZero.Audio.Sfx.Play("scan", 0.5f, 0.8f + scan * 0.8f); }
                label.text = $"SCANNING {Mathf.RoundToInt(scan * 100)}%";
                label.color = Palette.Holo;
                if (scan >= 1f) Reveal();
            }
            else if (!revealed && scan > 0f)
            {
                scan = Mathf.Max(0f, scan - Time.deltaTime * 0.25f); // slow decay: keep sweeping
                if (scan <= 0f) label.text = "";
            }

            float ringSize = revealed ? 0f : scan * 0.16f;
            ring.localScale = new Vector3(ringSize, 0.002f, ringSize);
            if (!revealed) relic.localScale = Vector3.one * (scan * 0.6f);
            else relic.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
        }

        void Reveal()
        {
            revealed = true;
            relic.localScale = Vector3.one;
            relic.GetComponentInChildren<Collider>().enabled = true;
            label.text = "FRAGMENT FOUND: TOUCH IT";
            RoomZero.Audio.Sfx.Play("chime");
            Fx.Burst(transform.position, Palette.Holo, 0.25f);
            var sp = relic.gameObject.AddComponent<Sparkles>();
            sp.color = Palette.Success;
            label.color = Palette.Accent;
            ZeroLog.Info("Formula", $"fragment {Index} revealed");
        }

        void Collect()
        {
            if (!revealed || Completed) return;
            int digit = runtime.RelicDigit(Id);
            label.text = $"FRAGMENT {Index}  =  {digit}";
            label.color = Palette.Success;
            poke.SetTint(Palette.Success);
            poke.interactable = false;
            Complete();
        }

        public override void ShowHint() { hinted = true; }

        protected override string AutoStep()
        {
            if (!revealed) { Reveal(); return "My scanner picked something up here."; }
            Collect();
            return $"Fragment {Index}: number {runtime.RelicDigit(Id)}.";
        }

        public override string StatusLine() =>
            Completed ? $"Fragment {Index}:  {runtime.RelicDigit(Id)}  (done)"
            : revealed ? $"Fragment {Index}:  found, touch it!"
            : IsCurrent && scan > 0f ? $"Fragment {Index}:  scanning {Mathf.RoundToInt(scan * 100)}%"
            : $"Fragment {Index}:  hidden";

        public override string Guide(string where)
        {
            if (revealed) return $"formula fragment found! Touch it ({where}) to collect its number.";
            string riddle = Riddle(Data.anchor != null ? Data.anchor.type : "");
            string scanner = RoomZeroBootstrap.DesktopMode ? "sweep the mouse over it" : "sweep your hand over it";
            string prefix = Index == 1 ? "the first formula fragment" : $"clue {Index}: the next fragment";
            return $"{prefix} hides {riddle} ({where}). Look for a tiny shimmer and {scanner}.";
        }

        /// Room-aware riddles: the furniture type comes from the real room scan (MRUK labels).
        public static string Riddle(string surfaceType)
        {
            switch ((surfaceType ?? "").ToLowerInvariant())
            {
                case "table": return "where you'd set down a cup";
                case "storage": return "on top of what keeps things tidy";
                case "couch": return "where you'd sink in to rest";
                case "bed": return "where dreams begin";
                case "screen": return "beside what glows when it's on";
                case "virtual_desk": return "on ZERO's hologram desk";
                default: return "on a surface within reach";
            }
        }
    }

    public class SafeDialObjective : ObjectiveBehaviour, IKeyboardTarget
    {
        public override float MarkerHeight => 0.62f;   // above the DIAL readout and status line
        public override bool ShowMarker => false;       // it's right in front of you; an arrow would only add clutter
        Transform dial, door, treasure;
        Light interiorGlow;
        TextMesh display, status;
        readonly List<Transform> lights = new List<Transform>();
        int value;           // number currently on the dial (0-9)
        int progress;        // digits correctly locked so far
        bool opening;

        string Combo => runtime.SafeCombo();

        protected override void Build()
        {
            var face = new GameObject("Safe").transform;
            face.SetParent(transform, false);
            face.localPosition = new Vector3(0, 0.19f, 0);   // body bottom sits just above the surface
            face.localScale = Vector3.one * 1.4f;            // a proper, chunky vault

            // Hollow vault: back, sides, top and bottom panels, a lit interior and a shelf.
            // When the door swings open you look inside and see the Harvest Formula on the shelf.
            Color shell = new Color(0.7f, 0.74f, 0.76f), inner = new Color(0.82f, 0.95f, 0.88f);
            var vaultBody = Shapes.RoundCube("Back", face, new Vector3(0, 0, 0.125f), new Vector3(0.22f, 0.22f, 0.012f), shell, 0.005f, true, 0.6f, 0.7f);
            UiFactory.Primitive(PrimitiveType.Cube, "InnerBack", face, new Vector3(0, 0, 0.117f), new Vector3(0.19f, 0.19f, 0.004f), inner);
            Shapes.RoundCube("Left", face, new Vector3(-0.105f, 0, 0.06f), new Vector3(0.012f, 0.22f, 0.14f), shell, 0.005f, true, 0.6f, 0.7f);
            Shapes.RoundCube("Right", face, new Vector3(0.105f, 0, 0.06f), new Vector3(0.012f, 0.22f, 0.14f), shell, 0.005f, true, 0.6f, 0.7f);
            Shapes.RoundCube("Top", face, new Vector3(0, 0.105f, 0.06f), new Vector3(0.22f, 0.012f, 0.14f), shell, 0.005f, true, 0.6f, 0.7f);
            Shapes.RoundCube("Bottom", face, new Vector3(0, -0.105f, 0.06f), new Vector3(0.22f, 0.012f, 0.14f), shell, 0.005f, true, 0.6f, 0.7f);
            Shapes.RoundCube("Shelf", face, new Vector3(0, -0.055f, 0.06f), new Vector3(0.19f, 0.006f, 0.12f), Color.white, 0.002f).GetComponent<Renderer>().sharedMaterial = Shapes.Glass(new Color(0.85f, 0.95f, 1f, 0.35f));
            UiFactory.Primitive(PrimitiveType.Cube, "InteriorLight", face, new Vector3(0, 0.095f, 0.06f), new Vector3(0.16f, 0.006f, 0.08f), new Color(0.9f, 1f, 0.95f));
            var vaultModel = UiFactory.ApplyModel(UiFactory.Config ? UiFactory.Config.vaultModel : null, vaultBody, 0.28f);
            if (vaultModel) foreach (Transform part in face) if (part.name != "Back" && part.GetComponent<Renderer>()) part.GetComponent<Renderer>().enabled = false;
            interiorGlow = new GameObject("VaultGlow").AddComponent<Light>();
            interiorGlow.transform.SetParent(face, false);
            interiorGlow.transform.localPosition = new Vector3(0, 0.03f, 0.03f);
            interiorGlow.type = LightType.Point;
            interiorGlow.color = new Color(0.6f, 1f, 0.7f);
            interiorGlow.range = 0.5f;
            interiorGlow.intensity = 0f;   // switched on when the door opens

            door = new GameObject("DoorHinge").transform;            // hinge on the left edge
            door.SetParent(face, false);
            door.localPosition = new Vector3(-0.1f, 0, -0.012f);
            var doorPlate = Shapes.RoundCube("Door", door, new Vector3(0.1f, 0, 0), new Vector3(0.21f, 0.21f, 0.014f), new Color(0.62f, 0.66f, 0.69f), 0.006f, true, 0.65f, 0.8f);
            var hinge = Shapes.Make("Hinge", Shapes.Rod(0.005f, 0.18f, 12), Shapes.DarkSteel, door, door.position);
            hinge.transform.localPosition = new Vector3(-0.004f, -0.09f, 0);
            if (vaultModel) doorPlate.GetComponent<Renderer>().enabled = false; // the model has its own door
            // The recovered formula: a glowing seed pod with a sprout, shown when the door opens.
            treasure = new GameObject("HarvestFormula").transform;
            treasure.SetParent(face, false);
            treasure.localPosition = new Vector3(0, -0.026f, 0.06f);   // on the shelf, inside the vault
            // A sealed specimen jar holding a thriving super-crop seedling: the Harvest Formula.
            var pod = new GameObject("SeedPod");
            pod.transform.SetParent(treasure, false);
            pod.transform.localPosition = new Vector3(0, -0.022f, 0);
            Shapes.Make("JarBase", Shapes.Rod(0.036f, 0.008f), Shapes.Steel, pod.transform, pod.transform.position).transform.localPosition = Vector3.zero;
            var soil = Shapes.Make("Soil", Shapes.Rod(0.031f, 0.016f), UiFactory.LitMat(Palette.Soil), pod.transform, pod.transform.position);
            soil.transform.localPosition = new Vector3(0, 0.008f, 0);
            var sprout = new GameObject("Sprout").transform;
            sprout.SetParent(pod.transform, false);
            sprout.localPosition = new Vector3(0, 0.024f, 0);
            Shapes.Make("Stem", Shapes.Rod(0.0025f, 0.03f, 8), UiFactory.LitMat(Palette.Leaf), sprout, sprout.position).transform.localPosition = Vector3.zero;
            Shapes.Foliage(sprout, sprout.TransformPoint(0, 0.03f, 0), 0.15f, 3);
            var jar = Shapes.NoShadow(Shapes.Make("Jar", Shapes.Lathe("jar", new[] { new Vector2(0.034f, 0), new Vector2(0.034f, 0.085f), new Vector2(0.028f, 0.095f), new Vector2(0, 0.097f) }), Shapes.Glass(new Color(0.85f, 1f, 0.9f, 0.18f)), pod.transform, pod.transform.position));
            jar.transform.localPosition = new Vector3(0, 0.008f, 0);
            var lid = Shapes.Make("JarLid", Shapes.Rod(0.03f, 0.008f), Shapes.Steel, pod.transform, pod.transform.position);
            lid.transform.localPosition = new Vector3(0, 0.103f, 0);
            pod.transform.localScale = Vector3.one * 0.85f;
            if (UiFactory.ApplyModel(UiFactory.Config ? UiFactory.Config.seedPodModel : null, pod, 0.12f))
                pod.SetActive(false);
            treasure.gameObject.SetActive(false);   // appears in front when the door swings open

            dial = new GameObject("Dial").transform;
            dial.SetParent(door, false);
            dial.localPosition = new Vector3(0.1f, 0.01f, -0.008f);
            var dialMesh = Shapes.Make("DialKnob", Shapes.Lathe("dialknob", new[] { new Vector2(0, 0), new Vector2(0.045f, 0), new Vector2(0.046f, 0.006f), new Vector2(0.04f, 0.014f), new Vector2(0.018f, 0.016f), new Vector2(0.016f, 0.026f), new Vector2(0, 0.027f) }, 32), Shapes.Steel, dial, dial.position);
            dialMesh.transform.localPosition = Vector3.zero;
            dial.localRotation = Quaternion.Euler(-90, 0, 0);   // cylinder axis now faces the player
            Shapes.RoundCube("Notch", dial, new Vector3(0, 0.016f, 0.031f), new Vector3(0.006f, 0.006f, 0.016f), Palette.Accent, 0.002f, lit: false);

            display = UiFactory.BackedText(face, "DIAL 0", new Vector3(0, 0.165f, -0.03f), 0.03f, Palette.Accent);
            status = UiFactory.BackedText(face, "", new Vector3(0, 0.225f, -0.03f), 0.015f, Palette.Muted);

            int n = Mathf.Max(1, Combo.Length);
            for (int i = 0; i < n; i++)
                lights.Add(UiFactory.Primitive(PrimitiveType.Sphere, $"Light{i}", face, new Vector3((i - (n - 1) * 0.5f) * 0.03f, 0.085f, -0.02f), Vector3.one * 0.016f, Palette.Danger).transform);

            Btn(face, "<", new Vector3(-0.155f, 0.02f, -0.02f), () => Turn(-1));
            Btn(face, ">", new Vector3(0.155f, 0.02f, -0.02f), () => Turn(+1));
            Btn(face, "SET", new Vector3(0.155f, -0.06f, -0.02f), Set, 0.07f);
            Refresh();
        }

        void Btn(Transform parent, string label, Vector3 pos, System.Action a, float w = 0.055f)
        {
            var b = UiFactory.Button(parent, $"{Id}_{label}", label, pos, new Vector2(w, 0.045f), Palette.Button, a);
            b.cooldownSeconds = 0.2f;
        }

        public void TypeKey(string k)
        {
            if (PokeTarget.InputBlocked || Completed) return;
            if (k == "OK") { Set(); return; }
            if (k == "C") { progress = 0; Refresh(); return; }
            if (int.TryParse(k, out int d)) { value = d; Refresh(); }
        }

        void Turn(int dir) { if (Completed || opening) return; value = (value + dir + 10) % 10; Refresh(); }

        public override float AutoInterval => 0.7f;

        protected override string AutoStep()
        {
            if (opening) return null;
            string combo = Combo;
            if (combo.Length == 0 || progress >= combo.Length || !Unlocked(progress)) return null;
            int want = combo[progress] - '0';
            if (value != want) { Turn(((want - value + 10) % 10) <= 5 ? 1 : -1); return null; }
            Set();
            return opening ? "That's the last digit. Opening!" : $"Digit {progress}: {want}.";
        }

        void Set()
        {
            if (Completed || opening) return;
            string combo = Combo;
            if (combo.Length == 0) return;
            if (!Unlocked(progress)) { status.text = "FIND THE FRAGMENTS FIRST"; status.color = Palette.Danger; return; }

            if (value == combo[progress] - '0')
            {
                progress++;
                if (progress >= combo.Length) { StartCoroutine(Open()); return; }
            }
            else
            {
                progress = 0;
                status.text = "WRONG: SEQUENCE RESET";
                foreach (var l in lights) l.GetComponent<Renderer>().sharedMaterial = UiFactory.Mat(Palette.Danger);
                status.color = Palette.Danger;
                FailAttempt();
                Refresh(keepStatus: true);
                return;
            }
            Refresh();
        }

        /// Digit i can only be entered once relic i+1 has been collected.
        bool Unlocked(int i)
        {
            var relics = runtime.RelicIds();
            if (i >= relics.Count) return false;
            var o = runtime.Objective(relics[i]);
            return o && o.Completed;
        }

        IEnumerator Open()
        {
            opening = true;
            status.text = "FORMULA RECOVERED!";
            status.color = Palette.Success;
            Refresh(keepStatus: true);
            for (float t = 0; t < 1f; t += Time.deltaTime * 1.5f)
            {
                door.localRotation = Quaternion.Euler(0, -100f * Mathf.SmoothStep(0, 1, t), 0);
                yield return null;
            }
            treasure.gameObject.SetActive(true);
            treasure.gameObject.AddComponent<GrowIn>().duration = 0.9f;
            if (interiorGlow) interiorGlow.intensity = 1.6f;
            display.text = "OPEN";
            treasure.gameObject.AddComponent<Sparkles>().color = Palette.Success;
            RoomZero.Audio.Sfx.Play("grow");
            Complete();
        }

        void Refresh(bool keepStatus = false)
        {
            display.text = $"DIAL {value}";
            // Spin around the dial's own axis: tilt first, then rotate about local Y.
            dial.localRotation = Quaternion.Euler(-90, 0, 0) * Quaternion.Euler(0, value * 36f, 0);
            for (int i = 0; i < lights.Count; i++)
                lights[i].GetComponent<Renderer>().sharedMaterial = UiFactory.Mat(i < progress ? Palette.Success : Palette.Danger);
            if (!keepStatus)
            {
                status.text = $"DIGIT {Mathf.Min(progress + 1, lights.Count)} OF {lights.Count}";
                status.color = Palette.Muted;
            }
        }

        public override string StatusLine() =>
            Completed ? "Seed vault:  OPEN  (done)"
            : $"Vault dial: {value}  |  set {progress}/{lights.Count}  |  code {runtime.KnownComboText()}";

        public override void ShowHint()
        {
            string combo = Combo;
            if (progress < combo.Length && Unlocked(progress)) { status.text = $"NEXT DIGIT IS {combo[progress]}"; status.color = Palette.Accent; }
        }

        public override string Guide(string where)
        {
            string known = runtime.KnownComboText();
            string keys = RoomZeroBootstrap.DesktopMode ? " (or number keys + Enter)" : "";
            return $"unlock the SEED VAULT ({where}): set the dial to {known} one digit at a time with < > and SET{keys}.";
        }
    }
}
