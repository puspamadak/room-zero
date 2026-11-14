// Remote (LLM-backed) agents. The headset POSTs compact JSON to YOUR agent proxy
// (tools/agent-proxy), which calls the model and returns structured JSON.
// API keys live on the proxy, never in the APK.
//
// Endpoint contract:  POST {agentProxyUrl}/agents/{agentName}
//   body:  the agent's *Input class as JSON
//   reply: the agent's output class as JSON (MissionPlan, PlacementList, NpcLine, AdaptationProposal)
//
// Anything that fails to arrive, parse, or validate is replaced by the local agent
// in ZeroOrchestrator. These classes never decide anything on their own.
// Owner: #5 Agent / LLM Engineer.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace RoomZero.Zero
{
    public class AgentProxyClient
    {
        readonly string baseUrl;
        public AgentProxyClient(string baseUrl) { this.baseUrl = (baseUrl ?? "").TrimEnd('/'); }

        public async Task<TOut> CallAsync<TIn, TOut>(string agent, TIn input, CancellationToken ct) where TOut : class
        {
            string body = JsonUtility.ToJson(input);
            using (var req = new UnityWebRequest($"{baseUrl}/agents/{agent}", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                var op = req.SendWebRequest();
                var tcs = new TaskCompletionSource<bool>();
                op.completed += _ => tcs.TrySetResult(true);
                using (ct.Register(() => { req.Abort(); tcs.TrySetCanceled(); }))
                    await tcs.Task;

                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"{agent}: HTTP {req.responseCode} {req.error}");

                var text = req.downloadHandler.text;
                if (string.IsNullOrEmpty(text) || text.Length > 16000)
                    throw new Exception($"{agent}: empty or oversized reply");

                var parsed = JsonUtility.FromJson<TOut>(text);
                if (parsed == null) throw new Exception($"{agent}: unparseable reply");
                return parsed;
            }
        }
    }

    public class RemoteDirectorAgent : IDirectorAgent
    {
        readonly AgentProxyClient client;
        public RemoteDirectorAgent(AgentProxyClient c) { client = c; }
        public Task<MissionPlan> ProposeMissionAsync(DirectorInput input, CancellationToken ct)
            => client.CallAsync<DirectorInput, MissionPlan>("director", input, ct);
    }

    public class RemotePuzzleArchitectAgent : IPuzzleArchitectAgent
    {
        readonly AgentProxyClient client;
        public RemotePuzzleArchitectAgent(AgentProxyClient c) { client = c; }
        public Task<PlacementList> PlacePuzzlesAsync(PuzzleInput input, CancellationToken ct)
            => client.CallAsync<PuzzleInput, PlacementList>("puzzle_architect", input, ct);
    }

    public class RemoteNpcAgent : INpcAgent
    {
        readonly AgentProxyClient client;
        public RemoteNpcAgent(AgentProxyClient c) { client = c; }
        public Task<NpcLine> GetLineAsync(NpcInput input, CancellationToken ct)
            => client.CallAsync<NpcInput, NpcLine>("npc", input, ct);
    }

    public class RemoteMentorAgent : IMentorAgent
    {
        readonly AgentProxyClient client;
        public RemoteMentorAgent(AgentProxyClient c) { client = c; }
        public Task<MentorDecision> DecideAsync(MentorInput input, CancellationToken ct)
            => client.CallAsync<MentorInput, MentorDecision>("mentor", input, ct);
    }

    public class RemoteAdaptationAgent : IAdaptationAgent
    {
        readonly AgentProxyClient client;
        public RemoteAdaptationAgent(AgentProxyClient c) { client = c; }
        public Task<AdaptationProposal> ProposeAsync(AdaptationInput input, CancellationToken ct)
            => client.CallAsync<AdaptationInput, AdaptationProposal>("adaptation", input, ct);
    }
}
