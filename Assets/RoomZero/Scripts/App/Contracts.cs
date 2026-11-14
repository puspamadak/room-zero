// ROOM//ZERO shared data contracts (spec section 5).
// Owners: #1 Tech Lead (GameCommand/MissionEvent), #4 Orchestration (MissionPlan),
//         #2 Room (RoomMap), #7 Persistence (PlayerProfile).
// Rule: agree changes to this file as a team BEFORE coding against them.
//
// Agent-facing fields that carry enum values are strings on purpose: agents return
// JSON text, and MissionValidator is the only place strings become enums. Anything
// it cannot parse is rejected, never guessed.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomZero
{
    public static class Schema
    {
        public const int Version = 1;
    }

    // ---------- Enums (the only values the runtime will ever act on) ----------

    /// TREASURE_HUNT, GREENHOUSE and GENE_LAB are the three rooms of the Harvest Formula campaign.
    public enum MissionTemplateId { DESK_VAULT, SERVER_HEIST, TREASURE_HUNT, GREENHOUSE, GENE_LAB }

    public enum ObjectiveType
    {
        FIND_CLUE,      // touch a data shard to reveal the code
        ENTER_CODE,     // poke the code into a keypad
        DISABLE_SENSOR, // touch sensor nodes in the shown order (extra step)
        HIDDEN_RELIC,   // invisible until scanned by hand/mouse; collecting it gives a number + the next clue
        SAFE_DIAL,      // dial in the relic numbers, in order, to open the treasure safe
        NUTRIENT_MIX,   // greenhouse: add nutrient drops to match the feed recipe exactly, then mix
        GROW_LIGHT,     // greenhouse: tune light / water / heat into the green band to grow the crop
        MICROSCOPE_FOCUS, // gene lab: focus each slide, then tag it HEALTHY or DISEASED
        DNA_SPLICE      // gene lab: fill each gap in the gene with the matching base (A-T, G-C)
    }

    public enum NpcIntent { IDLE, INTRO, SUSPICIOUS, ALERT, CALM, PRAISE, TAUNT, HINT }

    public enum AdaptationAction
    {
        NONE,
        OFFER_HINT,
        EXTEND_TIMER,
        LOWER_GUARD_ALERTNESS,
        RAISE_GUARD_ALERTNESS
    }

    public enum RouteChoice { NONE, SILENT_ROUTE, LOUD_ROUTE }

    public enum MissionStateId
    {
        BOOT,
        ROOM_READY,
        MISSION_REQUESTED,
        PLANNING,
        VALIDATING,
        BRIEFING,
        ACTIVE,
        ADAPTING,
        COMPLETE,
        SAVING,
        READY_FOR_NEXT_SESSION,
        ERROR
    }

    public enum GameCommandType { SPAWN_OBJECTIVE, PLAY_NPC_BEAT, APPLY_ADAPTATION, END_MISSION }

    public enum MissionOutcome { NONE, SUCCESS, FAILED_TIMEOUT, FAILED_CAUGHT, ABANDONED }

    // ---------- RoomMap (Room Scout output) ----------

    [Serializable]
    public class RoomMap
    {
        public int schemaVersion = Schema.Version;
        public string roomId = "local-room-01";
        public string source = "MOCK";          // MRUK | MOCK | SEATED_FALLBACK
        public float[] playerOrigin = { 0f, 0f, 0f };
        public List<SurfaceInfo> surfaces = new List<SurfaceInfo>();
        public List<ZoneInfo> zones = new List<ZoneInfo>();
        public List<string> excludedZoneIds = new List<string>();

        public SurfaceInfo FindSurface(string id) => surfaces.Find(s => s.id == id);
        public ZoneInfo FindZone(string id) => zones.Find(z => z.id == id);
        public Vector3 PlayerOrigin => V3.From(playerOrigin);
    }

    [Serializable]
    public class SurfaceInfo
    {
        public string id;
        public string type = "table";            // table | wall | floor | couch | storage | virtual_desk ...
        public float[] center = { 0f, 0f, 0f };  // world metres, top of the surface
        public float[] size = { 0f, 0f, 0f };    // metres (x, thickness, z)
        public float[] normal = { 0f, 1f, 0f };  // up for horizontal surfaces, outwards for walls
        public float yawDegrees;                 // rotation of the surface's x/z axes around world up
        public float confidence = 1f;            // 0..1
        public bool allowed = true;              // Room Scout can veto a surface

        public Vector3 Center => V3.From(center);
        public Vector3 Size => V3.From(size);
        public Vector3 Normal => V3.From(normal);
        public bool IsHorizontal => Normal.y > 0.7f;
    }

    [Serializable]
    public class ZoneInfo
    {
        public string id;
        public string type = "playable";          // playable | excluded
        public float[] center = { 0f, 0f, 0f };
        public float[] size = { 1f, 1f, 1f };      // axis-aligned box

        public Bounds Bounds => new Bounds(V3.From(center), V3.From(size));
    }

    // ---------- MissionPlan (Director + Puzzle Architect proposal) ----------

    [Serializable]
    public class MissionPlan
    {
        public int schemaVersion = Schema.Version;
        public string missionId;
        public string templateId;                 // MissionTemplateId as text
        public int difficulty = 1;                // 1..3
        public int durationSeconds = 180;
        public string briefing;                   // short, shown on the briefing panel
        public List<ObjectiveSpec> objectives = new List<ObjectiveSpec>();
        public List<string> npcBeatIds = new List<string>();
        public string successCondition = "ALL_OBJECTIVES_COMPLETE";
    }

    [Serializable]
    public class ObjectiveSpec
    {
        public string id;
        public string type;                       // ObjectiveType as text
        public string anchorId;                   // must be a SurfaceInfo.id in the RoomMap
        public string targetId;                   // optional link, e.g. ENTER_CODE -> clue objective id
        public int steps = 4;                     // code length / sensor count
    }

    // ---------- Agent I/O ----------

    [Serializable]
    public class NpcLine
    {
        public string beatId;
        public string intent;                     // NpcIntent as text
        public string text;                       // validated: short, plain
    }

    [Serializable]
    public class AdaptationProposal
    {
        public string action = "NONE";            // AdaptationAction as text
        public int amount;                        // e.g. seconds for EXTEND_TIMER
        public string reason;
    }

    /// Mentor agent (Dr. Asha): what the player is doing on the current challenge.
    [Serializable]
    public class MentorInput
    {
        public string objectiveType;
        public float secondsStuck;      // since the player last made progress or touched anything
        public int failedAttempts;      // on this challenge
        public int hintsGiven;          // on this challenge
        public bool justSolvedOne;      // the player solved the previous challenge themselves
        public int objectivesDone, objectivesTotal;
        public float secondsLeft;
        public List<string> allowedActions = new List<string> { "WAIT", "ENCOURAGE", "HINT", "SHOW_ONE_STEP" };
    }

    /// The mentor's decision. Only these four actions exist; Dr. Asha's words for each are fixed
    /// (and voiced), so the model chooses WHAT to do and WHEN, never free text.
    [Serializable]
    public class MentorDecision
    {
        public string action = "WAIT";
        public string reason = "";
    }

    /// Signals the Adaptation agent may look at. Game telemetry only.
    [Serializable]
    public class MissionSignals
    {
        public float elapsedSeconds;
        public float durationSeconds;
        public int failedAttempts;
        public int hintsUsed;
        public int objectivesComplete;
        public int objectivesTotal;
        public float guardAlertness;
        public int difficulty;
    }

    // ---------- Runtime commands & events ----------

    /// The ONLY way anything changes in the room. Built from a validated plan.
    [Serializable]
    public class GameCommand
    {
        public GameCommandType type;
        public string missionId;
        public string objectiveId;
        public string beatId;
        public AdaptationAction adaptation;
        public int amount;
    }

    // ---------- Persistence ----------

    [Serializable]
    public class PlayerProfile
    {
        public int schemaVersion = Schema.Version;
        public string profileId = "local-player";
        public ProfileStats stats = new ProfileStats();
        public ProfilePreferences preferences = new ProfilePreferences();
        public SessionSummary lastSession;        // JsonUtility never yields null: use HasLastSession
        public List<SessionSummary> history = new List<SessionSummary>();

        public bool HasLastSession => lastSession != null && !string.IsNullOrEmpty(lastSession.sessionId);
    }

    [Serializable]
    public class ProfileStats
    {
        public int missions;
        public int completions;
        public float completionRate;
        public float medianSeconds;
        public float hintRate;
        public int recentDifficulty = 1;
    }

    [Serializable]
    public class ProfilePreferences
    {
        public string puzzleStyle = "observation"; // observation | sequence
    }

    [Serializable]
    public class SessionSummary
    {
        public string sessionId;
        public string missionId;
        public string templateId;
        public string choice = "NONE";            // RouteChoice as text
        public string outcome = "NONE";           // MissionOutcome as text
        public bool completed;
        public int retries;
        public int hintsUsed;
        public float seconds;
        public int difficulty;
        public List<string> adaptations = new List<string>();
        public string summary;
        public string timestampUtc;
    }

    // ---------- Helpers ----------

    public static class V3
    {
        public static Vector3 From(float[] a) =>
            a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

        public static float[] To(Vector3 v) => new[] { v.x, v.y, v.z };
    }

    public static class EnumParse
    {
        public static bool TryParse<T>(string s, out T value) where T : struct
        {
            value = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (int.TryParse(s, out _)) return false; // numbers are not valid enum names here
            return Enum.TryParse(s.Trim(), false, out value) && Enum.IsDefined(typeof(T), value);
        }
    }
}
