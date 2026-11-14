// Meta XR hands -> PokeTargets. Compiled only when ROOMZERO_META_XR is defined
// (menu: RoomZero > Enable Meta XR Integration, after installing the Meta XR SDK).
//
// - Finds every OVRSkeleton (hand prefabs in OVRCameraRig / Building Blocks) and puts a
//   Fingertip trigger on each index-finger tip, so poking works with real hands and with
//   the Meta XR Simulator's synthetic hands.
// - Publishes HandTrackingChanged after 1.5 s without tracked hands (spec: "Hand tracking
//   lost -> game pauses or explains how to recover").
// If your rig uses Interaction SDK synthetic hands without OVRSkeleton, add the Fingertip
// component to the index tip transform yourself; nothing else changes.
// Owner: #3 Interaction + VR UX.

#if ROOMZERO_META_XR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RoomZero.Input;

namespace RoomZero.Integrations.Meta
{
    public class MetaHandFingertips : MonoBehaviour
    {
        public float lostAfterSeconds = 1.5f;

        readonly HashSet<OVRSkeleton> attached = new HashSet<OVRSkeleton>();
        OVRHand[] hands = new OVRHand[0];
        float nextScan, lostSince = -1f;
        bool reportedLost;

        void Update()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 1f;
                AttachFingertips();
                hands = SceneQuery.FindAll<OVRHand>().ToArray();
            }
            WatchTracking();
        }

        void AttachFingertips()
        {
            foreach (var sk in SceneQuery.FindAll<OVRSkeleton>())
            {
                if (attached.Contains(sk) || !sk.IsInitialized || sk.Bones == null) continue;
                foreach (var bone in sk.Bones)
                {
                    // Matches both Hand_IndexTip (legacy) and XRHand_IndexTip (OpenXR skeleton).
                    if (bone == null || !bone.Transform || !bone.Id.ToString().Contains("IndexTip")) continue;
                    var tip = new GameObject("RZ_Fingertip");
                    tip.transform.SetParent(bone.Transform, false);
                    tip.AddComponent<SphereCollider>();
                    tip.AddComponent<Fingertip>();
                    attached.Add(sk);
                    ZeroLog.Info("MetaHands", $"fingertip attached to {sk.name}");
                    break;
                }
            }
        }

        void WatchTracking()
        {
            if (hands.Length == 0) return;
            bool anyHand = false;
            foreach (var h in hands) if (h && h.IsTracked) anyHand = true;
            bool controllers = (OVRInput.GetActiveController() & OVRInput.Controller.Touch) != 0;

            if (anyHand || controllers)
            {
                lostSince = -1f;
                if (reportedLost) { reportedLost = false; EventBus.Publish(new HandTrackingChanged { tracked = true }); }
            }
            else
            {
                if (lostSince < 0) lostSince = Time.unscaledTime;
                if (!reportedLost && Time.unscaledTime - lostSince > lostAfterSeconds)
                {
                    reportedLost = true;
                    EventBus.Publish(new HandTrackingChanged { tracked = false });
                }
            }
        }
    }
}
#endif
