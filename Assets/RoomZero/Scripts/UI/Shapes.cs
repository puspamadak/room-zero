// Real curved geometry generated in code: lathe ("turned") meshes for glassware, bottles and pots,
// and curved two-sided leaf meshes. Plus a see-through glass material.
// This is what makes props read as test tubes / flasks / plants instead of cylinders and spheres.
// Owner: #3 / art. For photo-real results, drop artist models into the RoomZeroConfig slots.

using System.Collections.Generic;
using UnityEngine;

namespace RoomZero.UI
{
    public static class Shapes
    {
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static Material glass;
        static readonly Dictionary<Color, Material> tinted = new Dictionary<Color, Material>();

        // ------------------------------------------------------------------ materials

        /// Clear, glossy, see-through glass (works with Built-in Standard and URP Lit).
        public static Material Glass(Color tint)
        {
            if (tinted.TryGetValue(tint, out var cached) && cached) return cached;
            var m = new Material(UiFactory.LitMat(new Color(tint.r, tint.g, tint.b)));
            MakeTransparent(m, new Color(tint.r, tint.g, tint.b, tint.a));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.95f);
            tinted[tint] = m;
            return m;
        }

        public static Material ClearGlass => glass ? glass : (glass = Glass(new Color(0.85f, 0.95f, 1f, 0.22f)));

