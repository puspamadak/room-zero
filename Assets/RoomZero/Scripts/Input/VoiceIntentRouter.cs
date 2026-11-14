// Voice -> normalized intents. Speech-to-text is NOT done here; any recognizer
// (Meta Voice SDK, Android STT, a test harness) just calls Submit(transcript).
// Every voice intent also has a hand/UI equivalent (spec: never voice-only).
// Owner: #3 Interaction + VR UX.
//
// Meta Voice SDK hookup (after installing it):
//   appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(VoiceIntentRouter.Submit);
//   and call appVoiceExperience.Activate() on a "hold to talk" gesture or wake word.

using UnityEngine;

namespace RoomZero.Input
{
    public static class VoiceIntentRouter
    {
        public static VoiceIntentId Parse(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return VoiceIntentId.UNKNOWN;
            string t = transcript.ToLowerInvariant();

            if (Has(t, "resume", "continue", "unpause")) return VoiceIntentId.RESUME;
            if (Has(t, "pause", "stop", "wait")) return VoiceIntentId.PAUSE;
            if (Has(t, "hint", "help", "stuck")) return VoiceIntentId.HINT;
            if (Has(t, "silent", "quiet", "stealth", "sneak")) return VoiceIntentId.CHOOSE_SILENT;
            if (Has(t, "loud", "smash", "force", "fast route")) return VoiceIntentId.CHOOSE_LOUD;
            if (Has(t, "next", "again", "another")) return VoiceIntentId.NEXT_MISSION;
            if (Has(t, "mission", "start", "begin", "heist", "treasure", "formula", "harvest")) return VoiceIntentId.START_MISSION;
            return VoiceIntentId.UNKNOWN;
        }

        public static void Submit(string transcript) => Submit(transcript, InputSource.Voice);

        public static void Submit(string transcript, InputSource source)
        {
            var intent = Parse(transcript);
            ZeroLog.Info("Voice", $"\"{transcript}\" -> {intent}");
            EventBus.Publish(new VoiceIntent { intent = intent, transcript = transcript });

            switch (intent)
            {
                case VoiceIntentId.START_MISSION:
                    string lower = transcript.ToLowerInvariant();
                    bool hunt = lower.Contains("treasure") || lower.Contains("formula") || lower.Contains("harvest");
                    EventBus.Publish(new MissionRequested { source = source, template = hunt ? "TREASURE_HUNT" : null });
                    break;
                case VoiceIntentId.HINT: EventBus.Publish(new HintRequested { source = source }); break;
                case VoiceIntentId.PAUSE: EventBus.Publish(new PauseRequested { paused = true, reason = "voice" }); break;
                case VoiceIntentId.RESUME: EventBus.Publish(new PauseRequested { paused = false, reason = "voice" }); break;
                case VoiceIntentId.CHOOSE_SILENT: EventBus.Publish(new RouteChosen { route = RouteChoice.SILENT_ROUTE }); break;
                case VoiceIntentId.CHOOSE_LOUD: EventBus.Publish(new RouteChosen { route = RouteChoice.LOUD_ROUTE }); break;
                case VoiceIntentId.NEXT_MISSION: EventBus.Publish(new NextMissionRequested()); break;
            }
        }

        static bool Has(string t, params string[] words)
        {
            foreach (var w in words) if (t.Contains(w)) return true;
            return false;
        }
    }
}
