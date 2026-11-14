// Single logging convention (spec section 8):
// session ID, state name, agent name, duration, fallback reason.
// Filter the Console / `adb logcat -s Unity` by "[RZ]".

using System.Collections.Generic;
using UnityEngine;

namespace RoomZero
{
    public static class ZeroLog
    {
        public static string SessionId = "no-session";
        /// Info lines go to the Console only when true (RoomZeroConfig.verboseLogs). They always go
        /// to the in-game trace panel (G). Warnings and errors always reach the Console.
        public static bool Verbose = false;
        public static MissionStateId State = MissionStateId.BOOT;

        /// Last lines, shown on the in-headset debug panel.
        public static readonly List<string> Recent = new List<string>();
        const int MaxRecent = 12;

        public static void Info(string agent, string message, long durationMs = -1, string fallbackReason = null)
            => Write(LogType.Log, agent, message, durationMs, fallbackReason);

        public static void Warn(string agent, string message, long durationMs = -1, string fallbackReason = null)
            => Write(LogType.Warning, agent, message, durationMs, fallbackReason);

        public static void Error(string agent, string message)
            => Write(LogType.Error, agent, message, -1, null);

        static void Write(LogType type, string agent, string message, long durationMs, string fallbackReason)
        {
            var line = $"[RZ][{SessionId}][{State}][{agent}] {message}";
            if (durationMs >= 0) line += $" ({durationMs}ms)";
            if (!string.IsNullOrEmpty(fallbackReason)) line += $" fallback={fallbackReason}";

            Recent.Add(line.Replace("[RZ]", ""));
            if (Recent.Count > MaxRecent) Recent.RemoveAt(0);

            switch (type)
            {
                case LogType.Error: Debug.LogError(line); break;
                case LogType.Warning: Debug.LogWarning(line); break;
                default: if (Verbose) Debug.Log(line); break;
            }
        }
    }
}
