// Code-built holograms and diegetic UI. No prefabs or TextMeshPro needed, so the template
// works in any URP or Built-in project. Replace with real art/prefabs as the team grows.
// Owner: #3 Interaction + VR UX.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RoomZero.Input;

namespace RoomZero.UI
{
    /// Bio-lab greenhouse palette: mint holograms, leaf green, warm amber highlights, dark-teal panels.
    public static class Palette
    {
        public static readonly Color Holo = new Color(0.45f, 1f, 0.82f);      // mint hologram
        public static readonly Color Accent = new Color(1f, 0.8f, 0.35f);     // warm amber: "look here"
        public static readonly Color Danger = new Color(1f, 0.32f, 0.32f);
        public static readonly Color Success = new Color(0.45f, 1f, 0.5f);    // leaf green
        public static readonly Color Panel = new Color(0.04f, 0.12f, 0.11f);  // dark teal
        public static readonly Color Button = new Color(0.1f, 0.42f, 0.38f);
        public static readonly Color Muted = new Color(0.62f, 0.74f, 0.72f);
        // Lab materials
        public static readonly Color LabWhite = new Color(0.93f, 0.95f, 0.94f);
        public static readonly Color Steel = new Color(0.62f, 0.66f, 0.68f);
        public static readonly Color Mint = new Color(0.72f, 0.88f, 0.83f);
        public static readonly Color Leaf = new Color(0.25f, 0.62f, 0.3f);
        public static readonly Color LeafLight = new Color(0.45f, 0.78f, 0.38f);
        public static readonly Color Soil = new Color(0.32f, 0.23f, 0.17f);
    }

    public static class UiFactory
    {
        static Material baseMat, litMat;
        static Font font;
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> litCache = new Dictionary<Color, Material>();

        /// The active config (model slots etc.).
        public static RoomZeroConfig Config { get; private set; }

        public static void Init(RoomZeroConfig cfg)
        {
            Config = cfg;
            baseMat = cfg ? cfg.baseMaterial : null;
            litMat = cfg ? cfg.litMaterial : null;
            litCache.Clear();
            font = cfg ? cfg.font : null;
            if (!font)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
                if (!font) try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { }
            }
            cache.Clear();
        }

        public static Material Mat(Color c)
        {
            if (cache.TryGetValue(c, out var m) && m) return m;
            if (baseMat) m = new Material(baseMat);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (!sh) sh = Shader.Find("Unlit/Color");
                m = new Material(sh);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            cache[c] = m;
            return m;
        }

