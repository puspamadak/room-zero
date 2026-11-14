// Composition root: the only place that decides which implementations are used.
// Put ONE of these in the main scene (RoomZero > Build Demo Scene does it for you).
// Owner: #1 Tech Lead + Unity Integrator.

using UnityEngine;
using RoomZero.Game;
using RoomZero.Input;
using RoomZero.Memory;
using RoomZero.Spatial;
using RoomZero.UI;
using RoomZero.Zero;

namespace RoomZero
{
    public static class RoomZeroVersion
    {
        /// Shown in the desktop help box and logged at start, so you can see which package is running.
        public const string Number = "v29";
    }

    [DefaultExecutionOrder(-100)]
    public class RoomZeroBootstrap : MonoBehaviour
    {
        /// True when playing with mouse + keyboard (no XR runtime). Used for on-screen tips.
        public static bool DesktopMode { get; private set; }

        public RoomZeroConfig config;
        [Tooltip("Player head. Leave empty to use Camera.main (works with OVRCameraRig's CenterEyeAnchor).")]
        public Transform head;

        void Start()
        {
            if (!config)
            {
                config = ScriptableObject.CreateInstance<RoomZeroConfig>();
                Debug.LogWarning("[RZ] No RoomZeroConfig assigned; using defaults. Run RoomZero > Build Demo Scene.");
            }
            if (!head && Camera.main) head = Camera.main.transform;
            if (!head) { Debug.LogError("[RZ] No head/camera found."); enabled = false; return; }

            ZeroLog.Verbose = config.verboseLogs;
            EventBus.Clear();
            PokeTarget.InputBlocked = false;
            UiFactory.Init(config);
            EnsureLight();
            if (DesktopMode) DesktopQuality();

            bool xrActive = XrIsActive();
            Debug.Log($"[RZ] ROOM//ZERO {RoomZeroVersion.Number} started");
            ZeroLog.Info("Boot", $"xrActive={xrActive} aiMode={config.aiMode} seated={config.seatedMode} reach={config.Reach}m");

            // ---- input ----
            bool desktopCamera = !xrActive || config.forceDesktopInput;
            DesktopMode = desktopCamera;
            // In the Editor the keyboard shortcuts stay on even with the Meta XR Simulator running.
            if (desktopCamera || Application.isEditor)
            {
                var sim = head.GetComponent<DesktopSimulatorInput>();
                if (!sim) sim = head.gameObject.AddComponent<DesktopSimulatorInput>();
                sim.cameraControl = desktopCamera;
                if (!xrActive)
                {
                    // Desktop framing: seated eye height, wider lens, looking slightly down at the desk.
                    if (head.position.y < 0.5f) head.position = new Vector3(head.position.x, 1.2f, head.position.z);
                    head.rotation = Quaternion.Euler(22f, head.eulerAngles.y, 0f);
                    var cam = head.GetComponent<Camera>();
                    if (cam) { cam.fieldOfView = 75f; cam.nearClipPlane = 0.02f; }
                    ApplyDesktopSky(cam);
                }
            }
#if ROOMZERO_META_XR
            gameObject.AddComponent<RoomZero.Integrations.Meta.MetaHandFingertips>();
            gameObject.AddComponent<RoomZero.Integrations.Meta.MetaControllerPointer>();
#endif

            // ---- room ----
            IRoomProvider provider = new MockRoomProvider();
#if ROOMZERO_META_XR
            if (!config.forceMockRoom && xrActive) provider = new RoomZero.Integrations.Meta.MrukRoomProvider();
#endif
            var scout = new RoomScout(provider, config);

            // ---- runtime / memory / UI ----
            var runtime = new GameObject("GameRuntime").AddComponent<GameRuntime>();
            runtime.Init(config);
            var memory = new MemoryStore();
            UiPanel.ScreenSpace = DesktopMode;   // desktop: crisp 2D overlay instead of 3D text panels
            var panel = UiPanel.Create("MainPanel", head, 0.6f, -0.08f);
            var hud = MissionHud.Create(head,
                onHint: () => EventBus.Publish(new HintRequested { source = InputSource.Ui }),
                onPause: () => EventBus.Publish(new PauseRequested { paused = true, reason = "ui" }));
            hud.Hide();
            if (DesktopMode)
            {
                var overlay = gameObject.AddComponent<DesktopOverlay>();
                overlay.panel = panel;
                overlay.hud = hud;
            }
            // Developer trace: on in the headset if configured; on desktop start hidden (press G).
            var trace = DebugPanel.Create(head);
            trace.gameObject.SetActive(config.showDebugPanel && !DesktopMode);
            var roomDebug = new GameObject("RoomDebugView").AddComponent<RoomDebugView>();
            // Headset / MR: the agents' decisions float beside you (desktop draws them in the overlay).
            if (!DesktopMode && config.agentMindPanel) AgentMindPanel.Create(head);

            // ---- ZERO ----
            var zero = gameObject.AddComponent<ZeroOrchestrator>();
            zero.cfg = config;
            zero.head = head;
            zero.roomScout = scout;
            zero.memory = memory;
            zero.runtime = runtime;
            zero.panel = panel;
            zero.hud = hud;
            zero.roomDebug = roomDebug;

            if (config.aiMode == AiMode.RemoteWithFallback)
            {
                var client = new AgentProxyClient(config.agentProxyUrl);
                zero.remoteDirector = new RemoteDirectorAgent(client);
                zero.remotePuzzle = new RemotePuzzleArchitectAgent(client);
                zero.remoteNpc = new RemoteNpcAgent(client);
                zero.remoteAdaptation = new RemoteAdaptationAgent(client);
                zero.remoteMentor = new RemoteMentorAgent(client);
            }

            zero.Begin();
        }

