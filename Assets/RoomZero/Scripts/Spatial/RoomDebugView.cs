// Draws the room:
// - Desktop / mock room: a bio-lab greenhouse built in code (tiled floor, lab tables, cabinets,
//   glass greenhouse wall, planters and plants). On Quest the REAL room shows through passthrough.
// - Developer view (press G on desktop): green outline = surface ZERO may use, red = rejected.
// - Virtual desks (seated fallback) are always drawn.
// Owner: #2 MR / Room Understanding (environment art: anyone; swap for real models later).

using UnityEngine;
using RoomZero.UI;

namespace RoomZero.Spatial
{
    public partial class RoomDebugView : MonoBehaviour
    {
        public static RoomDebugView Instance { get; private set; }
        Transform outlines, environment;
        static Texture2D tileTex;

        void Awake() { Instance = this; }

        /// Developer view toggle (G on desktop): surface outlines on/off.
        public static void ToggleOutlines()
        {
            if (Instance && Instance.outlines) Instance.outlines.gameObject.SetActive(!Instance.outlines.gameObject.activeSelf);
        }

        RoomMap lastMap;
        bool lastDebug;

        /// Current room theme (one per campaign room). Changing it rebuilds the room around the player.
        public LabTheme Theme { get; private set; } = LabTheme.VaultLab;

        public void SetTheme(LabTheme theme)
        {
            if (theme == Theme && environment) return;
            Theme = theme;
            ApplyAtmosphere(theme);
            if (lastMap != null) Show(lastMap, lastDebug);
        }

        public void Show(RoomMap map, bool debug)
        {
            lastMap = map; lastDebug = debug;
            Clear();
            outlines = new GameObject("DevOutlines").transform;
            outlines.SetParent(transform, false);
            environment = new GameObject("Environment").transform;
            environment.SetParent(transform, false);

            if (map.source.StartsWith("MOCK")) DrawLab(map);
            else
            {
                DrawThemeAccents(map);   // headset: keep the real room, add a few themed holograms
                var cfg = UiFactory.Config;
                if (cfg == null || cfg.labWindow)
                {
                    try { LabWindow.Create(this, map, Theme, environment); }
                    catch (System.Exception e) { Debug.LogWarning("[RZ] lab window skipped: " + e.Message); }
                }
            }
            foreach (var s in map.surfaces)
                if (s.type == "virtual_desk") DrawVirtualDesk(s);

            if (debug) DrawOutlines(map);
            // Desktop: clean "game" look by default; the developer view is one key away (G).
            outlines.gameObject.SetActive(debug && !RoomZeroBootstrap.DesktopMode);
        }

        // ------------------------------------------------------------------ developer outlines

