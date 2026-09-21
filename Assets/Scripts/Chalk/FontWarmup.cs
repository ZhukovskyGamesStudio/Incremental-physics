using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// The web build draws its text with a font file of its own, whose glyph atlas grows as bigger letters are asked
    /// for. The letters are rasterised up front, and whenever the atlas is rebuilt anyway every label is redrawn on the
    /// next frame, so no text is left blank by a rebuild that happened while it was being laid out.
    public class FontWarmup : MonoBehaviour
    {
        const string Chars = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
                             "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
                             " .,:;!?%()[]{}+-×·²³½≈→↑←►√«»—–/\\\"'<>=_μρωηλΔΨ";
        bool _dirty;
        int _frames;

        public static void Attach(GameObject host)
        {
            if (!ChalkTex.WebFont) return;
            var w = host.AddComponent<FontWarmup>();
            var f = ChalkTex.Font;
            if (f == null) return;
            foreach (int size in new[] { 10, 13, 16, 20, 24, 28, 34, 40, 48, 60 }) f.RequestCharactersInTexture(Chars, size, FontStyle.Normal);
            var m = ChalkTex.MathFont;
            if (m != null && m != f) foreach (int size in new[] { 10, 14, 18, 24, 30, 40, 50 }) m.RequestCharactersInTexture(Chars, size, FontStyle.Normal);
            Font.textureRebuilt += w.OnRebuilt;
        }

        void OnDestroy() { Font.textureRebuilt -= OnRebuilt; }

        void OnRebuilt(Font f) { if (f == ChalkTex.Font || f == ChalkTex.MathFont) _dirty = true; }

        void LateUpdate()
        {
            // a few early frames get a full redraw regardless: the first layout happens while the atlas is still growing
            _frames++;
            if (!_dirty && _frames != 3 && _frames != 30) return;
            _dirty = false;
            foreach (var t in FindObjectsByType<Text>(FindObjectsSortMode.None)) t.SetAllDirty();
        }
    }
}
