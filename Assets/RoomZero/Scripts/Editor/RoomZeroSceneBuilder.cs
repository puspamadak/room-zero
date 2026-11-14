// Menu: RoomZero > Build Demo Scene
// Creates (or refreshes) the config asset, a hologram material (so the shader ships in
// Android builds), and Assets/RoomZero/Scenes/RoomZero_Main.unity, then adds the scene
// to Build Settings. Safe to run again at any time.
// Owner: #1 Tech Lead.

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RoomZero.EditorTools
{
    public static class RoomZeroSceneBuilder
    {
        const string Root = "Assets/RoomZero";
        const string ScenePath = Root + "/Scenes/RoomZero_Main.unity";
        const string ConfigPath = Root + "/RoomZeroConfig.asset";
        const string MaterialPath = Root + "/Materials/RZ_Hologram.mat";
        const string LitMaterialPath = Root + "/Materials/RZ_Solid.mat";

        [MenuItem("RoomZero/Build Demo Scene", priority = 0)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Materials");

            var mat = EnsureMaterial();
            var cfg = EnsureConfig(mat);
            cfg.litMaterial = EnsureLitMaterial();
            ScientistSetup.Configure(cfg);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            c.nearClipPlane = 0.01f;
            cam.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(0, 1.2f, 0);

            var boot = new GameObject("ROOM_ZERO");
            var b = boot.AddComponent<RoomZeroBootstrap>();
            b.config = cfg;

            var note = new GameObject("_README: for Quest, replace Main Camera with Meta Camera Rig + Passthrough + MRUK building blocks");
            note.transform.SetParent(boot.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Selection.activeObject = cfg;
            Debug.Log($"[RZ] Built {ScenePath}. Press Play to run the desktop simulator (V = start mission).");
        }

        static Material EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var shader = urp ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
            if (!shader) shader = Shader.Find("Unlit/Color");
            if (!mat)
            {
                mat = new Material(shader) { name = "RZ_Hologram" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else if (mat.shader != shader) mat.shader = shader;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.25f, 0.9f, 1f));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.25f, 0.9f, 1f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// Shaded material for solid objects (furniture, vault, keypad, drone).
        static Material EnsureLitMaterial()
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (!mat)
            {
                mat = new Material(shader) { name = "RZ_Solid" };
                AssetDatabase.CreateAsset(mat, LitMaterialPath);
            }
            else if (mat.shader != shader) mat.shader = shader;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static RoomZeroConfig EnsureConfig(Material mat)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<RoomZeroConfig>(ConfigPath);
            if (!cfg)
            {
                cfg = ScriptableObject.CreateInstance<RoomZeroConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
            }
            cfg.baseMaterial = mat;
            if (!cfg.font)
            {
                cfg.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (!cfg.font) cfg.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("RoomZero/Open Profile Folder", priority = 30)]
        static void OpenProfileFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath + "/");

        [MenuItem("RoomZero/Delete Saved Profile (Editor)", priority = 31)]
        static void DeleteProfile()
        {
            var p = Path.Combine(Application.persistentDataPath, Memory.MemoryStore.FileName);
            if (File.Exists(p)) File.Delete(p);
            Debug.Log($"[RZ] Deleted {p}");
        }
    }
}
#endif