        void DrawOutlines(RoomMap map)
        {
            foreach (var s in map.surfaces)
            {
                var color = s.allowed ? new Color(0.45f, 0.9f, 0.6f) : new Color(0.95f, 0.45f, 0.45f);
                var t = new GameObject($"Surface_{s.id}").transform;
                t.SetParent(outlines, false);
                t.position = s.Center + Vector3.up * 0.003f;
                Vector3 n = s.Normal.sqrMagnitude > 0.01f ? s.Normal : Vector3.up;
                t.rotation = s.IsHorizontal ? Quaternion.Euler(0, s.yawDegrees, 0) : Quaternion.LookRotation(-n, Vector3.up);
                if (s.IsHorizontal) Outline(t, s.Size.x, s.Size.z, color, true);
                else Outline(t, s.Size.x, s.Size.y, color, false);
                if (!RoomZeroBootstrap.DesktopMode)
                {
                    var label = UiFactory.Text(t, $"{s.id} ({s.type}) {s.confidence:0.00}", s.IsHorizontal ? new Vector3(0, 0.02f, 0) : Vector3.zero, 0.012f, color);
                    if (s.IsHorizontal) label.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
            }
            foreach (var zid in map.excludedZoneIds)
            {
                var z = map.FindZone(zid);
                if (z == null) continue;
                var zone = new GameObject($"Excluded_{zid}").transform;
                zone.SetParent(outlines, false);
                zone.position = new Vector3(z.Bounds.center.x, 0.006f, z.Bounds.center.z);
                Outline(zone, z.Bounds.size.x, z.Bounds.size.z, Palette.Danger, true);
            }
        }

        void DrawVirtualDesk(SurfaceInfo s)
        {
            var go = UiFactory.Primitive(PrimitiveType.Cube, "VirtualDesk", transform, Vector3.zero, new Vector3(s.Size.x, 0.012f, s.Size.z), new Color(0.2f, 0.55f, 0.48f));
            go.transform.position = s.Center - Vector3.up * 0.008f;
            go.transform.rotation = Quaternion.Euler(0, s.yawDegrees, 0);
        }

        // ------------------------------------------------------------------ modern lab
        //
        // Top-down plan (metres; x = right, z = forward from the seated player at 0,0):
        //   play surfaces (from the room map): desk  x -0.30..0.30  z 0.30..0.80
        //                                      left cabinet  x -0.68..-0.33  z 0.25..0.55
        //                                      right cabinet x  0.35..0.75   z 0.25..0.65
        //   decor (never inside the play area):
        //     biosafety cabinet  x -0.45..0.45  z 1.46..1.86
        //     benches            x -1.80..-0.55 and 0.55..1.80, z 1.50..1.86 (+ wall cabinets above)
        //     growth chamber     x  1.40..1.85  z -0.20..0.60       lab chair  x -1.1, z 1.1
        //     door on the left wall at z 0.5 (the map's "unsafe: door" zone)

        static readonly Color Steel = new Color(0.74f, 0.77f, 0.8f);
        static readonly Color SteelDark = new Color(0.45f, 0.48f, 0.52f);
        static readonly Color WallGrey = new Color(0.6f, 0.64f, 0.68f);
        static readonly Color Bench = new Color(0.86f, 0.88f, 0.9f);
        static readonly Color Epoxy = new Color(0.24f, 0.26f, 0.29f);
        static readonly Color Glow = new Color(0.92f, 0.97f, 1f);

        static readonly Color[] Liquids =
        {
            new Color(1f, 0.3f, 0.75f), new Color(0.2f, 0.88f, 1f), new Color(0.6f, 1f, 0.2f),
            new Color(1f, 0.68f, 0.2f), new Color(0.62f, 0.42f, 1f), new Color(0.3f, 0.52f, 1f)
        };

        Quaternion rot; Vector3 fwd, right, o;
        Vector3 P(float x, float y, float z) => o + right * x + Vector3.up * y + fwd * z;   // plan -> world

        void DrawLab(RoomMap map)
        {
            o = map.PlayerOrigin; o.y = 0;
            var desk = map.FindSurface("surface-desk");
            rot = Quaternion.Euler(0, desk != null ? desk.yawDegrees : 0f, 0);
            fwd = rot * Vector3.forward; right = rot * Vector3.right;

            var floor = Box("Floor", P(0, -0.005f, 0.4f), new Vector3(4f, 0.01f, 5f), rot, Color.white);
            floor.GetComponent<Renderer>().sharedMaterial = FloorMaterial(Theme);

            // Play surfaces from the room map
            foreach (var s in map.surfaces)
            {
                if (s.type == "virtual_desk" || !s.IsHorizontal) continue;
                float depth = Vector3.Dot(s.Center - o, fwd);
                if (depth > 1.4f || depth < 0.05f) continue;   // behind the back wall / behind the seat: not drawn
                var srot = Quaternion.Euler(0, s.yawDegrees, 0);
                Shapes.ContactShadow(environment, new Vector3(s.Center.x, 0, s.Center.z), s.Size.x * 1.25f, s.Size.z * 1.25f, srot, 0.45f);
                if (s.type == "storage") DrawerCabinet(s.Center, s.Size, srot);
                else LabTable(s.Center, s.Size, srot, s.confidence < 0.6f);
            }

            // Photo-real option: an artist-made lab prefab replaces all generated decor.
            var cfg = UiFactory.Config;
            if (cfg && cfg.labEnvironmentModel)
            {
                var lab = Instantiate(cfg.labEnvironmentModel, environment, false);
                lab.name = "LabEnvironment (custom)";
                lab.transform.position = o;
                lab.transform.rotation = rot;
                lab.transform.localScale = Vector3.one * Mathf.Max(0.01f, cfg.labEnvironmentScale);
                floor.SetActive(false);
                return;
            }

            DrawDecor();
        }

        /// Walls, equipment and atmosphere of the current theme around o/rot (no play tables).
        /// Also used to build the room seen through the MR lab window.
        void DrawDecor()
        {
            if (Theme == LabTheme.Greenhouse) { DrawGreenhouse(); return; }
            if (Theme == LabTheme.GeneLab) { DrawGeneLab(); return; }

            // Walls: back + sides, grey panels with seams
            PanelWall(P(0, 1.3f, 1.9f), 3.8f, 2.6f, rot);
            PanelWall(P(-1.9f, 1.3f, 0.4f), 3.0f, 2.6f, rot * Quaternion.Euler(0, -90, 0));
            PanelWall(P(1.9f, 1.3f, 0.4f), 3.0f, 2.6f, rot * Quaternion.Euler(0, 90, 0));
            Door(P(-1.88f, 0, 0.5f));

            BiosafetyCabinet(P(0, 0, 1.66f));
            BenchUnit(P(-1.18f, 0, 1.68f), 1.25f);
            BenchUnit(P(1.18f, 0, 1.68f), 1.25f);
            WallCabinet(P(-1.18f, 1.38f, 1.74f), 1.2f, 0);
            WallCabinet(P(1.18f, 1.38f, 1.74f), 1.2f, 3);

            var facing = Quaternion.LookRotation(-fwd, Vector3.up);
            Microscope(P(-1.55f, 0.9f, 1.66f), rot);
            TubeRack(P(-1.1f, 0.9f, 1.62f), 0);
            Flask(P(-0.78f, 0.9f, 1.66f), Liquids[1]);
            Monitor(P(0.88f, 0.9f, 1.72f), facing);
            Microscope(P(1.68f, 0.9f, 1.66f), rot);
            Lamp(P(-1.32f, 0.9f, 1.76f), new Color(1f, 0.94f, 0.85f));

            RoundFlask(P(-0.62f, 0.9f, 1.62f), Liquids[2]);
            Beaker(P(0.62f, 0.9f, 1.6f), Liquids[0]);
            Shapes.PottedPlant(environment, P(-1.65f, 0, 1.05f), 2.4f, 41, new Color(0.93f, 0.94f, 0.95f));
            Shapes.PottedPlant(environment, P(-0.68f, 0.9f, 1.79f), 0.7f, 7, new Color(0.78f, 0.45f, 0.32f));
            GrowthChamber(P(1.62f, 0, 0.2f));
            LabChair(P(1.15f, 0, 1.1f), rot * Quaternion.Euler(0, 200, 0));    // clear of Dr. Asha's walkway and the entrance doors

            Centrifuge(P(1.37f, 0.9f, 1.7f));
            PetriStack(P(-0.9f, 0.9f, 1.55f));
            PetriStack(P(-0.92f, 0.9f, 1.76f));
            WashBottle(P(1.16f, 0.9f, 1.8f));
            PipetteStand(P(-1.42f, 0.9f, 1.58f));
            FireExtinguisher(P(1.8f, 0, 1.15f));
            WallClock(P(0, 2.12f, 1.875f));
            Ceiling();

            // Contact shadows ground the big furniture on the floor.
            Quaternion r0 = rot;
            Shapes.ContactShadow(environment, P(0, 0, 1.66f), 1.1f, 0.6f, r0, 0.5f);
            Shapes.ContactShadow(environment, P(-1.18f, 0, 1.68f), 1.45f, 0.55f, r0, 0.5f);
            Shapes.ContactShadow(environment, P(1.18f, 0, 1.68f), 1.45f, 0.55f, r0, 0.5f);
            Shapes.ContactShadow(environment, P(1.62f, 0, 0.2f), 0.65f, 0.95f, r0, 0.5f);
            Shapes.ContactShadow(environment, P(1.15f, 0, 1.1f), 0.7f, 0.7f, r0, 0.4f);
            Shapes.ContactShadow(environment, P(-1.65f, 0, 1.05f), 0.55f, 0.55f, r0, 0.45f);

            AddReflections(P(0, 1.3f, 0.6f), new Vector3(4f, 2.6f, 3.2f));
        }

        // ---- room shell & atmosphere -------------------------------------------------------

        void Ceiling()
        {
            var c = Box("Ceiling", P(0, 2.62f, 0.4f), new Vector3(3.8f, 0.04f, 3.2f), rot, Color.white);
            c.GetComponent<Renderer>().sharedMaterial = Shapes.CeilingTile;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    Vector3 lp = P(-0.8f + i * 1.6f, 2.595f, 0.1f + j * 1.1f);
                    Box("LEDPanelFrame", lp + Vector3.up * 0.002f, new Vector3(0.62f, 0.012f, 0.62f), rot, SteelDark);
                    Box("LEDPanel", lp - Vector3.up * 0.004f, new Vector3(0.58f, 0.006f, 0.58f), rot, new Color(0.97f, 0.99f, 1f), lit: false);
                    if (j == 1) AddLight(lp - Vector3.up * 0.25f, new Color(0.98f, 0.99f, 1f), 3.2f, 0.7f);
                }
        }

        /// Captures the room once so steel, glass and liquids reflect the lab instead of a flat colour.
        void AddReflections(Vector3 centre, Vector3 size)
        {
            var go = new GameObject("LabReflections");
            go.transform.SetParent(environment, false);
            go.transform.position = centre;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.boxProjection = true;
            probe.size = size;
            probe.resolution = 256;
            probe.intensity = 1f;
            StartCoroutine(RenderProbeSoon(probe));
        }

        System.Collections.IEnumerator RenderProbeSoon(ReflectionProbe probe)
        {
            yield return null;   // let every prop exist and get its material first
            yield return null;
            if (probe) probe.RenderProbe();
        }

        // ---- extra lab equipment -----------------------------------------------------------