        /// Shaded objects need light: a soft sun plus gentle ambient fill (only if the scene has none).
        /// Desktop / Simulator: the same crisp look in every project (Built-in pipeline settings;
        /// with URP the pipeline asset decides, see README "Same quality as before").
        static void DesktopQuality()
        {
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 12f);
            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 4);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.realtimeReflectionProbes = true;
            QualitySettings.globalTextureMipmapLimit = 0;
        }

        static void EnsureLight()
        {
            if (SceneQuery.FindAll<Light>().Count > 0) return;
            var go = new GameObject("RZ_Sun");
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.97f, 0.98f, 1f);   // clean lab daylight
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            go.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.5f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.3f, 0.28f);
        }

        /// Soft daylight sky for the desktop simulator (the headset shows your real room instead).
        static void ApplyDesktopSky(Camera cam)
        {
            if (!cam) return;
            var sky = Shader.Find("Skybox/Procedural");
            if (sky)
            {
                var mat = new Material(sky);
                if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", new Color(0.62f, 0.82f, 0.82f));
                if (mat.HasProperty("_GroundColor")) mat.SetColor("_GroundColor", new Color(0.82f, 0.86f, 0.84f));
                if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1.05f);
                if (mat.HasProperty("_AtmosphereThickness")) mat.SetFloat("_AtmosphereThickness", 0.8f);
                RenderSettings.skybox = mat;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.78f, 0.9f, 0.88f); // soft greenhouse daylight
            }
            // Gentle greenhouse haze for depth.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.86f, 0.93f, 0.91f);
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 14f;
        }

        /// True when an XR runtime (Quest, Link, Meta XR Simulator) is rendering.
        /// Uses the XR display subsystem, which exists on every Unity 6 version.
        /// (XRSettings lived in the VR module, which Unity 6.6 removed.)
        static bool XrIsActive()
        {
            try
            {
                var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
                SubsystemManager.GetSubsystems(displays);
                foreach (var d in displays) if (d.running) return true;
            }
            catch { }
            return false;
        }
    }
}
