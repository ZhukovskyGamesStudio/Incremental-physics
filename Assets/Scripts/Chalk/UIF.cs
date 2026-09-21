using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// Tiny UGUI factory so the whole game can be built from code.
    public static class UIF
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        public static RectTransform Fill(string name, Transform parent) => Stretch(name, parent, Vector2.zero, Vector2.one);

        public static Text Text(Transform parent, string s, int size, Color c, Vector2 pos, Vector2 box, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect("T", parent, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = ChalkTex.Font;
            t.fontSize = Mathf.RoundToInt(size * ChalkTex.FontScale);
            t.color = c;
            t.alignment = align;
            t.text = ChalkTex.Sym(s);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        public static ChalkShape Shape(Transform parent, int seed = 1, string name = "Chalk")
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero);
            var s = rt.gameObject.AddComponent<ChalkShape>();
            s.seed = seed;
            s.raycastTarget = false;
            return s;
        }

        /// Invisible raycast target.
        public static Image Catcher(Transform parent)
        {
            var img = parent.gameObject.GetComponent<Image>();
            if (img == null) img = parent.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = true;
            return img;
        }

        public static Vector2 ToLocal(RectTransform rt, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, null, out var p);
            return p;
        }
    }
}