        /// Benchtop centrifuge: rounded white body, tinted dome lid, control panel.
        void Centrifuge(Vector3 p)
        {
            Part(Shapes.RoundedBox(new Vector3(0.24f, 0.12f, 0.26f), 0.035f), Shapes.GlossWhite, p + Vector3.up * 0.06f, rot);
            Shapes.NoShadow(Shapes.Make("Lid", Shapes.Dome(0.095f, 0.05f), Shapes.Glass(new Color(0.3f, 0.45f, 0.6f, 0.55f)), environment, p + Vector3.up * 0.12f + fwd * 0.02f));
            Shapes.Make("LidRing", Shapes.Rod(0.1f, 0.006f, 28), Shapes.DarkSteel, environment, p + Vector3.up * 0.118f + fwd * 0.02f);
            Part(Shapes.RoundedBox(new Vector3(0.14f, 0.04f, 0.004f), 0.002f), UiFactory.Mat(new Color(0.05f, 0.12f, 0.14f)), p + Vector3.up * 0.06f - fwd * 0.131f, rot);
            Part(Shapes.RoundedBox(new Vector3(0.05f, 0.018f, 0.002f), 0.001f), UiFactory.Mat(Palette.Holo), p + Vector3.up * 0.062f - fwd * 0.134f - right * 0.03f, rot);
            foreach (int s in new[] { 0, 1 })
                Shapes.Make("Button", Shapes.Rod(0.007f, 0.004f, 12), UiFactory.Mat(s == 0 ? Palette.Success : Palette.Danger), environment, p + Vector3.up * 0.06f - fwd * 0.133f + right * (0.03f + s * 0.025f), Quaternion.LookRotation(Vector3.up, -fwd));
            Shapes.ContactShadow(environment, p, 0.34f, 0.36f, rot, 0.4f);
        }

        /// A stack of petri dishes with amber agar.
        void PetriStack(Vector3 p)
        {
            var agar = Shapes.Surface(new Color(0.95f, 0.8f, 0.45f), 0.8f, 0f);
            var colony = UiFactory.LitMat(new Color(0.95f, 0.95f, 0.9f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 dp = p + Vector3.up * (i * 0.015f);
                Shapes.Make("Agar", Shapes.Rod(0.041f, 0.006f, 28), agar, environment, dp + Vector3.up * 0.001f);
                Shapes.NoShadow(Shapes.Make("Dish", Shapes.Rod(0.045f, 0.014f, 28), Shapes.Glass(new Color(0.9f, 0.97f, 1f, 0.25f)), environment, dp));
                if (i == 3)
                    for (int k = 0; k < 6; k++)
                        Shapes.Make("Colony", Shapes.Rod(0.004f + (k % 3) * 0.002f, 0.002f, 10), colony, environment, dp + Vector3.up * 0.007f + Quaternion.Euler(0, k * 61f, 0) * Vector3.forward * (0.012f + (k % 2) * 0.012f));
            }
            Shapes.ContactShadow(environment, p, 0.12f, 0.12f, rot, 0.35f);
        }

        /// Squeezy wash bottle with a bent nozzle.
        void WashBottle(Vector3 p)
        {
            var body = Shapes.Lathe("washbottle", new[] { new Vector2(0, 0), new Vector2(0.03f, 0), new Vector2(0.032f, 0.008f), new Vector2(0.032f, 0.1f), new Vector2(0.024f, 0.118f), new Vector2(0.012f, 0.124f), new Vector2(0.012f, 0.134f), new Vector2(0, 0.134f) });
            Shapes.Make("WashBottle", body, Shapes.Surface(new Color(0.85f, 0.92f, 0.95f), 0.5f, 0f), environment, p);
            Shapes.Make("WashCap", Shapes.Rod(0.014f, 0.016f, 16), UiFactory.LitMat(new Color(0.15f, 0.45f, 0.8f)), environment, p + Vector3.up * 0.132f);
            Shapes.Make("Nozzle", Shapes.Rod(0.003f, 0.05f, 8), Shapes.Surface(new Color(0.85f, 0.92f, 0.95f), 0.5f, 0f), environment, p + Vector3.up * 0.146f, Quaternion.FromToRotation(Vector3.up, (Vector3.up * 0.6f - fwd).normalized));
            Part(Shapes.RoundedBox(new Vector3(0.03f, 0.035f, 0.0008f), 0.0003f), Shapes.LabelMat, p + Vector3.up * 0.06f - fwd * 0.0325f, Quaternion.LookRotation(fwd));
            Shapes.ContactShadow(environment, p, 0.09f, 0.09f, rot, 0.35f);
        }

        /// Carousel of three pipettes on a stand.
        void PipetteStand(Vector3 p)
        {
            Shapes.Make("StandBase", Shapes.Rod(0.05f, 0.015f, 24), Shapes.GlossWhite, environment, p);
            Shapes.Make("StandPole", Shapes.Rod(0.006f, 0.24f, 12), Shapes.Steel, environment, p + Vector3.up * 0.015f);
            Color[] buttons = { new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.45f, 1f), new Color(0.9f, 0.2f, 0.2f) };
            for (int i = 0; i < 3; i++)
            {
                Vector3 dir = Quaternion.Euler(0, -60f + i * 60f, 0) * (-fwd);
                Vector3 pp = p + dir * 0.03f + Vector3.up * 0.06f;
                Shapes.Make("PipetteBody", Shapes.Lathe("pipette", new[] { new Vector2(0, 0), new Vector2(0.004f, 0.0f), new Vector2(0.007f, 0.05f), new Vector2(0.011f, 0.08f), new Vector2(0.011f, 0.15f), new Vector2(0.008f, 0.16f), new Vector2(0, 0.16f) }, 14), Shapes.GlossWhite, environment, pp);
                Shapes.Make("PipetteTip", Shapes.Lathe("ptip", new[] { new Vector2(0, 0), new Vector2(0.0035f, 0.045f), new Vector2(0, 0.045f) }, 10), Shapes.Surface(new Color(0.95f, 0.92f, 0.6f), 0.6f, 0f), environment, pp + Vector3.down * 0.045f);
                Shapes.Make("Plunger", Shapes.Rod(0.007f, 0.012f, 12), UiFactory.LitMat(buttons[i]), environment, pp + Vector3.up * 0.16f);
            }
            Shapes.ContactShadow(environment, p, 0.14f, 0.14f, rot, 0.35f);
        }

        void FireExtinguisher(Vector3 p)
        {
            var body = Shapes.Lathe("extinguisher", Join2(new[] { new Vector2(0, 0), new Vector2(0.07f, 0) }, Arc2(0.07f, 0.4f, 0, 90, 8)));
            Shapes.Make("Extinguisher", body, Shapes.Surface(new Color(0.8f, 0.08f, 0.06f), 0.75f, 0f), environment, p + Vector3.up * 0.2f);
            Shapes.Make("Valve", Shapes.Rod(0.018f, 0.06f, 12), Shapes.Steel, environment, p + Vector3.up * 0.67f);
            var handle = Shapes.RoundCube("Handle", environment, Vector3.zero, new Vector3(0.012f, 0.02f, 0.11f), Color.white, 0.005f);
            handle.GetComponent<Renderer>().sharedMaterial = Shapes.BlackPlastic;
            handle.transform.position = p + Vector3.up * 0.74f; handle.transform.rotation = rot * Quaternion.Euler(-10, 0, 0);
            Shapes.Make("Hose", Shapes.Rod(0.008f, 0.42f, 10), Shapes.BlackPlastic, environment, p + Vector3.up * 0.28f + fwd * 0.075f);
            Part(Shapes.RoundedBox(new Vector3(0.08f, 0.1f, 0.001f), 0.0005f), Shapes.LabelMat, p + Vector3.up * 0.42f - right * 0.0705f, Quaternion.LookRotation(right));
            Box("WallBracket", p + Vector3.up * 0.55f + right * 0.06f, new Vector3(0.02f, 0.1f, 0.08f), rot, SteelDark);
            Shapes.ContactShadow(environment, p + Vector3.up * 0.2f, 0.22f, 0.22f, rot, 0.4f).transform.position = p + Vector3.up * 0.0015f;
        }

