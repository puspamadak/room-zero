// Room 1 entrance: a bio-secure glass partition with telescopic sliding doors across the lab.
// The lab behind it is dim until the player's palm is verified; then the doors slide away into the
// side walls, the lights flicker on one by one, and Dr. Asha walks in through the doorway.
// Desktop / mock room only (on the headset the real room is already "inside").
// Owner: #3 / art.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RoomZero.UI;

namespace RoomZero.Spatial
{
    public partial class RoomDebugView
    {
        public const float EntranceZ = 1.4f;   // plan z of the partition (just in front of the back benches)

        Transform entrance;
        readonly List<Transform> panels = new List<Transform>();
        readonly List<float> panelTravel = new List<float>();
        Renderer statusLight;
        readonly List<Light> dimmed = new List<Light>();
        float savedSun = -1f;
        Color savedSky, savedEquator, savedGround;

        public bool EntranceShown => entrance;

        /// Close the lab behind the bio-secure doors and dim it. Only in the drawn (desktop) vault lab.
        public void ShowEntrance()
        {
            if (lastMap == null || !lastMap.source.StartsWith("MOCK") || Theme != LabTheme.VaultLab || !environment) return;
            if (entrance) return;
            entrance = new GameObject("LabEntrance").transform;
            entrance.SetParent(environment, false);
            panels.Clear(); panelTravel.Clear();

            var frame = Shapes.Steel;
            var frosted = Shapes.Glass(new Color(0.82f, 0.9f, 0.95f, 0.62f));
            const float h = 2.55f, half = 1.9f;
            // Four panels: an inner pair (the doors) and an outer pair; all slide out into the side walls.
            float[] centers = { -1.425f, -0.475f, 0.475f, 1.425f };
            float[] travel = { -1.0f, -1.95f, 1.95f, 1.0f };
            for (int i = 0; i < 4; i++)
            {
                var panel = new GameObject(i == 1 || i == 2 ? "Door" : "SidePanel").transform;
                panel.SetParent(entrance, false);
                panel.position = P(centers[i], 0, EntranceZ + (i == 1 || i == 2 ? -0.04f : 0f));
                panel.rotation = rot;
                float w = 0.95f;
                var glass = Shapes.NoShadow(Shapes.RoundCube("Glass", panel, new Vector3(0, h / 2f, 0), new Vector3(w - 0.04f, h - 0.06f, 0.012f), Color.white, 0.004f));
                glass.GetComponent<Renderer>().sharedMaterial = frosted;
                foreach (int s in new[] { -1, 1 })
                {
                    var stile = Shapes.RoundCube("Stile", panel, new Vector3(s * (w / 2f - 0.02f), h / 2f, 0), new Vector3(0.04f, h, 0.04f), Color.white, 0.008f);
                    stile.GetComponent<Renderer>().sharedMaterial = frame;
                }
                foreach (float y in new[] { 0.03f, h - 0.03f })
                {
                    var rail = Shapes.RoundCube("Rail", panel, new Vector3(0, y, 0), new Vector3(w, 0.05f, 0.04f), Color.white, 0.008f);
                    rail.GetComponent<Renderer>().sharedMaterial = frame;
                }
                // Clear vision strip and a hazard band across every panel.
                var band = Shapes.RoundCube("HazardBand", panel, new Vector3(0, 1.05f, -0.008f), new Vector3(w - 0.06f, 0.06f, 0.003f), new Color(1f, 0.78f, 0.15f), 0.001f, lit: false);
                band.name = "HazardBand";
                if (i == 1 || i == 2)
                {
                    var pull = Shapes.RoundCube("Pull", panel, new Vector3(i == 1 ? 0.4f : -0.4f, 1.1f, -0.03f), new Vector3(0.025f, 0.45f, 0.025f), Color.white, 0.01f);
                    pull.GetComponent<Renderer>().sharedMaterial = frame;
                }
                panels.Add(panel);
                panelTravel.Add(travel[i]);
            }
            // Header with the lab name, status light and warning.
            var header = Shapes.RoundCube("Header", entrance, Vector3.zero, new Vector3(half * 2f, 0.12f, 0.08f), Color.white, 0.01f);
            header.GetComponent<Renderer>().sharedMaterial = Shapes.DarkSteel;
            header.transform.position = P(0, h + 0.02f, EntranceZ - 0.02f); header.transform.rotation = rot;
            var sign = UiFactory.BackedText(entrance, "BIO-SECURE LAB  ·  AUTHORISED PERSONNEL ONLY", Vector3.zero, 0.05f, new Color(1f, 0.82f, 0.3f), faceCamera: false);
            sign.transform.position = P(0, 1.95f, EntranceZ - 0.08f); sign.transform.rotation = rot;
            var status = Shapes.RoundCube("StatusLight", entrance, Vector3.zero, new Vector3(0.5f, 0.03f, 0.02f), Palette.Danger, 0.008f, lit: false);
            status.transform.position = P(0, h - 0.06f, EntranceZ - 0.07f); status.transform.rotation = rot;
            statusLight = status.GetComponent<Renderer>();

            Dim(true);
        }

