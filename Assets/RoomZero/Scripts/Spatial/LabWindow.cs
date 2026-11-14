// MR "lab window": a glowing frame on one of your REAL walls that looks into the current room's
// lab (seed vault lab / greenhouse / gene lab), with correct perspective as you move your head.
// Dr. Asha walks out of it into your room.
//
// How it works: the themed lab is built far away (out of view of the main camera); a second camera
// sits at the same place relative to that virtual window as your head is relative to the real one,
// with an off-axis projection through the window rectangle, rendering into the window's texture.
// It works with any render pipeline. Mono image (same for both eyes); head-motion parallax gives depth.
// Owner: #3 / art + #2 spatial.

using UnityEngine;
using RoomZero.UI;

namespace RoomZero.Spatial
{
    public partial class RoomDebugView
    {
        static readonly Vector3 PortalOrigin = new Vector3(5000f, 0f, 5000f);
        public const float PortalWindowZ = 0.6f, PortalWindowY = 1.35f;

        /// Build the themed lab far away, for the window to look into. Returns its root.
        public Transform BuildPortalWorld(LabTheme theme, Transform parent)
        {
            var savedEnv = environment; var savedO = o; var savedRot = rot; var savedF = fwd; var savedR = right; var savedTheme = Theme;
            var root = new GameObject("PortalWorld_" + theme).transform;
            root.SetParent(parent, false);
            environment = root;
            o = PortalOrigin; rot = Quaternion.identity; fwd = Vector3.forward; right = Vector3.right;
            Theme = theme;
            try
            {
                var floor = Box("Floor", P(0, -0.005f, 0.6f), new Vector3(4f, 0.01f, 3.4f), rot, Color.white);
                floor.GetComponent<Renderer>().sharedMaterial = FloorMaterial(theme);
                DrawDecor();
            }
            finally
            {
                environment = savedEnv; o = savedO; rot = savedRot; fwd = savedF; right = savedR; Theme = savedTheme;
            }
            return root;
        }

        public static Vector3 PortalWindowCenter => PortalOrigin + new Vector3(0, PortalWindowY, PortalWindowZ);
    }

    public class LabWindow : MonoBehaviour
    {
        public static LabWindow Current { get; private set; }

        /// Floor point just in front of the window, where Dr. Asha steps into the room.
        public Vector3 EntryPoint { get; private set; }
        public Vector3 Normal { get; private set; }   // points into the room

        const float W = 1.3f, H = 0.9f;
        Camera cam;
        RenderTexture rt;
        Transform frameRoot;
        float openT;

        /// Put a window on the real wall in front of the player (or float one if no wall was scanned).
        public static LabWindow Create(RoomDebugView view, RoomMap map, LabTheme theme, Transform parent)
        {
            Vector3 origin = map.PlayerOrigin;
            SurfaceInfo best = null; float bestScore = float.MaxValue;
            Vector3 headFwd = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized : Vector3.forward;
            foreach (var s in map.surfaces)
            {
                if (s.type != "wall" || s.Size.x < W + 0.2f) continue;
                Vector3 to = s.Center - origin; to.y = 0;
                float d = to.magnitude;
                if (d < 0.8f || d > 4.5f) continue;
                float facing = Vector3.Dot(to.normalized, headFwd);          // prefer the wall you look at
                float score = d - facing * 2f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            Vector3 center, n;
            if (best != null)
            {
                n = best.Normal; n.y = 0; n.Normalize();
                if (Vector3.Dot(n, origin - best.Center) < 0) n = -n;          // must face the player
                Vector3 c = best.Center;
                // Stay inside the wall, centred towards the player, at a comfortable height.
                Vector3 along = Vector3.Cross(Vector3.up, n);
                float slide = Mathf.Clamp(Vector3.Dot(origin - c, along), -(best.Size.x - W) / 2f, (best.Size.x - W) / 2f);
                center = new Vector3(c.x, 0, c.z) + along * slide + Vector3.up * RoomDebugView.PortalWindowY + n * 0.01f;
            }
            else
            {
                n = -headFwd;
                center = new Vector3(origin.x, 0, origin.z) + headFwd * 2.2f + Vector3.up * RoomDebugView.PortalWindowY;
            }

            var go = new GameObject("LabWindow");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);   // +z looks into the wall
            var w = go.AddComponent<LabWindow>();
            w.Normal = n;
            w.EntryPoint = new Vector3(center.x, 0, center.z) + n * 0.35f;
            w.Build(view, theme);
            Current = w;
            return w;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (rt) { rt.Release(); Destroy(rt); }
        }