        void WallClock(Vector3 p)
        {
            var face = Quaternion.LookRotation(Vector3.up, -fwd);   // disc axis (+y) points at the room
            Shapes.Make("ClockRim", Shapes.Rod(0.16f, 0.03f, 40), Shapes.DarkSteel, environment, p + fwd * 0.03f, face);
            Shapes.Make("ClockFace", Shapes.Rod(0.145f, 0.032f, 40), Shapes.GlossWhite, environment, p + fwd * 0.03f, face);
            for (int i = 0; i < 12; i++)
            {
                var tick = Shapes.RoundCube("Tick", environment, Vector3.zero, new Vector3(0.008f, i % 3 == 0 ? 0.03f : 0.016f, 0.003f), new Color(0.15f, 0.15f, 0.18f), 0.001f);
                tick.transform.rotation = rot * Quaternion.Euler(0, 0, i * 30f);
                tick.transform.position = p - fwd * 0.003f + tick.transform.up * 0.12f;
            }
            var hour = Shapes.RoundCube("HourHand", environment, Vector3.zero, new Vector3(0.01f, 0.07f, 0.003f), new Color(0.12f, 0.12f, 0.15f), 0.002f);
            hour.transform.rotation = rot * Quaternion.Euler(0, 0, -300f); hour.transform.position = p - fwd * 0.005f + hour.transform.up * 0.03f;
            var minute = Shapes.RoundCube("MinuteHand", environment, Vector3.zero, new Vector3(0.007f, 0.11f, 0.003f), new Color(0.12f, 0.12f, 0.15f), 0.002f);
            minute.transform.rotation = rot * Quaternion.Euler(0, 0, -60f); minute.transform.position = p - fwd * 0.007f + minute.transform.up * 0.05f;
        }