        /// Palm verified: doors slide into the walls, lights come on one by one.
        public void OpenEntrance()
        {
            if (!entrance) return;
            StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            statusLight.sharedMaterial = UiFactory.Mat(Palette.Success);
            RoomZero.Audio.Sfx.Play("scan");
            var start = new List<Vector3>();
            foreach (var p in panels) start.Add(p.position);
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.8f)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                for (int i = 0; i < panels.Count; i++)
                {
                    if (!panels[i]) continue;
                    // Doors move first; the outer panels follow half a beat later (telescopic).
                    float k = i == 1 || i == 2 ? e : Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.25f) / 0.75f));
                    panels[i].position = start[i] + right * (panelTravel[i] * k);
                }
                yield return null;
            }
            yield return StartCoroutine(LightsOn());
            if (entrance) Destroy(entrance.gameObject);   // fully inside the walls now
            entrance = null;
        }

        IEnumerator LightsOn()
        {
            Dim(false, restoreLights: false);
            RoomZero.Audio.Sfx.Play("click", 0.5f);
            foreach (var l in dimmed)
            {
                if (!l) continue;
                l.enabled = true;
                yield return new WaitForSeconds(0.09f);
                l.enabled = false;                                   // a quick fluorescent flicker
                yield return new WaitForSeconds(0.05f);
                l.enabled = true;
            }
            dimmed.Clear();
        }

        void Dim(bool on, bool restoreLights = true)
        {
            var sun = Sun();
            if (on)
            {
                if (savedSun < 0f)
                {
                    savedSun = sun ? sun.intensity : 1f;
                    savedSky = RenderSettings.ambientSkyColor; savedEquator = RenderSettings.ambientEquatorColor; savedGround = RenderSettings.ambientGroundColor;
                }
                if (sun) sun.intensity = savedSun * 0.35f;
                RenderSettings.ambientSkyColor = savedSky * 0.45f; RenderSettings.ambientEquatorColor = savedEquator * 0.45f; RenderSettings.ambientGroundColor = savedGround * 0.45f;
                dimmed.Clear();
                foreach (var l in environment.GetComponentsInChildren<Light>())
                    if (l.type != LightType.Directional && l.enabled) { l.enabled = false; dimmed.Add(l); }
                // A cool spotlight on the scanner so the first thing you see is where to put your hand.
                entranceLight = AddLight(P(0, 1.6f, 0.45f), new Color(0.5f, 0.9f, 1f), 1.6f, 1.4f);
            }
            else
            {
                if (savedSun >= 0f)
                {
                    if (sun) sun.intensity = savedSun;
                    RenderSettings.ambientSkyColor = savedSky; RenderSettings.ambientEquatorColor = savedEquator; RenderSettings.ambientGroundColor = savedGround;
                    savedSun = -1f;
                }
                if (entranceLight) Destroy(entranceLight.gameObject);
                entranceLight = null;
                if (restoreLights) { foreach (var l in dimmed) if (l) l.enabled = true; dimmed.Clear(); }
            }
        }

        Light entranceLight;

        /// Remove the entrance instantly (skip / room change) and restore the lights.
        public void HideEntrance()
        {
            if (entrance) Destroy(entrance.gameObject);
            entrance = null;
            Dim(false);
        }
    }
}
