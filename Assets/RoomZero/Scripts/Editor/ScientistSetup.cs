// Imports Dr. Asha correctly and wires her into the config. Called by RoomZero > Build Demo Scene
// (and available on its own as RoomZero > Set Up Dr. Asha).
//   - model + animations imported as Legacy (materials keep their names; textures are applied at runtime) (they share the same Bip01 skeleton, so clips play directly)
//   - normal maps flagged as normal maps, hair texture uses its alpha
//   - textures, model and clips assigned to RoomZeroConfig
// Character: Microsoft Rocketbox "Medical_Female_02" (MIT licence, see Characters/DrAsha/ROCKETBOX_LICENSE.md).
// Owner: #3 art / #1 tech lead.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RoomZero.EditorTools
{
    public static class ScientistSetup
    {
        const string Dir = "Assets/RoomZero/Characters/DrAsha";

        [MenuItem("RoomZero/Set Up Dr. Asha", priority = 2)]
        static void Menu()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<RoomZeroConfig>("Assets/RoomZero/RoomZeroConfig.asset");
            if (!cfg) { Debug.LogWarning("[RZ] Run RoomZero > Build Demo Scene first."); return; }
            Configure(cfg);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
        }

        public static void Configure(RoomZeroConfig cfg)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) { Debug.Log("[RZ] Dr. Asha model not found: using the built-in figure."); return; }

            cfg.scientistModel = Model($"{Dir}/Medical_Female_02.fbx");
            cfg.scientistIdle = Clip("f_idle_breathe_01");
            cfg.scientistWalk = Clip("f_walk_neutral_01");
            cfg.scientistTalk = Clip("f_gestic_talk_neutral_01");
            cfg.scientistWork = Clip("f_work_table");
            cfg.scientistListen = Clip("f_gestic_listen_neutral_01");
            cfg.scientistCheer = Clip("f_cheer_01");
            cfg.scientistWave = Clip("f_wave_01");

            cfg.scientistBody = Tex("f153_body_color.jpg");
            cfg.scientistHead = Tex("f153_head_color.jpg");
            cfg.scientistHair = Tex("f153_opacity_color.png", alpha: true);
            cfg.scientistExtra = Tex("f153_stetoskop_color.jpg");
            cfg.scientistBodyNormal = Tex("f153_body_normal.png", normal: true);
            cfg.scientistHeadNormal = Tex("f153_head_normal.png", normal: true);
            cfg.scientistExtraNormal = Tex("f153_stetoskop_normal.png", normal: true);
            Debug.Log(cfg.scientistModel && cfg.scientistWalk ? "[RZ] Dr. Asha is ready." : "[RZ] Dr. Asha: some files missing, check Characters/DrAsha.");
            SetupAudio(cfg);
        }

        const string AudioDir = "Assets/RoomZero/Audio";

        /// Voices (one clip per spoken line) and the three room music loops.
        public static void SetupAudio(RoomZeroConfig cfg)
        {
            var clips = new System.Collections.Generic.List<AudioClip>();
            if (AssetDatabase.IsValidFolder(AudioDir + "/Voice"))
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir + "/Voice" }))
                {
                    var c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (c) clips.Add(c);
                }
            cfg.voiceClips = clips.ToArray();
            cfg.musicVaultLab = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/Music/music_vault_lab.ogg");
            cfg.musicGreenhouse = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/Music/music_greenhouse.ogg");
            cfg.musicGeneLab = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/Music/music_gene_lab.ogg");
            Debug.Log($"[RZ] Audio: {clips.Count} voice lines, music {(cfg.musicVaultLab && cfg.musicGreenhouse && cfg.musicGeneLab ? "ready" : "missing")}.");
        }

        static GameObject Model(string path)
        {
            LegacyImport(path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static AnimationClip Clip(string name)
        {
            string path = $"{Dir}/Animations/{name}.fbx";
            LegacyImport(path);
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        static void LegacyImport(string path)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!mi) return;
            bool changed = false;
            if (mi.animationType != ModelImporterAnimationType.Legacy) { mi.animationType = ModelImporterAnimationType.Legacy; changed = true; }
            if (!mi.importAnimation) { mi.importAnimation = true; changed = true; }
            // Keep the "Bip01" root node in every file so the clips' bone paths match the model's.
            if (!mi.preserveHierarchy) { mi.preserveHierarchy = true; changed = true; }
            if (changed) mi.SaveAndReimport();
        }

        static Texture2D Tex(string file, bool normal = false, bool alpha = false)
        {
            string path = $"{Dir}/Textures/{file}";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti)
            {
                bool changed = false;
                // Always flat 2D images (Unity sometimes guesses "Cube" for square images, which would leave her white).
                if (ti.textureShape != TextureImporterShape.Texture2D) { ti.textureShape = TextureImporterShape.Texture2D; changed = true; }
                var wantType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != wantType) { ti.textureType = wantType; changed = true; }
                if (!normal && !ti.sRGBTexture) { ti.sRGBTexture = true; changed = true; }
                if (alpha && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; changed = true; }
                if (changed) ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!tex) Debug.LogWarning($"[RZ] Dr. Asha texture not found or not a 2D image: {path}");
            return tex;
        }
    }
}
#endif