        static Vector2[] Arc2(float r, float cy, float fromDeg, float toDeg, int steps)
        {
            var pts = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
                pts[i] = new Vector2(Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        static Vector2[] Join2(Vector2[] a, Vector2[] b)
        {
            var r = new Vector2[a.Length + b.Length];
            a.CopyTo(r, 0); b.CopyTo(r, a.Length);
            return r;
        }

        void LabTable(Vector3 top, Vector3 size, Quaternion r, bool faded)
        {
            Box("TableTop", top + Vector3.down * 0.02f, new Vector3(size.x, 0.04f, size.z), r, faded ? new Color(0.7f, 0.72f, 0.74f) : Bench);
            float legH = top.y - 0.04f, ix = size.x / 2f - 0.05f, iz = size.z / 2f - 0.05f;
            foreach (var c in new[] { new Vector2(ix, iz), new Vector2(-ix, iz), new Vector2(ix, -iz), new Vector2(-ix, -iz) })
            {
                Vector3 foot = new Vector3(top.x, 0, top.z) + r * new Vector3(c.x, 0, c.y);
                Shapes.Make("Leg", Shapes.Rod(0.016f, legH), Shapes.Steel, environment, foot);
                Shapes.Make("Foot", Shapes.Rod(0.022f, 0.012f), Shapes.BlackPlastic, environment, foot);
            }
            if (size.x > 0.45f) Box("Rail", new Vector3(top.x, 0.2f, top.z), new Vector3(size.x - 0.1f, 0.022f, 0.022f), r, Steel);
        }

        /// Stainless steel cabinet with drawers, bar handles and a dark epoxy worktop.
        void DrawerCabinet(Vector3 top, Vector3 size, Quaternion r)
        {
            float h = top.y - 0.03f;
            Box("Cabinet", new Vector3(top.x, h / 2f + 0.04f, top.z), new Vector3(size.x, h - 0.08f, size.z), r, Steel);
            Box("Plinth", new Vector3(top.x, 0.02f, top.z), new Vector3(size.x - 0.04f, 0.04f, size.z - 0.04f), r, Epoxy);
            Box("Worktop", top + Vector3.down * 0.015f, new Vector3(size.x + 0.02f, 0.03f, size.z + 0.02f), r, Epoxy);
            Vector3 toPlayer = o - top; toPlayer.y = 0; toPlayer.Normalize();
            var face = Quaternion.LookRotation(-toPlayer, Vector3.up);
            float half = Mathf.Min(size.x, size.z) / 2f;
            int drawers = Mathf.Max(2, Mathf.RoundToInt(h / 0.22f));
            float dh = (h - 0.1f) / drawers;
            for (int i = 0; i < drawers; i++)
            {
                Vector3 p = new Vector3(top.x, 0.06f + dh * (i + 0.5f), top.z) + toPlayer * half;
                Box("DrawerFront", p + toPlayer * 0.004f, new Vector3(Mathf.Min(size.x, size.z) * 0.92f, dh - 0.012f, 0.012f), face, Steel);
                BarHandle(p + toPlayer * 0.022f + Vector3.up * (dh * 0.25f), face, 0.12f);
            }
        }

        /// A rounded bar handle on two stand-offs (like real cabinet pulls).
        void BarHandle(Vector3 p, Quaternion face, float len)
        {
            var bar = Shapes.Make("Handle", Shapes.Rod(0.005f, len, 12), Shapes.Steel, environment, p - face * Vector3.right * (len / 2f), face * Quaternion.Euler(0, 0, -90));
            foreach (int s in new[] { -1, 1 })
                Shapes.Make("HandlePost", Shapes.Rod(0.004f, 0.016f, 10), Shapes.Steel, environment, p + face * new Vector3(s * len * 0.4f, 0, 0), face * Quaternion.Euler(90, 0, 0));
            bar.name = "Handle";
        }

        void PanelWall(Vector3 center, float width, float height, Quaternion r)
        {
            Box("Wall", center, new Vector3(width, height, 0.04f), r, WallGrey);
            int panels = Mathf.RoundToInt(width / 0.6f);
            for (int i = 1; i < panels; i++)
                Box("Seam", center + r * new Vector3(-width / 2f + i * width / panels, 0, -0.022f), new Vector3(0.008f, height, 0.004f), r, new Color(0.5f, 0.54f, 0.58f));
            Box("Skirting", center + r * new Vector3(0, -height / 2f + 0.06f, -0.025f), new Vector3(width, 0.12f, 0.01f), r, SteelDark);
        }

        void Door(Vector3 floorPos)
        {
            var r = rot * Quaternion.Euler(0, 90, 0);
            Box("DoorFrame", floorPos + Vector3.up * 1.06f, new Vector3(1.0f, 2.12f, 0.03f), r, SteelDark);
            Box("Door", floorPos + Vector3.up * 1.03f + right * 0.02f, new Vector3(0.9f, 2.04f, 0.04f), r, Steel);
            var win = Shapes.NoShadow(Shapes.RoundCube("DoorWindowGlass", environment, Vector3.zero, new Vector3(0.36f, 0.5f, 0.012f), Color.white, 0.02f));
            win.GetComponent<Renderer>().sharedMaterial = Shapes.Glass(new Color(0.75f, 0.9f, 1f, 0.35f));
            win.transform.position = floorPos + Vector3.up * 1.5f + right * 0.045f; win.transform.rotation = r;
            BarHandle(floorPos + Vector3.up * 1.0f + right * 0.06f + fwd * 0.33f, Quaternion.LookRotation(-right) * Quaternion.Euler(0, 0, 90), 0.2f);
        }

        /// Biosafety cabinet like the reference photo: steel base with drawers, lit work chamber,
        /// a real glass sash, and a top console with a glowing display.
        void BiosafetyCabinet(Vector3 floorPos)
        {
            const float w = 0.9f, d = 0.4f;
            Box("BSCBase", floorPos + Vector3.up * 0.44f, new Vector3(w, 0.8f, d), rot, Steel);
            Box("BSCPlinth", floorPos + Vector3.up * 0.02f, new Vector3(w - 0.04f, 0.04f, d - 0.04f), rot, Epoxy);
            var face = Quaternion.LookRotation(fwd);   // +z into the furniture
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = floorPos + Vector3.up * (0.18f + i * 0.255f) - fwd * (d / 2f);
                Box("BSCDrawer", p - fwd * 0.005f, new Vector3(w * 0.94f, 0.24f, 0.012f), rot, Steel);
                BarHandle(p - fwd * 0.022f + Vector3.up * 0.07f, face, 0.2f);
            }
            Box("BSCWorkSurface", floorPos + Vector3.up * 0.86f, new Vector3(w, 0.04f, d), rot, Epoxy);
            Box("BSCBack", floorPos + Vector3.up * 1.2f + fwd * (d / 2f - 0.01f), new Vector3(w, 0.66f, 0.02f), rot, new Color(0.92f, 0.94f, 0.95f));
            foreach (int side in new[] { -1, 1 })
                Box("BSCSide", floorPos + Vector3.up * 1.2f + right * (side * (w / 2f - 0.012f)), new Vector3(0.024f, 0.66f, d), rot, Steel);
            Box("BSCTop", floorPos + Vector3.up * 1.66f, new Vector3(w + 0.04f, 0.26f, d + 0.04f), rot, Steel);
            Box("SashTop", floorPos + Vector3.up * 1.5f - fwd * (d / 2f), new Vector3(w, 0.04f, 0.025f), rot, SteelDark);
            Box("SashBottom", floorPos + Vector3.up * 1.02f - fwd * (d / 2f), new Vector3(w, 0.03f, 0.025f), rot, SteelDark);
            var sash = Shapes.NoShadow(Shapes.RoundCube("SashGlass", environment, Vector3.zero, new Vector3(w - 0.04f, 0.45f, 0.008f), Color.white, 0.004f));
            sash.GetComponent<Renderer>().sharedMaterial = Shapes.Glass(new Color(0.8f, 0.92f, 1f, 0.16f));
            sash.transform.position = floorPos + Vector3.up * 1.26f - fwd * (d / 2f); sash.transform.rotation = rot;
            Box("InteriorLight", floorPos + Vector3.up * 1.48f, new Vector3(w * 0.85f, 0.015f, 0.05f), rot, Glow, lit: false);
            Box("Display", floorPos + Vector3.up * 1.68f - fwd * (d / 2f + 0.025f), new Vector3(0.42f, 0.12f, 0.012f), rot, new Color(0.04f, 0.07f, 0.09f), lit: false);
            for (int i = 0; i < 5; i++)
                Box("Readout", floorPos + Vector3.up * 1.68f - fwd * (d / 2f + 0.033f) + right * (-0.16f + i * 0.08f), new Vector3(0.05f, 0.035f, 0.004f), rot, i % 2 == 0 ? Palette.Success : Palette.Holo, lit: false);
            TubeRack(floorPos + Vector3.up * 0.88f - right * 0.2f, 1);
            Beaker(floorPos + Vector3.up * 0.88f + right * 0.18f, Liquids[4]);
            AddLight(floorPos + Vector3.up * 1.4f, new Color(0.95f, 0.98f, 1f), 1.2f, 1.0f);
        }

        void BenchUnit(Vector3 floorPos, float w)
        {
            const float h = 0.9f, d = 0.36f;
            Box("BenchBody", floorPos + Vector3.up * (h / 2f + 0.02f), new Vector3(w, h - 0.07f, d), rot, Steel);
            Box("BenchPlinth", floorPos + Vector3.up * 0.02f, new Vector3(w - 0.04f, 0.04f, d - 0.04f), rot, Epoxy);
            Box("BenchTop", floorPos + Vector3.up * (h - 0.015f), new Vector3(w + 0.02f, 0.03f, d + 0.02f), rot, Epoxy);
            var face = Quaternion.LookRotation(fwd);   // +z into the furniture
            for (int col = 0; col < 3; col++)
                for (int row = 0; row < 3; row++)
                {
                    Vector3 p = floorPos + right * (-w / 3f + col * w / 3f) + Vector3.up * (0.17f + row * 0.255f) - fwd * (d / 2f);
                    Box("DrawerFront", p - fwd * 0.005f, new Vector3(w / 3f - 0.012f, 0.243f, 0.012f), rot, Steel);
                    BarHandle(p - fwd * 0.022f + Vector3.up * 0.07f, face, 0.12f);
                }
        }

        /// Glass-fronted wall cabinet: steel frame, lit shelves, reagent bottles and flasks.
        void WallCabinet(Vector3 bottomCenter, float w, int colorOffset)
        {
            const float h = 0.72f, d = 0.3f;
            Vector3 c = bottomCenter + Vector3.up * (h / 2f);
            Box("WCBack", c + fwd * (d / 2f - 0.01f), new Vector3(w, h, 0.02f), rot, new Color(0.9f, 0.93f, 0.95f));
            foreach (int side in new[] { -1, 1 })
                Box("WCSide", c + right * (side * (w / 2f - 0.012f)), new Vector3(0.024f, h, d), rot, Steel);
            Box("WCTop", c + Vector3.up * (h / 2f), new Vector3(w, 0.03f, d), rot, Steel);
            Box("WCBottom", c - Vector3.up * (h / 2f), new Vector3(w, 0.03f, d), rot, Steel);
            Box("WCMullion", c - fwd * (d / 2f), new Vector3(0.024f, h, 0.024f), rot, Steel);
            foreach (int side in new[] { -1, 1 })
            {
                var pane = Shapes.NoShadow(Shapes.RoundCube("WCGlassDoor", environment, Vector3.zero, new Vector3(w / 2f - 0.03f, h - 0.04f, 0.006f), Color.white, 0.003f));
                pane.GetComponent<Renderer>().sharedMaterial = Shapes.Glass(new Color(0.8f, 0.92f, 1f, 0.12f));
                pane.transform.position = c - fwd * (d / 2f) + right * (side * w / 4f);
                pane.transform.rotation = rot;
            }
            var amber = Shapes.Glass(new Color(0.55f, 0.28f, 0.05f, 0.82f));
            var blue = Shapes.Glass(new Color(0.15f, 0.35f, 0.75f, 0.7f));
            for (int shelf = 0; shelf < 2; shelf++)
            {
                float y = -h / 2f + 0.02f + shelf * 0.34f;
                Box("WCShelf", c + Vector3.up * y, new Vector3(w - 0.05f, 0.012f, d - 0.03f), rot, Glow, lit: false);
                Vector3 shelfTop = c + Vector3.up * (y + 0.006f);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 bp = shelfTop + right * (-w / 2f + 0.09f + i * (w - 0.18f) / 7f) + fwd * ((i % 2) * 0.05f);
                    int kind = (i + shelf + colorOffset) % 4;
                    if (kind == 0) Shapes.Make("AmberBottle", Shapes.Bottle(), amber, environment, bp, null, 1.1f);
                    else if (kind == 1) Shapes.Make("BlueBottle", Shapes.Bottle(), blue, environment, bp);
                    else if (kind == 2) { Flask(bp, Liquids[(i + colorOffset) % Liquids.Length]); continue; }
                    else
                    {
                        Shapes.Make("ClearBottle", Shapes.Bottle(), Shapes.ClearGlass, environment, bp);
                        Shapes.Make("Reagent", Shapes.TubeLiquid(0.0205f, 0.05f), Liquid(Liquids[(i + shelf) % Liquids.Length]), environment, bp + Vector3.up * 0.001f);
                    }
                    Shapes.Make("Cap", Shapes.Cap(), Shapes.BlackPlastic, environment, bp + Vector3.up * (kind == 0 ? 0.097f : 0.088f));
                    float lr = kind == 0 ? 0.0256f : 0.0234f;
                    Part(Shapes.RoundedBox(new Vector3(0.028f, 0.03f, 0.0008f), 0.0003f), Shapes.LabelMat, bp + Vector3.up * 0.033f - fwd * lr, Quaternion.LookRotation(fwd));
                }
            }
        }

