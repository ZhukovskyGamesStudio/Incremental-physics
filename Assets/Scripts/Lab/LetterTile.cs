using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// A screen that shows letter tiles: gives them a tooltip and receives their drags.
    public interface ILetterHost
    {
        RectTransform Content { get; }
        void Inspect(string id);
        void TileBeginDrag(LetterTile tile, PointerEventData e);
        void TileDrag(PointerEventData e);
        void TileEndDrag(PointerEventData e);
    }

    /// A letter (physical quantity). On the map: a closed letter is a button that opens it for joules; an open one
    /// shows its value and upgrades on click (hold = repeat). On the alchemy screen: a tile to drag onto a question mark.
    public class LetterTile : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float TW = 132, TH = 132;
        public string Id;
        public bool AlchemyMode;
        public float BaseScale = 1f;             // a crowded shelf makes its letters a little smaller
        ILetterHost _host;
        ChalkShape _box;
        Text _sym, _val, _cost;
        bool _pressed, _dragging, _hover;
        float _holdT, _repeatT, _flash, _appear;
        string _boxKey;
        InkText _ink;

        void SetCost(string s) { if (_ink != null) _ink.Set(s, new Color(_cost.color.r, _cost.color.g, _cost.color.b, 1)); else _cost.text = s; }

        GameState G => GameState.I;
        LetterDef D => Defs.L(Id);
        bool Open => G.Known.Contains(Id);

        public void Build(string id, ILetterHost host, bool alchemy)
        {
            Id = id; _host = host; AlchemyMode = alchemy;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(TW, TH);
            UIF.Catcher(transform);
            _box = UIF.Shape(transform, id.GetHashCode() & 0xfff, "Box");
            _sym = ChalkTex.Math(UIF.Text(transform, D.sym, 48, ChalkTex.White, new Vector2(0, alchemy ? 10 : 22), new Vector2(TW + 20, 58)));
            _val = Fit(UIF.Text(transform, "", 20, ChalkTex.Dim, new Vector2(0, alchemy ? -28 : -12), new Vector2(TW - 30, 24)), 12, 20);
            _cost = Fit(UIF.Text(transform, "", 21, ChalkTex.Yellow, new Vector2(0, -33), new Vector2(TW - 48, 22)), 12, 21);
            if (D.openCur == Cur.Obs) { _ink = InkText.On(_cost); _cost.fontSize = Mathf.RoundToInt(18 * ChalkTex.FontScale); }
            _appear = 0;
        }

        /// A label that always fits its box: the font shrinks when the text is long.
        public static Text Fit(Text t, int min, int max)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.RoundToInt(min * ChalkTex.FontScale);
            t.resizeTextMaxSize = Mathf.RoundToInt(max * ChalkTex.FontScale);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        /// Show up after a delay (a batch of new nodes appears one by one).
        public void Appear(float delay) { _appear = -delay * 2.5f; transform.localScale = Vector3.zero; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_appear < 0) { _appear += dt * 2.5f; transform.localScale = Vector3.zero; return; }
            if (_appear < 1)
            {
                _appear = Mathf.Min(1, _appear + dt * 2.5f);
                float s = Mathf.Lerp(0.5f, 1, 1 - (1 - _appear) * (1 - _appear)) * BaseScale;
                transform.localScale = new Vector3(s, s, 1);
            }
            else transform.localScale = Vector3.one * BaseScale * (_hover && !_dragging ? 1.06f : 1f);

            var d = D;
            bool derived = d.Derived, fixedV = d.perSample, open = Open;
            var cc = Apparatus.CurColor((int)d.cost);
            bool afford = false;
            if (AlchemyMode)
            {
                _val.text = d.unit.Length > 0 ? "[" + d.unit + "]" : "[—]";
                _val.color = new Color(0.45f, 0.86f, 1f, 0.8f);
                SetCost("");
            }
            else if (!open)
            {
                afford = G.CanOpen(Id);
                _val.text = "";
                SetCost(d.openCost > 0 ? GameState.FmtPrice(d.openCost, d.openCur) : "открыть");
                var yc = Apparatus.CurColor((int)d.openCur);
                _cost.color = afford ? new Color(yc.r, yc.g, yc.b, 0.8f + 0.2f * Mathf.Sin(Time.time * 5)) : new Color(yc.r, yc.g, yc.b, 0.4f);
            }
            else
            {
                double cost = derived || fixedV || G.AtLimit(Id) ? -1 : G.UpgradeCost(Id);
                afford = cost >= 0 && G.CanUpgrade(Id);
                if (fixedV) { _val.text = "разная"; SetCost(""); }
                else if (derived) { _val.text = G.LetterValue(Id); SetCost(""); }
                else if (!G.UpgradesOpen) { _val.text = G.LetterValue(Id); SetCost(""); }     // levels come with the first joules
                else if (cost < 0) { _val.text = G.LetterValue(Id); SetCost("макс."); _cost.color = ChalkTex.Dim; }
                else
                {
                    _val.text = G.LetterValue(Id);
                    SetCost("↑ " + GameState.Fmt(cost));
                    _cost.color = afford ? new Color(cc.r, cc.g, cc.b, 0.75f + 0.25f * Mathf.Sin(Time.time * 5)) : new Color(cc.r, cc.g, cc.b, 0.35f);
                }
                _val.color = ChalkTex.Dim;
            }
            _sym.color = !open && !AlchemyMode ? new Color(1, 1, 1, 0.45f) : derived || fixedV ? ChalkTex.Cyan : ChalkTex.White;

            string key = $"{AlchemyMode}{open}{derived}{fixedV}{afford}{_hover}";
            if (key != _boxKey)
            {
                _boxKey = key;
                _box.Clear();
                if (!open && !AlchemyMode)
                {   // a closed letter: a dashed button that lights up when affordable
                    var c = afford ? new Color(1f, 0.84f, 0.32f, 0.95f) : new Color(1, 1, 1, 0.4f);
                    if (_hover) c.a = 1;
                    DashedCircle(_box, TW / 2 - 4, c, afford ? 3.5f : 2.5f);
                    if (afford)
                        for (int i = 0; i < 12; i++)
                        {   // ready to open: short strokes around the button, outside it, clear of every label
                            float a = i / 12f * Mathf.PI * 2 + 0.13f; var dr = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            _box.Line(dr * (TW / 2 + 2), dr * (TW / 2 + 9), new Color(c.r, c.g, c.b, 0.55f), 2f);
                        }
                }
                else
                {
                    Color c = derived || fixedV ? new Color(0.45f, 0.86f, 1f, 0.6f) : afford ? new Color(cc.r, cc.g, cc.b, 0.8f) : new Color(1, 1, 1, 0.45f);
                    if (AlchemyMode) c = new Color(1, 1, 1, 0.6f);
                    if (_hover) c.a = 1;
                    _box.Circle(Vector2.zero, TW / 2 - 4, c, _hover ? 3.5f : 2.5f);
                }
            }
            if (_flash > 0) { _flash -= dt * 3; _box.color = Color.Lerp(Color.white, ChalkTex.Green, _flash); }
            else _box.color = Color.white;

            if (_pressed && !_dragging && open && !AlchemyMode)
            {
                _holdT += dt;
                if (_holdT > 0.45f)
                {
                    _repeatT += dt;
                    if (_repeatT > 0.09f) { _repeatT = 0; TryUpgrade(true); }
                }
            }
        }

        static void DashedCircle(ChalkShape s, float r, Color c, float w)
        {
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float a0 = i / (float)n * Mathf.PI * 2, a1 = a0 + Mathf.PI * 2 / n * 0.6f;
                s.Arc(Vector2.zero, r, a0, a1, c, w);
            }
        }

        Vector2 FixedPos => (Vector2)((RectTransform)transform).anchoredPosition * ((RectTransform)transform.parent).localScale.x + (Vector2)((RectTransform)transform.parent).anchoredPosition;

        void TryUpgrade(bool quiet)
        {
            if (D.Derived || D.perSample) return;
            if (G.Upgrade(Id))
            {
                _flash = 1;
                Sfx.Play("up", quiet ? 0.35f : 0.6f, 1f + Mathf.Min(0.5f, G.Level(Id) * 0.01f), 0.03f, 0.03f);
                if (!quiet) FX.I.Dust(_host.Content, FixedPos, ChalkTex.Yellow, 6, 120f);
                if (Id == "g" && !quiet)
                {
                    string[] jokes = { "Земля немного потяжелела!", "Яблоки падают бодрее!", "Ньютон одобряет", "Луна нервно отодвинулась" };
                    FX.I.Text(_host.Content, FixedPos + new Vector2(0, 60), jokes[Random.Range(0, jokes.Length)], ChalkTex.Pink, 24, 1.8f);
                }
                if (!quiet) Buddy.I?.React(Buddy.Mood.Happy, 0.7f);
                _host.Inspect(Id);
            }
            else if (!quiet) Sfx.Play("nope", 0.4f);
        }

        void TryOpen()
        {
            if (G.OpenLetter(Id))
            {
                _flash = 1; _appear = 0;
                Sfx.Play("correct", 0.7f); Sfx.Play("chalk", 0.6f);
                FX.I.Dust(_host.Content, FixedPos, ChalkTex.Yellow, 14, 220f);
                _host.Inspect(Id);
            }
            else { Sfx.Play("nope", 0.4f); FX.I.Text(_host.Content, FixedPos + new Vector2(0, 70), D.openCur == Cur.Cal ? "не хватает килокалорий — проведи эксперимент" : D.openCur == Cur.Obs ? "не хватает идей — проведи эксперимент" : "не хватает джоулей — проведи эксперимент", ChalkTex.Red, 24, 1.6f); }
        }

        public void OnPointerDown(PointerEventData e) { _pressed = true; _dragging = false; _holdT = 0; _repeatT = 0; }

        public void OnPointerUp(PointerEventData e)
        {
            if (_pressed && !_dragging && !AlchemyMode && _holdT <= 0.45f) { if (Open) TryUpgrade(false); else TryOpen(); }
            _pressed = false;
        }

        public void OnBeginDrag(PointerEventData e) { _dragging = true; _pressed = false; _host.TileBeginDrag(this, e); }
        public void OnDrag(PointerEventData e) => _host.TileDrag(e);
        public void OnEndDrag(PointerEventData e) { _dragging = false; _host.TileEndDrag(e); }
        public void OnPointerEnter(PointerEventData e) { _hover = true; _host.Inspect(Id); }
        public void OnPointerExit(PointerEventData e) { _hover = false; _host.Inspect(null); }
    }

    /// A patch of the board texture under a node, aligned with the screen's big board background so it looks like bare board:
    /// it hides whatever is drawn under the node. The owner must be a direct child of the layer that holds the background.
    public class BoardPatch : MonoBehaviour
    {
        RawImage _img;
        RectTransform _rt, _owner;

        public static BoardPatch Add(RectTransform owner, Vector2 size)
        {
            var rt = UIF.Rect("Patch", owner, Vector2.zero, size);
            rt.SetAsFirstSibling();
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = ChalkTex.Board;
            img.raycastTarget = false;
            var p = rt.gameObject.AddComponent<BoardPatch>();
            p._img = img; p._rt = rt; p._owner = owner;
            p.LateUpdate();
            return p;
        }

        public void Resize(Vector2 size) { _rt.sizeDelta = size; LateUpdate(); }

        void LateUpdate()
        {
            var pos = _owner.anchoredPosition + _rt.anchoredPosition;
            var s = _rt.sizeDelta;
            _img.uvRect = new Rect((pos.x - s.x / 2 + Lab.BG / 2) / Lab.Tile, (pos.y - s.y / 2 + Lab.BG / 2) / Lab.Tile, s.x / Lab.Tile, s.y / Lab.Tile);
        }
    }
}
