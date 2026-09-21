using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics
{
    /// A little body of water, simulated. Its surface is a row of columns: each one is pulled back to the calm level
    /// and tugged by its neighbours, so a dropped specimen pushes a dent that springs back into a jet and sends waves
    /// running to the walls and back. Droplets thrown up by a splash fly on their own and fall back in with a ripple.
    /// The same pool is used by the bath, the measuring cylinder, the pot and the ice.
    public class Pool
    {
        readonly float[] _h, _v;
        class Drop { public Vector2 p, v; public float life; }
        readonly List<Drop> _drops = new List<Drop>();
        readonly float _stiff, _spread, _damp;

        public float Left, Right;        // walls at the calm surface
        public float Rest;               // height of the calm surface
        public float Phase;              // offset of the idle ripple, so two pools never breathe in step
        public int N => _h.Length;

        public Pool(int columns, float stiff = 34f, float spread = 2400f, float damp = 1.3f)
        {
            _h = new float[columns]; _v = new float[columns];
            _stiff = stiff; _spread = spread; _damp = damp;
        }

        public void Reset() { for (int i = 0; i < N; i++) { _h[i] = 0; _v[i] = 0; } _drops.Clear(); }

        float ColX(int i) => Mathf.Lerp(Left, Right, i / (float)(N - 1));

        /// The surface at x, idle breathing included.
        public float Surface(float x)
        {
            float u = Mathf.Clamp01((x - Left) / Mathf.Max(1, Right - Left)) * (N - 1);
            int i = Mathf.Min(N - 2, Mathf.FloorToInt(u));
            float h = Mathf.Lerp(_h[i], _h[i + 1], u - i);
            return Rest + h + Mathf.Sin(Time.time * 2.4f + Phase + x * 0.05f) * 1.6f;
        }

        /// How much the water is moving: the biggest dent or bump right now.
        public float Agitation { get { float m = 0; for (int i = 0; i < N; i++) m = Mathf.Max(m, Mathf.Abs(_h[i])); return m; } }

        /// Something hit the water at x: the columns under it are shoved down (strength = speed, px/s), and a crown of
        /// droplets is thrown up — more of them, and higher, for a harder hit.
        public void Splash(float x, float strength, float width, int drops = -1)
        {
            for (int i = 0; i < N; i++)
            {
                float d = (ColX(i) - x) / Mathf.Max(4, width);
                _v[i] -= strength * Mathf.Exp(-d * d * 2f);
            }
            if (drops < 0) drops = Mathf.Clamp(Mathf.RoundToInt(strength / 40f), 2, 12);
            for (int k = 0; k < drops; k++)
            {
                float side = k % 2 == 0 ? 1 : -1;
                float a = Mathf.PI / 2 + side * Random.Range(0.25f, 1.05f);
                float sp = strength * Random.Range(0.7f, 1.3f);
                _drops.Add(new Drop { p = new Vector2(x + side * Random.Range(2f, width * 0.6f), Rest + 2), v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp });
            }
        }

        /// A gentle push without droplets (bubbles, a stir).
        public void Nudge(float x, float strength, float width = 10f)
        {
            for (int i = 0; i < N; i++) { float d = (ColX(i) - x) / Mathf.Max(4, width); _v[i] += strength * Mathf.Exp(-d * d * 2f); }
        }

        public void Step(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            const int sub = 4;
            float h = dt / sub;
            for (int s = 0; s < sub; s++)
            {
                for (int i = 0; i < N; i++)
                {
                    float l = i > 0 ? _h[i - 1] : _h[i], r = i < N - 1 ? _h[i + 1] : _h[i];   // the walls reflect
                    float lap = l + r - 2 * _h[i];
                    _v[i] += (_spread * lap - _stiff * _h[i] - _damp * _v[i]) * h;
                }
                for (int i = 0; i < N; i++) _h[i] = Mathf.Clamp(_h[i] + _v[i] * h, -40f, 40f);
            }
            // droplets fly and fall back; where one lands, it rings the surface
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                d.life += dt;
                d.v.y -= 900f * dt;
                d.p += d.v * dt;
                bool back = d.v.y < 0 && d.p.y <= Surface(d.p.x) && d.p.x > Left && d.p.x < Right;
                if (back) { Nudge(d.p.x, -40f, 6f); _drops.RemoveAt(i); continue; }
                if (d.life > 1.4f || d.p.y < Rest - 200) _drops.RemoveAt(i);
            }
        }

        /// Draws the pool: the body as chalk strokes clipped to the moving surface (a trough bares the strokes under it),
        /// the surface as one line, a meniscus at each wall, and the flying droplets.
        public void Draw(ChalkShape D, float bottom, System.Func<float, float> left, System.Func<float, float> right, float alpha = 1f)
        {
            var body = new Color(0.45f, 0.86f, 1f, 0.17f * alpha);
            var top = new Color(0.45f, 0.86f, 1f, 0.75f * alpha);
            const int cols = 28;
            float peak = Rest + 45;
            for (float y = bottom + 6; y < peak; y += 9)
            {
                float x0 = left(Mathf.Min(y, Rest)) + 5, x1 = right(Mathf.Min(y, Rest)) - 5;
                if (x1 <= x0) continue;
                float segStart = float.NaN, prevX = x0;
                for (int j = 0; j <= cols; j++)
                {
                    float x = Mathf.Lerp(x0, x1, j / (float)cols);
                    bool wet = Surface(x) > y + 3;
                    if (wet && float.IsNaN(segStart)) segStart = x;
                    if ((!wet || j == cols) && !float.IsNaN(segStart))
                    {
                        float end = wet ? x : prevX;
                        if (end - segStart > 3) D.Line(new Vector2(segStart, y), new Vector2(end, y), body, 2.2f);
                        segStart = float.NaN;
                    }
                    prevX = x;
                }
            }
            var line = new List<Vector2>();
            for (int j = 0; j <= 30; j++) { float x = Mathf.Lerp(Left, Right, j / 30f); line.Add(new Vector2(x, Surface(x))); }
            D.Poly(line, top, 2.6f, false, false);
            var edge = new Color(0.45f, 0.86f, 1f, 0.5f * alpha);
            D.Line(new Vector2(Left, Surface(Left)), new Vector2(Left + 4, Surface(Left) + 5), edge, 2f);
            D.Line(new Vector2(Right, Surface(Right)), new Vector2(Right - 4, Surface(Right) + 5), edge, 2f);
            foreach (var d in _drops)
            {   // a droplet: a short stroke along its flight, a tear drop when it falls
                var dir = d.v.sqrMagnitude > 1 ? d.v.normalized : Vector2.up;
                D.Line(d.p, d.p - dir * (d.v.y < 0 ? 10f : 7f), new Color(0.6f, 0.92f, 1f, 0.95f * alpha), 3.6f);
            }
        }
    }
}
