// Room Scout brain center (deterministic, no AI): raw room data -> safe RoomMap.
// Guardrails from spec section 4: never infer hidden hazards; mark unknown confidence;
// exclude doors, stairs, edges and blocked zones. If scan data is missing, fall back
// to a bounded virtual layout in front of the player instead of guessing.
// Owner: #2 MR / Room Understanding.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RoomZero.Spatial
{
    public class RoomScout
    {
        readonly IRoomProvider provider;
        readonly RoomZeroConfig config;

        public RoomMap Current { get; private set; }

        public RoomScout(IRoomProvider provider, RoomZeroConfig config)
        {
            this.provider = provider;
            this.config = config;
        }

        public async Task<RoomMap> ScanAsync(Transform head, int timeoutMs, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            RoomProviderResult raw;
            try
            {
                var load = provider.LoadAsync(head, ct);
                var done = await Task.WhenAny(load, Task.Delay(timeoutMs, ct));
                raw = done == load ? load.Result : RoomProviderResult.Unavailable("timeout");
            }
            catch (Exception e)
            {
                raw = RoomProviderResult.Unavailable(e.GetType().Name);
            }

            RoomMap map;
            if (raw.status == RoomStatus.Available && raw.map != null)
            {
                map = Normalize(raw.map, head);
                ZeroLog.Info("RoomScout", $"{provider.Name}: {map.surfaces.Count} surfaces, {map.surfaces.FindAll(s => s.allowed).Count} allowed", sw.ElapsedMilliseconds);
            }
            else
            {
                map = EmptyMap(head);
                ZeroLog.Warn("RoomScout", $"{provider.Name} unavailable: {raw.message}", sw.ElapsedMilliseconds, "seated_fallback_layout");
            }

            EnsureReachableSurface(map, head);
            Current = map;
            return map;
        }

        /// Apply safety filters. Public so the self-tests can run it on fixtures.
        public RoomMap Normalize(RoomMap map, Transform head)
        {
            if (head) map.playerOrigin = V3.To(head.position);

            foreach (var s in map.surfaces)
            {
                if (string.IsNullOrEmpty(s.id)) { s.allowed = false; continue; }
                if (s.confidence < config.minSurfaceConfidence) s.allowed = false;

                string t = (s.type ?? "").ToLowerInvariant();
                if (t.Contains("door") || t.Contains("window") || t.Contains("stair") || t.Contains("unknown"))
                    s.allowed = false;

                foreach (var zid in map.excludedZoneIds)
                {
                    var z = map.FindZone(zid);
                    if (z != null && z.Bounds.Contains(s.Center)) s.allowed = false;
                }
            }
            return map;
        }

        public RoomMap EmptyMap(Transform head)
        {
            var origin = head ? head.position : new Vector3(0, 1.2f, 0);
            return new RoomMap { roomId = "fallback-room", source = "SEATED_FALLBACK", playerOrigin = V3.To(origin) };
        }

        /// The airplane-seat guarantee: there is always at least one allowed horizontal
        /// surface inside reach. If the real room has none, add a virtual desk.
        public void EnsureReachableSurface(RoomMap map, Transform head)
        {
            Vector3 origin = map.PlayerOrigin;
            foreach (var s in map.surfaces)
            {
                if (!s.allowed || !s.IsHorizontal) continue;
                if (HorizontalDistance(origin, s.Center) <= config.Reach &&
                    s.Center.y >= config.minObjectHeight && s.Center.y <= config.maxObjectHeight)
                    return;
            }

            Vector3 fwd = head ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            float deskY = Mathf.Clamp(origin.y - 0.45f, config.minObjectHeight + 0.05f, 1.1f);
            var desk = new Vector3(origin.x, deskY, origin.z) + fwd * Mathf.Min(0.45f, config.Reach * 0.8f);

            map.surfaces.Add(new SurfaceInfo
            {
                id = "virtual-desk",
                type = "virtual_desk",
                center = V3.To(desk),
                size = new[] { 0.7f, 0.02f, 0.4f },
                normal = new[] { 0f, 1f, 0f },
                confidence = 1f,
                allowed = true
            });
            if (!map.source.Contains("FALLBACK")) map.source += "+VIRTUAL_DESK";
            ZeroLog.Info("RoomScout", "no reachable real surface; added virtual-desk", -1, "no_reachable_surface");
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0; b.y = 0;
            return Vector3.Distance(a, b);
        }
    }
}