        /// Shaded (lit) material: solid objects get real light and shadow instead of flat colour.
        public static Material LitMat(Color c)
        {
            if (litCache.TryGetValue(c, out var m) && m) return m;
            if (litMat) m = new Material(litMat);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (!sh) sh = Shader.Find("Standard");
                if (!sh) return Mat(c);
                m = new Material(sh);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.3f);
            litCache[c] = m;
            return m;
        }

        /// Shaded material with a repeating texture (floor tiles etc.). Not cached: textures differ.
        public static Material LitTextured(Color c, Texture tex, Vector2 tiling)
        {
            var m = new Material(LitMat(c));
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            return m;
        }

        /// If a 3D model is assigned for this slot, show it in place of `primitive` (fitted to `size`
        /// metres, centred on the primitive). The primitive keeps its collider, so touching still works.
        public static GameObject ApplyModel(GameObject modelPrefab, GameObject primitive, float size)
        {
            if (!modelPrefab || !primitive) return null;
            var r = primitive.GetComponent<Renderer>();
            if (r) r.enabled = false;
            foreach (var childRenderer in primitive.GetComponentsInChildren<Renderer>(true))
                if (childRenderer != r) childRenderer.enabled = false;

            var anchor = new GameObject($"{primitive.name}_Model").transform;
            anchor.SetParent(primitive.transform.parent, false);
            anchor.localPosition = primitive.transform.localPosition;
            anchor.localRotation = Quaternion.identity;

            var model = UnityEngine.Object.Instantiate(modelPrefab, anchor, false);
            foreach (var col in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(col);

            // Fit the model's largest dimension to `size`, centred on the anchor.
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var rr in renderers) b.Encapsulate(rr.bounds);
                float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (largest > 0.0001f) model.transform.localScale *= size / largest;
                b = renderers[0].bounds;
                foreach (var rr in renderers) b.Encapsulate(rr.bounds);
                model.transform.position += anchor.position - b.center;
            }
            return model;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool keepCollider = false, bool lit = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = lit ? LitMat(color) : Mat(color);
            if (!keepCollider) UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static TextMesh Text(Transform parent, string text, Vector3 localPos, float height = 0.018f, Color? color = null, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.fontSize = 128;                 // high-res glyphs, scaled down: much crisper
            tm.characterSize = height / 12.8f; // ~height metres per line
            tm.color = color ?? Color.white;
            if (font)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return tm;
        }

        /// Text with a dark backing plate (always readable over any background), optionally turning
        /// to face the camera so nothing in the scene hides it.
        public static TextMesh BackedText(Transform parent, string text, Vector3 localPos, float height, Color color, bool faceCamera = true)
        {
            var tm = Text(parent, text, localPos, height, color);
            var backed = tm.gameObject.AddComponent<BackedLabel>();
            backed.lineHeight = height;
            if (faceCamera) tm.gameObject.AddComponent<FaceCamera>();
            return tm;
        }

        /// Pressable 3D button. Fingertips hit the front face; mouse/voice call Press().
        public static PokeTarget Button(Transform parent, string id, string label, Vector3 localPos, Vector2 size, Color color, Action onPress)
        {
            var go = Primitive(PrimitiveType.Cube, $"Button_{id}", parent, localPos, new Vector3(size.x, size.y, 0.02f), color, keepCollider: true);
            var poke = go.AddComponent<PokeTarget>();
            poke.targetId = id;
            poke.Pressed += _ => onPress?.Invoke();

            // Label sits on a sibling so it is not squashed by the cube's scale.
            var labelRoot = new GameObject($"Label_{id}").transform;
            labelRoot.SetParent(parent, false);
            labelRoot.localPosition = localPos + new Vector3(0, 0, -0.012f);
            Text(labelRoot, label, Vector3.zero, Mathf.Min(0.016f, size.y * 0.45f));
            labelRoot.SetParent(go.transform, true);
            return poke;
        }

        public static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            foreach (var paragraph in text.Split('\n'))
            {
                int line = 0;
                foreach (var word in paragraph.Split(' '))
                {
                    if (line > 0 && line + word.Length + 1 > maxChars) { sb.Append('\n'); line = 0; }
                    else if (line > 0) { sb.Append(' '); line++; }
                    sb.Append(word);
                    line += word.Length;
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }
    }

    /// Keeps a panel comfortably in view without glueing it to the head.
    public class LazyFollow : MonoBehaviour
    {
        public Transform head;
        public float distance = 0.6f;
        public float verticalOffset = -0.12f;
        public float horizontalOffset = 0f;
        public float maxAngle = 28f;
        public float speed = 4f;

        bool moving;

        public void SnapNow()
        {
            if (!head) return;
            transform.position = Target();
            Face();
        }

        void LateUpdate()
        {
            if (!head) return;
            Vector3 target = Target();
            Vector3 toPanel = transform.position - head.position;
            float angle = Vector3.Angle(Flat(head.forward), Flat(toPanel));
            if (angle > maxAngle || Mathf.Abs(toPanel.magnitude - distance) > 0.25f) moving = true;
            if (moving)
            {
                transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
                if ((transform.position - target).sqrMagnitude < 0.0004f) moving = false;
            }
            Face();
        }

        Vector3 Target()
        {
            Vector3 fwd = Flat(head.forward);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            return head.position + fwd * distance + Vector3.up * verticalOffset + right * horizontalOffset;
        }

        void Face()
        {
            Vector3 dir = transform.position - head.position;
            dir.y *= 0.5f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }
    }

    /// Keeps a dark plate sized to the text behind it; hidden when the text is empty.
    public class BackedLabel : MonoBehaviour
    {
        public float lineHeight = 0.014f;
        TextMesh tm;
        Transform plate;
        string last;

        void Awake()
        {
            tm = GetComponent<TextMesh>();
            plate = UiFactory.Primitive(PrimitiveType.Cube, "Backing", transform, new Vector3(0, 0, 0.003f), Vector3.one * 0.001f, new Color(0.02f, 0.04f, 0.07f)).transform;
        }

        void LateUpdate()
        {
            if (!tm || tm.text == last) return;
            last = tm.text;
            bool empty = string.IsNullOrEmpty(last);
            plate.gameObject.SetActive(!empty);
            if (empty) return;
            var lines = last.Split('\n');
            int longest = 0;
            foreach (var l in lines) longest = Mathf.Max(longest, l.Length);
            float w = longest * lineHeight * 0.58f + lineHeight * 0.9f;
            float h = lines.Length * lineHeight * 1.2f + lineHeight * 0.5f;
            plate.localScale = new Vector3(w, h, 0.002f);
        }
    }

    /// Turns an object so its text is readable from the main camera (desktop) or the headset.
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            Vector3 dir = transform.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
