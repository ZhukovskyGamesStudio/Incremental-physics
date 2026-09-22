using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// The alchemy screen: a puzzle. The experiment to come is sketched in chalk, big, in the middle of the board, with
    /// question marks where its quantities belong (an arrow for the pull, a dimension for a height…) and nothing else
    /// but its name. Drag a letter from the shelf onto a question mark: the right one sticks. When every mark is
    /// answered the sketch slides aside and the formula appears beside it with its story; a click brings the next puzzle.
    public class Alchemy : MonoBehaviour, ILetterHost
    {
        public const float LW = 1920, LH = 1080;
        public RectTransform Content { get; private set; }
        static readonly Color Ink = new Color(0.45f, 0.86f, 1f, 0.75f);
        const float RevealX = -470, PanelX = 470;

        readonly Dictionary<string, LetterTile> _tiles = new Dictionary<string, LetterTile>();
        readonly List<PuzzleSlot> _slots = new List<PuzzleSlot>();
        RectTransform _sketchRt, _tip, _startBtn, _mapBtn, _hintBack, _ghost, _navL, _navR, _panel, _catcher;
        ChalkShape _sketch, _tipBox, _startBox, _mapBox, _panelBox;
        CanvasGroup _panelGroup;
        Text _title, _formula, _riddle, _effect, _outputs, _continue, _count, _hint, _ghostText, _tipTitle, _tipBody, _startText, _empty;
        LetterTile _drag;
        PuzzleSlot _hot;
        string _current, _key;
        float _hintT, _tipW, _tipH, _startShake, _revealT;
        bool _tipOn;
        FormulaDef _reveal;                        // the formula just found, shown beside its sketch until a click
        Vector2 _solvePos, _revealPos, _titlePos;  // layout of the current sketch: centred while solving, left once found
        float _solveScale, _revealScale;

        GameState G => GameState.I;

        readonly List<RectTransform> _decor = new List<RectTransform>();   // found theories, pinned around the board
        RectTransform _decorRoot;
        CanvasGroup _decorGroup;

        public void Build()
        {
            var root = (RectTransform)transform;
            Content = UIF.Rect("Content", root, Vector2.zero, new Vector2(LW, LH));
            gameObject.AddComponent<ScaleToFit>().Init(Content, LW, LH);
            UIF.Catcher(Content);
            var bg = UIF.Rect("Bg", Content, Vector2.zero, new Vector2(Lab.BG, Lab.BG)).gameObject.AddComponent<RawImage>();
            bg.texture = ChalkTex.Board;
            bg.uvRect = new Rect(0, 0, Lab.BG / Lab.Tile, Lab.BG / Lab.Tile);
            bg.raycastTarget = false;

            var bc = new Color(0.105f, 0.14f, 0.125f, 0.9f);
            Lab.Backing(Content, new Vector2(0, 500), new Vector2(LW, 80), bc);
            UIF.Text(Content, "Теории", 46, ChalkTex.White, new Vector2(-660, 500), new Vector2(540, 60), TextAnchor.MiddleLeft);
            var ul = UIF.Shape(Content, 12);
            ul.Line(new Vector2(-930, 470), new Vector2(-600, 472), ChalkTex.White, 3f);
            _count = UIF.Text(Content, "", 30, ChalkTex.Yellow, new Vector2(150, 500), new Vector2(520, 60));
            _navL = NavButton(new Vector2(430, 500), "‹", -1);
            _navR = NavButton(new Vector2(500, 500), "›", 1);

            // the sketch: while solving it stands alone in the middle, with only the experiment's name above it
            _sketchRt = UIF.Rect("Sketch", Content, Vector2.zero, Vector2.zero);
            _sketch = UIF.Shape(_sketchRt, 33, "Art");
            _title = UIF.Text(Content, "", 40, ChalkTex.White, new Vector2(0, 235), new Vector2(900, 50));
            _empty = UIF.Text(Content, "Теорий пока нет — открой новые величины в лаборатории", 32, ChalkTex.Dim, new Vector2(0, -40), new Vector2(1200, 60));

            // the story of a found formula, on the right; hidden until the puzzle is solved
            _panel = UIF.Rect("Panel", Content, new Vector2(PanelX, 0), new Vector2(760, 600));
            _panelGroup = _panel.gameObject.AddComponent<CanvasGroup>();
            _panelGroup.alpha = 0; _panelGroup.blocksRaycasts = false; _panelGroup.interactable = false;
            _panelBox = UIF.Shape(_panel, 96);
            _formula = ChalkTex.Math(UIF.Text(_panel, "", 60, ChalkTex.Green, new Vector2(0, 150), new Vector2(740, 80)));
            _riddle = UIF.Text(_panel, "", 24, ChalkTex.Dim, new Vector2(0, 50), new Vector2(720, 100));
            _effect = UIF.Text(_panel, "", 27, ChalkTex.Cyan, new Vector2(0, -45), new Vector2(720, 70));
            _outputs = UIF.Text(_panel, "", 24, new Color(0.45f, 0.86f, 1f, 0.7f), new Vector2(0, -110), new Vector2(720, 56));
            _continue = UIF.Text(_panel, "нажми, чтобы продолжить", 26, ChalkTex.Yellow, new Vector2(0, -200), new Vector2(720, 40));

            _hintBack = Lab.Backing(Content, new Vector2(0, -500), new Vector2(1000, 52), bc);
            _hint = UIF.Text(Content, "", 28, ChalkTex.Cyan, new Vector2(0, -500), new Vector2(980, 56));

            // a click anywhere turns the page after a find (the buttons below stay above it)
            _catcher = UIF.Rect("Continue", Content, Vector2.zero, new Vector2(LW, LH));
            UIF.Catcher(_catcher);
            _catcher.gameObject.AddComponent<Clickable>().onClick = EndReveal;
            _catcher.gameObject.SetActive(false);

            _mapBtn = UIF.Rect("Map", Content, new Vector2(-720, -460), new Vector2(400, 84));
            UIF.Catcher(_mapBtn);
            _mapBox = UIF.Shape(_mapBtn, 62);
            _mapBox.Rect(Vector2.zero, new Vector2(392, 76), ChalkTex.White, 3f);
            UIF.Text(_mapBtn, "← Лаборатория", 36, ChalkTex.White, Vector2.zero, new Vector2(400, 80));
            var mc = _mapBtn.gameObject.AddComponent<Clickable>();
            mc.onClick = () => { Sfx.Play("click"); GameRoot.I.ShowMap(); };
            mc.onHover = h => _mapBtn.localScale = Vector3.one * (h ? 1.05f : 1f);

            _startBtn = UIF.Rect("Start", Content, new Vector2(720, -460), new Vector2(400, 84));
            UIF.Catcher(_startBtn);
            _startBox = UIF.Shape(_startBtn, 60);
            _startText = LetterTile.Fit(UIF.Text(_startBtn, "", 36, ChalkTex.Yellow, Vector2.zero, new Vector2(380, 46)), 22, 36);
            var sc = _startBtn.gameObject.AddComponent<Clickable>();
            sc.onClick = () => { if (!G.AnyStation) { _startShake = 1; Sfx.Play("nope"); return; } Sfx.Play("bell", 0.7f, 1.2f); GameRoot.I.StartLesson(); };
            sc.onHover = h => _startBtn.localScale = Vector3.one * (h ? 1.05f : 1f);

            _ghost = UIF.Rect("Ghost", Content, Vector2.zero, new Vector2(90, 90));
            var gs = UIF.Shape(_ghost, 99);
            gs.Circle(Vector2.zero, 36, ChalkTex.Yellow, 3f);
            _ghostText = UIF.Text(_ghost, "", 44, ChalkTex.Yellow, Vector2.zero, new Vector2(100, 60));
            _ghost.gameObject.SetActive(false);

            _tip = UIF.Rect("Tip", Content, Vector2.zero, new Vector2(460, 120));
            _tip.pivot = new Vector2(0, 1);
            var tipBg = Lab.Backing(_tip, Vector2.zero, Vector2.zero, new Color(0.105f, 0.14f, 0.125f, 0.95f));
            tipBg.anchorMin = Vector2.zero; tipBg.anchorMax = Vector2.one; tipBg.offsetMin = new Vector2(4, 4); tipBg.offsetMax = new Vector2(-4, -4);
            _tipBox = UIF.Shape(_tip, 98);
            _tipTitle = UIF.Text(_tip, "", 30, ChalkTex.White, Vector2.zero, new Vector2(430, 38), TextAnchor.UpperLeft);
            _tipBody = UIF.Text(_tip, "", 24, ChalkTex.Dim, Vector2.zero, new Vector2(430, 90), TextAnchor.UpperLeft);
            _tip.gameObject.SetActive(false);

            G.Changed += Refresh;
            G.FormulaProven += OnDiscovered;
            Refresh();
        }

        RectTransform NavButton(Vector2 pos, string glyph, int dir)
        {
            var rt = UIF.Rect("Nav", Content, pos, new Vector2(56, 56));
            UIF.Catcher(rt);
            var t = UIF.Text(rt, glyph, 48, ChalkTex.Yellow, new Vector2(0, 4), new Vector2(56, 56));
            var c = rt.gameObject.AddComponent<Clickable>();
            c.onClick = () => Step(dir);
            c.onHover = h => rt.localScale = Vector3.one * (h ? 1.15f : 1f);
            return rt;
        }

        void OnDestroy()
        {
            if (G == null) return;
            G.Changed -= Refresh;
            G.FormulaProven -= OnDiscovered;
        }

        void OnDisable() { _reveal = null; _key = null; }

        List<FormulaDef> Avail() => G.Discoverable().ToList();

        /// Open a particular puzzle (from the map's question-mark node), or whatever is on offer.
        public void Select(string formulaId)
        {
            _reveal = null;
            if (formulaId != null && Avail().Any(f => f.id == formulaId)) _current = formulaId;
            _key = null;
            Refresh();
        }

        void Step(int dir)
        {
            var a = Avail();
            if (a.Count < 2 || _reveal != null) return;
            int i = a.FindIndex(f => f.id == _current);
            _current = a[(i + dir + a.Count) % a.Count].id;
            Sfx.Play("chalk", 0.5f);
            _key = null;
            Refresh();
        }

        public void Refresh()
        {
            if (_reveal != null) { RebuildShelf(); Raise(); return; }   // the found formula stays on the board until the click
            var avail = Avail();
            if (_current == null || !avail.Any(f => f.id == _current)) _current = avail.Count > 0 ? avail[0].id : null;
            int filled = _current != null ? Defs.F(_current).Slots().Count(s => G.IsFilled(_current, s)) : 0;
            string key = $"{G.Known.Count}|{G.Proven.Count}|{G.Won}|{_current}|{filled}|{avail.Count}";
            if (key == _key) return;
            _key = key;
            HideTip();
            RebuildShelf();
            RebuildPuzzle(avail);
        }

        float _shelfBottom = 340;                // where the shelf of letters ends: the puzzle starts below it

        /// The shelf of letters: the more letters there are, the smaller they get, bit by bit, so the shelf keeps to
        /// one row as long as it can and never eats the board.
        void RebuildShelf()
        {
            // a quantity kept in the notebook from before a revolution stays off the shelf until the puzzle in
            // front of the player asks for it (the formula of everything does), or its instrument is built again
            var need = _current != null ? new HashSet<string>(Defs.F(_current).Slots()) : new HashSet<string>();
            var letters = G.Known.Where(id => !Defs.L(id).Derived || G.Built(Defs.L(id).derivedFrom) || need.Contains(id)).ToList();
            int n = letters.Count;
            float scale = n <= 10 ? 1f : Mathf.Max(0.6f, 1f - (n - 10) * 0.035f);
            float step = (LetterTile.TW + 14) * scale, rowH = (LetterTile.TH + 8) * scale;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(1820 / step));
            var shown = new HashSet<string>();
            for (int i = 0; i < n; i++)
            {
                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, n - row * perRow);
                PlaceLetter(letters[i], new Vector2((col - (inRow - 1) / 2f) * step, 404 - (1 - scale) * 50 - row * rowH));
                _tiles[letters[i]].BaseScale = scale;
                shown.Add(letters[i]);
            }
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)perRow));
            _shelfBottom = 404 - (1 - scale) * 50 - (rows - 1) * rowH - 58 * scale;
            foreach (var kv in _tiles) kv.Value.gameObject.SetActive(shown.Contains(kv.Key));
        }

        void PlaceLetter(string id, Vector2 pos)
        {
            if (!_tiles.TryGetValue(id, out var tile))
            {
                var rt = UIF.Rect("L_" + id, Content, Vector2.zero, Vector2.one);
                tile = rt.gameObject.AddComponent<LetterTile>();
                tile.Build(id, this, true);
                _tiles[id] = tile;
            }
            ((RectTransform)tile.transform).anchoredPosition = pos;
            tile.gameObject.SetActive(true);
            tile.transform.SetAsLastSibling();
        }

        static Rect Union(Rect b, Vector2 p, float r) => Rect.MinMaxRect(Mathf.Min(b.xMin, p.x - r), Mathf.Min(b.yMin, p.y - r), Mathf.Max(b.xMax, p.x + r), Mathf.Max(b.yMax, p.y + r));

        /// Every theory already found is pinned to the edges of the board — its picture and its formula, slightly askew,
        /// like notes on a wall. The board fills up as the work goes on.
        void RebuildDecor()
        {
            if (_decorRoot == null)
            {   // one layer for all of them, so they can step back as a whole without ever leaving the board
                _decorRoot = UIF.Rect("Found", Content, Vector2.zero, new Vector2(LW, LH));
                _decorGroup = _decorRoot.gameObject.AddComponent<CanvasGroup>();
                _decorGroup.blocksRaycasts = false; _decorGroup.interactable = false;
                _decorRoot.SetSiblingIndex(1);
            }
            foreach (var d in _decor) if (d != null) Destroy(d.gameObject);
            _decor.Clear();
            var found = new List<FormulaDef>();
            foreach (var f in Defs.Formulas) if (G.Built(f.id) && f.kind != FKind.Final && f.id != _current) found.Add(f);
            // scattered by hand, not on a grid: every note has its own place (the same each time), its own tilt and
            // size, and keeps clear of the others; all of them stay faint, the puzzle is what matters
            var placed = new List<Vector2>();
            for (int i = 0; i < found.Count; i++)
            {
                var f = found[i];
                var rnd = new System.Random(f.id.GetHashCode() * 31 + 7);
                Vector2 at = Vector2.zero; float bestGap = -1;
                for (int tries = 0; tries < 40; tries++)
                {
                    bool left = (rnd.Next(2) == 0) ^ (i % 2 == 1);
                    float x = (float)(560 + rnd.NextDouble() * 340) * (left ? -1 : 1);
                    float y = (float)(-340 + rnd.NextDouble() * Mathf.Max(80, _shelfBottom - 80 + 340));
                    var p = new Vector2(x, y);
                    float gap = 1e9f;
                    foreach (var q in placed) gap = Mathf.Min(gap, Vector2.Distance(p, q));
                    if (gap > bestGap) { bestGap = gap; at = p; }
                    if (gap > 190) break;
                }
                placed.Add(at);
                var rt = UIF.Rect("Found_" + f.id, _decorRoot, at, new Vector2(150, 96));
                rt.localRotation = Quaternion.Euler(0, 0, (float)(rnd.NextDouble() * 24 - 12));
                float sc = (float)(0.85 + rnd.NextDouble() * 0.3);
                rt.localScale = new Vector3(sc, sc, 1);
                var ic = UIF.Shape(rt, 200 + i, "Icon");
                ic.rectTransform.anchoredPosition = new Vector2(0, 22);
                Lab.DrawIcon(ic, f.icon ?? "psi", new Color(1, 1, 1, 0.21f), 0.8f);
                var t = UIF.Text(rt, GameState.Symbolic(f), 20, new Color(1, 1, 1, 0.2f), new Vector2(0, -30), new Vector2(210, 30));
                ChalkTex.Math(LetterTile.Fit(t, 12, 20));
                _decor.Add(rt);
            }
        }

        /// Fits a sketch of the given bounds (sketch units) into a box of the board: the origin and the scale to use.
        static void Fit(Rect b, float cx, float top, float bottom, float maxW, float maxScale, out Vector2 pos, out float scale)
        {
            scale = Mathf.Min(maxScale, (top - bottom) / Mathf.Max(1, b.height), maxW / Mathf.Max(1, b.width));
            float y = bottom + ((top - bottom) - b.height * scale) / 2 - b.yMin * scale;
            pos = new Vector2(cx - b.center.x * scale, y);
        }

        void RebuildPuzzle(List<FormulaDef> avail)
        {
            foreach (var s in _slots) if (s != null) Destroy(s.gameObject);
            _slots.Clear();
            _sketch.Clear();
            _hot = null;
            _panelGroup.alpha = 0;
            if (_decorGroup != null) _decorGroup.alpha = 1f;
            _catcher.gameObject.SetActive(false);
            bool any = _current != null;
            _empty.gameObject.SetActive(!any && !G.Won);
            _navL.gameObject.SetActive(avail.Count > 1);
            _navR.gameObject.SetActive(avail.Count > 1);
            _sketchRt.gameObject.SetActive(any);
            if (!any) { _title.text = ""; _count.text = ""; return; }
            var f = Defs.F(_current);
            int idx = avail.FindIndex(x => x.id == _current);
            _count.text = avail.Count > 1 ? $"теория {idx + 1} из {avail.Count}" : "";
            _title.text = f.title;

            // the sketch of the experiment with its question marks
            var art = Puzzles.For(f.id);
            var bounds = Puzzles.Bounds(f);
            if (f.kind != FKind.Final) _sketch.Ground(new Vector2(bounds.xMin, 0), new Vector2(bounds.xMax, 0), new Color(1, 1, 1, 0.5f), 3f, 8f, 16f);
            StationArt.Draw(_sketch, Puzzles.Sketch(f), Vector2.zero, new StationArt.Look { shelfY = 200, pendLen = 200, sketch = true, upTo = f.id });
            foreach (var sa in art)
            {
                switch (sa.kind)
                {
                    case "arrow": _sketch.Arrow(sa.a, sa.b, Ink, 2.5f, 10f); break;
                    case "dim":
                        {
                            _sketch.Line(sa.a, sa.b, Ink, 2f);
                            var d = (sa.b - sa.a).normalized; var n = new Vector2(-d.y, d.x) * 7;
                            _sketch.Line(sa.a - n, sa.a + n, Ink, 2f); _sketch.Line(sa.b - n, sa.b + n, Ink, 2f);
                            break;
                        }
                    case "spin":
                        {
                            float r = sa.b.x;
                            _sketch.Arc(sa.a, r, Mathf.PI * 0.25f, Mathf.PI * 1.05f, Ink, 2.5f);
                            var end = sa.a + new Vector2(Mathf.Cos(Mathf.PI * 0.25f), Mathf.Sin(Mathf.PI * 0.25f)) * r;
                            _sketch.Line(end, end + new Vector2(-9, 4), Ink, 2.5f); _sketch.Line(end, end + new Vector2(2, 10), Ink, 2.5f);
                            break;
                        }
                }
                if (sa.leader) { var dir = (sa.a - sa.at).normalized; _sketch.Line(sa.at + dir * 28, sa.a, Ink, 1.6f); }
                MakeSlot(f, sa.letter, sa.at);
                bounds = Union(bounds, sa.at, 45);
            }
            // a letter of the formula with no place on the sketch waits in a row under it (a safety net)
            var missing = f.Slots().Where(l => !art.Any(a => a.letter == l)).ToList();
            for (int i = 0; i < missing.Count; i++)
            {
                var p = new Vector2((i - (missing.Count - 1) / 2f) * 90, -75);
                MakeSlot(f, missing[i], p);
                bounds = Union(bounds, p, 45);
            }

            // layout: while solving, the sketch fills the board between the shelf of letters and the buttons;
            // once found, it moves to the left half and the formula takes the right
            float titleY = Mathf.Min(235, _shelfBottom - 75);
            _titlePos = new Vector2(0, titleY);
            RebuildDecor();
            Fit(bounds, 0, titleY - 45, -400, 1180, 1.5f, out _solvePos, out _solveScale);
            Fit(bounds, RevealX, titleY + 85, -400, 820, 1.4f, out _revealPos, out _revealScale);
            _sketchRt.anchoredPosition = _solvePos;
            _sketchRt.localScale = new Vector3(_solveScale, _solveScale, 1);
            _title.rectTransform.anchoredPosition = _titlePos;
        }

        PuzzleSlot MakeSlot(FormulaDef f, string letter, Vector2 pos)
        {
            var rt = UIF.Rect("Q_" + letter, _sketchRt, pos, Vector2.one);
            var ps = rt.gameObject.AddComponent<PuzzleSlot>();
            ps.Build(f.id, letter);
            ps.SetBase(pos);
            _slots.Add(ps);
            return ps;
        }

        /// Keeps the click-catcher above the shelf and the buttons above the catcher.
        void Raise()
        {
            if (_reveal == null) return;
            _catcher.SetAsLastSibling();
            _mapBtn.SetAsLastSibling();
            _startBtn.SetAsLastSibling();
            if (_tipOn) _tip.SetAsLastSibling();
        }

        // ---------------- tooltip ----------------
        public void ShowTip(string title, string body)
        {
            _tipOn = true;
            _tip.gameObject.SetActive(true);
            _tip.SetAsLastSibling();
            _tipTitle.text = title;
            _tipBody.text = body;
            const float inner = 470;
            _tipW = inner + 30;
            _tipTitle.rectTransform.sizeDelta = new Vector2(inner, 400);
            _tipBody.rectTransform.sizeDelta = new Vector2(inner, 400);
            float th = Mathf.Max(36, _tipTitle.preferredHeight);       // a long title wraps and pushes the rest down
            float bh = string.IsNullOrEmpty(body) ? 0 : Mathf.Max(28, _tipBody.preferredHeight);
            float top = 12 + th + 8;
            _tipH = top + bh + 16;
            _tip.sizeDelta = new Vector2(_tipW, _tipH);
            _tipTitle.rectTransform.sizeDelta = new Vector2(inner, th + 4);
            _tipTitle.rectTransform.anchoredPosition = new Vector2(0, _tipH / 2 - 12 - th / 2);
            _tipBody.rectTransform.sizeDelta = new Vector2(inner, bh + 4);
            _tipBody.rectTransform.anchoredPosition = new Vector2(0, _tipH / 2 - top - bh / 2);
            _tipBox.Clear();
            _tipBox.Rect(Vector2.zero, new Vector2(_tipW - 4, _tipH - 4), ChalkTex.White, 2.5f);
        }

        public void HideTip() { _tipOn = false; if (_tip != null) _tip.gameObject.SetActive(false); }

        void UpdateTip()
        {
            if (!_tipOn || Mouse.current == null) return;
            var p = UIF.ToLocal(Content, Mouse.current.position.ReadValue()) + new Vector2(18, -18);
            p.x = Mathf.Clamp(p.x, -LW / 2 + 10, LW / 2 - 10 - _tipW);
            p.y = Mathf.Clamp(p.y, -LH / 2 + 10 + _tipH, LH / 2 - 10);
            _tip.anchoredPosition = p;
        }

        /// Tooltip for a letter on the shelf: what it is and where it is already used. The units are the alchemist's clue.
        public void Inspect(string id)
        {
            if (id == null) { HideTip(); return; }
            var d = Defs.L(id);
            string body = d.Derived ? $"Измеряется опытом «{Defs.F(d.derivedFrom).title}»: {GameState.Symbolic(Defs.F(d.derivedFrom))}" : "";
            ShowTip(d.unit.Length > 0 ? $"{d.sym} — {d.name}  [{d.unit}]" : $"{d.sym} — {d.name}", body);
        }

        // ---------------- drag & drop ----------------
        public void TileBeginDrag(LetterTile tile, PointerEventData e)
        {
            _drag = tile;
            HideTip();
            _ghost.gameObject.SetActive(true);
            _ghost.SetAsLastSibling();
            _ghostText.text = Defs.L(tile.Id).sym;
            Sfx.Play("chalk", 0.6f);
            Buddy.I?.Poke();
            TileDrag(e);
        }

        public void TileDrag(PointerEventData e)
        {
            if (_drag == null) return;
            _ghost.anchoredPosition = UIF.ToLocal(Content, e.position);
            SetHot(SlotUnder(e));
        }

        void SetHot(PuzzleSlot s)
        {
            if (s == _hot) return;
            if (_hot != null) _hot.Hot = false;
            _hot = s;
            if (_hot != null) _hot.Hot = true;
        }

        PuzzleSlot SlotUnder(PointerEventData e)
        {
            var go = e.pointerCurrentRaycast.gameObject;
            var s = go ? go.GetComponentInParent<PuzzleSlot>() : null;
            return s != null && !G.IsFilled(s.Formula, s.Letter) ? s : null;
        }

        public void TileEndDrag(PointerEventData e)
        {
            if (_drag == null) return;
            var tile = _drag;
            _drag = null;
            _ghost.gameObject.SetActive(false);
            SetHot(null);
            var slot = SlotUnder(e);
            if (slot == null || _reveal != null) return;
            Vector2 at = UIF.ToLocal(Content, e.position);
            switch (G.Place(slot.Formula, tile.Id, slot.Letter))
            {
                case PlaceResult.Ok:
                    foreach (var s in _slots) if (s != null && s.Letter == slot.Letter) s.Flash();
                    Sfx.Play("place", 0.9f); Sfx.Play("chalk", 0.5f);
                    FX.I.Dust(Content, at, ChalkTex.Cyan, 10, 200f);
                    Buddy.I?.React(Buddy.Mood.Happy, 0.9f);
                    break;
                case PlaceResult.WrongUnit:
                case PlaceResult.WrongSameUnit:
                    // a miss says nothing: the chalk simply crumbles red, and the slot starts spelling out its name
                    slot.Shake(); Sfx.Play("wrong", 0.8f);
                    FX.I.Dust(Content, at, ChalkTex.Red, 18, 260f);
                    FX.I.Dust(Content, slot.transform.parent == _sketchRt ? (Vector2)_sketchRt.anchoredPosition + slot.Base * _sketchRt.localScale.x : at, ChalkTex.Red, 10, 180f);
                    Buddy.I?.React(Buddy.Mood.Oops, 1.6f);
                    break;
            }
        }

        /// The last question mark answered: the sketch slides aside and the formula appears with its story.
        void OnDiscovered(string id)
        {
            var f = Defs.F(id);
            Sfx.Play("bell", 0.6f); Sfx.Play("correct", 0.9f);
            Buddy.I?.React(Buddy.Mood.Joy, 2.6f);
            if (f.kind == FKind.Final || _current != id || !isActiveAndEnabled) return;   // the ending, or a find made elsewhere
            FX.I.Dust(Content, _sketchRt.anchoredPosition + new Vector2(0, 110 * _solveScale), ChalkTex.Green, 30, 380f);
            _reveal = f; _revealT = 0;
            _formula.text = GameState.Symbolic(f);
            _panelBox.Clear();
            _panelBox.Line(new Vector2(-190, 108), new Vector2(190, 108), new Color(0.55f, 1f, 0.6f, 0.35f), 2.5f);   // the find, underlined
            _riddle.text = f.riddle;
            _effect.text = string.IsNullOrEmpty(f.effect) ? "" : f.effect;
            _outputs.text = f.outputs.Length > 0 ? "Новая буква на полке: " + string.Join(", ", f.outputs.Select(o => Defs.L(o).sym + " — " + Defs.L(o).name)) : "";
            _navL.gameObject.SetActive(false); _navR.gameObject.SetActive(false);
            _count.text = "";
            _catcher.gameObject.SetActive(true);
            Raise();
        }

        /// The click after a find: on to the next puzzle, or back to the map when there is none.
        void EndReveal()
        {
            if (_reveal == null) return;
            Sfx.Play("chalk", 0.5f);
            _reveal = null; _key = null;
            _panelGroup.alpha = 0;
            _catcher.gameObject.SetActive(false);
            if (Avail().Count == 0) { GameRoot.I.ShowMap(); return; }
            Refresh();
        }

        // ---------------- per-frame ----------------
        void Update()
        {
            _startShake = Mathf.Max(0, _startShake - Time.deltaTime * 3);
            bool ready = G.AnyStation && !G.Won;
            _startBtn.gameObject.SetActive(ready);
            if (ready)
            {
                _startBtn.anchoredPosition = new Vector2(720 + Mathf.Sin(Time.time * 60) * 8 * _startShake, -460);
                float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 4);
                _startBox.Clear();
                _startBox.Rect(Vector2.zero, new Vector2(392 + p * 8, 76 + p * 4), ChalkTex.Yellow, 4f);
                _startText.text = $"▶ Эксперимент {G.Lessons + 1}  ({G.MaxActions} действий)";
            }

            if (_reveal != null)
            {   // the sketch slides to the left, the story fades in on the right
                _revealT += Time.deltaTime;
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_revealT - 0.4f) / 0.8f));
                _sketchRt.anchoredPosition = Vector2.Lerp(_solvePos, _revealPos, k);
                float s = Mathf.Lerp(_solveScale, _revealScale, k);
                _sketchRt.localScale = new Vector3(s, s, 1);
                _title.rectTransform.anchoredPosition = Vector2.Lerp(_titlePos, new Vector2(PanelX, 250), k);
                _panelGroup.alpha = Mathf.Clamp01((_revealT - 1.0f) / 0.5f);
                if (_decorGroup != null) _decorGroup.alpha = Mathf.Lerp(1f, 0.35f, k);   // the found ones stay, just quieter
                _continue.color = new Color(1f, 0.84f, 0.32f, 0.55f + 0.45f * Mathf.Sin(Time.time * 3));
            }

            UpdateTip();
            _hintT -= Time.deltaTime;
            if (_hintT <= 0) { _hintT = 0.3f; _hint.text = Hint(); _hintBack.gameObject.SetActive(_hint.text.Length > 0); }
            _hint.color = new Color(0.45f, 0.86f, 1f, 0.7f + 0.3f * Mathf.Sin(Time.time * 3));
        }

        /// One hint, for the very first puzzle only.
        string Hint()
        {
            if (G.Won || _current == null || _reveal != null || G.Proven.Count > 0) return "";
            return "перетащи величину с полки на знак вопроса";
        }
    }

    /// A question mark on the sketch: a circle where a letter belongs. It scales with the sketch.
    public class PuzzleSlot : MonoBehaviour
    {
        public string Formula, Letter;
        public bool Hot;
        ChalkShape _box;
        Text _sym, _name;
        float _shake, _flash;
        Vector2 _base;
        string _key;
        int _maskT = -1;               // how many misses the spelled-out hint was built for
        GameState G => GameState.I;

        public void Build(string formula, string letter)
        {
            Formula = formula; Letter = letter;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(58, 58);
            UIF.Catcher(transform);
            _box = UIF.Shape(transform, letter.GetHashCode() & 0xff, "Slot");
            _sym = ChalkTex.Math(UIF.Text(transform, "?", 30, ChalkTex.Yellow, new Vector2(0, 1), new Vector2(120, 50)));
            _name = UIF.Text(transform, "", 21, ChalkTex.Dim, new Vector2(0, -44), new Vector2(320, 28));
        }

        public Vector2 Base => _base;
        public void SetBase(Vector2 pos) { _base = pos; ((RectTransform)transform).anchoredPosition = pos; }
        public void Shake() { _shake = 1; _flash = -1; }
        public void Flash() { _flash = 1; }

        /// After three misses the slot starts spelling out the name of what it wants: three random letters at first,
        /// one more with every further miss, until the whole word is there.
        static string Masked(string name, int revealed, int seed)
        {
            var idx = new List<int>();
            for (int i = 0; i < name.Length; i++) if (name[i] != ' ') idx.Add(i);
            var rnd = new System.Random(seed);
            for (int i = idx.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (idx[i], idx[j]) = (idx[j], idx[i]); }
            var show = new HashSet<int>();
            for (int i = 0; i < Mathf.Min(revealed, idx.Count); i++) show.Add(idx[i]);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++) sb.Append(name[i] == ' ' ? ' ' : show.Contains(i) ? name[i] : '·');
            return sb.ToString();
        }

        void Update()
        {
            bool filled = G.IsFilled(Formula, Letter);
            int wrong = G.WrongCount(Formula, Letter);
            _sym.text = filled ? Defs.L(Letter).sym : "?";
            _sym.color = filled ? ChalkTex.Cyan : Hot ? ChalkTex.Yellow : new Color(1f, 0.84f, 0.32f, 0.85f + 0.15f * Mathf.Sin(Time.time * 4));
            if (filled || wrong < 3) _name.text = "";
            else if (wrong != _maskT) { _maskT = wrong; _name.text = Masked(Defs.L(Letter).name, wrong, Letter.GetHashCode() ^ Formula.GetHashCode()); }
            string key = $"{filled}{Hot}";
            if (key != _key)
            {
                _key = key; _box.Clear();
                var c = filled ? new Color(0.45f, 0.86f, 1f, 0.45f) : Hot ? ChalkTex.Yellow : new Color(1f, 0.84f, 0.32f, 0.8f);
                _box.Circle(Vector2.zero, 25, c, Hot ? 3.5f : 2.5f);
            }
            if (_shake > 0) _shake = Mathf.Max(0, _shake - Time.deltaTime * 3);
            ((RectTransform)transform).anchoredPosition = _base + new Vector2(Mathf.Sin(Time.time * 60) * 7 * _shake, 0);
            // empty slots breathe, waiting; a correct letter makes the slot pop
            float pop = _flash > 0 ? 1 + 0.3f * Mathf.Sin((1 - _flash) * Mathf.PI) : 1;
            float breathe = filled ? 1 : 1 + 0.035f * Mathf.Sin(Time.time * 3 + _base.x * 0.02f);
            transform.localScale = Vector3.one * (pop * breathe * (Hot ? 1.1f : 1f));
            if (_flash > 0) { _flash = Mathf.Max(0, _flash - Time.deltaTime * 2); _box.color = Color.Lerp(Color.white, ChalkTex.Green, _flash); }
            else if (_flash < 0) { _flash = Mathf.Min(0, _flash + Time.deltaTime * 2); _box.color = Color.Lerp(Color.white, ChalkTex.Red, -_flash); }
            else _box.color = Color.white;
        }
    }
}
