// Rooms 2 and 3 of the Harvest Formula campaign. Four hands-first challenges, each deterministic,
// each solvable by the player's hands OR by Dr. Asha (AutoStep), and each costing a failed attempt
// when the player gets it wrong (so the guard / adaptation loop still applies).
//
//   NUTRIENT_MIX      (greenhouse) pipette nutrient drops until the beaker matches the feed recipe, then MIX.
//   GROW_LIGHT        (greenhouse) push LIGHT / WATER / HEAT into the green band. Lamps add heat, water cools.
//   MICROSCOPE_FOCUS  (gene lab)   focus each slide until the cells are sharp, then tag HEALTHY or DISEASED.
//   DNA_SPLICE        (gene lab)   fill each gap in the gene with the matching base: A-T, G-C.
//
// Owner: #6 Game Systems + Puzzle Designer.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RoomZero.Input;
using RoomZero.UI;
using RoomZero.Zero;

namespace RoomZero.Game
{
    /// Shared building blocks for the lab challenges.
    public abstract class LabChallenge : ObjectiveBehaviour
    {
        protected TextMesh title, status;
        protected System.Random rng;

        protected void Header(string text, float y)
        {
            title = UiFactory.BackedText(transform, text, new Vector3(0, y, 0), 0.017f, Palette.Accent, faceCamera: false);
            status = UiFactory.Text(transform, "", new Vector3(0, y - 0.03f, -0.005f), 0.012f, Palette.Muted);
        }

        protected void Say(string text, Color c) { if (status) { status.text = text; status.color = c; } }

        protected PokeTarget Btn(string label, Vector3 pos, Color color, System.Action a, float w = 0.06f, float h = 0.036f)
        {
            var b = UiFactory.Button(transform, $"{Id}_{label}", label, pos, new Vector2(w, h), color, a);
            b.cooldownSeconds = 0.15f;
            return b;
        }

        protected GameObject Bar(string name, Vector3 pos, Vector3 size, Color c, bool lit = false)
            => UiFactory.Primitive(PrimitiveType.Cube, name, transform, pos, size, c, lit: lit);

        protected GameObject Mesh(string name, Mesh mesh, Material mat, Vector3 localPos, Transform parent = null)
        {
            var go = Shapes.Make(name, mesh, mat, parent ? parent : transform, transform.position);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        protected void Tray(float width, float depth)
        {
            var tray = Shapes.RoundCube("Tray", transform, new Vector3(0, 0.008f, 0.03f), new Vector3(width, 0.016f, depth), Color.white, 0.006f);
            tray.GetComponent<Renderer>().sharedMaterial = Shapes.DarkSteel;
            Shapes.ContactShadow(transform, transform.position, width * 1.15f, depth * 1.3f, transform.rotation, 0.4f);
        }

        public override void Init(GameRuntime rt, ValidatedObjective data)
        {
            rng = new System.Random(rt.Seed * 31 + (data.spec.id ?? "").GetHashCode());
            base.Init(rt, data);
        }
    }

    // ===================================================================== NUTRIENT_MIX

    public class NutrientMixObjective : LabChallenge
    {
        static readonly string[] Names = { "N", "P", "K" };
        static readonly string[] Long = { "NITROGEN", "PHOSPHORUS", "POTASSIUM" };
        static readonly Color[] Colors = { new Color(0.25f, 0.55f, 1f), new Color(1f, 0.4f, 0.7f), new Color(1f, 0.75f, 0.2f) };

        int[] recipe, added;
        int n;
        TextMesh recipeText, beakerText;
        Transform liquid;
        Renderer liquidRenderer;
        bool mixing;

        public override float MarkerHeight => 0.42f;
        public override Vector3 WorkPoint => transform.TransformPoint(0, 0.17f, 0.07f);
        public override float AutoInterval => 0.9f;

        protected override void Build()
        {
            n = Mathf.Clamp(Data.spec.steps, 2, 3);
            recipe = new int[n]; added = new int[n];
            for (int i = 0; i < n; i++) recipe[i] = 1 + rng.Next(i == 0 ? 3 : 2) + (i == 0 ? 1 : 0);   // N 2-4, P/K 1-2

            Tray(0.36f, 0.2f);
            Header("NUTRIENT MIX", 0.37f);
            recipeText = UiFactory.BackedText(transform, "", new Vector3(0, 0.3f, 0), 0.014f, Palette.Holo, faceCamera: false);
            beakerText = UiFactory.Text(transform, "", new Vector3(0, 0.272f, -0.005f), 0.012f, Palette.LabWhite);

            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) / 2f) * 0.12f;
                var glass = Shapes.Glass(new Color(Colors[i].r, Colors[i].g, Colors[i].b, 0.7f));
                var bottle = Mesh($"Bottle_{Names[i]}", Shapes.Bottle(), glass, new Vector3(x, 0.016f, 0.07f));
                bottle.transform.localScale = Vector3.one * 1.3f;
                Shapes.NoShadow(bottle);
                Mesh("Cap", Shapes.Cap(), Shapes.BlackPlastic, new Vector3(x, 0.016f + 0.117f, 0.07f));
                UiFactory.Text(transform, Names[i], new Vector3(x, 0.07f, 0.07f - 0.031f), 0.02f, Color.white);
                int idx = i;
                Btn($"+{Names[i]}", new Vector3(x, 0.2f, 0.05f), Colors[i] * 0.55f + Color.black * 0.45f, () => Drop(idx), 0.07f);
            }

