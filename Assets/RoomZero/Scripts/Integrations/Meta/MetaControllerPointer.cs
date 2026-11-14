// Controller laser pointer: aim + index trigger presses any PokeTarget.
// Why: in the Meta XR Simulator, keyboard/mouse drive *simulated controllers*, so a
// fingertip can't easily touch a button. On a real Quest with hands, controllers are not
// connected and the pointer hides itself, so hands stay the primary input (competition
// rule: fully usable with hands). Compiled only with ROOMZERO_META_XR.
// Owner: #3 Interaction + VR UX.

#if ROOMZERO_META_XR
using UnityEngine;
using RoomZero.Input;
using RoomZero.UI;

namespace RoomZero.Integrations.Meta
{
    public class MetaControllerPointer : MonoBehaviour
    {
        public float maxDistance = 3f;

        OVRCameraRig rig;
        readonly LineRenderer[] lines = new LineRenderer[2];
        float nextFind;

        void Update()
        {
            if (!rig)
            {
                if (Time.unscaledTime < nextFind) return;
                nextFind = Time.unscaledTime + 1f;
                rig = SceneQuery.FindFirst<OVRCameraRig>();
                if (!rig) return;
                lines[0] = MakeLine("RZ_PointerLeft");
                lines[1] = MakeLine("RZ_PointerRight");
            }
            Handle(lines[0], rig.leftControllerAnchor, OVRInput.Controller.LTouch);
            Handle(lines[1], rig.rightControllerAnchor, OVRInput.Controller.RTouch);
        }

        void Handle(LineRenderer line, Transform anchor, OVRInput.Controller controller)
        {
            bool active = anchor && OVRInput.IsControllerConnected(controller);
            line.enabled = active;
            if (!active) return;

            var ray = new Ray(anchor.position, anchor.forward);
            ScanSources.SetRay(controller == OVRInput.Controller.LTouch ? 1 : 2, ray); // also scans hidden relics
            bool hit = Physics.Raycast(ray, out var info, maxDistance, ~0, QueryTriggerInteraction.Collide);
            var target = hit ? info.collider.GetComponentInParent<PokeTarget>() : null;

            line.SetPosition(0, ray.origin);
            line.SetPosition(1, hit ? info.point : ray.origin + ray.direction * maxDistance);
            line.sharedMaterial = UiFactory.Mat(target ? Palette.Success : Palette.Holo);

            if (target && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
                target.Press(InputSource.Simulator);
        }

        LineRenderer MakeLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.004f;
            lr.endWidth = 0.002f;
            lr.sharedMaterial = UiFactory.Mat(Palette.Holo);
            return lr;
        }
    }
}
#endif
