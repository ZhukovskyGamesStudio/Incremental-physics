using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics
{
    /// A question mark on the puzzle sketch: where the letter belongs and how it is pointed at.
    public class SlotArt
    {
        public string letter;
        public Vector2 at;                // where the "?" sits (relative to the station's origin)
        public string kind = "label";     // label: a leader line to `a`; arrow: an arrow a→b; dim: a dimension a–b; spin: an arc around a with radius b.x
        public Vector2 a, b;
        public bool leader;
    }

    /// For every formula: the annotated sketch of its experiment. The player drags letters onto the question marks.
    public static class Puzzles
    {
        static SlotArt L(string l, float x, float y) => new SlotArt { letter = l, at = new Vector2(x, y) };
        static SlotArt L(string l, float x, float y, float ax, float ay) => new SlotArt { letter = l, at = new Vector2(x, y), a = new Vector2(ax, ay), leader = true };
        static SlotArt Arrow(string l, float x, float y, float ax, float ay, float bx, float by) => new SlotArt { letter = l, at = new Vector2(x, y), kind = "arrow", a = new Vector2(ax, ay), b = new Vector2(bx, by) };
        static SlotArt Dim(string l, float x, float y, float ax, float ay, float bx, float by) => new SlotArt { letter = l, at = new Vector2(x, y), kind = "dim", a = new Vector2(ax, ay), b = new Vector2(bx, by) };
        static SlotArt Spin(string l, float x, float y, float cx, float cy, float r) => new SlotArt { letter = l, at = new Vector2(x, y), kind = "spin", a = new Vector2(cx, cy), b = new Vector2(r, 0) };

        public static readonly Dictionary<string, SlotArt[]> Art = new Dictionary<string, SlotArt[]>
        {
            { "fall", new[] { L("m", -60, 262, 0, 228), Arrow("g", -135, 215, -125, 185, -125, 125), Dim("h", 150, 100, 95, 0, 95, 200) } },
            { "dyna", new[] { L("m", -80, 60, -21, 55), Arrow("g", 100, 50, 50, 60, 50, 10) } },
            { "spring", new[] { L("k", -80, 50, -8, 50), Dim("x", 90, 100, 45, 118, 45, 85) } },
            { "pend", new[] { Dim("l", -30, 200, 40, 300, 108, 112), Arrow("g", 150, 60, 120, 90, 120, 40) } },
            { "arch", new[] { L("rho", 140, 60, 110, 75), Arrow("g", -110, 70, -30, 85, -30, 40), L("V", 60, 165, 60, 62) } },
            { "density", new[] { L("m", -100, 55, -21, 55), Dim("V", 100, 110, 52, 98, 52, 122) } },
            { "slide", new[] { L("mu", 60, -55, 15, -2), L("m", -110, 45, -21, 25), Arrow("g", 100, 45, 60, 60, 60, 25), Dim("d", 10, 105, -70, 70, 90, 70) } },
            { "friction", new[] { L("mu", -40, -55, -40, -2), L("m", -120, 60, -61, 30), Arrow("g", 30, 105, 0, 90, 0, 50) } },
            { "fly", new[] { L("I", 60, 200, 60, 157), Spin("w", 165, 130, 60, 100, 78) } },
            { "heater", new[] { L("c", 110, 125, 55, 112), L("m", -115, 40, -43, 31), L("dT", 40, 195, 40, 150) } },
            { "calm", new[] { L("Q", -160, 60, -30, 38), L("c", 150, 75, 42, 80), L("dT", 150, 200, 38, 190) } },
            { "ice", new[] { L("lam", -170, 45, -62, 45), L("m", 150, 120, 24, 100) } },
            { "carnot", new[] { L("Tn", 0, 165, 0, 122), L("Tx", 110, 105, 110, 67) } },
            { "engine", new[] { L("eta", 190, 110, 190, 72), L("Q", -120, 145, -70, 105) } },
            { "gas", new[] { L("p", -90, 50, -32, 50), Dim("dV", 95, 125, 50, 100, 50, 150) } },
            { "psi", new[] { L("F", 0, 175), L("T", 152, 88), L("Fa", 152, -88), L("Ie", 0, -175), L("P", -152, -88), L("eta", -152, 88) } },
        };

        /// The sketch of a formula uses its station's drawing, except the formula of everything.
        public static string Sketch(FormulaDef f) => f.kind == FKind.Final ? "psi" : f.station;

        public static SlotArt[] For(string formulaId)
        {
            if (Defs.F(formulaId).station == "stand") return Electric(formulaId);
            return Art.TryGetValue(formulaId == "heat" ? "heater" : formulaId, out var a) ? a : new SlotArt[0];
        }

        /// The region of the board a sketch takes (sketch units), so it can be fitted and centred.
        public static Rect Bounds(FormulaDef f)
        {
            if (f.kind == FKind.Final) return Rect.MinMaxRect(-60, -60, 60, 60);
            if (f.station != "stand") return Rect.MinMaxRect(-200, -10, 300, 300);
            var parts = StationArt.SketchCircuit(f.id);
            float x0 = StationArt.CircuitLeft(parts) - 30, x1 = StationArt.CircuitRight(parts) + (StationArt.SketchClock(f.id) ? 110 : 30);
            return Rect.MinMaxRect(x0, -10, x1, StationArt.WireTop + 150);
        }

        /// The electric puzzles: a question mark stands straight above the element it names, clear of the drawing
        /// (the elements are 110 apart, so the marks never touch each other either).
        static SlotArt[] Electric(string f)
        {
            var parts = StationArt.SketchCircuit(f);
            float X(string kind) { foreach (var p in parts) if (p.kind == kind) return p.x; return 0; }
            float top = StationArt.WireTop;
            SlotArt Over(string letter, string kind, float lift) { float x = X(kind); return L(letter, x, top + lift + 62, x, top + lift + 4); }
            var clock = StationArt.ClockPos(parts);
            SlotArt T() => L("t", clock.x + 78, clock.y, clock.x + 20, clock.y);
            switch (f)
            {
                case "ohm": return new[] { Over("U", "bat", 20), Over("R", "res", 22) };
                case "charge": return new[] { Over("Ie", "amp", 18), T() };
                case "pow": return new[] { Over("U", "bat", 20), Over("Ie", "amp", 18) };
                case "lamp": return new[] { Over("P", "lamp", 16), T() };
                case "cap": return new[] { Over("U", "bat", 20), Over("Cc", "cap", 44) };
                case "coil": return new[] { Over("Lind", "coil", 42), Over("Ie", "amp", 18) };
            }
            return new SlotArt[0];
        }
    }
}
