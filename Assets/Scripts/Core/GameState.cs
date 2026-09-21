using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ChalkPhysics
{
    public enum Phase { Lesson, Break }
    public enum PlaceResult { Ok, WrongUnit, WrongSameUnit, Occupied, NoResearch }

    [Serializable]
    public class SaveData
    {
        public double[] cur = new double[4];
        public double[] total = new double[4];
        public float time;
        public int revolutions, lessons;
        public bool won, muted;
        public List<string> known = new List<string>(), proven = new List<string>(), devices = new List<string>(), perks = new List<string>(), used = new List<string>(), gates = new List<string>(), filled = new List<string>();
        public List<string> builtIds = new List<string>();
        public List<int> builtVals = new List<int>();
        public List<string> lvlIds = new List<string>();
        public List<int> lvlVals = new List<int>();
    }

    /// A specimen from the box: hidden mass and volume until measured.
    public class Sample
    {
        public int id;
        public double massR, volR, mu;  // multipliers of the letters m and V; roughness 0..1
        public bool knowM, knowV, knowMu, golden, knowGold;
        public int pattern;             // hatch style, so samples are easy to tell apart
        public bool Knows(string prop) => prop == "m" ? knowM : prop == "V" ? knowV : prop == "mu" ? knowMu : true;
        public bool Floats => massR / volR < 1.2;
        public int uses;
        /// Instruments this very specimen has already run on: only there may its result be shown in advance.
        public readonly HashSet<string> seenAt = new HashSet<string>();
    }

    /// All game rules and data. The bench calls Fire() whenever an experiment happens.
    /// A lesson gives the player a limited number of actions; when they run out the bell rings and the map opens.
    /// On the map the player opens letters for joules (the tree grows layer by layer) and buys devices and perks
    /// for observations; formulas are found on the alchemy screen by combining letters.
    public class GameState
    {
        public static GameState I;
        public static float DevSpeed = 1f;
        public const int BaseActions = 6;

        public readonly double[] Cur = new double[4];
        public readonly double[] Total = new double[4];
        public readonly double[] LessonEarned = new double[4];
        public float PlayTime;
        public int Revolutions, Lessons, ActionsLeft;
        public bool Won;
        public Phase Phase = Phase.Break;

        public readonly HashSet<string> Proven = new HashSet<string>();
        public readonly HashSet<string> Devices = new HashSet<string>();
        public readonly HashSet<string> Perks = new HashSet<string>();
        public readonly HashSet<string> Gates = new HashSet<string>();
        public readonly HashSet<string> Used = new HashSet<string>();      // bench hints stop after the first use
        public readonly List<string> Known = new List<string>();           // opened letters
        public readonly Dictionary<string, HashSet<string>> Filled = new Dictionary<string, HashSet<string>>();   // puzzle: formula -> letters placed
        public readonly Dictionary<string, int> Wrong = new Dictionary<string, int>();                            // puzzle: "formula:slot" -> misses
        public readonly Dictionary<string, int> BuiltAt = new Dictionary<string, int>();   // formula -> Lessons when it was found
        readonly Dictionary<string, int> _lvl = new Dictionary<string, int>();
        public readonly Dictionary<string, int> LessonFires = new Dictionary<string, int>();
        public readonly List<Sample> Samples = new List<Sample>();
        public Sample ActiveSample;      // the sample an experiment is being evaluated with
        public int Selected = -1;        // sample picked by the player on the bench

        public event Action Changed, LessonStarted, LessonEnded;
        public event Action<string> FormulaProven, DeviceBought, PerkBought, LetterOpened, GateBought;
        public event Action<Sample> GoldFound;
        bool _preview;                   // while true, a golden sample counts as golden only once the player knows it
        public event Action<int> RevolutionDone;

        static string SavePath => Path.Combine(Application.persistentDataPath, "chalk_save7.json");
        // in a browser the save lives in PlayerPrefs (IndexedDB), which is flushed for sure on Save()
        const string SaveKey = "chalk_save7";
        static bool Web => Application.platform == RuntimePlatform.WebGLPlayer;
        static void WriteSave(string json) { if (Web) { PlayerPrefs.SetString(SaveKey, json); PlayerPrefs.Save(); } else File.WriteAllText(SavePath, json); }
        static string ReadSave() => Web ? PlayerPrefs.GetString(SaveKey, "") : File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;

        public GameState()
        {
            I = this;
            if (!Load()) NewGame();
        }

        public void NewGame()
        {
            for (int i = 0; i < 4; i++) Cur[i] = Total[i] = 0;
            PlayTime = 0; Revolutions = 0; Lessons = 0; Won = false;
            Proven.Clear(); Known.Clear(); Devices.Clear(); Perks.Clear(); Gates.Clear(); Used.Clear(); Filled.Clear(); Wrong.Clear(); BuiltAt.Clear(); _lvl.Clear();
            Phase = Phase.Break;
        }

        // ---------------- values ----------------
        public int Level(string id) => _lvl.TryGetValue(id, out var l) ? l : 1;

        public double V(string id)
        {
            var d = Defs.L(id);
            if (d.Derived) return Defs.F(d.derivedFrom).eval(this);
            if (d.perSample) return ActiveSample != null ? ActiveSample.mu : d.baseV;
            double v = ValueAt(id, Level(id));
            // a specimen changes only what is written in the formula: its own mass and volume
            if (ActiveSample != null)
            {
                if (id == "m") v *= ActiveSample.massR;
                if (id == "V") v *= ActiveSample.volR;
            }
            return v;
        }

        public double ValueAt(string id, int level)
        {
            var d = Defs.L(id);
            double v = d.mul ? d.baseV * Math.Pow(d.step, level - 1) : d.baseV + d.step * (level - 1);
            return Math.Min(d.max, Math.Max(d.min, v));
        }

        public bool AtLimit(string id)
        {
            var d = Defs.L(id);
            if (d.Derived || d.perSample) return true;
            double v = V(id);
            return d.mul && d.step < 1 ? v <= d.min + 1e-9 : v >= d.max - 1e-9;
        }

        public double UpgradeCost(string id)
        {
            var d = Defs.L(id);
            return Math.Round(d.cost0 * Math.Pow(d.costK, Level(id) - 1));
        }

        /// A letter of an epoch the lab has not reached cannot be levelled.
        public bool CanUpgrade(string id) => Known.Contains(id) && !Defs.L(id).Derived && !Defs.L(id).perSample && Defs.L(id).domain <= Era && !AtLimit(id) && Cur[(int)Defs.L(id).cost] >= UpgradeCost(id);

        public bool Upgrade(string id)
        {
            if (!CanUpgrade(id)) return false;
            Cur[(int)Defs.L(id).cost] -= UpgradeCost(id);
            _lvl[id] = Level(id) + 1;
            Changed?.Invoke();
            return true;
        }

        public double WithNextLevel(string id, Func<double> f)
        {
            int old = Level(id);
            _lvl[id] = old + 1;
            double r = f();
            _lvl[id] = old;
            return r;
        }

        public double Mult => Defs.DomainMult[Math.Min(Revolutions, Defs.DomainMult.Length - 1)] * DevSpeed;
        /// The epoch the game is in: only its tree, letters and upgrades are on the board.
        public int Era => Math.Min(Revolutions, 2);
        public bool Built(string formulaId) => Proven.Contains(formulaId);
        public bool Has(string device) => Devices.Contains(device);
        public bool HasPerk(string perk) => Perks.Contains(perk);
        /// Whether the bench has anything to do at all.
        public bool AnyStation { get { foreach (var f in Defs.Formulas) if (Built(f.id) && f.station.Length > 0) return true; return false; } }

        // ---------------- bench parameters ----------------
        public int MaxActions { get { int n = BaseActions; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.Actions) n += (int)d.value; } return n; } }
        public double AutoMult { get { double m = 1; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.AutoMult) m *= d.value; } return m; } }
        public double StationMult(string station) { double m = 1; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.StationMult && d.station == station) m *= d.value; } return m; }
        public float SwingPeriod => Mathf.Max(0.3f, (float)V("T"));
        public int SampleCount { get { int n = 4; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.Samples) n += (int)d.value; } return n; } }
        public double GoldChance => 0.06 + (HasPerk("gold1") ? 0.06 : 0) + (HasPerk("gold2") ? 0.08 : 0) + (HasPerk("gold3") ? 0.1 : 0);
        public double CurMult(Cur c) { double m = 1; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.CurMult && d.cur == c) m *= d.value; } return m; }
        /// What a golden specimen multiplies an energy experiment by.
        public double GoldMult => HasPerk("shine") ? 5 : 3;
        public int ShelfCapacity => 3;
        public int BallsPerDrop => Has("hopper3") ? 4 : Has("hopper2") ? 2 : 1;
        /// The pendulum swings by itself from the day it is built; everything else needs its device.
        public bool Automated(string station) { if (station == "pend") return true; var d = Defs.AutoDevice(station); return d != null && Has(d.id); }
        /// Whether an instrument keeps its specimen after a run. The circuit holds its specimens in their sockets,
        /// yet it never runs by itself: it cannot be automated at all.
        public bool Keeps(string station) => station == "stand" || Automated(station);

        // ---------------- the steam era: the bench has a temperature ----------------
        /// How hot the bench is (0..1). Every heating raises it, it cools down by itself; the engine, the piston
        /// and the ice all work the harder the hotter it is. A run starts cold.
        public float Boiler;
        public float BoilerRise => HasPerk("stoke") ? 0.4f : 0.22f;     // per heating
        public float BoilerCool => Has("lagging") ? 0.05f : 0.1f;       // per action
        public double BoilerMult => 0.4 + 2.6 * Boiler;
        static bool Steam(string station) => station == "engine" || station == "piston" || station == "ice";

        // ---------------- the launch console ("Пульт запуска") ----------------
        /// With the console the instruments no longer fire when a specimen is put on them: the player arranges the
        /// bench, then presses "Пуск" and everything runs at once. Specimens stay where they were put.
        public bool LaunchMode => HasPerk("hands") || HasPerk("hands3");
        public int PlaceBudget => HasPerk("hands2") || HasPerk("hands4") ? 3 : 2;
        public int Places;               // rearrangements left before the next launch
        /// A specimen hanging on the pendulum gets boring: each round on the thread gives fewer ideas.
        public double PendFatigue(int rounds) => Math.Max(Has("clock") ? 0.45 : 0.3, Math.Pow(Has("clock") ? 0.93 : 0.85, rounds));
        public double ObsMult { get { double m = (Has("lens1") ? 1.5 : 1) * (Has("lens2") ? 1.5 : 1) * (Has("lens3") ? 1.5 : 1) * (Has("lens4") ? 1.5 : 1); foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.ObsMult) m *= d.value; } return m; } }

        /// Levels of the letters in a measuring formula sharpen it: joules put into letters pay back in observations.
        public double Precision(FormulaDef f)
        {
            if (f.kind != FKind.Measure) return 1;
            double n = 0;
            foreach (var l in f.Slots()) { var d = Defs.L(l); if (!d.Derived && !d.perSample) n += Level(l) - 1; }
            return 1 + 0.04 * n;
        }

        public static bool IsMeasurer(FormulaDef f) => f.reveals != null;

        /// Everything the bench of this epoch can find out about a specimen is known.
        public bool Measured(Sample s)
        {
            if (s == null) return false;
            bool any = false;
            foreach (var f in Defs.Formulas)
                if (f.reveals != null && Built(f.id)) { any = true; if (!s.Knows(f.reveals)) return false; }
            return any;
        }
        /// "Полная опись": every studied specimen adds a share to the whole run.
        public double CensusRate => HasPerk("census2") ? 0.05 : HasPerk("census") ? 0.04 : 0;
        public bool Census => CensusRate > 0;
        public int StudiedCount { get { int n = 0; foreach (var s in Samples) if (Measured(s)) n++; return n; } }
        public double CensusBonus => CensusRate * StudiedCount;
        /// Whether every property the experiment needs has been measured on this sample.
        public static bool Scientific(Sample s, FormulaDef f)
        {
            if (s == null) return true;
            foreach (var p in f.needs) if (!s.Knows(p)) return false;
            return true;
        }

        /// Yield of one firing of a formula in the given currency, with a given sample (null = generic).
        /// preview = what the player is allowed to know: a secret golden sample counts as ordinary.
        public double YieldOf(FormulaDef f, Cur c, Sample sample = null, bool preview = false)
        {
            var prev = ActiveSample; bool prevP = _preview;
            ActiveSample = sample; _preview = preview;
            double y = 0;
            double sm = StationMult(f.station);
            // an experiment on an unmeasured sample is "unscientific": only half is credited
            double sci = Scientific(sample, f) ? 1 : 0.5;
            // the bath only weighs what the water pushes back: a specimen that sinks gives nothing
            if (f.station == "arch" && sample != null && !sample.Floats) { ActiveSample = prev; _preview = prevP; return 0; }
            if (f.kind == FKind.Energy && f.yields == c) y += f.eval(this) * Mult * sm * sci;
            // ideas come from measuring instruments only: an experiment that just makes energy teaches nothing new
            if (c == ChalkPhysics.Cur.Obs && f.kind == FKind.Measure) y += f.obs * ObsMult * sm * Precision(f) * Mult / DevSpeed * sci;
            ActiveSample = prev; _preview = prevP;
            if (y <= 0) return 0;
            y *= CurMult(c);
            // a golden specimen pays on the result, whatever the experiment
            if (sample != null && sample.golden && (sample.knowGold || !preview)) y *= GoldMult;
            // a specimen studied to the end is worth more with the census
            if (Census && sample != null && Measured(sample)) y *= 1.25;
            if (Steam(f.station)) y *= BoilerMult;
            return y;
        }

        /// Best sample to use on a station: the one with the biggest known yield, or an unmeasured one
        /// for measurers. Only samples passing the filter (e.g. not busy on the bench) are considered; null if none.
        public Sample BestSampleFor(FormulaDef f, Func<Sample, bool> allowed = null)
        {
            if (!f.sample || Samples.Count == 0) return null;
            if (IsMeasurer(f) && f.reveals != null)
            {
                foreach (var s in Samples) if (!s.Knows(f.reveals) && (allowed == null || allowed(s))) return s;
            }
            Sample best = null; double bv = -1;
            var c = f.kind == FKind.Energy ? f.yields : ChalkPhysics.Cur.Obs;
            foreach (var s in Samples)
            {
                if (allowed != null && !allowed(s)) continue;
                double v = YieldOf(f, c, s, true);
                if (v > bv) { bv = v; best = s; }
            }
            return best;
        }

        public void Fire(FormulaDef f, Sample sample = null, double mult = 1)
        {
            if (!Built(f.id)) return;
            if (f.kind == FKind.Energy) Earn(f.yields, YieldOf(f, f.yields, sample) * mult);
            Earn(ChalkPhysics.Cur.Obs, YieldOf(f, ChalkPhysics.Cur.Obs, sample) * mult);
            LessonFires[f.id] = (LessonFires.TryGetValue(f.id, out var n) ? n : 0) + 1;
            if (sample != null)
            {
                sample.uses++;
                sample.seenAt.Add(f.station);   // from now on this instrument can forecast this specimen
                if (f.reveals == "m") sample.knowM = true;
                if (f.reveals == "V") sample.knowV = true;
                if (f.reveals == "mu") sample.knowMu = true;
                // gold shows itself only in a real experiment, never on a measuring instrument
                if (f.kind == FKind.Energy && sample.golden && !sample.knowGold) { sample.knowGold = true; GoldFound?.Invoke(sample); }
            }
        }

        public void Earn(Cur c, double e)
        {
            if (e <= 0 || double.IsNaN(e)) return;
            int i = (int)c;
            Cur[i] += e;
            Total[i] += e;
            LessonEarned[i] += e;
        }

        /// The player has done this on the bench once: its hint is not needed any more.
        public void MarkUsed(string key) { if (Used.Add(key)) Save(); }

        // ---------------- lessons ----------------
        public bool UseAction()
        {
            if (Phase != Phase.Lesson || ActionsLeft <= 0) return false;
            ActionsLeft--;
            Boiler = Mathf.Max(0, Boiler - BoilerCool);      // the bench cools a little with every action
            Changed?.Invoke();
            return true;
        }

        public void StartLesson()
        {
            if (Phase == Phase.Lesson || Won || !AnyStation) return;
            Phase = Phase.Lesson;
            ActionsLeft = MaxActions;
            Places = PlaceBudget;
            Boiler = 0;
            GenerateSamples();
            for (int i = 0; i < 4; i++) LessonEarned[i] = 0;
            LessonFires.Clear();
            LessonStarted?.Invoke();
            Changed?.Invoke();
        }

        static readonly double[] Ratios = { 0.5, 0.7, 1.0, 1.0, 1.4, 2.0, 3.0 };
        void GenerateSamples()
        {
            Samples.Clear();
            Selected = -1;
            var rnd = new System.Random();
            int n = SampleCount, shift = rnd.Next(6);
            for (int i = 0; i < n; i++)
            {
                double heavy = HasPerk("heavy") ? 1.25 : 1;
                var s = new Sample { id = i, massR = Ratios[rnd.Next(Ratios.Length)] * heavy, volR = Ratios[rnd.Next(Ratios.Length)], mu = System.Math.Round(0.1 + rnd.NextDouble() * 0.8, 2), pattern = (i + shift) % 6 };
                s.golden = rnd.NextDouble() < GoldChance;
                Samples.Add(s);
            }
            // there is always something to find: one clearly heavy and one clearly light sample
            Samples[rnd.Next(n)].massR = 3.0 * (HasPerk("heavy") ? 1.25 : 1);
            int light = rnd.Next(n);
            if (Samples[light].massR < 3.0) Samples[light].massR = 0.5;
            if (HasPerk("autoweigh")) foreach (var s in Samples) s.knowM = true;
        }

        public void EndLesson()
        {
            if (Phase != Phase.Lesson) return;
            Phase = Phase.Break;
            Lessons++;
            LessonEnded?.Invoke();
            Changed?.Invoke();
            Save();
            if (Has("lab")) Autobuy();
        }

        void Autobuy()
        {
            if (RevolutionOffered) return;
            var start = (double[])Cur.Clone();
            bool any = false;
            for (int guard = 0; guard < 50; guard++)
            {
                string best = null; double bc = double.MaxValue;
                foreach (var id in Known)
                {
                    var d = Defs.L(id);
                    if (d.Derived || AtLimit(id) || !CanUpgrade(id)) continue;
                    double c = UpgradeCost(id);
                    if (c < bc && Cur[(int)d.cost] - c >= start[(int)d.cost] * 0.75) { bc = c; best = id; }
                }
                if (best == null) break;
                Upgrade(best);
                any = true;
            }
            if (any) Sfx.Play("up", 0.25f);
        }

        // ---------------- the tree: what is on the map ----------------
        public bool NodeDone(string code)
        {
            switch (Defs.Kind(code))
            {
                case 'L': return Known.Contains(Defs.Id(code));
                case 'F': return Built(Defs.Id(code));
                case 'R': return Revolutions >= int.Parse(Defs.Id(code));
                case 'G': return Gates.Contains(Defs.Id(code));
            }
            return false;
        }

        public bool LayerDone(int k) { foreach (var c in Defs.Tree[k]) if (!NodeDone(c)) return false; return true; }
        bool LayerHasFormula(int k) { foreach (var c in Defs.Tree[k]) if (Defs.Kind(c) == 'F') return true; return false; }
        /// The lesson count when the layer's last formula was found.
        int LayerDoneLesson(int k) { int n = 0; foreach (var c in Defs.Tree[k]) if (Defs.Kind(c) == 'F' && BuiltAt.TryGetValue(Defs.Id(c), out var l)) n = Math.Max(n, l); return n; }

        /// The layer a layer grows out of: the previous one, except that trunk layers skip over a side branch.
        public static int PrevLayer(int k)
        {
            if (k <= 0) return -1;
            if (Defs.LayerEra[k - 1] != Defs.LayerEra[k]) return -1;   // the first layer of an epoch grows out of nothing
            if (Defs.IsBranch[k]) return k - 1;                       // a branch continues from its previous layer (or the revolution)
            int j = k - 1;
            while (j > 0 && Defs.IsBranch[j]) j--;
            return j;
        }

        /// A layer grows out of the previous one once that is done; after a formula the player first goes to a lesson with it.
        public bool LayerVisible(int k)
        {
            if (k < 0 || k >= Defs.Tree.Length) return false;
            if (Defs.LayerEra[k] > Era) return false;   // every epoch reached is on the board, each a tree of its own
            int prev = PrevLayer(k);
            if (prev < 0) return true;
            if (!LayerDone(prev)) return false;
            if (LayerHasFormula(prev) && Lessons <= LayerDoneLesson(prev)) return false;
            return true;
        }

        public bool NodeVisible(string code) { int k = Defs.LayerOf(code); return k >= 0 && LayerVisible(k); }
        public int LastVisibleLayer { get { int last = 0; for (int k = 0; k < Defs.Tree.Length; k++) if (LayerVisible(k)) last = k; return last; } }
        /// A formula's branch of upgrades starts growing once the player has been to a lesson with the formula.
        public bool ChainOpen(string key)
        {
            if (key.StartsWith("L:")) return Known.Contains(key.Substring(2)) && Lessons > 0;
            return Built(key) && Lessons > (BuiltAt.TryGetValue(key, out var l) ? l : 0);
        }

        // ---------------- letters: opened for joules ----------------
        public bool CanOpen(string id)
        {
            var d = Defs.L(id);
            return !Known.Contains(id) && !d.Derived && NodeVisible("L:" + id) && Cur[(int)d.openCur] >= d.openCost;
        }

        public bool OpenLetter(string id)
        {
            if (!CanOpen(id)) return false;
            Cur[(int)Defs.L(id).openCur] -= Defs.L(id).openCost;
            AddKnown(id);
            LetterOpened?.Invoke(id);
            Changed?.Invoke();
            Save();
            return true;
        }

        void AddKnown(string id)
        {
            if (Known.Contains(id)) return;
            Known.Add(id);
            Known.Sort((a, b) => Defs.Letters.FindIndex(x => x.id == a).CompareTo(Defs.Letters.FindIndex(x => x.id == b)));
        }

        public bool CanBuyGate(string id) => !Gates.Contains(id) && !Won && NodeVisible("G:" + id) && Cur[(int)ChalkPhysics.Cur.J] >= Defs.Gt(id).cost;

        public bool BuyGate(string id)
        {
            if (!CanBuyGate(id)) return false;
            Cur[(int)ChalkPhysics.Cur.J] -= Defs.Gt(id).cost;
            Gates.Add(id);
            GateBought?.Invoke(id);
            Changed?.Invoke();
            Save();
            return true;
        }

        // ---------------- alchemy: finding formulas ----------------
        /// A formula can be found once its slot on the map is there, all its letters are open and its epoch has come.
        public bool Available(FormulaDef f)
        {
            if (f.domain > Revolutions || !NodeVisible("F:" + f.id)) return false;
            if (f.kind == FKind.Final && !Gates.Contains("final")) return false;
            foreach (var l in f.Slots()) if (!Known.Contains(l)) return false;
            return true;
        }

        public IEnumerable<FormulaDef> Discoverable()
        {
            foreach (var f in Defs.Formulas) if (!Built(f.id) && Available(f)) yield return f;
        }

        public int DiscoverableCount { get { int n = 0; foreach (var f in Discoverable()) n++; return n; } }
        /// The alchemy screen opens as soon as there is a formula to find.
        public bool AlchemyUnlocked => Proven.Count > 0 || DiscoverableCount > 0;

        public bool IsFilled(string formula, string slot) => Filled.TryGetValue(formula, out var set) && set.Contains(slot);
        public int WrongCount(string formula, string slot) => Wrong.TryGetValue(formula + ":" + slot, out var n) ? n : 0;

        /// The puzzle: a letter is dropped on a question mark of a formula's sketch. The right one sticks; when every
        /// question mark is answered the formula is found and its instrument appears on the bench.
        public PlaceResult Place(string formulaId, string letter, string slot)
        {
            var f = Defs.F(formulaId);
            if (Built(f.id) || !Available(f) || Won) return PlaceResult.NoResearch;
            if (IsFilled(f.id, slot)) return PlaceResult.Occupied;
            if (letter != slot)
            {
                Wrong[f.id + ":" + slot] = WrongCount(f.id, slot) + 1;
                return Defs.L(letter).unit == Defs.L(slot).unit ? PlaceResult.WrongSameUnit : PlaceResult.WrongUnit;
            }
            if (!Filled.TryGetValue(f.id, out var set)) Filled[f.id] = set = new HashSet<string>();
            set.Add(slot);
            bool all = true;
            foreach (var l in f.Slots()) if (!set.Contains(l)) { all = false; break; }
            if (all) Discover(f);
            else { Changed?.Invoke(); Save(); }
            return PlaceResult.Ok;
        }

        public void Discover(FormulaDef f)
        {
            if (Built(f.id)) return;
            Proven.Add(f.id);
            BuiltAt[f.id] = Lessons;
            foreach (var o in f.outputs) AddKnown(o);
            if (f.kind == FKind.Final) Won = true;
            FormulaProven?.Invoke(f.id);
            Changed?.Invoke();
            Save();
        }

        public IEnumerable<DeviceDef> AvailableDevices()
        {
            foreach (var d in Defs.Devices)
                if (!Devices.Contains(d.id) && Built(d.requires) && d.domain <= Era) yield return d;
        }

        public bool BuyDevice(string id)
        {
            var d = Defs.D(id);
            if (d == null || Devices.Contains(id) || !Built(d.requires) || Cur[(int)d.cost] < d.price) return false;
            Cur[(int)d.cost] -= d.price;
            Devices.Add(id);
            DeviceBought?.Invoke(id);
            Changed?.Invoke();
            Save();
            return true;
        }

        public bool BuyPerk(string id)
        {
            var p = Defs.Pk(id);
            if (p == null || Perks.Contains(id) || p.domain > Era || !ChainOpen(p.requires) || Cur[(int)p.cost] < p.price) return false;
            Cur[(int)p.cost] -= p.price;
            Perks.Add(id);
            PerkBought?.Invoke(id);
            Changed?.Invoke();
            Save();
            return true;
        }

        // ---------------- revolutions ----------------
        public RevolutionDef NextRevolution { get { foreach (var r in Defs.Revolutions) if (r.to > Revolutions) return r; return null; } }
        /// The revolution node is on the map once the tree has grown up to it.
        public bool RevolutionOffered { get { var r = NextRevolution; return r != null && !Won && NodeVisible("R:" + r.to); } }
        public bool CanRevolt => RevolutionOffered && Cur[(int)ChalkPhysics.Cur.J] >= NextRevolution.cost;

        public void Revolt()
        {
            if (!CanRevolt) return;
            var r = NextRevolution;
            for (int i = 0; i < 4; i++) Cur[i] = 0;
            _lvl.Clear();
            Devices.Clear();
            Perks.Clear();
            // everything goes: theories, instruments, letters. Only the quantities the instruments measured stay,
            // written down in the notebook — the formula of everything will need them all at the end
            var keep = Known.Where(x => Defs.L(x).Derived).ToList();
            Known.Clear();
            foreach (var x in keep) Known.Add(x);
            Proven.Clear(); BuiltAt.Clear(); Filled.Clear(); Wrong.Clear();
            Revolutions = r.to;
            foreach (var l in r.freeLetters) AddKnown(l);
            RevolutionDone?.Invoke(Revolutions);
            Changed?.Invoke();
            Save();
        }

        float _saveT;
        public void Tick(float dt)
        {
            if (!Won) PlayTime += dt;
            _saveT += dt;
            if (_saveT > 5f) { _saveT = 0; Save(); }
        }

        public void NotifyChanged() => Changed?.Invoke();

        // ---------------- save / load ----------------
        public void Save()
        {
            try
            {
                var d = new SaveData
                {
                    cur = (double[])Cur.Clone(), total = (double[])Total.Clone(), time = PlayTime, revolutions = Revolutions, lessons = Lessons,
                    won = Won, muted = Sfx.Muted,
                    known = Known.ToList(), proven = Proven.ToList(), devices = Devices.ToList(), perks = Perks.ToList(), used = Used.ToList(), gates = Gates.ToList(),
                    builtIds = BuiltAt.Keys.ToList(), builtVals = BuiltAt.Values.ToList(),
                    lvlIds = _lvl.Keys.ToList(), lvlVals = _lvl.Values.ToList(),
                };
                foreach (var kv in Filled) foreach (var l in kv.Value) d.filled.Add(kv.Key + ":" + l);
                WriteSave(JsonUtility.ToJson(d));
            }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
        }

        bool Load()
        {
            try
            {
                string json = ReadSave();
                if (string.IsNullOrEmpty(json)) return false;
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null || d.cur == null || d.cur.Length != 4) return false;
                for (int i = 0; i < 4; i++) { Cur[i] = d.cur[i]; Total[i] = d.total[i]; }
                PlayTime = d.time; Revolutions = d.revolutions; Lessons = d.lessons;
                Won = d.won; Sfx.Muted = d.muted;
                foreach (var x in d.known) if (Defs.HasLetter(x)) Known.Add(x);
                foreach (var x in d.proven) Proven.Add(x);
                // ids that no longer exist (upgrades dropped since the save was written) are quietly forgotten
                foreach (var x in d.devices) if (Defs.D(x) != null) Devices.Add(x);
                foreach (var x in d.perks) if (Defs.Pk(x) != null) Perks.Add(x);
                foreach (var x in d.used) Used.Add(x);
                foreach (var x in d.gates) Gates.Add(x);
                for (int i = 0; i < d.builtIds.Count; i++) BuiltAt[d.builtIds[i]] = d.builtVals[i];
                for (int i = 0; i < d.lvlIds.Count; i++) _lvl[d.lvlIds[i]] = d.lvlVals[i];
                foreach (var x in d.filled)
                {
                    int i = x.IndexOf(':');
                    if (i <= 0) continue;
                    string f = x.Substring(0, i), l = x.Substring(i + 1);
                    if (!Filled.TryGetValue(f, out var set)) Filled[f] = set = new HashSet<string>();
                    set.Add(l);
                }
                Phase = Phase.Break;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("Load failed: " + e.Message); return false; }
        }

        public static void DeleteSave()
        {
            try { if (Web) { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); } else if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
        }

        // ---------------- text helpers ----------------
        public static string Fmt(double v)
        {
            double a = Math.Abs(v);
            string[] suf = { "", "k", "M", "G", "T", "P", "E" };
            if (a < 1e4)
            {
                if (a >= 100) return v.ToString("0");
                if (a >= 10) return v.ToString("0.#");
                return v.ToString("0.##");
            }
            int i = 0;
            while (a >= 1000 && i < suf.Length - 1) { a /= 1000; v /= 1000; i++; }
            return v.ToString(a >= 100 ? "0" : a >= 10 ? "0.#" : "0.##") + suf[i];
        }

        /// Ideas have no name: their number stands alone, with a chalk lightbulb drawn beside it.
        public static string FmtCur(double v, Cur c) { var n = Defs.CurName[(int)c]; return n.Length > 0 ? Fmt(v) + " " + n : Fmt(v); }
        /// The mark a chalk lightbulb is drawn over (see InkText): ideas are written the same way everywhere.
        public const string Bulb = "¤";
        /// A price or a yield in any currency; ideas get their lightbulb after the number.
        public static string FmtPrice(double v, Cur c) => c == ChalkPhysics.Cur.Obs ? Fmt(v) + " " + Bulb : FmtCur(v, c);
        public string LetterValue(string id) => FmtValue(id, V(id));

        public static string FmtValue(string id, double v)
        {
            var d = Defs.L(id);
            if (d.fmt == "0%") return (v * 100).ToString("0") + "%";
            return (Math.Abs(v) >= 1e4 ? Fmt(v) : v.ToString(d.fmt)) + (d.unit.Length > 0 ? " " + d.unit : "");
        }

        public static string Symbolic(FormulaDef f)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in f.parts) sb.Append(p.StartsWith("{") ? Defs.L(p.Substring(1, p.Length - 2)).sym : p);
            return sb.ToString();
        }
    }
}
