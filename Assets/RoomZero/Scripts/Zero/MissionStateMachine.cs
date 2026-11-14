// Deterministic mission state machine (spec section 4):
// BOOT → ROOM_READY → MISSION_REQUESTED → PLANNING → VALIDATING → BRIEFING → ACTIVE →
// ADAPTING (optional) → COMPLETE → SAVING → READY_FOR_NEXT_SESSION.
// Every state has a timeout and an error/fallback transition. Illegal transitions are
// refused and logged, which makes "stuck state" bugs visible instead of silent.
// Owner: #4 AI Orchestration.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomZero.Zero
{
    public class MissionStateMachine
    {
        public MissionStateId State { get; private set; } = MissionStateId.BOOT;
        public float SecondsInState => Time.unscaledTime - enteredAt - heldTotal - (held ? Time.unscaledTime - heldSince : 0f);

        public event Action<MissionStateId, MissionStateId, string> Changed;
        public event Action<MissionStateId> TimedOut;

        float enteredAt, heldSince, heldTotal;
        bool held, timeoutFired;

        static readonly Dictionary<MissionStateId, MissionStateId[]> Allowed = new Dictionary<MissionStateId, MissionStateId[]>
        {
            { MissionStateId.BOOT,                   new[] { MissionStateId.ROOM_READY, MissionStateId.ERROR } },
            { MissionStateId.ROOM_READY,             new[] { MissionStateId.MISSION_REQUESTED, MissionStateId.BOOT, MissionStateId.ERROR } },
            { MissionStateId.MISSION_REQUESTED,      new[] { MissionStateId.PLANNING, MissionStateId.ERROR } },
            { MissionStateId.PLANNING,               new[] { MissionStateId.VALIDATING, MissionStateId.ERROR } },
            { MissionStateId.VALIDATING,             new[] { MissionStateId.BRIEFING, MissionStateId.PLANNING, MissionStateId.ERROR } },
            { MissionStateId.BRIEFING,               new[] { MissionStateId.ACTIVE, MissionStateId.ERROR } },
            { MissionStateId.ACTIVE,                 new[] { MissionStateId.ADAPTING, MissionStateId.COMPLETE, MissionStateId.ERROR } },
            { MissionStateId.ADAPTING,               new[] { MissionStateId.ACTIVE, MissionStateId.COMPLETE, MissionStateId.ERROR } },
            { MissionStateId.COMPLETE,               new[] { MissionStateId.SAVING, MissionStateId.ERROR } },
            { MissionStateId.SAVING,                 new[] { MissionStateId.READY_FOR_NEXT_SESSION, MissionStateId.ERROR } },
            { MissionStateId.READY_FOR_NEXT_SESSION, new[] { MissionStateId.MISSION_REQUESTED, MissionStateId.ROOM_READY, MissionStateId.BOOT } },
            { MissionStateId.ERROR,                  new[] { MissionStateId.ROOM_READY, MissionStateId.BRIEFING, MissionStateId.BOOT } },
        };

        /// Seconds before TimedOut fires. Player-paced states wait forever by design
        /// (the player is reading or deciding), and ACTIVE is governed by the mission timer.
        public static readonly Dictionary<MissionStateId, float> Timeouts = new Dictionary<MissionStateId, float>
        {
            { MissionStateId.BOOT, 15f },
            { MissionStateId.ROOM_READY, float.PositiveInfinity },
            { MissionStateId.MISSION_REQUESTED, 3f },
            { MissionStateId.PLANNING, 10f },
            { MissionStateId.VALIDATING, 3f },
            { MissionStateId.BRIEFING, 90f },          // no choice made -> default SILENT_ROUTE
            { MissionStateId.ACTIVE, float.PositiveInfinity },
            { MissionStateId.ADAPTING, 6f },
            { MissionStateId.COMPLETE, 4f },
            { MissionStateId.SAVING, 4f },
            { MissionStateId.READY_FOR_NEXT_SESSION, float.PositiveInfinity },
            { MissionStateId.ERROR, 4f },
        };

        public bool Is(params MissionStateId[] states) => Array.IndexOf(states, State) >= 0;

        public bool TransitionTo(MissionStateId to, string reason = null)
        {
            if (!Allowed.TryGetValue(State, out var next) || Array.IndexOf(next, to) < 0)
            {
                ZeroLog.Error("StateMachine", $"illegal transition {State} -> {to} ({reason})");
                return false;
            }
            var from = State;
            State = to;
            ZeroLog.State = to;
            enteredAt = Time.unscaledTime;
            heldTotal = 0; held = false; timeoutFired = false;
            ZeroLog.Info("StateMachine", $"{from} -> {to}" + (reason != null ? $" ({reason})" : ""));
            Changed?.Invoke(from, to, reason);
            EventBus.Publish(new MissionStateChanged { from = from, to = to, reason = reason });
            return true;
        }

        /// Pause freezes state timeouts (e.g. reading the briefing while paused).
        public void Hold(bool hold)
        {
            if (hold == held) return;
            if (hold) heldSince = Time.unscaledTime;
            else heldTotal += Time.unscaledTime - heldSince;
            held = hold;
        }

        public void Tick()
        {
            if (held || timeoutFired) return;
            if (SecondsInState > Timeouts[State])
            {
                timeoutFired = true;
                ZeroLog.Warn("StateMachine", $"{State} timed out after {Timeouts[State]}s");
                TimedOut?.Invoke(State);
            }
        }
    }
}
