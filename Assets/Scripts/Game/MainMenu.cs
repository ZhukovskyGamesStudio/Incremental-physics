using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// The main menu, on the same chalk board as the game: the title with its "demo" tag, a big "Играть", "Выйти"
    /// (not in a browser, where a page cannot close itself) and the sound switch at the side. Faint drawings of the
    /// instruments to come fill the corners.
    public class MainMenu : MonoBehaviour
    {
        const float W = 1920, H = 1080;
        RectTransform _content, _play, _quit, _sound, _full;
        ChalkShape _playBox, _soundIcon, _fullIcon;
        RawImage _bg;
        Text _soundText, _newText, _fullText;
        int _fullShown = -1;
        float _armed = -9;
        System.Action _onPlay;
        float _t;

        public bool Open => gameObject.activeSelf;

        public static MainMenu Build(Transform parent, System.Action onPlay)
        {
            var root = UIF.Fill("Menu", parent);
            var m = root.gameObject.AddComponent<MainMenu>();
            m._onPlay = onPlay;
            m.Make(root);
            return m;
        }

        void Make(RectTransform root)
        {
            // its own piece of board, so nothing of the game shows through
            _bg = root.gameObject.AddComponent<RawImage>();
            _bg.texture = ChalkTex.Board;
            _bg.raycastTarget = true;
            _content = UIF.Rect("Content", root, Vector2.zero, new Vector2(W, H));
            gameObject.AddComponent<ScaleToFit>().Init(_content, W, H);

            // the instruments of the game, sketched faintly around the edges
            var faint = new Color(1, 1, 1, 0.13f);
            (string icon, float x, float y, float k)[] art =
            {
                ("fall", -760, 300, 3.2f), ("pend", 740, 330, 3.0f), ("dyna", -700, -300, 3.0f), ("stand", 700, -320, 2.8f),
                ("spring", -330, -380, 2.2f), ("lamp", 360, -400, 2.2f), ("fly", -880, 20, 2.0f), ("coil", 880, 10, 2.2f),
            };
            foreach (var a in art)
            {   // each drawing on a layer of its own, placed by the layer (the icons take no offset for their boxes)
                var d = UIF.Shape(_content, 801 + (int)a.x, "Doodle");
                d.rectTransform.anchoredPosition = new Vector2(a.x, a.y);
                Lab.DrawIcon(d, a.icon, faint, a.k);
            }
            ChalkTex.Math(UIF.Text(_content, "E = m·g·h", 40, new Color(1, 1, 1, 0.12f), new Vector2(-650, 100), new Vector2(400, 60))).transform.localRotation = Quaternion.Euler(0, 0, 6);
            ChalkTex.Math(UIF.Text(_content, "T = 2π·√(l/g)", 34, new Color(1, 1, 1, 0.1f), new Vector2(660, 110), new Vector2(420, 60))).transform.localRotation = Quaternion.Euler(0, 0, -5);
            ChalkTex.Math(UIF.Text(_content, "I = U / R", 34, new Color(1, 1, 1, 0.1f), new Vector2(-600, -150), new Vector2(360, 60))).transform.localRotation = Quaternion.Euler(0, 0, 4);

            // the title is drawn, not typed (see Logo), with the demo tag pinned to its corner
            var logo = Logo.Build(_content);
            var tag = UIF.Rect("Demo", _content, logo.TagAt, new Vector2(200, 76));
            tag.localRotation = Quaternion.Euler(0, 0, -9);
            var tagBox = UIF.Shape(tag, 803, "Tag");
            tagBox.Rect(Vector2.zero, new Vector2(190, 68), ChalkTex.Yellow, 3.5f);
            tagBox.HatchRect(Vector2.zero, new Vector2(176, 54), new Color(1f, 0.84f, 0.32f, 0.16f), 2f, 9f);
            tagBox.Circle(new Vector2(-78, 0), 5, ChalkTex.Yellow, 2.5f);                         // the tag's hole
            UIF.Text(tag, "demo", 44, ChalkTex.Yellow, new Vector2(10, 2), new Vector2(180, 60));

            // play: big and inviting
            _play = UIF.Rect("Play", _content, new Vector2(0, -30), new Vector2(540, 120));
            UIF.Catcher(_play);
            _playBox = UIF.Shape(_play, 804);
            UIF.Text(_play, "Играть", 64, ChalkTex.Yellow, new Vector2(0, 2), new Vector2(520, 100));
            var pc = _play.gameObject.AddComponent<Clickable>();
            pc.onClick = () => { Sfx.Play("bell", 0.6f, 1.2f); _onPlay?.Invoke(); };
            pc.onHover = h => { _play.localScale = Vector3.one * (h ? 1.06f : 1f); if (h) Sfx.Play("tick", 0.2f, 1.4f); };

            // a new game: asks once more before it wipes the save
            var ng = UIF.Rect("NewGame", _content, new Vector2(0, -170), new Vector2(380, 84));
            UIF.Catcher(ng);
            var nb = UIF.Shape(ng, 807);
            nb.Rect(Vector2.zero, new Vector2(370, 76), new Color(1, 1, 1, 0.5f), 3f);
            _newText = UIF.Text(ng, "Новая игра", 40, new Color(1, 1, 1, 0.75f), new Vector2(0, 2), new Vector2(360, 72));
            var nc = ng.gameObject.AddComponent<Clickable>();
            nc.onClick = () =>
            {
                Sfx.Play("click");
                if (Time.unscaledTime - _armed < 3f) { GameState.DeleteSave(); GameRoot.I.Restart(false); return; }
                _armed = Time.unscaledTime;
            };
            nc.onHover = h => ng.localScale = Vector3.one * (h ? 1.05f : 1f);

            // quit: quieter, and only where a game can close itself
            _quit = UIF.Rect("Quit", _content, new Vector2(0, -280), new Vector2(380, 84));
            UIF.Catcher(_quit);
            var qb = UIF.Shape(_quit, 805);
            qb.Rect(Vector2.zero, new Vector2(370, 76), new Color(1, 1, 1, 0.5f), 3f);
            UIF.Text(_quit, "Выйти", 40, new Color(1, 1, 1, 0.75f), new Vector2(0, 2), new Vector2(360, 72));
            var qc = _quit.gameObject.AddComponent<Clickable>();
            qc.onClick = () => { Sfx.Play("click"); GameState.I.Save(); Application.Quit(); };
            qc.onHover = h => _quit.localScale = Vector3.one * (h ? 1.05f : 1f);
            _quit.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);

            // the sound switch at the side: a chalk speaker, with waves or crossed out
            _sound = UIF.Rect("Sound", _content, new Vector2(-800, -440), new Vector2(300, 90));
            UIF.Catcher(_sound);
            _soundIcon = UIF.Shape(_sound, 806);
            _soundIcon.rectTransform.anchoredPosition = new Vector2(-90, 0);
            _soundText = UIF.Text(_sound, "", 34, ChalkTex.White, new Vector2(78, 0), new Vector2(170, 60), TextAnchor.MiddleLeft);
            var sc = _sound.gameObject.AddComponent<Clickable>();
            sc.onClick = () => { Sfx.Muted = !Sfx.Muted; Sfx.Play("click"); GameState.I.Save(); DrawSound(); };
            sc.onHover = h => _sound.localScale = Vector3.one * (h ? 1.06f : 1f);
            DrawSound();

            // full screen, next to the sound: in a browser the game fills the whole screen by itself (a site's own
            // full-screen button only centres the embed at its page size, with black bars around it)
            _full = UIF.Rect("Fullscreen", _content, new Vector2(-470, -440), new Vector2(340, 90));
            UIF.Catcher(_full);
            _fullIcon = UIF.Shape(_full, 808);
            _fullIcon.rectTransform.anchoredPosition = new Vector2(-130, 0);
            _fullText = UIF.Text(_full, "", 34, ChalkTex.White, new Vector2(38, 0), new Vector2(250, 60), TextAnchor.MiddleLeft);
            var fc = _full.gameObject.AddComponent<Clickable>();
            fc.onClick = () => { Sfx.Play("click"); ScreenMode.Click(); };
            fc.onHover = h => _full.localScale = Vector3.one * (h ? 1.06f : 1f);

            UIF.Text(_content, "Zhukovsky Games", 26, new Color(1, 1, 1, 0.35f), new Vector2(720, -480), new Vector2(360, 40), TextAnchor.MiddleRight);
        }

        void DrawSound()
        {
            var s = _soundIcon; s.Clear();
            var c = Sfx.Muted ? new Color(1, 1, 1, 0.5f) : ChalkTex.White;
            s.Rect(new Vector2(-14, 0), new Vector2(16, 24), c, 3f);
            s.Poly(new[] { new Vector2(-6, 12), new Vector2(12, 26), new Vector2(12, -26), new Vector2(-6, -12) }, c, 3f, true);
            if (Sfx.Muted) { s.Line(new Vector2(22, -12), new Vector2(42, 12), ChalkTex.Red, 3.5f); s.Line(new Vector2(22, 12), new Vector2(42, -12), ChalkTex.Red, 3.5f); }
            else
            {   // two sound waves, each its own dense line (a small chalk arc would come out angular)
                foreach (var (r, a) in new[] { (18f, 1f), (30f, 0.7f) })
                {
                    var pts = new System.Collections.Generic.List<Vector2>();
                    for (int i = 0; i <= 12; i++) { float ang = Mathf.Lerp(-0.85f, 0.85f, i / 12f); pts.Add(new Vector2(12 + Mathf.Cos(ang) * r, Mathf.Sin(ang) * r)); }
                    s.Poly(pts, new Color(c.r, c.g, c.b, c.a * a), 3f, false, false);
                }
            }
            _soundText.text = Sfx.Muted ? "звук выкл" : "звук вкл";
            _soundText.color = Sfx.Muted ? new Color(1, 1, 1, 0.55f) : ChalkTex.White;
        }

        /// Four corners of a screen: pointing out to go full screen, pointing in to come back to a window.
        void DrawFull(bool on)
        {
            var s = _fullIcon; s.Clear();
            var c = ChalkTex.White;
            foreach (var (sx, sy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
            {
                if (!on) s.Poly(new[] { new Vector2(22 * sx, 6 * sy), new Vector2(22 * sx, 16 * sy), new Vector2(10 * sx, 16 * sy) }, c, 3f, false);
                else s.Poly(new[] { new Vector2(20 * sx, 7 * sy), new Vector2(9 * sx, 7 * sy), new Vector2(9 * sx, 16 * sy) }, c, 3f, false);
            }
            _fullText.text = on ? "в окне" : "на весь экран";
        }

        public void Show() { gameObject.SetActive(true); transform.SetAsLastSibling(); DrawSound(); _fullShown = -1; }
        public void Hide() { gameObject.SetActive(false); ScreenMode.Area(null); }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            _newText.text = Time.unscaledTime - _armed < 3f ? "точно? ещё клик" : "Новая игра";
            int full = ScreenMode.On ? 1 : 0;
            if (full != _fullShown) { _fullShown = full; DrawFull(full == 1); }
            ScreenMode.Area(_full);
            _bg.uvRect = new Rect(0, 0, Screen.width / 700f, Screen.height / 700f);   // the board's grain at its usual size
            float p = 0.5f + 0.5f * Mathf.Sin(_t * 3.5f);     // the play button breathes
            _playBox.Clear();
            _playBox.Rect(Vector2.zero, new Vector2(526 + p * 10, 106 + p * 6), ChalkTex.Yellow, 4.5f);
            _playBox.Rect(Vector2.zero, new Vector2(548 + p * 10, 128 + p * 6), new Color(1f, 0.84f, 0.32f, 0.25f), 2.5f);
        }
    }
}
