using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChalkPhysics
{
    /// Мел — a piece of chalk that lives on the board. It cannot talk, but it has a face: it cheers for the player's finds
    /// and records, winces at mistakes, watches the cursor, dozes off when nothing happens and giggles when poked.
    public class Buddy : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public static Buddy I;
        public enum Mood { Idle, Happy, Joy, Wow, Oops, Sleep }

        RectTransform _rt;
        ChalkShape _s;
        Mood _mood = Mood.Idle;
        float _moodT, _idle, _blink, _nextBlink = 2.5f, _hop, _tilt, _spark, _spin, _hoverT;
        int _pokes;
        Vector2 _look;
        bool _hover;

        public static Buddy Create(RectTransform root)
        {
            var rt = UIF.Rect("Buddy", root, new Vector2(880, -40), new Vector2(120, 170));
            var b = rt.gameObject.AddComponent<Buddy>();
            b._rt = rt;
            UIF.Catcher(rt);
            b._s = UIF.Shape(rt, 4242, "Chalk");
            I = b;
            return b;
        }

        /// Where the buddy stands on the current screen.
        public void SetPos(Vector2 p) { _rt.anchoredPosition = Vector2.Lerp(_rt.anchoredPosition, p, 0.35f); }

        /// Something happened: show it. Big moods are not interrupted by small ones.
        public void React(Mood m, float dur = 1.5f)
        {
            if ((_mood == Mood.Joy || _mood == Mood.Wow) && _moodT > 0.4f && (m == Mood.Happy)) return;
            _mood = m; _moodT = dur; _idle = 0;
            if (m == Mood.Joy || m == Mood.Wow) { _hop = 1; _spark = m == Mood.Joy ? 1.2f : 0.6f; FX.I?.Dust(_rt, new Vector2(0, -74), new Color(1, 1, 1, 0.7f), 5, 130f, 0.4f); }
            if (m == Mood.Happy) _hop = Mathf.Max(_hop, 0.4f);
            if (m == Mood.Oops) _tilt = 1;
        }

        /// The player did something: wake up.
        public void Poke() { _idle = 0; if (_mood == Mood.Sleep) _mood = Mood.Idle; }

        public void OnPointerClick(PointerEventData e)
        {   // poked: a giggle, a hop; every fifth poke a full spin
            _pokes++;
            Poke();
            if (_pokes % 5 == 0) { _spin = 1; React(Mood.Wow, 1.4f); Sfx.Play("squeak", 0.5f, 1.2f); }
            else { React(Random.value < 0.5f ? Mood.Joy : Mood.Happy, 1.2f); Sfx.Play("squeak", 0.45f, 0.9f + 0.15f * (_pokes % 3), 0.1f); }
            FX.I.Dust(_rt, new Vector2(0, -20), ChalkTex.White, 8, 160f, 0.5f);
        }
        public void OnPointerEnter(PointerEventData e) { _hover = true; }
        public void OnPointerExit(PointerEventData e) { _hover = false; }

        void Update()
        {
            float dt = Time.deltaTime;
            _idle += dt; _moodT -= dt;
            if (_moodT <= 0 && _mood != Mood.Idle && _mood != Mood.Sleep) _mood = Mood.Idle;
            if (_mood == Mood.Idle && _idle > 28f) _mood = Mood.Sleep;
            _nextBlink -= dt;
            if (_nextBlink <= 0) { _blink = 0.14f; _nextBlink = Random.Range(2.2f, 5f); }
            _blink = Mathf.Max(0, _blink - dt);
            _hop = Mathf.Max(0, _hop - dt * 1.4f);
            _tilt = Mathf.Max(0, _tilt - dt * 0.8f);
            _spark = Mathf.Max(0, _spark - dt);
            _spin = Mathf.Max(0, _spin - dt * 1.6f);
            _hoverT = Mathf.Lerp(_hoverT, _hover ? 1 : 0, dt * 8);
            // the eyes follow the cursor when it is near
            if (Mouse.current != null && _mood != Mood.Sleep)
            {
                var m = UIF.ToLocal(_rt, Mouse.current.position.ReadValue());
                float d = m.magnitude;
                var target = d < 420 ? m.normalized * Mathf.Min(3.5f, d / 40f) : Vector2.zero;
                _look = Vector2.Lerp(_look, target, dt * 6);
            }
            Draw();
        }

        void Draw()
        {
            _s.Clear();
            float t = Time.time;
            var W = ChalkTex.White; var Dim = new Color(1, 1, 1, 0.45f);
            bool sleep = _mood == Mood.Sleep;
            float bob = sleep ? Mathf.Sin(t * 1.2f) * 2 : Mathf.Sin(t * 2.2f) * 3;
            float hop = _hop > 0 ? Mathf.Abs(Mathf.Sin(_hop * Mathf.PI * 2)) * 36 * _hop : 0;
            float ang = _mood == Mood.Oops ? -0.28f * _tilt : sleep ? 0.18f : Mathf.Sin(t * 1.5f) * 0.03f;
            ang += _spin > 0 ? (1 - _spin) * Mathf.PI * 2 : 0;
            float squash = _hop > 0 ? 1 + 0.12f * Mathf.Sin(_hop * Mathf.PI * 4) : 1;
            squash *= 1 + 0.04f * _hoverT;
            Vector2 c = new Vector2(0, bob + hop - 20);
            Vector2 R(float x, float y) { x *= 1 / squash; y *= squash; return c + new Vector2(x * Mathf.Cos(ang) - y * Mathf.Sin(ang), x * Mathf.Sin(ang) + y * Mathf.Cos(ang)); }

            // body: a stick of chalk with a worn, slanted tip
            _s.Poly(new[] { R(-20, -62), R(20, -62), R(20, 52), R(13, 64), R(-9, 62), R(-20, 54) }, W, 3.2f, true);
            _s.Line(R(-20, -50), R(20, -50), Dim, 1.5f);
            _s.Line(R(-14, -58), R(14, -58), Dim, 1.2f);

            // eyes (they look where the cursor is)
            float ey = 22; float lx = _look.x, ly = _look.y;
            bool closed = _blink > 0 || sleep;
            switch (closed ? Mood.Sleep : _mood)
            {
                case Mood.Sleep: _s.Arc(R(-8, ey), 4, Mathf.PI * 1.15f, Mathf.PI * 1.85f, W, 2f); _s.Arc(R(8, ey), 4, Mathf.PI * 1.15f, Mathf.PI * 1.85f, W, 2f); break;
                case Mood.Joy: case Mood.Happy: _s.Arc(R(-8, ey), 5, Mathf.PI * 0.15f, Mathf.PI * 0.85f, W, 2.5f); _s.Arc(R(8, ey), 5, Mathf.PI * 0.15f, Mathf.PI * 0.85f, W, 2.5f); break;
                case Mood.Wow: _s.Circle(R(-8, ey), 6, W, 2.2f); _s.Circle(R(8, ey), 6, W, 2.2f); _s.Circle(R(-8 + lx, ey + ly), 1.5f, W, 2f, false); _s.Circle(R(8 + lx, ey + ly), 1.5f, W, 2f, false); break;
                case Mood.Oops: _s.Line(R(-12, ey + 4), R(-4, ey - 2), W, 2.5f); _s.Line(R(4, ey - 2), R(12, ey + 4), W, 2.5f); break;
                default: _s.Circle(R(-8 + lx, ey + ly), 2.6f, W, 2.5f, false); _s.Circle(R(8 + lx, ey + ly), 2.6f, W, 2.5f, false); break;
            }
            // mouth
            switch (sleep ? Mood.Sleep : _mood)
            {
                case Mood.Joy: _s.Arc(R(0, 8), 10, Mathf.PI * 1.1f, Mathf.PI * 1.9f, W, 2.5f); _s.Line(R(-9, 5), R(9, 5), W, 2.2f); break;
                case Mood.Happy: _s.Arc(R(0, 8), 8, Mathf.PI * 1.15f, Mathf.PI * 1.85f, W, 2.5f); break;
                case Mood.Wow: _s.Circle(R(0, 2), 5.5f, W, 2.3f); break;
                case Mood.Oops: _s.Poly(new[] { R(-8, 2), R(-3, 5), R(3, 1), R(8, 4) }, W, 2.2f); break;
                case Mood.Sleep: _s.Circle(R(2, 3), 3, W, 2f); break;
                default: _s.Arc(R(0, 6), 7 + 2 * _hoverT, Mathf.PI * 1.2f, Mathf.PI * 1.8f, W, 2.3f); break;
            }
            // cheeks when pleased
            if (_mood == Mood.Joy || _mood == Mood.Happy) { var pink = new Color(1f, 0.6f, 0.85f, 0.8f); _s.Circle(R(-15, 10), 2.5f, pink, 2f, false); _s.Circle(R(15, 10), 2.5f, pink, 2f, false); }
            // arms
            Vector2 la, ra;
            switch (_mood)
            {
                case Mood.Joy: la = R(-38, 34 + Mathf.Sin(t * 14) * 5); ra = R(38, 34 - Mathf.Sin(t * 14) * 5); break;
                case Mood.Wow: la = R(-40, 12); ra = R(40, 12); break;
                case Mood.Happy: la = R(-36, 10); ra = R(36, 22); break;
                case Mood.Oops: la = R(-30, -28); ra = R(30, -28); break;
                case Mood.Sleep: la = R(-28, -34); ra = R(28, -34); break;
                default: la = R(-36, -14 + Mathf.Sin(t * 2.2f) * 2 + 20 * _hoverT); ra = R(36, -14 - Mathf.Sin(t * 2.2f) * 2 + 20 * _hoverT); break;
            }
            _s.Line(R(-20, 0), la, W, 2.6f);
            _s.Line(R(20, 0), ra, W, 2.6f);
            // extras
            if (_spark > 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    float a = t * 3 + i * 1.257f; float r = 52 + 10 * Mathf.Sin(t * 5 + i);
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    var col = new Color(1f, 0.84f, 0.32f, _spark);
                    _s.Line(p + new Vector2(-4, 0), p + new Vector2(4, 0), col, 2f); _s.Line(p + new Vector2(0, -4), p + new Vector2(0, 4), col, 2f);
                }
            }
            if (_mood == Mood.Oops) { var d = R(26, 34); _s.Arc(d, 5, Mathf.PI * 1.2f, Mathf.PI * 2.8f, ChalkTex.Cyan, 2f); _s.Line(d + new Vector2(0, 5), d + new Vector2(0, 12), ChalkTex.Cyan, 2f); }
            if (sleep)
            {
                for (int i = 0; i < 3; i++)
                {
                    float k = (t * 0.5f + i * 0.33f) % 1f;
                    var p = c + new Vector2(34 + k * 30, 40 + k * 60);
                    float z = 5 + i * 2; var col = new Color(1, 1, 1, 0.6f * (1 - k));
                    _s.Poly(new[] { p + new Vector2(-z, z), p + new Vector2(z, z), p + new Vector2(-z, -z), p + new Vector2(z, -z) }, col, 1.8f);
                }
            }
        }
    }
}
