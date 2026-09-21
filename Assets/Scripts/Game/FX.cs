using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// Floating chalk text (with a little punch as it appears) and chalk-dust bursts.
    public class FX : MonoBehaviour
    {
        public static FX I;

        class Popup { public Text t; public float age, life, punch, lean; public Vector2 start; public Color c; }
        class Burst { public ChalkShape s; public float age; public Vector2[] p, v; public Color c; }

        readonly List<Popup> _popups = new List<Popup>();
        readonly List<Burst> _bursts = new List<Burst>();

        void Awake() => I = this;

        public void Text(Transform parent, Vector2 pos, string s, Color c, int size = 30, float life = 1.1f, float punch = 1.35f)
        {
            if (string.IsNullOrEmpty(s) || parent == null) return;
            var t = UIF.Text(parent, s, size, c, pos, new Vector2(Mathf.Max(500, s.Length * size * 0.62f), size * 2.2f));
            if (s.Contains(GameState.Bulb)) InkText.On(t).Set(s, new Color(c.r, c.g, c.b, 1));   // ideas: the number and its lightbulb
            float lean = Random.Range(-2.5f, 2.5f);          // written in a hurry, never quite straight
            t.transform.localRotation = Quaternion.Euler(0, 0, lean);
            _popups.Add(new Popup { t = t, life = life, start = pos, c = c, punch = punch, lean = lean });
        }

        public void Dust(Transform parent, Vector2 pos, Color c, int count = 10, float speed = 220f, float upBias = 0f)
        {
            var s = UIF.Shape(parent, Random.Range(1, 9999), "Dust");
            s.rectTransform.anchoredPosition = pos;
            var b = new Burst { s = s, p = new Vector2[count], v = new Vector2[count], c = c };
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2);
                b.v[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a) + upBias) * speed * Random.Range(0.4f, 1f);
            }
            _bursts.Add(b);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                p.age += dt;
                float k = p.age / p.life;
                if (k >= 1 || p.t == null) { if (p.t) Destroy(p.t.gameObject); _popups.RemoveAt(i); continue; }
                p.t.rectTransform.anchoredPosition = p.start + new Vector2(0, 70f * (1 - (1 - k) * (1 - k)));
                // lands with a punch: big for an instant, then settles
                float sc = p.age < 0.14f ? Mathf.Lerp(p.punch, 1f, p.age / 0.14f) : 1f;
                p.t.transform.localScale = Vector3.one * sc;
                p.t.transform.localRotation = Quaternion.Euler(0, 0, p.lean * (1 - k * 0.5f));   // it straightens as it drifts up
                var c = p.c; c.a *= k < 0.6f ? 1 : 1 - (k - 0.6f) / 0.4f;
                p.t.color = c;
            }
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var b = _bursts[i];
                b.age += dt;
                if (b.age > 0.55f || b.s == null) { if (b.s) Destroy(b.s.gameObject); _bursts.RemoveAt(i); continue; }
                b.s.Clear();
                var c = b.c; c.a *= 1 - b.age / 0.55f;
                for (int j = 0; j < b.p.Length; j++)
                {
                    b.v[j] *= 1 - 3f * dt;
                    b.v[j].y -= 300f * dt;
                    b.p[j] += b.v[j] * dt;
                    b.s.Poly(new[] { b.p[j], b.p[j] - b.v[j] * 0.035f }, c, 3f, false, false);
                }
            }
        }
    }
}
