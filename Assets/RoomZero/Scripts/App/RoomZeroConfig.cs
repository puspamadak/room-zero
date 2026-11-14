// Project-wide tuning. Create via Assets > Create > ROOM//ZERO > Config
// (the scene builder makes one for you at Assets/RoomZero/RoomZeroConfig.asset).
// Never put API keys here: the headset talks to YOUR agent proxy, which holds the keys.

using UnityEngine;

namespace RoomZero
{
    public enum AiMode
    {
        LocalOnly,              // deterministic agents only. Default; demo-safe offline.
        RemoteWithFallback      // call the agent proxy; any timeout/invalid output -> local agent
    }

    [CreateAssetMenu(menuName = "ROOM//ZERO/Config", fileName = "RoomZeroConfig")]
    public class RoomZeroConfig : ScriptableObject
    {
        [Header("AI")]
        public AiMode aiMode = AiMode.LocalOnly;
        [Tooltip("Base URL of your agent proxy, e.g. http://192.168.1.20:8787 (see tools/agent-proxy)")]
        public string agentProxyUrl = "http://127.0.0.1:8787";
        [Tooltip("Per-agent call timeout. Late answers are discarded and the local agent is used.")]
        public int agentTimeoutMs = 4000;

        [Header("Comfort / safety")]
        [Tooltip("Competition 'airplane seat test': every interaction within reach of a seated player.")]
        public bool seatedMode = true;
        [Tooltip("Max horizontal distance from the player to any interactive object (m).")]
        public float seatedReach = 0.75f;
        public float roomScaleReach = 2.0f;
        [Tooltip("Surfaces below this Room Scout confidence are never used.")]
        [Range(0f, 1f)] public float minSurfaceConfidence = 0.6f;
        public float minObjectHeight = 0.4f;
        public float maxObjectHeight = 1.7f;

        [Header("Mission")]
        public int maxObjectives = 4;
        public Vector2Int durationRangeSeconds = new Vector2Int(90, 600);
        [Tooltip("Seconds between adaptation checks while ACTIVE.")]
        public float adaptationInterval = 10f;
        [Tooltip("Minimum seconds between two applied adaptations.")]
        public float adaptationCooldown = 25f;
        public int maxAdaptationsPerMission = 3;

        [Header("Desktop / simulator")]
        [Tooltip("Force mouse+keyboard input even if an XR device is active.")]
        public bool forceDesktopInput = false;
        [Tooltip("Use the mock room even when MRUK is available (handy in the Meta XR Simulator).")]
        public bool forceMockRoom = false;
        public bool showDebugPanel = true;
        [Tooltip("Print every [RZ] info line to the Console. Off = only warnings/errors (trace panel still has everything).")]
        public bool verboseLogs = false;
        public bool showRoomDebug = true;

        [Header("Mixed reality")]
        [Tooltip("MR: open a 'lab window' on one of your real walls that looks into the room's lab; Dr. Asha walks out of it.")]
        public bool labWindow = true;
        [Tooltip("MR / headset: floating ZERO'S MIND panel showing the agents' latest decisions.")]
        public bool agentMindPanel = true;

        [Header("Visuals (assigned by the scene builder so shaders ship in the build)")]
        public Material baseMaterial;
        [Tooltip("Shaded material for solid objects (furniture, vault, keypad, drone). Created by the scene builder.")]
        public Material litMaterial;
        public Font font;

        [Header("3D models (optional): drag a model/prefab here to replace the placeholder shape")]
        public GameObject shardModel;      // heist: data shard
        public GameObject fragmentModel;   // formula hunt: formula fragment (vial)
        public GameObject vaultModel;      // formula hunt: seed vault body
        public GameObject seedPodModel;    // formula hunt: the recovered Harvest Formula
        public GameObject keypadModel;     // heist: keypad housing (buttons stay)
        public GameObject droneModel;      // guard drone

        [Header("Dr. Asha, the scientist (filled in automatically by RoomZero > Build Demo Scene)")]
        [Tooltip("Rigged human model (Microsoft Rocketbox Medical_Female_02, MIT licence). Empty = simple built-in figure.")]
        public GameObject scientistModel;
        public Texture2D scientistBody, scientistHead, scientistHair, scientistExtra;
        public Texture2D scientistBodyNormal, scientistHeadNormal, scientistExtraNormal;
        public AnimationClip scientistIdle, scientistWalk, scientistTalk, scientistWork, scientistListen, scientistCheer, scientistWave;

        [Header("Voices and music (filled in automatically by RoomZero > Build Demo Scene)")]
        public AudioClip[] voiceClips;
        public AudioClip musicVaultLab, musicGreenhouse, musicGeneLab;
        [Range(0f, 1f)] public float musicVolume = 0.22f;

        [Header("Photo-real lab (optional, desktop/Simulator only)")]
        [Tooltip("Drag a lab environment prefab here (e.g. a free lab pack from the Unity Asset Store). It replaces the generated walls, benches and decor; the play tables stay so the mission still fits. Placed with its origin at the seated player, facing forward.")]
        public GameObject labEnvironmentModel;
        [Tooltip("Scale applied to the lab environment prefab (1 = the prefab's own metres).")]
        public float labEnvironmentScale = 1f;

        public float Reach => seatedMode ? seatedReach : roomScaleReach;
    }
}
