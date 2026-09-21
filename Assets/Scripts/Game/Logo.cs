using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics
{
    /// The game's title drawn by hand in chalk, not typed: "Physics" stands on a lab table with a specimen on its edge,
    /// the dot of its i is a lightbulb and the tail of its y is a pendulum that swings; "Incremental" climbs letter by
    /// letter like a growth curve and ends in an arrow going up.
    public class Logo : MonoBehaviour
    {
        const float S1 = 1.45f, S2 = 1.0f;                  // letter scale of the two words
        const float Base1 = 272, Base2 = 118, Rise = 5;     // baselines; each letter of the second word climbs a little
        ChalkShape _anim;
        Vector2 _pivot, _bulb;
        float _t;

        /// Where the "demo" tag pins on: the top right of the first word.
        public Vector2 TagAt { get; private set; }

        public static Logo Build(RectTransform parent)
        {
            var rt = UIF.Rect("Logo", parent, Vector2.zero, Vector2.zero);
            var l = rt.gameObject.AddComponent<Logo>();
            l.Make(rt);
            return l;
        }

        // ---------------- the letters: strokes in a box 100 high (x-height 60), y up, baseline 0 ----------------
        static List<Vector2> P(params float[] xy) { var l = new List<Vector2>(); for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(new Vector2(xy[i], xy[i + 1])); return l; }
        static List<Vector2> Arc(float cx, float cy, float rx, float ry, float a0, float a1, int n = 18)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= n; i++) { float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad; l.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry)); }
            return l;
        }
        static List<Vector2> J(params List<Vector2>[] parts) { var l = new List<Vector2>(); foreach (var p in parts) l.AddRange(p); return l; }

        /// The strokes of one letter; returns its width.
        static float Glyph(char c, List<List<Vector2>> g)
        {
            switch (c)
            {
                case 'P': g.Add(P(0, 0, 0, 100)); g.Add(J(P(0, 100, 26, 100), Arc(26, 75, 24, 25, 90, -90), P(26, 50, 0, 50))); return 52;
                case 'h': g.Add(P(0, 0, 0, 100)); g.Add(J(Arc(20, 38, 20, 22, 180, 0), P(40, 38, 40, 0))); return 40;
                case 'y': g.Add(P(0, 60, 21, 16)); g.Add(P(42, 60, 21, 16)); return 42;          // its tail is the pendulum
                case 's': g.Add(J(Arc(19, 45, 17, 15, 20, 270), Arc(19, 15, 19, 15, 90, -160))); return 38;
                case 'i': g.Add(P(0, 0, 0, 54)); return 4;                                        // its dot is the lightbulb
                case 'c': g.Add(Arc(22, 30, 22, 30, 50, 310)); return 42;
                case 'I': g.Add(P(12, 0, 12, 100)); g.Add(P(0, 100, 24, 100)); g.Add(P(0, 0, 24, 0)); return 24;
                case 'n': g.Add(P(0, 0, 0, 60)); g.Add(J(Arc(20, 38, 20, 22, 180, 0), P(40, 38, 40, 0))); return 40;
                case 'r': g.Add(P(0, 0, 0, 60)); g.Add(Arc(18, 34, 18, 22, 180, 55)); return 30;
                case 'e': g.Add(J(P(2, 30, 44, 30), Arc(22, 30, 22, 30, 0, 320))); return 44;
                case 'm': g.Add(P(0, 0, 0, 60)); g.Add(J(Arc(17, 38, 17, 22, 180, 0), P(34, 38, 34, 0))); g.Add(J(Arc(51, 38, 17, 22, 180, 0), P(68, 38, 68, 0))); return 68;
                case 't': g.Add(J(P(10, 92, 10, 12), Arc(20, 12, 10, 12, 180, 300))); g.Add(P(0, 60, 26, 60)); return 26;
                case 'a': g.Add(Arc(21, 29, 21, 29, 30, 390, 24)); g.Add(P(43, 60, 43, 0)); return 44;
                case 'l': g.Add(J(P(0, 100, 0, 12), Arc(10, 12, 10, 12, 180, 300))); return 14;
            }
            return 30;
        }

        static float Width(string word, float gap) { float w = 0; var g = new List<List<Vector2>>(); foreach (char c in word) { g.Clear(); w += Glyph(c, g) + gap; } return w - gap; }

        /// Writes a word; lift(i) raises letter i. Returns the x where each letter starts (in unit steps scaled).
        List<float> Write(ChalkShape s, string word, Vector2 at, float scale, float gap, float width, System.Func<int, float> lift)
        {
            var starts = new List<float>();
            var g = new List<List<Vector2>>();
            float x = 0;
            var main = ChalkTex.White;
            var ghost = new Color(1, 1, 1, 0.22f);
            for (int i = 0; i < word.Length; i++)
            {
                g.Clear();
                float w = Glyph(word[i], g);
                starts.Add(at.x + x * scale);
                var o = new Vector2(at.x + x * scale, at.y + lift(i));
                foreach (var stroke in g)
                {
                    var pts = new List<Vector2>();
                    foreach (var p in stroke) pts.Add(o + p * scale);
                    s.Poly(pts, main, width, false, true);
                    // a second, fainter pass slightly off: chalk goes over its letters twice
                    var off = new List<Vector2>();
                    foreach (var p in pts) off.Add(p + new Vector2(2.2f, -1.8f));
                    s.Poly(off, ghost, width * 0.55f, false, true);
                }
                x += w + gap;
            }
            return starts;
        }

        void Make(RectTransform rt)
        {
            var s = UIF.Shape(rt, 820, "Letters");
            float w1 = Width("Physics", 18) * S1, w2 = Width("Incremental", 16) * S2;
            // the two words stand staggered: the second starts past the pendulum hanging from the first
            float x1 = -440, x2 = -118;
            var starts = Write(s, "Physics", new Vector2(x1, Base1), S1, 18, 7.5f, i => 0);
            Write(s, "Incremental", new Vector2(x2, Base2), S2, 16, 5.8f, i => i * Rise);

            // "Physics" stands on a lab table, a specimen waiting on its edge
            var W = ChalkTex.White;
            float tl = x1 - 18, tr = x1 + w1 + 64;
            s.Line(new Vector2(tl, Base1 - 9), new Vector2(tr, Base1 - 7), W, 4.5f);
            s.Line(new Vector2(tl + 10, Base1 - 9), new Vector2(tl + 2, Base1 - 48), W, 3.5f);
            s.Line(new Vector2(tr - 10, Base1 - 7), new Vector2(tr - 2, Base1 - 46), W, 3.5f);
            var cube = new Vector2(tr - 24, Base1 + 8);
            s.Rect(cube, new Vector2(28, 28), W, 3f);
            s.HatchRect(cube, new Vector2(20, 20), new Color(1, 1, 1, 0.45f), 1.8f, 7f);

            // "Incremental" climbs: a faint rising line under it and a yellow arrow carrying on up
            float end = x2 + w2;
            s.Line(new Vector2(x2 - 14, Base2 - 10), new Vector2(end + 8, Base2 - 10 + (10 * Rise) + 4), new Color(1, 1, 1, 0.3f), 2.5f);
            var a0 = new Vector2(end + 18, Base2 + 10 * Rise + 20);
            var a1 = a0 + new Vector2(70, 58);
            s.Arrow(a0, a1, ChalkTex.Yellow, 4f, 18f);

            // the pendulum hangs from the fork of the y, the lightbulb sits over the i
            _pivot = new Vector2(starts[2] + 21 * S1, Base1 + 16 * S1);
            _bulb = new Vector2(starts[4] + 2 * S1, Base1 + 82 * S1);
            _anim = UIF.Shape(rt, 821, "Moving");
            TagAt = new Vector2(x1 + w1 + 150, Base1 + 128);
            Animate();
        }

        void Update() { _t += Time.unscaledDeltaTime; Animate(); }

        void Animate()
        {
            var s = _anim; s.Clear();
            var W = ChalkTex.White;
            // the pendulum: a thread and a specimen, swinging slowly
            float ang = Mathf.Sin(_t * 1.9f) * 0.2f;
            var dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
            const float len = 96;
            var bob = _pivot + dir * (len + 15);
            s.Line(_pivot, _pivot + dir * len, W, 3f);
            float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            Vector2 R(float x, float y) => bob + new Vector2(x * cs - y * sn, x * sn + y * cs);
            s.Poly(new[] { R(-15, -15), R(15, -15), R(15, 15), R(-15, 15) }, W, 3f, true);
            for (int i = -1; i <= 1; i++) s.Line(R(-10, i * 7), R(10, i * 7), new Color(1, 1, 1, 0.45f), 1.8f);
            var trace = new List<Vector2>();                          // the arc it sweeps, faintly
            for (int i = 0; i <= 16; i++) { float a = Mathf.Lerp(-0.2f, 0.2f, i / 16f); trace.Add(_pivot + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * (len + 15)); }
            s.Poly(trace, new Color(1, 1, 1, 0.14f), 2f, false, false);
            // the dot of the i: an idea, glowing on and off
            float glow = 0.5f + 0.5f * Mathf.Sin(_t * 2.6f);
            Lab.DrawIcon(s, "bulb", Color.Lerp(ChalkTex.Cyan, ChalkTex.Yellow, glow * 0.6f), 1.5f, _bulb);
        }
    }
}
