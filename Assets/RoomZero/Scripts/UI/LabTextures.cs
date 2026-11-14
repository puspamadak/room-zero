// Surface detail painted in code (no downloaded art): brushed steel, speckled epoxy worktops,
// matte wall paint, reagent-bottle labels, a lab-monitor dashboard, and soft contact shadows.
// Each texture is generated once and shared.
// Owner: #3 / art.

using UnityEngine;

namespace RoomZero.UI
{
    public static class LabTextures
    {
        static Texture2D brushed, epoxy, wall, label, screen, blob, ceiling;

        static Texture2D New(int w, int h, bool repeat = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            return t;
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// Fine horizontal grain, like satin stainless steel.
        public static Texture2D BrushedSteel
        {
            get
            {
                if (brushed) return brushed;
                const int n = 256;
                brushed = New(n, n);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                {
                    float row = Hash(0, y) * 0.06f + Mathf.PerlinNoise(0.5f, y * 0.08f) * 0.05f;
                    for (int x = 0; x < n; x++)
                    {
                        float streak = Mathf.PerlinNoise(x * 0.012f + y * 3.1f, y * 0.9f) * 0.08f;
                        float v = 0.84f + row + streak - 0.06f;
                        px[y * n + x] = new Color(v * 0.98f, v, v * 1.02f, 1f);
                    }
                }
                brushed.SetPixels(px);
                brushed.Apply(true);
                return brushed;
            }
        }

        /// Dark epoxy resin worktop with a fine mineral speckle.
        public static Texture2D Epoxy
        {
            get
            {
                if (epoxy) return epoxy;
                const int n = 256;
                epoxy = New(n, n);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cloud = Mathf.PerlinNoise(x * 0.03f, y * 0.03f) * 0.06f;
                        float speck = Hash(x, y) > 0.985f ? 0.25f : 0f;
                        float v = 0.22f + cloud + speck;
                        px[y * n + x] = new Color(v, v * 1.03f, v * 1.08f, 1f);
                    }
                epoxy.SetPixels(px);
                epoxy.Apply(true);
                return epoxy;
            }
        }

        /// Matte wall paint with a faint roller texture.
        public static Texture2D Wall
        {
            get
            {
                if (wall) return wall;
                const int n = 128;
                wall = New(n, n);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float v = 0.93f + Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.05f + (Hash(x, y) - 0.5f) * 0.025f;
                        px[y * n + x] = new Color(v, v, v * 1.01f, 1f);
                    }
                wall.SetPixels(px);
                wall.Apply(true);
                return wall;
            }
        }

