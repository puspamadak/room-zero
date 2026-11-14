// The three rooms of the Harvest Formula campaign, each with its own look and lighting:
//   VaultLab   (room 1) modern stainless lab with the seed vault        (RoomDebugView.cs)
//   Greenhouse (room 2) glass hydroponics greenhouse, daylight, plants  (this file)
//   GeneLab    (room 3) dark clean-room, blue LEDs, DNA screens         (this file)
// The play tables stay identical in every room so the validated layout always fits.
// On the headset the real room stays; DrawThemeAccents adds a few holograms instead.
// Owner: #3 / art.

using UnityEngine;
using RoomZero.Game;
using RoomZero.UI;

namespace RoomZero.Spatial
{
    public enum LabTheme { VaultLab, Greenhouse, GeneLab }

    public partial class RoomDebugView
    {
        // ------------------------------------------------------------------ atmosphere

        static bool captured;
        static Color defSky, defEquator, defGround, defFog, defSun;
        static float defSunIntensity;

        static Light Sun()
        {
            foreach (var l in SceneQuery.FindAll<Light>()) if (l.type == LightType.Directional) return l;
            return null;
        }

        static void ApplyAtmosphere(LabTheme theme)
        {
            var sun = Sun();
            if (!captured)
            {
                captured = true;
                defSky = RenderSettings.ambientSkyColor; defEquator = RenderSettings.ambientEquatorColor; defGround = RenderSettings.ambientGroundColor;
                defFog = RenderSettings.fogColor;
                defSun = sun ? sun.color : Color.white; defSunIntensity = sun ? sun.intensity : 1f;
            }
            switch (theme)
            {
                case LabTheme.Greenhouse:
                    RenderSettings.ambientSkyColor = new Color(0.62f, 0.75f, 0.68f);
                    RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.48f);
                    RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.22f);
                    RenderSettings.fogColor = new Color(0.82f, 0.9f, 0.86f);
                    if (sun) { sun.color = new Color(1f, 0.95f, 0.85f); sun.intensity = 1.25f; }
                    break;
                case LabTheme.GeneLab:
                    RenderSettings.ambientSkyColor = new Color(0.16f, 0.24f, 0.38f);
                    RenderSettings.ambientEquatorColor = new Color(0.1f, 0.15f, 0.24f);
                    RenderSettings.ambientGroundColor = new Color(0.05f, 0.07f, 0.1f);
                    RenderSettings.fogColor = new Color(0.04f, 0.07f, 0.12f);
                    if (sun) { sun.color = new Color(0.7f, 0.82f, 1f); sun.intensity = 0.35f; }
                    break;
                default:
                    RenderSettings.ambientSkyColor = defSky; RenderSettings.ambientEquatorColor = defEquator; RenderSettings.ambientGroundColor = defGround;
                    RenderSettings.fogColor = defFog;
                    if (sun) { sun.color = defSun; sun.intensity = defSunIntensity; }
                    break;
            }
        }

        Material FloorMaterial(LabTheme theme)
        {
            switch (theme)
            {
                case LabTheme.Greenhouse: return UiFactory.LitTextured(new Color(0.78f, 0.62f, 0.48f), TileTexture(), new Vector2(6, 7));     // terracotta tiles
                case LabTheme.GeneLab: return Shapes.Textured("genefloor", new Color(0.16f, 0.2f, 0.27f), TileTexture(), 0.85f, 0.1f, new Vector2(8, 10)); // glossy dark
                default: return UiFactory.LitTextured(new Color(0.8f, 0.82f, 0.84f), TileTexture(), new Vector2(8, 10));
            }
        }

        // ------------------------------------------------------------------ ROOM 2: GREENHOUSE

        static readonly Color FrameGreen = new Color(0.2f, 0.32f, 0.26f);
        static readonly Color KneeWall = new Color(0.86f, 0.84f, 0.8f);

        void DrawGreenhouse()
        {
            var glass = Shapes.Glass(new Color(0.85f, 0.95f, 0.92f, 0.12f));
            var frame = Shapes.Surface(FrameGreen, 0.5f, 0.4f);

            // Knee walls + glass walls with green steel frames (back and both sides).
            GlassWall(P(0, 0, 1.9f), 3.8f, rot, glass, frame, false);
            GlassWall(P(-1.9f, 0, 0.4f), 3.0f, rot * Quaternion.Euler(0, -90, 0), glass, frame, true);
            GlassWall(P(1.9f, 0, 0.4f), 3.0f, rot * Quaternion.Euler(0, 90, 0), glass, frame, false);

            // Pitched glass roof with rafters.
            foreach (int side in new[] { -1, 1 })
            {
                var roofRot = rot * Quaternion.Euler(0, 0, side * -17f);
                var pane = Shapes.NoShadow(Shapes.RoundCube("RoofGlass", environment, Vector3.zero, new Vector3(2.0f, 0.01f, 3.2f), Color.white, 0.003f));
                pane.GetComponent<Renderer>().sharedMaterial = glass;
                pane.transform.position = P(side * 0.95f, 2.72f, 0.4f); pane.transform.rotation = roofRot;
                for (int i = 0; i < 6; i++)
                {
                    var rafter = Shapes.RoundCube("Rafter", environment, Vector3.zero, new Vector3(2.0f, 0.05f, 0.04f), Color.white, 0.01f);
                    rafter.GetComponent<Renderer>().sharedMaterial = frame;
                    rafter.transform.position = P(side * 0.95f, 2.70f, -1.1f + i * 0.6f); rafter.transform.rotation = roofRot;
                }
            }
            Box("Ridge", P(0, 3.0f, 0.4f), new Vector3(0.06f, 0.06f, 3.2f), rot, FrameGreen);

            // Outside: lawn, trees and hills seen through the glass.
            var lawn = Box("Lawn", P(0, -0.03f, 0.4f), new Vector3(60f, 0.02f, 60f), rot, new Color(0.36f, 0.58f, 0.3f));
            lawn.GetComponent<Renderer>().sharedMaterial = Shapes.Surface(new Color(0.36f, 0.58f, 0.3f), 0.1f, 0f);
            var rng = new System.Random(7);
            for (int i = 0; i < 18; i++)
            {
                float a = i * 20f * Mathf.Deg2Rad + (float)rng.NextDouble() * 0.3f, d = 6f + (float)rng.NextDouble() * 8f;
                Vector3 tp = P(Mathf.Sin(a) * d, 0, 0.4f + Mathf.Cos(a) * d);
                if (Vector3.Dot(tp - o, fwd) < 1.5f && Mathf.Abs(Vector3.Dot(tp - o, right)) < 2.5f) continue;   // keep the player's back clear
                Tree(tp, 0.8f + (float)rng.NextDouble() * 0.8f, rng.Next(3));
            }
            for (int i = 0; i < 4; i++)
            {
                var hill = UiFactory.Primitive(PrimitiveType.Sphere, "Hill", environment, Vector3.zero, new Vector3(18f, 4f, 10f), new Color(0.42f, 0.6f, 0.38f), lit: true);
                hill.transform.position = P(-24f + i * 16f, -1.4f, 26f + (i % 2) * 6f);
            }

            // Back wall: tiered plant shelves on both sides, hydroponic NFT rig in the middle.
            PlantShelf(P(-1.18f, 0, 1.68f), 1.25f, 0);
            PlantShelf(P(1.18f, 0, 1.68f), 1.25f, 1);
            HydroponicRig(P(0, 0, 1.68f));

            // Right side: potting bench with soil sacks and a watering can.
            PottingBench(P(1.55f, 0, 0.2f));

            // Big plants in the corners; hanging grow lights over the shelves.
            Shapes.PottedPlant(environment, P(-1.62f, 0, 1.22f), 3.2f, 51, new Color(0.62f, 0.4f, 0.28f));
            Shapes.PottedPlant(environment, P(1.62f, 0, 1.25f), 2.8f, 52, new Color(0.62f, 0.4f, 0.28f));
            foreach (float x in new[] { -1.18f, 0f, 1.18f })
            {
                Box("GrowLightBar", P(x, 2.05f, 1.6f), new Vector3(1.0f, 0.03f, 0.12f), rot, SteelDark);
                Box("GrowLEDs", P(x, 2.033f, 1.6f), new Vector3(0.95f, 0.006f, 0.09f), rot, new Color(0.95f, 0.55f, 1f), lit: false);
                foreach (int s in new[] { -1, 1 })
                    Shapes.Make("Wire", Shapes.Rod(0.002f, 0.65f, 6), Shapes.DarkSteel, environment, P(x + s * 0.45f, 2.06f, 1.6f));
                AddLight(P(x, 1.85f, 1.55f), new Color(0.95f, 0.65f, 1f), 1.6f, 1.2f);
            }
            AddLight(P(0, 2.3f, 0.6f), new Color(1f, 0.97f, 0.9f), 4f, 0.8f);
            WateringCan(P(0.22f, 0.31f, 1.6f));

            Shapes.ContactShadow(environment, P(-1.18f, 0, 1.68f), 1.45f, 0.55f, rot, 0.45f);
            Shapes.ContactShadow(environment, P(1.18f, 0, 1.68f), 1.45f, 0.55f, rot, 0.45f);
            Shapes.ContactShadow(environment, P(0, 0, 1.68f), 1.0f, 0.5f, rot, 0.45f);
            AddReflections(P(0, 1.3f, 0.6f), new Vector3(4f, 3f, 3.2f));
        }

        void GlassWall(Vector3 baseCenter, float width, Quaternion r, Material glass, Material frame, bool withDoor)
        {
            // Door gap on the left wall at plan z 0.5 (wall-local x = z - 0.4 because the wall is centred at z 0.4).
            float gapC = 0.1f, gapW = withDoor ? 0.95f : 0f;
            float L = -width / 2f, R = width / 2f;
            var spans = withDoor ? new[] { new Vector2(L, gapC - gapW / 2f), new Vector2(gapC + gapW / 2f, R) } : new[] { new Vector2(L, R) };
            foreach (var sp in spans)
            {
                float w = sp.y - sp.x, c = (sp.x + sp.y) / 2f;
                var knee = Shapes.RoundCube("KneeWall", environment, Vector3.zero, new Vector3(w, 0.5f, 0.12f), Color.white, 0.01f);
                knee.GetComponent<Renderer>().sharedMaterial = Shapes.Surface(KneeWall, 0.2f, 0f);
                knee.transform.position = baseCenter + r * new Vector3(c, 0.25f, 0); knee.transform.rotation = r;
                var pane = Shapes.NoShadow(Shapes.RoundCube("WallGlass", environment, Vector3.zero, new Vector3(w, 1.95f, 0.01f), Color.white, 0.003f));
                pane.GetComponent<Renderer>().sharedMaterial = glass;
                pane.transform.position = baseCenter + r * new Vector3(c, 1.475f, 0); pane.transform.rotation = r;
                foreach (float y in new[] { 0.52f, 1.45f })
                {
                    var rail = Shapes.RoundCube("Rail", environment, Vector3.zero, new Vector3(w, 0.04f, 0.06f), Color.white, 0.008f);
                    rail.GetComponent<Renderer>().sharedMaterial = frame;
                    rail.transform.position = baseCenter + r * new Vector3(c, y, 0); rail.transform.rotation = r;
                }
            }
            var top = Shapes.RoundCube("TopRail", environment, Vector3.zero, new Vector3(width, 0.04f, 0.06f), Color.white, 0.008f);
            top.GetComponent<Renderer>().sharedMaterial = frame;
            top.transform.position = baseCenter + Vector3.up * 2.44f; top.transform.rotation = r;
            if (withDoor)
            {
                var transom = Shapes.NoShadow(Shapes.RoundCube("TransomGlass", environment, Vector3.zero, new Vector3(gapW, 0.3f, 0.01f), Color.white, 0.003f));
                transom.GetComponent<Renderer>().sharedMaterial = glass;
                transom.transform.position = baseCenter + r * new Vector3(gapC, 2.28f, 0); transom.transform.rotation = r;
            }
            int bays = Mathf.RoundToInt(width / 0.6f);
            for (int i = 0; i <= bays; i++)
            {
                float x = -width / 2f + i * width / bays;
                if (withDoor && Mathf.Abs(x - gapC) < gapW / 2f) continue;
                var m = Shapes.RoundCube("Mullion", environment, Vector3.zero, new Vector3(0.04f, 1.95f, 0.05f), Color.white, 0.008f);
                m.GetComponent<Renderer>().sharedMaterial = frame;
                m.transform.position = baseCenter + r * new Vector3(x, 1.475f, 0); m.transform.rotation = r;
            }
            if (withDoor)
                foreach (int sgn in new[] { -1, 1 })
                {
                    var post = Shapes.RoundCube("DoorPost", environment, Vector3.zero, new Vector3(0.06f, 2.12f, 0.08f), Color.white, 0.01f);
                    post.GetComponent<Renderer>().sharedMaterial = frame;
                    post.transform.position = baseCenter + r * new Vector3(gapC + sgn * gapW / 2f, 1.06f, 0); post.transform.rotation = r;
                }
        }

        void Tree(Vector3 p, float scale, int kind)
        {
            Shapes.Make("Trunk", Shapes.Rod(0.12f * scale, 1.6f * scale, 10), UiFactory.LitMat(new Color(0.4f, 0.28f, 0.18f)), environment, p);
            Color leaf = kind == 0 ? new Color(0.25f, 0.5f, 0.25f) : kind == 1 ? new Color(0.32f, 0.58f, 0.28f) : new Color(0.2f, 0.42f, 0.24f);
            for (int i = 0; i < 3; i++)
            {
                var crown = UiFactory.Primitive(PrimitiveType.Sphere, "Crown", environment, Vector3.zero, Vector3.one * (1.4f - i * 0.25f) * scale, leaf, lit: true);
                crown.transform.position = p + Vector3.up * (1.7f + i * 0.55f) * scale + right * ((i - 1) * 0.25f * scale);
            }
        }

        void PlantShelf(Vector3 floorPos, float w, int seed)
        {
            var steel = Shapes.Surface(FrameGreen, 0.45f, 0.4f);
            foreach (int s in new[] { -1, 1 })
                foreach (float dz in new[] { -0.15f, 0.15f })
                {
                    var post = Shapes.RoundCube("ShelfPost", environment, Vector3.zero, new Vector3(0.03f, 1.6f, 0.03f), Color.white, 0.006f);
                    post.GetComponent<Renderer>().sharedMaterial = steel;
                    post.transform.position = floorPos + right * (s * w / 2f) + fwd * dz + Vector3.up * 0.8f; post.transform.rotation = rot;
                }
            Color[] pots = { new Color(0.78f, 0.45f, 0.32f), new Color(0.93f, 0.94f, 0.95f), new Color(0.3f, 0.42f, 0.3f) };
            for (int tier = 0; tier < 3; tier++)
            {
                float y = 0.35f + tier * 0.5f;
                var plank = Box("ShelfBoard", floorPos + Vector3.up * y, new Vector3(w, 0.03f, 0.34f), rot, new Color(0.62f, 0.46f, 0.3f));
                plank.GetComponent<Renderer>().sharedMaterial = Shapes.Surface(new Color(0.62f, 0.46f, 0.3f), 0.25f, 0f);
                int count = 5 - (tier == 2 ? 1 : 0);
                for (int i = 0; i < count; i++)
                {
                    Vector3 pp = floorPos + Vector3.up * (y + 0.015f) + right * (-w / 2f + 0.14f + i * (w - 0.28f) / (count - 1)) + fwd * ((i % 2) * 0.05f - 0.02f);
                    var plant = Shapes.PottedPlant(environment, pp, 0.55f + ((i + tier) % 3) * 0.12f, seed * 37 + tier * 7 + i, pots[(i + tier + seed) % 3]);
                    if ((i + tier + seed) % 3 == 0)   // some are fruiting: ripe tomatoes / bean pods
                        for (int f = 0; f < 3; f++)
                        {
                            var fr = UiFactory.Primitive(PrimitiveType.Sphere, "Tomato", plant, Vector3.zero, Vector3.one * 0.022f, new Color(0.9f, 0.2f, 0.12f), lit: true);
                            fr.transform.position = pp + Vector3.up * 0.1f + Quaternion.Euler(0, f * 120f + i * 30f, 0) * Vector3.forward * 0.05f;
                        }
                }
            }
        }

        /// NFT hydroponics: three white channels on an A-frame with lettuce growing out of them, a reservoir below.
        void HydroponicRig(Vector3 floorPos)
        {
            var pvc = Shapes.Surface(new Color(0.95f, 0.96f, 0.97f), 0.6f, 0f);
            Box("Reservoir", floorPos + Vector3.up * 0.16f, new Vector3(0.8f, 0.3f, 0.32f), rot, new Color(0.2f, 0.45f, 0.65f));
            var water = Box("ReservoirLabel", floorPos + Vector3.up * 0.2f - fwd * 0.162f, new Vector3(0.3f, 0.08f, 0.002f), rot, Color.white);
            water.GetComponent<Renderer>().sharedMaterial = Shapes.LabelMat;
            foreach (int s in new[] { -1, 1 })
            {
                var leg = Shapes.RoundCube("AFrame", environment, Vector3.zero, new Vector3(0.03f, 1.5f, 0.03f), Color.white, 0.006f);
                leg.GetComponent<Renderer>().sharedMaterial = Shapes.Steel;
                leg.transform.position = floorPos + right * (s * 0.42f) + Vector3.up * 0.75f + fwd * 0.05f;
                leg.transform.rotation = rot * Quaternion.Euler(-8, 0, 0);
            }
            for (int row = 0; row < 3; row++)
            {
                float y = 0.62f + row * 0.36f;
                Vector3 c = floorPos + Vector3.up * y + fwd * (0.1f - row * 0.06f);
                Shapes.Make("Channel", Shapes.Rod(0.04f, 0.86f, 16), pvc, environment, c - right * 0.43f, Quaternion.FromToRotation(Vector3.up, right));
                for (int k = 0; k < 5; k++)
                {
                    Vector3 hole = c - right * 0.32f + right * (k * 0.16f) + Vector3.up * 0.04f;
                    Shapes.Make("NetCup", Shapes.Rod(0.022f, 0.012f, 12), Shapes.BlackPlastic, environment, hole - Vector3.up * 0.01f);
                    Shapes.Foliage(environment, hole, 0.5f + ((k + row) % 2) * 0.15f, row * 10 + k);
                }
            }
            Shapes.Make("ReturnPipe", Shapes.Rod(0.012f, 1.2f, 10), Shapes.BlackPlastic, environment, floorPos + right * 0.45f + Vector3.up * 0.3f);
        }

        void PottingBench(Vector3 floorPos)
        {
            var wood = Shapes.Surface(new Color(0.6f, 0.43f, 0.27f), 0.25f, 0f);
            var top = Shapes.RoundCube("PottingTop", environment, Vector3.zero, new Vector3(0.5f, 0.04f, 0.9f), Color.white, 0.01f);
            top.GetComponent<Renderer>().sharedMaterial = wood;
            top.transform.position = floorPos + Vector3.up * 0.85f; top.transform.rotation = rot;
            foreach (var c in new[] { new Vector2(-0.2f, -0.4f), new Vector2(0.2f, -0.4f), new Vector2(-0.2f, 0.4f), new Vector2(0.2f, 0.4f) })
            {
                var leg = Shapes.RoundCube("PottingLeg", environment, Vector3.zero, new Vector3(0.05f, 0.83f, 0.05f), Color.white, 0.008f);
                leg.GetComponent<Renderer>().sharedMaterial = wood;
                leg.transform.position = floorPos + right * c.x + fwd * c.y + Vector3.up * 0.415f; leg.transform.rotation = rot;
            }
            for (int i = 0; i < 3; i++)   // soil sacks under the bench
            {
                var sack = Shapes.RoundCube("SoilSack", environment, Vector3.zero, new Vector3(0.3f, 0.16f, 0.24f), Color.white, 0.06f);
                sack.GetComponent<Renderer>().sharedMaterial = Shapes.Surface(i == 1 ? new Color(0.35f, 0.5f, 0.3f) : new Color(0.55f, 0.45f, 0.3f), 0.2f, 0f);
                sack.transform.position = floorPos + Vector3.up * (0.08f + (i == 2 ? 0.16f : 0f)) + fwd * (i == 1 ? 0.25f : -0.15f);
                sack.transform.rotation = rot * Quaternion.Euler(0, 10f * i, 0);
            }
            for (int i = 0; i < 4; i++)
                Shapes.PottedPlant(environment, floorPos + Vector3.up * 0.87f + fwd * (-0.3f + i * 0.2f), 0.4f, 70 + i, new Color(0.78f, 0.45f, 0.32f));
            Shapes.ContactShadow(environment, floorPos, 0.65f, 1.05f, rot, 0.45f);
        }

        void WateringCan(Vector3 p)
        {
            var green = Shapes.Surface(new Color(0.25f, 0.55f, 0.35f), 0.6f, 0.2f);
            Shapes.Make("CanBody", Shapes.Lathe("can", new[] { new Vector2(0, 0), new Vector2(0.07f, 0), new Vector2(0.075f, 0.01f), new Vector2(0.07f, 0.13f), new Vector2(0.04f, 0.15f), new Vector2(0, 0.15f) }), green, environment, p);
            Shapes.Make("Spout", Shapes.Rod(0.008f, 0.2f, 10), green, environment, p + Vector3.up * 0.05f - right * 0.06f, Quaternion.FromToRotation(Vector3.up, (Vector3.up - right * 1.4f).normalized));
            Shapes.Make("Rose", Shapes.Rod(0.018f, 0.01f, 12), green, environment, p + Vector3.up * 0.16f - right * 0.22f, Quaternion.FromToRotation(Vector3.up, (Vector3.up - right * 1.4f).normalized));
        }

        // ------------------------------------------------------------------ ROOM 3: GENE LAB

        static readonly Color Navy = new Color(0.1f, 0.13f, 0.2f);
        static readonly Color LedCyan = new Color(0.3f, 0.85f, 1f);

        void DrawGeneLab()
        {
            var panel = Shapes.Surface(Navy, 0.55f, 0.3f);
            DarkWall(P(0, 1.3f, 1.9f), 3.8f, rot, panel);
            DarkWall(P(-1.9f, 1.3f, 0.4f), 3.0f, rot * Quaternion.Euler(0, -90, 0), panel);
            DarkWall(P(1.9f, 1.3f, 0.4f), 3.0f, rot * Quaternion.Euler(0, 90, 0), panel);
            var ceil = Box("Ceiling", P(0, 2.62f, 0.4f), new Vector3(3.8f, 0.04f, 3.2f), rot, Navy);
            ceil.GetComponent<Renderer>().sharedMaterial = Shapes.Surface(new Color(0.08f, 0.1f, 0.14f), 0.3f, 0f);
            foreach (float x in new[] { -0.9f, 0f, 0.9f })
                Box("CeilingLED", P(x, 2.595f, 0.6f), new Vector3(0.06f, 0.01f, 2.6f), rot, LedCyan, lit: false);

            // Sliding clean-room door where Dr. Asha enters.
            Box("AirlockFrame", P(-1.875f, 1.06f, 0.5f), new Vector3(0.04f, 2.12f, 1.0f), rot, SteelDark);
            Box("AirlockDoor", P(-1.86f, 1.03f, 0.5f), new Vector3(0.03f, 2.0f, 0.9f), rot, new Color(0.75f, 0.8f, 0.86f));
            Box("AirlockLight", P(-1.84f, 2.15f, 0.5f), new Vector3(0.02f, 0.03f, 0.4f), rot, Palette.Success, lit: false);

            // Giant DNA wall screen (back wall, above the benches).
            var screen = Box("WallScreen", P(0, 1.75f, 1.865f), new Vector3(1.9f, 0.95f, 0.02f), rot, Color.white, lit: false);
            screen.GetComponent<Renderer>().sharedMaterial = Shapes.ScreenMatFront;
            Box("ScreenBezel", P(0, 1.75f, 1.877f), new Vector3(1.96f, 1.01f, 0.02f), rot, new Color(0.03f, 0.04f, 0.06f));

            // Benches with sequencers, a monitor and a microscope.
            BenchUnit(P(-1.18f, 0, 1.68f), 1.25f);
            BenchUnit(P(1.18f, 0, 1.68f), 1.25f);
            Box("BenchMiddle", P(0, 0.45f, 1.68f), new Vector3(1.0f, 0.9f, 0.36f), rot, Steel);
            Box("BenchMiddleTop", P(0, 0.885f, 1.68f), new Vector3(1.02f, 0.03f, 0.38f), rot, Epoxy);
            Sequencer(P(-1.4f, 0.9f, 1.68f), 0);
            Sequencer(P(-0.85f, 0.9f, 1.68f), 1);
            Sequencer(P(0.25f, 0.9f, 1.68f), 2);
            Monitor(P(-0.22f, 0.9f, 1.72f), Quaternion.LookRotation(-fwd, Vector3.up));
            Microscope(P(1.0f, 0.9f, 1.66f), rot);
            TubeRack(P(1.42f, 0.9f, 1.6f), 2);
            PetriStack(P(0.65f, 0.9f, 1.58f));

            // Right side: -80 freezer and a blinking server rack; left corner: rotating DNA hologram.
            Freezer(P(1.6f, 0, 0.05f));
            ServerRack(P(1.6f, 0, 0.7f));
            DnaHologram(P(-1.55f, 0, 1.25f));

            AddLight(P(0, 2.2f, 0.5f), new Color(0.6f, 0.8f, 1f), 3.5f, 0.9f);
            AddLight(P(0, 1.6f, 1.5f), LedCyan, 2.2f, 0.8f);
            AddLight(P(-1.5f, 1.2f, 1.2f), new Color(0.4f, 1f, 0.8f), 1.5f, 1.0f);
            Shapes.ContactShadow(environment, P(-1.18f, 0, 1.68f), 1.45f, 0.55f, rot, 0.5f);
            Shapes.ContactShadow(environment, P(1.18f, 0, 1.68f), 1.45f, 0.55f, rot, 0.5f);
            AddReflections(P(0, 1.3f, 0.6f), new Vector3(4f, 2.6f, 3.2f));
        }

        void DarkWall(Vector3 center, float width, Quaternion r, Material panel)
        {
            var wall = Box("Wall", center, new Vector3(width, 2.6f, 0.04f), r, Navy);
            wall.GetComponent<Renderer>().sharedMaterial = panel;
            int panels = Mathf.RoundToInt(width / 0.6f);
            for (int i = 1; i < panels; i++)
                Box("LEDSeam", center + r * new Vector3(-width / 2f + i * width / panels, 0, -0.022f), new Vector3(0.008f, 2.6f, 0.004f), r, new Color(0.18f, 0.5f, 0.6f), lit: false);
            foreach (float y in new[] { -1.0f, 0.95f })
                Box("LEDStrip", center + r * new Vector3(0, y, -0.024f), new Vector3(width, 0.015f, 0.004f), r, LedCyan, lit: false);
        }

        void Sequencer(Vector3 p, int variant)
        {
            Vector3 size = variant == 1 ? new Vector3(0.34f, 0.3f, 0.3f) : new Vector3(0.4f, 0.22f, 0.32f);
            var body = Shapes.RoundCube("Sequencer", environment, Vector3.zero, size, Color.white, 0.03f);
            body.GetComponent<Renderer>().sharedMaterial = variant == 1 ? Shapes.GlossWhite : Shapes.Surface(new Color(0.22f, 0.24f, 0.28f), 0.6f, 0.2f);
            body.transform.position = p + Vector3.up * (size.y / 2f); body.transform.rotation = rot;
            Box("SeqStatus", p + Vector3.up * (size.y * 0.75f) - fwd * (size.z / 2f + 0.002f), new Vector3(size.x * 0.8f, 0.012f, 0.003f), rot, variant == 2 ? Palette.Accent : LedCyan, lit: false);
            Box("SeqScreen", p + Vector3.up * (size.y * 0.45f) - fwd * (size.z / 2f + 0.002f) + right * (size.x * 0.2f), new Vector3(0.1f, 0.06f, 0.003f), rot, new Color(0.05f, 0.25f, 0.3f), lit: false);
            Box("SeqDrawer", p + Vector3.up * (size.y * 0.45f) - fwd * (size.z / 2f + 0.003f) - right * (size.x * 0.18f), new Vector3(0.14f, 0.04f, 0.004f), rot, SteelDark);
            Shapes.ContactShadow(environment, p, size.x * 1.25f, size.z * 1.25f, rot, 0.4f);
        }

        void Freezer(Vector3 floorPos)
        {
            var body = Shapes.RoundCube("Freezer", environment, Vector3.zero, new Vector3(0.6f, 1.9f, 0.65f), Color.white, 0.03f);
            body.GetComponent<Renderer>().sharedMaterial = Shapes.GlossWhite;
            body.transform.position = floorPos + Vector3.up * 0.95f; body.transform.rotation = rot;
            Vector3 front = floorPos - right * 0.302f;
            Box("FreezerDisplay", front + Vector3.up * 1.6f, new Vector3(0.004f, 0.08f, 0.2f), rot, new Color(0.05f, 0.1f, 0.12f), lit: false);
            var t = UiFactory.Text(environment, "-80°C", Vector3.zero, 0.05f, LedCyan);
            t.transform.position = front + Vector3.up * 1.6f - right * 0.004f;
            t.transform.rotation = Quaternion.LookRotation(right, Vector3.up);
            BarHandle(front + Vector3.up * 1.0f - right * 0.02f + fwd * 0.25f, Quaternion.LookRotation(right) * Quaternion.Euler(0, 0, 90), 0.4f);
            Shapes.ContactShadow(environment, floorPos, 0.75f, 0.8f, rot, 0.5f);
        }

        void ServerRack(Vector3 floorPos)
        {
            var body = Shapes.RoundCube("ServerRack", environment, Vector3.zero, new Vector3(0.55f, 1.8f, 0.55f), Color.white, 0.015f);
            body.GetComponent<Renderer>().sharedMaterial = Shapes.Surface(new Color(0.08f, 0.09f, 0.11f), 0.5f, 0.3f);
            body.transform.position = floorPos + Vector3.up * 0.9f; body.transform.rotation = rot;
            var rng = new System.Random(3);
            for (int u = 0; u < 12; u++)
            {
                Vector3 row = floorPos - right * 0.278f + Vector3.up * (0.2f + u * 0.13f);
                Box("ServerUnit", row, new Vector3(0.006f, 0.1f, 0.48f), rot, new Color(0.15f, 0.16f, 0.19f));
                for (int k = 0; k < 4; k++)
                {
                    Color c = rng.Next(4) == 0 ? Palette.Accent : rng.Next(3) == 0 ? Palette.Success : LedCyan;
                    Box("ServerLED", row - right * 0.004f + fwd * (-0.18f + k * 0.025f), new Vector3(0.003f, 0.01f, 0.01f), rot, c, lit: false);
                }
            }
            Shapes.ContactShadow(environment, floorPos, 0.7f, 0.7f, rot, 0.5f);
        }

        void DnaHologram(Vector3 floorPos)
        {
            Shapes.Make("HoloPedestal", Shapes.Rod(0.22f, 0.12f, 32), Shapes.DarkSteel, environment, floorPos);
            Shapes.Make("HoloEmitter", Shapes.Rod(0.18f, 0.01f, 32), UiFactory.Mat(LedCyan), environment, floorPos + Vector3.up * 0.12f);
            var helix = new GameObject("DnaHelix").transform;
            helix.SetParent(environment, false);
            helix.position = floorPos + Vector3.up * 0.16f;
            var a = UiFactory.Mat(LedCyan); var b = UiFactory.Mat(new Color(1f, 0.45f, 0.75f));
            for (int i = 0; i < 26; i++)
            {
                float ang = i * 26f * Mathf.Deg2Rad, y = i * 0.05f;
                Vector3 pa = new Vector3(Mathf.Cos(ang) * 0.12f, y, Mathf.Sin(ang) * 0.12f), pb = -new Vector3(pa.x, 0, pa.z) + Vector3.up * y;
                foreach (var pr in new[] { new { p = pa, m = a }, new { p = pb, m = b } })
                {
                    var ball = UiFactory.Primitive(PrimitiveType.Sphere, "Node", helix, pr.p, Vector3.one * 0.03f, Color.white);
                    ball.GetComponent<Renderer>().sharedMaterial = pr.m;
                }
                if (i % 2 == 0)
                {
                    var rung = Shapes.Make("Rung", Shapes.Rod(0.004f, 0.24f, 8), UiFactory.Mat(new Color(0.6f, 0.8f, 0.9f)), helix, helix.TransformPoint(pa), Quaternion.FromToRotation(Vector3.up, (pb - pa).normalized));
                    rung.transform.localPosition = pa;
                }
            }
            helix.gameObject.AddComponent<Spinner>().degreesPerSecond = 25f;
            AddLight(floorPos + Vector3.up * 0.8f, LedCyan, 1.2f, 1.2f);
            Shapes.ContactShadow(environment, floorPos, 0.6f, 0.6f, rot, 0.4f);
        }

        // ------------------------------------------------------------------ headset: themed holograms over the real room

        void DrawThemeAccents(RoomMap map)
        {
            if (Theme == LabTheme.VaultLab) return;
            Vector3 origin = map.PlayerOrigin;
            var desk = map.FindSurface("surface-desk");
            var r = Quaternion.Euler(0, desk != null ? desk.yawDegrees : 0f, 0);
            Vector3 f = r * Vector3.forward, rt = r * Vector3.right;
            string name = Theme == LabTheme.Greenhouse ? "ROOM 2 · HYDROPONICS GREENHOUSE" : "ROOM 3 · GENE LAB";
            var banner = UiFactory.BackedText(environment, name, Vector3.zero, 0.03f, Theme == LabTheme.Greenhouse ? Palette.Success : LedCyan);
            banner.transform.position = origin + f * 1.3f + Vector3.up * 0.75f;
            if (Theme == LabTheme.GeneLab) DnaHologram(origin + f * 1.1f - rt * 0.9f - Vector3.up * (origin.y - 0.4f));
            else
                for (int i = 0; i < 3; i++)
                    Shapes.Foliage(environment, origin + f * (0.9f + i * 0.1f) + rt * (-0.8f + i * 0.8f) + Vector3.up * 0.1f, 1.2f, 90 + i);
        }
    }
}
