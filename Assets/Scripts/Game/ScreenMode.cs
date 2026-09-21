using UnityEngine;

namespace ChalkPhysics
{
    /// Full screen or a window. In a browser the page goes full screen from inside the game (see ChalkFullscreen.jslib);
    /// on Windows the game switches between a borderless full screen at the display's own resolution and a window.
    public static class ScreenMode
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void ChalkFullscreenInit();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void ChalkFullscreenArea(float x0, float y0, float x1, float y1);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int ChalkFullscreenIs();
        static bool _init;
        static readonly Vector3[] _c = new Vector3[4];
        public static bool On => ChalkFullscreenIs() == 1;
        public static void Click() { }                 // the page toggles it itself, inside the click
        public static void Tick() { }

        /// Where the full-screen button is on screen right now (null: nowhere, the menu is closed).
        public static void Area(RectTransform rt)
        {
            if (!_init) { _init = true; ChalkFullscreenInit(); }
            if (rt == null || !rt.gameObject.activeInHierarchy || Screen.width <= 0) { ChalkFullscreenArea(0, 0, -1, -1); return; }
            rt.GetWorldCorners(_c);                    // an overlay canvas: world space is screen pixels
            ChalkFullscreenArea(_c[0].x / Screen.width, 1 - _c[2].y / Screen.height, _c[2].x / Screen.width, 1 - _c[0].y / Screen.height);
        }
#else
        public static bool On => Screen.fullScreenMode != FullScreenMode.Windowed;
        public static void Area(RectTransform rt) { }
        static float _next;

        public static void Click()
        {
            var d = Screen.mainWindowDisplayInfo;
            if (d.width <= 0) return;
            if (On) Screen.SetResolution(d.width * 3 / 4, d.height * 3 / 4, FullScreenMode.Windowed);
            else Screen.SetResolution(d.width, d.height, FullScreenMode.FullScreenWindow);
            _next = Time.unscaledTime + 1f;
        }

        /// Alt+Enter keeps the window's resolution, and a full screen smaller than the display is letterboxed with
        /// black bars: a full screen always takes the display's own size.
        public static void Tick()
        {
            if (Application.isEditor || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow) return;
            var d = Screen.mainWindowDisplayInfo;
            if (d.width > 0 && (Screen.width != d.width || Screen.height != d.height))
                Screen.SetResolution(d.width, d.height, FullScreenMode.FullScreenWindow);
        }
#endif
    }
}
