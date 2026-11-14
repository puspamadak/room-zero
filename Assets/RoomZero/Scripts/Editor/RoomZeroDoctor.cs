// Menu: RoomZero > Check Project Compatibility       (one report, every known pitfall)
// Menu: RoomZero > Install XR Packages (match this Unity)
// Android pre-build check: warns about duplicate AAR namespaces BEFORE Gradle fails
// (Unity 6000.4.4+ uses Android Gradle Plugin 9, which rejects them; Meta SDKs older than
// v207 ship OVRPlugin.aar and InteractionSdk.aar with the same namespace).
// Owner: #1 Tech Lead + #8 QA.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoomZero.EditorTools
{
    public static class RoomZeroDoctor
    {
        // ------------------------------------------------------------------ report

        [MenuItem("RoomZero/Check Project Compatibility", priority = 1)]
        public static void Check()
        {
            var r = new Report();
            var unity = ParseUnity(Application.unityVersion);

            // Unity version
            string uv = Application.unityVersion;
            if (unity.major != 6000) r.Bad($"Unity {uv}: this template targets Unity 6 (6000.x).");
            else if (unity.minor == 0 && unity.patch < 66) r.Warn($"Unity {uv}: Meta XR SDK v207 needs 6000.0.66f2 or newer. Update the 6000.0 LTS patch.");
            else if (unity.minor == 0 || unity.minor == 3) r.Ok($"Unity {uv} (LTS).");
            else if (AtLeast(unity, 6000, 4, 4)) r.Ok($"Unity {uv}: fine with Meta XR SDK v207+. Older Meta SDKs fail here (Gradle namespace / EntityId errors).");
            else r.Warn($"Unity {uv}: not an LTS. Prefer 6000.0 or 6000.3 LTS, or 6000.4.4+ with Meta SDK v207.");

            // Render pipeline
            var rp = GraphicsSettings.currentRenderPipeline;
            r.Ok(rp == null ? "Render pipeline: Built-in (supported by the template)." : $"Render pipeline: {rp.GetType().Name} ({rp.name}).");

            // Input
#if ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
            r.Ok("Active Input Handling: Both.");
#elif ENABLE_LEGACY_INPUT_MANAGER
            r.Ok("Active Input Handling: Input Manager (Old).");
#else
            if (TypeExists("UnityEngine.InputSystem.Keyboard")) r.Ok("Active Input Handling: Input System package " + Version("com.unity.inputsystem") + ".");
            else r.Bad("Active Input Handling is 'Input System' but the package is missing. Install it, or set Player > Active Input Handling to 'Both'.");
#endif

            // XR
            bool xrMgmt = TypeExists("UnityEngine.XR.Management.XRGeneralSettings");
            bool openxr = TypeExists("UnityEngine.XR.OpenXR.OpenXRSettings");
            if (!xrMgmt || !openxr) r.Warn("XR Plug-in Management / OpenXR not installed. Needed for Quest + simulator: RoomZero > Install XR Packages.");
            else
            {
                string ov = Version("com.unity.xr.openxr");
                var v = ParseSemver(ov);
                if (v.major == 1 && v.minor < 13) r.Warn($"OpenXR plugin {ov}: Meta XR Simulator on macOS needs 1.13.0+.");
                else r.Ok($"OpenXR plugin {ov}.");
            }

            // Meta SDK
            bool ovr = TypeExists("OVRManager");
            bool mruk = TypeExists("Meta.XR.MRUtilityKit.MRUK");
            string metaVer = Version("com.meta.xr.sdk.core");
            if (!ovr) r.Warn("Meta XR SDK not installed (desktop mode still works). For Quest/simulator install Meta XR All-in-One SDK v207+.");
            else
            {
                var mv = ParseSemver(metaVer);
                if (mv.major > 0 && mv.major < 207 && AtLeast(unity, 6000, 4, 4))
                    r.Bad($"Meta XR Core SDK {metaVer} on Unity {uv}: upgrade to v207+ (older versions fail to build on 6000.4.4+).");
                else r.Ok($"Meta XR Core SDK {metaVer}. MRUK: {(mruk ? "yes" : "MISSING (install Meta MR Utility Kit)")}.");
            }

            // Define vs SDK
            bool defineOn = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Contains("ROOMZERO_META_XR");
            if (defineOn && !(ovr && mruk)) r.Bad("ROOMZERO_META_XR is on but the Meta SDK/MRUK is missing: RoomZero > Disable Meta XR Integration.");
            else if (!defineOn && ovr && mruk) r.Warn("Meta SDK installed but integration is off: RoomZero > Enable Meta XR Integration.");
            else r.Ok($"Meta integration define: {(defineOn ? "on" : "off")}.");

            // Scene + config
            r.Check(File.Exists("Assets/RoomZero/RoomZeroConfig.asset"), "Config asset present.", "No config yet: RoomZero > Build Demo Scene.");
            r.Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("RoomZero_Main.unity")), "RoomZero_Main is in the build scene list.", "RoomZero_Main not in build scenes: RoomZero > Build Demo Scene.");

            // Android
            if (ovr)
            {
                var dups = DuplicateAarNamespaces();
                if (dups.Count == 0) r.Ok("Android AAR namespaces: no duplicates.");
                else if (AtLeast(unity, 6000, 4, 4)) r.Bad("Duplicate Android namespaces (Gradle will fail): " + string.Join("; ", dups) + ". Upgrade the Meta SDK packages to v207+.");
                else r.Ok("Duplicate AAR namespaces present but harmless before Unity 6000.4.4.");
            }

            Debug.Log(r.Text());
            EditorUtility.DisplayDialog("ROOM//ZERO compatibility",
                $"{r.ok} OK · {r.warn} warnings · {r.bad} problems\n\nFull report in the Console.", "OK");
        }

        // ------------------------------------------------------------------ XR packages

        [MenuItem("RoomZero/Install XR Packages (match this Unity)", priority = 2)]
        public static void InstallXr()
        {
            // No version numbers: Package Manager picks the version recommended for THIS editor.
            var request = UnityEditor.PackageManager.Client.AddAndRemove(
                new[] { "com.unity.xr.management", "com.unity.xr.openxr" }, null);
            Debug.Log("[RZ] Installing XR Plug-in Management + OpenXR (versions matched to this Unity)...");
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= poll;
                if (request.Status == UnityEditor.PackageManager.StatusCode.Success) Debug.Log("[RZ] XR packages installed.");
                else Debug.LogError("[RZ] XR package install failed: " + request.Error?.message);
            };
            EditorApplication.update += poll;
        }

        // ------------------------------------------------------------------ helpers

        public static List<string> DuplicateAarNamespaces()
        {
            var byNamespace = new Dictionary<string, List<string>>();
            foreach (var imp in PluginImporter.GetAllImporters())
            {
                if (imp == null || !imp.assetPath.EndsWith(".aar", StringComparison.OrdinalIgnoreCase)) continue;
                if (!imp.GetCompatibleWithPlatform(BuildTarget.Android)) continue;
                string ns = ReadAarNamespace(Physical(imp.assetPath));
                if (string.IsNullOrEmpty(ns)) continue;
                if (!byNamespace.TryGetValue(ns, out var list)) byNamespace[ns] = list = new List<string>();
                list.Add(Path.GetFileName(imp.assetPath));
            }
            return byNamespace.Where(kv => kv.Value.Count > 1)
                              .Select(kv => $"{kv.Key} in {string.Join(", ", kv.Value)}").ToList();
        }

        static string Physical(string assetPath)
        {
            try { return Path.GetFullPath(FileUtil.GetPhysicalPath(assetPath)); }
            catch { return Path.GetFullPath(assetPath); }
        }

        static string ReadAarNamespace(string path)
        {
            try
            {
                using (var zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read))
                {
                    var entry = zip.GetEntry("AndroidManifest.xml");
                    if (entry == null) return null;
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        var m = Regex.Match(reader.ReadToEnd(), "package\\s*=\\s*\"([^\"]+)\"");
                        return m.Success ? m.Groups[1].Value : null;
                    }
                }
            }
            catch { return null; }
        }

        static string Version(string package)
        {
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + package);
                return info != null ? info.version : "not installed";
            }
            catch { return "unknown"; }
        }

        static bool TypeExists(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => { try { return a.GetType(fullName, false) != null; } catch { return false; } });

        public struct Ver { public int major, minor, patch; }

        public static Ver ParseUnity(string v)
        {
            var m = Regex.Match(v ?? "", @"^(\d+)\.(\d+)\.(\d+)");
            return m.Success ? new Ver { major = int.Parse(m.Groups[1].Value), minor = int.Parse(m.Groups[2].Value), patch = int.Parse(m.Groups[3].Value) } : new Ver();
        }

        static Ver ParseSemver(string v) => ParseUnity(v);

        public static bool AtLeast(Ver v, int major, int minor, int patch) =>
            v.major != major ? v.major > major : v.minor != minor ? v.minor > minor : v.patch >= patch;

        class Report
        {
            public int ok, warn, bad;
            readonly StringBuilder sb = new StringBuilder("ROOM//ZERO compatibility report\n");
            public void Ok(string s) { ok++; sb.AppendLine("  OK    " + s); }
            public void Warn(string s) { warn++; sb.AppendLine("  WARN  " + s); }
            public void Bad(string s) { bad++; sb.AppendLine("  FIX   " + s); }
            public void Check(bool cond, string good, string badText) { if (cond) Ok(good); else Warn(badText); }
            public string Text() => sb.ToString();
        }
    }

    /// First-open auto-fix: if the project uses Input System-only handling but the package is
    /// missing (possible when a template-less project is opened in a newer Unity), install the
    /// version that matches this editor. Runs once per editor session.
    [InitializeOnLoad]
    static class RoomZeroAutoSetup
    {
        static RoomZeroAutoSetup()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            if (SessionState.GetBool("rz_input_checked", false)) return;
            SessionState.SetBool("rz_input_checked", true);
            EditorApplication.delayCall += () =>
            {
                bool hasInputSystem = AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => { try { return a.GetType("UnityEngine.InputSystem.Keyboard", false) != null; } catch { return false; } });
                if (hasInputSystem) return;
                Debug.Log("[RZ] Input System package missing for 'Input System' handling. Installing the version matched to this Unity...");
                UnityEditor.PackageManager.Client.Add("com.unity.inputsystem");
            };
#endif
        }
    }

    /// Runs before every Android build. Warns (doesn't block) so a false positive can't stop you.
    public class RoomZeroAndroidPreflight : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var unity = RoomZeroDoctor.ParseUnity(Application.unityVersion);
            if (!RoomZeroDoctor.AtLeast(unity, 6000, 4, 4)) return;
            var dups = RoomZeroDoctor.DuplicateAarNamespaces();
            if (dups.Count > 0)
                Debug.LogError("[RZ] This Android build will probably fail in Gradle (Unity 6000.4.4+ rejects duplicate AAR namespaces): " +
                               string.Join("; ", dups) + ". Fix: update all Meta XR packages to v207+ (Package Manager), or use Unity 6000.3/6000.0 LTS.");
        }
    }
}
#endif
