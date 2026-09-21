using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// UGUI graphic that renders hand-drawn chalk strokes. Coordinates are local to the RectTransform (pivot = origin).
    [RequireComponent(typeof(CanvasRenderer))]
    public class ChalkShape : MaskableGraphic
    {
        struct Stroke { public int start, count; public float width; public Color color; public bool closed; public float uOff; }

        readonly List<Vector2> _pts = new List<Vector2>();
        readonly List<Stroke> _strokes = new List<Stroke>();
        System.Random _rnd;
        public int seed = 1;
        public float wobble = 1.3f;

        public override Texture mainTexture => ChalkTex.Stroke;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            _rnd = new System.Random(seed);
        }

        public ChalkShape Clear()
        {
            _pts.Clear();
            _strokes.Clear();
            _rnd = new System.Random(seed);
            SetVerticesDirty();
            return this;
        }

        float R() => (float)(_rnd ?? (_rnd = new System.Random(seed))).NextDouble() * 2f - 1f;

        // ---------- primitives ----------

        public void Poly(IList<Vector2> p, Color c, float w = 4f, bool closed = false, bool wob = true)
        {
            if (p.Count < 2) return;
            var s = new Stroke { start = _pts.Count, width = w, color = c, closed = closed, uOff = (R() + 1) * 0.5f };
            int n = p.Count;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % n];
                Vector2 d = b - a;
                float len = d.magnitude;
                int div = wob ? Mathf.Max(1, Mathf.CeilToInt(len / 16f)) : 1;
                Vector2 perp = len > 0.001f ? new Vector2(-d.y, d.x) / len : Vector2.zero;
                for (int j = 0; j < div; j++)
                {
                    Vector2 q = a + d * (j / (float)div);
                    if (wob && (j > 0 || i > 0)) q += perp * R() * wobble;
                    _pts.Add(q);
                }
            }
            if (!closed) _pts.Add(p[n - 1]);
            s.count = _pts.Count - s.start;
            _strokes.Add(s);
            SetVerticesDirty();
        }

        public void Line(Vector2 a, Vector2 b, Color c, float w = 4f)
        {
            // slight overshoot like a real chalk line
            Vector2 d = (b - a).normalized * (R() * 1.5f);
            Poly(new[] { a - d, b + d }, c, w);
        }

        public void Dashed(Vector2 a, Vector2 b, Color c, float w = 3f, float dash = 14f, float gap = 10f)
        {
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / Mathf.Max(0.001f, len);
            for (float t = 0; t < len; t += dash + gap)
                Poly(new[] { a + dir * t, a + dir * Mathf.Min(len, t + dash) }, c, w, false, false);
        }

        public void Circle(Vector2 c0, float r, Color c, float w = 4f, bool handDrawn = true)
        {
            int seg = Mathf.Clamp(Mathf.CeilToInt(r * 0.45f), 12, 64);
            var p = new List<Vector2>(seg + 4);
            float a0 = R() * Mathf.PI;
            float extra = handDrawn ? 0.25f + R() * 0.1f : 0f;
            float total = Mathf.PI * 2 + extra;
            int n = Mathf.CeilToInt(seg * total / (Mathf.PI * 2));
            float rr = r * (1 + R() * 0.02f);
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + total * i / n;
                float rad = rr + (handDrawn ? (i / (float)n) * r * 0.04f : 0);
                p.Add(c0 + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad);
            }
            Poly(p, c, w, !handDrawn, false);
        }

        public void Arc(Vector2 c0, float r, float a0, float a1, Color c, float w = 4f)
        {
            int n = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * r / 12f));
            var p = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n);
                p.Add(c0 + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            Poly(p, c, w, false, false);
        }

        public void Rect(Vector2 center, Vector2 size, Color c, float w = 4f)
        {
            Vector2 h = size * 0.5f;
            Vector2 bl = center - h, tr = center + h, br = new Vector2(tr.x, bl.y), tl = new Vector2(bl.x, tr.y);
            // four separate sides overshooting the corners, like drawn by hand
            float o = 5f;
            Line(bl + new Vector2(-o * Mathf.Abs(R()), R()), br + new Vector2(o * Mathf.Abs(R()), R()), c, w);
            Line(br + new Vector2(R(), -o * Mathf.Abs(R())), tr + new Vector2(R(), o * Mathf.Abs(R())), c, w);
            Line(tr + new Vector2(o * Mathf.Abs(R()), R()), tl + new Vector2(-o * Mathf.Abs(R()), R()), c, w);
            Line(tl + new Vector2(R(), o * Mathf.Abs(R())), bl + new Vector2(R(), -o * Mathf.Abs(R())), c, w);
        }

        public void Arrow(Vector2 a, Vector2 b, Color c, float w = 4f, float head = 16f)
        {
            Line(a, b, c, w);
            Vector2 d = (b - a).normalized;
            Vector2 p = new Vector2(-d.y, d.x);
            Line(b, b - d * head + p * head * 0.6f, c, w);
            Line(b, b - d * head - p * head * 0.6f, c, w);
        }

        /// Diagonal hatching clipped to a circle — the "fill" of chalk drawings.
        public void HatchCircle(Vector2 c0, float r, Color c, float w = 2.5f, float spacing = 9f)
        {
            Vector2 dir = new Vector2(1, 1).normalized, perp = new Vector2(-1, 1).normalized;
            for (float o = -r + spacing * 0.5f; o < r; o += spacing)
            {
                float half = Mathf.Sqrt(Mathf.Max(0, r * r - o * o)) * 0.9f;
                if (half < 2) continue;
                Vector2 m = c0 + perp * o;
                Poly(new[] { m - dir * half, m + dir * half }, c, w, false, false);
            }
        }

        public void HatchRect(Vector2 center, Vector2 size, Color c, float w = 2.5f, float spacing = 10f)
        {
            Vector2 h = size * 0.5f;
            // lines x - y = k  inside rect
            for (float k = -h.x - h.y + spacing * 0.5f; k < h.x + h.y; k += spacing)
            {
                // intersections of y = x - k with the box
                var ps = new List<Vector2>(2);
                float y1 = -h.x - k; if (y1 >= -h.y && y1 <= h.y) ps.Add(new Vector2(-h.x, y1));
                float y2 = h.x - k; if (y2 >= -h.y && y2 <= h.y) ps.Add(new Vector2(h.x, y2));
                float x1 = -h.y + k; if (x1 > -h.x && x1 < h.x) ps.Add(new Vector2(x1, -h.y));
                float x2 = h.y + k; if (x2 > -h.x && x2 < h.x) ps.Add(new Vector2(x2, h.y));
                if (ps.Count >= 2) Poly(new[] { center + ps[0] * 0.95f, center + ps[1] * 0.95f }, c, w, false, false);
            }
        }

        /// Ground / wall hatch: a line with short diagonal ticks on one side.
        public void Ground(Vector2 a, Vector2 b, Color c, float w = 4f, float tick = 14f, float spacing = 18f, bool below = true)
        {
            Line(a, b, c, w);
            Vector2 d = (b - a).normalized;
            Vector2 n = new Vector2(-d.y, d.x) * (below ? -1 : 1);
            float len = Vector2.Distance(a, b);
            for (float t = spacing * 0.5f; t < len; t += spacing)
            {
                Vector2 p = a + d * t;
                Poly(new[] { p, p + (n - d) * tick * 0.7f }, c, w * 0.6f, false, false);
            }
        }

        public void Zigzag(Vector2 a, Vector2 b, int coils, float amp, Color c, float w = 3.5f)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            Vector2 dir = d / Mathf.Max(0.001f, len), perp = new Vector2(-dir.y, dir.x);
            float lead = Mathf.Min(20f, len * 0.1f);
            var p = new List<Vector2> { a, a + dir * lead };
            float body = len - lead * 2;
            int n = coils * 2;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                p.Add(a + dir * (lead + body * t) + perp * amp * (i % 2 == 0 ? 1 : -1));
            }
            p.Add(b - dir * lead);
            p.Add(b);
            Poly(p, c, w, false, false);
        }

        // ---------- mesh ----------

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var ui = UIVertex.simpleVert;
            foreach (var s in _strokes)
            {
                Color32 col = s.color * color;
                ui.color = col;
                int segs = s.closed ? s.count : s.count - 1;
                float dist = 0;
                float uScale = 1f / (s.width * 7f);
                for (int i = 0; i < segs; i++)
                {
                    Vector2 a = _pts[s.start + i], b = _pts[s.start + (i + 1) % s.count];
                    Vector2 d = b - a;
                    float len = d.magnitude;
                    if (len < 0.01f) continue;
                    Vector2 dir = d / len;
                    Vector2 n = new Vector2(-dir.y, dir.x) * (s.width * 0.5f);
                    float ext = s.width * 0.35f;
                    Vector2 a2 = a - dir * ext, b2 = b + dir * ext;
                    float u0 = s.uOff + dist * uScale, u1 = s.uOff + (dist + len + ext * 2) * uScale;
                    int idx = vh.currentVertCount;
                    ui.position = a2 - n; ui.uv0 = new Vector2(u0, 0); vh.AddVert(ui);
                    ui.position = a2 + n; ui.uv0 = new Vector2(u0, 1); vh.AddVert(ui);
                    ui.position = b2 + n; ui.uv0 = new Vector2(u1, 1); vh.AddVert(ui);
                    ui.position = b2 - n; ui.uv0 = new Vector2(u1, 0); vh.AddVert(ui);
                    vh.AddTriangle(idx, idx + 1, idx + 2);
                    vh.AddTriangle(idx, idx + 2, idx + 3);
                    dist += len;
                }
            }
        }
    }
}
