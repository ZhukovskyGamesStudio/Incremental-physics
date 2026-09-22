using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics.EditorTools
{
    /// Art for the store page, drawn by the game itself, in play mode, driven step by step from outside (MCP):
    /// Begin(w, h) moves every canvas onto a camera that draws into a w×h picture, as if the screen were that size;
    /// a frame later Snap(name) saves what it shows; End() puts everything back. Solo(...) shows one composition
    /// alone (the logo, the buddy, a sheet of instrument drawings) instead of the game. Pictures go to Build/ItchArt.
    /// Background(black or white) twice for the same still gives a picture with its alpha (URP drops the alpha of a
    /// camera texture, so it is worked out from the two, outside).
    public static class ItchArt
    {
        static RenderTexture _rt;
        static Camera _cam;
        static readonly List<(Canvas c, RenderMode mode, Camera cam, float dist)> _saved = new List<(Canvas, RenderMode, Camera, float)>();
        static GameObject _solo;
        static Buddy _buddyWas;

        public static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "ItchArt"));

        public static string Begin(int w, int h)
        {
            End();
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "ItchArt", antiAliasing = 1 };
            _rt.Create();
            var go = new GameObject("ItchArtCamera");
            _cam = go.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = ChalkTex.BoardColor;
            _cam.orthographic = true;
            _cam.cullingMask = ~0;
            _cam.depth = -100;
            _cam.targetTexture = _rt;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (!c.isRootCanvas) continue;
                _saved.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = _cam;
                c.planeDistance = 100;
            }
            return $"capturing {w}x{h}, {_saved.Count} canvases";
        }

        public static string Background(bool white)
        {
            if (_cam == null) return "not capturing";
            _cam.backgroundColor = white ? Color.white : Color.black;
            return "background " + (white ? "white" : "black");
        }

        public static string Snap(string name)
        {
            if (_rt == null) return "not capturing";
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Dir);
            var path = Path.Combine(Dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        /// One composition alone on the capture camera, the game's canvases hidden. what: "logo", "buddy:Joy",
        /// "icons:fall,pend,..." (a grid, cells of `cell` px; the order is written to icons.txt beside the picture).
        public static string Solo(string what, float scale = 1f, float cell = 260f, float k = 4f)
        {
            if (_cam == null) return "not capturing";
            Unsolo();
            foreach (var s in _saved) s.c.enabled = false;
            _solo = new GameObject("ItchArtSolo", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = _solo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 100;
            var scaler = _solo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scale;
            var root = UIF.Fill("Content", _solo.transform);
            var parts = what.Split(':');
            switch (parts[0])
            {
                case "logo":
                    Logo.Build(root);
                    break;
                case "buddy":
                    _buddyWas = Buddy.I;
                    var b = Buddy.Create(root);
                    ((RectTransform)b.transform).anchoredPosition = Vector2.zero;
                    b.enabled = true;
                    if (parts.Length > 1 && System.Enum.TryParse(parts[1], out Buddy.Mood m)) b.React(m, 999f);
                    break;
                case "icons":
                    var names = parts[1].Split(',');
                    int cols = Mathf.Max(1, Mathf.FloorToInt(_rt.width / scale / cell));
                    var list = new StringBuilder();
                    for (int i = 0; i < names.Length; i++)
                    {
                        float x = -_rt.width / scale / 2 + cell * (i % cols + 0.5f), y = _rt.height / scale / 2 - cell * (i / cols + 0.5f);
                        var d = UIF.Shape(root, 900 + i, names[i]);
                        d.rectTransform.anchoredPosition = new Vector2(x, y);
                        Lab.DrawIcon(d, names[i], ChalkTex.White, k);
                        list.AppendLine($"{names[i]} {(x * scale + _rt.width / 2f):0} {(_rt.height / 2f - y * scale):0} {cell * scale:0}");
                    }
                    Directory.CreateDirectory(Dir);
                    File.WriteAllText(Path.Combine(Dir, "icons.txt"), list.ToString());
                    break;
                default:
                    return "unknown composition " + parts[0];
            }
            return "solo " + what;
        }

        public static string Unsolo()
        {
            if (_solo != null) Object.Destroy(_solo);
            _solo = null;
            if (_buddyWas != null) { Buddy.I = _buddyWas; _buddyWas = null; }
            foreach (var s in _saved) if (s.c != null) s.c.enabled = true;
            return "game shown";
        }

        static int _recLeft, _recIdx;
        static string _recName;

        /// A run of frames for an animation: the game's clock steps exactly 1/fps per frame (Time.captureFramerate),
        /// so the frames play back at fps whatever the editor's speed. Frames are name_000.png, name_001.png, ...
        public static string Record(string name, int frames, int fps)
        {
            if (_cam == null) return "not capturing";
            _recName = name; _recLeft = frames; _recIdx = 0;
            Time.captureFramerate = fps;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnCamera;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnCamera;
            return $"recording {frames} frames";
        }

        public static string Recorded => _recLeft > 0 ? $"{_recIdx} so far" : $"done, {_recIdx} frames";

        static void OnCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera c)
        {
            if (c != _cam || _recLeft <= 0) return;
            Snap($"{_recName}_{_recIdx:000}");
            _recIdx++;
            if (--_recLeft > 0) return;
            Time.captureFramerate = 0;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnCamera;
        }

        /// The board texture itself, tileable.
        public static string Board()
        {
            Directory.CreateDirectory(Dir);
            var path = Path.Combine(Dir, "board_tile.png");
            File.WriteAllBytes(path, ChalkTex.Board.EncodeToPNG());
            return path;
        }

        public static string End()
        {
            Unsolo();
            foreach (var s in _saved)
            {
                if (s.c == null) continue;
                s.c.renderMode = s.mode;
                s.c.worldCamera = s.cam;
                s.c.planeDistance = s.dist;
            }
            _saved.Clear();
            if (_cam != null) Object.Destroy(_cam.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            _cam = null;
            _rt = null;
            return "done";
        }
    }
}