            // Mixing beaker, front centre.
            var beakerRoot = new GameObject("Beaker").transform;
            beakerRoot.SetParent(transform, false);
            beakerRoot.localPosition = new Vector3(0, 0.016f, -0.04f);
            beakerRoot.localScale = Vector3.one * 1.15f;
            liquid = Mesh("Liquid", Shapes.BeakerLiquid(), Shapes.Liquid(Palette.LabWhite), Vector3.zero, beakerRoot).transform;
            liquidRenderer = liquid.GetComponent<Renderer>();
            Shapes.NoShadow(Mesh("Glass", Shapes.Beaker(), Shapes.ClearGlass, Vector3.zero, beakerRoot));

            Btn("MIX", new Vector3(0.135f, 0.04f, -0.07f), Palette.Success * 0.5f + Color.black * 0.5f, Mix, 0.07f);
            Btn("EMPTY", new Vector3(-0.135f, 0.04f, -0.07f), Palette.Button, Empty, 0.07f);
            Refresh();
            Say("Tap a bottle's + to add one drop", Palette.Muted);
        }

        int Total { get { int t = 0; foreach (var a in added) t += a; return t; } }

        void Drop(int i)
        {
            if (Completed || mixing || PokeTarget.InputBlocked) return;
            if (Total >= 10) { Say("Beaker full: EMPTY it", Palette.Danger); return; }
            added[i]++;
            StartCoroutine(FallingDrop(i));
            RoomZero.Audio.Sfx.Play("click", 0.5f);
            Refresh();
            Say(added[i] > recipe[i] ? $"Too much {Long[i]}! EMPTY and start again" : $"+1 {Long[i]}", added[i] > recipe[i] ? Palette.Danger : Palette.Muted);
        }

        IEnumerator FallingDrop(int i)
        {
            float x = (i - (n - 1) / 2f) * 0.12f;
            var d = UiFactory.Primitive(PrimitiveType.Sphere, "Drop", transform, new Vector3(x, 0.2f, 0.05f), Vector3.one * 0.012f, Colors[i], lit: true);
            Vector3 a = d.transform.localPosition, b = new Vector3(0, 0.09f, -0.04f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                if (!d) yield break;
                d.transform.localPosition = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.04f;
                yield return null;
            }
            if (d) Destroy(d);
        }

        void Empty()
        {
            if (Completed || mixing || PokeTarget.InputBlocked) return;
            for (int i = 0; i < n; i++) added[i] = 0;
            Refresh();
            Say("Beaker emptied", Palette.Muted);
        }

        void Mix()
        {
            if (Completed || mixing || PokeTarget.InputBlocked) return;
            bool ok = true;
            for (int i = 0; i < n; i++) ok &= added[i] == recipe[i];
            if (ok) { StartCoroutine(MixAndFinish()); return; }
            Say("Wrong ratio: the feed curdled. Emptied.", Palette.Danger);
            for (int i = 0; i < n; i++) added[i] = 0;
            Refresh();
            FailAttempt();
        }

        IEnumerator MixAndFinish()
        {
            mixing = true;
            RoomZero.Audio.Sfx.Play("scan");
            Color from = MixColor();
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.1f)
            {
                liquid.localRotation = Quaternion.Euler(0, t * 720f, 0);
                liquidRenderer.sharedMaterial = Shapes.Liquid(Color.Lerp(from, Palette.Success, Mathf.Round(t * 6f) / 6f));
                yield return null;
            }
            recipeText.text = "FEED READY";
            recipeText.color = Palette.Success;
            Say("Perfect ratio: protein feed ready", Palette.Success);
            gameObject.AddComponent<Sparkles>().color = Palette.Success;
            Complete();
        }

        Color MixColor()
        {
            if (Total == 0) return Palette.LabWhite;
            Color c = Color.black;
            for (int i = 0; i < n; i++) c += Colors[i] * added[i];
            c /= (float)Total;
            c.a = 1f;
            return c;
        }

        void Refresh()
        {
            var sb = new System.Text.StringBuilder("RECIPE ");
            var bb = new System.Text.StringBuilder("BEAKER ");
            for (int i = 0; i < n; i++) { sb.Append($"  {Names[i]} {recipe[i]}"); bb.Append($"  {Names[i]} {added[i]}"); }
            recipeText.text = sb.ToString();
            beakerText.text = bb.ToString();
            float fill = Mathf.Clamp01(Total / 10f);
            liquid.localScale = new Vector3(1f, Mathf.Max(0.04f, fill * 1.6f), 1f);
            liquid.gameObject.SetActive(Total > 0);
            liquidRenderer.sharedMaterial = Shapes.Liquid(MixColor());
        }

        protected override string AutoStep()
        {
            if (mixing) return null;
            for (int i = 0; i < n; i++) if (added[i] > recipe[i]) { Empty(); return "Too much. Starting over."; }
            for (int i = 0; i < n; i++)
                if (added[i] < recipe[i]) { Drop(i); return added[i] == recipe[i] ? $"{Long[i]}: {recipe[i]} drops, done." : null; }
            Mix();
            return "Exact recipe. Mixing!";
        }

        public override void ShowHint() => Say($"Add exactly: {string.Join(", ", RecipeParts())}. Then MIX.", Palette.Accent);

        IEnumerable<string> RecipeParts() { for (int i = 0; i < n; i++) yield return $"{recipe[i]} {Long[i]}"; }

        public override string Guide(string where) => $"match the RECIPE ({where}): tap each bottle's + for one drop, then MIX.";

        public override string StatusLine()
        {
            if (Completed) return "Nutrient feed:  READY  (done)";
            var sb = new System.Text.StringBuilder("Nutrient feed:  ");
            for (int i = 0; i < n; i++) sb.Append($"{Names[i]} {added[i]}/{recipe[i]}  ");
            return sb.ToString();
        }
    }

    // ===================================================================== GROW_LIGHT

    public class GrowLightObjective : LabChallenge
    {
        static readonly string[] Names = { "LIGHT", "WATER", "HEAT" };
        static readonly Color[] Colors = { new Color(0.85f, 0.5f, 1f), new Color(0.3f, 0.65f, 1f), new Color(1f, 0.45f, 0.3f) };
        const int Max = 5;
        const float SegH = 0.032f, GaugeY = 0.095f;

        int[] level = new int[3], target = new int[3];
        Transform[] fills = new Transform[3];
        TextMesh[] values = new TextMesh[3];
        Transform plant, fruit;
        Renderer lampBar;
        Light lamp;
        bool growing;
        float scorchedUntil;

        public override float MarkerHeight => 0.44f;
        public override Vector3 WorkPoint => transform.TransformPoint(0.12f, 0.2f, -0.02f);
        public override float AutoInterval => 0.8f;

        protected override void Build()
        {
            for (int i = 0; i < 3; i++) target[i] = 2 + rng.Next(3);       // 2..4
            level[0] = 1; level[1] = 1; level[2] = 1;

            Tray(0.52f, 0.22f);
            Header("GROW-LIGHT BALANCE", 0.4f);
            UiFactory.Text(transform, "Lamps add heat  ·  Water cools", new Vector3(0.12f, 0.345f, -0.005f), 0.01f, Palette.Muted);

            // Seedling in a pot under a grow lamp (left side).
            Vector3 potPos = new Vector3(-0.16f, 0.016f, 0.04f);
            Mesh("Pot", Shapes.Pot(0.05f, 0.09f), Shapes.Surface(new Color(0.78f, 0.45f, 0.32f), 0.35f, 0f), potPos);
            Mesh("Soil", Shapes.Rod(0.045f, 0.01f), UiFactory.LitMat(Palette.Soil), potPos + Vector3.up * 0.075f);
            plant = new GameObject("Crop").transform;
            plant.SetParent(transform, false);
            plant.localPosition = potPos + Vector3.up * 0.085f;
            Mesh("Stem", Shapes.Rod(0.004f, 0.08f, 8), UiFactory.LitMat(Palette.Leaf), Vector3.zero, plant);
            Shapes.Foliage(plant, plant.TransformPoint(0, 0.06f, 0), 0.6f, 11);
            fruit = new GameObject("Pods").transform;
            fruit.SetParent(plant, false);
            for (int i = 0; i < 5; i++)
            {
                var pod = UiFactory.Primitive(PrimitiveType.Capsule, "BeanPod", fruit, Quaternion.Euler(0, i * 72f, 0) * new Vector3(0.035f, 0.07f, 0), new Vector3(0.012f, 0.025f, 0.012f), new Color(0.55f, 0.85f, 0.3f), lit: true);
                pod.transform.localRotation = Quaternion.Euler(0, i * 72f, 35f);
            }
            fruit.gameObject.SetActive(false);
            plant.localScale = Vector3.one * 0.45f;

            Mesh("LampPole", Shapes.Rod(0.005f, 0.3f, 10), Shapes.Steel, new Vector3(-0.25f, 0.016f, 0.08f));
            var arm = Shapes.RoundCube("LampArm", transform, new Vector3(-0.2f, 0.31f, 0.06f), new Vector3(0.11f, 0.01f, 0.012f), Color.white, 0.003f);
            arm.GetComponent<Renderer>().sharedMaterial = Shapes.Steel;
            var housing = Shapes.RoundCube("LampHousing", transform, new Vector3(-0.16f, 0.3f, 0.04f), new Vector3(0.15f, 0.018f, 0.06f), Color.white, 0.005f);
            housing.GetComponent<Renderer>().sharedMaterial = Shapes.DarkSteel;
            var bar = Shapes.RoundCube("LampLEDs", transform, new Vector3(-0.16f, 0.289f, 0.04f), new Vector3(0.13f, 0.004f, 0.045f), Color.white, 0.0015f, lit: false);
            lampBar = bar.GetComponent<Renderer>();
            var lgo = new GameObject("GrowLight");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = new Vector3(-0.16f, 0.25f, 0.03f);
            lamp = lgo.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 0.6f; lamp.color = Colors[0];

            // Three gauges with the green target band (right side).
            for (int i = 0; i < 3; i++)
            {
                float x = 0.03f + i * 0.095f;
                Bar($"Gauge_{Names[i]}", new Vector3(x, GaugeY + SegH * Max / 2f, 0.01f), new Vector3(0.05f, SegH * Max + 0.01f, 0.01f), new Color(0.06f, 0.12f, 0.13f));
                Bar("TargetBand", new Vector3(x, GaugeY + SegH * target[i], 0.004f), new Vector3(0.058f, SegH * 0.7f, 0.002f), new Color(0.2f, 0.6f, 0.3f));
                for (int k = 1; k < Max; k++)
                    Bar("Tick", new Vector3(x, GaugeY + SegH * k, 0.002f), new Vector3(0.05f, 0.0015f, 0.002f), new Color(0.3f, 0.4f, 0.42f));
                fills[i] = Bar("Fill", new Vector3(x, GaugeY, -0.001f), new Vector3(0.03f, 0.001f, 0.004f), Colors[i]).transform;
                UiFactory.Text(transform, Names[i], new Vector3(x, 0.08f, -0.006f), 0.011f, Colors[i]);
                values[i] = UiFactory.Text(transform, "", new Vector3(x, 0.275f, -0.006f), 0.012f, Color.white);
                int idx = i;
                Btn($"{Names[i]}+", new Vector3(x, 0.31f, -0.015f), Palette.Button, () => Change(idx, 1), 0.06f, 0.03f);
                Btn($"{Names[i]}-", new Vector3(x, 0.045f, -0.015f), Palette.Button, () => Change(idx, -1), 0.06f, 0.03f);
            }
            Refresh();
            Say("Push every bar into its GREEN band", Palette.Muted);
        }

        void Change(int i, int delta)
        {
            if (Completed || growing || PokeTarget.InputBlocked) return;
            ApplyChange(level, i, delta);
            RoomZero.Audio.Sfx.Play("click", 0.4f);
            if (i == 1 && delta > 0) Fx.Burst(plant.position, new Color(0.4f, 0.7f, 1f), 0.08f);

            for (int k = 0; k < 3; k++)
                if (level[k] >= target[k] + 2 && Time.time > scorchedUntil)
                {
                    scorchedUntil = Time.time + 2f;
                    Say($"Too much {Names[k]}! The leaves are scorching", Palette.Danger);
                    FailAttempt();
                    Refresh();
                    return;
                }
            Refresh();
            if (InBand == 3) StartCoroutine(Grow());
            else Say($"{InBand} of 3 in the green band", Palette.Muted);
        }

        /// The rules, as a pure function (unit-tested): lamps heat the room, watering cools it.
        public static void ApplyChange(int[] level, int i, int delta)
        {
            level[i] = Mathf.Clamp(level[i] + delta, 0, Max);
            if (i == 0) level[2] = Mathf.Clamp(level[2] + delta, 0, Max);
            if (i == 1 && delta > 0) level[2] = Mathf.Clamp(level[2] - 1, 0, Max);
        }

        /// Dr. Asha's next move (unit-tested to always converge without scorching). Returns false when solved.
        public static bool NextAutoMove(int[] level, int[] target, out int control, out int dir)
        {
            control = -1; dir = 0;
            if (level[0] < target[0] && level[2] + 1 >= target[2] + 2) { control = 2; dir = -1; return true; }
            for (int i = 0; i < 3; i++)
                if (level[i] != target[i]) { control = i; dir = level[i] < target[i] ? 1 : -1; return true; }
            return false;
        }

        public static bool Scorched(int[] level, int[] target)
        {
            for (int k = 0; k < 3; k++) if (level[k] >= target[k] + 2) return true;
            return false;
        }

        int InBand { get { int c = 0; for (int k = 0; k < 3; k++) if (level[k] == target[k]) c++; return c; } }

        void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                float h = Mathf.Max(0.001f, SegH * level[i]);
                fills[i].localScale = new Vector3(0.03f, h, 0.004f);
                fills[i].localPosition = new Vector3(fills[i].localPosition.x, GaugeY + h / 2f, -0.001f);
                bool ok = level[i] == target[i];
                values[i].text = ok ? "OK" : level[i] < target[i] ? "LOW" : "HIGH";
                values[i].color = ok ? Palette.Success : level[i] < target[i] ? Palette.Accent : Palette.Danger;
            }
            float light01 = level[0] / (float)Max;
            lampBar.sharedMaterial = UiFactory.Mat(Color.Lerp(new Color(0.15f, 0.1f, 0.2f), Colors[0], light01));
            lamp.intensity = 0.2f + light01 * 2.2f;
            if (!growing) plant.localScale = Vector3.one * (0.45f + 0.08f * InBand);
            // Wilt a little when something is badly off.
            float worst = 0; for (int k = 0; k < 3; k++) worst = Mathf.Max(worst, Mathf.Abs(level[k] - target[k]));
            plant.localRotation = Quaternion.Euler(Mathf.Min(worst, 3) * 6f, 0, 0);
        }

        IEnumerator Grow()
        {
            growing = true;
            Say("Balanced! The crop is growing...", Palette.Success);
            RoomZero.Audio.Sfx.Play("grow");
            float from = plant.localScale.x;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.4f)
            {
                plant.localScale = Vector3.one * Mathf.Lerp(from, 1.15f, Mathf.SmoothStep(0, 1, t));
                plant.localRotation = Quaternion.Slerp(plant.localRotation, Quaternion.identity, t);
                yield return null;
            }
            fruit.gameObject.SetActive(true);
            fruit.gameObject.AddComponent<GrowIn>().duration = 0.6f;
            gameObject.AddComponent<Sparkles>().color = Palette.Success;
            title.text = "CROP GROWN";
            title.color = Palette.Success;
            Say("Protein-rich bean pods: harvest ready", Palette.Success);
            Complete();
        }

        protected override string AutoStep()
        {
            if (growing) return null;
            // Order matters: LIGHT also moves HEAT and WATER cools it, so fix LIGHT, then WATER, then HEAT.
            if (!NextAutoMove(level, target, out int i, out int dir)) return null;
            bool coolingFirst = i == 2 && level[0] < target[0];
            Change(i, dir);
            if (coolingFirst) return "Cooling first, so the lamp won't scorch it.";
            if (i == 0) return dir > 0 ? "More light. That warms it too." : "Dimming the lamp.";
            if (i == 1) return dir > 0 ? "A little water. That cools it." : "Less water.";
            return dir > 0 ? "Bit warmer." : "Cooling down.";
        }

        public override void ShowHint() => Say("Fix LIGHT first, then WATER, then HEAT (lamps heat, water cools)", Palette.Accent);

        public override string Guide(string where) => $"use + and - ({where}) to push LIGHT, WATER and HEAT into their green bands.";

        public override string StatusLine() =>
            Completed ? "Grow lights:  BALANCED  (done)" : $"Grow lights:  {InBand}/3 in band   (L {level[0]}  W {level[1]}  H {level[2]})";
    }

    // ===================================================================== MICROSCOPE_FOCUS

    public class MicroscopeObjective : LabChallenge
    {
        const int MaxFocus = 8;
        const float ViewR = 0.085f;

        int slides, slide, focus, targetFocus, tagged;
        bool[] healthy;
        Transform view, cellsRoot, knob;
        TextMesh slideText, focusText;
        readonly List<Transform> cellBodies = new List<Transform>();
        readonly List<GameObject> crisp = new List<GameObject>();
        readonly List<Renderer> bodyRenderers = new List<Renderer>();
        readonly List<Color> bodyColors = new List<Color>();
        static readonly Color ViewBg = new Color(0.82f, 0.88f, 0.8f);

        public override float MarkerHeight => 0.42f;
        public override Vector3 WorkPoint => knob ? knob.position : transform.position + Vector3.up * 0.1f;
        public override float AutoInterval => 0.7f;

        protected override void Build()
        {
            slides = Mathf.Clamp(Data.spec.steps, 2, 5);
            healthy = new bool[slides];
            bool anyHealthy = false, anySick = false;
            for (int i = 0; i < slides; i++) { healthy[i] = rng.Next(2) == 0; anyHealthy |= healthy[i]; anySick |= !healthy[i]; }
            if (!anyHealthy) healthy[0] = true;
            if (!anySick) healthy[slides - 1] = false;

            Tray(0.36f, 0.2f);
            Header("MICROSCOPE", 0.37f);
            BuildMicroscope(new Vector3(-0.11f, 0.016f, 0.03f));

            // Big holographic eyepiece view to the right.
            view = new GameObject("EyepieceView").transform;
            view.SetParent(transform, false);
            view.localPosition = new Vector3(0.07f, 0.2f, 0.02f);
            var rim = Shapes.Make("ViewRim", Shapes.Rod(ViewR + 0.008f, 0.006f, 40), Shapes.BlackPlastic, view, view.position);
            rim.transform.localPosition = Vector3.zero; rim.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var bg = Shapes.Make("ViewField", Shapes.Rod(ViewR, 0.007f, 40), UiFactory.Mat(ViewBg), view, view.position);
            bg.transform.localPosition = Vector3.zero; bg.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            cellsRoot = new GameObject("Cells").transform;
            cellsRoot.SetParent(view, false);
            cellsRoot.localPosition = new Vector3(0, 0, -0.0075f);

            slideText = UiFactory.Text(transform, "", new Vector3(0.07f, 0.098f, -0.005f), 0.012f, Color.white);
            focusText = UiFactory.Text(transform, "", new Vector3(-0.11f, 0.25f, -0.005f), 0.011f, Palette.Holo);
            Btn("FOCUS-", new Vector3(-0.15f, 0.045f, -0.075f), Palette.Button, () => Focus(-1), 0.065f, 0.032f);
            Btn("FOCUS+", new Vector3(-0.075f, 0.045f, -0.075f), Palette.Button, () => Focus(1), 0.065f, 0.032f);
            Btn("HEALTHY", new Vector3(0.03f, 0.045f, -0.075f), Palette.Success * 0.45f + Color.black * 0.55f, () => Tag(true), 0.075f, 0.032f);
            Btn("DISEASED", new Vector3(0.115f, 0.045f, -0.075f), Palette.Danger * 0.45f + Color.black * 0.55f, () => Tag(false), 0.08f, 0.032f);
            LoadSlide(0);
        }

        void BuildMicroscope(Vector3 p)
        {
            var white = Shapes.GlossWhite;
            Mesh("Base", Shapes.RoundedBox(new Vector3(0.1f, 0.022f, 0.13f), 0.009f), white, p + new Vector3(0, 0.011f, 0));
            Mesh("Pillar", Shapes.RoundedBox(new Vector3(0.035f, 0.14f, 0.04f), 0.012f), white, p + new Vector3(0, 0.09f, 0.045f));
            Mesh("Stage", Shapes.RoundedBox(new Vector3(0.085f, 0.008f, 0.075f), 0.003f), Shapes.BlackPlastic, p + new Vector3(0, 0.075f, -0.005f));
            Mesh("Slide", Shapes.RoundedBox(new Vector3(0.05f, 0.002f, 0.018f), 0.0008f), Shapes.Glass(new Color(0.9f, 0.95f, 1f, 0.6f)), p + new Vector3(0, 0.08f, -0.005f));
            Mesh("Lamp", Shapes.Rod(0.012f, 0.01f), UiFactory.Mat(new Color(1f, 0.97f, 0.85f)), p + new Vector3(0, 0.022f, -0.005f));
            Mesh("Head", Shapes.RoundedBox(new Vector3(0.045f, 0.035f, 0.065f), 0.012f), white, p + new Vector3(0, 0.175f, 0.02f));
            var turret = Mesh("Turret", Shapes.Rod(0.018f, 0.012f), Shapes.Steel, p + new Vector3(0, 0.16f, -0.005f));
            turret.transform.localRotation = Quaternion.Euler(180, 0, 0);
            var obj = Mesh("Objective", Shapes.Rod(0.005f, 0.035f, 12), Shapes.Steel, p + new Vector3(0, 0.148f, -0.005f));
            obj.transform.localRotation = Quaternion.Euler(180, 0, 0);
            var eye = Mesh("Eyepiece", Shapes.Rod(0.008f, 0.045f, 14), Shapes.BlackPlastic, p + new Vector3(0, 0.188f, 0.0f));
            eye.transform.localRotation = Quaternion.Euler(-40, 0, 0);
            knob = Mesh("FocusKnob", Shapes.Rod(0.02f, 0.014f, 20), Shapes.BlackPlastic, p + new Vector3(0.024f, 0.07f, 0.045f)).transform;
            knob.localRotation = Quaternion.Euler(0, 0, -90);
            Shapes.Make("KnobMark", Shapes.Rod(0.003f, 0.016f, 6), UiFactory.Mat(Palette.Accent), knob, knob.position).transform.localPosition = new Vector3(0, 0, 0.016f);
        }

        void LoadSlide(int i)
        {
            slide = i;
            targetFocus = 2 + rng.Next(MaxFocus - 3);
            focus = Mathf.Clamp(targetFocus + (rng.Next(2) == 0 ? -3 : 3), 0, MaxFocus);
            foreach (Transform c in cellsRoot) Destroy(c.gameObject);
            cellBodies.Clear(); crisp.Clear(); bodyRenderers.Clear(); bodyColors.Clear();

            bool good = healthy[i];
            int count = 7;
            for (int k = 0; k < count; k++)
            {
                float a = k * 2.4f + (float)rng.NextDouble(), r = ViewR * 0.62f * Mathf.Sqrt((k + 0.5f) / count);
                var pos = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
                Color body = good ? new Color(0.35f, 0.75f, 0.35f) : new Color(0.62f, 0.25f, 0.55f);
                var cell = Shapes.Make("Cell", Shapes.Rod(0.012f, 0.002f, 20), UiFactory.Mat(body), cellsRoot, cellsRoot.position);
                cell.transform.localPosition = pos;
                cell.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                cell.transform.localScale = good ? Vector3.one : new Vector3(1.25f, 1f, 0.75f);   // diseased: misshapen
                cellBodies.Add(cell.transform);
                bodyRenderers.Add(cell.GetComponent<Renderer>());
                bodyColors.Add(body);

                // Crisp details only visible when in focus: nucleus (healthy) or dark lesions (diseased).
                var detail = new GameObject("Detail");
                detail.transform.SetParent(cellsRoot, false);
                detail.transform.localPosition = pos + new Vector3(0, 0, -0.0025f);
                int spots = good ? 1 : 3;
                for (int s = 0; s < spots; s++)
                {
                    var d = Shapes.Make("Spot", Shapes.Rod(good ? 0.0045f : 0.003f, 0.001f, 12), UiFactory.Mat(good ? new Color(0.15f, 0.35f, 0.2f) : new Color(0.18f, 0.05f, 0.12f)), detail.transform, detail.transform.position);
                    d.transform.localPosition = good ? Vector3.zero : new Vector3((s - 1) * 0.005f, (s % 2) * 0.004f - 0.002f, 0);
                    d.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                }
                crisp.Add(detail);
            }
            Refresh();
            Say("Turn FOCUS until the cells are sharp", Palette.Muted);
        }

        void Focus(int d)
        {
            if (Completed || PokeTarget.InputBlocked) return;
            focus = Mathf.Clamp(focus + d, 0, MaxFocus);
            RoomZero.Audio.Sfx.Play("click", 0.35f);
            Refresh();
            if (Sharp) Say("Sharp! HEALTHY or DISEASED?", Palette.Accent);
        }

        bool Sharp => focus == targetFocus;

        void Tag(bool saysHealthy)
        {
            if (Completed || PokeTarget.InputBlocked) return;
            if (!Sharp) { Say("Too blurry to tell: focus first", Palette.Danger); return; }
            if (saysHealthy != healthy[slide])
            {
                Say("Look again: sick cells are purple, lumpy, with dark spots", Palette.Danger);
                FailAttempt();
                return;
            }
            tagged++;
            RoomZero.Audio.Sfx.Play("chime");
            Fx.Burst(view.position, healthy[slide] ? Palette.Success : Palette.Danger, 0.12f);
            if (slide + 1 < slides) { LoadSlide(slide + 1); Say($"Slide {slide} tagged. Next sample.", Palette.Success); return; }
            title.text = "SAMPLES TAGGED";
            title.color = Palette.Success;
            Say("Healthy donor cells found", Palette.Success);
            Complete();
        }

        void Refresh()
        {
            int err = Mathf.Abs(focus - targetFocus);
            float blur = err / 4f;
            for (int k = 0; k < cellBodies.Count; k++)
            {
                float scale = 1f + blur * 1.1f;
                bool sick = !healthy[slide];
                float sx = sick ? 1.25f : 1f, sz = sick ? 0.75f : 1f;
                cellBodies[k].localScale = new Vector3(sx * scale, 1f, sz * scale);
                bodyRenderers[k].sharedMaterial = UiFactory.Mat(Color.Lerp(bodyColors[k], ViewBg, Mathf.Clamp01(blur * 0.75f)));
                crisp[k].SetActive(err == 0);
            }
            if (knob) knob.localRotation = Quaternion.Euler(0, 0, -90) * Quaternion.Euler(0, focus * 40f, 0);
            slideText.text = $"SLIDE {slide + 1} OF {slides}";
            focusText.text = err == 0 ? "IN FOCUS" : $"FOCUS {focus}";
            focusText.color = err == 0 ? Palette.Success : Palette.Holo;
        }

        protected override string AutoStep()
        {
            if (!Sharp) { Focus(focus < targetFocus ? 1 : -1); return Sharp ? "There. In focus." : null; }
            bool h = healthy[slide];
            Tag(h);
            return h ? "Round, green, one nucleus: healthy." : "Purple and spotty: diseased.";
        }

        public override void ShowHint() => Say("Healthy = round green cells. Diseased = purple, lumpy, dark spots", Palette.Accent);

        public override string Guide(string where) => $"press FOCUS ({where}) until the cells are sharp, then tag the slide HEALTHY or DISEASED.";

        public override string StatusLine() =>
            Completed ? "Microscope:  ALL SAMPLES TAGGED  (done)" : $"Microscope:  slide {slide + 1}/{slides}, {(Sharp ? "in focus" : "blurry")}";
    }

    // ===================================================================== DNA_SPLICE

    public class DnaSpliceObjective : LabChallenge
    {
        const int Pairs = 10;
        const float Span = 0.46f, HelixR = 0.045f, HelixY = 0.215f;
        static readonly char[] BaseSet = { 'A', 'T', 'G', 'C' };

        char[] top, bottom;
        bool[] filled;
        List<int> gaps;
        int current;
        readonly Transform[] topHalf = new Transform[Pairs], botHalf = new Transform[Pairs], topBall = new Transform[Pairs], botBall = new Transform[Pairs];
        readonly Renderer[] botRend = new Renderer[Pairs], botBallRend = new Renderer[Pairs];
        TextMesh topRow, bottomRow;
        float spin = 20f, phase;

        public override float MarkerHeight => 0.42f;
        public override Vector3 WorkPoint => transform.TransformPoint(0, 0.06f, -0.06f);
        public override float AutoInterval => 1.1f;

        public static char Pair(char b) => b == 'A' ? 'T' : b == 'T' ? 'A' : b == 'G' ? 'C' : 'G';

        static Color BaseColor(char b) =>
            b == 'A' ? new Color(0.35f, 0.9f, 0.45f) : b == 'T' ? new Color(1f, 0.45f, 0.35f) : b == 'G' ? new Color(1f, 0.82f, 0.25f) : new Color(0.35f, 0.6f, 1f);

        protected override void Build()
        {
            int gapCount = Mathf.Clamp(Data.spec.steps, 2, 5);
            top = new char[Pairs]; bottom = new char[Pairs]; filled = new bool[Pairs];
            for (int i = 0; i < Pairs; i++) { top[i] = BaseSet[rng.Next(4)]; bottom[i] = Pair(top[i]); filled[i] = true; }
            gaps = new List<int>();
            while (gaps.Count < gapCount) { int g = 1 + rng.Next(Pairs - 2); if (!gaps.Contains(g)) gaps.Add(g); }
            gaps.Sort();
            foreach (var g in gaps) filled[g] = false;

            Tray(0.56f, 0.2f);
            Header("GENE SPLICER", 0.39f);
            // Glass display case base for the hologram.
            Bar("Emitter", new Vector3(0, 0.03f, 0.03f), new Vector3(0.5f, 0.012f, 0.06f), new Color(0.1f, 0.3f, 0.45f));

            var backbone = UiFactory.LitMat(new Color(0.75f, 0.82f, 0.9f));
            for (int i = 0; i < Pairs; i++)
            {
                topHalf[i] = Mesh("TopBase", Shapes.Rod(0.0045f, 1f, 10), UiFactory.Mat(BaseColor(top[i])), Vector3.zero).transform;
                botHalf[i] = Mesh("BottomBase", Shapes.Rod(0.0045f, 1f, 10), UiFactory.Mat(BaseColor(bottom[i])), Vector3.zero).transform;
                botRend[i] = botHalf[i].GetComponent<Renderer>();
                topBall[i] = UiFactory.Primitive(PrimitiveType.Sphere, "Backbone", transform, Vector3.zero, Vector3.one * 0.013f, Color.white, lit: true).transform;
                topBall[i].GetComponent<Renderer>().sharedMaterial = backbone;
                botBall[i] = UiFactory.Primitive(PrimitiveType.Sphere, "Backbone", transform, Vector3.zero, Vector3.one * 0.013f, Color.white, lit: true).transform;
                botBallRend[i] = botBall[i].GetComponent<Renderer>();
                botBallRend[i].sharedMaterial = backbone;
            }

            topRow = UiFactory.Text(transform, "", new Vector3(0, 0.31f, -0.01f), 0.016f, Color.white);
            bottomRow = UiFactory.Text(transform, "", new Vector3(0, 0.12f, -0.01f), 0.016f, Color.white);
            UiFactory.Text(transform, "A pairs with T   ·   G pairs with C", new Vector3(0, 0.335f, -0.01f), 0.01f, Palette.Muted);

            for (int b = 0; b < 4; b++)
            {
                char c = BaseSet[b];
                Btn(c.ToString(), new Vector3(-0.15f + b * 0.1f, 0.06f, -0.075f), BaseColor(c) * 0.5f + Color.black * 0.5f, () => Insert(c), 0.07f, 0.04f);
            }
            current = 0;
            Refresh();
            Say("Fill the flashing gap with the matching base", Palette.Muted);
        }

        void Update()
        {
            phase += spin * Time.deltaTime;
            bool blink = Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            for (int i = 0; i < Pairs; i++)
            {
                float x = -Span / 2f + i * Span / (Pairs - 1);
                float a = (i * 36f + phase) * Mathf.Deg2Rad;
                var t = new Vector3(x, HelixY + Mathf.Cos(a) * HelixR, Mathf.Sin(a) * HelixR + 0.03f);
                var b = new Vector3(x, HelixY - Mathf.Cos(a) * HelixR, -Mathf.Sin(a) * HelixR + 0.03f);
                var mid = (t + b) * 0.5f;
                Place(topHalf[i], t, mid);
                topBall[i].localPosition = t;
                botBall[i].localPosition = b;
                bool gapHere = !filled[i];
                botHalf[i].gameObject.SetActive(!gapHere);
                if (!gapHere) Place(botHalf[i], b, mid);
                botBallRend[i].enabled = !gapHere || (i == CurrentGap && blink);
            }
        }

        void Place(Transform rod, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            rod.localPosition = from;
            rod.localRotation = d.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, d.normalized) : Quaternion.identity;
            rod.localScale = new Vector3(1f, d.magnitude, 1f);
        }

        int CurrentGap => current < gaps.Count ? gaps[current] : -1;

        void Insert(char c)
        {
            if (Completed || PokeTarget.InputBlocked || current >= gaps.Count) return;
            int g = gaps[current];
            if (c != bottom[g])
            {
                Say($"{top[g]} pairs with {Pair(top[g])}, not {c}", Palette.Danger);
                FailAttempt();
                return;
            }
            filled[g] = true;
            botRend[g].sharedMaterial = UiFactory.Mat(BaseColor(c));
            current++;
            RoomZero.Audio.Sfx.Play("chime");
            Fx.Burst(transform.TransformPoint(-Span / 2f + g * Span / (Pairs - 1), HelixY, 0.03f), BaseColor(c), 0.06f);
            Refresh();
            if (current >= gaps.Count) StartCoroutine(Finish());
            else Say($"{gaps.Count - current} gap{(gaps.Count - current == 1 ? "" : "s")} left", Palette.Success);
        }

        IEnumerator Finish()
        {
            Say("Gene repaired! Sequencing...", Palette.Success);
            RoomZero.Audio.Sfx.Play("grow");
            for (float t = 0; t < 1.2f; t += Time.deltaTime) { spin = Mathf.Lerp(20f, 260f, t / 1.2f); yield return null; }
            spin = 60f;
            title.text = "GENE REPAIRED";
            title.color = Palette.Success;
            Say("Drought-proof, high-protein crop unlocked", Palette.Success);
            gameObject.AddComponent<Sparkles>().color = Palette.Holo;
            Complete();
        }

        void Refresh()
        {
            var a = new System.Text.StringBuilder();
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < Pairs; i++)
            {
                a.Append(top[i]).Append(' ');
                b.Append(filled[i] ? bottom[i] : (i == CurrentGap ? '?' : '_')).Append(' ');
            }
            topRow.text = a.ToString().TrimEnd();
            bottomRow.text = b.ToString().TrimEnd();
        }

        protected override string AutoStep()
        {
            if (current >= gaps.Count) return null;
            char top0 = top[gaps[current]], want = Pair(top0);
            Insert(want);
            return $"{top0} pairs with {want}.";
        }

        public override void ShowHint()
        {
            int g = CurrentGap;
            if (g >= 0) Say($"The gap is under {top[g]}: it needs {Pair(top[g])}", Palette.Accent);
        }

        public override string Guide(string where) => $"look at the letter above the flashing gap ({where}) and press its partner: A-T, G-C.";

        public override string StatusLine() =>
            Completed ? "Gene splice:  REPAIRED  (done)" : $"Gene splice:  {current}/{gaps.Count} bases placed";
    }
}