        /// Ceiling tile with a soft grid.
        public static Texture2D Ceiling
        {
            get
            {
                if (ceiling) return ceiling;
                const int n = 64;
                ceiling = New(n, n);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        bool grid = x < 2 || y < 2;
                        float v = grid ? 0.75f : 0.95f - Hash(x, y) * 0.04f;
                        px[y * n + x] = new Color(v, v, v, 1f);
                    }
                ceiling.SetPixels(px);
                ceiling.Apply(true);
                return ceiling;
            }
        }

        /// Reagent label: white paper, coloured band, printed "text" lines and a hazard diamond.
        public static Texture2D Label
        {
            get
            {
                if (label) return label;
                const int w = 64, h = 64;
                label = New(w, h, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color c = new Color(0.97f, 0.97f, 0.94f);
                        if (y > 50) c = new Color(0.15f, 0.55f, 0.75f);                                   // header band
                        else if ((y == 42 || y == 41) && x > 6 && x < 50) c = new Color(0.15f, 0.15f, 0.18f); // name
                        else if ((y == 34 || y == 28 || y == 22) && x > 6 && x < 30 + (y % 3) * 6) c = new Color(0.45f, 0.45f, 0.48f);
                        int dx = Mathf.Abs(x - 50), dy = Mathf.Abs(y - 20);
                        if (dx + dy < 9) c = dx + dy > 6 ? new Color(0.85f, 0.15f, 0.1f) : Color.white; // hazard diamond
                        if (x < 2 || x > w - 3 || y < 2) c *= 0.9f;
                        c.a = 1f;
                        px[y * w + x] = c;
                    }
                label.SetPixels(px);
                label.Apply(true);
                return label;
            }
        }

        /// Lab monitor dashboard: header, growth curve on a grid, yield bars, and a DNA helix.
        public static Texture2D Screen
        {
            get
            {
                if (screen) return screen;
                const int w = 512, h = 300;
                screen = New(w, h, false);
                var px = new Color[w * h];
                Color bg = new Color(0.03f, 0.1f, 0.12f), gridC = new Color(0.07f, 0.2f, 0.22f);
                Color mint = new Color(0.45f, 1f, 0.82f), leaf = new Color(0.45f, 1f, 0.5f), amber = new Color(1f, 0.8f, 0.35f), pink = new Color(1f, 0.45f, 0.7f);
                for (int i = 0; i < px.Length; i++) px[i] = bg;
                System.Action<int, int, Color> Dot = (x, y, c) => { if (x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = new Color(c.r, c.g, c.b, 1f); };
                System.Action<int, int, int, int, Color> Rect = (x0, y0, x1, y1, c) => { for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Dot(x, y, c); };

                Rect(0, h - 28, w, h, new Color(0.08f, 0.32f, 0.3f));                      // header bar
                for (int i = 0; i < 6; i++) Rect(14 + i * 22, h - 18, 30 + i * 22, h - 10, i == 0 ? leaf : new Color(0.6f, 0.85f, 0.8f));
                Rect(w - 60, h - 20, w - 50, h - 10, leaf); Rect(w - 44, h - 20, w - 34, h - 10, amber);

                // Panel 1: growth curve
                int gx0 = 16, gy0 = 120, gx1 = 300, gy1 = 258;
                for (int x = gx0; x <= gx1; x += 24) Rect(x, gy0, x + 1, gy1, gridC);
                for (int y = gy0; y <= gy1; y += 23) Rect(gx0, y, gx1, y + 1, gridC);
                for (int x = gx0; x < gx1; x++)
                {
                    float t = (x - gx0) / (float)(gx1 - gx0);
                    float a = 1f / (1f + Mathf.Exp(-10f * (t - 0.45f)));                         // logistic growth
                    float b = 0.75f / (1f + Mathf.Exp(-8f * (t - 0.6f)));
                    int ya = gy0 + 6 + (int)(a * (gy1 - gy0 - 14)), yb = gy0 + 6 + (int)(b * (gy1 - gy0 - 14));
                    Rect(x, ya - 1, x + 1, ya + 2, leaf);
                    Rect(x, yb - 1, x + 1, yb + 1, amber);
                }
                // Panel 2: yield bars
                for (int i = 0; i < 10; i++)
                {
                    int bh = 18 + (int)(i * 8 + Mathf.Sin(i * 1.7f) * 10);
                    Rect(18 + i * 28, 16, 36 + i * 28, 16 + bh, Color.Lerp(mint, leaf, i / 9f));
                }
                // Panel 3: DNA double helix
                int cx = 410;
                for (int y = 20; y < 250; y++)
                {
                    float p = y * 0.075f;
                    int xa = cx + (int)(Mathf.Sin(p) * 40), xb = cx + (int)(Mathf.Sin(p + Mathf.PI) * 40);
                    Rect(xa - 2, y, xa + 2, y + 1, Mathf.Cos(p) > 0 ? mint : mint * 0.6f);
                    Rect(xb - 2, y, xb + 2, y + 1, Mathf.Cos(p + Mathf.PI) > 0 ? pink : pink * 0.6f);
                    if (y % 12 == 0) Rect(Mathf.Min(xa, xb), y, Mathf.Max(xa, xb), y + 2, new Color(0.5f, 0.6f, 0.65f));
                }
                Rect(340, 14, 480, 15, gridC); Rect(340, 254, 480, 255, gridC);
                screen.SetPixels(px);
                screen.Apply(true);
                return screen;
            }
        }

        static Texture2D hand;

        /// Glowing hand outline on a dark glass pad (biometric palm scanner).
        public static Texture2D HandPrint
        {
            get
            {
                if (hand) return hand;
                const int n = 256;
                hand = New(n, n, false);
                var px = new Color[n * n];
                Color bg = new Color(0.02f, 0.08f, 0.12f), line = new Color(0.35f, 0.95f, 1f), grid = new Color(0.05f, 0.18f, 0.24f);
                // Palm (ellipse) + five fingers (capsules), in 0..1 coordinates, wrist at the bottom.
                Vector2[] a = { new Vector2(0.26f, 0.5f), new Vector2(0.4f, 0.6f), new Vector2(0.52f, 0.62f), new Vector2(0.63f, 0.58f), new Vector2(0.72f, 0.5f) };
                Vector2[] b = { new Vector2(0.12f, 0.66f), new Vector2(0.36f, 0.9f), new Vector2(0.52f, 0.94f), new Vector2(0.67f, 0.88f), new Vector2(0.82f, 0.74f) };
                float[] r = { 0.055f, 0.05f, 0.052f, 0.048f, 0.042f };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                        float dx = (p.x - 0.5f) / 0.22f, dy = (p.y - 0.36f) / 0.24f;
                        float d = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * 0.22f;            // ~signed distance to the palm edge
                        for (int f = 0; f < 5; f++) d = Mathf.Min(d, Capsule(p, a[f], b[f]) - r[f]);
                        Color c = (x % 16 == 0 || y % 16 == 0) ? grid : bg;
                        float edge = Mathf.Abs(d);
                        if (edge < 0.006f) c = line;
                        else if (edge < 0.016f) c = Color.Lerp(line, c, (edge - 0.006f) / 0.01f);
                        else if (d < 0f && (y % 6 == 0)) c = Color.Lerp(c, line, 0.18f);   // faint ridges inside the hand
                        c.a = 1f;
                        px[y * n + x] = c;
                    }
                hand.SetPixels(px);
                hand.Apply(true);
                return hand;
            }
        }

        static float Capsule(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        /// Soft round shadow (alpha falls off from the centre): grounds objects on surfaces.
        public static Texture2D Blob
        {
            get
            {
                if (blob) return blob;
                const int n = 64;
                blob = New(n, n, false);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        px[y * n + x] = new Color(0, 0, 0, a * a * (3f - 2f * a));
                    }
                blob.SetPixels(px);
                blob.Apply(true);
                return blob;
            }
        }
    }
}
