using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// The map of physics: a tree of buttons that grows out of itself. The trunk runs left to right in school order
    /// (letters to open for joules, formulas found in the alchemy, revolutions); a formula's upgrades branch up or down
    /// from it, one node at a time. Lines only join neighbouring nodes, so nothing ever crosses. Drag to pan.
    public class Lab : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ILetterHost
    {
        public const float LW = 1920, LH = 1080;
        public const float BG = 24000, Tile = 700;   // pannable board texture: size and tile period (design units)
        const float NODE = 112, SMALL = 92;
        public RectTransform Content { get; private set; }   // fixed layer (design 1920x1080)
        RectTransform _board;                                 // pannable layer inside Content

        readonly Dictionary<string, LetterTile> _tiles = new Dictionary<string, LetterTile>();
        readonly Dictionary<string, RectTransform> _nodes = new Dictionary<string, RectTransform>();
        readonly HashSet<string> _seen = new HashSet<string>();
        bool _everBuilt;
        int _newIndex;
        readonly Text[] _cur = new Text[4];
        Text _hint, _startText, _alchText, _tipTitle, _tipBody, _tipStat;
        InkText _tipInk;
        RectTransform _startBtn, _alchBtn, _tip, _hintBack, _startBack, _alchBack;
        ChalkShape _edges, _startBox, _alchBox, _tipBox, _curIcons;
        string _treeKey;
        Vector2 _pan, _panTarget;
        float _hintT, _startShake, _alchShake, _tipW, _tipH, _minX, _maxX, _minY, _maxY;
        float _zoom = 1, _zoomT = 1;                          // the map's own zoom (mouse wheel)
        readonly double[] _shownCur = new double[4], _curTarget = new double[4];   // counters tick up to their value
        readonly float[] _curFlash = new float[4];
        bool _panning, _tipOn;

        GameState G => GameState.I;
        static readonly Color Lit = new Color(1, 1, 1, 0.55f), Dim = new Color(1, 1, 1, 0.3f);

        public void Build()
        {
            var root = (RectTransform)transform;
            Content = UIF.Rect("Content", root, Vector2.zero, new Vector2(LW, LH));
            gameObject.AddComponent<ScaleToFit>().Init(Content, LW, LH);
            UIF.Catcher(Content);

            _board = UIF.Rect("Board", Content, Vector2.zero, Vector2.zero);
            // the board texture pans with the map, so it feels like a camera over a real board
            var bg = UIF.Rect("Bg", _board, Vector2.zero, new Vector2(BG, BG)).gameObject.AddComponent<RawImage>();
            bg.texture = ChalkTex.Board;
            bg.uvRect = new Rect(0, 0, BG / Tile, BG / Tile);
            bg.raycastTarget = false;
            _edges = UIF.Shape(_board, 11, "Edges");

            // dark backing under the fixed HUD so map nodes never bleed through it
            var bc = new Color(0.105f, 0.14f, 0.125f, 0.9f);
            Backing(Content, new Vector2(0, 500), new Vector2(LW, 80), bc);
            _startBack = Backing(Content, new Vector2(720, -460), new Vector2(420, 96), bc);
            _alchBack = Backing(Content, new Vector2(-720, -460), new Vector2(420, 96), bc);
            _hintBack = Backing(Content, new Vector2(0, -500), new Vector2(1000, 52), bc);

            UIF.Text(Content, "Лаборатория", 46, ChalkTex.White, new Vector2(-700, 500), new Vector2(460, 60), TextAnchor.MiddleLeft);
            var ul = UIF.Shape(Content, 12);
            ul.Line(new Vector2(-930, 470), new Vector2(-630, 472), ChalkTex.White, 3f);
            for (int i = 0; i < 4; i++)
                _cur[i] = UIF.Text(Content, "", 34, Apparatus.CurColor(i), Vector2.zero, new Vector2(250, 60));
            _curIcons = UIF.Shape(Content, 13, "CurIcons");

            _hint = UIF.Text(Content, "", 28, ChalkTex.Cyan, new Vector2(0, -500), new Vector2(980, 56));

            _startBtn = UIF.Rect("Start", Content, new Vector2(720, -460), new Vector2(400, 84));
            UIF.Catcher(_startBtn);
            _startBox = UIF.Shape(_startBtn, 60);
            _startText = LetterTile.Fit(UIF.Text(_startBtn, "", 36, ChalkTex.Yellow, Vector2.zero, new Vector2(380, 46)), 22, 36);
            var sc = _startBtn.gameObject.AddComponent<Clickable>();
            sc.onClick = () => { if (!G.AnyStation) { _startShake = 1; Sfx.Play("nope"); return; } Sfx.Play("bell", 0.7f, 1.2f); GameRoot.I.StartLesson(); };
            sc.onHover = h => _startBtn.localScale = Vector3.one * (h ? 1.05f : 1f);

            _alchBtn = UIF.Rect("Alchemy", Content, new Vector2(-720, -460), new Vector2(400, 84));
            UIF.Catcher(_alchBtn);
            _alchBox = UIF.Shape(_alchBtn, 61);
            _alchText = UIF.Text(_alchBtn, "⚗ Теории", 36, ChalkTex.Cyan, Vector2.zero, new Vector2(400, 80));
            var ac = _alchBtn.gameObject.AddComponent<Clickable>();
            ac.onClick = () => { if (!G.AlchemyUnlocked) { _alchShake = 1; Sfx.Play("nope"); return; } Sfx.Play("click"); GameRoot.I.ShowAlchemy(); };
            ac.onHover = h => _alchBtn.localScale = Vector3.one * (h ? 1.05f : 1f);

            // tooltip (fixed layer, follows the cursor; pivot = top-left corner)
            _tip = UIF.Rect("Tip", Content, Vector2.zero, new Vector2(460, 120));
            _tip.pivot = new Vector2(0, 1);
            var tipBg = Backing(_tip, Vector2.zero, Vector2.zero, new Color(0.105f, 0.14f, 0.125f, 0.95f));
            tipBg.anchorMin = Vector2.zero; tipBg.anchorMax = Vector2.one; tipBg.offsetMin = new Vector2(4, 4); tipBg.offsetMax = new Vector2(-4, -4);
            _tipBox = UIF.Shape(_tip, 98);
            _tipTitle = UIF.Text(_tip, "", 30, ChalkTex.White, Vector2.zero, new Vector2(430, 38), TextAnchor.UpperLeft);
            _tipBody = UIF.Text(_tip, "", 24, ChalkTex.Dim, Vector2.zero, new Vector2(430, 90), TextAnchor.UpperLeft);
            // the last line of a tip is its price or its yield, written the same way as everywhere: number, then unit
            _tipStat = UIF.Text(_tip, "", 25, ChalkTex.Yellow, Vector2.zero, new Vector2(430, 34), TextAnchor.MiddleLeft);
            _tipInk = InkText.On(_tipStat);
            _tip.gameObject.SetActive(false);

            G.Changed += Refresh;
            G.LetterOpened += OnLetterOpened;
            G.GateBought += OnGateBought;
            Refresh();
            _pan = _panTarget = FrontierPan();
        }

        void OnDestroy()
        {
            if (G == null) return;
            G.Changed -= Refresh;
            G.LetterOpened -= OnLetterOpened;
            G.GateBought -= OnGateBought;
        }

        // ---------------- camera ----------------
        float Top => LH / 2 - 90;
        float Bottom => -LH / 2 + 110;
        Vector2 _lastSlot, _lastDir;
        int _slotCount;

        /// The pan that shows the frontier of the tree: its newest node a little ahead of centre (a young tree sits centred).
        Vector2 FrontierPan()
        {
            Vector2 c = _slotCount <= 3 || _wide ? new Vector2((_minX + _maxX) / 2, (_minY + _maxY) / 2) : _lastSlot - _lastDir * 150;
            return ClampPan(-c * _zoomT + new Vector2(0, (Top + Bottom) / 2));
        }

        public void FocusFrontier() { _panTarget = FrontierPan(); }

        /// The map can be dragged anywhere as long as it stays on screen (with a little slack), never into the void.
        Vector2 ClampPan(Vector2 p)
        {
            float minx = _minX * _zoom - 80, maxx = _maxX * _zoom + 80, miny = _minY * _zoom - 80, maxy = _maxY * _zoom + 80;
            float left = -LW / 2 + 40, right = LW / 2 - 40, slack = 160f;
            float a = left - minx, b = right - maxx;
            p.x = Mathf.Clamp(p.x, Mathf.Min(a, b) - slack, Mathf.Max(a, b) + slack);
            float c = Bottom - miny, d = Top - maxy;
            p.y = Mathf.Clamp(p.y, Mathf.Min(c, d) - slack, Mathf.Max(c, d) + slack);
            return p;
        }

        public void Refresh()
        {
            string key = $"{G.Lessons}|{G.Known.Count}|{G.Proven.Count}|{G.Devices.Count}|{G.Perks.Count}|{G.Gates.Count}|{G.Revolutions}|{G.Won}";
            if (key != _treeKey) { _treeKey = key; BuildTree(); }
        }

        // ---------------- the tree: a rectangular spiral growing out of the centre ----------------
        const float GAP = 900, STEP = 190, CHAIN = 120, CORNER = 150;
        static float Radius(string code) => Defs.Kind(code) == 'L' ? LetterTile.TW / 2 : NODE / 2;

        class Slot { public string code, inward; public Vector2 pos, dir, n; public int seg, layer; }

        static List<Vector2> _path;
        static List<float> _cum;

        /// The trunk's path: a rectangular spiral, counter-clockwise, one ring GAP further out per turn.
        static void BuildPath()
        {
            _path = new List<Vector2> { Vector2.zero };
            _cum = new List<float> { 0 };
            Vector2[] dirs = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
            var p = Vector2.zero; float len = 0;
            for (int i = 0; i < 12; i++) { float L = GAP * (i / 2 + 1); p += dirs[i % 4] * L; len += L; _path.Add(p); _cum.Add(len); }
        }

        static void PathAt(float s, out Vector2 p, out Vector2 dir, out int seg)
        {
            seg = 0;
            while (seg < _cum.Count - 2 && s >= _cum[seg + 1]) seg++;
            dir = (_path[seg + 1] - _path[seg]).normalized;
            p = _path[seg] + dir * (s - _cum[seg]);
        }

        void Bounds(Vector2 p) { _minX = Mathf.Min(_minX, p.x); _maxX = Mathf.Max(_maxX, p.x); _minY = Mathf.Min(_minY, p.y); _maxY = Mathf.Max(_maxY, p.y); }

        const float TreeGap = 3400;                         // the epochs' trees stand side by side, never joined
        readonly List<GameObject> _titles = new List<GameObject>();
        readonly HashSet<string> _slotSeen = new HashSet<string>();
        int _trees;
        bool _wide;                                         // the view steps back to show every tree (after a revolution)

        /// The whole board: one tree per epoch the lab has reached, each grown along its own spiral from its own root.
        /// A revolution wipes them all; after it the old trees start again from their roots beside the new one.
        void BuildTree()
        {
            if (_path == null) BuildPath();
            foreach (var kv in _nodes) Destroy(kv.Value.gameObject);
            _nodes.Clear();
            foreach (var t in _titles) if (t != null) Destroy(t);
            _titles.Clear();
            _edges.Clear();
            HideTip();
            var shown = new HashSet<string>();
            _newIndex = 0;
            _minX = _maxX = _minY = _maxY = 0;
            int count = 0, trees = 0;
            Slot fresh = null, newest = null;
            for (int era = 0; era <= G.Era; era++)
            {
                var origin = new Vector2(era * TreeGap, 0);
                // the trunk: one slot per layer, another for the second formula of a pair; a second letter hangs on the inner side
                var slots = new List<Slot>();
                for (int k = 0; k < Defs.Tree.Length; k++)
                {
                    if (Defs.LayerEra[k] != era || Defs.IsBranch[k] || !G.LayerVisible(k)) continue;
                    var layer = Defs.Tree[k];
                    var s0 = new Slot { code = layer[0], layer = k };
                    for (int i = 1; i < layer.Length; i++) if (Defs.Kind(layer[i]) == 'L') s0.inward = layer[i];
                    slots.Add(s0);
                    for (int i = 1; i < layer.Length; i++) if (Defs.Kind(layer[i]) == 'F') slots.Add(new Slot { code = layer[i], layer = k });
                }
                if (slots.Count == 0) continue;
                trees++;
                // slots sit along the spiral STEP apart, never too close to a corner
                float s = 0;
                foreach (var sl in slots)
                {
                    PathAt(s, out _, out _, out int seg);
                    if (seg > 0 && s - _cum[seg] < CORNER) s = _cum[seg] + CORNER;
                    else if (_cum[seg + 1] - s < CORNER) s = _cum[seg + 1] + CORNER;
                    PathAt(s, out sl.pos, out sl.dir, out sl.seg);
                    sl.pos += origin;
                    sl.n = new Vector2(sl.dir.y, -sl.dir.x);   // outward: away from the centre of the spiral
                    s += STEP;
                }
                if (G.Era > 0) EraTitle(era, origin);
                for (int i = 0; i < slots.Count; i++)
                {
                    var sl = slots[i];
                    float r = Radius(sl.code);
                    bool done = G.NodeDone(sl.code);
                    if (i > 0)
                    {   // trunk line from the previous slot, bending at the spiral's corner when there is one between
                        var pv = slots[i - 1]; float pr = Radius(pv.code);
                        var col = done ? Lit : Dim;
                        if (pv.seg == sl.seg) _edges.Line(pv.pos + sl.dir * (pr + 4), sl.pos - sl.dir * (r + 4), col, 3f);
                        else { var corner = _path[sl.seg] + origin; _edges.Line(pv.pos + pv.dir * (pr + 4), corner, col, 3f); _edges.Line(corner, sl.pos - sl.dir * (r + 4), col, 3f); }
                    }
                    BuildNode(sl.code, sl.pos, i, shown);
                    Bounds(sl.pos);
                    if (Defs.Kind(sl.code) == 'F') BuildChain(Defs.Id(sl.code), sl.pos, r, sl.n, 5);
                    else if (Defs.Kind(sl.code) == 'L') BuildChain("L:" + Defs.Id(sl.code), sl.pos, r, sl.n, 5);
                    if (sl.inward != null)
                    {   // the second letter of a pair: on the inner side, sprouting from the previous node like its sibling
                        var lp = sl.pos - sl.n * 134;
                        var col = G.NodeDone(sl.inward) ? Lit : Dim;
                        var pv = i > 0 ? slots[i - 1] : null;
                        if (pv != null && pv.seg == sl.seg)
                        {
                            var c = pv.pos - sl.n * 134;
                            _edges.Line(pv.pos - sl.n * (Radius(pv.code) + 4), c, col, 3f);
                            _edges.Line(c, lp - sl.dir * (LetterTile.TW / 2 + 4), col, 3f);
                        }
                        else _edges.Line(sl.pos - sl.n * (r + 4), lp + sl.n * (LetterTile.TW / 2 + 4), col, 3f);
                        BuildNode(sl.inward, lp, i, shown);
                        Bounds(lp);
                    }
                    if (!_slotSeen.Contains(sl.code) && (fresh == null || sl.layer > fresh.layer)) fresh = sl;
                }
                newest = slots[slots.Count - 1];
                foreach (var sl in slots) _slotSeen.Add(sl.code);
                count += slots.Count;
            }
            foreach (var kv in _tiles) kv.Value.gameObject.SetActive(shown.Contains(kv.Key));
            // the camera follows whatever has just grown, in whichever tree it grew
            var focus = fresh ?? newest;
            if (focus != null) { _lastSlot = focus.pos; _lastDir = focus.dir; }
            bool wide = trees > _trees && trees > 1;
            if (wide)
            {   // a revolution (or a board with several trees on load): step back so every root is in view
                _zoomT = Mathf.Clamp((LW - 300) / Mathf.Max(1, _maxX - _minX + 600), 0.35f, 1f);
                if (!_everBuilt) _zoom = _zoomT;
            }
            _trees = trees;
            if (count != _slotCount || wide)
            {
                if (_everBuilt && !wide) _wide = false;
                if (wide) _wide = true;
                _slotCount = count;
                if (_everBuilt) FocusFrontier();
            }
            _everBuilt = true;
        }

        /// The name of an epoch's tree, written faintly by its root (only once there is more than one tree).
        void EraTitle(int era, Vector2 origin)
        {
            var col = new Color(1, 1, 1, 0.42f);
            var t = UIF.Text(_board, Defs.DomainNames[era], 44, col, origin + new Vector2(-330, 8), new Vector2(460, 60), TextAnchor.MiddleRight);
            t.transform.SetAsFirstSibling();
            t.transform.SetSiblingIndex(1);
            _titles.Add(t.gameObject);
            _edges.Line(origin + new Vector2(-520, -26), origin + new Vector2(-110, -24), new Color(1, 1, 1, 0.25f), 2.5f);
        }

        /// A node's upgrades (a formula's devices and perks, a letter's tricks), one node at a time in a straight line.
        void BuildChain(string key, Vector2 origin, float originR, Vector2 dir, int max)
        {
            if (!G.ChainOpen(key)) return;
            var chain = Chain(key);
            Vector2 from = origin; float fromR = originR;
            for (int i = 0; i < chain.Count && i < max; i++)
            {
                var it = chain[i];
                bool have = it.device != null ? G.Has(it.device.id) : G.HasPerk(it.perk.id);
                var cp = origin + dir * (150 + i * CHAIN);
                _edges.Line(from + dir * (fromR + 4), cp - dir * (SMALL / 2 + 4), have ? Lit : Dim, have ? 2.5f : 2f);
                if (it.device != null) BuildDeviceNode(it.device, cp); else BuildPerkNode(it.perk, cp);
                Bounds(cp);
                from = cp; fromR = SMALL / 2;
                if (!have) break;   // deeper nodes stay hidden until this one is bought
            }
        }

        void BuildNode(string code, Vector2 pos, int k, HashSet<string> shown)
        {
            string id = Defs.Id(code);
            switch (Defs.Kind(code))
            {
                case 'L': PlaceLetter(id, pos); shown.Add(id); break;
                case 'F': { var f = Defs.F(id); if (G.Built(f.id)) BuildFormulaNode(f, pos, k); else BuildGhostNode(f, pos, k); break; }
                case 'R': BuildRevolutionNode(int.Parse(id), pos); break;
                case 'G': BuildGateNode(Defs.Gt(id), pos, k); break;
            }
        }

        void PlaceLetter(string id, Vector2 pos)
        {
            if (!_tiles.TryGetValue(id, out var tile))
            {
                var rt = UIF.Rect("L_" + id, _board, Vector2.zero, Vector2.one);
                tile = rt.gameObject.AddComponent<LetterTile>();
                tile.Build(id, this, false);
                _tiles[id] = tile;
                if (_everBuilt) tile.Appear(0.15f * _newIndex++);
            }
            ((RectTransform)tile.transform).anchoredPosition = pos;
            tile.gameObject.SetActive(true);
            tile.transform.SetAsLastSibling();
        }

        RectTransform Node(string name, Vector2 pos, float size)
        {
            var rt = UIF.Rect(name, _board, pos, new Vector2(size, size));
            BoardPatch.Add(rt, new Vector2(size - 12, size - 12));   // bare board under the node: no lines show through
            UIF.Catcher(rt);
            _nodes[name] = rt;
            // fresh nodes bounce in one after another
            if (_everBuilt && !_seen.Contains(name)) rt.gameObject.AddComponent<PopIn>().delay = 0.15f * _newIndex++;
            _seen.Add(name);
            return rt;
        }

        class ChainItem { public DeviceDef device; public PerkDef perk; public double price; public int coin; }

        /// A node's upgrades, cheapest first: the order in which the branch grows. The key is a formula id or "L:" + letter id.
        List<ChainItem> Chain(string key)
        {
            var list = new List<ChainItem>();
            foreach (var d in Defs.Devices) if (d.requires == key && d.domain <= G.Era) list.Add(new ChainItem { device = d, price = d.price });
            foreach (var p in Defs.Perks) if (p.requires == key && p.domain <= G.Era) list.Add(new ChainItem { perk = p, price = p.price, coin = p.cost == Cur.Obs ? 0 : 1 });
            // what is paid in ideas grows first; a teaser priced in the next era's coin always closes the branch
            list.Sort((a, b) => a.coin != b.coin ? a.coin.CompareTo(b.coin) : a.price.CompareTo(b.price));
            return list;
        }

        /// A freshly revealed node bounces in (after a delay, so a batch of new nodes appears one by one).
        class PopIn : MonoBehaviour
        {
            public float delay;
            float _t;
            void Update()
            {
                if (delay > 0) { delay -= Time.deltaTime; transform.localScale = Vector3.zero; return; }
                _t = Mathf.Min(1, _t + Time.deltaTime * 3f);
                transform.localScale = Vector3.one * (1 + 0.4f * (1 - _t) * Mathf.Sin(_t * Mathf.PI));
                if (_t >= 1) { transform.localScale = Vector3.one; Destroy(this); }
            }
        }

        /// A gentle breathing scale for things that wait for the player.
        class Breathe : MonoBehaviour
        {
            void Update() { if (GetComponent<PopIn>() == null) transform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(Time.time * 3f)); }
        }

        // ---------------- icons ----------------
        public static void DrawIcon(ChalkShape s, string icon, Color c, float k = 1f, Vector2 at = default)
        {
            Vector2 P(float x, float y) => new Vector2(x * k, y * k) + at;
            var faint = new Color(c.r, c.g, c.b, c.a * 0.5f);
            switch (icon)
            {
                // the lightbulb of ideas: the currency that used to be called "наблюдения"
                case "bulb":
                    {   // a classic pear-shaped bulb: round glass narrowing into a neck, a screwed base, a filament, rays
                        float lw = Mathf.Lerp(1.7f, 2.6f, Mathf.Clamp01(k));
                        const float r = 10f, phi = 0.55f;                       // glass radius; half-angle of the neck gap
                        float ex = r * Mathf.Sin(phi), ey = 5 - r * Mathf.Cos(phi);   // where the glass meets the neck
                        {   // the glass is small on screen, so it gets its own dense outline instead of a coarse arc
                            var glass = new List<Vector2>();
                            for (int i = 0; i <= 22; i++) { float a = Mathf.Lerp(-Mathf.PI / 2 + phi, Mathf.PI * 1.5f - phi, i / 22f); glass.Add(P(Mathf.Cos(a) * r, 5 + Mathf.Sin(a) * r)); }
                            s.Poly(glass, c, lw, false, false);
                        }
                        s.Line(P(-ex, ey), P(-4.6f, -8), c, lw); s.Line(P(ex, ey), P(4.6f, -8), c, lw);
                        s.Line(P(-4.6f, -8), P(4.6f, -8), c, lw);
                        s.Line(P(-4.2f, -11), P(4.2f, -11), c, lw);
                        s.Line(P(-3.6f, -14), P(3.6f, -14), c, lw);
                        s.Line(P(-1.8f, -16.5f), P(1.8f, -16.5f), c, lw);
                        if (k >= 0.6f)
                        {   // the filament: two legs and a little loop
                            s.Line(P(-2.5f, -4), P(-2.5f, 1), c, lw * 0.75f); s.Line(P(2.5f, -4), P(2.5f, 1), c, lw * 0.75f);
                            var loop = new List<Vector2>();
                            for (int i = 0; i <= 8; i++) { float a = Mathf.PI * i / 8f; loop.Add(P(Mathf.Cos(a) * 2.5f, 1 + Mathf.Sin(a) * 2.5f)); }
                            s.Poly(loop, c, lw * 0.75f, false, false);
                        }
                        for (int i = -2; i <= 2; i++)
                        {
                            float a = Mathf.PI / 2 + i * 0.55f;
                            s.Line(P(Mathf.Cos(a) * (r + 3.5f), 5 + Mathf.Sin(a) * (r + 3.5f)), P(Mathf.Cos(a) * (r + 7.5f), 5 + Mathf.Sin(a) * (r + 7.5f)), faint, lw * 0.85f);
                        }
                        break;
                    }
                case "pusher": s.Rect(P(8, 2), P(16, 16), c, 2.5f); s.Line(P(-20, 2), P(0, 2), c, 3f); s.Line(P(-20, -4), P(-20, 8), c, 3f); { var pts = new List<Vector2>(); for (int i = 0; i <= 20; i++) { float a = i / 20f * Mathf.PI * 2; float r = (i % 4 < 2) ? 9 : 6; pts.Add(P(-16 + Mathf.Cos(a) * r, -14 + Mathf.Sin(a) * r)); } s.Poly(pts, faint, 2f, false, false); } break;
                case "hook": s.Line(P(-12, 18), P(12, 18), c, 3f); s.Zigzag(P(0, 18), P(0, 0), 4, 7, c, 2.5f); s.Arc(P(0, -6), 7, Mathf.PI * 0.15f, Mathf.PI * 1.6f, c, 2.8f); s.Rect(P(0, -18), P(14, 10), faint, 2f); break;
                case "cam": { var pts = new List<Vector2>(); for (int i = 0; i <= 24; i++) { float a = i / 24f * Mathf.PI * 2; float r = 11 + 6 * Mathf.Cos(a); pts.Add(P(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); } s.Poly(pts, c, 2.5f, true, false); s.Circle(P(0, 0), 3.5f, c, 2f); s.Line(P(-18, -16), P(18, -16), faint, 2.5f); } break;
                case "stack": s.Rect(P(0, -12), P(30, 12), c, 2.5f); s.Rect(P(-3, 0), P(24, 12), c, 2.2f); s.Rect(P(2, 12), P(18, 12), faint, 2f); break;
                case "scoop": s.Arc(P(0, -2), 13, Mathf.PI, Mathf.PI * 2, c, 2.8f); s.Line(P(-13, -2), P(-13, 6), c, 2.5f); s.Line(P(13, -2), P(13, 6), c, 2.5f); s.Line(P(13, 4), P(22, 16), c, 2.5f); s.Line(P(-16, -14), P(16, -14), faint, 2f); break;
                case "scales": s.Line(P(0, -16), P(0, 14), c, 2.8f); s.Line(P(-16, 14), P(16, 14), c, 2.8f); s.Line(P(-8, -16), P(8, -16), c, 2.5f); s.Arc(P(-16, 10), 7, Mathf.PI, Mathf.PI * 2, c, 2.2f); s.Arc(P(16, 10), 7, Mathf.PI, Mathf.PI * 2, c, 2.2f); break;
                case "nugget": { var pts = new[] { P(-12, -6), P(-6, 8), P(6, 11), P(14, 2), P(9, -9), P(-4, -12) }; s.Poly(pts, c, 2.5f, true); s.Line(P(-6, 8), P(2, -2), faint, 1.8f); s.Line(P(9, -9), P(2, -2), faint, 1.8f); s.Line(P(14, 12), P(18, 16), faint, 2f); s.Line(P(18, 12), P(14, 16), faint, 2f); } break;
                case "console": s.Poly(new[] { P(-18, -10), P(18, -10), P(14, 8), P(-14, 8) }, c, 2.5f, true); s.Circle(P(0, 0), 6, c, 2.8f); s.Line(P(-9, 4), P(-9, -5), faint, 2f); s.Line(P(9, 4), P(9, -5), faint, 2f); s.Line(P(-10, -16), P(10, -16), faint, 2.2f); break;
                case "hand": s.Line(P(-9, -14), P(-9, 2), c, 2.5f); s.Line(P(-3, -14), P(-3, 8), c, 2.5f); s.Line(P(3, -14), P(3, 10), c, 2.5f); s.Line(P(9, -14), P(9, 4), c, 2.5f); s.Arc(P(0, -14), 9, Mathf.PI, Mathf.PI * 2, c, 2.5f); s.Line(P(-14, -8), P(-9, -2), c, 2.2f); break;
                case "bank": s.Arc(P(0, 0), 13, 0, Mathf.PI * 2, c, 2.5f); s.Line(P(-5, 13), P(5, 13), c, 2.5f); s.Line(P(0, 13), P(0, 18), c, 2.2f); s.Line(P(-5, -2), P(5, -2), faint, 2f); s.Line(P(0, -7), P(0, 3), faint, 2f); break;
                case "list": s.Rect(P(0, 0), P(24, 30), c, 2.5f); for (int i = 0; i < 3; i++) { float y = 8 - i * 8; s.Line(P(-4, y), P(8, y), faint, 2f); s.Line(P(-10, y + 1), P(-7, y - 2), c, 2.2f); s.Line(P(-7, y - 2), P(-3, y + 4), c, 2.2f); } break;
                case "bolt": s.Poly(new[] { P(2, 18), P(-9, 0), P(1, 0), P(-2, -18), P(9, 2), P(0, 2) }, c, 2.5f, true); break;
                case "spark": for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 4; s.Line(P(Mathf.Cos(a) * -14, Mathf.Sin(a) * -14), P(Mathf.Cos(a) * 14, Mathf.Sin(a) * 14), i % 2 == 0 ? c : faint, i % 2 == 0 ? 2.5f : 2f); } s.Circle(P(0, 0), 4, c, 2f); break;
                case "fall": s.Line(P(-18, 6), P(6, 6), c, 3f); s.Line(P(-14, 6), P(-14, -16), c, 2.5f); s.Rect(P(14, -8), P(14, 14), c, 2.5f); s.Arrow(P(14, 12), P(14, 0), c, 2f, 6f); break;
                case "dyna": s.Line(P(-10, 20), P(10, 20), c, 3f); s.Zigzag(P(0, 20), P(0, -6), 4, 6, c, 2.5f); s.Line(P(-8, -8), P(8, -8), c, 3f); s.Arc(P(0, -16), 5, 0.6f, 5.2f, c, 2.5f); break;
                case "spring": s.Line(P(-12, -20), P(12, -20), c, 3f); s.Zigzag(P(0, -20), P(0, 8), 5, 8, c, 2.5f); s.Line(P(-12, 10), P(12, 10), c, 3f); s.Rect(P(0, 18), P(12, 12), c, 2f); break;
                case "pend": s.Line(P(-14, 20), P(14, 20), c, 3f); s.Line(P(0, 20), P(10, -10), c, 2f); s.Circle(P(11, -13), 6, c, 2.5f); s.Arc(P(0, 20), 30, -Mathf.PI * 0.85f, -Mathf.PI * 0.55f, faint, 1.5f); break;
                case "arch": s.Poly(new[] { P(-18, 12), P(-14, -14), P(14, -14), P(18, 12) }, c, 2.5f); s.Line(P(-16, 2), P(16, 2), ChalkTex.Cyan, 2f); s.Rect(P(0, 6), P(10, 10), c, 2f); break;
                case "density": s.Poly(new[] { P(-10, 16), P(-10, -12), P(10, -12), P(10, 16) }, c, 2.5f); s.Line(P(-10, 16), P(-14, 20), c, 2f); s.Line(P(6, -4), P(10, -4), c, 1.5f); s.Line(P(6, 4), P(10, 4), c, 1.5f); s.Rect(P(0, -16), P(24, 6), c, 2f); break;
                case "friction": s.Line(P(-20, -8), P(20, -8), c, 3f); s.Rect(P(-6, 0), P(14, 14), c, 2.5f); s.Zigzag(P(2, 0), P(18, 0), 3, 3, c, 1.5f); for (int i = 0; i < 4; i++) s.Line(P(-11 + i * 3.5f, 7), P(-10 + i * 3.5f, 10), c, 1.5f); break;
                case "slide": s.Line(P(-22, -8), P(22, -8), c, 3f); s.Rect(P(10, 0), P(12, 12), c, 2.5f); s.Arrow(P(-18, 2), P(0, 2), c, 2f, 6f); break;
                case "cap": s.Line(P(-22, 0), P(-6, 0), c, 2.5f); s.Line(P(-6, -15), P(-6, 15), c, 3f); s.Line(P(6, -15), P(6, 15), c, 3f); s.Line(P(6, 0), P(22, 0), c, 2.5f); for (int i = 0; i < 3; i++) s.Line(P(-2, -9 + i * 9), P(2, -9 + i * 9), faint, 1.5f); break;
                case "coil": s.Line(P(-24, 0), P(-18, 0), c, 2.5f); for (int i = 0; i < 4; i++) s.Arc(P(-13.5f + i * 9, 0), 4.5f, 0, Mathf.PI, c, 2.5f); s.Line(P(18, 0), P(24, 0), c, 2.5f); s.Line(P(-16, -9), P(16, -9), faint, 2f); s.Line(P(-16, -13), P(16, -13), faint, 2f); break;
                case "fly": s.Circle(P(4, 2), 16, c, 2.5f, false); for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2; s.Line(P(4, 2), P(4 + Mathf.Cos(a) * 14, 2 + Mathf.Sin(a) * 14), c, 1.5f); } s.Rect(P(-18, -10), P(8, 10), c, 2f); break;
                case "stand": s.Rect(P(0, 0), P(36, 20), c, 2.5f); s.Zigzag(P(-10, 10), P(10, 10), 3, 4, c, 2f); s.Line(P(-4, -10), P(4, -10), c, 3f); s.Line(P(-1, -14), P(1, -14), c, 2f); break;
                case "charge": s.Line(P(-2, 18), P(-10, 0), c, 2.5f); s.Line(P(-10, 0), P(4, 0), c, 2.5f); s.Line(P(4, 0), P(-4, -18), c, 2.5f); break;
                case "lamp": s.Circle(P(0, 4), 12, c, 2.5f); s.Line(P(-5, -10), P(5, -10), c, 2.5f); s.Line(P(-4, -15), P(4, -15), c, 2f); s.Line(P(-4, 0), P(0, 6), faint, 1.5f); s.Line(P(4, 0), P(0, 6), faint, 1.5f); break;
                case "calor": s.Rect(P(0, -2), P(30, 30), c, 2.5f); s.Rect(P(0, -4), P(20, 22), faint, 1.8f); s.Line(P(-18, 14), P(18, 14), c, 2.5f); s.Line(P(8, 22), P(8, -8), ChalkTex.Red, 2.2f); s.Circle(P(8, -10), 3, ChalkTex.Red, 1.8f); break;
                case "ice": s.Line(P(-20, -14), P(20, -14), c, 2.5f); s.Poly(new[] { P(-14, -12), P(-15, 8), P(0, 11), P(15, 8), P(14, -12) }, new Color(0.75f, 0.93f, 1f, c.a), 2.5f, true); s.Rect(P(0, 16), P(10, 10), c, 2f); s.Line(P(-6, -4), P(-1, 4), faint, 1.6f); break;
                case "heater": s.Poly(new[] { P(-14, 14), P(-12, -12), P(12, -12), P(14, 14) }, c, 2.5f); s.Line(P(-12, 4), P(12, 4), ChalkTex.Cyan, 2f); s.Zigzag(P(2, 20), P(2, -6), 4, 4, c, 2f); break;
                case "engine": s.Rect(P(-8, -2), P(20, 20), c, 2.5f); s.Circle(P(16, 2), 8, c, 2f); s.Line(P(2, 2), P(16, 2), c, 2f); for (int i = 0; i < 3; i++) s.Arc(P(-14 + i * 6, 16), 3, 0, Mathf.PI, c, 1.5f); break;
                case "piston": s.Rect(P(0, 0), P(18, 36), c, 2.5f); s.Line(P(-9, 4), P(9, 4), ChalkTex.Cyan, 3f); s.Line(P(0, 4), P(0, 20), c, 2.5f); s.Line(P(-9, -18), P(9, -18), ChalkTex.Red, 3f); break;
                case "psi": s.Line(P(0, -18), P(0, 18), c, 3f); s.Arc(P(0, 4), 12, Mathf.PI, Mathf.PI * 2, c, 3f); s.Line(P(-8, 18), P(8, 18), c, 2.5f); break;
                case "gear": { var pts = new List<Vector2>(); for (int i = 0; i <= 32; i++) { float a = i / 32f * Mathf.PI * 2; float r = (i % 4 < 2) ? 16 : 12; pts.Add(P(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); } s.Poly(pts, c, 2.5f, false, false); s.Circle(P(0, 0), 5, c, 2f); } break;
                case "lens": s.Circle(P(-4, 4), 11, c, 2.5f); s.Line(P(4, -4), P(16, -16), c, 3.5f); break;
                case "star": { var pts = new List<Vector2>(); for (int i = 0; i < 11; i++) { float a = Mathf.PI / 2 + i / 10f * Mathf.PI * 2; float r = i % 2 == 0 ? 17 : 8; pts.Add(P(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); } s.Poly(pts, c, 2.5f, false, false); } break;
                case "box": s.Rect(P(0, -2), P(28, 22), c, 2.5f); s.Line(P(-14, 6), P(14, 6), c, 2f); s.Line(P(0, 6), P(0, -13), c, 1.5f); break;
                case "eye": s.Arc(P(0, -4), 18, 0.5f, Mathf.PI - 0.5f, c, 2.5f); s.Arc(P(0, 12), 18, Mathf.PI + 0.5f, Mathf.PI * 2 - 0.5f, c, 2.5f); s.Circle(P(0, 4), 6, c, 2.5f); break;
                case "clock": s.Circle(P(0, 0), 16, c, 2.5f); s.Line(P(0, 0), P(0, 10), c, 2.5f); s.Line(P(0, 0), P(7, -4), c, 2.5f); break;
                case "rev": s.Arc(P(0, 0), 16, 0.3f, Mathf.PI * 1.7f, c, 3f); s.Arrow(P(15, 2), P(10, -8), c, 2.5f, 8f); break;
                case "act": for (int i = 0; i < 4; i++) s.Line(P(-12 + i * 7, -12), P(-12 + i * 7, 12), c, 2.5f); s.Line(P(-16, -8), P(14, 10), c, 2.5f); break;
                default: s.Circle(P(0, 0), 14, c, 2.5f); break;
            }
        }

        /// Every upgrade shows what it adds: a device that runs an instrument wears that instrument's picture,
        /// a perk that multiplies one wears it too (with its ×N), and the rest have their own little drawings.
        static string DeviceIcon(DeviceDef d) => d.icon ?? (d.automates != null ? Defs.StationIcon(d.automates) : "gear");
        static string PerkIcon(PerkDef p) => p.icon ?? (p.kind switch
        {
            PerkKind.Actions => "act",
            PerkKind.Samples => "box",
            PerkKind.AutoMult => "gear",
            PerkKind.ObsMult => "bulb",
            PerkKind.StationMult => Defs.StationIcon(p.station),
            _ => "star",
        });

        public static void DashedRect(ChalkShape s, Vector2 size, Color c, float w)
        {
            Vector2 h = size * 0.5f;
            s.Dashed(new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), c, w, 12f, 8f);
            s.Dashed(new Vector2(h.x, -h.y), new Vector2(h.x, h.y), c, w, 12f, 8f);
            s.Dashed(new Vector2(h.x, h.y), new Vector2(-h.x, h.y), c, w, 12f, 8f);
            s.Dashed(new Vector2(-h.x, h.y), new Vector2(-h.x, -h.y), c, w, 12f, 8f);
        }

        // ---------------- nodes (every label lives inside its button) ----------------
        void BuildFormulaNode(FormulaDef f, Vector2 pos, int k)
        {
            var rt = Node("F_" + f.id, pos, NODE);
            var box = UIF.Shape(rt, 40 + k);
            box.Rect(Vector2.zero, new Vector2(NODE - 6, NODE - 6), ChalkTex.White, 3.5f);
            var ic = UIF.Shape(rt, 70 + k);
            ic.rectTransform.anchoredPosition = new Vector2(0, 18);
            DrawIcon(ic, f.icon ?? "psi", ChalkTex.White, 1.0f);
            ChalkTex.Math(LetterTile.Fit(UIF.Text(rt, GameState.Symbolic(f), 18, ChalkTex.Dim, new Vector2(0, -30), new Vector2(NODE - 16, 26)), 10, 18));
            box.Line(new Vector2(-NODE / 2 + 16, -42), new Vector2(NODE / 2 - 16, -42), new Color(1, 1, 1, 0.22f), 2f);   // underlined like a heading
            var c = rt.gameObject.AddComponent<Clickable>();
            var ff = f;
            c.onHover = h =>
            {
                rt.localScale = Vector3.one * (h ? 1.06f : 1f);
                if (!h) { HideTip(); return; }
                string body = ff.riddle, stat = null;
                var sc2 = ChalkTex.Yellow;
                if (ff.kind != FKind.Final)
                {
                    var yc = ff.kind == FKind.Energy ? ff.yields : Cur.Obs;
                    body += $"\n<color=#8CFF99>{ff.effect}</color>";
                    if (ff.kind == FKind.Measure) body += "\nУровни букв формулы делают измерение точнее";
                    if (ff.outputs.Length > 0) body += $"\nДаёт новую величину: {string.Join(", ", ff.outputs.Select(o => Defs.L(o).sym))}";
                    stat = "За действие: " + GameState.FmtPrice(G.YieldOf(ff, yc), yc) + (G.Automated(ff.station) ? "  · работает сама" : "");
                    sc2 = Apparatus.CurColor((int)yc);
                }
                ShowTip(ff.title, body, stat, sc2);
            };
        }

        /// A formula that can be found in the alchemy: its letters are open.
        void BuildGhostNode(FormulaDef f, Vector2 pos, int k)
        {
            var rt = Node("G_" + f.id, pos, NODE);
            var box = UIF.Shape(rt, 40 + k);
            var col = new Color(1f, 0.84f, 0.32f, 0.7f);
            DashedRect(box, new Vector2(NODE - 6, NODE - 6), col, 2.5f);
            UIF.Text(rt, "?", 54, ChalkTex.Yellow, new Vector2(0, 14), new Vector2(NODE, 64));
            LetterTile.Fit(UIF.Text(rt, "новая теория", 16, col, new Vector2(0, -28), new Vector2(NODE - 14, 40)), 11, 16);
            rt.gameObject.AddComponent<Breathe>();
            var c = rt.gameObject.AddComponent<Clickable>();
            c.onClick = () => { Sfx.Play("click"); GameRoot.I.ShowAlchemy(f.id); };
            c.onHover = h =>
            {
                if (!h) { HideTip(); return; }
                ShowTip("Новая теория", "Все её величины уже открыты. Нажми — и на экране теорий расставь их по чертежу опыта.");
            };
        }

        void BuildRevolutionNode(int to, Vector2 pos)
        {
            var rt = Node("R_" + to, pos, NODE);
            var box = UIF.Shape(rt, 77 + to);
            bool done = G.Revolutions >= to;
            bool now = !done && G.RevolutionOffered && G.NextRevolution.to == to;
            double cost = Defs.Revolutions[to - 1].cost;
            var col = done ? ChalkTex.White : ChalkTex.Pink;
            box.Rect(Vector2.zero, new Vector2(NODE - 6, NODE - 6), col, 3f);
            var ic = UIF.Shape(rt, 88 + to);
            ic.rectTransform.anchoredPosition = new Vector2(0, 18);
            DrawIcon(ic, "rev", col, 1.0f);
            LetterTile.Fit(UIF.Text(rt, done ? Defs.DomainNames[to] : GameState.FmtCur(cost, Cur.J), 18, done ? ChalkTex.Dim : ChalkTex.Yellow, new Vector2(0, -30), new Vector2(NODE - 14, 26)), 10, 18);
            if (now) rt.gameObject.AddComponent<PulseView>().Init(box, () => G.CanRevolt, ChalkTex.Pink, ChalkTex.Yellow, new Vector2(NODE - 6, NODE - 6));
            var c = rt.gameObject.AddComponent<Clickable>();
            c.onClick = () => { if (now) { if (G.CanRevolt) GameRoot.I.ShowRevolutionDialog(); else { Sfx.Play("nope"); FX.I.Text(Content, BoardToFixed(pos) + new Vector2(0, 80), "копи энергию", ChalkTex.Red, 24); } } };
            c.onHover = h =>
            {
                rt.localScale = Vector3.one * (h && now ? 1.06f : 1f);
                if (!h) { HideTip(); return; }
                string dom = Defs.DomainNames[to];
                ShowTip("Научная революция → " + dom, done ? "Свершилась: все опыты работают ×" + Defs.DomainMult[to] + "." : $"Стирает с доски всё: теории, приборы, буквы, улучшения и валюты. Потом лаборатория растёт заново — уже из нескольких начал: старые ветки можно прокачать снова, а рядом начинается новая. Навсегда ×{Defs.DomainMult[to]} ко всем опытам.",
                    done ? null : "Нужно " + GameState.FmtCur(cost, Cur.J), ChalkTex.Yellow);
            };
        }

        void BuildGateNode(GateDef g, Vector2 pos, int k)
        {
            var rt = Node("Gt_" + g.id, pos, NODE);
            var box = UIF.Shape(rt, 40 + k);
            bool have = G.Gates.Contains(g.id);
            var ic = UIF.Shape(rt, 70 + k);
            ic.rectTransform.anchoredPosition = new Vector2(0, 18);
            DrawIcon(ic, "psi", have ? new Color(1, 1, 1, 0.45f) : ChalkTex.Yellow, 1.0f);
            var price = LetterTile.Fit(UIF.Text(rt, have ? "✓" : GameState.FmtCur(g.cost, Cur.J), 18, have ? ChalkTex.Green : ChalkTex.Yellow, new Vector2(0, -30), new Vector2(NODE - 14, 26)), 10, 18);
            rt.gameObject.AddComponent<BuyNodeView>().Init(box, price, () => G.CanBuyGate(g.id), have, ChalkTex.Yellow, new Vector2(NODE - 6, NODE - 6));
            var c = rt.gameObject.AddComponent<Clickable>();
            c.onClick = () =>
            {
                if (G.Gates.Contains(g.id)) return;
                if (G.BuyGate(g.id)) { Sfx.Play("correct", 0.7f); FX.I.Dust(Content, BoardToFixed(pos), ChalkTex.Yellow, 20, 260f); HideTip(); }
                else { Sfx.Play("nope", 0.5f); FX.I.Text(Content, BoardToFixed(pos) + new Vector2(0, 80), "не хватает джоулей", ChalkTex.Red, 24); }
            };
            c.onHover = h =>
            {
                rt.localScale = Vector3.one * (h && !G.Gates.Contains(g.id) ? 1.06f : 1f);
                if (!h) { HideTip(); return; }
                bool got = G.Gates.Contains(g.id);
                ShowTip(g.title, got ? g.hint : g.riddle, got ? "ищи её в теориях" : "Подсказка стоит " + GameState.FmtCur(g.cost, Cur.J), got ? ChalkTex.Green : ChalkTex.Yellow);
            };
        }

        void BuildDeviceNode(DeviceDef d, Vector2 pos)
        {
            var rt = Node("D_" + d.id, pos, SMALL);
            var box = UIF.Shape(rt, 400 + (d.id.GetHashCode() & 0x3f));
            bool have = G.Has(d.id);
            var cc = Apparatus.CurColor((int)d.cost);
            var ic = UIF.Shape(rt, 500 + (d.id.GetHashCode() & 0x3f));
            ic.rectTransform.anchoredPosition = new Vector2(0, 12);
            var icol = have ? new Color(1, 1, 1, 0.45f) : ChalkTex.White;
            DrawIcon(ic, DeviceIcon(d), icol, 0.8f);
            // a device that runs an instrument by itself carries a little gear in the corner
            if (d.automates != null) DrawIcon(ic, "gear", new Color(icol.r, icol.g, icol.b, icol.a * 0.55f), 0.3f, new Vector2(24, -16));
            var price = UIF.Text(rt, "", 17, have ? ChalkTex.Green : cc, new Vector2(0, -26), new Vector2(SMALL - 8, 22));
            InkText.On(price).Set(have ? "✓" : (d.cost == Cur.Obs ? GameState.FmtPrice(d.price, Cur.Obs) : GameState.Fmt(d.price)), cc);
            rt.gameObject.AddComponent<BuyNodeView>().Init(box, price, () => G.Cur[(int)d.cost] >= d.price, have, cc, new Vector2(SMALL - 6, SMALL - 6));
            var c = rt.gameObject.AddComponent<Clickable>();
            c.onClick = () =>
            {
                if (G.Has(d.id)) return;
                if (G.BuyDevice(d.id)) { Sfx.Play("correct", 0.6f); FX.I.Dust(Content, BoardToFixed(pos), ChalkTex.Green, 14, 220f); HideTip(); }
                else Sfx.Play("nope", 0.5f);
            };
            c.onHover = h =>
            {
                rt.localScale = Vector3.one * (h && !G.Has(d.id) ? 1.06f : 1f);
                if (!h) { HideTip(); return; }
                bool got = G.Has(d.id);
                ShowTip("⚙ " + d.title, d.desc, got ? "куплено" : "Цена: " + GameState.FmtPrice(d.price, d.cost), got ? ChalkTex.Green : cc);
            };
        }

        void BuildPerkNode(PerkDef p, Vector2 pos)
        {
            var rt = Node("P_" + p.id, pos, SMALL);
            var box = UIF.Shape(rt, 600 + (p.id.GetHashCode() & 0x3f));
            bool have = G.HasPerk(p.id);
            var cc = ChalkTex.Pink;
            var ic = UIF.Shape(rt, 700 + (p.id.GetHashCode() & 0x3f));
            ic.rectTransform.anchoredPosition = new Vector2(0, 12);
            var pcol = have ? new Color(1, 1, 1, 0.45f) : ChalkTex.Pink;
            DrawIcon(ic, PerkIcon(p), pcol, 0.8f);
            // a perk that multiplies one instrument shows that instrument with its multiplier
            if (p.kind == PerkKind.StationMult) UIF.Text(rt, "×" + (int)p.value, 19, pcol, new Vector2(28, -2), new Vector2(40, 24));
            if (p.kind == PerkKind.Actions) UIF.Text(rt, "+" + (int)p.value, 17, pcol, new Vector2(27, -1), new Vector2(40, 22));
            var pc = Apparatus.CurColor((int)p.cost);
            var price = UIF.Text(rt, "", 17, have ? ChalkTex.Green : pc, new Vector2(0, -26), new Vector2(SMALL - 8, 22));
            InkText.On(price).Set(have ? "✓" : p.cost == Cur.Obs ? GameState.FmtPrice(p.price, Cur.Obs) : GameState.FmtCur(p.price, p.cost), pc);
            rt.gameObject.AddComponent<BuyNodeView>().Init(box, price, () => G.Cur[(int)p.cost] >= p.price, have, cc, new Vector2(SMALL - 6, SMALL - 6));
            var c = rt.gameObject.AddComponent<Clickable>();
            c.onClick = () =>
            {
                if (G.HasPerk(p.id)) return;
                if (G.BuyPerk(p.id)) { Sfx.Play("correct", 0.7f); FX.I.Dust(Content, BoardToFixed(pos), ChalkTex.Pink, 16, 240f); FX.I.Text(Content, BoardToFixed(pos) + new Vector2(0, 70), p.desc, ChalkTex.Pink, 24, 1.6f); HideTip(); }
                else
                {
                    Sfx.Play("nope", 0.5f);
                    if (p.cost != Cur.Obs && G.Revolutions < (p.cost == Cur.C ? 1 : 2))
                        FX.I.Text(Content, BoardToFixed(pos) + new Vector2(0, 70), p.cost == Cur.C ? "кулоны появятся с электричеством" : "килокалории появятся с паром", Apparatus.CurColor((int)p.cost), 24, 1.6f);
                }
            };
            c.onHover = h =>
            {
                rt.localScale = Vector3.one * (h && !G.HasPerk(p.id) ? 1.06f : 1f);
                if (!h) { HideTip(); return; }
                bool got = G.HasPerk(p.id);
                string body = p.desc;
                if (!got && p.cost != Cur.Obs && G.Revolutions < (p.cost == Cur.C ? 1 : 2))
                    body += p.cost == Cur.C ? "\n<color=#BF99FF>Платится кулонами — их принесёт электричество</color>" : "\n<color=#FF8C66>Платится килокалориями — их принесёт паровая эпоха</color>";
                ShowTip("★ " + p.title, body, got ? "взято" : "Цена: " + (p.cost == Cur.Obs ? GameState.FmtPrice(p.price, Cur.Obs) : GameState.FmtCur(p.price, p.cost)), got ? ChalkTex.Green : pc);
            };
        }

        class BuyNodeView : MonoBehaviour
        {
            ChalkShape _box; Text _price; System.Func<bool> _afford; bool _have; Color _col; Vector2 _size; string _key;
            public void Init(ChalkShape box, Text price, System.Func<bool> afford, bool have, Color col, Vector2 size)
            { _box = box; _price = price; _afford = afford; _have = have; _col = col; _size = size; }
            void Update()
            {
                bool a = !_have && _afford();
                string k = $"{a}";
                if (k != _key)
                {
                    _key = k; _box.Clear();
                    _box.Rect(Vector2.zero, _size, _have ? new Color(1, 1, 1, 0.3f) : a ? new Color(_col.r, _col.g, _col.b, 0.9f) : new Color(1, 1, 1, 0.45f), a ? 3.5f : 2.5f);
                    if (a)
                    {   // two ticked corners mark what can be bought right now
                        float hx = _size.x / 2, hy = _size.y / 2;
                        _box.Line(new Vector2(-hx, hy - 12), new Vector2(-hx + 12, hy), _col, 2.2f);
                        _box.Line(new Vector2(hx, -hy + 12), new Vector2(hx - 12, -hy), _col, 2.2f);
                    }
                }
                if (!_have) { var c = _price.color; _price.color = new Color(c.r, c.g, c.b, a ? 0.8f + 0.2f * Mathf.Sin(Time.time * 5) : 0.4f); }
            }
        }

        class PulseView : MonoBehaviour
        {
            ChalkShape _box; System.Func<bool> _on; Color _a, _b; Vector2 _size; string _key;
            public void Init(ChalkShape box, System.Func<bool> on, Color a, Color b, Vector2 size) { _box = box; _on = on; _a = a; _b = b; _size = size; }
            void Update()
            {
                bool on = _on();
                string k = on + "";
                if (k != _key) { _key = k; _box.Clear(); _box.Rect(Vector2.zero, _size, on ? _b : _a, on ? 3.5f : 3f); }
                if (on) transform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(Time.time * 5));
            }
        }

        Vector2 BoardToFixed(Vector2 boardPos) => boardPos * _zoom + _board.anchoredPosition;

        // ---------------- tooltip ----------------
        public void ShowTip(string title, string body, string stat = null, Color? statCol = null)
        {
            _tipOn = true;
            _tip.gameObject.SetActive(true);
            _tip.SetAsLastSibling();
            _tipTitle.text = ChalkTex.Sym(title);
            _tipBody.text = body ?? "";
            _tipBody.rectTransform.sizeDelta = new Vector2(430, 400);
            bool hasBody = _tipBody.text.Length > 0, hasStat = !string.IsNullOrEmpty(stat);
            float bh = hasBody ? Mathf.Max(28, _tipBody.preferredHeight) : 0;
            float sh = hasStat ? 38 : 0;
            _tipW = 460; _tipH = 58 + bh + sh + 18;
            _tip.sizeDelta = new Vector2(_tipW, _tipH);
            _tipTitle.rectTransform.anchoredPosition = new Vector2(0, _tipH / 2 - 10 - 19);
            _tipBody.rectTransform.sizeDelta = new Vector2(430, bh + 4);
            _tipBody.rectTransform.anchoredPosition = new Vector2(0, _tipH / 2 - 56 - bh / 2);
            _tipStat.gameObject.SetActive(hasStat);
            if (hasStat)
            {
                _tipStat.color = statCol ?? ChalkTex.Yellow;
                _tipStat.rectTransform.anchoredPosition = new Vector2(0, _tipH / 2 - 56 - bh - 4 - 17);
                _tipInk.Set(stat, _tipStat.color);
                _tipInk.Refresh();
            }
            _tipBox.Clear();
            _tipBox.Rect(Vector2.zero, new Vector2(_tipW - 4, _tipH - 4), ChalkTex.White, 2.5f);
        }

        /// A flat board-coloured rectangle (UGUI Image) that hides whatever is drawn under it.
        public static RectTransform Backing(Transform parent, Vector2 pos, Vector2 size, Color c)
        {
            var rt = UIF.Rect("Back", parent, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return rt;
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

        /// Tooltip for a letter (hover / after a click). Never does the arithmetic for the player.
        public void Inspect(string id)
        {
            if (id == null) { HideTip(); return; }
            var d = Defs.L(id);
            var sb = new System.Text.StringBuilder();
            string head = d.unit.Length > 0 ? $"{d.sym} — {d.name}  [{d.unit}]" : $"{d.sym} — {d.name}";
            if (!G.Known.Contains(id))
            {
                ShowTip(head, "", d.openCost > 0 ? "Открыть за " + GameState.FmtCur(d.openCost, d.openCur) : "Нажми, чтобы открыть", Apparatus.CurColor((int)d.openCur));
                return;
            }
            if (d.Derived)
            {
                var f = Defs.F(d.derivedFrom);
                sb.Append(G.Built(f.id) ? $"{G.LetterValue(id)}\nИзмеряется опытом «{f.title}»: {GameState.Symbolic(f)}"
                                        : $"Измерена в прошлой эпохе опытом «{f.title}» и записана в тетрадь");
            }
            else if (d.perSample) sb.Append("У каждого образца своя, от гладкого до шершавого. Узнаётся на столе трения.");
            else if (G.AtLimit(id)) sb.Append($"{G.LetterValue(id)} — предел прибора");
            else
            {
                double now = G.V(id), next = G.ValueAt(id, G.Level(id) + 1);
                sb.Append($"{GameState.FmtValue(id, now)} → <color=#8CFF99>{GameState.FmtValue(id, next)}</color>   за <color=#{ColorUtility.ToHtmlStringRGB(Apparatus.CurColor((int)d.cost))}>{GameState.FmtCur(G.UpgradeCost(id), d.cost)}</color>");
            }
            ShowTip(head, sb.ToString().Trim());
        }

        void OnLetterOpened(string id)
        {
            var d = Defs.L(id);
            Vector2 at = _tiles.TryGetValue(id, out var t) ? BoardToFixed(((RectTransform)t.transform).anchoredPosition) : Vector2.zero;
            at.x = Mathf.Clamp(at.x, -LW / 2 + 300, LW / 2 - 300);
            FX.I.Text(Content, at + new Vector2(0, 90), $"Новая величина: {d.sym} — {d.name}", ChalkTex.Green, 28, 2.6f);
        }

        void OnGateBought(string id)
        {
            FX.I.Text(Content, new Vector2(0, 220), Defs.Gt(id).hint, ChalkTex.Yellow, 30, 5f);
        }

        // ---------------- letter tiles on the map: clicks open/upgrade, drags pan ----------------
        public void TileBeginDrag(LetterTile tile, PointerEventData e) { _panning = true; HideTip(); }
        public void TileDrag(PointerEventData e) => OnDrag(e);
        public void TileEndDrag(PointerEventData e) { _panning = false; }

        // ---------------- panning ----------------
        public void OnBeginDrag(PointerEventData e)
        {
            if (e.pointerPressRaycast.gameObject != Content.gameObject) return;
            _panning = true;
            HideTip();
        }
        public void OnDrag(PointerEventData e) { if (_panning) { _panTarget = ClampPan(_panTarget + e.delta / Content.localScale.x); _pan = _panTarget; } }
        public void OnEndDrag(PointerEventData e) { _panning = false; }

        // ---------------- per-frame ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            // the wheel zooms the map; the point under the cursor stays where it is
            if (Mouse.current != null)
            {
                float sc = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(sc) > 0.01f)
                {
                    float old = _zoomT;
                    _zoomT = Mathf.Clamp(_zoomT * (sc > 0 ? 1.15f : 1f / 1.15f), 0.35f, 1.6f);
                    if (Mathf.Abs(old - _zoomT) > 1e-4f)
                    {
                        var m = UIF.ToLocal(Content, Mouse.current.position.ReadValue());
                        _panTarget = m - (m - _panTarget) / old * _zoomT;
                        HideTip();
                    }
                }
            }
            _zoom = Mathf.Lerp(_zoom, _zoomT, dt * 12f);
            _board.localScale = new Vector3(_zoom, _zoom, 1);
            if (!_panning) _panTarget = ClampPan(_panTarget);
            _pan = Vector2.Lerp(_pan, _panTarget, dt * 6f);
            _board.anchoredPosition = _pan;

            int shown = 0;
            _curIcons.Clear();
            for (int i = 0; i < 4; i++)
            {
                bool show = i < 2 || (i == 2 && G.Era >= 1) || (i == 3 && G.Era >= 2);
                _cur[i].gameObject.SetActive(show);
                if (!show) continue;
                // ideas carry no name: a chalk lightbulb follows the number, and the pair is centred as one block
                float cx = -370 + shown * 250;
                bool bulb = i == (int)Cur.Obs;
                float shift = bulb ? -23f : 0f;
                _cur[i].rectTransform.anchoredPosition = new Vector2(cx + shift, 500);
                if (bulb) DrawIcon(_curIcons, "bulb", ChalkTex.Cyan, 0.95f, new Vector2(cx + shift + _cur[i].preferredWidth / 2 + 28, 501));
                // the counter ticks up to its value and gives a little jump when it grows
                double target = G.Cur[i];
                if (target > _curTarget[i] + 1e-9 && GameState.Fmt(target) != GameState.Fmt(_curTarget[i])) _curFlash[i] = 1;
                _curTarget[i] = target;
                _shownCur[i] = target < _shownCur[i] ? target : _shownCur[i] + (target - _shownCur[i]) * Mathf.Min(1f, Time.deltaTime * 4f);
                if (System.Math.Abs(target - _shownCur[i]) < System.Math.Max(1e-6, target * 1e-4)) _shownCur[i] = target;
                _curFlash[i] = Mathf.Max(0, _curFlash[i] - Time.deltaTime * 2f);
                _cur[i].transform.localScale = Vector3.one * (1 + 0.18f * _curFlash[i]);
                _cur[i].text = GameState.FmtCur(_shownCur[i], (Cur)i);
                shown++;
            }

            // the buttons exist only once there is something behind them: no greyed-out promises
            _startShake = Mathf.Max(0, _startShake - Time.deltaTime * 3);
            bool ready = G.AnyStation && !G.Won;
            _startBtn.gameObject.SetActive(ready);
            _startBack.gameObject.SetActive(ready);
            if (ready)
            {
                _startBtn.anchoredPosition = new Vector2(720 + Mathf.Sin(Time.time * 60) * 8 * _startShake, -460);
                float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 4);
                _startBox.Clear();
                _startBox.Rect(Vector2.zero, new Vector2(392 + p * 8, 76 + p * 4), ChalkTex.Yellow, 4f);
                _startText.text = ChalkTex.Sym($"▶ Эксперимент {G.Lessons + 1}  ({G.MaxActions} действий)");
            }

            _alchShake = Mathf.Max(0, _alchShake - Time.deltaTime * 3);
            bool alch = G.AlchemyUnlocked && !G.Won;
            _alchBtn.gameObject.SetActive(alch);
            _alchBack.gameObject.SetActive(alch);
            if (alch)
            {
                _alchBtn.anchoredPosition = new Vector2(-720 + Mathf.Sin(Time.time * 60) * 8 * _alchShake, -460);
                int nq = G.DiscoverableCount;
                bool waiting = nq > 0;
                _alchText.text = ChalkTex.Sym(nq > 0 ? $"⚗ Теории  ·  {nq}" : "⚗ Теории");
                float q = waiting ? 0.5f + 0.5f * Mathf.Sin(Time.time * 4) : 0;
                _alchBox.Clear();
                _alchBox.Rect(Vector2.zero, new Vector2(392 + q * 8, 76 + q * 4), ChalkTex.Cyan, waiting ? 4f : 2.5f);
            }

            UpdateTip();
            _hintT -= Time.deltaTime;
            if (_hintT <= 0) { _hintT = 0.3f; _hint.text = Hint(); _hintBack.gameObject.SetActive(_hint.text.Length > 0); }
            _hint.color = new Color(0.45f, 0.86f, 1f, 0.7f + 0.3f * Mathf.Sin(Time.time * 3));
        }

        /// The map explains itself: nodes pulse when they can be bought, so nothing needs to be said.
        string Hint() => "";
    }
}
