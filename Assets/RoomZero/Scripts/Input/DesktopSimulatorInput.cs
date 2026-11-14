// Play the whole loop in the Unity Editor with no headset.
// With the Meta XR Simulator active, cameraControl is off (the simulator owns the head) but
// the keyboard shortcuts below still work in the Game view, alongside the controller pointer.
//   Right-mouse drag: look     WASD / Q E: move     Left click: poke
//   V: say "ZERO, give me a mission"   B: "formula hunt"   H: hint   P: pause/resume
//   1 / 2: silent / loud route   N: next mission / next room   J: Dr. Asha help on/off   T: simulate hand-tracking loss
//   During a mission: 0-9 / Enter / Backspace type on the keypad, F faces the next target.
// Works with either input backend (legacy Input Manager or Input System package) on every
// Unity 6 version, without a compile-time dependency on the Input System package.
// Owner: #3 Interaction + VR UX.

using System;
using System.Reflection;
using UnityEngine;

namespace RoomZero.Input
{
    public class DesktopSimulatorInput : MonoBehaviour
    {
        public float moveSpeed = 1.2f;
        public float lookSpeed = 0.15f;
        public bool showHelp = true;
        [Tooltip("Mouse-look, WASD and click-to-poke. Off when an XR runtime (e.g. Meta XR Simulator) drives the head.")]
        public bool cameraControl = true;

        float yaw, pitch;
        bool trackingLost;
        Camera cam;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (!cam) cam = Camera.main;
            var e = transform.eulerAngles;
            yaw = e.y; pitch = e.x > 180 ? e.x - 360 : e.x;
        }

        void Update()
        {
            if (cameraControl) LookMoveAndPoke();

            // ---- simulated voice + shortcuts (always on) ----
            if (SimKeys.Down(SimKey.V)) VoiceIntentRouter.Submit("ZERO, give me a mission", InputSource.Desktop);
            if (SimKeys.Down(SimKey.B)) VoiceIntentRouter.Submit("ZERO, start the formula hunt", InputSource.Desktop);
            if (SimKeys.Down(SimKey.H)) VoiceIntentRouter.Submit("ZERO, I need a hint", InputSource.Desktop);
            if (SimKeys.Down(SimKey.P)) EventBus.Publish(new PauseRequested { paused = !PokeTarget.InputBlocked, reason = "desktop" });
            if (SimKeys.Down(SimKey.Alpha1)) VoiceIntentRouter.Submit("go silent", InputSource.Desktop);
            if (SimKeys.Down(SimKey.Alpha2)) VoiceIntentRouter.Submit("go loud", InputSource.Desktop);
            if (SimKeys.Down(SimKey.N)) VoiceIntentRouter.Submit("next mission", InputSource.Desktop);
            // Keyboard into the current keypad / safe dial (only while one is the next objective).
            var rt = RoomZero.Game.GameRuntime.Instance;
            if (rt && rt.CurrentTarget is RoomZero.Game.IKeyboardTarget pad)
            {
                bool typed = false;
                for (int d = 0; d <= 9; d++)
                    if (SimKeys.Down((SimKey)((int)SimKey.Alpha0 + d))) { pad.TypeKey(d.ToString()); typed = true; }
                if (SimKeys.Down(SimKey.Enter)) { pad.TypeKey("OK"); typed = true; }
                if (SimKeys.Down(SimKey.Backspace)) { pad.TypeKey("C"); typed = true; }
                if (typed && RoomZero.Game.ScientistNpc.Instance) RoomZero.Game.ScientistNpc.Instance.PlayerActed();
            }
            if (cameraControl && SimKeys.Down(SimKey.F)) FaceNextTarget();
            if (SimKeys.Down(SimKey.J) && RoomZero.Game.ScientistNpc.Instance) RoomZero.Game.ScientistNpc.Instance.ToggleHelp();
            if (SimKeys.Down(SimKey.Z)) RoomZero.Zero.AgentTrace.Visible = !RoomZero.Zero.AgentTrace.Visible;
            if (SimKeys.Down(SimKey.G)) { RoomZero.UI.DebugPanel.Toggle(); RoomZero.Spatial.RoomDebugView.ToggleOutlines(); }
            if (SimKeys.Down(SimKey.Slash)) showHelp = !showHelp;

            if (SimKeys.Down(SimKey.T))
            {
                trackingLost = !trackingLost;
                EventBus.Publish(new HandTrackingChanged { tracked = !trackingLost });
            }
        }

