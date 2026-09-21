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
        RectTransform _content, _play, _quit, _sound;
        ChalkShape _playBox, _soundIcon;
        RawImage _bg;
        Text _soundText;
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
            ChalkTex.Math(UIF.Text(_content, "I = U / R", 34, new Color(1, 1, 1, 0.1f), new Vector2(560, -170), new Vector2(360, 60))).transform.localRotation = Quaternion.Euler(0, 0, 4);

            // the title, underlined by hand, with the demo tag pinned to its corner
            var title = UIF.Text(_content, "Physics Incremental", 118, ChalkTex.White, new Vector2(0, 250), new Vector2(1500, 150));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            float tw = Mathf.Min(1400, title.preferredWidth);
            var line = UIF.Shape(_content, 802, "Underline");
            line.Line(new Vector2(-tw / 2 - 10, 172), new Vector2(tw / 2 + 20, 176), new Color(1, 1, 1, 0.75f), 4f);
            line.Line(new Vector2(-tw / 2 + 40, 160), new Vector2(tw / 2 - 60, 163), new Color(1, 1, 1, 0.3f), 2.5f);
            var tag = UIF.Rect("Demo", _content, new Vector2(tw / 2 + 70, 330), new Vector2(200, 76));
            tag.localRotation = Quaternion.Euler(0, 0, -9);
            var tagBox = UIF.Shape(tag, 803, "Tag");
            tagBox.Rect(Vector2.zero, new Vector2(190, 68), ChalkTex.Yellow, 3.5f);
            tagBox.HatchRect(Vector2.zero, new Vector2(176, 54), new Color(1f, 0.84f, 0.32f, 0.16f), 2f, 9f);
            tagBox.Circle(new Vector2(-78, 0), 5, ChalkTex.Yellow, 2.5f);                         // the tag's hole
            UIF.Text(tag, "demo", 44, ChalkTex.Yellow, new Vector2(10, 2), new Vector2(180, 60));

            // play: big and inviting
            _play = UIF.Rect("Play", _content, new Vector2(0, -40), new Vector2(540, 120));
            UIF.Catcher(_play);
            _playBox = UIF.Shape(_play, 804);
            UIF.Text(_play, "Играть", 64, ChalkTex.Yellow, new Vector2(0, 2), new Vector2(520, 100));
            var pc = _play.gameObject.AddComponent<Clickable>();
            pc.onClick = () => { Sfx.Play("bell", 0.6f, 1.2f); _onPlay?.Invoke(); };
            pc.onHover = h => { _play.localScale = Vector3.one * (h ? 1.06f : 1f); if (h) Sfx.Play("tick", 0.2f, 1.4f); };

            // quit: quieter, and only where a game can close itself
            _quit = UIF.Rect("Quit", _content, new Vector2(0, -200), new Vector2(380, 92));
            UIF.Catcher(_quit);
            var qb = UIF.Shape(_quit, 805);
            qb.Rect(Vector2.zero, new Vector2(370, 84), new Color(1, 1, 1, 0.6f), 3f);
            UIF.Text(_quit, "Выйти", 46, new Color(1, 1, 1, 0.8f), new Vector2(0, 2), new Vector2(360, 80));
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

            UIF.Text(_content, "Zhukovsky Games", 26, new Color(1, 1, 1, 0.35f), new Vector2(780, -480), new Vector2(360, 40), TextAnchor.MiddleRight);
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

        public void Show() { gameObject.SetActive(true); transform.SetAsLastSibling(); DrawSound(); }
        public void Hide() { gameObject.SetActive(false); }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            _bg.uvRect = new Rect(0, 0, Screen.width / 700f, Screen.height / 700f);   // the board's grain at its usual size
            float p = 0.5f + 0.5f * Mathf.Sin(_t * 3.5f);     // the play button breathes
            _playBox.Clear();
            _playBox.Rect(Vector2.zero, new Vector2(526 + p * 10, 106 + p * 6), ChalkTex.Yellow, 4.5f);
            _playBox.Rect(Vector2.zero, new Vector2(548 + p * 10, 128 + p * 6), new Color(1f, 0.84f, 0.32f, 0.25f), 2.5f);
        }
    }
}
