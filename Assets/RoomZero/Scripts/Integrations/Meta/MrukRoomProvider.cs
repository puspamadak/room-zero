// MR Utility Kit -> raw RoomMap. Compiled only when ROOMZERO_META_XR is defined.
// Requirements: an MRUK object in the scene (Building Block "MR Utility Kit" or the MRUK
// prefab) with scene loading enabled, the Scene permission, and Space Setup done on the
// headset (or a sample room selected in the Meta XR Simulator).
//
// VERIFY ON DAY ONE (gate G1): MRUK's API moves between SDK versions. This file uses
// MRUK.Instance, GetCurrentRoom(), room.Anchors, anchor.Label, PlaneRect, VolumeBounds.
// Geometry is computed orientation-agnostically from the anchor transform so it does not
// depend on MRUK's axis conventions. Room Scout then applies all safety filters.
// Owner: #2 MR / Room Understanding.

#if ROOMZERO_META_XR
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using RoomZero.Spatial;

namespace RoomZero.Integrations.Meta
{
    public class MrukRoomProvider : IRoomProvider
    {
        public string Name => "MRUK";

        public async Task<RoomProviderResult> LoadAsync(Transform head, CancellationToken ct)
        {
            var mruk = MRUK.Instance;
            if (mruk == null) return RoomProviderResult.Unavailable("no MRUK object in scene");

            for (int i = 0; i < 30 && mruk.GetCurrentRoom() == null; i++)
                await Task.Delay(200, ct);

            var room = mruk.GetCurrentRoom();
            if (room == null) return RoomProviderResult.Unavailable("no scene data (run Space Setup)");

            var map = new RoomMap { roomId = "mruk-room", source = "MRUK", playerOrigin = V3.To(head ? head.position : Vector3.zero) };
            int n = 0;
            foreach (var a in room.Anchors)
            {
                if (a == null) continue;
                string label = a.Label.ToString().ToUpperInvariant();   // e.g. TABLE, WALL_FACE, DOOR_FRAME
                string id = $"mruk-{n++:00}-{label.Split(',')[0].Trim().ToLowerInvariant()}";
                var t = a.transform;

                if (label.Contains("DOOR") || label.Contains("WINDOW"))
                {
                    var zone = new ZoneInfo { id = id, type = "excluded", center = V3.To(Floor(t.position) + Vector3.up * 1.1f), size = new[] { 1.4f, 2.4f, 1.4f } };
                    map.zones.Add(zone);
                    map.excludedZoneIds.Add(id);
                    continue;
                }
                if (label.Contains("FLOOR") || label.Contains("CEILING") || label.Contains("GLOBAL_MESH") || label.Contains("INVISIBLE")) continue;

                SurfaceInfo s = null;
                if (a.VolumeBounds.HasValue) s = FromVolume(id, label, t, a.VolumeBounds.Value);
                else if (a.PlaneRect.HasValue) s = FromPlane(id, label, t, a.PlaneRect.Value);
                if (s == null) continue;

                s.confidence = label.Contains("OTHER") ? 0.6f : 0.9f;
                map.surfaces.Add(s);
            }
            return new RoomProviderResult { status = RoomStatus.Available, map = map, message = $"{map.surfaces.Count} anchors" };
        }

        /// Top face of a volume (table, couch, storage...). Works for any local axis convention.
        static SurfaceInfo FromVolume(string id, string label, Transform t, Bounds b)
        {
            Vector3[] axes = { t.right, t.up, t.forward };
            float[] lengths = { b.size.x, b.size.y, b.size.z };
            int vertical = 0;
            for (int i = 1; i < 3; i++) if (Mathf.Abs(axes[i].y) > Mathf.Abs(axes[vertical].y)) vertical = i;
            int h1 = (vertical + 1) % 3, h2 = (vertical + 2) % 3;

            Vector3 center = t.TransformPoint(b.center);
            float topY = center.y + Mathf.Abs(axes[vertical].y) * lengths[vertical] * 0.5f;
            Vector3 a1 = Vector3.ProjectOnPlane(axes[h1], Vector3.up).normalized;

            return new SurfaceInfo
            {
                id = id,
                type = TypeFor(label),
                center = V3.To(new Vector3(center.x, topY, center.z)),
                size = new[] { lengths[h1], 0.03f, lengths[h2] },
                normal = new[] { 0f, 1f, 0f },
                yawDegrees = Mathf.Atan2(-a1.z, a1.x) * Mathf.Rad2Deg,
                allowed = true
            };
        }

        /// Planes (walls, flat tables). The Rect lives in local XY, so the normal is local Z.
        static SurfaceInfo FromPlane(string id, string label, Transform t, Rect r)
        {
            Vector3 normal = t.forward;
            bool horizontal = Mathf.Abs(normal.y) > 0.7f;
            if (horizontal && normal.y < 0) normal = -normal;
            Vector3 center = t.TransformPoint(new Vector3(r.center.x, r.center.y, 0));
            Vector3 a1 = Vector3.ProjectOnPlane(t.right, Vector3.up).normalized;

            return new SurfaceInfo
            {
                id = id,
                type = TypeFor(label),
                center = V3.To(center),
                size = horizontal ? new[] { r.width, 0.02f, r.height } : new[] { r.width, r.height, 0.02f },
                normal = V3.To(normal),
                yawDegrees = Mathf.Atan2(-a1.z, a1.x) * Mathf.Rad2Deg,
                allowed = true
            };
        }

        static string TypeFor(string label)
        {
            if (label.Contains("TABLE")) return "table";
            if (label.Contains("WALL")) return "wall";
            if (label.Contains("COUCH")) return "couch";
            if (label.Contains("STORAGE")) return "storage";
            if (label.Contains("BED")) return "bed";
            if (label.Contains("SCREEN")) return "screen";
            return "other";
        }

        static Vector3 Floor(Vector3 p) => new Vector3(p.x, 0, p.z);
    }
}
#endif