        PokeTarget hovered;
        public static DesktopSimulatorInput Instance { get; private set; }
        void Awake() { Instance = this; }

        /// Desktop only: step back and up from the seat so the whole play area (desk, side
        /// tables, shelf) and every label fit on screen. Gameplay positions don't change.
        public void FrameRoom(Vector3 seat, Vector3 forward)
        {
            if (!cameraControl) return;
            forward.y = 0;
            forward = forward.sqrMagnitude < 0.001f ? Vector3.forward : forward.normalized;
            transform.position = seat - forward * 0.62f + Vector3.up * 0.22f;
            yaw = Quaternion.LookRotation(forward).eulerAngles.y;
            pitch = 20f;   // desk and Dr. Asha both in view
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        /// Turn the camera to look at the next objective (F key).
        void FaceNextTarget()
        {
            var runtime = RoomZero.Game.GameRuntime.Instance;
            var t = runtime ? runtime.CurrentTarget : null;
            if (!t) return;
            var dir = (t.transform.position + Vector3.up * 0.1f) - transform.position;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles;
            yaw = rot.y;
            pitch = rot.x > 180 ? rot.x - 360 : rot.x;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        void UpdateHover()
        {
            PokeTarget now = null;
            if (cam && !RoomZero.UI.DesktopOverlay.PointerOverUI(SimKeys.MousePosition) && Physics.Raycast(cam.ScreenPointToRay(SimKeys.MousePosition), out var hit, 10f, ~0, QueryTriggerInteraction.Collide))
                now = hit.collider.GetComponentInParent<PokeTarget>();
            if (now == hovered) return;
            if (hovered) hovered.SetHover(false);
            hovered = now;
            if (hovered) hovered.SetHover(true);
        }

        void LookMoveAndPoke()
        {
            UpdateHover();
            // The mouse is the desktop "scanner": hidden relics react when the cursor sweeps over them.
            if (cam && !RoomZero.UI.DesktopOverlay.PointerOverUI(SimKeys.MousePosition))
                ScanSources.SetRay(0, cam.ScreenPointToRay(SimKeys.MousePosition));
            if (SimKeys.RightMouseHeld)
            {
                Vector2 d = SimKeys.MouseDelta;
                yaw += d.x * lookSpeed;
                pitch = Mathf.Clamp(pitch - d.y * lookSpeed, -80f, 80f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }

            Vector3 move = Vector3.zero;
            if (SimKeys.Held(SimKey.W)) move += Vector3.forward;
            if (SimKeys.Held(SimKey.S)) move += Vector3.back;
            if (SimKeys.Held(SimKey.A)) move += Vector3.left;
            if (SimKeys.Held(SimKey.D)) move += Vector3.right;
            if (SimKeys.Held(SimKey.E)) move += Vector3.up;
            if (SimKeys.Held(SimKey.Q)) move += Vector3.down;
            transform.position += transform.rotation * move * moveSpeed * Time.unscaledDeltaTime;

            if (SimKeys.LeftClick && cam && !RoomZero.UI.DesktopOverlay.PointerOverUI(SimKeys.MousePosition))
            {
                var ray = cam.ScreenPointToRay(SimKeys.MousePosition);
                if (Physics.Raycast(ray, out var hit, 10f, ~0, QueryTriggerInteraction.Collide))
                {
                    var target = hit.collider.GetComponentInParent<PokeTarget>();
                    if (target) target.Press(InputSource.Desktop);
                }
            }
        }

        GUIStyle helpStyle;

        void OnGUI()
        {
            if (helpStyle == null)
            {
                helpStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                helpStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);
            }
            if (!showHelp)
            {
                GUI.Label(new Rect(12, Screen.height - 28, 300, 22), "<b>/</b>  show controls", helpStyle);
                return;
            }
            string text = cameraControl
                ? $"<b>ROOM//ZERO {RoomZeroVersion.Number}</b>   right-drag look · WASD move · click = touch\n" +
                  "<b>V</b> heist  <b>B</b> formula hunt  <b>1</b> silent  <b>2</b> loud  <b>F</b> face next target  <b>0-9 Enter</b> code\n" +
                  "<b>H</b> hint  <b>P</b> pause  <b>N</b> next room  <b>J</b> Dr. Asha demo  <b>Z</b> ZERO's mind  <b>G</b> dev view  <b>/</b> hide"
                : $"<b>ROOM//ZERO {RoomZeroVersion.Number} + XR Simulator</b>   aim controller + trigger = touch\n" +
                  "<b>V</b> mission  <b>B</b> campaign  <b>1/2</b> route  <b>H</b> hint  <b>N</b> next room  <b>J</b> Dr. Asha  <b>/</b> hide";
            var rect = new Rect(10, Screen.height - 78, 520, 68);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 10, rect.y + 6, rect.width - 20, rect.height - 8), text, helpStyle);
            if (trackingLost) GUI.Label(new Rect(rect.x + 10, rect.y - 22, 400, 20), "<b>[hand tracking LOST (simulated)]</b>", helpStyle);
        }
    }

    public enum SimKey { W, A, S, D, Q, E, V, H, P, N, T, F, G, B, J, Z, Slash, Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, Enter, Backspace }

    /// Input shim. Legacy Input Manager when it's enabled; otherwise the Input System package,
    /// reached by reflection so the project compiles whether or not that package is installed
    /// and whatever version Unity resolved (6000.0 ships 1.11+, 6000.6 ships 1.19).
    public static class SimKeys
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        static KeyCode Map(SimKey k)
        {
            if (k == SimKey.Enter) return KeyCode.Return;
            return (KeyCode)Enum.Parse(typeof(KeyCode), k.ToString()); // W, A..., Alpha0-9, Backspace, F
        }
        public static bool Held(SimKey k) => UnityEngine.Input.GetKey(Map(k));
        public static bool Down(SimKey k) => UnityEngine.Input.GetKeyDown(Map(k));
        public static bool LeftClick => UnityEngine.Input.GetMouseButtonDown(0);
        public static bool RightMouseHeld => UnityEngine.Input.GetMouseButton(1);
        // Legacy axes are per-frame deltas; scale to roughly match Input System pixels.
        public static Vector2 MouseDelta => new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y")) * 10f;
        public static Vector2 MousePosition => UnityEngine.Input.mousePosition;
