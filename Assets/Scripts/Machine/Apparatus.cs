using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// The lesson screen: a school physics bench drawn in chalk. The teacher's box of samples sits at the bottom;
    /// the player drags a sample onto an instrument (or clicks an instrument that needs no sample). Each action
    /// spends one of the lesson's actions and makes the automated part of the bench work along.
    /// Instruments appear from the centre outwards; a camera keeps the built ones filling the screen.
    public class Apparatus : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float FW = 1920, FH = 1080;
        public RectTransform Content;          // the world: zoomed and panned by the camera
        RectTransform _frame, _fixed;

        const float Floor = -150;       // lower bench
        const float Shelf2 = 330;       // upper bench
        // the box of samples: under the bench, or — while the bench of the first epoch holds only one or two
        // instruments — right on the table beside them, so the camera can come close and the first instrument is big
        bool TrayOnTable { get { if (G.Revolutions > 0) return false; int n = 0; foreach (var kv in O) if (StationBuilt(kv.Key)) n++; return n <= 2; } }
        float TrayY => TrayOnTable ? Floor + 44 : -262;
        float TrayX
        {
            get
            {
                if (!TrayOnTable) return 0;
                float left = 1e9f;
                foreach (var kv in O) if (StationBuilt(kv.Key)) left = Mathf.Min(left, StationRect(kv.Key).xMin);
                if (left > 1e8f) left = 0;
                int n = Mathf.Max(1, G.Samples.Count);
                return left - 110 - ((n - 1) / 2f * 120f + 70);
            }
        }

        // instruments alternate right / left of the centre in the order they are discovered
        static readonly Dictionary<string, Vector2> O = new Dictionary<string, Vector2>
        {
            { "dyna", new Vector2(0, Floor) }, { "fall", new Vector2(250, Floor) }, { "spring", new Vector2(-230, Floor) },
            { "pend", new Vector2(530, Floor) }, { "arch", new Vector2(-560, Floor) }, { "density", new Vector2(850, Floor) },
            { "slide", new Vector2(-800, Floor) }, { "friction", new Vector2(1080, Floor) }, { "fly", new Vector2(-1060, Floor) },
            // the upper bench: the circuit in the middle (it grows both ways), the steam instruments on either side
            { "stand", new Vector2(0, Shelf2) },
            { "heater", new Vector2(560, Shelf2) }, { "engine", new Vector2(860, Shelf2) }, { "piston", new Vector2(1190, Shelf2) },
            { "calor", new Vector2(-590, Shelf2) }, { "ice", new Vector2(-850, Shelf2) },
        };
        // what each instrument occupies, relative to its origin (x, y, width, height)
        static readonly Dictionary<string, Rect> Extent = new Dictionary<string, Rect>
        {
            { "fall", new Rect(-120, -20, 270, 440) }, { "dyna", new Rect(-70, 0, 140, 340) }, { "spring", new Rect(-60, 0, 120, 310) },
            { "pend", new Rect(-80, 0, 240, 350) }, { "arch", new Rect(-80, -10, 280, 250) }, { "density", new Rect(-80, 0, 160, 230) },
            { "slide", new Rect(-100, 0, 230, 170) }, { "friction", new Rect(-90, 0, 200, 170) }, { "fly", new Rect(-100, 0, 230, 250) },
            { "stand", new Rect(-250, 0, 500, 240) },   // the circuit grows: see StationRect
            { "heater", new Rect(-150, 0, 240, 240) }, { "calor", new Rect(-80, 0, 170, 240) }, { "engine", new Rect(-100, 0, 340, 210) },
            { "ice", new Rect(-100, 0, 230, 180) }, { "piston", new Rect(-50, 0, 120, 210) },
        };

        class Flight { public float t; public Sample s; public int count = 1; }
        class Station
        {
            public string id;
            public float anim, cooldown, bounce, gear;
            public bool wet;                     // the bath: its specimen has already been dropped in
            public int incoming, queued;         // samples flying here; automatic firings waiting for the sample to land
            public Sample sample;                // sample used by the running animation
            public Sample resident;              // sample that stays on an automated instrument
            public readonly Sample[] sockets = new Sample[3];   // the circuit: a specimen in each of its sockets
            public readonly Sample[] sockIn = new Sample[3];    // specimens on their way to a socket
            public bool splashed;                // the specimen has already hit the water in this run
            public readonly List<Flight> flights = new List<Flight>();
        }
        readonly Dictionary<string, Station> _st = new Dictionary<string, Station>();

        /// A sample flying between the box and an instrument. While in transit (or on an instrument) it is busy.
        class Transit { public Sample s; public Vector2 from, to; public float t, dur; public string station; public System.Action<Transit> onArrive; }
        readonly List<Transit> _transits = new List<Transit>();
        readonly Dictionary<Sample, string> _busy = new Dictionary<Sample, string>();
        readonly Dictionary<string, double> _lastYield = new Dictionary<string, double>();
        int _pending;                            // actions reserved by samples on their way to an instrument

        ChalkShape _ground, _static, _dyn, _hud, _tray;
        Text _hudText, _ghostText, _launchText, _censusText, _thermoText;
        readonly Text[] _earn = new Text[4];     // what the run has brought, one counter per currency
        readonly float[] _earnFl = new float[4];
        readonly string[] _earnStr = { "", "", "", "" };
        RectTransform _launchBtn;
        ChalkShape _launchBox;
        int _pendHang;                           // rounds the current specimen has hung on the pendulum
        readonly Dictionary<string, Text> _labels = new Dictionary<string, Text>();
        readonly Dictionary<string, Text> _hints = new Dictionary<string, Text>();
        readonly List<Text> _sampleTexts = new List<Text>();
        // the water on the bench is simulated: a dropped specimen really splashes, and waves run to the walls
        readonly Pool _bath = new Pool(28) { Phase = 0f }, _cyl = new Pool(8) { Phase = 1.3f }, _pot = new Pool(14) { Phase = 2.1f },
                      _cal = new Pool(10) { Phase = 0.7f }, _melt = new Pool(16) { Phase = 2.9f };
        float _iceDent, _iceMelt, _meltLevel;    // how far the ice has given way in this run
        int _dragSocket = -1;                    // the socket of the circuit the dragged specimen came out of
        readonly Dictionary<string, double[]> _runBy = new Dictionary<string, double[]>();   // what each instrument brought this run
        RectTransform _ghost;
        ChalkShape _ghostShape;
        string _staticKey;
        public float Scale = 1;
        public float FrameScale => _frame != null ? _frame.localScale.x : 1f;
        int _dragSample = -1;
        string _dragFromStation;                 // the automated instrument the dragged sample was taken from
        bool _dragging;
        float _msgT, _bellWait, _spentT = -9;
        bool _bellPending, _summaryShown;
        float _zoom = 1, _zoomT = 1;
        Vector2 _cam, _camT;
        bool _camSnap = true;
        readonly Dictionary<string, double> _best = new Dictionary<string, double>();   // records per instrument (this session)
        readonly Dictionary<string, int> _fired = new Dictionary<string, int>();
        static readonly Sample Probe = new Sample { volR = 1 };

        float _pendAmp, _pendPhase, _pendAngle, _prevAngle; bool _lastRising; int _swingsLeft;
        float _wheelAngle, _engineAngle, _engineSpeed;

        GameState G => GameState.I;
        float ShelfY => Floor + 80 + ((float)G.V("h") - 1) / 19f * 220f;
        float PendLen => 100 + Mathf.Clamp01((float)G.V("l")) * 100f;
        Vector2 PendPivot => O["pend"] + new Vector2(40, 300);
        Vector2 PendDir => new Vector2(Mathf.Sin(_pendAngle), -Mathf.Cos(_pendAngle));
        Vector2 WheelC => O["fly"] + new Vector2(60, 100);
        float WeightX => O["fly"].x - 50;
        float WeightY(float lift) => Mathf.Lerp(O["fly"].y + 45, O["fly"].y + 215, lift);
        // ---- the circuit of the electric bench: its elements come from the theories found, in order
        List<StationArt.Part> Circuit => StationArt.BenchCircuit(G);
        Vector2 PartPos(string kind) { foreach (var p in Circuit) if (p.kind == kind) return O["stand"] + new Vector2(p.x, StationArt.WireTop); return O["stand"] + new Vector2(0, StationArt.WireTop); }
        Vector2 SocketPos(int i) { foreach (var p in Circuit) if (p.socket == i) return O["stand"] + new Vector2(p.x, StationArt.WireTop); return O["stand"] + new Vector2(0, StationArt.WireTop); }
        int Sockets => Defs.StandSlots(G);
        /// The circuit runs only with a specimen in every one of its sockets.
        bool CircuitReady { get { var s = _st["stand"]; int n = Sockets; if (n == 0) return false; for (int i = 0; i < n; i++) if (s.sockets[i] == null) return false; return true; } }
        int MissingSockets { get { var s = _st["stand"]; int m = 0; for (int i = 0; i < Sockets; i++) if (s.sockets[i] == null && s.sockIn[i] == null) m++; return m; } }
        Vector2 SwitchPos => PartPos("sw");
        static string Specimens(int n) => n % 10 == 1 && n % 100 != 11 ? "образец" : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14) ? "образца" : "образцов";
        Vector2 ValvePos => O["engine"] + new Vector2(-70, 60);
        Vector2 HeaterPos => O["heater"] + new Vector2(0, 110);
        Vector2 PistonPos => O["piston"] + new Vector2(0, 80);
        const float WheelR = 55;

        /// Where the chalk buddy stands on this screen (frame coordinates): on the floor, at the right edge.
        public Vector2 BuddyAnchor => new Vector2(900, _cam.y + Floor * _zoom + 84);

        public void Build()
        {
            var root = (RectTransform)transform;
            _frame = UIF.Rect("Frame", root, Vector2.zero, new Vector2(FW, FH));
            gameObject.AddComponent<ScaleToFit>().Init(_frame, FW, FH);
            UIF.Catcher(_frame);
            Content = UIF.Rect("World", _frame, Vector2.zero, Vector2.zero);
            // three depths of chalk: the table almost fades into the board, the instruments sit quietly on it,
            // and everything that moves stays bright
            _ground = UIF.Shape(Content, 300, "Ground");
            _ground.color = new Color(0.88f, 0.92f, 0.95f, 0.10f);
            _static = UIF.Shape(Content, 301, "Static");
            _static.color = new Color(0.88f, 0.92f, 0.95f, 0.20f);
            _dyn = UIF.Shape(Content, 303, "Dyn");
            _tray = UIF.Shape(Content, 304, "Tray");
            foreach (var kv in O)
            {
                _st[kv.Key] = new Station { id = kv.Key };
                _hints[kv.Key] = UIF.Text(Content, "", 26, ChalkTex.Cyan, Vector2.zero, new Vector2(420, 36));
                var t = UIF.Text(Content, "", 24, ChalkTex.White, Vector2.zero, new Vector2(340, 30));
                t.supportRichText = true;
                _labels[kv.Key] = t;
            }
            _hints["tray"] = UIF.Text(Content, "", 26, ChalkTex.Cyan, Vector2.zero, new Vector2(520, 36));
            _thermoText = UIF.Text(Content, "", 22, new Color(1f, 0.55f, 0.4f, 1f), Vector2.zero, new Vector2(120, 30));
            _ghost = UIF.Rect("Ghost", Content, Vector2.zero, new Vector2(80, 80));
            _ghostShape = UIF.Shape(_ghost, 99);
            _ghostText = UIF.Text(_ghost, "", 18, ChalkTex.White, new Vector2(0, -58), new Vector2(220, 24));
            _ghost.gameObject.SetActive(false);

            _fixed = UIF.Rect("Fixed", _frame, Vector2.zero, new Vector2(FW, FH));
            _hud = UIF.Shape(_fixed, 306, "Hud");
            _hudText = UIF.Text(_fixed, "", 38, ChalkTex.White, new Vector2(-560, 490), new Vector2(680, 52), TextAnchor.MiddleLeft);
            // earnings: one counter per currency, each jumping only when its own figure changes
            for (int i = 0; i < 4; i++) _earn[i] = UIF.Text(_fixed, "", 32, CurColor(i), Vector2.zero, new Vector2(320, 52), TextAnchor.MiddleRight);
            InkText.On(_earn[(int)Cur.Obs]);
            _censusText = UIF.Text(_fixed, "", 26, ChalkTex.Green, new Vector2(-560, 444), new Vector2(680, 36), TextAnchor.MiddleLeft);

            // the launch console: with it the instruments wait for "Пуск" instead of firing on their own
            _launchBtn = UIF.Rect("Launch", _fixed, new Vector2(660, -450), new Vector2(460, 84));
            UIF.Catcher(_launchBtn);
            _launchBox = UIF.Shape(_launchBtn, 307);
            _launchText = LetterTile.Fit(UIF.Text(_launchBtn, "", 32, ChalkTex.Yellow, Vector2.zero, new Vector2(440, 48)), 18, 32);
            var lc = _launchBtn.gameObject.AddComponent<Clickable>();
            lc.onClick = () => Launch();
            lc.onHover = h => _launchBtn.localScale = Vector3.one * (h ? 1.05f : 1f);
            _launchBtn.gameObject.SetActive(false);

            G.LessonStarted += ResetBench;
            G.LessonEnded += ResetBench;
        }

        void ResetBench()
        {
            _transits.Clear(); _busy.Clear(); _lastYield.Clear(); _pending = 0;
            foreach (var s in _st.Values)
            {
                s.sample = null; s.resident = null; s.incoming = 0; s.queued = 0; s.anim = 0; s.bounce = 0; s.wet = false; s.splashed = false; s.flights.Clear();
                for (int i = 0; i < 3; i++) { s.sockets[i] = null; s.sockIn[i] = null; }
            }
            _bath.Reset(); _cyl.Reset(); _pot.Reset(); _cal.Reset(); _melt.Reset();
            _iceDent = 0; _iceMelt = 0; _meltLevel = 0; _dragSocket = -1;
            _runBy.Clear();
            HideSummary();
            _dragSample = -1; _dragging = false; _ghost.gameObject.SetActive(false);
            _pendAmp = 0; _pendAngle = 0; _prevAngle = 0; _swingsLeft = 0; _pendHang = 0;
            _camSnap = true;
            _bellPending = false; _summaryShown = false; _bellWait = 0;
        }

        bool StationBuilt(string id)
        {
            foreach (var f in Defs.Formulas) if (f.station == id && G.Built(f.id)) return true;
            return false;
        }

        FormulaDef MainFormula(string station)
        {
            foreach (var f in Defs.Formulas) if (f.station == station && G.Built(f.id)) return f;
            return null;
        }

        bool NeedsSample(string station) { var f = MainFormula(station); return f != null && f.sample; }

        // ---------------- camera: the built instruments fill the screen ----------------
        Rect StationRect(string st)
        {
            var e = Extent[st]; var o = O[st];
            if (st == "fall") return new Rect(o.x + e.x, o.y + e.y, e.width, ShelfY - Floor + 170);
            if (st == "stand")
            {
                var c = Circuit; float x0 = StationArt.CircuitLeft(c) - 10, x1 = StationArt.CircuitRight(c) + 10;
                return new Rect(o.x + x0, o.y, x1 - x0, StationArt.WireTop + (G.Built("charge") ? 125 : 70));
            }
            return new Rect(o.x + e.x, o.y + e.y, e.width, e.height);
        }

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        void UpdateCamera(float dt)
        {
            int n = Mathf.Max(1, G.Samples.Count);
            float tl = 1e9f, tr = -1e9f;
            for (int i = 0; i < n; i++) { tl = Mathf.Min(tl, SamplePos(i).x - 80); tr = Mathf.Max(tr, SamplePos(i).x + 80); }
            if (G.Has("lab")) tr += 120;
            // the box and the built instruments (with room above them for a hint) fill the screen: a lone first
            // instrument is shown big, and no empty strip is left under the box
            var r = Rect.MinMaxRect(tl, TrayFloor(0) - 62, tr, TrayY + (TrayRows - 1) * 125f + 60);
            foreach (var kv in O) if (StationBuilt(kv.Key)) { var sr = StationRect(kv.Key); r = Union(r, Rect.MinMaxRect(sr.xMin, sr.yMin, sr.xMax, sr.yMax + 44)); }
            float w = r.width + 80, h = r.height + 24;
            _zoomT = Mathf.Clamp(Mathf.Min((FW - 80) / w, (FH - 118) / h), 0.7f, 2.0f);
            _camT = -r.center * _zoomT + new Vector2(0, -46);
            if (_camSnap) { _zoom = _zoomT; _cam = _camT; _camSnap = false; }
            else { _zoom = Mathf.Lerp(_zoom, _zoomT, dt * 2.5f); _cam = Vector2.Lerp(_cam, _camT, dt * 2.5f); }
            Content.localScale = new Vector3(_zoom, _zoom, 1);
            Content.anchoredPosition = _cam;
            Scale = _frame.localScale.x * _zoom;
            // texts keep their size on screen whatever the zoom
            var inv = Vector3.one / _zoom;
            foreach (var t in _labels.Values) t.transform.localScale = inv;
            foreach (var t in _hints.Values) t.transform.localScale = inv;
            foreach (var t in _sampleTexts) t.transform.localScale = inv;
            _thermoText.transform.localScale = inv;
        }

        // ---------------- actions ----------------
        Vector2 Local(PointerEventData e) => UIF.ToLocal(Content, e.position);

        // ---------------- sample flow: box -> instrument -> box ----------------
        public bool IsBusy(Sample s) => s != null && _busy.ContainsKey(s);
        public bool Free(Sample s) => !IsBusy(s);

        /// Where a sample lands on an instrument at the start of the experiment.
        Vector2 LandingPos(string st, Sample s)
        {
            var o = O[st]; float w = SampleSize(s);
            switch (st)
            {
                case "fall": return new Vector2(o.x + 12, ShelfY + w / 2);
                case "dyna": return o + new Vector2(0, 104 - w / 2);
                case "spring": return o + new Vector2(0, 118 + w / 2);
                case "pend": return PendPivot + new Vector2(0, -(PendLen + w / 2));
                case "arch": return o + new Vector2(60, 200);
                case "density": return o + new Vector2(0, 180);
                case "friction": return o + new Vector2(-55, w / 2);
                case "slide": return o + new Vector2(-70, w / 2);
                case "fly": return new Vector2(WeightX, WeightY(0) - w / 2);
                case "stand": { int i = FreeSocket(); return SocketPos(i < 0 ? 0 : i); }
                case "calor": return o + new Vector2(-4, 200);
                case "ice": return o + new Vector2(0, 76 - _iceMelt - _iceDent + w / 2);
                case "heater": return o + new Vector2(-22, 10 + w / 2);
            }
            return o + new Vector2(0, 60);
        }

        /// Where the sample is when the experiment lets go of it.
        Vector2 ReleasePos(string st, Sample s)
        {
            var o = O[st]; float w = SampleSize(s);
            switch (st)
            {
                case "fall": return new Vector2(o.x + 74, Floor + w / 2);
                case "arch": return o + new Vector2(60, s.Floats ? 95 + w * 0.5f - w * FloatDepth(s) : 12 + w / 2);
                case "calor": return o + new Vector2(-4, 14 + w / 2);
                case "density": return o + new Vector2(0, 34 + w / 2);
                case "friction": return o + new Vector2(30, w / 2);
                case "slide": return o + new Vector2(90, w / 2);
                case "pend": return PendPivot + PendDir * (PendLen + w / 2);
            }
            return LandingPos(st, s);
        }

        int IndexOf(Sample s) => G.Samples.IndexOf(s);

        void Fly(Sample s, Vector2 from, Vector2 to, string station, System.Action<Transit> onArrive)
        {
            float dur = Mathf.Clamp(Vector2.Distance(from, to) / 1000f, 0.14f, 0.8f);
            _transits.Add(new Transit { s = s, from = from, to = to, dur = dur, station = station, onArrive = onArrive });
        }

        /// The player (or the bot) lets a sample go over an instrument: it flies there and the action happens on landing.
        /// With the launch console nothing fires on landing — the specimen just takes its place and waits for "Пуск".
        public bool Drop(string st, Sample sample, Vector2 from)
        {
            if (sample == null || !StationBuilt(st)) return false;
            if (G.Phase != Phase.Lesson) return false;
            if (!NeedsSample(st)) { Sfx.Play("nope", 0.25f); Message(O[st] + new Vector2(40, 130), "этому опыту образец не нужен — просто кликни"); return false; }
            if (IsBusy(sample)) return false;
            if (st == "stand") return DropStand(sample, from);
            if (!Ready(st)) { Sfx.Play("nope", 0.25f); Message(O[st] + new Vector2(40, 130), "прибор занят"); return false; }
            bool launch = G.LaunchMode;
            bool samePlace = _dragFromStation == st;        // back where it came from: nothing was rearranged
            if (launch && !samePlace)
            {
                if (G.Places <= 0) { Sfx.Play("nope", 0.25f); Message(O[st] + new Vector2(40, 130), "перестановки кончились — жми «Пуск»"); return false; }
            }
            else if (G.ActionsLeft - _pending <= 0) { Sfx.Play("nope", 0.25f); Message(O[st] + new Vector2(40, 130), "действия кончились — эксперимент окончен"); return false; }
            string swapTo = _dragFromStation == "stand" ? null : _dragFromStation;   // swapping two loaded instruments: the other one goes there
            _busy[sample] = st; _pending++; _st[st].incoming++;
            if (G.Automated(st)) G.MarkUsed("auto:" + st);   // the player knows how to feed an automaton now
            Sfx.Play("whoosh", 0.25f, 1.6f);
            if (launch && !samePlace) { G.Places--; G.NotifyChanged(); }
            Fly(sample, from, LandingPos(st, sample), st, tr =>
            {
                _pending--; _st[st].incoming--;
                var stn = _st[st];
                if (stn.resident != null && stn.resident != sample)
                {   // the instrument was loaded: its specimen swaps over to where this one came from, or goes home
                    var old = stn.resident; stn.resident = null;
                    if (swapTo != null && swapTo != st && StationBuilt(swapTo) && _st[swapTo].resident == null) SendTo(old, swapTo, ReleasePos(st, old), !launch);
                    else Return(old, ReleasePos(st, old));
                }
                if (launch)
                {   // the console: the specimen simply stays on the instrument
                    stn.resident = sample; stn.wet = false;
                    if (st == "pend") { _pendHang = 0; _pendAmp = Mathf.Max(_pendAmp, 0.35f); }
                    G.MarkUsed(st);
                    Sfx.Play("tick", 0.35f);
                    Buddy.I?.Poke();
                }
                else if (!Act(st, sample)) Return(sample, tr.to);
            });
            return true;
        }

        /// The empty socket of the circuit nearest to a point (or the first one), -1 when all are taken.
        int FreeSocket(Vector2? near = null)
        {
            var s = _st["stand"]; int best = -1; float bd = 1e9f;
            for (int i = 0; i < Sockets; i++)
            {
                if (s.sockets[i] != null || s.sockIn[i] != null) continue;
                float d = near.HasValue ? Vector2.Distance(near.Value, SocketPos(i)) : i;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// Mounting a specimen in the circuit is assembly, not an experiment: it costs nothing. The circuit runs only
        /// once every socket is filled and the switch is closed — a click on the circuit, or «Пуск».
        bool DropStand(Sample sample, Vector2 from)
        {
            var s = _st["stand"];
            int i = FreeSocket(from);
            if (i < 0)
            {   // every socket is taken: the nearest specimen makes room and goes home
                float bd = 1e9f;
                for (int j = 0; j < Sockets; j++) { if (s.sockIn[j] != null) continue; float d = Vector2.Distance(from, SocketPos(j)); if (d < bd) { bd = d; i = j; } }
                if (i < 0) return false;
                var old = s.sockets[i]; s.sockets[i] = null;
                if (old != null) Return(old, SocketPos(i));
            }
            _busy[sample] = "stand"; s.sockIn[i] = sample;
            Sfx.Play("whoosh", 0.25f, 1.6f);
            int slot = i;
            Fly(sample, from, SocketPos(slot), "stand", tr =>
            {
                if (s.sockIn[slot] == sample) s.sockIn[slot] = null;
                if (!_busy.TryGetValue(sample, out var w) || w != "stand") return;
                s.sockets[slot] = sample;
                Sfx.Play("tick", 0.4f, 1.2f);
                FX.I.Dust(Content, SocketPos(slot), ChalkTex.Cyan, 5, 100f);
                Buddy.I?.Poke();
            });
            return true;
        }

        /// Moves a specimen straight from one instrument to another (the swap half of a rearrangement).
        /// The swap is one action for both instruments, so this one runs on arrival too.
        void SendTo(Sample sample, string st, Vector2 from, bool run)
        {
            _busy[sample] = st; _st[st].incoming++;
            Fly(sample, from, LandingPos(st, sample), st, tr =>
            {
                _st[st].incoming--;
                if (!_busy.TryGetValue(sample, out var w) || w != st) return;
                _st[st].resident = sample; _st[st].wet = false;
                if (st == "pend") _pendHang = 0;
                if (run) Do(st, false, sample);
            });
        }

        /// The launch console fires the whole bench at once: one action, then the rearrangements are refilled.
        public bool Launch()
        {
            if (G.Phase != Phase.Lesson || !G.LaunchMode || G.ActionsLeft <= 0) { Sfx.Play("nope", 0.25f); return false; }
            var run = new List<string>();
            foreach (var kv in O)
            {
                var st = kv.Key;
                if (!StationBuilt(st)) continue;
                if (st == "stand" ? !CircuitReady : NeedsSample(st) && _st[st].resident == null) continue;
                run.Add(st);
            }
            if (run.Count == 0) { Sfx.Play("nope", 0.25f); Message(new Vector2(TrayX, TrayY + 150), "поставь образец на прибор"); return false; }
            double n = G.AutoMult;
            int whole = Mathf.FloorToInt((float)n);
            if (Random.value < n - whole) whole++;
            foreach (var st in run)
            {
                int times = G.Automated(st) ? Mathf.Max(1, whole) : 1;
                var smp = NeedsSample(st) ? _st[st].resident : null;
                if (st == "pend") _pendHang++;
                for (int i = 0; i < times; i++) Do(st, false, smp);
                G.MarkUsed(st);
            }
            G.UseAction(); _spentT = Time.time;
            G.Places = G.PlaceBudget;
            Sfx.Play("zap", 0.45f, 0.7f);
            Buddy.I?.Poke();
            if (G.ActionsLeft <= 0) { _bellPending = true; _bellWait = 0; }
            return true;
        }

        /// The sample goes home to its slot in the box; it stays busy until it lands (with a puff of chalk).
        void Return(Sample sample, Vector2 from)
        {
            if (sample == null) return;
            int i = IndexOf(sample);
            if (i < 0) { _busy.Remove(sample); return; }
            _busy[sample] = "box";
            Fly(sample, from, SamplePos(i), "box", tr =>
            {
                if (_busy.TryGetValue(sample, out var where) && where == "box") _busy.Remove(sample);
                FX.I.Dust(Content, SamplePos(i) + new Vector2(0, -SampleSize(sample) / 2), ChalkTex.White, 4, 90f, 0.6f);
            });
        }

        /// An instrument is done with its sample: an automated one keeps it (it works every action from now on),
        /// a manual one sends it home.
        void Release(Station s)
        {
            if (s.sample == null) return;
            var smp = s.sample; s.sample = null;
            if (!_busy.TryGetValue(smp, out var where) || where != s.id) return;
            if (G.Automated(s.id)) s.resident = smp;
            else { if (s.resident == smp) s.resident = null; Return(smp, ReleasePos(s.id, smp)); }
        }

        /// Automatic firing: an automated instrument works only with the sample the player gave it.
        void AutoFire(string st, int times)
        {
            var s = _st[st];
            if (!NeedsSample(st)) { for (int i = 0; i < times; i++) Do(st, false, null); return; }
            if (s.resident == null)
            {   // the shelf: its sample may still be falling — count the extra drops into that flight
                if (st == "fall") { var fl = s.flights.Find(x => x.s != null && _busy.TryGetValue(x.s, out var w) && w == st); if (fl != null) fl.count += times * G.BallsPerDrop; }
                return;                                     // nothing assigned: the assistant idles
            }
            var smp = s.resident;
            if (st == "pend") _pendHang++;    // another round on the thread: the specimen gets less interesting
            for (int i = 0; i < times; i++) Do(st, false, smp);
        }

        public bool NeedsSampleAt(string st) => NeedsSample(st);
        public bool HasResident(string st) => _st.ContainsKey(st) && (st == "stand" ? MissingSockets == 0 : _st[st].resident != null || _st[st].incoming > 0);
        /// For the bot: the circuit wants another specimen in one of its sockets.
        public bool CircuitWants => StationBuilt("stand") && MissingSockets > 0;
        public bool CircuitArmed => StationBuilt("stand") && CircuitReady;
        bool InFlight(Sample smp) => _transits.Exists(t => t.s == smp) || _st["fall"].flights.Exists(f => f.s == smp) || _st["arch"].flights.Exists(f => f.s == smp);

        public bool Act(string id, Sample sample)
        {
            if (!StationBuilt(id)) return false;
            if (G.Phase != Phase.Lesson || G.ActionsLeft <= 0) { Sfx.Play("nope", 0.25f); return false; }
            // with the console nothing runs by itself: the whole bench waits for "Пуск"
            if (G.LaunchMode) { Sfx.Play("nope", 0.25f); Message(O[id] + new Vector2(40, 130), "жми «Пуск» — запустятся все приборы"); return false; }
            if (id == "stand" && !CircuitReady)
            {   // the circuit is open somewhere: it says how many specimens it still lacks
                Sfx.Play("nope", 0.25f);
                int m = MissingSockets;
                Message(O[id] + new Vector2(0, 260), m > 0 ? $"вставь в схему ещё {m} {Specimens(m)}" : "образец ещё летит");
                return false;
            }
            if (id != "stand" && NeedsSample(id) && sample == null)
            {
                Sfx.Play("nope", 0.25f);
                Message(O[id] + new Vector2(40, 130), "перетащи сюда образец из коробки");
                return false;
            }
            if (!Do(id, true, sample)) return false;
            G.UseAction(); _spentT = Time.time;
            G.MarkUsed(id);                                 // its hint has done its job
            Buddy.I?.Poke();
            AutoAlong(id);
            if (G.ActionsLeft <= 0) { _bellPending = true; _bellWait = 0; }
            return true;
        }

        void Message(Vector2 at, string text)
        {
            if (Time.time - _msgT < 0.8f) return;
            _msgT = Time.time;
            FX.I.Text(Content, at, text, ChalkTex.Red, 28, 1.6f);
        }

        void AutoAlong(string except)
        {
            double n = G.AutoMult;
            int whole = Mathf.FloorToInt((float)n);
            if (Random.value < n - whole) whole++;
            if (whole <= 0) return;
            foreach (var st in _st.Keys)
                if (st != except && G.Automated(st) && StationBuilt(st))
                    AutoFire(st, whole);
        }

        /// One-shot instrument. Manual starts the animation; an automaton starts it too when idle (so the player sees it
        /// work) and just counts extra firings while it runs.
        bool OneShot(Station s, bool manual, Sample sample, string formula, Vector2 at, string sfx, float pitch = 1f)
        {
            if (manual && s.anim > 0) return false;
            if (!manual && s.anim > 0) { s.bounce = 1f; Fire(formula, at, sample); return true; }
            s.anim = 0.001f; s.sample = sample;
            Sfx.Play(sfx, manual ? 0.4f : 0.25f, pitch);
            return true;
        }

        /// The whole circuit fires at once: every formula at its own element, with the specimen of its own socket.
        void FireStand()
        {
            var s = _st["stand"];
            Vector2 Up(string kind, float dy) => PartPos(kind) + new Vector2(0, dy);
            // every element works, but the circuit answers with one figure per currency, so the numbers never pile up
            double ideas = 0, coul = 0, joules = 0;
            ideas += Fire("ohm", Up("amp", 70), s.sockets[0], 1, true);
            coul += Fire("charge", Up("sw", 90), s.sockets[0], 1, true);
            ideas += Fire("pow", Up("lamp", 70), s.sockets[0], 1, true);
            joules += Fire("lamp", Up("lamp", 110), s.sockets[0], 1, true);
            joules += Fire("cap", Up("cap", 96), s.sockets[1], 1, true);
            joules += Fire("coil", Up("coil", 96), s.sockets[2], 1, true);
            if (_popT.TryGetValue("stand", out var t0) && Time.time - t0 < 0.6f) return;
            _popT["stand"] = Time.time;
            var mid = O["stand"] + new Vector2(0, StationArt.WireTop);
            float half = (StationArt.CircuitRight(Circuit) - StationArt.CircuitLeft(Circuit)) / 2;
            if (ideas > 0) FX.I.Text(Content, mid + new Vector2(-half * 0.55f, 120), "+" + GameState.FmtPrice(ideas, Cur.Obs), CurColor((int)Cur.Obs), 30, 0.9f);
            if (joules > 0) FX.I.Text(Content, mid + new Vector2(0, 150), "+" + GameState.FmtPrice(joules, Cur.J), CurColor((int)Cur.J), 34, 0.9f);
            if (coul > 0) FX.I.Text(Content, mid + new Vector2(half * 0.55f, 120), "+" + GameState.FmtPrice(coul, Cur.C), CurColor((int)Cur.C), 32, 0.9f);
        }

        bool Do(string id, bool manual, Sample sample)
        {
            var s = _st[id];
            switch (id)
            {
                case "fall":
                    if (manual && s.flights.Count >= G.ShelfCapacity) { Sfx.Play("nope", 0.2f); return false; }
                    // an automatic drop of the same sample counts as several (the stack) but is drawn once
                    if (!manual) { var ex = s.flights.Find(x => x.s == sample); if (ex != null) { ex.count += G.BallsPerDrop; return true; } }
                    s.flights.Add(new Flight { t = 0, s = sample, count = manual ? 1 : G.BallsPerDrop });
                    if (s.resident == sample) s.resident = null;      // the flight owns it until it comes back
                    if (manual) Sfx.Play("tick", 0.5f);
                    return true;
                case "dyna": return OneShot(s, manual, sample, "dyna", O["dyna"] + new Vector2(60, 200), "tick", 1.3f);
                case "spring":
                    if (manual && s.anim > 0) return false;
                    if (!manual && s.anim > 0) { Fire("spring", O["spring"] + new Vector2(0, 150), sample); return true; }
                    s.anim = 0.001f; s.sample = sample; Sfx.Play("boing", 0.4f, 1.1f, 0.1f, 0.1f);
                    return true;
                case "pend":
                    {   // the pendulum swings on its own; an action is just another push of the thread
                        if (sample == null) return false;
                        if (s.resident != sample && s.sample != sample) _pendHang = 0;   // a fresh specimen is interesting again
                        if (manual) { s.sample = sample; _pendHang = 0; }
                        _pendAmp = 0.18f + 0.37f * (float)G.PendFatigue(_pendHang);   // the less it gives, the smaller it swings
                        _pendPhase = 0; _prevAngle = _pendAmp; _lastRising = false;
                        _swingsLeft = 3 + (G.HasPerk("swings") ? 2 : 0);
                        Sfx.Play("whoosh", manual ? 0.4f : 0.25f, 1.4f);
                        return true;
                    }
                case "arch":
                    if (manual && s.flights.Count > 2) return false;
                    // already in the water: another splash where it lies
                    if (s.resident == sample && s.wet) { s.bounce = 1f; _bath.Nudge(O["arch"].x + 60, -110f, 18f); Fire("arch", O["arch"] + new Vector2(60, 150), sample); return true; }
                    { var ex = s.flights.Find(x => x.s == sample); if (ex != null) { ex.count++; return true; } }
                    s.flights.Add(new Flight { t = 0, s = sample }); Sfx.Play("tick", 0.4f, 1.1f);
                    return true;
                case "density": return OneShot(s, manual, sample, "density", O["density"] + new Vector2(0, 140), "tick", 1.1f);
                case "friction": return OneShot(s, manual, sample, "friction", O["friction"] + new Vector2(0, 110), "chalk", 0.8f);
                case "slide": return OneShot(s, manual, sample, "slide", O["slide"] + new Vector2(30, 110), "chalk", 0.6f);
                case "fly": return OneShot(s, manual, sample, "fly", WheelC + new Vector2(0, 100), "tick", 0.8f);
                case "stand":
                    if (!CircuitReady) return false;
                    if (manual && s.cooldown > 0) return false;
                    if (!manual && s.anim > 0) { s.bounce = 1f; FireStand(); return true; }
                    s.cooldown = 0.6f; s.anim = 0.001f;
                    FireStand();
                    Sfx.Play("zap", manual ? 0.5f : 0.3f); FX.I.Dust(Content, SwitchPos, ChalkTex.Cyan, 8, 160f);
                    return true;
                case "calor": return OneShot(s, manual, sample, "calm", O["calor"] + new Vector2(0, 240), "tick", 0.9f);
                case "ice": return OneShot(s, manual, sample, "ice", O["ice"] + new Vector2(0, 170), "hiss", 1.6f);
                case "heater":
                    if (s.anim <= 0) _pot.Splash(O["heater"].x - 22, 120f, 14f, 4);
                    return OneShot(s, manual, sample, "heat", HeaterPos + new Vector2(0, 80), "hiss", 1.3f);
                case "engine":
                    if (manual && s.cooldown > 0) return false;
                    s.cooldown = 0.6f; s.anim = 0.001f; _engineSpeed = 5f + 13f * G.Boiler;   // a cold engine barely turns
                    Fire("carnot", O["engine"] + new Vector2(-70, 170), null); Fire("engine", O["engine"] + new Vector2(70, 200), null); Sfx.Play("hiss", manual ? 0.6f : 0.35f);
                    return true;
                case "piston":
                    if (manual && s.anim > 0) return false;
                    s.anim = 0.001f;
                    Fire("gas", PistonPos + new Vector2(0, 80), null); Sfx.Play("whoosh", 0.4f, 0.9f);
                    return true;
            }
            return false;
        }

        readonly Dictionary<string, float> _popT = new Dictionary<string, float>();
        /// quiet = no yield popup (the circuit shows one sum per currency instead); returns what the firing gave.
        double Fire(string formula, Vector2 at, Sample sample, double extra = 1, bool quiet = false)
        {
            if (!G.Built(formula)) return 0;
            var f = Defs.F(formula);
            bool wasUnknown = sample != null && f.reveals != null && !sample.Knows(f.reveals);
            bool wasSecret = sample != null && sample.golden && !sample.knowGold;
            bool sci = GameState.Scientific(sample, f);
            // "критический опыт": now and then an experiment counts double
            double mult = (G.HasPerk("crit") && Random.value < 0.1f ? 2 : 1) * extra;
            double y = (f.kind == FKind.Energy ? G.YieldOf(f, f.yields, sample) : G.YieldOf(f, Cur.Obs, sample)) * mult;
            G.Fire(f, sample, mult);
            _lastYield[formula] = y;
            {   // the run's tally per instrument, for the results screen
                var yc = f.kind == FKind.Energy ? f.yields : Cur.Obs;
                if (!_runBy.TryGetValue(f.station, out var arr)) _runBy[f.station] = arr = new double[4];
                arr[(int)yc] += y;
            }
            if (formula == "heat") G.Boiler = Mathf.Min(1f, G.Boiler + G.BoilerRise);   // every heating warms the whole bench
            if (mult > 1) { FX.I.Text(Content, at + new Vector2(60, 40), "×2!", ChalkTex.Pink, 40, 1.2f, 2f); Sfx.Play("ding", 0.5f, 0.8f); }
            // a personal best for this instrument is worth a cheer (once it has run a few times)
            string stn = f.station;
            int nf = _fired.TryGetValue(stn, out var n0) ? n0 : 0;
            _fired[stn] = nf + 1;
            double best = _best.TryGetValue(stn, out var b0) ? b0 : 0;
            if (y > best)
            {
                _best[stn] = y;
                if (nf >= 3 && y > best * 1.05) { FX.I.Text(Content, at + new Vector2(0, 100), "Рекорд!", ChalkTex.Yellow, 36, 1.6f, 1.8f); Sfx.Play("ding", 0.6f, 1.5f); Buddy.I?.React(Buddy.Mood.Wow, 1.5f); }
            }
            if (wasUnknown)
            {
                string what = f.reveals == "m" ? $"m = {GameState.FmtValue("m", G.V("m") * sample.massR)}"
                    : f.reveals == "V" ? $"V = {GameState.FmtValue("V", G.V("V") * sample.volR)}"
                    : $"μ = {sample.mu:0.00}";
                FX.I.Text(Content, at + new Vector2(0, 46), what, ChalkTex.Green, 30, 1.6f);
                Sfx.Play("ding", 0.5f, 1.2f);
            }
            if (wasSecret && sample.knowGold)
            {   // the surprise: this one is golden
                FX.I.Text(Content, at + new Vector2(0, 70), $"Золотой образец!  ×{G.GoldMult:0}", ChalkTex.Yellow, 38, 2.2f);
                FX.I.Dust(Content, at, ChalkTex.Yellow, 24, 260f);
                Sfx.Play("correct", 0.8f, 1.3f); Sfx.Play("ding", 0.6f, 1.6f);
            }
            if (f.station == "arch" && sample != null && !sample.Floats)
            {   // the bath has nothing to say about what lies on its bottom
                if (!_popT.TryGetValue(formula, out var t1) || Time.time - t1 >= 0.6f) { _popT[formula] = Time.time; FX.I.Text(Content, at, "утонул", ChalkTex.Red, 28, 1.1f); }
                return y;
            }
            if (quiet) return y;
            if (_popT.TryGetValue(formula, out var t) && Time.time - t < 0.6f) return y;
            _popT[formula] = Time.time;
            var c = f.kind == FKind.Energy ? f.yields : Cur.Obs;
            FX.I.Text(Content, at, "+" + GameState.FmtPrice(y, c) + (sci ? "" : "  (½)"), CurColor((int)c), f.kind == FKind.Energy ? 32 : 28, 0.9f);
            return y;
        }

        // ---------------- input ----------------
        int SampleAt(Vector2 p)
        {
            for (int i = 0; i < G.Samples.Count; i++) if (Vector2.Distance(p, SamplePos(i)) < 48) return i;
            return -1;
        }

        string StationAt(Vector2 p)
        {
            bool In(string id, float x0, float x1, float y0, float y1) => StationBuilt(id) && p.x >= O[id].x + x0 && p.x <= O[id].x + x1 && p.y >= O[id].y + y0 && p.y <= O[id].y + y1;
            if (StationBuilt("stand") && StationRect("stand").Contains(p)) return "stand";
            if (In("calor", -75, 75, 0, 240)) return "calor";
            if (In("ice", -95, 95, 0, 170)) return "ice";
            if (In("engine", -100, 240, 0, 200)) return "engine";
            if (In("heater", -80, 80, 0, 200)) return "heater";
            if (In("piston", -50, 50, 0, 200)) return "piston";
            if (In("pend", -80, 160, 0, 330)) return "pend";
            if (In("fly", -100, 130, 0, 240)) return "fly";
            if (In("friction", -90, 90, 0, 160)) return "friction";
            if (In("slide", -100, 130, 0, 160)) return "slide";
            if (In("dyna", -70, 70, 0, 320)) return "dyna";
            if (In("spring", -60, 60, 0, 200)) return "spring";
            if (In("arch", -80, 200, -10, 240)) return "arch";
            if (In("density", -80, 80, 0, 220)) return "density";
            if (In("fall", -120, 130, -20, 440)) return "fall";
            return null;
        }

        public void OnPointerDown(PointerEventData e)
        {
            var p = Local(e);
            _dragging = false;
            int si = SampleAt(p);
            if (si >= 0)
            {
                if (IsBusy(G.Samples[si])) { Sfx.Play("nope", 0.2f); _dragSample = -1; return; }
                _dragSample = si; Sfx.Play("chalk", 0.4f); return;
            }
            _dragSample = -1; _dragFromStation = null;
            var st = StationAt(p);
            if (st == null) return;
            var stn = _st[st];
            if (st == "stand")
            {   // a specimen can be pulled out of its socket; a click anywhere else on the circuit closes the switch
                for (int i = 0; i < Sockets; i++)
                {
                    var smp = stn.sockets[i];
                    if (smp == null || Vector2.Distance(p, SocketPos(i)) > SampleSize(smp) / 2 + 12) continue;
                    _dragSample = IndexOf(smp); _dragFromStation = "stand"; _dragSocket = i; Sfx.Play("chalk", 0.4f); return;
                }
                Act("stand", null);
                return;
            }
            if (NeedsSample(st) && stn.resident != null && stn.anim <= 0 && !InFlight(stn.resident))
            {   // the sample on an automated instrument can be taken back (drag) or used again by hand (click)
                _dragSample = IndexOf(stn.resident); _dragFromStation = st; Sfx.Play("chalk", 0.4f); return;
            }
            if (NeedsSample(st)) { Sfx.Play("nope", 0.25f); Message(O[st] + new Vector2(40, 130), "перетащи сюда образец из коробки"); }
            else Act(st, null);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (_dragSample < 0) return;
            if (_dragFromStation == "stand" && _dragSocket >= 0)
            {   // out of its socket: the circuit is open again
                var stn = _st["stand"]; var smp = stn.sockets[_dragSocket]; stn.sockets[_dragSocket] = null;
                if (smp != null) _busy.Remove(smp);
            }
            else if (_dragFromStation != null)
            {   // lifted off its instrument: it is free again until it lands somewhere
                var stn = _st[_dragFromStation]; var smp = stn.resident; stn.resident = null;
                if (smp != null) _busy.Remove(smp);
            }
            _dragging = true;
            _ghost.gameObject.SetActive(true);
            _ghost.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData e) { if (_dragging) _ghost.anchoredPosition = Local(e); }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            _ghost.gameObject.SetActive(false);
            var p = Local(e);
            var st = StationAt(p);
            if (_dragSample >= 0 && _dragSample < G.Samples.Count)
            {
                var sample = G.Samples[_dragSample];
                // either it flies onto the instrument, or it flies back home
                if (st == null || !Drop(st, sample, p)) Return(sample, p);
            }
            _dragSample = -1; _dragFromStation = null; _dragSocket = -1;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_dragging) return;
            if (_dragFromStation != null && _dragSample >= 0 && _dragSample < G.Samples.Count) Act(_dragFromStation, G.Samples[_dragSample]);
            _dragSample = -1; _dragFromStation = null; _dragSocket = -1;
        }

        public bool Ready(string id)
        {
            if (!StationBuilt(id) || !_st.ContainsKey(id)) return false;
            var s = _st[id];
            if (s.incoming > 0) return false;
            switch (id)
            {
                case "fall": return s.flights.Count < G.ShelfCapacity;
                case "pend": return true;                       // a new specimen may always replace the hanging one
                case "arch": return s.flights.Count <= 2;
                case "stand": return true;                      // a specimen can always go in: a full circuit swaps one out
                case "engine": return s.cooldown <= 0;
                default: return s.anim <= 0;
            }
        }

        public bool BotAction(string station, Sample sample)
        {
            if (sample == null) return Act(station, NeedsSample(station) ? _st[station].resident : null);
            int i = IndexOf(sample);
            return Drop(station, sample, i >= 0 ? SamplePos(i) : new Vector2(TrayX, TrayY));
        }

        // ---------------- update ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            UpdateCamera(dt);
            string key = $"{G.Proven.Count}|{G.V("h")}|{G.V("l")}|{G.Revolutions}|{G.Devices.Count}|{G.Perks.Count}";
            if (key != _staticKey) { _staticKey = key; DrawStatic(); }

            foreach (var s in _st.Values)
            {
                s.cooldown = Mathf.Max(0, s.cooldown - dt); s.bounce = Mathf.Max(0, s.bounce - dt * 2.5f);
                bool busy = s.anim > 0 || s.bounce > 0 || s.flights.Count > 0 || (s.id == "pend" && _pendAmp > 0.1f);
                if (busy && G.Automated(s.id)) s.gear += dt * 7f;
            }
            for (int i = _transits.Count - 1; i >= 0; i--)
            {
                var tr = _transits[i];
                tr.t += dt / tr.dur;
                if (tr.t >= 1) { _transits.RemoveAt(i); tr.onArrive?.Invoke(tr); }
            }
            _bath.Step(dt); _cyl.Step(dt); _pot.Step(dt); _cal.Step(dt); _melt.Step(dt);
            if (_bellPending && G.Phase == Phase.Lesson)
            {   // the bell waits for the bench to go quiet, then the lesson's tally is shown before the curtain
                _bellWait += dt;
                if (!_summaryShown && _bellWait > 0.8f && (Quiet() || _bellWait > 7f)) { _summaryShown = true; ShowSummary(); }
                if (_summaryShown) UpdateSummary(Time.unscaledDeltaTime * Mathf.Max(1f, Time.timeScale));
            }

            {
                var s = _st["fall"];
                float dur = 0.3f + Mathf.Clamp(Mathf.Sqrt(2f * (float)G.V("h") / (float)G.V("g")) * 0.6f, 0.4f, 1.1f);
                for (int i = s.flights.Count - 1; i >= 0; i--)
                {
                    s.flights[i].t += dt / dur;
                    if (s.flights[i].t >= 1)
                    {
                        var fl = s.flights[i];
                        s.flights.RemoveAt(i);
                        for (int k = 0; k < fl.count; k++) Fire("fall", new Vector2(O["fall"].x + 60, Floor + 40), fl.s);
                        Sfx.Play("thud", 0.35f, 1f, 0.15f, 0.06f);
                        FX.I.Dust(Content, new Vector2(O["fall"].x + 60, Floor), ChalkTex.White, 4, 100f, 0.8f);
                        if (fl.s != null && _busy.TryGetValue(fl.s, out var where) && where == "fall")
                        {
                            if (G.Automated("fall")) { var smp = fl.s; Fly(smp, ReleasePos("fall", smp), LandingPos("fall", smp), "fall", tr => { if (_busy.TryGetValue(smp, out var w2) && w2 == "fall") s.resident = smp; }); }
                            else { if (s.resident == fl.s) s.resident = null; Return(fl.s, ReleasePos("fall", fl.s)); }
                        }
                    }
                }
            }
            // every instrument takes the same time for one run, so no single station is the slow one to wait for
            Anim("dyna", Run, 0.5f, st => Fire("dyna", O["dyna"] + new Vector2(60, 200), st.sample));
            Anim("spring", Run, 0.35f, st => Fire("spring", O["spring"] + new Vector2(0, 150), st.sample));
            Anim("density", Run, 0.55f, st => Fire("density", O["density"] + new Vector2(0, 140), st.sample));
            Anim("friction", Run, 0.6f, st => Fire("friction", O["friction"] + new Vector2(0, 110), st.sample));
            Anim("slide", Run, 0.65f, st => Fire("slide", O["slide"] + new Vector2(30, 110), st.sample));
            {
                var s = _st["fly"];
                if (s.anim > 0) _wheelAngle += dt * (1 + 14 * s.anim);
                Anim("fly", Run, 0.85f, st => { Fire("fly", WheelC + new Vector2(0, 100), st.sample); Sfx.Play("whoosh", 0.3f, 1.1f, 0.1f, 0.2f); });
            }
            Anim("stand", Run, 2f, null);
            Anim("calor", Run, 0.6f, st => Fire("calm", O["calor"] + new Vector2(0, 240), st.sample));
            Anim("ice", Run, 0.6f, st =>
            {   // the ice gives way: a deeper hollow, a little less ice, more water in the dish
                Fire("ice", O["ice"] + new Vector2(0, 170), st.sample);
                _iceDent = Mathf.Min(20f, _iceDent + 4f); _iceMelt = Mathf.Min(18f, _iceMelt + 1.5f); _meltLevel = Mathf.Min(14f, _meltLevel + 2.2f);
                _melt.Splash(O["ice"].x + Random.Range(-70f, 70f), 60f, 10f, 2);
            });
            Anim("heater", Run, 0.5f, st => Fire("heat", HeaterPos + new Vector2(0, 80), st.sample));
            Anim("engine", Run, 2f, null);
            Anim("piston", Run, 2f, null);
            _engineSpeed = Mathf.Max(0, _engineSpeed - dt * 5f);          // the flywheel of the engine spins down with momentum
            _engineAngle += dt * _engineSpeed;
            {
                var s = _st["arch"];
                for (int i = s.flights.Count - 1; i >= 0; i--)
                {
                    float prev = s.flights[i].t;
                    s.flights[i].t += dt / 2.0f;
                    if (s.flights[i].t >= 0.3f && prev < 0.3f)
                    {   // it hits the water: a dent that springs back as a jet, a crown of droplets, waves off to the walls
                        var fs = s.flights[i].s;
                        float str = fs != null ? 170f + 60f * Mathf.Clamp((float)fs.massR, 0.5f, 3f) : 200f;
                        _bath.Splash(O["arch"].x + 60, str, fs != null ? SampleSize(fs) * 0.8f : 30f);
                        Sfx.Play("splash", 0.35f, 1f, 0.15f, 0.1f);
                    }
                    if (s.flights[i].t >= 1)
                    {
                        var fl = s.flights[i]; s.flights.RemoveAt(i);
                        for (int k = 0; k < fl.count; k++) Fire("arch", O["arch"] + new Vector2(60, 150), fl.s);
                        if (fl.s != null && _busy.TryGetValue(fl.s, out var where) && where == "arch")
                        {
                            if (G.Automated("arch")) { s.resident = fl.s; s.wet = true; }
                            else { if (s.resident == fl.s) s.resident = null; Return(fl.s, ReleasePos("arch", fl.s)); }
                        }
                    }
                }
            }
            if (StationBuilt("pend"))
            {   // a damped swing: the sample on the thread is pulled aside and let go; each period is one observation
                var s = _st["pend"];
                if (_pendAmp > 0.03f)
                {
                    _pendPhase += dt * Mathf.PI * 2 / G.SwingPeriod;
                    _pendAmp *= Mathf.Exp(-dt * (_swingsLeft > 0 ? 0.08f : 2.5f));
                    _pendAngle = _pendAmp * Mathf.Cos(_pendPhase);
                    bool rising = _pendAngle >= _prevAngle;
                    if (!_lastRising && rising && _swingsLeft > 0)
                    {
                        var who = s.sample ?? s.resident;
                        Fire("pend", PendPivot + PendDir * PendLen + new Vector2(0, -40), who, G.PendFatigue(_pendHang));
                        Sfx.Play("tock", 0.2f, 1f, 0.05f, 0.1f);
                        _swingsLeft--;
                    }
                    _lastRising = rising;
                    _prevAngle = _pendAngle;
                }
                else
                {
                    _pendAmp = 0;
                    _pendAngle = Mathf.Lerp(_pendAngle, 0, dt * 4);
                    if (s.sample != null && Mathf.Abs(_pendAngle) < 0.02f) Release(s);
                }
            }

            DrawDynamic();
            DrawTray();
            DrawHints();
            DrawHud();
            UpdateLabels();
        }

        bool Quiet()
        {
            if (_transits.Count > 0 || _pendAmp > 0.1f) return false;
            foreach (var s in _st.Values) if (s.anim > 0 || s.flights.Count > 0 || s.cooldown > 0 || s.incoming > 0) return false;
            return true;
        }

        // ---------------- the results of a run: a screen of its own ----------------
        /// Swallows every pointer event, so nothing under the results screen can be grabbed.
        class Swallow : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public System.Action onPress;
            public void OnPointerDown(PointerEventData e) => onPress?.Invoke();
            public void OnPointerUp(PointerEventData e) { }
            public void OnPointerClick(PointerEventData e) { }
            public void OnBeginDrag(PointerEventData e) { }
            public void OnDrag(PointerEventData e) { }
            public void OnEndDrag(PointerEventData e) { }
        }

        RectTransform _sum;
        CanvasGroup _sumGroup;
        readonly List<RectTransform> _sumLines = new List<RectTransform>();
        Text _sumFoot;
        float _sumT, _sumIdle;
        int _sumShown;
        bool _sumSkip;

        string StationTitle(string st) => st == "stand" ? "Электрическая схема" : st == "engine" ? "Паровая машина" : MainFormula(st)?.title ?? st;

        /// The bell has rung: the bench steps back under a veil and a card comes up; the results appear on it line by
        /// line — every instrument with what it brought, the census, the total. A click shows them all at once, the
        /// next one (or a few quiet seconds) closes the run.
        void ShowSummary()
        {
            Sfx.Play("bell", 0.7f, 1.1f);
            Buddy.I?.React(Buddy.Mood.Joy, 2.4f);
            // the census adds its share now, so the total on the card is what the run really brought
            double bonus = G.CensusBonus;
            if (bonus > 0) for (int i = 0; i < 4; i++) G.Earn((Cur)i, G.LessonEarned[i] * bonus);

            HideSummary();
            _sum = UIF.Rect("Results", _fixed, Vector2.zero, new Vector2(FW * 2, FH * 2));
            _sum.SetAsLastSibling();
            var veil = _sum.gameObject.AddComponent<Image>();
            veil.color = new Color(ChalkTex.BoardColor.r, ChalkTex.BoardColor.g, ChalkTex.BoardColor.b, 0.8f);
            veil.raycastTarget = true;
            _sum.gameObject.AddComponent<Swallow>().onPress = SummaryClick;
            _sumGroup = _sum.gameObject.AddComponent<CanvasGroup>();
            _sumGroup.alpha = 0;
            var back = Lab.Backing(_sum, Vector2.zero, new Vector2(1060, 720), new Color(0.105f, 0.14f, 0.125f, 0.97f));
            var frame = UIF.Shape(_sum, 391);
            var title = UIF.Text(_sum, $"Эксперимент {G.Lessons + 1} окончен", 54, ChalkTex.White, Vector2.zero, new Vector2(1000, 70));

            string Amounts(double[] a)
            {
                var sb = new System.Text.StringBuilder();
                foreach (int i in new[] { 0, 2, 3, 1 })
                    if (a[i] > 0) sb.Append((sb.Length > 0 ? "     " : "") + $"<color=#{ColorUtility.ToHtmlStringRGB(CurColor(i))}>+{GameState.FmtPrice(a[i], (Cur)i)}</color>");
                return sb.ToString();
            }
            var rows = new List<(string left, string right, Color col, int size)>();
            foreach (var kv in O)
            {   // one line per instrument that worked, in the order they stand on the bench
                if (!_runBy.TryGetValue(kv.Key, out var arr)) continue;
                string a = Amounts(arr);
                if (a.Length > 0) rows.Add((StationTitle(kv.Key), a, new Color(1, 1, 1, 0.8f), 29));
            }
            if (G.Census) rows.Add(($"Опись: изучено {G.StudiedCount}", bonus > 0 ? $"итог +{bonus * 100:0}%" : "—", ChalkTex.Green, 29));
            rows.Add(("Итого", Amounts(G.LessonEarned), ChalkTex.Yellow, 40));

            // the card is as tall as what it has to say, and stands in the middle of the screen
            int n = rows.Count;
            float step = Mathf.Min(50f, 420f / Mathf.Max(1, n - 1));
            float h = 150 + (n - 1) * step + 110 + 70;
            float top = h / 2;
            back.sizeDelta = new Vector2(1060, h);
            frame.Rect(Vector2.zero, new Vector2(1060, h), ChalkTex.White, 4f);
            frame.Rect(Vector2.zero, new Vector2(1082, h + 22), new Color(1, 1, 1, 0.3f), 2.5f);
            title.rectTransform.anchoredPosition = new Vector2(0, top - 68);
            frame.Line(new Vector2(-320, top - 108), new Vector2(320, top - 105), new Color(1, 1, 1, 0.5f), 3f);
            for (int i = 0; i < n; i++)
            {
                bool total = i == n - 1;
                float y = total ? top - 150 - (n - 2) * step - 96 : top - 150 - i * step;
                var line = UIF.Rect("Row", _sum, new Vector2(0, y), new Vector2(980, 56));
                line.gameObject.AddComponent<CanvasGroup>().alpha = 0;
                UIF.Text(line, rows[i].left, rows[i].size, rows[i].col, new Vector2(-230, 0), new Vector2(480, 54), TextAnchor.MiddleLeft);
                var r = UIF.Text(line, "", rows[i].size, Color.white, new Vector2(200, 0), new Vector2(560, 54), TextAnchor.MiddleRight);
                InkText.On(r).Set(rows[i].right);
                if (total) { var ul = UIF.Shape(line, 392); ul.Line(new Vector2(-470, 38), new Vector2(470, 38), new Color(1, 1, 1, 0.4f), 2.5f); }
                _sumLines.Add(line);
            }
            _sumFoot = UIF.Text(_sum, "нажми, чтобы продолжить", 28, new Color(1f, 0.84f, 0.32f, 0), new Vector2(0, -top + 44), new Vector2(900, 40));
        }

        void UpdateSummary(float dt)
        {
            if (_sum == null) return;
            _sumT += dt;
            _sumGroup.alpha = Mathf.Clamp01(_sumT / 0.3f);
            for (int i = 0; i < _sumLines.Count; i++)
            {   // the lines come one by one, each with a little punch
                float t0 = _sumSkip ? 0 : 0.5f + i * 0.34f;
                float k = Mathf.Clamp01((_sumT - t0) / 0.22f);
                if (k > 0 && i >= _sumShown)
                {
                    _sumShown = i + 1;
                    if (!_sumSkip) Sfx.Play(i == _sumLines.Count - 1 ? "correct" : "tick", i == _sumLines.Count - 1 ? 0.6f : 0.35f, 1f + i * 0.05f);
                }
                _sumLines[i].GetComponent<CanvasGroup>().alpha = k;
                _sumLines[i].localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, k * k);
            }
            if (_sumShown < _sumLines.Count) return;
            _sumIdle += dt;
            _sumFoot.color = new Color(1f, 0.84f, 0.32f, (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3)) * Mathf.Clamp01(_sumIdle / 0.5f));
            if (_sumIdle > 9f) CloseSummary();          // nobody at the board: the run closes by itself
        }

        void SummaryClick()
        {
            if (_sumShown < _sumLines.Count) { _sumSkip = true; return; }   // the first click shows everything at once
            if (_sumIdle < 0.25f) return;
            CloseSummary();
        }

        void CloseSummary()
        {
            if (_sum == null) return;
            HideSummary();
            _bellPending = false;
            G.EndLesson();
            Sfx.Play("bell", 0.9f, 0.8f);
        }

        void HideSummary()
        {
            if (_sum != null) Destroy(_sum.gameObject);
            _sum = null; _sumLines.Clear(); _sumT = 0; _sumShown = 0; _sumIdle = 0; _sumSkip = false;
        }

        const float Run = 1.3f;           // how long one run of any instrument takes

        void Anim(string id, float duration, float at, System.Action<Station> onFire)
        {
            var s = _st[id];
            if (s.anim <= 0) return;
            float prev = s.anim;
            s.anim += Time.deltaTime / duration;
            if (onFire != null && prev < at && s.anim >= at) onFire(s);
            if (s.anim >= 1) { s.anim = 0; Release(s); }
        }

        // ---------------- samples ----------------
        public static float SampleSize(Sample s) => 28 + 14 * (float)s.volR;


        /// Draws a sample exactly the same way everywhere: size hints the volume, hatching (with the trained eye) hints the mass,
        /// ticks on the top edge (with the trained eye) hint the roughness. Golden samples are yellow. rot = tilted (on a thread).
        void DrawSample(ChalkShape S, Sample s, Vector2 c, float alpha = 1f, bool selected = false, float rot = 0f)
        {
            float w = SampleSize(s), h = w / 2;
            var col = s.golden && s.knowGold ? ChalkTex.Yellow : ChalkTex.White;
            col.a *= alpha;
            // one drawing for every angle, so a tilted specimen keeps its own hatching instead of a generic square
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            Vector2 R(Vector2 v) => c + (rot == 0f ? v : new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs));
            S.Poly(new[] { R(new Vector2(-h, -h)), R(new Vector2(h, -h)), R(new Vector2(h, h)), R(new Vector2(-h, h)) }, col, selected ? 4f : 2.8f, true);
            Fill(S, R, w - 8, s.pattern, 9f, new Color(col.r, col.g, col.b, 0.45f * alpha));
            if (s.knowMu)
            {
                int ticks = Mathf.RoundToInt((float)s.mu * 8);
                for (int i = 0; i < ticks; i++) { float x = -h + 6 + i * (w - 12) / 8f; S.Poly(new[] { R(new Vector2(x, h)), R(new Vector2(x + 3, h + 5)) }, col, 1.8f, false, false); }
            }
            if (selected)
            {
                float g = h + 8;
                S.Poly(new[] { R(new Vector2(-g, -g)), R(new Vector2(g, -g)), R(new Vector2(g, g)), R(new Vector2(-g, g)) }, new Color(0.45f, 0.86f, 1f, alpha), 2.5f, true);
            }
        }

        /// Six hatch styles so the specimens in a box are easy to tell apart. Every point goes through the
        /// same mapping as the outline, so the pattern turns with the specimen.
        static void Fill(ChalkShape S, System.Func<Vector2, Vector2> R, float size, int pattern, float sp, Color col)
        {
            float h = size / 2;
            void Rows(float step) { for (float y = -h + step * 0.5f; y < h; y += step) S.Line(R(new Vector2(-h, y)), R(new Vector2(h, y)), col, 1.8f); }
            void Cols(float step) { for (float x = -h + step * 0.5f; x < h; x += step) S.Line(R(new Vector2(x, -h)), R(new Vector2(x, h)), col, 1.8f); }
            switch (pattern)
            {
                case 0: Rows(sp); Cols(sp); break;
                case 1: Diag(S, R, h, sp, col); break;
                case 2: Rows(sp * 1.5f); Cols(sp * 1.5f); Diag(S, R, h, sp * 1.5f, col); break;
                case 3: Rows(sp); break;
                case 4: Cols(sp); break;
                default:
                    for (float y = -h + sp * 0.5f; y < h; y += sp)
                        for (float x = -h + sp * 0.5f; x < h; x += sp)
                            S.Line(R(new Vector2(x - 1.5f, y - 1.5f)), R(new Vector2(x + 1.5f, y + 1.5f)), col, 2.4f);
                    break;
            }
        }

        /// Backslash hatch (lines x + y = k) clipped to the square.
        static void Diag(ChalkShape S, System.Func<Vector2, Vector2> R, float h, float sp, Color col)
        {
            for (float k = -2 * h + sp * 0.5f; k < 2 * h; k += sp)
            {
                float x0 = Mathf.Max(-h, k - h), x1 = Mathf.Min(h, k + h);
                if (x1 <= x0) continue;
                S.Line(R(new Vector2(x0, k - x0) * 0.95f), R(new Vector2(x1, k - x1) * 0.95f), col, 1.8f);
            }
        }

        /// How deep a floating specimen sits: a denser one rides lower.
        static float FloatDepth(Sample s) => Mathf.Clamp((float)(s.massR / s.volR) / 1.2f, 0.25f, 0.9f);

        /// A specimen dropped into the bath: it falls from where it hung, hits the water, and either plunges under and
        /// bobs back up to float, or sinks with a wobble to the bottom.
        Vector2 ArchFlightPos(Flight fl, out float rot)
        {
            var o = O["arch"]; float cx = o.x + 60; float w = SampleSize(fl.s); float t = fl.t;
            float hang = o.y + 200, surf = _bath.Rest;
            rot = 0;
            if (t < 0.3f) { float k = t / 0.3f; return new Vector2(cx, Mathf.Lerp(hang, surf + w / 2, k * k)); }
            float q = Mathf.Clamp01((t - 0.3f) / 0.55f);
            rot = Mathf.Sin(q * 8f) * 0.18f * (1 - q);
            if (fl.s.Floats)
            {
                float deep = surf - w * 0.8f, top = _bath.Surface(cx) + w * 0.5f - w * FloatDepth(fl.s);
                float y = q < 0.3f ? Mathf.Lerp(surf + w / 2, deep, Mathf.SmoothStep(0, 1, q / 0.3f)) : Mathf.Lerp(deep, top, 1 - Mathf.Pow(1 - (q - 0.3f) / 0.7f, 3));
                return new Vector2(cx, y);
            }
            float bottom = o.y + 12 + w / 2;
            return new Vector2(cx + Mathf.Sin(q * 7f) * 5f * (1 - q), Mathf.Lerp(surf + w / 2, bottom, 1 - (1 - q) * (1 - q)));
        }

        /// A glowing sample: the one carrying current in the circuit, or heating up.
        void Glow(ChalkShape S, Vector2 c, float r, float strength)
        {
            var col = new Color(1f, 0.84f, 0.32f, 0.5f * strength);
            int rays = Mathf.RoundToInt(6 + 6 * strength);
            for (int i = 0; i < rays; i++)
            {
                float a = i / (float)rays * Mathf.PI * 2 + Time.time * 1.5f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                S.Line(c + d * (r + 4), c + d * (r + 12 + 6 * Mathf.Sin(Time.time * 9 + i)), col, 2.2f);
            }
        }

        const int PerRow = 32;           // one row, always: the box never stacks its specimens
        int TrayRows => (Mathf.Max(1, G.Samples.Count) + PerRow - 1) / PerRow;
        /// The floor of a row of the box: specimens rest on it, so a big one sits higher than a small one.
        float TrayFloor(int row) => TrayY + row * 125f - 40f;

        /// The box holds up to eight samples in a row; a fuller box gets a second row above the first.
        Vector2 SamplePos(int i)
        {
            int n = Mathf.Max(1, G.Samples.Count);
            int row = i / PerRow, inRow = Mathf.Min(PerRow, n - row * PerRow), col = i % PerRow;
            float w = i >= 0 && i < G.Samples.Count ? SampleSize(G.Samples[i]) : 42f;
            return new Vector2(TrayX + (col - (inRow - 1) / 2f) * 120f, TrayFloor(row) + w / 2 + 3);
        }

        void DrawTray()
        {
            _tray.Clear();
            bool lesson = G.Phase == Phase.Lesson;
            for (int i = _sampleTexts.Count; i < G.Samples.Count; i++) _sampleTexts.Add(UIF.Text(Content, "", 17, ChalkTex.White, Vector2.zero, new Vector2(110, 52)));
            for (int i = 0; i < _sampleTexts.Count; i++) _sampleTexts[i].gameObject.SetActive(lesson && i < G.Samples.Count);
            if (!lesson) return;
            int n = G.Samples.Count;
            var bc = new Color(1, 1, 1, 0.45f);
            var W = ChalkTex.White; var Dim = new Color(1, 1, 1, 0.45f);
            for (int r = 0; r < TrayRows; r++)
            {
                int first = r * PerRow, last = Mathf.Min(n, (r + 1) * PerRow) - 1;
                float left = SamplePos(first).x - 70, right = SamplePos(last).x + 70, y = TrayFloor(r);
                _tray.Line(new Vector2(left, y), new Vector2(right, y), bc, 3f);
                _tray.Line(new Vector2(left, y), new Vector2(left - 6, y + 84), bc, 3f);
                _tray.Line(new Vector2(right, y), new Vector2(right + 6, y + 84), bc, 3f);
                for (int i = first; i < last; i++) { float mx = (SamplePos(i).x + SamplePos(i + 1).x) / 2; _tray.Line(new Vector2(mx, y), new Vector2(mx, y + 34), new Color(1, 1, 1, 0.2f), 2f); }
            }
            if (G.Has("lab"))
            {   // the lab assistant by the box (holding scales once he weighs everything)
                float ax = SamplePos(Mathf.Min(n, PerRow) - 1).x + 130, ay = TrayY - 40;
                _tray.Circle(new Vector2(ax, ay + 96), 14, W, 2.5f);
                _tray.Circle(new Vector2(ax - 6, ay + 97), 4, W, 1.5f); _tray.Circle(new Vector2(ax + 6, ay + 97), 4, W, 1.5f);
                _tray.Line(new Vector2(ax, ay + 82), new Vector2(ax, ay + 30), W, 2.5f);
                _tray.Line(new Vector2(ax, ay + 30), new Vector2(ax - 12, ay), W, 2.5f); _tray.Line(new Vector2(ax, ay + 30), new Vector2(ax + 12, ay), W, 2.5f);
                _tray.Line(new Vector2(ax, ay + 70), new Vector2(ax - 22, ay + 50), W, 2.2f); _tray.Line(new Vector2(ax, ay + 70), new Vector2(ax + 22, ay + 50 + Mathf.Sin(Time.time * 3) * 4), W, 2.2f);
                if (G.HasPerk("autoweigh")) { _tray.Line(new Vector2(ax + 22, ay + 50), new Vector2(ax + 22, ay + 36), Dim, 1.5f); _tray.Line(new Vector2(ax + 8, ay + 36), new Vector2(ax + 36, ay + 36), Dim, 2f); _tray.Line(new Vector2(ax + 8, ay + 36), new Vector2(ax + 8, ay + 30), Dim, 1.5f); _tray.Line(new Vector2(ax + 36, ay + 36), new Vector2(ax + 36, ay + 30), Dim, 1.5f); }
            }
            for (int i = 0; i < n; i++)
            {
                var s = G.Samples[i];
                var c = SamplePos(i);
                bool away = IsBusy(s) || (_dragging && _dragSample == i);
                if (!away)
                {
                    DrawSample(_tray, s, c, 1f, false);
                    if (s.golden && (s.knowGold || G.HasPerk("shine")))
                    {   // a golden one twinkles
                        for (int k = 0; k < 3; k++)
                        {
                            float a = Time.time * 1.3f + k * 2.1f; float r = SampleSize(s) / 2 + 12 + 3 * Mathf.Sin(Time.time * 4 + k);
                            var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                            var yc = new Color(1f, 0.84f, 0.32f, 0.5f + 0.5f * Mathf.Sin(Time.time * 6 + k * 2));
                            _tray.Line(p + new Vector2(-4, 0), p + new Vector2(4, 0), yc, 2f); _tray.Line(p + new Vector2(0, -4), p + new Vector2(0, 4), yc, 2f);
                        }
                    }
                }
                else
                {   // an empty slot: the sample is out on the bench
                    float w = SampleSize(s), hw = w / 2;
                    var dc = new Color(1, 1, 1, 0.25f);
                    _tray.Dashed(c + new Vector2(-hw, -hw), c + new Vector2(hw, -hw), dc, 2f, 6f, 6f);
                    _tray.Dashed(c + new Vector2(hw, -hw), c + new Vector2(hw, hw), dc, 2f, 6f, 6f);
                    _tray.Dashed(c + new Vector2(hw, hw), c + new Vector2(-hw, hw), dc, 2f, 6f, 6f);
                    _tray.Dashed(c + new Vector2(-hw, hw), c + new Vector2(-hw, -hw), dc, 2f, 6f, 6f);
                }
                bool massHere = G.Built("dyna") || G.Built("calm");                     // something on this bench can weigh it
                string m = s.knowM ? "<color=#8CFF99>" + GameState.FmtValue("m", G.V("m") * s.massR) + "</color>" : massHere ? "m?" : "";
                string v = s.knowV ? "<color=#8CFF99>" + GameState.FmtValue("V", G.V("V") * s.volR) + "</color>" : (G.Built("arch") ? "V?" : "");
                string mu = s.knowMu ? "<color=#8CFF99>μ " + s.mu.ToString("0.00") + "</color>" : (G.Built("slide") ? "μ?" : "");
                var t = _sampleTexts[i];
                // two short lines under the box floor, so the labels never run into each other when the box is full
                t.rectTransform.anchoredPosition = new Vector2(c.x, TrayFloor(i / PerRow) - 26);
                string rest = v + (mu.Length > 0 ? (v.Length > 0 ? " " : "") + mu : "");
                t.text = m + (rest.Length > 0 ? (m.Length > 0 ? "\n" : "") + rest : "") + (s.golden && s.knowGold ? $"\n<color=#FFD752>золотой ×{G.GoldMult:0}</color>" : "")
                       + (G.Census && G.Measured(s) ? "\n<color=#8CFF99>изучен ×1.25</color>" : "");
                t.color = new Color(1, 1, 1, away ? 0.45f : 0.75f);
            }
            if (_dragging && _dragSample >= 0 && _dragSample < n)
            {
                _ghostShape.Clear();
                var gsm = G.Samples[_dragSample];
                DrawSample(_ghostShape, gsm, Vector2.zero, 0.9f, true);
                // what is known about it travels with it
                var parts = new List<string>();
                if (gsm.knowM || G.Built("dyna") || G.Built("calm")) parts.Add(gsm.knowM ? GameState.FmtValue("m", G.V("m") * gsm.massR) : "m?");
                if (G.Built("arch")) parts.Add(gsm.knowV ? GameState.FmtValue("V", G.V("V") * gsm.volR) : "V?");
                if (G.Built("slide")) parts.Add(gsm.knowMu ? "μ " + gsm.mu.ToString("0.00") : "μ?");
                _ghostText.text = string.Join(" · ", parts);
                _ghostText.transform.localScale = Vector3.one / _zoom;
            }
        }

        // ---------------- devices: a gear that spins while the automaton works ----------------
        Vector2 GearAt(string st)
        {
            var o = O[st];
            switch (st)
            {
                case "dyna": return o + new Vector2(52, 300);
                case "spring": return o + new Vector2(62, 60);
                case "pend": return PendPivot + new Vector2(60, -30);
                case "arch": return o + new Vector2(205, 165);
                case "density": return o + new Vector2(68, 180);
                case "friction": return o + new Vector2(112, 62);
                case "slide": return o + new Vector2(-112, 70);
                case "fly": return o + new Vector2(-95, 235);
                case "calor": return o + new Vector2(90, 205);
                case "ice": return o + new Vector2(118, 100);
                case "heater": return o + new Vector2(78, 200);
                case "engine": return o + new Vector2(-98, 130);
                case "piston": return o + new Vector2(62, 110);
            }
            return o + new Vector2(60, 60);
        }

        static void Gear(ChalkShape S, Vector2 c, float r, float angle, Color col) => StationArt.Gear(S, c, r, angle, col);

        // ---------------- static drawing ----------------
        void DrawStatic()
        {
            var S = _static;
            S.Clear();
            _ground.Clear();
            var W = ChalkTex.White;
            _ground.Ground(new Vector2(-1300, Floor), new Vector2(1400, Floor), W, 4f, 10f, 16f);
            {   // the upper bench spans the instruments that stand on it
                float x0 = 1e9f, x1 = -1e9f;
                foreach (var kv in O) if (kv.Value.y == Shelf2 && StationBuilt(kv.Key)) { var r = StationRect(kv.Key); x0 = Mathf.Min(x0, r.xMin - 60); x1 = Mathf.Max(x1, r.xMax + 60); }
                if (x1 > x0) _ground.Ground(new Vector2(x0, Shelf2), new Vector2(x1, Shelf2), W, 3.5f, 8f, 16f);
            }
            // every instrument, with whatever the player has bought for it drawn on
            var look = new StationArt.Look { shelfY = ShelfY, pendLen = PendLen, G = G };
            foreach (var kv in O)
            {
                if (!StationBuilt(kv.Key)) continue;
                StationArt.Draw(S, kv.Key, kv.Value, look);
            }
            if (StationBuilt("engine") && StationBuilt("heater")) S.Dashed(O["heater"] + new Vector2(62, 80), O["engine"] + new Vector2(-60, 100), new Color(1f, 0.5f, 0.35f, 0.5f), 2f, 8f, 8f);

        }

        static void DashedBox(ChalkShape S, Vector2 c, Vector2 size, Color col)
        {
            Vector2 h = size / 2;
            S.Dashed(c + new Vector2(-h.x, -h.y), c + new Vector2(h.x, -h.y), col, 2f, 10f, 8f);
            S.Dashed(c + new Vector2(h.x, -h.y), c + new Vector2(h.x, h.y), col, 2f, 10f, 8f);
            S.Dashed(c + new Vector2(h.x, h.y), c + new Vector2(-h.x, h.y), col, 2f, 10f, 8f);
            S.Dashed(c + new Vector2(-h.x, h.y), c + new Vector2(-h.x, -h.y), col, 2f, 10f, 8f);
        }

        // ---------------- dynamic drawing ----------------
        void DrawDynamic()
        {
            var D = _dyn;
            D.Clear();
            var W = ChalkTex.White; var Dim = new Color(1, 1, 1, 0.5f);

            {   // shelf: the sample waits on the edge, then tips over and falls; the pusher's rod shoves it
                var o = O["fall"]; var s = _st["fall"]; float sy = ShelfY;
                float rod = 0;
                if (s.resident != null)
                {
                    DrawSample(D, s.resident, LandingPos("fall", s.resident));
                    int extra = G.BallsPerDrop - 1;                    // the stack waiting behind it
                    for (int i = 1; i <= extra; i++) DrawSample(D, s.resident, LandingPos("fall", s.resident) + new Vector2(-9 * i, 9 * i), 0.35f);
                }
                foreach (var fl in s.flights)
                {
                    if (fl.t < 0 || fl.s == null) continue;
                    float w = SampleSize(fl.s);
                    // pushed along the shelf with growing speed, then it tips over the edge, turning as it falls
                    if (fl.t < 0.3f) { float k = fl.t / 0.3f; DrawSample(D, fl.s, new Vector2(o.x + 12 + 48 * k * k, sy + w / 2), 1f); rod = Mathf.Max(rod, 48 * k * k + 12 - w / 2 + 60); }
                    else { float k = (fl.t - 0.3f) / 0.7f; DrawSample(D, fl.s, new Vector2(o.x + 60 + 14 * k, Mathf.Lerp(sy + w / 2, Floor + w / 2, k * k)), 1f, false, -k * 1.5f); }
                    if (fl.t > 0.26f && fl.t < 0.46f)
                    {   // a few crumbs break off the edge at the tipping point
                        float q = (fl.t - 0.26f) / 0.2f;
                        for (int i = 0; i < 3; i++)
                        {
                            var cp = new Vector2(o.x + 38 + i * 5, sy - 2 - q * (14 + i * 6));
                            D.Line(cp, cp + new Vector2(-3 - i, -4), new Color(1, 1, 1, 0.6f * (1 - q)), 2f);
                        }
                    }
                }
                if (G.Has("hopper"))
                {
                    float tip = o.x - 92 + Mathf.Max(20, rod);
                    D.Line(new Vector2(o.x - 92, sy + 18), new Vector2(tip, sy + 18), W, 3.5f);
                    D.Line(new Vector2(tip, sy + 8), new Vector2(tip, sy + 28), W, 3.5f);
                    Gear(D, new Vector2(o.x - 106, sy + 24), 11, s.gear, Dim);
                }
            }
            if (StationBuilt("dyna"))
            {   // the hook hangs from the plate; the sample hangs on the hook and stretches the spring by its weight
                var o = O["dyna"]; var s = _st["dyna"];
                var hanging = s.anim > 0 ? s.sample : s.resident;
                float k = s.anim <= 0 ? (hanging != null ? 1 : 0) : Mathf.Min(1, s.anim / 0.25f);
                float heavy = hanging != null ? Mathf.Clamp((float)hanging.massR * (hanging.golden ? 3 : 1), 0.5f, 3f) : 1f;
                float stretch = k * (8 + 14 * heavy) + Mathf.Sin(s.bounce * 12) * 4 * s.bounce;
                float plateY = 150 - stretch;
                D.Zigzag(o + new Vector2(0, 270), o + new Vector2(0, plateY), 6, 10, W, 2.5f);
                D.Line(o + new Vector2(-16, plateY - 8), o + new Vector2(16, plateY - 8), ChalkTex.Cyan, 4f);
                D.Line(o + new Vector2(0, plateY - 8), o + new Vector2(0, plateY - 26), W, 2.5f);
                D.Arc(o + new Vector2(0, plateY - 36), 10, Mathf.PI * 0.2f, Mathf.PI * 1.6f, ChalkTex.Cyan, 3f);
                float ny = Mathf.Clamp(plateY + 66, 146, 250);                  // the needle reads the load off the scale
                D.Line(o + new Vector2(-17, ny), o + new Vector2(9, ny), ChalkTex.Red, 2.5f);
                if (k > 0.01f && hanging != null) DrawSample(D, hanging, o + new Vector2(0, plateY - 46 - SampleSize(hanging) / 2), Mathf.Min(1, k * 2));
            }
            if (StationBuilt("spring"))
            {   // vertical spring: the sample sits on the plate; a heavier one squeezes the spring further and flies higher
                var o = O["spring"]; var s = _st["spring"];
                var sp = s.anim > 0 ? s.sample : s.resident;
                float heavy = sp != null ? Mathf.Clamp((float)sp.massR, 0.5f, 2f) : 1f;
                float comp = s.anim <= 0 ? 0 : s.anim < 0.3f ? s.anim / 0.3f : s.anim < 0.42f ? 1 - (s.anim - 0.3f) / 0.12f : 0;
                float rest = 1 - (sp != null ? 0.08f * heavy : 0);                 // the weight alone presses it a little
                float plate = 8 + 110 * rest * (1 - 0.45f * comp * Mathf.Sqrt(heavy));
                D.Zigzag(o + new Vector2(0, 8), o + new Vector2(0, plate), 7, 14, W, G.HasPerk("springx2") ? 4.5f : 3f);
                if (G.HasPerk("springx3")) { D.Zigzag(o + new Vector2(-18, 8), o + new Vector2(-18, plate), 7, 8, W, 2.5f); D.Zigzag(o + new Vector2(18, 8), o + new Vector2(18, plate), 7, 8, W, 2.5f); }
                D.Line(o + new Vector2(-26, plate), o + new Vector2(26, plate), ChalkTex.Cyan, 5f);
                if (sp != null)
                {
                    float w = SampleSize(sp);
                    float fly = s.anim <= 0 || s.anim < 0.42f ? 0 : Mathf.Sin(Mathf.Clamp01((s.anim - 0.42f) / 0.58f) * Mathf.PI) * 120 * heavy;
                    DrawSample(D, sp, o + new Vector2(0, plate + w / 2 + fly), 1f);
                    if (fly > 6) for (int i = -1; i <= 1; i++) D.Line(o + new Vector2(i * 13, plate + fly - 4), o + new Vector2(i * 13, plate + fly - 16 - Mathf.Abs(i) * 4), new Color(1, 1, 1, 0.45f), 2f);
                }
            }
            if (StationBuilt("arch"))
            {   // real water: the specimen hangs over it, drops in with a splash whose waves run to the walls and back;
                // a floater rides the waves, a sinker lies on the bottom
                var o = O["arch"]; var s = _st["arch"];
                System.Func<float, float> wl = y => o.x - 60 - 10 * (y - o.y - 12) / 118f;
                System.Func<float, float> wr = y => o.x + 180 + 10 * (y - o.y - 12) / 118f;
                _bath.Rest = o.y + 95; _bath.Left = wl(o.y + 95); _bath.Right = wr(o.y + 95);
                float cx = o.x + 60;
                if (s.resident != null)
                {
                    var r = s.resident; float w = SampleSize(r);
                    if (!s.wet)
                    {   // until the experiment drops it in, the specimen hangs over the water on a thread
                        var at = LandingPos("arch", r) + new Vector2(0, Mathf.Sin(Time.time * 2f) * 3);
                        DrawSample(D, r, at);
                        D.Line(new Vector2(at.x, at.y + w / 2), new Vector2(at.x, o.y + 250), new Color(1, 1, 1, 0.35f), 2f);
                    }
                    else if (r.Floats)
                    {
                        float tilt = (_bath.Surface(cx + 14) - _bath.Surface(cx - 14)) / 28f;
                        DrawSample(D, r, new Vector2(cx, _bath.Surface(cx) + w * 0.5f - w * FloatDepth(r)), 1f, false, Mathf.Atan(tilt));
                    }
                    else DrawSample(D, r, ReleasePos("arch", r) + new Vector2(0, Mathf.Sin(s.bounce * 12) * 4 * s.bounce));
                }
                foreach (var fl in s.flights)
                {
                    if (fl.s == null) continue;
                    var p = ArchFlightPos(fl, out float rot);
                    DrawSample(D, fl.s, p, 1f, false, rot);
                }
                _bath.Draw(D, o.y + 16, wl, wr);           // drawn over the specimen: what is under water shows through it
            }
            if (StationBuilt("density"))
            {   // the specimen drops into the cylinder with a splash; the water climbs by its volume, the pointer swings
                var o = O["density"]; var s = _st["density"];
                var sample = s.anim > 0 ? s.sample : s.resident;
                float k = s.anim <= 0 ? (sample != null ? 1 : 0) : Mathf.Min(1, s.anim / 0.4f);
                float w = sample != null ? SampleSize(sample) : 0;
                float sy = sample != null ? Mathf.Lerp(180, 34 + w / 2, k * k) : 0;
                bool under = sample != null && o.y + sy - w / 2 < o.y + 98;
                float level = 70 + (under ? Mathf.Clamp01((o.y + 98 - (o.y + sy - w / 2)) / Mathf.Max(1, w)) * 12 * (float)sample.volR : 0);
                _cyl.Rest = o.y + 28 + level; _cyl.Left = o.x - 38; _cyl.Right = o.x + 38;
                if (sample != null && s.anim > 0 && !s.splashed && under) { s.splashed = true; _cyl.Splash(o.x, 150f, 14f, 4); Sfx.Play("splash", 0.2f, 1.5f, 0.1f, 0.1f); }
                if (s.anim <= 0) s.splashed = false;
                if (sample != null && k > 0) DrawSample(D, sample, o + new Vector2(0, sy), 1f);
                _cyl.Draw(D, o.y + 30, y => o.x - 38, y => o.x + 38);
                float na = Mathf.PI / 2 - (sample != null ? k * 0.9f * (float)sample.massR / 3f : 0) + Mathf.Sin(s.bounce * 12) * 0.2f * s.bounce;
                D.Line(o + new Vector2(0, 165), o + new Vector2(0, 165) + new Vector2(Mathf.Cos(na), Mathf.Sin(na)) * 13, ChalkTex.Red, 2.5f);
            }
            if (StationBuilt("friction"))
            {   // the sample is pulled to the right by the dynamometer's spring
                var o = O["friction"]; var s = _st["friction"];
                var sample = s.anim > 0 ? s.sample : s.resident;
                float k = s.anim <= 0 ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(s.anim / 0.7f));
                float x = -55 + 85 * k;
                if (sample != null)
                {
                    float w = SampleSize(sample);
                    float shake = s.anim > 0.15f && s.anim < 0.8f ? Mathf.Sin(Time.time * 55) * 1.6f : 0;   // it judders as it goes
                    DrawSample(D, sample, o + new Vector2(x, w / 2 + shake));
                    float pull = 8 + 12 * (float)sample.mu * (s.anim > 0 && s.anim < 0.75f ? 1 : 0.2f);
                    D.Zigzag(o + new Vector2(x + w / 2, 26), o + new Vector2(70 - 13 - pull * 0.5f, 26), 4, 6, W, 2f);
                }
            }
            if (StationBuilt("slide"))
            {   // the pusher shoves the sample along the table; it slows down and stops
                var o = O["slide"]; var s = _st["slide"];
                var sample = s.anim > 0 ? s.sample : s.resident;
                float k = s.anim <= 0 ? 0 : 1 - Mathf.Pow(1 - Mathf.Clamp01(s.anim / 0.8f), 2.2f);
                float x = -70 + 160 * k;
                D.Line(o + new Vector2(-90 + 20 * Mathf.Clamp01(k * 4), 8), o + new Vector2(-90 + 20 * Mathf.Clamp01(k * 4), 50), ChalkTex.Cyan, 5f);
                if (sample != null)
                {
                    DrawSample(D, sample, o + new Vector2(x, SampleSize(sample) / 2));
                    if (s.anim > 0.2f && s.anim < 0.85f) for (int i = 0; i < 3; i++) D.Line(o + new Vector2(x - 20 - i * 8, 4 + i * 2), o + new Vector2(x - 30 - i * 8, 6 + i * 2), new Color(1f, 0.6f, 0.4f, 0.7f), 2f);
                    if (s.anim > 0) for (float q = -66; q < x - 14; q += 22) D.Line(o + new Vector2(q, 3), o + new Vector2(q + 9, 3), new Color(1, 1, 1, 0.22f), 2f);   // scuffs on the table
                }
            }
            if (StationBuilt("pend"))
            {   // the thread ends at the top of the sample, which swings tilted with it
                var s = _st["pend"];
                Vector2 piv = PendPivot;
                var bob = s.sample ?? s.resident;
                if (bob != null)
                {
                    float w = SampleSize(bob);
                    if (_pendAmp > 0.05f)
                    {   // the path it sweeps, left behind like a smudge of chalk
                        float pr = PendLen + w / 2;
                        D.Arc(piv, pr, -Mathf.PI / 2 - _pendAmp, -Mathf.PI / 2 + _pendAmp, new Color(1, 1, 1, 0.16f + 0.14f * _pendAmp), 2f);
                    }
                    D.Line(piv, piv + PendDir * PendLen, W, 2.5f);
                    DrawSample(D, bob, piv + PendDir * (PendLen + w / 2), 1f, false, _pendAngle);
                }
                else D.Line(piv, piv + new Vector2(0, -PendLen), Dim, 2f);
            }
            if (StationBuilt("fly"))
            {   // the sample is the weight on the rope; falling, it spins the wheel (blurring the spokes)
                var s = _st["fly"]; var c = WheelC;
                D.Circle(c, WheelR, W, G.HasPerk("flyx2") ? 6f : 4f, false);
                D.Circle(c, WheelR - 10, W, 2.5f, false);
                float speed = s.anim > 0 ? s.anim : 0;
                var sc = new Color(1, 1, 1, Mathf.Lerp(0.95f, 0.35f, speed));
                for (int i = 0; i < 6; i++) { float a = _wheelAngle + i / 6f * Mathf.PI * 2; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); D.Line(c + d * 10, c + d * (WheelR - 10), sc, 3f); }
                if (speed > 0.3f) D.Circle(c, WheelR - 22, new Color(1, 1, 1, 0.25f * speed), 6f, false);
                if (speed > 0.25f) for (int i = 0; i < 3; i++) { float sa = _wheelAngle * 0.6f + i * 2.1f; D.Arc(c, WheelR + 9 + i * 5, sa, sa + 0.5f + 0.5f * speed, new Color(1, 1, 1, 0.3f * speed), 2f); }
                float lift = s.anim <= 0 ? 0 : 1 - s.anim;
                float wy = WeightY(lift);
                var weight = s.anim > 0 ? s.sample : s.resident;
                D.Line(new Vector2(WeightX, WeightY(1) + 35), new Vector2(WeightX, wy), W, 2f);
                if (weight != null) DrawSample(D, weight, new Vector2(WeightX, wy - SampleSize(weight) / 2), 1f);
            }
            if (StationBuilt("stand"))
            {   // the circuit: a specimen in each socket, the knife closes, the current runs round, every element answers
                var s = _st["stand"]; var o = O["stand"];
                var parts = Circuit;
                bool on = s.anim > 0;
                float k = on ? Mathf.Sin(Mathf.Clamp01(s.anim) * Mathf.PI) : 0;
                float yT = o.y + StationArt.WireTop, yB = o.y + StationArt.WireBot;
                float xL = o.x + StationArt.CircuitLeft(parts), xR = o.x + StationArt.CircuitRight(parts);
                var sw = SwitchPos;
                var hinge = sw + new Vector2(-24, 0);
                float ang = Mathf.Lerp(0.95f, 0f, on ? Mathf.Clamp01(s.anim / 0.06f) : 0);
                D.Line(hinge, hinge + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 48, ChalkTex.Cyan, 4f);
                if (on && s.anim < 0.25f)
                {   // a spark at the contact the moment the knife closes
                    float q = 1 - s.anim / 0.25f; var cp = sw + new Vector2(24, 0);
                    for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2 + 0.4f; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); D.Line(cp + d * 4, cp + d * (10 + 7 * q), new Color(1f, 0.84f, 0.32f, q), 2.2f); }
                }
                var gap = new Color(1f, 0.84f, 0.32f, 0.35f + 0.3f * Mathf.Sin(Time.time * 5));
                foreach (var p in parts)
                {
                    var c = new Vector2(o.x + p.x, yT);
                    if (p.socket >= 0)
                    {
                        var smp = s.sockets[p.socket];
                        if (smp != null) { DrawSample(D, smp, c); if (on) Glow(D, c, SampleSize(smp) / 2, 0.25f + 0.55f * k); }
                        else if (s.sockIn[p.socket] == null && !(_dragging && _dragSample >= 0)) DashedBox(D, c, new Vector2(46, 46), gap);   // what the circuit still lacks
                    }
                    switch (p.kind)
                    {
                        case "amp": { float na = Mathf.PI * (0.8f - 0.6f * k); D.Line(c, c + new Vector2(Mathf.Cos(na), Mathf.Sin(na)) * 14, ChalkTex.Red, 2.5f); break; }
                        case "lamp":
                            if (!on) break;
                            D.HatchCircle(c, 13, new Color(1f, 0.84f, 0.32f, 0.3f + 0.6f * k), 2f, 5f);
                            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2 + Time.time * 2; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); D.Line(c + d * 22, c + d * (30 + 10 * k + 3 * Mathf.Sin(Time.time * 10 + i)), ChalkTex.Yellow, 2.3f); }
                            break;
                        case "cap":
                            if (!on) break;
                            {   // the plates light up with their charge: + on one, − on the other
                                var fc = new Color(1f, 0.84f, 0.32f, 0.9f * k);
                                D.Line(c + new Vector2(-40, -44), c + new Vector2(-40, 44), fc, 3f); D.Line(c + new Vector2(40, -44), c + new Vector2(40, 44), fc, 3f);
                                var pc = new Color(0.45f, 0.86f, 1f, k);
                                D.Line(c + new Vector2(-58, 36), c + new Vector2(-48, 36), pc, 2.5f); D.Line(c + new Vector2(-53, 31), c + new Vector2(-53, 41), pc, 2.5f);
                                D.Line(c + new Vector2(48, 36), c + new Vector2(58, 36), pc, 2.5f);
                            }
                            break;
                        case "coil":
                            if (!on) break;
                            for (int i = 1; i <= 2; i++)
                            {   // the field closes round the coil on both sides
                                var fc = new Color(1f, 0.6f, 0.85f, 0.7f * k);
                                foreach (float side in new[] { -1f, 1f })
                                {
                                    var pts = new List<Vector2>();
                                    for (int j = 0; j <= 30; j++) { float a = j / 30f * Mathf.PI * 2; pts.Add(c + new Vector2(side * (60 + i * 8) + Mathf.Cos(a) * (8 + i * 7), Mathf.Sin(a) * (30 + i * 12))); }
                                    D.Poly(pts, fc, 1.8f, true, false);
                                }
                            }
                            break;
                    }
                }
                if (on)
                {   // the current: dashes running round the frame
                    Vector2[] loop = { new Vector2(xR, yT), new Vector2(xR, yB), new Vector2(xL, yB), new Vector2(xL, yT) };
                    float off = (Time.time * 70f) % 24f;
                    for (int i = 0; i < loop.Length - 1; i++)
                    {
                        var p0 = loop[i]; var p1 = loop[i + 1]; float len = Vector2.Distance(p0, p1); var dir = (p1 - p0) / Mathf.Max(1, len);
                        for (float q = off; q < len; q += 24f) D.Line(p0 + dir * q, p0 + dir * Mathf.Min(len, q + 9f), new Color(1f, 0.84f, 0.32f, 0.85f), 3f);
                    }
                }
                if (G.Built("charge"))
                {   // the stopwatch runs while the switch is closed
                    var cp = o + StationArt.ClockPos(parts);
                    float ha = Mathf.PI / 2 - (on ? Mathf.Clamp01(s.anim) * Mathf.PI * 2 : 0);
                    D.Line(cp, cp + new Vector2(Mathf.Cos(ha), Mathf.Sin(ha)) * 12, ChalkTex.Yellow, 2.2f);
                }
            }
            if (StationBuilt("heater"))
            {   // the pot: the specimen sits on the bottom, the water boils around it
                var o = O["heater"]; var s = _st["heater"];
                System.Func<float, float> pl = y => o.x - 52 - 8 * (y - o.y) / 150f, pr = y => o.x + 52 + 8 * (y - o.y) / 150f;
                _pot.Rest = o.y + 120; _pot.Left = pl(o.y + 120); _pot.Right = pr(o.y + 120);
                var sample = s.anim > 0 ? s.sample : s.resident;
                if (sample != null) DrawSample(D, sample, o + new Vector2(-22, 10 + SampleSize(sample) / 2), 0.9f);
                if (s.anim > 0)
                {   // bubbles rise, and where they burst the surface jumps
                    for (int i = 0; i < 5; i++) { float t = (Time.time * 0.8f + i * 0.2f) % 1f; D.Circle(o + new Vector2(-40 + i * 20, 40 + t * 70), 4 + t * 3, new Color(1, 1, 1, 1 - t), 2f, false); }
                    if (Random.value < Time.deltaTime * 16f) _pot.Nudge(o.x + Random.Range(-44f, 44f), Random.Range(40f, 90f), 7f);
                }
                _pot.Draw(D, o.y + 8, pl, pr);
                int wisps = 1 + Mathf.RoundToInt(3 * G.Boiler);
                for (int i = 0; i < wisps; i++)
                {   // steam curling off the surface: the hotter the bench, the more of it
                    float t = (Time.time * 0.5f + i * 0.29f) % 1f;
                    var wisp = new List<Vector2>();
                    for (int j = 0; j <= 5; j++) wisp.Add(o + new Vector2(-30 + i * 22 + Mathf.Sin(t * 6 + j * 0.9f + i) * 7, 132 + j * 11 + t * 22));
                    D.Poly(wisp, new Color(1, 1, 1, (0.2f + 0.2f * G.Boiler) * (1 - t)), 2f, false, false);
                }
                // the bench thermometer: how hot the whole steam bench is, and what that is worth
                var tp = o + new Vector2(-128, 0);
                var red = new Color(1f, 0.4f, 0.3f, 1f);
                D.HatchCircle(tp + new Vector2(0, 12), 9, red, 2f, 4f);
                D.Line(tp + new Vector2(0, 20), tp + new Vector2(0, 26 + 176 * G.Boiler), red, 5f);
                _thermoText.gameObject.SetActive(G.Phase == Phase.Lesson);
                _thermoText.rectTransform.anchoredPosition = tp + new Vector2(0, 232);
                _thermoText.text = "×" + G.BoilerMult.ToString("0.0");
            }
            else _thermoText.gameObject.SetActive(false);
            if (StationBuilt("calor"))
            {   // the hot specimen sinks into the calorimeter, gives its heat to the water, the thermometer creeps up
                var o = O["calor"]; var s = _st["calor"];
                var sample = s.anim > 0 ? s.sample : s.resident;
                float k = s.anim <= 0 ? (sample != null ? 1 : 0) : Mathf.Min(1, s.anim / 0.35f);
                _cal.Rest = o.y + 100; _cal.Left = o.x - 46; _cal.Right = o.x + 46;
                if (sample != null && k > 0)
                {
                    float w = SampleSize(sample);
                    var at = o + new Vector2(-4, Mathf.Lerp(200, 14 + w / 2, k * k));
                    DrawSample(D, sample, at);
                    if (s.anim > 0 && s.anim < 0.8f) Glow(D, at, w / 2, 0.8f * (1 - s.anim));
                    if (s.anim > 0 && !s.splashed && at.y - w / 2 <= _cal.Rest) { s.splashed = true; _cal.Splash(at.x, 140f, w * 0.5f, 3); Sfx.Play("splash", 0.2f, 1.2f, 0.1f, 0.1f); }
                }
                if (s.anim <= 0) s.splashed = false;
                _cal.Draw(D, o.y + 14, y => o.x - 46, y => o.x + 46);
                float read = s.anim > 0 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((s.anim - 0.25f) / 0.5f)) : sample != null ? 1 : 0;
                float heavy = sample != null ? Mathf.Clamp((float)sample.massR / 3f, 0.25f, 1f) : 0;
                D.Line(o + new Vector2(30, 40), o + new Vector2(30, 70 + 130 * read * heavy), ChalkTex.Red, 3.5f);
                if (s.anim > 0) { float sx = Mathf.Sin(Time.time * 14) * 5; D.Line(o + new Vector2(-30 + sx, 44), o + new Vector2(-30 + sx, 60), W, 3f); }   // the stirrer works
            }
            if (StationBuilt("engine"))
            {   // the valve lets the steam in; the rod turns the wheel — the harder, the hotter the bench
                var o = O["engine"]; var s = _st["engine"];
                D.Circle(ValvePos, 14, ChalkTex.Cyan, 3f);
                float va = s.anim > 0 ? Time.time * 6 : 0;
                D.Line(ValvePos + new Vector2(Mathf.Cos(va), Mathf.Sin(va)) * 14, ValvePos - new Vector2(Mathf.Cos(va), Mathf.Sin(va)) * 14, ChalkTex.Cyan, 2.5f);
                if (s.anim > 0 && s.anim < 0.5f)
                {   // steam escaping the valve
                    float q = s.anim / 0.5f;
                    for (int i = -1; i <= 1; i++) D.Circle(ValvePos + new Vector2(-18 - q * 26, 10 + i * 12 + q * 8), (5 + q * 7) * (0.6f + 0.6f * G.Boiler), new Color(1, 1, 1, 0.55f * (1 - q)), 2f, false);
                }
                float px = Mathf.Sin(_engineAngle) * 18;
                D.Line(o + new Vector2(120, 40), o + new Vector2(140 + px, 40), W, 3f);
                D.Circle(o + new Vector2(190, 40), 30, W, 3f);
                D.Line(o + new Vector2(190, 40), o + new Vector2(190, 40) + new Vector2(Mathf.Cos(_engineAngle), Mathf.Sin(_engineAngle)) * 26, W, 2.5f);
                if (s.anim > 0) for (int k = -1; k <= 1; k++) { var pts = new List<Vector2>(); for (int j = 0; j <= 6; j++) pts.Add(o + new Vector2(k * 16 + Mathf.Sin(Time.time * 5 + j + k) * 4, 130 + j * 9)); D.Poly(pts, new Color(1, 1, 1, 0.5f), 2f, false, false); }
            }
            if (StationBuilt("ice"))
            {   // the specimen melts its way into the ice; the meltwater gathers in the dish
                var o = O["ice"]; var s = _st["ice"];
                var sample = s.anim > 0 ? s.sample : s.resident;
                float w = sample != null ? SampleSize(sample) : 40;
                float dent = Mathf.Min(24f, _iceDent + (s.anim > 0 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(s.anim / 0.7f)) * 6f : 0));
                _melt.Rest = o.y + 8 + _meltLevel; _melt.Left = o.x - 84; _melt.Right = o.x + 84;
                if (_meltLevel > 0.5f) _melt.Draw(D, o.y + 5, y => o.x - 84, y => o.x + 84, 0.9f);
                StationArt.Ice(D, o, dent, _iceMelt, 0, w);
                if (sample != null)
                {
                    var at = o + new Vector2(0, 76 - _iceMelt - dent + w / 2);
                    DrawSample(D, sample, at);
                    if (s.anim > 0)
                    {   // it hisses: a glow, a little steam
                        Glow(D, at, w / 2, 0.8f * Mathf.Sin(Mathf.Clamp01(s.anim) * Mathf.PI));
                        for (int i = -1; i <= 1; i++) { float t = (Time.time * 0.9f + i * 0.3f) % 1f; D.Line(at + new Vector2(i * 16 + Mathf.Sin(t * 7) * 4, w / 2 + 4 + t * 26), at + new Vector2(i * 16 + Mathf.Sin(t * 7 + 1) * 4, w / 2 + 12 + t * 26), new Color(1, 1, 1, 0.5f * (1 - t)), 2f); }
                        if (Random.value < Time.deltaTime * 5f) _melt.Nudge(o.x + (Random.value < 0.5f ? -66 : 66), -60f, 6f);   // drips off the block
                    }
                }
            }
            if (StationBuilt("piston"))
            {   // the heated gas pushes the piston up — a cold bench barely lifts it
                var o = O["piston"]; var s = _st["piston"];
                float up = s.anim <= 0 ? 0 : Mathf.Sin(s.anim * Mathf.PI) * (0.35f + 0.65f * G.Boiler);
                float py = o.y + 60 + up * 90;
                D.Line(o + new Vector2(-28, py - o.y), o + new Vector2(28, py - o.y), ChalkTex.Cyan, 5f);
                D.Line(new Vector2(o.x, py), new Vector2(o.x, py + 60), W, 3f);
                D.HatchRect(new Vector2(o.x, (o.y + 4 + py) / 2), new Vector2(50, py - o.y - 8), new Color(1f, 0.5f, 0.35f, 0.25f + 0.25f * G.Boiler), 2f, 10f);
                if (up > 0.15f) for (int i = -1; i <= 1; i += 2) D.Line(new Vector2(o.x + i * 20, py - 12), new Vector2(o.x + i * 20, py - 30 - up * 12), new Color(1, 1, 1, 0.35f * up), 2f);
            }
            // the automatons: a gear on each automated instrument, spinning while it works; "слаженная работа" adds gears
            int gears = G.AutoMult >= 3 ? 2 : G.AutoMult >= 2 ? 1 : 0;
            foreach (var kv in _st)
            {
                if (kv.Key == "fall" || !StationBuilt(kv.Key) || !G.Automated(kv.Key)) continue;
                var gp = GearAt(kv.Key);
                Gear(D, gp, 13, kv.Value.gear, Dim);
                for (int i = 0; i < gears; i++) Gear(D, gp + new Vector2(22 + i * 16, -14 + i * 10), 8, -kv.Value.gear * 1.6f + i, Dim);
            }
            // an automaton with nothing to work on: a pulsing empty slot where its sample should go
            var idleCol = new Color(0.45f, 0.86f, 1f, 0.3f + 0.25f * Mathf.Sin(Time.time * 4));
            foreach (var kv in _st)
            {
                var st = kv.Key; var s = kv.Value;
                if (!StationBuilt(st) || !G.Automated(st) || !NeedsSample(st) || s.resident != null || s.incoming > 0 || s.sample != null || s.flights.Count > 0) continue;
                DashedBox(D, LandingPos(st, Probe), new Vector2(42, 42), idleCol);
            }
            if (_dragging && _dragSample >= 0 && _dragSample < G.Samples.Count)
            {   // where this specimen may go: the slot itself lights up, green when the experiment counts in full
                var smp = G.Samples[_dragSample];
                foreach (var kv in O)
                {
                    var st = kv.Key;
                    if (!StationBuilt(st) || !NeedsSample(st)) continue;
                    var f = MainFormula(st);
                    var col = !Ready(st) ? new Color(1, 1, 1, 0.18f) : GameState.Scientific(smp, f) ? new Color(0.55f, 1f, 0.6f, 0.6f) : new Color(1f, 0.7f, 0.35f, 0.6f);
                    float w = SampleSize(smp) + 12;
                    DashedBox(D, LandingPos(st, smp), new Vector2(w, w), col);
                }
            }
            // an instrument built since the last run is new: a few chalk sparkles over it, no frame
            foreach (var kv in O)
            {
                var f = MainFormula(kv.Key);
                if (f == null || !G.BuiltAt.TryGetValue(f.id, out var bl) || bl != G.Lessons) continue;
                var r = StationRect(kv.Key);
                for (int i = 0; i < 3; i++)
                {
                    var p = new Vector2(r.center.x + (i - 1) * 44, r.yMax + 24 + Mathf.Sin(Time.time * 2.5f + i * 1.7f) * 6);
                    var col = new Color(1f, 0.84f, 0.32f, 0.3f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 2.2f + i * 1.1f)));
                    float s = 9 - Mathf.Abs(i - 1) * 3, d = s * 0.38f;
                    D.Line(p + new Vector2(0, -s), p + new Vector2(0, s), col, 2.2f);      // a twinkle: a long axis,
                    D.Line(p + new Vector2(-s * 0.75f, 0), p + new Vector2(s * 0.75f, 0), col, 2f);
                    D.Line(p + new Vector2(-d, -d), p + new Vector2(d, d), col, 1.6f);     // and two short diagonals
                    D.Line(p + new Vector2(-d, d), p + new Vector2(d, -d), col, 1.6f);
                }
            }
            // samples in flight between the box and the instruments: a quick arc
            foreach (var tr in _transits)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(tr.t));
                var p = Vector2.Lerp(tr.from, tr.to, k) + new Vector2(0, Mathf.Sin(k * Mathf.PI) * 40f);
                DrawSample(D, tr.s, p, 1f);
            }
        }

        void DrawHints()
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 4);
            var col = new Color(0.45f, 0.86f, 1f, pulse);
            bool lesson = G.Phase == Phase.Lesson && G.ActionsLeft > 0;
            void Show(string k, bool on, Vector2 at, string text)
            {
                _hints[k].gameObject.SetActive(on);
                if (!on) return;
                _hints[k].rectTransform.anchoredPosition = at;
                _hints[k].text = text;
                _hints[k].color = col;
            }
            // hints teach each instrument once: after the player has used it, they only make noise
            bool Manual(string k) => lesson && G.Lessons < 2 && StationBuilt(k) && !G.Automated(k) && !G.Used.Contains(k);
            // an automaton explains itself once in the whole game (one instrument at a time); after that the pulsing
            // empty slot on the instrument says it without words
            bool everFed = false;
            foreach (var u in G.Used) if (u.StartsWith("auto:")) { everFed = true; break; }
            bool autoHintUsed = false;
            bool Wants(string k)
            {
                if (!lesson || !StationBuilt(k)) return false;
                if (!G.Automated(k)) return G.Lessons < 2 && !G.Used.Contains(k);   // only the first two lessons teach
                if (everFed || autoHintUsed || !NeedsSample(k) || _st[k].resident != null || _st[k].incoming > 0) return false;
                autoHintUsed = true;
                return true;
            }
            string S(string k, string verb) => G.Automated(k) ? "выдай образец — будет работать сам" : "тяни образец: " + verb;
            Show("tray", lesson && G.Used.Count == 0 && !_dragging, new Vector2(TrayX, TrayY + 110), "тяни образец из коробки на прибор");
            Show("fall", Wants("fall"), new Vector2(O["fall"].x, ShelfY + 150), S("fall", "уронить"));
            Show("dyna", Wants("dyna") && _st["dyna"].anim <= 0, O["dyna"] + new Vector2(0, 340), S("dyna", "взвесить"));
            Show("spring", Wants("spring") && _st["spring"].anim <= 0, O["spring"] + new Vector2(0, 200), S("spring", "на пружину"));
            Show("pend", Wants("pend") && _pendAmp <= 0.1f, PendPivot + new Vector2(-10, 52), S("pend", "качнуть"));
            Show("arch", Wants("arch") && _st["arch"].flights.Count <= 2, O["arch"] + new Vector2(60, 250), S("arch", "в воду"));
            Show("density", Wants("density") && _st["density"].anim <= 0, O["density"] + new Vector2(0, 225), S("density", "в мензурку"));
            Show("friction", Wants("friction") && _st["friction"].anim <= 0, O["friction"] + new Vector2(0, 100), S("friction", "потянуть"));
            Show("slide", Wants("slide") && _st["slide"].anim <= 0, O["slide"] + new Vector2(20, 110), S("slide", "толкнуть"));
            Show("fly", Wants("fly") && _st["fly"].anim <= 0, WheelC + new Vector2(30, WheelR + 60), S("fly", "на верёвку"));
            // the circuit explains itself once: fill the sockets, then close the switch
            Show("stand", lesson && StationBuilt("stand") && !G.Used.Contains("stand") && !_dragging, O["stand"] + new Vector2(0, StationArt.WireTop + 150),
                 MissingSockets > 0 ? "вставь образец в гнездо схемы" : "клик по схеме — замкнуть рубильник");
            Show("calor", Wants("calor") && _st["calor"].anim <= 0, O["calor"] + new Vector2(0, 262), S("calor", "в калориметр"));
            Show("ice", Wants("ice") && _st["ice"].anim <= 0, O["ice"] + new Vector2(0, 200), S("ice", "на лёд"));
            Show("heater", Wants("heater") && _st["heater"].anim <= 0, O["heater"] + new Vector2(0, 250), S("heater", "греть"));
            Show("engine", Manual("engine") && _st["engine"].cooldown <= 0, ValvePos + new Vector2(-20, -30), "клик: открыть клапан");
            Show("piston", Manual("piston") && _st["piston"].anim <= 0, PistonPos + new Vector2(0, 130), "клик: толкнуть");
        }

        void DrawHud()
        {
            _hud.Clear();
            bool lesson = G.Phase == Phase.Lesson;
            bool console = lesson && G.LaunchMode && !G.Won;
            _launchBtn.gameObject.SetActive(console);
            if (console)
            {   // the launch console: one press runs the whole bench
                bool can = G.ActionsLeft > 0;
                float lp = can ? 0.5f + 0.5f * Mathf.Sin(Time.time * 4) : 0;
                _launchBox.Clear();
                _launchBox.Rect(Vector2.zero, new Vector2(448 + lp * 8, 74 + lp * 4), can ? ChalkTex.Yellow : new Color(1, 1, 1, 0.3f), can ? 4f : 2.5f);
                _launchText.text = ChalkTex.Sym($"▶ ПУСК   ·   перестановок: {G.Places}");
                _launchText.color = can ? ChalkTex.Yellow : new Color(1, 1, 1, 0.35f);
            }
            if (!lesson) { _hudText.text = ""; _censusText.text = ""; for (int i = 0; i < 4; i++) { _earn[i].gameObject.SetActive(false); _earnStr[i] = ""; } return; }
            int left = G.ActionsLeft;
            _hudText.text = $"эксперимент {G.Lessons + 1}   ·   действий:";
            _hudText.color = left <= 2 ? ChalkTex.Yellow : ChalkTex.White;
            // the marks stand right after the word; a spent one burns out and is gone
            int shown = Mathf.Min(left, 30);
            float x0 = -900f + _hudText.preferredWidth + 24f, y = 490f, sp = 16f;
            float burn = Mathf.Clamp01(1 - (Time.time - _spentT) / 0.5f);
            void Mark(int i, Color c)
            {
                float tilt = ((i * 37) % 7 - 3) * 0.7f;
                if (i % 5 == 4) _hud.Line(new Vector2(x0 + (i - 4) * sp - 4, y - 11), new Vector2(x0 + i * sp + 4, y + 11), c, 3f);
                else _hud.Line(new Vector2(x0 + i * sp + tilt, y + 11), new Vector2(x0 + i * sp - tilt, y - 11), c, 3f);
            }
            for (int i = 0; i < shown; i++) Mark(i, ChalkTex.Yellow);
            if (burn > 0 && shown < 30) Mark(shown, new Color(1f, 0.55f, 0.3f, burn));
            // what the run has brought so far: one counter per currency, laid out from the right edge;
            // a counter gives its little jump only when its own figure changes
            float ex = 780f;                              // clear of the Esc key in the corner
            foreach (int i in new[] { (int)Cur.Obs, (int)Cur.Cal, (int)Cur.C, (int)Cur.J })
            {
                var t = _earn[i];
                string s = G.LessonEarned[i] > 0 ? "+" + GameState.FmtPrice(G.LessonEarned[i], (Cur)i) : "";
                if (s != _earnStr[i])
                {
                    if (_earnStr[i].Length > 0 && s.Length > 0) _earnFl[i] = 1;
                    _earnStr[i] = s;
                    if (i == (int)Cur.Obs) InkText.On(t).Set(s); else t.text = s;
                }
                _earnFl[i] = Mathf.Max(0, _earnFl[i] - Time.deltaTime * 3);
                t.transform.localScale = Vector3.one * (1 + 0.15f * _earnFl[i]);
                t.gameObject.SetActive(s.Length > 0);
                if (s.Length == 0) continue;
                t.rectTransform.anchoredPosition = new Vector2(ex - 160, 490);
                ex -= t.preferredWidth + 36;
            }
            // the census: its multiplier is on the board all run long, not only at the end
            if (G.Census)
            {
                int n = G.StudiedCount;
                _censusText.text = n > 0 ? $"опись: изучено {n}  ·  итог +{G.CensusBonus * 100:0}%" : "опись: изучи образцы — каждый добавит к итогу";
                _censusText.color = n > 0 ? ChalkTex.Green : new Color(0.55f, 1f, 0.6f, 0.55f);
            }
            else _censusText.text = "";
            // the lenses: each one bought is a magnifier under the tally
            int lenses = (G.Has("lens1") ? 1 : 0) + (G.Has("lens2") ? 1 : 0) + (G.Has("lens3") ? 1 : 0) + (G.Has("lens4") ? 1 : 0);
            for (int i = 0; i < lenses; i++) { var lc = new Vector2(-880 + i * 44, 400); _hud.Circle(lc, 11, ChalkTex.Cyan, 2.2f); _hud.Line(lc + new Vector2(8, -8), lc + new Vector2(18, -18), ChalkTex.Cyan, 3f); }
        }

        public static Color CurColor(int i) => i switch { 0 => ChalkTex.Yellow, 1 => ChalkTex.Cyan, 2 => new Color(0.75f, 0.6f, 1f, 1f), _ => new Color(1f, 0.55f, 0.4f, 1f) };

        /// Whether this very specimen's result on this instrument may be shown in advance: either it has already run
        /// here, or everything the experiment needs has been measured. Otherwise the instrument keeps its secret.
        bool KnowsResult(Sample s, string st)
        {
            if (s == null) return true;
            if (s.seenAt.Contains(st)) return true;
            foreach (var f in Defs.Formulas)
                if (f.station == st && G.Built(f.id) && f.sample && !GameState.Scientific(s, f)) return false;
            return true;
        }

        /// One short label per instrument: what its last run gave, or a forecast while a known specimen hovers over it.
        /// An unstudied specimen turns the label into question marks — the only way to learn is to run the experiment.
        void UpdateLabels()
        {
            string over = _dragging ? StationAt(_ghost.anchoredPosition) : null;
            var dragged = _dragging && _dragSample >= 0 && _dragSample < G.Samples.Count ? G.Samples[_dragSample] : null;
            foreach (var kv in _labels)
            {
                string st = kv.Key;
                bool show = G.Phase == Phase.Lesson && StationBuilt(st);
                kv.Value.gameObject.SetActive(show);
                if (!show) continue;
                float dx = st == "arch" ? 60 : st == "fly" ? 20 : st == "engine" ? 60 : st == "pend" ? 40 : 0;
                var pos = O[st] + new Vector2(dx, -30);
                var sums = new Dictionary<Cur, double>();
                var unknown = new HashSet<Cur>();
                bool forecast = dragged != null && over == st;
                bool secret = forecast && !KnowsResult(dragged, st);
                bool half = false;
                foreach (var f in Defs.Formulas)
                {
                    if (f.station != st || !G.Built(f.id) || f.kind == FKind.Final) continue;
                    var c = f.kind == FKind.Energy ? f.yields : Cur.Obs;
                    bool depends = f.sample && f.needs.Length > 0;
                    if (forecast)
                    {
                        if (secret) unknown.Add(c);
                        else { sums[c] = (sums.TryGetValue(c, out var a) ? a : 0) + G.YieldOf(f, c, dragged, true); if (!GameState.Scientific(dragged, f)) half = true; }
                    }
                    else if (!depends) sums[c] = (sums.TryGetValue(c, out var a) ? a : 0) + G.YieldOf(f, c, null, true);
                    else if (_lastYield.TryGetValue(f.id, out var last)) sums[c] = (sums.TryGetValue(c, out var a) ? a : 0) + last;
                    else unknown.Add(c);
                }
                var sb = new System.Text.StringBuilder();
                foreach (Cur c in new[] { Cur.J, Cur.C, Cur.Cal, Cur.Obs })
                {
                    bool has = sums.TryGetValue(c, out var v);
                    if (!has && !unknown.Contains(c)) continue;
                    if (sb.Length > 0) sb.Append("   ");
                    string unit = c == Cur.Obs ? " " + GameState.Bulb : Defs.CurName[(int)c].Length > 0 ? " " + Defs.CurName[(int)c] : "";
                    string txt = has ? (forecast ? "≈ " : "") + GameState.FmtPrice(v, c) : (secret ? "???" : "?") + unit;
                    sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(CurColor((int)c))}>{txt}</color>");
                }
                if (forecast && half && !secret) sb.Append(" <size=17>½ — не изучен</size>");
                kv.Value.rectTransform.anchoredPosition = pos;
                kv.Value.color = new Color(1, 1, 1, 0.85f);
                InkText.On(kv.Value).Set(sb.ToString());
            }
        }
    }
}