        void GrowthChamber(Vector3 floorPos)
        {
            const float w = 0.45f, h = 1.8f, d = 0.75f;
            Vector3 c = floorPos + Vector3.up * (h / 2f);
            Box("GCBack", c + right * (w / 2f - 0.01f), new Vector3(0.02f, h, d), rot, new Color(0.2f, 0.22f, 0.26f));
            foreach (int side in new[] { -1, 1 })
                Box("GCSide", c + fwd * (side * (d / 2f - 0.01f)), new Vector3(w, h, 0.02f), rot, Shapes.GlossWhite.color);
            Box("GCTop", c + Vector3.up * (h / 2f), new Vector3(w, 0.05f, d), rot, Steel);
            Box("GCBase", floorPos + Vector3.up * 0.1f, new Vector3(w, 0.2f, d), rot, Steel);
            var label = UiFactory.BackedText(environment, "GROWTH CHAMBER", Vector3.zero, 0.03f, Palette.Success, faceCamera: false);
            label.transform.position = c + Vector3.up * (h / 2f + 0.06f) - right * (w / 2f);
            label.transform.rotation = Quaternion.LookRotation(right, Vector3.up);
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = 0.35f + shelf * 0.5f;
                Box("GCShelf", floorPos + Vector3.up * y, new Vector3(w - 0.04f, 0.02f, d - 0.04f), rot, Steel);
                Box("GrowLight", floorPos + Vector3.up * (y + 0.44f), new Vector3(w - 0.08f, 0.015f, d - 0.1f), rot, new Color(0.95f, 0.6f, 1f), lit: false);
                Box("SeedTray", floorPos + Vector3.up * (y + 0.025f), new Vector3(w - 0.12f, 0.03f, d - 0.1f), rot, new Color(0.1f, 0.1f, 0.11f));
                for (int i = 0; i < 4; i++)
                    Plant(floorPos + Vector3.up * (y + 0.04f) + fwd * (-0.25f + i * 0.165f), 0.16f + (i % 2) * 0.04f, shelf * 10 + i);
            }
            var door = Shapes.NoShadow(Shapes.RoundCube("GCGlassDoor", environment, Vector3.zero, new Vector3(0.008f, h - 0.3f, d - 0.03f), Color.white, 0.003f));
            door.GetComponent<Renderer>().sharedMaterial = Shapes.Glass(new Color(0.85f, 0.8f, 1f, 0.1f));
            door.transform.position = floorPos + Vector3.up * (0.2f + (h - 0.3f) / 2f) - right * (w / 2f); door.transform.rotation = rot;
            AddLight(c + Vector3.up * 0.3f - right * 0.1f, new Color(0.95f, 0.7f, 1f), 1.4f, 0.9f);
        }

        void LabChair(Vector3 floorPos, Quaternion r)
        {
            var fabric = Shapes.Surface(new Color(0.13f, 0.14f, 0.16f), 0.15f, 0f);
            for (int i = 0; i < 5; i++)
            {
                var lr = r * Quaternion.Euler(0, i * 72f, 0);
                var leg = Shapes.RoundCube("ChairLeg", environment, Vector3.zero, new Vector3(0.035f, 0.025f, 0.28f), Color.white, 0.01f);
                leg.GetComponent<Renderer>().sharedMaterial = Shapes.Steel;
                leg.transform.rotation = lr;
                leg.transform.position = floorPos + Vector3.up * 0.065f + lr * Vector3.forward * 0.14f;
                Shapes.Make("Caster", Shapes.Rod(0.022f, 0.02f, 14), Shapes.BlackPlastic, environment, floorPos + Vector3.up * 0.022f + lr * Vector3.forward * 0.27f + lr * Vector3.right * 0.01f, lr * Quaternion.Euler(0, 0, 90));
            }
            Shapes.Make("GasLift", Shapes.Rod(0.028f, 0.16f), Shapes.BlackPlastic, environment, floorPos + Vector3.up * 0.07f);
            Shapes.Make("GasLiftChrome", Shapes.Rod(0.018f, 0.22f), Shapes.Steel, environment, floorPos + Vector3.up * 0.22f);
            var seat = Shapes.RoundCube("ChairSeat", environment, Vector3.zero, new Vector3(0.46f, 0.08f, 0.44f), Color.white, 0.035f);
            seat.GetComponent<Renderer>().sharedMaterial = fabric;
            seat.transform.position = floorPos + Vector3.up * 0.48f; seat.transform.rotation = r;
            var post = Shapes.RoundCube("ChairPost", environment, Vector3.zero, new Vector3(0.05f, 0.3f, 0.02f), Color.white, 0.008f);
            post.GetComponent<Renderer>().sharedMaterial = Shapes.Steel;
            post.transform.position = floorPos + Vector3.up * 0.6f + r * new Vector3(0, 0, 0.23f); post.transform.rotation = r;
            var back = Shapes.RoundCube("ChairBack", environment, Vector3.zero, new Vector3(0.42f, 0.38f, 0.06f), Color.white, 0.03f);
            back.GetComponent<Renderer>().sharedMaterial = fabric;
            back.transform.position = floorPos + Vector3.up * 0.9f + r * new Vector3(0, 0, 0.24f); back.transform.rotation = r * Quaternion.Euler(-8, 0, 0);
        }

        // ---- small props ------------------------------------------------------------------

        Material Liquid(Color c) => Shapes.Liquid(c);