        void Build(RoomDebugView view, LabTheme theme)
        {
            view.BuildPortalWorld(theme, transform);

            rt = new RenderTexture(1024, Mathf.RoundToInt(1024 * H / W), 16) { name = "LabWindowRT", antiAliasing = 2 };
            var camGo = new GameObject("LabWindowCamera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.clearFlags = theme == LabTheme.Greenhouse ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
            cam.depth = -10;
            cam.stereoTargetEye = StereoTargetEyeMask.None;   // renders only into the window, never to the headset

            frameRoot = new GameObject("Frame").transform;
            frameRoot.SetParent(transform, false);
            var view3d = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(view3d.GetComponent<Collider>());
            view3d.name = "WindowView";
            view3d.transform.SetParent(frameRoot, false);
            view3d.transform.localPosition = Vector3.zero;
            view3d.transform.localScale = new Vector3(W, H, 1f);
            var m = new Material(UiFactory.Mat(Color.white)) { mainTexture = rt };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", rt);
            view3d.GetComponent<Renderer>().sharedMaterial = m;

            // Brushed-steel frame with a soft glowing inner edge.
            var bars = new[]
            {
                new Vector3(0, H / 2f + 0.03f, -0.01f), new Vector3(W + 0.12f, 0.06f, 0.05f),
                new Vector3(0, -H / 2f - 0.03f, -0.01f), new Vector3(W + 0.12f, 0.06f, 0.05f),
                new Vector3(-W / 2f - 0.03f, 0, -0.01f), new Vector3(0.06f, H, 0.05f),
                new Vector3(W / 2f + 0.03f, 0, -0.01f), new Vector3(0.06f, H, 0.05f),
            };
            for (int i = 0; i < bars.Length; i += 2)
            {
                var bar = Shapes.RoundCube("FrameBar", frameRoot, bars[i], bars[i + 1], Color.white, 0.012f);
                bar.GetComponent<Renderer>().sharedMaterial = Shapes.Steel;
            }
            Color glow = theme == LabTheme.Greenhouse ? Palette.Success : theme == LabTheme.GeneLab ? new Color(0.3f, 0.85f, 1f) : Palette.Holo;
            var glows = new[]
            {
                new Vector3(0, H / 2f + 0.002f, -0.04f), new Vector3(W, 0.008f, 0.004f),
                new Vector3(0, -H / 2f - 0.002f, -0.04f), new Vector3(W, 0.008f, 0.004f),
                new Vector3(-W / 2f - 0.002f, 0, -0.04f), new Vector3(0.008f, H, 0.004f),
                new Vector3(W / 2f + 0.002f, 0, -0.04f), new Vector3(0.008f, H, 0.004f),
            };
            for (int i = 0; i < glows.Length; i += 2)
                Shapes.RoundCube("Glow", frameRoot, glows[i], glows[i + 1], glow, 0.002f, lit: false);
            string label = theme == LabTheme.Greenhouse ? "ROOM 2 · HYDROPONICS GREENHOUSE" : theme == LabTheme.GeneLab ? "ROOM 3 · GENE LAB" : "ROOM 1 · SEED VAULT LAB";
            var t = UiFactory.BackedText(frameRoot, label, new Vector3(0, H / 2f + 0.12f, -0.03f), 0.035f, glow, faceCamera: false);
            frameRoot.localScale = new Vector3(1f, 0.02f, 1f);          // opens with a quick vertical wipe
            RoomZero.Audio.Sfx.Play("scan", 0.6f);
        }

        void LateUpdate()
        {
            if (openT < 1f)
            {
                openT = Mathf.Min(1f, openT + Time.deltaTime / 0.8f);
                frameRoot.localScale = new Vector3(1f, Mathf.SmoothStep(0.02f, 1f, openT), 1f);
            }
            var head = Camera.main;
            if (!head || !cam) return;

            // Your eye in the real window's frame (x right, y up, z into the wall; you are at z < 0)...
            Vector3 rel = transform.InverseTransformPoint(head.transform.position);
            float d = Mathf.Max(0.1f, -rel.z);
            // ...placed at the same spot in front of the virtual window.
            cam.transform.position = RoomDebugView.PortalWindowCenter + new Vector3(rel.x, rel.y, -d);
            cam.transform.rotation = Quaternion.identity;   // virtual window faces -z, so look along +z
            // Off-axis projection through the window rectangle.
            var e = FrustumEdges(rel, cam.nearClipPlane);
            cam.projectionMatrix = Matrix4x4.Frustum(e[0], e[1], e[2], e[3], cam.nearClipPlane, cam.farClipPlane);
        }

        /// Left, right, bottom, top of the near plane for an eye at `rel` (window frame, z &lt; 0 = in front).
        public static float[] FrustumEdges(Vector3 rel, float near)
        {
            float d = Mathf.Max(0.1f, -rel.z), k = near / d;
            return new[] { (-W / 2f - rel.x) * k, (W / 2f - rel.x) * k, (-H / 2f - rel.y) * k, (H / 2f - rel.y) * k };
        }
    }
}
