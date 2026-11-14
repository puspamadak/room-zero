// Menu: RoomZero > Enable / Disable Meta XR Integration
// Adds/removes the ROOMZERO_META_XR scripting define (Android + Standalone), which compiles
// Integrations/Meta (MRUK room provider, OVR hand fingertips). The template compiles and
// runs WITHOUT the Meta SDK, so desktop / gameplay work never waits for headset setup.

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RoomZero.EditorTools
{
    public static class MetaIntegrationToggle
    {
        const string Define = "ROOMZERO_META_XR";
        static readonly NamedBuildTarget[] Targets = { NamedBuildTarget.Android, NamedBuildTarget.Standalone };

        [MenuItem("RoomZero/Enable Meta XR Integration", priority = 10)]
        static void Enable()
        {
            bool hasOvr = TypeExists("OVRHand");
            bool hasMruk = TypeExists("Meta.XR.MRUtilityKit.MRUK");
            if (!hasOvr || !hasMruk)
            {
                EditorUtility.DisplayDialog("Meta XR SDK not found",
                    $"Install the Meta XR All-in-One SDK (or at least Core + MR Utility Kit) first.\n\nOVRHand found: {hasOvr}\nMRUK found: {hasMruk}",
                    "OK");
                return;
            }
            foreach (var t in Targets) Set(t, true);
            Debug.Log("[RZ] ROOMZERO_META_XR enabled. Unity will recompile.");
        }

        [MenuItem("RoomZero/Disable Meta XR Integration", priority = 11)]
        static void Disable()
        {
            foreach (var t in Targets) Set(t, false);
            Debug.Log("[RZ] ROOMZERO_META_XR disabled.");
        }

        [MenuItem("RoomZero/Enable Meta XR Integration", true)]
        static bool CanEnable() => !IsOn();
        [MenuItem("RoomZero/Disable Meta XR Integration", true)]
        static bool CanDisable() => IsOn();

        static bool IsOn() => PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Contains(Define);

        static void Set(NamedBuildTarget target, bool on)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => d.Length > 0 && d != Define).ToList();
            if (on) defines.Add(Define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

        static bool TypeExists(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => { try { return a.GetType(fullName, false) != null; } catch { return false; } });
    }
}
#endif
