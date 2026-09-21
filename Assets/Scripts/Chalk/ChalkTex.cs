using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// Procedurally generated chalk textures, so all art can be made from code in the same style.
    public static class ChalkTex
    {
        static Texture2D _stroke, _board;
        static Font _font;

        public static readonly Color White = new Color(0.93f, 0.93f, 0.89f, 0.95f);
        public static readonly Color Dim = new Color(0.93f, 0.93f, 0.89f, 0.35f);
        public static readonly Color Yellow = new Color(1f, 0.84f, 0.32f, 1f);
        public static readonly Color Cyan = new Color(0.45f, 0.86f, 1f, 1f);
        public static readonly Color Red = new Color(1f, 0.45f, 0.45f, 1f);
        public static readonly Color Green = new Color(0.55f, 1f, 0.6f, 1f);
        public static readonly Color Pink = new Color(1f, 0.6f, 0.85f, 1f);
        public static readonly Color BoardColor = new Color(0.105f, 0.14f, 0.125f, 1f);

        /// Horizontal strip: ragged edges + grain. Repeats along u.
        public static Texture2D Stroke
        {
            get
            {
                if (_stroke != null) return _stroke;
                const int w = 256, h = 32;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "ChalkStroke" };
                var rnd = new System.Random(7);
                var px = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    // ragged edge per column (tileable noise via sin sums)
                    float u = x / (float)w * Mathf.PI * 2f;
                    float edgeTop = 0.82f + 0.08f * Mathf.Sin(u * 3 + 1.3f) + 0.06f * Mathf.Sin(u * 11 + 0.4f) + 0.04f * Mathf.Sin(u * 29);
                    float edgeBot = 0.18f + 0.08f * Mathf.Sin(u * 5 + 2.1f) + 0.05f * Mathf.Sin(u * 13 + 1.1f) + 0.04f * Mathf.Sin(u * 31 + 0.7f);
                    for (int y = 0; y < h; y++)
                    {
                        float v = (y + 0.5f) / h;
                        float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edgeBot - 0.06f, edgeBot + 0.06f, v)) *
                                  Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edgeTop + 0.06f, edgeTop - 0.06f, v));
                        float grain = (float)rnd.NextDouble();
                        a *= grain < 0.18f ? 0.15f : (0.7f + 0.3f * grain);
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                    }
                }
                t.SetPixels32(px);
                t.Apply(true);
                _stroke = t;
                return t;
            }
        }

        /// Dark board with chalk-dust smudges.
        public static Texture2D Board
        {
            get
            {
                if (_board != null) return _board;
                const int s = 512;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "ChalkBoard" };
                var rnd = new System.Random(3);
                var px = new Color32[s * s];
                Color b = BoardColor;
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = x / (float)s, v = y / (float)s;
                    float n = TileNoise(u, v, 3, 0) * 0.5f + TileNoise(u, v, 9, 10) * 0.3f + TileNoise(u, v, 23, 20) * 0.2f;
                    float smudge = Mathf.Clamp01((n - 0.45f) * 2.2f) * 0.05f;
                    float grain = ((float)rnd.NextDouble() - 0.5f) * 0.018f;
                    float k = smudge + grain;
                    px[y * s + x] = new Color(b.r + k, b.g + k * 1.05f, b.b + k, 1);
                }
                t.SetPixels32(px);
                t.Apply();
                _board = t;
                return t;
            }
        }

        static float TileNoise(float u, float v, int freq, float off)
        {
            // Perlin sampled on a torus-like mapping for tileability (approximate).
            float a = u * Mathf.PI * 2, b = v * Mathf.PI * 2;
            float r = freq / (Mathf.PI * 2);
            return Mathf.PerlinNoise(off + r * Mathf.Cos(a) + r * Mathf.Sin(b) * 0.37f, off + r * Mathf.Sin(a) + r * Mathf.Cos(b) * 0.61f);
        }

        /// A browser has no system fonts: the web build carries its own hand, Pangolin (SIL OFL, see the licence
        /// beside it), close to Ink Free in width and with real lower case (g is not G, t is not T). Its import settings
        /// fall back on Liberation Sans (SIL OFL) for what it lacks: Greek letters, arrows, ►.
#if UNITY_WEBGL && !UNITY_EDITOR
        public static readonly bool WebFont = true;
#elif UNITY_EDITOR
        // dev: an empty file Temp/webfont.flag previews the browser's font in the editor
        public static readonly bool WebFont = System.IO.File.Exists("Temp/webfont.flag");
#else
        public static readonly bool WebFont = false;
#endif
        public static float FontScale => 1f;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                if (WebFont) _font = Resources.Load<Font>("Fonts/Pangolin-Regular");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Ink Free", "Segoe Print", "Comic Sans MS", "Arial" }, 32);
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// Symbols of quantities and formulas: the same hand as everything else, on every platform.
        public static Font MathFont => Font;

        /// Marks a label that shows symbols or a formula (kept as the one place to give them a font of their own).
        public static Text Math(Text t) => t;

        /// The few pictograms the web font has no glyph for: swapped for ones it has, or left out.
        public static string Sym(string s)
        {
            if (!WebFont || string.IsNullOrEmpty(s)) return s;
            return s.Replace("▶", "►").Replace("✓", "√").Replace("⚗ ", "").Replace("⚙ ", "").Replace("★ ", "").Replace("⚗", "").Replace("⚙", "").Replace("★", "");
        }
    }
}