#else
        public static bool Held(SimKey k) => Bool(KeyControl(k), "isPressed");
        public static bool Down(SimKey k) => Bool(KeyControl(k), "wasPressedThisFrame");
        public static bool LeftClick => Bool(Prop(Device("Mouse"), "leftButton"), "wasPressedThisFrame");
        public static bool RightMouseHeld => Bool(Prop(Device("Mouse"), "rightButton"), "isPressed");
        public static Vector2 MouseDelta => ReadVector2(Prop(Device("Mouse"), "delta"));
        public static Vector2 MousePosition => ReadVector2(Prop(Device("Mouse"), "position"));

        const string Ns = "UnityEngine.InputSystem.";
        const string Asm = ", Unity.InputSystem";
        static Type keyType;
        static bool warned;

        static object Device(string name)
        {
            var t = Type.GetType(Ns + name + Asm);
            if (t == null) { Warn(); return null; }
            var current = t.GetProperty("current", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            return current?.GetValue(null);
        }

        static object KeyControl(SimKey k)
        {
            var kb = Device("Keyboard");
            if (kb == null) return null;
            if (keyType == null) keyType = Type.GetType(Ns + "Key" + Asm);
            if (keyType == null) return null;
            string name = k.ToString().StartsWith("Alpha") ? "Digit" + k.ToString().Substring(5) : k.ToString(); // Enter, Backspace, F...
            var indexer = kb.GetType().GetProperty("Item", new[] { keyType });
            return indexer?.GetValue(kb, new[] { Enum.Parse(keyType, name) });
        }

        static object Prop(object o, string name) => o?.GetType().GetProperty(name)?.GetValue(o);

        static bool Bool(object control, string name) => Prop(control, name) is bool b && b;

        static Vector2 ReadVector2(object control)
        {
            var read = control?.GetType().GetMethod("ReadValue", Type.EmptyTypes);
            return read != null && read.Invoke(control, null) is Vector2 v ? v : Vector2.zero;
        }

        static void Warn()
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[RZ] Active Input Handling is 'Input System' but the package isn't installed. " +
                             "Install Input System or set Project Settings > Player > Active Input Handling to 'Both'.");
        }
#endif
    }
}
