using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// Legacy UGUI text cannot hold a picture, so the lightbulb of ideas is drawn by hand into the line: every "¤"
    /// becomes an invisible stretch of glyphs (so the line keeps its width and its alignment) and a chalk bulb is
    /// drawn over it. One line of text; the bulb follows the text's scale, rotation and fading.
    public class InkText : MonoBehaviour
    {
        const string Gap = "<color=#00000000>M</color>";
        Text _t;
        ChalkShape _s;
        string _raw;
        Color _bulb = ChalkTex.Cyan;
        readonly List<float> _at = new List<float>();
        float _w, _y;
        bool _dirty;

        public Text Label => _t;

        public static InkText On(Text t)
        {
            var ink = t.GetComponent<InkText>();
            if (ink != null) return ink;
            ink = t.gameObject.AddComponent<InkText>();
            ink._t = t;
            t.supportRichText = true;
            t.resizeTextForBestFit = false;
            // one line, never shrunk: a line taller than its box (a glyph from a fallback font, like the ↑ Ink Free
            // lacks, brings a taller line) would otherwise not be drawn at all
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            ink._s = UIF.Shape(t.transform, 5, "Bulbs");
            return ink;
        }

        /// Sets the line; "¤" marks where a lightbulb goes.
        public void Set(string raw) => Set(raw, ChalkTex.Cyan);
        public void Set(string raw, Color bulb)
        {
            if (raw == _raw && bulb == _bulb) return;
            _raw = raw ?? ""; _bulb = bulb;
            _t.text = ChalkTex.Sym(_raw).Replace(GameState.Bulb, Gap);
            _dirty = true;
            Layout();
        }

        float Width(string s)
        {
            if (s.Length == 0) return 0;
            var set = _t.GetGenerationSettings(Vector2.zero);
            return _t.cachedTextGeneratorForLayout.GetPreferredWidth(s, set) / Mathf.Max(0.01f, _t.pixelsPerUnit);
        }

        /// A cut-off piece of rich text with its open tags closed again: an unclosed tag would be measured as letters.
        static string Closed(string s)
        {
            var open = new List<string>();
            int k = 0;
            while ((k = s.IndexOf('<', k)) >= 0)
            {
                int e = s.IndexOf('>', k);
                if (e < 0) break;
                string tag = s.Substring(k + 1, e - k - 1);
                if (tag.StartsWith("/")) { if (open.Count > 0) open.RemoveAt(open.Count - 1); }
                else { int q = tag.IndexOf('='); open.Add(q >= 0 ? tag.Substring(0, q) : tag); }
                k = e + 1;
            }
            for (int i = open.Count - 1; i >= 0; i--) s += "</" + open[i] + ">";
            return s;
        }

        /// Where the bulbs go: measured against the text itself, so the icon sits exactly in its gap.
        void Layout()
        {
            _at.Clear();
            if (_t.font == null) return;
            string shown = _t.text;
            float line = Width(shown), gap = Width(Gap);
            var r = _t.rectTransform.rect;
            float start;
            switch (_t.alignment)
            {
                case TextAnchor.UpperLeft: case TextAnchor.MiddleLeft: case TextAnchor.LowerLeft: start = r.xMin; break;
                case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: start = r.xMax - line; break;
                default: start = r.center.x - line / 2; break;
            }
            float lh = _t.fontSize * _t.lineSpacing * 1.15f;
            switch (_t.alignment)
            {
                case TextAnchor.UpperLeft: case TextAnchor.UpperCenter: case TextAnchor.UpperRight: _y = r.yMax - lh / 2; break;
                case TextAnchor.LowerLeft: case TextAnchor.LowerCenter: case TextAnchor.LowerRight: _y = r.yMin + lh / 2; break;
                default: _y = r.center.y; break;
            }
            int from = 0;
            while (true)
            {
                int i = shown.IndexOf(Gap, from, System.StringComparison.Ordinal);
                if (i < 0) break;
                float x = start + Width(Closed(shown.Substring(0, i)) + Gap) - gap;
                _at.Add(x + gap / 2);
                from = i + Gap.Length;
            }
            _w = gap;
        }

        void LateUpdate()
        {
            if (_t == null) return;
            if (_dirty) { _dirty = false; Layout(); }
            _s.Clear();
            if (_at.Count == 0) return;
            float k = Mathf.Clamp(_t.fontSize / 34f, 0.4f, 1.3f);
            foreach (var x in _at) Lab.DrawIcon(_s, "bulb", _bulb, k, new Vector2(x, _y + 1));
            var c = _t.color;
            _s.color = new Color(1, 1, 1, c.a);
        }

        /// Re-measures after the text's box or font size changed.
        public void Refresh() { _dirty = true; }
    }
}
