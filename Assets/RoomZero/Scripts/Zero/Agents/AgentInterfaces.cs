// The six brain centers as narrow, typed interfaces (spec section 4).
// Agents return PROPOSALS (structured data). They never touch the Unity scene.
// Owner: #5 Agent / LLM Engineer, against contracts owned by #4.
// Room Scout is deterministic and lives in Spatial/RoomScout.cs.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RoomZero.Zero
{
    [Serializable]
    public class DirectorInput
    {
        public RoomMap room;
        public PlayerProfile profile;
        public string requestedTemplate;   // what the player asked for; empty = Director decides
        public List<string> templates = new List<string>(Enum.GetNames(typeof(MissionTemplateId)));
        public List<string> objectiveTypes = new List<string>(Enum.GetNames(typeof(ObjectiveType)));
        public List<string> npcBeats = new List<string>(NpcCatalog.Beats);
    }

    [Serializable]
    public class PuzzleInput
    {
        public RoomMap room;
        public MissionPlan plan;
        public float reachMeters;
    }

    [Serializable]
    public class Placement { public string objectiveId; public string anchorId; }

    [Serializable]
    public class PlacementList { public List<Placement> placements = new List<Placement>(); }

    [Serializable]
    public class NpcInput
    {
        public string beatId;
        public string missionState;
        public string route;
        public float guardAlertness;
        public PlayerProfile profile;
    }

    [Serializable]
    public class AdaptationInput
    {
        public MissionSignals signals;
        public List<string> alreadyApplied = new List<string>();
    }

    public interface IDirectorAgent
    {
        Task<MissionPlan> ProposeMissionAsync(DirectorInput input, CancellationToken ct);
    }

    public interface IPuzzleArchitectAgent
    {
        Task<PlacementList> PlacePuzzlesAsync(PuzzleInput input, CancellationToken ct);
    }

    public interface INpcAgent
    {
        Task<NpcLine> GetLineAsync(NpcInput input, CancellationToken ct);
    }

    public interface IMentorAgent
    {
        Task<MentorDecision> DecideAsync(MentorInput input, CancellationToken ct);
    }

    public interface IAdaptationAgent
    {
        Task<AdaptationProposal> ProposeAsync(AdaptationInput input, CancellationToken ct);
    }

    /// Player Model stays deterministic and local: it is game telemetry, not chat.
    public interface IPlayerModel
    {
        PlayerProfile Apply(PlayerProfile profile, SessionSummary session);
    }
}
