// Room providers produce RAW room data; RoomScout normalizes it into a safe RoomMap.
// Owner: #2 MR / Room Understanding.
//
// MockRoomProvider gives gameplay people (#3, #6) a fake room on day one so nobody waits
// for headset scene data. The MRUK provider lives in Integrations/Meta.

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RoomZero.Spatial
{
    public enum RoomStatus { Available, Unavailable }

    public class RoomProviderResult
    {
        public RoomStatus status;
        public RoomMap map;          // raw: not yet filtered for safety
        public string message;

        public static RoomProviderResult Unavailable(string why) =>
            new RoomProviderResult { status = RoomStatus.Unavailable, message = why };
    }

    public interface IRoomProvider
    {
        string Name { get; }
        Task<RoomProviderResult> LoadAsync(Transform head, CancellationToken ct);
    }

    /// A believable small room built around wherever the player's head is.
    /// Includes deliberately bad surfaces so the validator has something to reject.
    public class MockRoomProvider : IRoomProvider
    {
        public string Name => "MockRoom";

        public Task<RoomProviderResult> LoadAsync(Transform head, CancellationToken ct)
        {
            Vector3 headPos = head ? head.position : new Vector3(0, 1.2f, 0);
            Vector3 fwd = head ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 floor = new Vector3(headPos.x, 0f, headPos.z);

            float yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y;
            var map = new RoomMap { roomId = "mock-room-01", source = "MOCK", playerOrigin = V3.To(new Vector3(headPos.x, headPos.y, headPos.z)) };

            map.surfaces.Add(Surface("surface-desk", "table", floor + fwd * 0.55f + Vector3.up * 0.74f, new Vector3(0.6f, 0.03f, 0.5f), Vector3.up, 0.95f, yaw));
            map.surfaces.Add(Surface("surface-side", "storage", floor + fwd * 0.45f + right * 0.55f + Vector3.up * 0.65f, new Vector3(0.4f, 0.03f, 0.4f), Vector3.up, 0.85f, yaw));
            map.surfaces.Add(Surface("surface-wall-front", "wall", floor + fwd * 1.9f + Vector3.up * 1.3f, new Vector3(3f, 2.6f, 0.02f), -fwd, 0.9f, yaw));
            map.surfaces.Add(Surface("surface-shelf", "storage", floor + fwd * 0.4f - right * 0.5f + Vector3.up * 0.9f, new Vector3(0.35f, 0.03f, 0.3f), Vector3.up, 0.9f, yaw));
            // Bad on purpose: too far for seated play.
            map.surfaces.Add(Surface("surface-far-shelf", "storage", floor + fwd * 2.6f - right * 0.8f + Vector3.up * 1.0f, new Vector3(0.8f, 0.03f, 0.3f), Vector3.up, 0.9f, yaw));
            // Bad on purpose: low confidence.
            map.surfaces.Add(Surface("surface-blurry", "table", floor - fwd * 0.4f - right * 0.7f + Vector3.up * 0.5f, new Vector3(0.5f, 0.03f, 0.5f), Vector3.up, 0.35f, yaw));

            map.zones.Add(new ZoneInfo { id = "zone-play", type = "playable", center = V3.To(floor + Vector3.up * 1f), size = new[] { 3f, 2f, 3f } });
            map.zones.Add(new ZoneInfo { id = "door-zone", type = "excluded", center = V3.To(floor - right * 1.4f + fwd * 0.5f + Vector3.up * 1f), size = new[] { 1f, 2.2f, 1f } });
            map.excludedZoneIds.Add("door-zone");

            return Task.FromResult(new RoomProviderResult { status = RoomStatus.Available, map = map, message = "mock room" });
        }

        static SurfaceInfo Surface(string id, string type, Vector3 c, Vector3 size, Vector3 normal, float conf, float yaw) =>
            new SurfaceInfo { id = id, type = type, center = V3.To(c), size = V3.To(size), normal = V3.To(normal), confidence = conf, allowed = true, yawDegrees = yaw };
    }
}
