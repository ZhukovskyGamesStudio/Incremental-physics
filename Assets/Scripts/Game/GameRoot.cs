using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChalkPhysics
{
    public class Clickable : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action onClick;
        public System.Action<bool> onHover;
        public void OnPointerClick(PointerEventData e) => onClick?.Invoke();
        public void OnPointerEnter(PointerEventData e) => onHover?.Invoke(true);
        public void OnPointerExit(PointerEventData e) => onHover?.Invoke(false);
    }

    /// Entry point: builds the whole UGUI scene from code. Three screens: the map (tree of upgrades), the alchemy
    /// (finding formulas) and the lesson (bench). The map and the alchemy are the break; the bell always leads to the map.
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot I;

        [Tooltip("Dev only: multiplies all income. Keep 1 for real play.")]
        public float devSpeed = 1f;
        [Tooltip("Dev only: a bot plays the game to measure pacing (see [BOT] logs).")]
        public bool autoPlayBot;
        [Tooltip("Dev only: Time.timeScale")]
        public float timeScale = 1f;

        enum BreakScreen { Map, Alchemy }

        GameState G;
        Canvas _canvas;
        RectTransform _root, _world, _labRt, _alchemyRt, _machineRt, _topBtns;
        RawImage _board;

        GameObject _dialog;
        Apparatus _apparatus;
        Lab _lab;
        Alchemy _alchemy;
        MainMenu _menu;
        static bool _booted;                          // the splash and the menu greet a fresh start, not a "new game"
        BreakScreen _breakScreen = BreakScreen.Map;
        bool _ending, _dontSave;


        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            gameObject.AddComponent<Sfx>();
            gameObject.AddComponent<Music>();
            FontWarmup.Attach(gameObject);               // the game's own font: letters ready before any label
            GameState.DevSpeed = devSpeed;
            G = new GameState();

            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = ChalkTex.BoardColor; }
            if (FindAnyObjectByType<AudioListener>() == null) (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var cgo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.layer = 5;
            _canvas = cgo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)cgo.transform;

            _board = UIF.Fill("Board", _root).gameObject.AddComponent<RawImage>();
            _board.texture = ChalkTex.Board;
            _board.raycastTarget = false;
            cgo.AddComponent<FX>();

            _world = UIF.Fill("World", _root);
            _labRt = UIF.Fill("Lab", _world);
            _alchemyRt = UIF.Fill("Alchemy", _world);
            _machineRt = UIF.Fill("Machine", _world);
            _lab = _labRt.gameObject.AddComponent<Lab>();
            _lab.Build();
            _alchemy = _alchemyRt.gameObject.AddComponent<Alchemy>();
            _alchemy.Build();
            _apparatus = _machineRt.gameObject.AddComponent<Apparatus>();
            _apparatus.Build();

            // Мел: the chalk buddy in the corner, cheering and wincing along
            var buddy = Buddy.Create(_root);
            G.FormulaProven += id => buddy.React(Buddy.Mood.Joy, 2.6f);
            G.LetterOpened += id => buddy.React(Buddy.Mood.Happy, 1.4f);
            G.DeviceBought += id => buddy.React(Buddy.Mood.Happy, 1.2f);
            G.PerkBought += id => buddy.React(Buddy.Mood.Happy, 1.2f);
            G.GoldFound += s => buddy.React(Buddy.Mood.Wow, 2.2f);
            G.RevolutionDone += d => buddy.React(Buddy.Mood.Joy, 4f);
            G.Changed += buddy.Poke;

            // one drawn key in the corner: Esc, which opens the menu (sound, a new game and the way out live there)
            _topBtns = UIF.Rect("Top", _root, Vector2.zero, new Vector2(104, 72));
            _topBtns.anchorMin = _topBtns.anchorMax = new Vector2(1, 1);
            _topBtns.anchoredPosition = new Vector2(-72, -52);
            EscKey(_topBtns);
            _menu = MainMenu.Build(_world, PlayFromMenu);

            G.FormulaProven += id => { if (Defs.F(id).kind == FKind.Final) StartCoroutine(Ending(false)); };
            G.LessonEnded += OnBell;
            ApplyPhase();
            if (G.Won) StartCoroutine(Ending(true));
            if (autoPlayBot || _booted) _menu.Hide();
            if (autoPlayBot) gameObject.AddComponent<AutoPlayBot>();
            else if (!_booted) Splash.Show(_root, null);   // the studio's mark first, then the menu under it
            _booted = true;
            Time.timeScale = timeScale;
        }

        /// The main menu over whatever screen the game is on; "Играть" lifts it with the usual curtain.
        public void ShowMenu()
        {
            if (_switching || _menu.Open) return;
            Sfx.Play("whoosh", 0.25f, 0.9f);
            StartCoroutine(Switch(() => _menu.Show()));
        }

        void PlayFromMenu()
        {
            if (_switching || !_menu.Open) return;
            StartCoroutine(Switch(() => { _menu.Hide(); if (G.Phase != Phase.Lesson) _lab.FocusFrontier(); }));
        }

        void ApplyPhase()
        {
            bool lesson = G.Phase == Phase.Lesson;
            _machineRt.gameObject.SetActive(lesson);
            _labRt.gameObject.SetActive(!lesson && _breakScreen == BreakScreen.Map);
            _alchemyRt.gameObject.SetActive(!lesson && _breakScreen == BreakScreen.Alchemy);
        }

        public void StartLesson()
        {
            if (G.Phase == Phase.Lesson || _switching || G.Won || !G.AnyStation) return;
            Sfx.Play("whoosh", 0.3f, 0.8f);
            StartCoroutine(Switch(() =>
            {
                G.StartLesson();
                FX.I.Text(_root, new Vector2(0, 300), $"Эксперимент {G.Lessons + 1}", ChalkTex.White, 56, 1.8f);
            }));
        }

        public void ShowAlchemy(string formula = null)
        {
            if (G.Phase == Phase.Lesson || _switching || !G.AlchemyUnlocked) return;
            if (_breakScreen == BreakScreen.Alchemy) { _alchemy.Select(formula); return; }
            Sfx.Play("whoosh", 0.25f, 1.1f);
            StartCoroutine(Switch(() => { _breakScreen = BreakScreen.Alchemy; _alchemy.Select(formula); }));
        }

        public void ShowMap()
        {
            if (G.Phase == Phase.Lesson || _switching || _breakScreen == BreakScreen.Map) return;
            Sfx.Play("whoosh", 0.25f, 0.9f);
            StartCoroutine(Switch(() => { _breakScreen = BreakScreen.Map; _lab.FocusFrontier(); }));
        }

        void OnBell()
        {
            StartCoroutine(Switch(() =>
            {
                _breakScreen = BreakScreen.Map;
                _lab.FocusFrontier();
                Buddy.I?.React(Buddy.Mood.Joy, 2f);
            }));
        }

        bool _switching;
        Image _veil;

        /// A short curtain between screens: the board darkens, the screens swap, it lifts again.
        IEnumerator Switch(System.Action atMidpoint)
        {
            _switching = true;
            if (_veil == null)
            {
                _veil = UIF.Fill("Veil", _root).gameObject.AddComponent<Image>();
                _veil.color = new Color(ChalkTex.BoardColor.r, ChalkTex.BoardColor.g, ChalkTex.BoardColor.b, 0);
            }
            _veil.transform.SetSiblingIndex(_world.GetSiblingIndex() + 1);
            _veil.raycastTarget = true;
            for (float t = 0; t < 0.28f; t += Time.deltaTime) { SetVeil(Mathf.SmoothStep(0, 1, t / 0.28f)); yield return null; }
            SetVeil(1);
            atMidpoint?.Invoke();
            ApplyPhase();
            yield return null;
            for (float t = 0; t < 0.45f; t += Time.deltaTime) { SetVeil(1 - Mathf.SmoothStep(0, 1, t / 0.45f)); yield return null; }
            SetVeil(0);
            _veil.raycastTarget = false;
            _switching = false;
        }

        void SetVeil(float a) { var c = _veil.color; c.a = a; _veil.color = c; }

        /// A keyboard key drawn in chalk: its cap, a lower lip for depth, and "Esc" on it. It sinks when pressed.
        void EscKey(RectTransform rt)
        {
            UIF.Catcher(rt);
            var face = UIF.Rect("Face", rt, Vector2.zero, new Vector2(104, 72));
            var s = UIF.Shape(face, 830);
            var W = new Color(1, 1, 1, 0.8f);
            s.Rect(new Vector2(0, -4), new Vector2(92, 58), new Color(1, 1, 1, 0.35f), 2.5f);   // the key's body, seen below the cap
            s.Rect(new Vector2(0, 4), new Vector2(92, 56), W, 3f);
            s.Line(new Vector2(-46, -24), new Vector2(-42, -32), new Color(1, 1, 1, 0.35f), 2f);
            s.Line(new Vector2(46, -24), new Vector2(42, -32), new Color(1, 1, 1, 0.35f), 2f);
            ChalkTex.Math(UIF.Text(face, "Esc", 30, W, new Vector2(0, 5), new Vector2(90, 44)));
            var c = rt.gameObject.AddComponent<Clickable>();
            c.onClick = () => { Sfx.Play("click"); ShowMenu(); };
            c.onHover = h => face.localScale = Vector3.one * (h ? 1.07f : 1f);
            var press = rt.gameObject.AddComponent<KeyPress>();
            press.face = face;
        }

        /// The key goes down a little under the finger.
        class KeyPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public RectTransform face;
            public void OnPointerDown(PointerEventData e) => face.anchoredPosition = new Vector2(0, -5);
            public void OnPointerUp(PointerEventData e) => face.anchoredPosition = Vector2.zero;
        }

        void Update()
        {
            GameState.DevSpeed = devSpeed;
            G.Tick(Time.deltaTime);
            _board.uvRect = new Rect(0, 0, Screen.width / 700f, Screen.height / 700f);
            ScreenMode.Tick();
            // Мел stands in the corner of the map, and on the floor by the bench during a lesson. In the menu he stands
            // just clear of the buttons: the menu shrinks on a narrow screen and he does not, so his own gap is not scaled
            float fs = _apparatus.FrameScale, half = _root.rect.width / 2;
            Buddy.I?.SetPos(_menu.Open ? new Vector2(300 * fs + 110, -150 * fs)
                          : G.Phase == Phase.Lesson ? _apparatus.BuddyAnchor * fs : new Vector2(Mathf.Min(880, half - 90), -40));
            _topBtns.gameObject.SetActive(!_menu.Open);
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !_menu.Open && _dialog == null) ShowMenu();
        }

        void OnApplicationQuit() { if (!_dontSave) G.Save(); }

        public void Restart(bool keepSave)
        {
            if (keepSave) G.Save(); else _dontSave = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---------------- dialogs ----------------
        RectTransform Overlay(string name, float alpha)
        {
            var p = UIF.Fill(name, _root);
            var bg = p.gameObject.AddComponent<RawImage>();
            bg.texture = ChalkTex.Board;
            bg.color = new Color(1, 1, 1, alpha);
            bg.uvRect = new Rect(0, 0, Screen.width / 700f, Screen.height / 700f);
            bg.raycastTarget = true;
            var c = UIF.Rect("C", p, Vector2.zero, new Vector2(1920, 1080));
            p.gameObject.AddComponent<ScaleToFit>().Init(c, 1920, 1080);
            return c;
        }

        Text DialogButton(RectTransform parent, Vector2 pos, string label, Color col, System.Action onClick)
        {
            var btn = UIF.Rect("Btn", parent, pos, new Vector2(330, 76));
            UIF.Catcher(btn);
            var box = UIF.Shape(btn, 70);
            box.Rect(Vector2.zero, new Vector2(320, 68), col, 4f);
            var t = UIF.Text(btn, label, 32, col, Vector2.zero, new Vector2(320, 64));
            var c = btn.gameObject.AddComponent<Clickable>();
            c.onClick = onClick;
            c.onHover = h => btn.localScale = Vector3.one * (h ? 1.05f : 1f);
            return t;
        }

        public void ShowRevolutionDialog()
        {
            if (_dialog != null || !G.CanRevolt) return;
            Sfx.Play("click");
            var rev = G.NextRevolution;
            string dom = Defs.DomainNames[rev.to];
            var c = Overlay("Revolution", 0.97f);
            _dialog = c.parent.gameObject;
            var fr = UIF.Shape(c, 71);
            fr.Rect(Vector2.zero, new Vector2(1160, 660), ChalkTex.White, 5f);
            fr.Rect(Vector2.zero, new Vector2(1184, 684), new Color(1, 1, 1, 0.35f), 3f);
            UIF.Text(c, "Научная революция!", 60, ChalkTex.Pink, new Vector2(0, 245), new Vector2(1100, 80));
            UIF.Text(c, $"Новая эпоха: <color=#FFD752>{dom}</color>", 40, ChalkTex.White, new Vector2(0, 170), new Vector2(1100, 60));
            var free = new System.Text.StringBuilder();
            foreach (var l in rev.freeLetters) free.Append(free.Length > 0 ? ", " : "").Append(Defs.L(l).sym);
            string body =
                "<color=#FF7373>Сотрётся всё:</color> теории, приборы, буквы, улучшения и валюты.\n" +
                $"<color=#8CFF99>Потом:</color> лаборатория растёт заново из {(rev.to == 1 ? "двух начал" : "трёх начал")} — старые ветки можно прокачать снова, а рядом начинается новая.\n" +
                $"<color=#FFD752>Навсегда:</color> ×{Defs.DomainMult[rev.to]} ко всем опытам. <color=#73DBFF>Сразу:</color> первые величины новой эпохи — {free}.\n\n" +
                (rev.to == 1
                    ? "Весь стенд станет одной электрической схемой: образцы вставляются в её гнёзда, и с каждой новой теорией схема растёт — и просит ещё один образец. Автоматизировать её нельзя. Первый же опыт даст <color=#BF99FF>кулоны</color>."
                    : "Стенд станет паровым: у него появится температура. Каждый нагрев разогревает его, каждое действие остужает — а машина, поршень и лёд работают тем сильнее, чем он горячее. Новая валюта — <color=#FF8C66>килокалории</color>.");
            UIF.Text(c, body, 26, ChalkTex.White, new Vector2(0, -5), new Vector2(1080, 290));
            DialogButton(c, new Vector2(-200, -230), "Революция!", ChalkTex.Yellow, () =>
            {
                Destroy(_dialog); _dialog = null;
                G.Revolt();
                Sfx.Play("revolution", 0.9f);
                StartCoroutine(Wipe(dom));
            });
            DialogButton(c, new Vector2(200, -230), "Позже", ChalkTex.Dim, () => { Sfx.Play("click"); Destroy(_dialog); _dialog = null; });
        }

        IEnumerator Wipe(string dom)
        {
            var e = UIF.Rect("Eraser", _root, new Vector2(-1500, 0), new Vector2(700, 1400));
            var img = e.gameObject.AddComponent<RawImage>();
            img.texture = ChalkTex.Board;
            img.raycastTarget = true;
            var edge = UIF.Shape(e, 72);
            for (int i = 0; i < 30; i++) edge.Line(new Vector2(-350 + Random.Range(-20f, 0), -700 + i * 48), new Vector2(-350 + Random.Range(-60f, -10f), -700 + i * 48 + 30), new Color(1, 1, 1, 0.25f), 3f);
            float t = 0;
            while (t < 1.6f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0, 1, t / 1.6f);
                e.anchoredPosition = new Vector2(Mathf.Lerp(-1500, 1500, k), 0);
                if (Random.value < 0.4f) FX.I.Dust(_root, e.anchoredPosition + new Vector2(-350, Random.Range(-500f, 500f)), ChalkTex.White, 4, 200f);
                yield return null;
            }
            Destroy(e.gameObject);
            FX.I.Text(_root, new Vector2(0, 0), $"Эпоха: {dom}!", ChalkTex.Yellow, 70, 2.8f);
            Sfx.Play("bell");
            _lab.FocusFrontier();
        }

        // ---------------- the ending ----------------
        IEnumerator Ending(bool instant)
        {
            if (_ending) yield break;
            _ending = true;
            var blocker = UIF.Fill("Blocker", _root);
            UIF.Catcher(blocker);

            if (!instant)
            {
                yield return new WaitForSeconds(1.5f);
                Sfx.Play("collapse", 1f);
                float T = 6.2f, t = 0;
                while (t < T)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Pow(t / T, 2.2f);
                    _world.localScale = Vector3.one * Mathf.Max(0.001f, 1 - k);
                    _world.localRotation = Quaternion.Euler(0, 0, k * 900f);
                    _board.color = Color.Lerp(Color.white, new Color(0.35f, 0.35f, 0.35f, 1), k);
                    if (Random.value < 0.5f)
                    {
                        float a = Random.Range(0, Mathf.PI * 2);
                        FX.I.Dust(_root, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(300f, 900f) * (1 - k), ChalkTex.White, 3, 260f);
                    }
                    yield return null;
                }
            }
            _world.gameObject.SetActive(false);
            _board.color = new Color(0.35f, 0.35f, 0.35f, 1);

            var dot = UIF.Shape(_root, 73, "Dot");
            float dt0 = 0;
            while (dt0 < (instant ? 0.2f : 2.2f))
            {
                dt0 += Time.deltaTime;
                dot.Clear();
                dot.Circle(Vector2.zero, 4 + 2 * Mathf.Sin(dt0 * 12), ChalkTex.White, 3f, false);
                yield return null;
            }
            dot.Clear();
            if (!instant) Sfx.Play("bang", 1f);

            var flash = UIF.Fill("Flash", _root).gameObject.AddComponent<Image>();
            flash.raycastTarget = false;
            float f = 0;
            while (f < 1.2f && !instant)
            {
                f += Time.deltaTime;
                flash.color = new Color(1, 1, 1, 1 - f / 1.2f);
                yield return null;
            }
            Destroy(flash.gameObject);
            _board.color = Color.white;

            var c = Overlay("End", 0f);
            string[] lines =
            {
                "<size=64><color=#FFD752>Ψ = F·T·Fа·I·P·η</color></size>",
                "Формула всего собрана.",
                "Вся информация о мире сжалась в одну точку —",
                "и мир наконец объяснил сам себя.",
            };
            for (int i = 0; i < lines.Length; i++)
            {
                var t = UIF.Text(c, lines[i], 40, ChalkTex.White, new Vector2(0, 300 - i * 80 - (i > 0 ? 20 : 0)), new Vector2(1600, 90));
                if (!instant) { yield return Fade(t, 1f); yield return new WaitForSeconds(0.6f); }
            }
            int s = Mathf.FloorToInt(G.PlayTime);
            var stats = UIF.Text(c, $"Время: {s / 3600}:{s / 60 % 60:00}:{s % 60:00}    Экспериментов: {G.Lessons}    Энергии добыто: {GameState.Fmt(G.Total[0])} Дж    Революций: {G.Revolutions}\n\nСпасибо за игру! Это был прототип — новые эпохи физики уже чешут мелом затылок.",
                26, ChalkTex.Dim, new Vector2(0, -80), new Vector2(1500, 140));
            if (!instant) yield return Fade(stats, 1f);
            DialogButton(c, new Vector2(0, -260), "Большой взрыв", ChalkTex.Yellow, () =>
            {
                Sfx.Play("bang");
                GameState.DeleteSave();
                Restart(false);
            });
        }

        IEnumerator Fade(Text t, float dur)
        {
            var c = t.color;
            float x = 0;
            while (x < dur)
            {
                x += Time.deltaTime;
                t.color = new Color(c.r, c.g, c.b, x / dur);
                yield return null;
            }
            t.color = c;
        }
    }

    /// Uniformly scales a fixed-size design container to fit its parent.
    public class ScaleToFit : MonoBehaviour
    {
        RectTransform _target;
        float _w, _h;
        public void Init(RectTransform target, float w, float h) { _target = target; _w = w; _h = h; Apply(); }
        void Update() => Apply();
        void Apply()
        {
            if (_target == null) return;
            var r = ((RectTransform)transform).rect;
            float s = Mathf.Min(r.width / _w, r.height / _h);
            if (s > 0) _target.localScale = new Vector3(s, s, 1);
        }
    }
}