        public static void MakeTransparent(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            // Built-in Standard: "Fade" rendering mode
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2f);
            // URP Lit: transparent surface
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // see the far wall of the glass too
            m.SetShaderPassEnabled("DepthOnly", false);      // URP: glass must not hide what's behind it
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = 3000;
        }

        // ------------------------------------------------------------------ mesh builders

        /// Turn a 2D profile (x = radius, y = height, bottom to top) around the Y axis.
        /// Start/end the profile at radius 0 to close the shape; leave it open for glass rims.
        public static Mesh Lathe(string key, Vector2[] profile, int segments = 24)
        {
            if (meshCache.TryGetValue(key, out var cached) && cached) return cached;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int ring = segments + 1;
            for (int i = 0; i < profile.Length; i++)
                for (int j = 0; j <= segments; j++)
                {
                    float a = j * Mathf.PI * 2f / segments;
                    verts.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                    uvs.Add(new Vector2((float)j / segments, (float)i / (profile.Length - 1)));
                }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = i * ring + j, b = (i + 1) * ring + j, c = i * ring + j + 1, d = (i + 1) * ring + j + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        /// A curved, pointed leaf, visible from both sides. Length along +Z.
        public static Mesh Leaf(float length = 1f, float width = 0.42f, float droop = 0.35f)
        {
            string key = $"leaf_{length}_{width}_{droop}";
            if (meshCache.TryGetValue(key, out var cached) && cached) return cached;
            const int rows = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= rows; i++)
            {
                float t = (float)i / rows;
                float half = width * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f));   // pointed tip, rounded body
                float y = -droop * t * t;                                                // bends down towards the tip
                float fold = half * 0.25f;                                               // slight V along the midrib
                verts.Add(new Vector3(-half, y + fold, t * length));
                verts.Add(new Vector3(0, y, t * length));
                verts.Add(new Vector3(half, y + fold, t * length));
            }
            int n = verts.Count;
            for (int i = 0; i < rows; i++)
                for (int k = 0; k < 2; k++)
                {
                    int a = i * 3 + k, b = a + 1, c = a + 3, d = a + 4;
                    tris.AddRange(new[] { a, c, b, b, c, d });          // top side
                }
            // Back side: same vertices duplicated with reversed winding.
            for (int i = 0; i < n; i++) verts.Add(verts[i]);
            int front = tris.Count;
            for (int i = 0; i < front; i += 3) tris.AddRange(new[] { tris[i] + n, tris[i + 2] + n, tris[i + 1] + n });
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        /// Box with softly rounded edges and corners, built at its real size (so the rounding
        /// stays round however the box is proportioned). Real furniture never has razor edges.
        public static Mesh RoundedBox(Vector3 size, float radius)
        {
            Vector3 h = size * 0.5f;
            float r = Mathf.Max(0.0005f, Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f));
            string key = $"rbox_{size.x:F4}_{size.y:F4}_{size.z:F4}_{r:F4}";
            if (meshCache.TryGetValue(key, out var cached) && cached) return cached;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3 inner = h - Vector3.one * r;
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                    float[] us = Steps(h[ua], r), vs = Steps(h[va], r);
                    int start = verts.Count;
                    for (int i = 0; i < us.Length; i++)
                        for (int j = 0; j < vs.Length; j++)
                        {
                            var p = Vector3.zero;
                            p[axis] = sign * h[axis]; p[ua] = us[i]; p[va] = vs[j];
                            var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            var n = (p - c).normalized;
                            verts.Add(c + n * r);
                            norms.Add(n);
                            uvs.Add(new Vector2((us[i] + h[ua]) / size[ua], (vs[j] + h[va]) / size[va]));
                        }
                    for (int i = 0; i < us.Length - 1; i++)
                        for (int j = 0; j < vs.Length - 1; j++)
                        {
                            int a = start + i * vs.Length + j, b = a + 1, c = a + vs.Length, d = c + 1;
                            AddQuad(verts, norms, tris, a, b, c, d);
                        }
                }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        static float[] Steps(float half, float r)
        {
            // Dense near the edges (the curve), one big span across the flat middle.
            var s = new List<float>();
            for (int k = 0; k <= 3; k++) s.Add(-half + r * (1f - Mathf.Cos(k * Mathf.PI / 6f)));
            for (int k = 3; k >= 0; k--) s.Add(half - r * (1f - Mathf.Cos(k * Mathf.PI / 6f)));
            return s.ToArray();
        }

        static void AddQuad(List<Vector3> v, List<Vector3> n, List<int> t, int a, int b, int c, int d)
        {
            // Pick the winding that faces outward.
            var avgN = n[a] + n[b] + n[c] + n[d];
            bool flip = Vector3.Dot(Vector3.Cross(v[c] - v[a], v[b] - v[a]), avgN) < 0;
            if (!flip) t.AddRange(new[] { a, c, b, b, c, d });
            else t.AddRange(new[] { a, b, c, b, d, c });
        }

        /// A rounded-edge box GameObject at its true size (scale stays 1).
        public static GameObject RoundCube(string name, Transform parent, Vector3 localPos, Vector3 size, Color color, float radius = -1f, bool lit = true, float smoothness = -1f, float metallic = 0f)
        {
            if (radius < 0) radius = Mathf.Min(0.012f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.3f);
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = RoundedBox(size, radius);
            go.AddComponent<MeshRenderer>().sharedMaterial = smoothness >= 0 || metallic > 0 ? Surface(color, smoothness < 0 ? 0.3f : smoothness, metallic) : (lit ? UiFactory.LitMat(color) : UiFactory.Mat(color));
            return go;
        }

        static readonly Dictionary<string, Material> surfaces = new Dictionary<string, Material>();

        /// Lit material with chosen gloss and metalness: brushed steel, glossy plastic, epoxy tops.
        public static Material Surface(Color c, float smoothness, float metallic)
        {
            string key = $"{c}_{smoothness:F2}_{metallic:F2}";
            if (surfaces.TryGetValue(key, out var m) && m) return m;
            m = new Material(UiFactory.LitMat(c));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            surfaces[key] = m;
            return m;
        }

        /// Lit material with a painted texture (brushed steel, epoxy, wall paint...).
        public static Material Textured(string key, Color c, Texture tex, float smoothness, float metallic, Vector2 tiling)
        {
            if (surfaces.TryGetValue(key, out var m) && m) return m;
            m = new Material(Surface(c, smoothness, metallic));
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            surfaces[key] = m;
            return m;
        }

        public static Material Steel => Textured("steel", new Color(0.8f, 0.83f, 0.85f), LabTextures.BrushedSteel, 0.68f, 0.85f, Vector2.one);
        public static Material DarkSteel => Textured("darksteel", new Color(0.5f, 0.53f, 0.56f), LabTextures.BrushedSteel, 0.6f, 0.85f, Vector2.one);
        public static Material EpoxyTop => Textured("epoxy", Color.white, LabTextures.Epoxy, 0.78f, 0f, Vector2.one);
        public static Material WallPaint => Textured("wall", new Color(0.86f, 0.89f, 0.9f), LabTextures.Wall, 0.15f, 0f, new Vector2(3, 2));
        public static Material CeilingTile => Textured("ceiling", Color.white, LabTextures.Ceiling, 0.1f, 0f, new Vector2(8, 10));
        public static Material LabelMat => Textured("label", Color.white, LabTextures.Label, 0.35f, 0f, Vector2.one);

        /// Glowing dashboard for the lab monitor (unlit so it reads like a lit screen).
        public static Material ScreenMat
        {
            get
            {
                if (surfaces.TryGetValue("screen", out var m) && m) return m;
                m = new Material(UiFactory.Mat(Color.white));
                m.mainTexture = LabTextures.Screen;
                m.mainTextureScale = new Vector2(-1, 1);       // the screen faces back towards the player
                m.mainTextureOffset = new Vector2(1, 0);
                surfaces["screen"] = m;
                return m;
            }
        }

        /// The same dashboard for a screen whose front faces the player directly (wall screens).
        public static Material ScreenMatFront
        {
            get
            {
                if (surfaces.TryGetValue("screenfront", out var m) && m) return m;
                m = new Material(UiFactory.Mat(Color.white));
                m.mainTexture = LabTextures.Screen;
                surfaces["screenfront"] = m;
                return m;
            }
        }

        /// Coloured liquid with a faint inner glow (reads as "lit through the glass").
        public static Material Liquid(Color c)
        {
            string key = "liquid_" + c;
            if (surfaces.TryGetValue(key, out var m) && m) return m;
            m = new Material(Surface(c, 0.85f, 0f));
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.35f);
            }
            surfaces[key] = m;
            return m;
        }

        // ---- soft contact shadows ------------------------------------------------------

        static Mesh quad;
        static Material shadowMat;

        static Mesh FlatQuad()
        {
            if (quad) return quad;
            quad = new Mesh { name = "flatquad" };
            quad.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) });
            quad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            quad.SetTriangles(new List<int> { 0, 2, 1, 1, 2, 3 }, 0);
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }

        /// A soft dark patch under an object so it sits ON the surface instead of floating.
        public static GameObject ContactShadow(Transform parent, Vector3 surfacePoint, float width, float depth, Quaternion rot, float strength = 0.45f)
        {
            if (!shadowMat)
            {
                shadowMat = new Material(UiFactory.LitMat(Color.black));
                MakeTransparent(shadowMat, new Color(0, 0, 0, 1f));
                shadowMat.mainTexture = LabTextures.Blob;
                if (shadowMat.HasProperty("_Smoothness")) shadowMat.SetFloat("_Smoothness", 0f);
                if (shadowMat.HasProperty("_Glossiness")) shadowMat.SetFloat("_Glossiness", 0f);
                shadowMat.renderQueue = 2990;   // before glass, after opaque
            }
            var go = Make("ContactShadow", FlatQuad(), shadowMat, parent, surfacePoint + Vector3.up * 0.0015f, rot);
            go.transform.localScale = new Vector3(width, 1f, depth);
            NoShadow(go);
            return go;
        }
        public static Material GlossWhite => Surface(new Color(0.94f, 0.95f, 0.96f), 0.7f, 0f);
        public static Material BlackPlastic => Surface(new Color(0.08f, 0.09f, 0.1f), 0.55f, 0f);

        /// Turn off shadows on see-through things so glass doesn't cast solid shadows.
        public static GameObject NoShadow(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Make(string name, Mesh mesh, Material mat, Transform parent, Vector3 worldPos, Quaternion? rot = null, float scale = 1f)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = rot ?? (parent ? parent.rotation : Quaternion.identity);   // default: line up with the parent
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------ profiles

        static Vector2[] Arc(float r, float cy, float fromDeg, float toDeg, int steps)
        {
            var pts = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
                pts[i] = new Vector2(Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        static Vector2[] Join(params Vector2[][] parts)
        {
            var list = new List<Vector2>();
            foreach (var p in parts) list.AddRange(p);
            return list.ToArray();
        }

        /// Test tube: rounded bottom, straight wall, flared lip, open top. r = outer radius.
        public static Mesh TestTube(float r = 0.009f, float h = 0.11f) => Lathe($"tube_{r}_{h}",
            Join(Arc(r, r, -90, 0, 6), new[] { new Vector2(r, h), new Vector2(r * 1.2f, h + 0.002f), new Vector2(r * 1.15f, h + 0.004f) }));

        /// Liquid inside a test tube up to `fill` metres.
        public static Mesh TubeLiquid(float r = 0.009f, float fill = 0.05f) => Lathe($"tubeliq_{r}_{fill}",
            Join(Arc(r * 0.82f, r, -90, 0, 6), new[] { new Vector2(r * 0.82f, fill), new Vector2(0, fill) }));

        /// Solid rod / disc / knob with softened edges. Base at y=0.
        public static Mesh Rod(float r, float h, int seg = 20)
        {
            float b = Mathf.Min(r, h) * 0.18f;
            return Lathe($"rod_{r}_{h}_{seg}", new[]
            {
                new Vector2(0, 0), new Vector2(r - b, 0), new Vector2(r, b), new Vector2(r, h - b), new Vector2(r - b, h), new Vector2(0, h)
            }, seg);
        }

        /// Dome (centrifuge lid, bell jar): hemisphere-ish cap, open at the bottom.
        public static Mesh Dome(float r, float h)
        {
            var pts = Arc(1f, 0f, 0, 90, 10);
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(pts[i].x * r, pts[i].y * h);
            return Lathe($"dome_{r}_{h}", pts);
        }

        /// Lamp shade / funnel: open cone, wide end down.
        public static Mesh Shade(float rTop, float rBottom, float h) => Lathe($"shade_{rTop}_{rBottom}_{h}", new[]
        {
            new Vector2(rBottom, 0), new Vector2(rBottom * 0.98f, h * 0.1f), new Vector2(rTop, h), new Vector2(0, h)
        });

        public static Mesh Erlenmeyer() => Lathe("erlen", new[]
        {
            new Vector2(0, 0), new Vector2(0.044f, 0), new Vector2(0.047f, 0.006f), new Vector2(0.03f, 0.06f),
            new Vector2(0.014f, 0.1f), new Vector2(0.012f, 0.108f), new Vector2(0.012f, 0.14f), new Vector2(0.016f, 0.142f), new Vector2(0.015f, 0.145f)
        });

        public static Mesh ErlenmeyerLiquid() => Lathe("erlenliq", new[]
        {
            new Vector2(0, 0.004f), new Vector2(0.042f, 0.004f), new Vector2(0.044f, 0.008f), new Vector2(0.034f, 0.045f), new Vector2(0, 0.045f)
        });

        public static Mesh RoundFlask() => Lathe("roundflask", Join(Arc(0.04f, 0.04f, -90, 72, 12),
            new[] { new Vector2(0.011f, 0.085f), new Vector2(0.011f, 0.13f), new Vector2(0.014f, 0.132f), new Vector2(0.013f, 0.135f) }));

        public static Mesh RoundFlaskLiquid() => Lathe("roundflaskliq", Join(Arc(0.037f, 0.04f, -90, 10, 10), new[] { new Vector2(0, 0.046f) }));

        public static Mesh Beaker() => Lathe("beaker", new[]
        {
            new Vector2(0, 0), new Vector2(0.038f, 0), new Vector2(0.04f, 0.004f), new Vector2(0.04f, 0.095f), new Vector2(0.044f, 0.098f), new Vector2(0.043f, 0.1f)
        });

        public static Mesh BeakerLiquid() => Lathe("beakerliq", new[]
        {
            new Vector2(0, 0.003f), new Vector2(0.037f, 0.003f), new Vector2(0.038f, 0.006f), new Vector2(0.038f, 0.05f), new Vector2(0, 0.05f)
        });

        /// Reagent bottle (closed): body, shoulder, neck.
        public static Mesh Bottle() => Lathe("bottle", new[]
        {
            new Vector2(0, 0), new Vector2(0.021f, 0), new Vector2(0.023f, 0.004f), new Vector2(0.023f, 0.06f),
            new Vector2(0.018f, 0.074f), new Vector2(0.009f, 0.08f), new Vector2(0.009f, 0.09f), new Vector2(0, 0.09f)
        });

        public static Mesh Cap() => Lathe("cap", new[]
        {
            new Vector2(0, 0), new Vector2(0.011f, 0), new Vector2(0.011f, 0.014f), new Vector2(0.009f, 0.017f), new Vector2(0, 0.017f)
        }, 16);

        /// Tapered plant pot with a rim (closed).
        public static Mesh Pot(float r = 0.06f, float h = 0.11f) => Lathe($"pot_{r}_{h}", new[]
        {
            new Vector2(0, 0), new Vector2(r * 0.72f, 0), new Vector2(r, h * 0.85f), new Vector2(r * 1.1f, h * 0.88f),
            new Vector2(r * 1.1f, h), new Vector2(r * 0.95f, h), new Vector2(0, h * 0.92f)
        });

        /// Glass vial with a rounded shoulder (formula fragments).
        public static Mesh Vial() => Lathe("vial", Join(Arc(0.016f, 0.016f, -90, 0, 5),
            new[] { new Vector2(0.016f, 0.06f), new Vector2(0.011f, 0.068f), new Vector2(0.008f, 0.072f), new Vector2(0.008f, 0.08f) }));

        public static Mesh VialLiquid() => Lathe("vialliq", Join(Arc(0.0135f, 0.016f, -90, 0, 5), new[] { new Vector2(0.0135f, 0.05f), new Vector2(0, 0.05f) }));

        // ------------------------------------------------------------------ assembled props

        /// A potted plant with curved leaves. Returns the root.
        public static Transform PottedPlant(Transform parent, Vector3 basePos, float size, int seed, Color potColor)
        {
            var root = new GameObject("PottedPlant").transform;
            root.SetParent(parent, false);
            root.position = basePos;
            float potR = 0.06f * size, potH = 0.11f * size;
            ContactShadow(root, basePos, potR * 3f, potR * 3f, Quaternion.identity, 0.4f);
            Make("Pot", Pot(potR, potH), Surface(potColor, 0.35f, 0f), root, basePos);
            Make("Soil", Lathe("soil", new[] { new Vector2(0, 0), new Vector2(1f, 0), new Vector2(0, 0.06f) }), UiFactory.LitMat(Palette.Soil), root, basePos + Vector3.up * potH * 0.9f, null, potR * 0.95f);
            Foliage(root, basePos + Vector3.up * potH * 0.92f, size, seed);
            return root;
        }

        /// Leaves radiating from a point, in two or three layers, with natural variation.
        public static void Foliage(Transform parent, Vector3 basePos, float size, int seed)
        {
            var rng = new System.Random(seed * 7919 + 17);
            int leaves = 7 + rng.Next(5);
            for (int i = 0; i < leaves; i++)
            {
                float yaw = i * (360f / leaves) + (float)rng.NextDouble() * 25f;
                float tilt = -45f - (float)rng.NextDouble() * 25f + (i % 3) * 12f;   // up-and-out
                float len = (0.11f + (float)rng.NextDouble() * 0.06f) * size;
                var mat = UiFactory.LitMat(i % 2 == 0 ? Palette.Leaf : Palette.LeafLight);
                var leaf = Make("Leaf", Leaf(1f, 0.4f, 0.35f), mat, parent, basePos + Vector3.up * (0.01f * (i % 3)) * size,
                                Quaternion.Euler(tilt, yaw, 0), len);
                leaf.transform.localScale = new Vector3(len, len, len);
            }
        }
    }
}