        /// Compound microscope: base, C-arm, stage with light, turret with three objectives,
        /// binocular head with eyepieces, and focus knobs.
        void Microscope(Vector3 p, Quaternion facing)
        {
            System.Func<float, float, float, Vector3> L = (x, y, z) => p + facing * new Vector3(x, y, z);
            var white = Shapes.GlossWhite; var black = Shapes.BlackPlastic;
            Shapes.ContactShadow(environment, p, 0.2f, 0.26f, facing, 0.4f);
            Part(Shapes.RoundedBox(new Vector3(0.13f, 0.03f, 0.19f), 0.012f), white, L(0, 0.015f, 0), facing);
            Part(Shapes.RoundedBox(new Vector3(0.045f, 0.17f, 0.05f), 0.015f), white, L(0, 0.11f, 0.06f), facing);              // pillar
            Part(Shapes.RoundedBox(new Vector3(0.04f, 0.035f, 0.1f), 0.014f), white, L(0, 0.2f, 0.03f), facing * Quaternion.Euler(12, 0, 0)); // arm
            Part(Shapes.RoundedBox(new Vector3(0.11f, 0.01f, 0.1f), 0.004f), black, L(0, 0.095f, -0.01f), facing);             // stage
            Part(Shapes.RoundedBox(new Vector3(0.05f, 0.002f, 0.02f), 0.0008f), Shapes.Glass(new Color(0.9f, 0.95f, 1f, 0.6f)), L(0, 0.101f, -0.01f), facing); // slide
            Shapes.Make("Illuminator", Shapes.Rod(0.016f, 0.012f), UiFactory.Mat(new Color(1f, 0.97f, 0.85f)), environment, L(0, 0.03f, -0.01f));
            Shapes.Make("Turret", Shapes.Rod(0.024f, 0.014f), Shapes.Steel, environment, L(0, 0.155f, -0.005f), facing * Quaternion.Euler(180, 0, 0));
            Color[] bands = { new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.45f, 1f) };
            for (int i = 0; i < 3; i++)
            {
                float a = (i - 1) * 0.9f;
                Vector3 op = L(Mathf.Sin(a) * 0.014f, 0.142f, -0.005f - Mathf.Cos(a) * 0.012f);
                Shapes.Make("Objective", Shapes.Rod(0.0055f, 0.026f, 14), Shapes.Steel, environment, op, facing * Quaternion.Euler(180, 0, 0));
                Shapes.Make("ObjectiveBand", Shapes.Rod(0.0058f, 0.003f, 14), UiFactory.LitMat(bands[i]), environment, op + Vector3.down * 0.012f);
            }
            Part(Shapes.RoundedBox(new Vector3(0.06f, 0.045f, 0.07f), 0.016f), white, L(0, 0.235f, 0.0f), facing);           // head
            foreach (int s in new[] { -1, 1 })
            {
                Shapes.Make("Eyepiece", Shapes.Rod(0.009f, 0.055f, 16), black, environment, L(s * 0.016f, 0.25f, 0.015f), facing * Quaternion.Euler(-40, 0, 0));
                Shapes.Make("FocusKnob", Shapes.Rod(0.019f, 0.012f, 18), black, environment, L(s * 0.025f, 0.075f, 0.06f), facing * Quaternion.Euler(0, 0, s * -90f));
                Shapes.Make("FineKnob", Shapes.Rod(0.011f, 0.01f, 14), Shapes.Steel, environment, L(s * 0.037f, 0.075f, 0.06f), facing * Quaternion.Euler(0, 0, s * -90f));
            }
        }

        GameObject Part(Mesh mesh, Material mat, Vector3 pos, Quaternion r) => Shapes.Make("Part", mesh, mat, environment, pos, r);

        /// White plastic rack holding real round-bottomed glass test tubes with coloured samples.
        void TubeRack(Vector3 p, int colorOffset)
        {
            var white = Shapes.GlossWhite;
            Shapes.ContactShadow(environment, p, 0.34f, 0.12f, rot, 0.35f);
            Part(Shapes.RoundedBox(new Vector3(0.27f, 0.012f, 0.07f), 0.005f), white, p + Vector3.up * 0.006f, rot);
            Part(Shapes.RoundedBox(new Vector3(0.27f, 0.01f, 0.07f), 0.004f), white, p + Vector3.up * 0.07f, rot);
            foreach (int s in new[] { -1, 1 })
                Part(Shapes.RoundedBox(new Vector3(0.008f, 0.07f, 0.07f), 0.003f), white, p + right * (s * 0.131f) + Vector3.up * 0.04f, rot);
            for (int i = 0; i < 6; i++)
            {
                Vector3 tp = p + right * (-0.105f + i * 0.042f) + Vector3.up * 0.014f;
                Shapes.Make("Hole", Shapes.Rod(0.011f, 0.002f, 16), UiFactory.Mat(new Color(0.55f, 0.58f, 0.6f)), environment, tp + Vector3.up * 0.0615f);
                Shapes.NoShadow(Shapes.Make("TestTube", Shapes.TestTube(0.009f, 0.115f), Shapes.ClearGlass, environment, tp));
                float fill = 0.045f + (i % 3) * 0.012f;
                Shapes.Make("Sample", Shapes.TubeLiquid(0.009f, fill), Liquid(Liquids[(i + colorOffset) % Liquids.Length]), environment, tp);
                if (i % 2 == 0)
                    Shapes.Make("Stopper", Shapes.Rod(0.0105f, 0.014f, 14), UiFactory.LitMat(Liquids[(i + colorOffset + 2) % Liquids.Length] * 0.8f), environment, tp + Vector3.up * 0.11f);
            }
        }

        void Beaker(Vector3 p, Color liquid)
        {
            Shapes.ContactShadow(environment, p, 0.12f, 0.12f, rot, 0.3f);
            Shapes.Make("BeakerLiquid", Shapes.BeakerLiquid(), Liquid(liquid), environment, p);
            Shapes.NoShadow(Shapes.Make("Beaker", Shapes.Beaker(), Shapes.ClearGlass, environment, p));
            for (int i = 1; i <= 4; i++)   // graduation marks on the side facing the player
            {
                var mark = Box("Graduation", p + Vector3.up * (0.018f * i) - fwd * 0.0405f, new Vector3(i % 2 == 0 ? 0.018f : 0.01f, 0.0015f, 0.001f), rot, Color.white, lit: false);
                mark.transform.position -= fwd * 0.0005f;
            }
        }

        /// Erlenmeyer (conical) flask with coloured liquid.
        void Flask(Vector3 p, Color liquid)
        {
            Shapes.ContactShadow(environment, p, 0.11f, 0.11f, rot, 0.3f);
            Shapes.Make("FlaskLiquid", Shapes.ErlenmeyerLiquid(), Liquid(liquid), environment, p, null, 0.85f);
            Shapes.NoShadow(Shapes.Make("Flask", Shapes.Erlenmeyer(), Shapes.ClearGlass, environment, p, null, 0.85f));
        }

        /// Round-bottom flask sitting in a cork ring.
        void RoundFlask(Vector3 p, Color liquid)
        {
            Shapes.Make("CorkRing", Shapes.Rod(0.03f, 0.014f), UiFactory.LitMat(new Color(0.62f, 0.45f, 0.3f)), environment, p);
            Shapes.Make("RoundFlaskLiquid", Shapes.RoundFlaskLiquid(), Liquid(liquid), environment, p + Vector3.up * 0.006f);
            Shapes.NoShadow(Shapes.Make("RoundFlask", Shapes.RoundFlask(), Shapes.ClearGlass, environment, p + Vector3.up * 0.006f));
        }

        /// Lab workstation: thin-bezel monitor on a stand, keyboard with keys, and a mouse.
        void Monitor(Vector3 p, Quaternion facing)
        {
            System.Func<float, float, float, Vector3> L = (x, y, z) => p + facing * new Vector3(x, y, z);
            var black = Shapes.BlackPlastic;
            Shapes.ContactShadow(environment, L(0, 0, -0.03f), 0.26f, 0.2f, facing, 0.4f);
            Shapes.ContactShadow(environment, L(0, 0, 0.12f), 0.4f, 0.16f, facing, 0.3f);
            Part(Shapes.RoundedBox(new Vector3(0.18f, 0.01f, 0.13f), 0.005f), Shapes.DarkSteel, L(0, 0.005f, -0.03f), facing);   // foot
            Part(Shapes.RoundedBox(new Vector3(0.045f, 0.17f, 0.016f), 0.006f), Shapes.DarkSteel, L(0, 0.09f, -0.06f), facing * Quaternion.Euler(-8, 0, 0)); // neck
            Part(Shapes.RoundedBox(new Vector3(0.46f, 0.28f, 0.022f), 0.008f), black, L(0, 0.25f, -0.045f), facing);            // housing
            Part(Shapes.RoundedBox(new Vector3(0.44f, 0.255f, 0.002f), 0.0005f), Shapes.ScreenMat, L(0, 0.255f, -0.0335f), facing); // live dashboard
            // Keyboard + keys
            Part(Shapes.RoundedBox(new Vector3(0.32f, 0.012f, 0.11f), 0.005f), Shapes.Surface(new Color(0.85f, 0.86f, 0.88f), 0.4f, 0.3f), L(0, 0.006f, 0.12f), facing * Quaternion.Euler(-3, 0, 0));
            var keyMat = Shapes.Surface(new Color(0.2f, 0.21f, 0.23f), 0.35f, 0f);
            var keyMesh = Shapes.RoundedBox(new Vector3(0.019f, 0.006f, 0.018f), 0.003f);
            for (int row = 0; row < 4; row++)
                for (int k = 0; k < 13; k++)
                    Part(keyMesh, keyMat, L(-0.144f + k * 0.024f + (row % 2) * 0.005f, 0.014f + row * 0.0015f, 0.155f - row * 0.023f), facing);
            Part(Shapes.RoundedBox(new Vector3(0.12f, 0.006f, 0.018f), 0.003f), keyMat, L(0, 0.0125f, 0.178f), facing);        // space bar
            Part(Shapes.RoundedBox(new Vector3(0.036f, 0.018f, 0.062f), 0.012f), Shapes.GlossWhite, L(-0.22f, 0.009f, 0.12f), facing); // mouse (right-hand side)
        }

        /// Articulated desk lamp: weighted base, pole, arm, metal shade with a glowing bulb.
        void Lamp(Vector3 p, Color glow)
        {
            var metal = Shapes.DarkSteel;
            Shapes.Make("LampBase", Shapes.Rod(0.05f, 0.016f), metal, environment, p);
            Shapes.Make("LampPole", Shapes.Rod(0.006f, 0.3f, 12), Shapes.Steel, environment, p + Vector3.up * 0.016f);
            Vector3 top = p + Vector3.up * 0.316f, head = p + Vector3.up * 0.4f - fwd * 0.14f;
            Shapes.Make("LampJoint", Shapes.Rod(0.011f, 0.022f, 12), metal, environment, top - right * 0.011f, Quaternion.FromToRotation(Vector3.up, right));
            Shapes.Make("LampArm", Shapes.Rod(0.005f, Vector3.Distance(top, head), 12), Shapes.Steel, environment, top, Quaternion.FromToRotation(Vector3.up, (head - top).normalized));
            var tilt = Quaternion.AngleAxis(-20f, right);   // shade leans towards the bench front
            Shapes.Make("LampShade", Shapes.Shade(0.016f, 0.055f, 0.07f), metal, environment, head + tilt * (Vector3.down * 0.07f), tilt);
            Shapes.Make("LampBulb", Shapes.Rod(0.051f, 0.004f, 20), UiFactory.Mat(glow), environment, head + tilt * (Vector3.down * 0.068f), tilt);
            AddLight(head + Vector3.down * 0.12f, glow, 1.6f, 1.2f);
        }

        /// Potted plant with curved leaves (size from the requested height).
        void Plant(Vector3 basePos, float height, int seed)
        {
            Color[] pots = { new Color(0.78f, 0.45f, 0.32f), new Color(0.93f, 0.94f, 0.95f), new Color(0.2f, 0.22f, 0.24f) };
            Shapes.PottedPlant(environment, basePos, height / 0.2f * 0.55f, seed, pots[seed % pots.Length]);
        }

        Light AddLight(Vector3 pos, Color color, float range, float intensity)
        {
            var go = new GameObject("LabLight");
            go.transform.SetParent(environment, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            return l;
        }

        /// Rounded-edge box; steel colours get a brushed-metal finish, epoxy a glossy one.
        GameObject Box(string name, Vector3 pos, Vector3 size, Quaternion rot, Color color, bool lit = true)
        {
            GameObject go;
            if (!lit) go = Shapes.RoundCube(name, environment, Vector3.zero, size, color, -1f, lit: false);
            else if (color == Steel || color == SteelDark || color == Epoxy || color == WallGrey)
            {
                go = Shapes.RoundCube(name, environment, Vector3.zero, size, color, -1f, true);
                go.GetComponent<Renderer>().sharedMaterial = color == Steel ? Shapes.Steel : color == SteelDark ? Shapes.DarkSteel : color == Epoxy ? Shapes.EpoxyTop : Shapes.WallPaint;
            }
            else go = Shapes.RoundCube(name, environment, Vector3.zero, size, color, -1f, true, 0.35f, 0f);
            go.transform.position = pos;
            go.transform.rotation = rot;
            return go;
        }

        /// White lab tiles with soft grey grout, made in code (64 px, repeats).
        static Texture2D TileTexture()
        {
            if (tileTex) return tileTex;
            const int n = 64;
            tileTex = new Texture2D(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool grout = x < 2 || y < 2;
                    float speck = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.04f;
                    tileTex.SetPixel(x, y, grout ? new Color(0.72f, 0.76f, 0.75f) : new Color(0.95f - speck, 0.97f - speck, 0.96f - speck));
                }
            tileTex.wrapMode = TextureWrapMode.Repeat;
            tileTex.filterMode = FilterMode.Bilinear;
            tileTex.Apply();
            return tileTex;
        }

        static void Outline(Transform parent, float w, float d, Color c, bool horizontal)
        {
            const float th = 0.004f;
            Vector3 a = new Vector3(w, th, th);
            Vector3 b = horizontal ? new Vector3(th, th, d) : new Vector3(th, d, th);
            Vector3 offA = horizontal ? new Vector3(0, 0, d / 2) : new Vector3(0, d / 2, 0);
            Vector3 offB = new Vector3(w / 2, 0, 0);
            UiFactory.Primitive(PrimitiveType.Cube, "e1", parent, offA, a, c);
            UiFactory.Primitive(PrimitiveType.Cube, "e2", parent, -offA, a, c);
            UiFactory.Primitive(PrimitiveType.Cube, "e3", parent, offB, b, c);
            UiFactory.Primitive(PrimitiveType.Cube, "e4", parent, -offB, b, c);
        }

        public void Clear()
        {
            if (entrance || savedSun >= 0f) { entrance = null; Dim(false); }
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        }
    }
}
