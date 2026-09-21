using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChalkPhysics
{
    /// Dev tool: plays lessons and spends at breaks like an engaged human. Enable on GameRoot ("autoPlayBot").
    public class AutoPlayBot : MonoBehaviour
    {
        public float thinkPerLetter = 2.5f;      // seconds to put one letter into a draft
        public float actionEvery = 1.0f;         // seconds between manual actions in a lesson
        public float breakTime = 6f;             // seconds spent on the map

        public static readonly List<string> Lines = new List<string>();
        GameState G => GameState.I;
        float _think, _act, _break;
        int _rr;                                 // round-robin over the manual instruments
        Apparatus _app;

        void Start()
        {
            _app = FindAnyObjectByType<Apparatus>(FindObjectsInactive.Include);
            G.FormulaProven += id => { Log("formula " + id); if (Defs.F(id).kind == FKind.Energy) LogYields(); };
            G.LetterOpened += id => Log("letter " + id);
            G.GateBought += id => Log("gate " + id);
            G.RevolutionDone += d => Log("REVOLUTION -> " + Defs.DomainNames[d]);
            G.LessonEnded += () => Log("bell");
            Log("start");
        }

        /// Balance check: what every built energy station gives per firing with its best sample, at this moment.
        void LogYields()
        {
            var sb = new System.Text.StringBuilder("yields:");
            foreach (var f in Defs.Formulas)
            {
                if (!G.Built(f.id) || f.kind != FKind.Energy) continue;
                sb.Append(" " + f.id + "=" + GameState.Fmt(G.YieldOf(f, f.yields, G.BestSampleFor(f))));
            }
            Log(sb.ToString());
        }

        void Log(string s)
        {
            string line = $"[BOT] {G.PlayTime / 60f:0.0} min | lesson {G.Lessons} | {s} | J={GameState.Fmt(G.Cur[0])} O={GameState.Fmt(G.Cur[1])} C={GameState.Fmt(G.Cur[2])} K={GameState.Fmt(G.Cur[3])}";
            Lines.Add(line);
            Debug.Log(line);
        }

        void Update()
        {
            if (G == null || _app == null) return;
            if (G.Won) { enabled = false; Log("WON"); return; }
            float dt = Time.deltaTime;

            if (G.Phase == Phase.Lesson)
            {
                _act += dt;
                if (_act >= actionEvery && G.ActionsLeft > 0)
                {
                    _act = 0;
                    // the circuit: fill its sockets (free), then close the switch
                    if (_app.CircuitWants)
                    {
                        var f0 = Defs.F("ohm");
                        var sm = G.BestSampleFor(f0, _app.Free);
                        if (sm != null) { _app.BotAction("stand", sm); _act = actionEvery - 0.25f; return; }
                    }
                    if (G.LaunchMode) { LaunchTurn(); return; }
                    // a thoughtful player: first measure a couple of unknown samples, then exploit the best known one
                    var all = new List<string>();
                    foreach (var f in Defs.Formulas)
                        if (G.Built(f.id) && f.station.Length > 0 && !all.Contains(f.station)) all.Add(f.station);
                    string pick = null;
                    Sample sample = null;
                    // 1. give idle automated instruments a sample: it works for the rest of the lesson —
                    //    but keep one sample for the newest manual instrument when the box runs short
                    int free = G.Samples.Count(x => _app.Free(x));
                    bool manualWants = all.Exists(st => !G.Automated(st) && _app.NeedsSampleAt(st));
                    for (int i = all.Count - 1; i >= 0 && pick == null && !(manualWants && free <= 1); i--)
                    {
                        var st = all[i];
                        if (!G.Automated(st) || !_app.NeedsSampleAt(st) || _app.HasResident(st) || !_app.Ready(st)) continue;
                        var f = Defs.Formulas.Find(x => x.station == st && G.Built(x.id));
                        sample = G.BestSampleFor(f, _app.Free);
                        if (sample != null) pick = st;
                    }
                    // 2. weigh a couple of unknown samples by hand
                    var unknown = G.Samples.FindAll(x => !x.knowM && _app.Free(x));
                    if (pick == null && G.Built("dyna") && !G.Automated("dyna") && unknown.Count > 0 && G.Samples.Count - unknown.Count < 2 && _app.Ready("dyna")) { pick = "dyna"; sample = unknown[Random.Range(0, unknown.Count)]; }
                    // 2b. the steam bench runs on heat: keep it warm before anything else
                    if (pick == null && G.Built("heat") && !G.Automated("heater") && G.Boiler < 0.55f && _app.Ready("heater"))
                    {
                        sample = G.BestSampleFor(Defs.F("heat"), _app.Free);
                        if (sample != null) pick = "heater";
                    }
                    // 3. the manual stations in turn (a player uses them all), then a manual repeat on an automated one
                    if (pick == null)
                    {
                        var order = new List<string>();
                        var manual = new List<string>();
                        for (int i = all.Count - 1; i >= 0; i--) if (!G.Automated(all[i])) manual.Add(all[i]);
                        _rr++;
                        int shift = G.Built("heat") ? _rr : 0;   // the steam bench wants all its instruments; before it, the newest pays best
                        for (int i = 0; i < manual.Count; i++) order.Add(manual[(i + shift) % manual.Count]);
                        for (int i = all.Count - 1; i >= 0; i--) if (G.Automated(all[i])) order.Add(all[i]);
                        foreach (var st in order)
                        {
                            if (!_app.Ready(st)) continue;
                            if (st == "stand") { if (!_app.CircuitArmed) continue; sample = null; pick = st; break; }   // the circuit: close the switch
                            var f = Defs.Formulas.Find(x => x.station == st && G.Built(x.id));
                            if (f.sample && G.Automated(st)) { if (!_app.HasResident(st)) continue; sample = null; }
                            else if (f.sample) { sample = G.BestSampleFor(f, _app.Free); if (sample == null) continue; }
                            else sample = null;
                            pick = st; break;
                        }
                    }
                    if (pick == null) { _act = actionEvery - 0.3f; return; }
                    _app.BotAction(pick, sample);
                }
                return;
            }

            // break: puzzle out any formula whose letters are all open (the alchemy), shop, then start the next lesson
            var next = G.Discoverable().FirstOrDefault();
            if (next != null)
            {   // the puzzle: one question mark per think
                _think += dt;
                if (_think >= thinkPerLetter)
                {
                    _think = 0;
                    var slot = next.Slots().FirstOrDefault(s => !G.IsFilled(next.id, s));
                    if (slot != null) { G.Place(next.id, slot, slot); if (G.Built(next.id)) Log("discovered " + next.id); }
                }
                return;
            }
            _break += dt;
            if (_break >= breakTime)
            {
                _break = 0;
                Spend();
                if (!G.AnyStation) { _break = breakTime - 0.5f; return; }
                GameRoot.I.StartLesson();
            }
        }

        /// With the launch console the bench is set up first and fired afterwards: load the empty instruments
        /// while rearrangements last, then press "Пуск".
        void LaunchTurn()
        {
            if (G.Places > 0)
            {
                var stations = new List<string>();
                foreach (var f in Defs.Formulas)
                    if (G.Built(f.id) && f.station.Length > 0 && !stations.Contains(f.station)) stations.Add(f.station);
                for (int i = stations.Count - 1; i >= 0; i--)
                {
                    var st = stations[i];
                    if (!_app.NeedsSampleAt(st) || _app.HasResident(st) || !_app.Ready(st)) continue;
                    var f = Defs.Formulas.Find(x => x.station == st && G.Built(x.id));
                    var s = G.BestSampleFor(f, _app.Free);
                    if (s != null) { _app.BotAction(st, s); return; }
                }
            }
            if (!_app.Launch()) _act = actionEvery - 0.3f;
        }

        void Spend()
        {
            if (G.CanRevolt) { G.Revolt(); return; }
            // joules: open every letter on offer (they open new formulas), then the gate; keep the rest for levels
            for (int guard = 0; guard < 4; guard++)
            {
                string open = null;
                foreach (var l in Defs.Letters) if (G.CanOpen(l.id)) { open = l.id; break; }
                if (open == null) break;
                G.OpenLetter(open);
            }
            foreach (var g in Defs.Gates) if (G.CanBuyGate(g.id)) G.BuyGate(g.id);
            // observations: the cheapest perks and devices on the map, a few per break
            for (int buys = 0; buys < 3; buys++)
            {
                var perk = Defs.Perks.Where(p => !G.HasPerk(p.id) && p.cost == Cur.Obs && G.ChainOpen(p.requires) && p.domain <= G.Era).OrderBy(p => p.price).FirstOrDefault();
                var dev = G.AvailableDevices().Where(d => G.ChainOpen(d.requires)).OrderBy(d => d.price).FirstOrDefault();
                double obs = G.Cur[(int)Cur.Obs];
                if (perk != null && (dev == null || perk.price <= dev.price) && obs >= perk.price) { G.BuyPerk(perk.id); Log("perk " + perk.id); }
                else if (dev != null && obs >= dev.price) { G.BuyDevice(dev.id); Log("device " + dev.id); }
                else break;
            }
            // a teaser priced in another coin, once that coin exists
            foreach (var p in Defs.Perks)
                if (p.cost != Cur.Obs && !G.HasPerk(p.id) && p.domain <= G.Era && G.ChainOpen(p.requires) && G.Cur[(int)p.cost] >= p.price * 3) { G.BuyPerk(p.id); Log("perk " + p.id); }
            // levels: spend a share of the joules, saving up when something big is on the map
            double reserve = 0;
            foreach (var l in Defs.Letters) if (!G.Known.Contains(l.id) && !l.Derived && l.openCur == Cur.J && G.NodeVisible("L:" + l.id)) reserve = System.Math.Max(reserve, l.openCost);
            if (G.RevolutionOffered) reserve = System.Math.Max(reserve, G.NextRevolution.cost);
            BuyLetters(reserve > 0 ? 0.5 : 0.7, reserve);
        }

        void BuyLetters(double frac, double reserve)
        {
            var start = (double[])G.Cur.Clone();
            for (int guard = 0; guard < 60; guard++)
            {
                string best = null; double bc = double.MaxValue;
                foreach (var id in G.Known)
                {
                    var d = Defs.L(id);
                    if (d.Derived || G.AtLimit(id) || !G.CanUpgrade(id)) continue;
                    double c = G.UpgradeCost(id);
                    double floor = start[(int)d.cost] * (1 - frac);
                    if (d.cost == Cur.J) floor = System.Math.Max(floor, System.Math.Min(reserve, start[0] * 0.8));
                    if (c < bc && G.Cur[(int)d.cost] - c >= floor) { bc = c; best = id; }
                }
                if (best == null) break;
                G.Upgrade(best);
            }
        }
    }
}
