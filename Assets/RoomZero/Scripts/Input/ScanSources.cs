// Everything that can "scan" or "reach" in the room, in one place, for any input:
// fingertips (real/simulated hands), controller laser pointers, and the desktop mouse ray.
// Objectives like HIDDEN_RELIC ask "is anything scanning this point?" without caring which.
// Owner: #3 Interaction + VR UX.

using System.Collections.Generic;
using UnityEngine;

namespace RoomZero.Input
{
    public static class ScanSources
    {
        static readonly Dictionary<int, Ray> rays = new Dictionary<int, Ray>();
        static readonly Dictionary<int, int> rayFrame = new Dictionary<int, int>();
        static readonly List<Transform> points = new List<Transform>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { rays.Clear(); rayFrame.Clear(); points.Clear(); }

        /// Pointers call this every frame they're active (id: any stable number per pointer).
        public static void SetRay(int id, Ray ray) { rays[id] = ray; rayFrame[id] = Time.frameCount; }

        public static void AddPoint(Transform t) { if (t && !points.Contains(t)) points.Add(t); }
        public static void RemovePoint(Transform t) { points.Remove(t); }

        /// True if a fingertip is within `reach` metres, or a fresh pointer ray passes within `rayRadius`.
        public static bool IsScanning(Vector3 target, float reach = 0.15f, float rayRadius = 0.09f)
        {
            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (!points[i]) { points.RemoveAt(i); continue; }
                if ((points[i].position - target).sqrMagnitude <= reach * reach) return true;
            }
            foreach (var kv in rays)
            {
                if (rayFrame[kv.Key] < Time.frameCount - 1) continue; // stale (pointer inactive)
                if (DistanceToRay(kv.Value, target) <= rayRadius) return true;
            }
            return false;
        }

        public static float DistanceToRay(Ray r, Vector3 p)
        {
            Vector3 d = r.direction.normalized;
            Vector3 v = p - r.origin;
            float t = Mathf.Max(0f, Vector3.Dot(v, d));
            return Vector3.Distance(r.origin + d * t, p);
        }
    }
}
