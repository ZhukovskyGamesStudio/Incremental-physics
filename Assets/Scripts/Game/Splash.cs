using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// A flat filled shape: the studio mark is flat colour, not chalk. Triangles are set from code.
    [RequireComponent(typeof(CanvasRenderer))]
    public class Blob : MaskableGraphic
    {
        readonly List<Vector2> _tris = new List<Vector2>();

        public void SetTriangles(List<Vector2> tris) { _tris.Clear(); _tris.AddRange(tris); SetVerticesDirty(); }

        public void Ellipse(Vector2 c, float rx, float ry, int seg = 48)
        {
            _tris.Clear();
            if (rx > 0.01f && ry > 0.01f)
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                    _tris.Add(c); _tris.Add(c + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry)); _tris.Add(c + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry));
                }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i + 2 < _tris.Count; i += 3)
            {
                int b = vh.currentVertCount;
                for (int k = 0; k < 3; k++) vh.AddVert(_tris[i + k], color, Vector2.zero);
                vh.AddTriangle(b, b + 1, b + 2);
            }
        }
    }

    /// The studio's splash: the Zhukovsky Games wing swoops in, opens its eye, watches the name being written beside
    /// it, blinks and flies off. It cannot be skipped, and it covers the whole screen all the way through: once the
    /// wing is gone the navy turns into the colour of the board, still opaque, and only then does the board lift off the
    /// game like any change of screen. Motion, timings and colours follow the approved animation
    /// (Marketing/assets/logo/anim/splash_spacecadet.html, 4.4 s, with its sound).
    public class Splash : MonoBehaviour
    {
        static readonly Color Navy = new Color32(0x2A, 0x32, 0x4B, 0xFF), Orange = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        const float S = 2.4f;                      // one unit of the 800x400 animation = 2.4 px of the 1920x1080 design
        const float DUR = 4400, T_EXIT = 3350, T_FLY = 1250, REST = -10, RAISE = 2.5f;

        RectTransform _wing, _clip;
        Blob _pupil, _hl;
        Image _veil;
        CanvasGroup _group, _wordGroup;
        AudioSource _audio;
        System.Action _done;
        float _t = -1, _out = -1;                  // animation clock (ms); the fade-out once it is over
        int _frames;
        float _wait;

        public static void Show(Transform parent, System.Action done)
        {
            var root = UIF.Fill("Splash", parent);
            root.SetAsLastSibling();
            var s = root.gameObject.AddComponent<Splash>();
            s._done = done;
            s.Build(root);
        }

        void Build(RectTransform root)
        {
            _veil = root.gameObject.AddComponent<Image>();
            _veil.color = Navy;
            _veil.raycastTarget = true;
            _group = root.gameObject.AddComponent<CanvasGroup>();
            var content = UIF.Rect("Content", root, Vector2.zero, new Vector2(1920, 1080));
            gameObject.AddComponent<ScaleToFit>().Init(content, 1920, 1080);

            // the name, written left to right behind a moving edge
            _clip = UIF.Rect("Clip", content, Vector2.zero, new Vector2(0, 240 * S));
            _clip.pivot = new Vector2(0, 0.5f);
            _clip.gameObject.AddComponent<RectMask2D>();
            _wordGroup = _clip.gameObject.AddComponent<CanvasGroup>();
            var word = UIF.Rect("Word", _clip, Vector2.zero, new Vector2(900 * 0.394f * S, 212 * 0.394f * S));
            word.anchorMin = word.anchorMax = new Vector2(0, 0.5f);
            word.pivot = new Vector2(0, 1);
            word.anchoredPosition = new Vector2(-3.94f * S, (200 - (164 - 3.94f)) * S);
            var img = word.gameObject.AddComponent<RawImage>();
            img.texture = Resources.Load<Texture2D>("Splash/zg_word");
            img.raycastTarget = false;

            // the wing, with its eye
            _wing = UIF.Rect("Wing", content, new Vector2(700 * S, 0), Vector2.zero);
            var body = _wing.gameObject.AddComponent<Blob>();
            body.color = Orange; body.raycastTarget = false;
            body.SetTriangles(Triangulate(Airfoil()));
            _pupil = UIF.Rect("Pupil", _wing, Vector2.zero, Vector2.zero).gameObject.AddComponent<Blob>();
            _pupil.color = Navy; _pupil.raycastTarget = false;
            _hl = UIF.Rect("Glint", _wing, Vector2.zero, Vector2.zero).gameObject.AddComponent<Blob>();
            _hl.color = Orange; _hl.raycastTarget = false;

            var clip = Resources.Load<AudioClip>("Splash/zg_splash");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.spatialBlend = 0; _audio.clip = clip; _audio.volume = 0.9f;
            if (clip != null && clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();   // a browser decodes it in its own time
            Music.Hold = true;
            Render(0);
        }


        void Update()
        {
            // the first frames after loading are long: the clock starts once the game runs smoothly
            if (_frames++ < 2) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 1000f;
            if (_t < 0)
            {   // the sound and the picture start together: wait (a little) for the sound to be ready
                _wait += dt;
                var c = _audio.clip;
                if (c != null && c.loadState == AudioDataLoadState.Loading && _wait < 1500f) return;
                _t = 0;
                if (_audio.clip != null && !Sfx.Muted) _audio.Play();
            }
            else _t += dt;
            Render(Mathf.Min(_t, DUR));
            if (_t >= DUR && _out < 0) _out = 0;
            if (_out >= 0)
            {   // the empty navy turns into the board (still covering everything), then the board lifts off the game
                _out += dt;
                float c = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_out / 400f));
                _veil.color = Color.Lerp(Navy, ChalkTex.BoardColor, c);
                float k = Mathf.Clamp01((_out - 400f) / 450f);
                _group.alpha = 1 - k * k * (3 - 2 * k);
                if (k >= 1) Finish();
            }
        }

        void Finish()
        {
            Music.Hold = false;
            _done?.Invoke();
            _done = null;
            Destroy(gameObject);
        }

        void OnDestroy() { Music.Hold = false; }

        // ---------------- the motion (a port of the approved animation; times in ms, units of its 800x400 frame) ----------------
        static float Clamp(float v, float a, float b) => Mathf.Max(a, Mathf.Min(b, v));
        static float Seg(float t, float a, float b) => Clamp((t - a) / (b - a), 0, 1);
        static float Back(float t) { const float s = 1.70158f; return 1 + (s + 1) * Mathf.Pow(t - 1, 3) + s * Mathf.Pow(t - 1, 2); }
        static float EaseOut(float t) => 1 - Mathf.Pow(1 - t, 3);
        static float EaseOutQuint(float t) => 1 - Mathf.Pow(1 - t, 5);
        static float EaseOutQuart(float t) => 1 - Mathf.Pow(1 - t, 4);
        static float EaseInCubic(float t) => t * t * t;
        static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1) / 2;

        static readonly Vector2 P0 = new Vector2(1080, 70), P1 = new Vector2(820, 320), P2 = new Vector2(430, 148), P3 = new Vector2(190, 200);
        static Vector2 Bez(Vector2 a0, Vector2 a1, Vector2 a2, Vector2 a3, float u) { float a = 1 - u; return a * a * a * a0 + 3 * a * a * u * a1 + 3 * a * u * u * a2 + u * u * u * a3; }
        static Vector2 DBez(Vector2 a0, Vector2 a1, Vector2 a2, Vector2 a3, float u) { float a = 1 - u; return 3 * a * a * (a1 - a0) + 6 * a * u * (a2 - a1) + 3 * u * u * (a3 - a2); }

        void Render(float t)
        {
            // flight: in fast from the top right, a dive, a pull-up into the resting spot
            float p = Seg(t, 0, T_FLY), u = EaseOutQuint(p);
            var pos = Bez(P0, P1, P2, P3, u); var d = DBez(P0, P1, P2, P3, u);
            float tangent = Mathf.Atan2(-d.y, -d.x) * Mathf.Rad2Deg;
            float level = Seg(t, T_FLY - 250, T_FLY + 150);
            float rot = tangent * (1 - level) + REST * level;
            float x = pos.x, y = pos.y, sx = 1;
            float speed = d.magnitude * (1 - p) * (1 - p);
            sx = 1 + Clamp(speed / 900, 0, 1) * 0.06f;
            if (t > T_FLY) { float h = EaseInOutSine(Seg(t, T_FLY, T_FLY + 900)); y += 2.2f * h * Mathf.Sin((t - T_FLY) / 430); rot += 0.8f * h * Mathf.Sin((t - T_FLY) / 520); }
            // exit: a little dip, then off nose-first along an arc
            float ex = Seg(t, T_EXIT + 200, T_EXIT + 820), exi = EaseInCubic(ex);
            if (ex > 0)
            {
                var E0 = new Vector2(x, y); var E1 = new Vector2(x - 70, y + 46); var E2 = new Vector2(x - 250, y - 30); var E3 = new Vector2(x - 560, y - 250);
                var q = Bez(E0, E1, E2, E3, exi); var dq = DBez(E0, E1, E2, E3, exi);
                float tang = Mathf.Atan2(-dq.y, -dq.x) * Mathf.Rad2Deg; float bl2 = Seg(ex, 0, 0.35f);
                rot = rot * (1 - bl2) + tang * bl2; x = q.x; y = q.y; sx = 1;
            }
            _wing.anchoredPosition = new Vector2((x - 400) * S, (200 - y) * S);
            _wing.localEulerAngles = new Vector3(0, 0, -rot);          // the animation's y points down
            _wing.localScale = new Vector3(sx, 1, 1);

            // the eye opens with a little overshoot, glances along the name as it is written, blinks, closes before leaving
            float op = Seg(t, 1150, 1450); float ry = 14 * Back(op);
            float cyBase = -5 - RAISE * (1 - EaseOut(Seg(t, 1450, 1800)));
            float w = EaseOutQuart(Seg(t, 1600, 2300));
            float toWord = EaseOut(Seg(t, 1500, 1750)); float track = 6 + 5 * w;
            float bl = Seg(t, 2600, 2800); float after = bl >= 0.5f ? 1 : 0;
            float dx = track * toWord * (1 - after), dy = 1.5f * toWord * (1 - after) + (-3) * after;
            if (bl > 0 && bl < 1) ry *= bl < 0.5f ? 1 - bl * 2 : (bl - 0.5f) * 2;
            ry *= 1 - Seg(t, T_EXIT, T_EXIT + 160);
            _pupil.Ellipse(new Vector2((-24 + dx) * S, -(cyBase + dy) * S), 14 * S, Mathf.Max(0, ry) * S);
            if (ry > 8) _hl.Ellipse(new Vector2((-21 + dx * 1.2f) * S, -(cyBase - 3 + dy * 1.2f) * S), 6 * S, 6 * S); else _hl.Ellipse(Vector2.zero, 0, 0);

            // the name: written left to right after the eye has opened, with a little push; it fades as the wing leaves
            _clip.anchoredPosition = new Vector2((300 - 400 - 12 * (1 - w)) * S, 0);
            _clip.sizeDelta = new Vector2(430 * w * S, 240 * S);
            _wordGroup.alpha = 1 - Seg(t, T_EXIT + 200, T_EXIT + 600);
        }

        // ---------------- the wing's outline (the approved airfoil path) ----------------
        const string Path = "100.7,5.3 100.4,5.2 99.9,5.1 99.3,5.1 98.7,4.9 97.9,4.8 97.0,4.6 96.0,4.5 94.9,4.2 93.7,4.0 92.4,3.8 91.0,3.5 89.5,3.2 87.9,2.9 86.2,2.5 84.4,2.2 82.6,1.8 80.7,1.4 78.7,1.0 76.6,0.6 74.5,0.2 72.3,-0.3 70.0,-0.8 67.7,-1.2 65.3,-1.7 62.8,-2.2 60.3,-2.7 57.8,-3.2 55.2,-3.7 52.5,-4.2 49.8,-4.7 47.1,-5.2 44.4,-5.7 41.6,-6.2 38.8,-6.7 35.9,-7.2 33.0,-7.7 30.1,-8.1 27.2,-8.6 24.3,-9.0 21.4,-9.5 18.4,-9.9 15.4,-10.3 12.5,-10.7 9.5,-11.0 6.5,-11.4 3.5,-11.7 0.5,-12.0 -2.4,-12.3 -5.4,-12.5 -8.3,-12.8 -11.3,-13.0 -14.2,-13.2 -17.1,-13.3 -20.0,-13.5 -22.9,-13.6 -25.7,-13.6 -28.5,-13.7 -31.3,-13.7 -34.1,-13.7 -36.8,-13.7 -39.5,-13.6 -42.2,-13.6 -44.8,-13.5 -47.4,-13.3 -49.9,-13.2 -52.4,-13.0 -54.9,-12.8 -57.3,-12.5 -59.6,-12.3 -61.9,-12.0 -64.2,-11.7 -66.4,-11.3 -68.5,-11.0 -70.6,-10.6 -72.7,-10.2 -74.6,-9.7 -76.6,-9.3 -78.4,-8.8 -80.2,-8.4 -81.9,-7.9 -83.6,-7.3 -85.1,-6.8 -86.7,-6.3 -88.1,-5.7 -89.5,-5.1 -90.8,-4.6 -92.0,-4.0 -93.2,-3.4 -94.2,-2.8 -95.2,-2.2 -96.2,-1.5 -97.0,-0.9 -97.8,-0.3 -98.5,0.3 -99.1,0.9 -99.6,1.6 -100.0,2.2 -100.4,2.8 -100.7,3.4 -100.9,4.0 -101.0,4.6 -101.0,5.2 -100.9,5.7 -100.8,6.3 -100.6,6.8 -100.2,7.4 -99.8,7.9 -99.4,8.4 -98.8,8.9 -98.1,9.3 -97.4,9.8 -96.6,10.2 -95.7,10.6 -94.7,11.0 -93.6,11.3 -92.4,11.7 -91.2,12.0 -89.8,12.3 -88.4,12.5 -86.9,12.8 -85.3,13.0 -83.7,13.2 -81.9,13.3 -80.1,13.5 -78.2,13.6 -76.3,13.6 -74.2,13.7 -72.1,13.7 -69.9,13.7 -67.6,13.7 -65.3,13.7 -62.8,13.6 -60.4,13.5 -57.8,13.4 -55.2,13.2 -52.5,13.1 -49.8,12.9 -47.0,12.7 -44.1,12.4 -41.2,12.2 -38.2,11.9 -35.2,11.7 -32.2,11.4 -29.0,11.1 -25.9,10.8 -22.7,10.4 -19.5,10.1 -16.2,9.8 -12.9,9.4 -9.6,9.1 -6.3,8.8 -2.9,8.4 0.5,8.1 3.9,7.7 7.3,7.4 10.7,7.1 14.1,6.8 17.5,6.5 20.9,6.2 24.2,5.9 27.6,5.6 30.9,5.4 34.3,5.1 37.5,4.9 40.8,4.7 44.0,4.5 47.1,4.4 50.2,4.2 53.3,4.1 56.3,4.0 59.2,3.9 62.1,3.9 64.8,3.8 67.6,3.8 70.2,3.8 72.7,3.8 75.2,3.8 77.5,3.8 79.8,3.9 81.9,3.9 84.0,4.0 85.9,4.1 87.8,4.2 89.5,4.3 91.1,4.4 92.6,4.5 94.0,4.6 95.2,4.7 96.3,4.8 97.4,4.9 98.2,5.0 99.0,5.1 99.6,5.1 100.2,5.2 100.6,5.3 100.8,5.3 101.0,5.3";

        /// The outline in design pixels (y up), without repeated points.
        static List<Vector2> Airfoil()
        {
            var pts = new List<Vector2>();
            foreach (var pair in Path.Split(' '))
            {
                var xy = pair.Split(',');
                var v = new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture) * S, -float.Parse(xy[1], CultureInfo.InvariantCulture) * S);
                if (pts.Count == 0 || (v - pts[pts.Count - 1]).sqrMagnitude > 0.01f) pts.Add(v);
            }
            if (pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude < 0.01f) pts.RemoveAt(pts.Count - 1);
            return pts;
        }

        /// Ear clipping: the wing is a thin curved outline, so a simple fan would spill out of it.
        static List<Vector2> Triangulate(List<Vector2> poly)
        {
            var res = new List<Vector2>();
            var idx = new List<int>();
            for (int i = 0; i < poly.Count; i++) idx.Add(i);
            float area = 0;
            for (int i = 0; i < poly.Count; i++) { var a = poly[i]; var b = poly[(i + 1) % poly.Count]; area += a.x * b.y - b.x * a.y; }
            bool ccw = area > 0;
            int guard = 0;
            while (idx.Count > 3 && guard++ < 100000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 A = poly[ia], B = poly[ib], C = poly[ic];
                    float cr = (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
                    if (Mathf.Abs(cr) < 1e-4f) { idx.RemoveAt(i); clipped = true; break; }   // a point on a straight run: drop it
                    if (ccw ? cr < 0 : cr > 0) continue;                                     // a reflex corner is no ear
                    bool inside = false;
                    for (int j = 0; j < idx.Count && !inside; j++)
                    {
                        int q = idx[j];
                        if (q == ia || q == ib || q == ic) continue;
                        inside = InTri(poly[q], A, B, C);
                    }
                    if (inside) continue;
                    res.Add(A); res.Add(B); res.Add(C);
                    idx.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { res.Add(poly[idx[0]]); res.Add(poly[idx[1]]); res.Add(poly[idx[2]]); }
            return res;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}
