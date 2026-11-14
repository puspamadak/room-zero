// Normalized events (spec: MissionRequested, HandAction, VoiceIntent, ObjectiveCompleted ...).
// Owner: #1 Tech Lead. Input (#3) publishes; ZERO (#4) and Game (#6) subscribe.

using System;
using System.Collections.Generic;

namespace RoomZero
{
    public static class EventBus
    {
        static readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler)
        {
            handlers.TryGetValue(typeof(T), out var existing);
            handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handlers.TryGetValue(typeof(T), out var existing))
                handlers[typeof(T)] = Delegate.Remove(existing, handler);
        }

        public static void Publish<T>(T evt)
        {
            if (handlers.TryGetValue(typeof(T), out var d) && d is Action<T> action)
            {
                try { action(evt); }
                catch (Exception e) { ZeroLog.Error("EventBus", $"{typeof(T).Name} handler threw: {e}"); }
            }
        }

        public static void Clear() => handlers.Clear();
        // Handlers from a previous Play session point at destroyed objects; drop them.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => handlers.Clear();
    }

    // ---------- Event types ----------

    public enum InputSource { Hand, Voice, Ui, Desktop, Simulator }

    public enum VoiceIntentId { UNKNOWN, START_MISSION, HINT, PAUSE, RESUME, CHOOSE_SILENT, CHOOSE_LOUD, NEXT_MISSION }

    public struct MissionRequested
    {
        public InputSource source;
        public string template;   // optional MissionTemplateId the player asked for (e.g. "TREASURE_HUNT")
    }

    public struct HandAction
    {
        public string targetId;
        public string action;      // "poke" | "grab" ...
        public InputSource source;
    }

    public struct VoiceIntent
    {
        public VoiceIntentId intent;
        public string transcript;
    }

    public struct RouteChosen { public RouteChoice route; }
    public struct HintRequested { public InputSource source; }
    public struct PauseRequested { public bool paused; public string reason; }
    public struct NextMissionRequested { }
    public struct ResetProfileRequested { }
    public struct HandTrackingChanged { public bool tracked; }

    public struct ObjectiveCompleted { public string objectiveId; }
    public struct ObjectiveFailedAttempt { public string objectiveId; }

    public struct MissionStateChanged { public MissionStateId from; public MissionStateId to; public string reason; }
}
