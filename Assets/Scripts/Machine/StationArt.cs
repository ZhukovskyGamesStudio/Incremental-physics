using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics
{
    /// Chalk drawings of the instruments, shared by the bench and the alchemy's puzzle sketch.
    /// Every perk and device the player buys changes the drawing, so an upgrade is always something to look at.
    public static class StationArt
    {
        public struct Look
        {
            public float shelfY, pendLen;     // absolute shelf height; thread length
            public GameState G;               // null = a sketch with no upgrades
            public bool sketch;               // draw the sample and the moving parts at rest
            public string upTo;               // a sketch of the circuit: the formula it is drawn for
            public bool Perk(string id) => G != null && G.HasPerk(id);
            public bool Dev(string id) => G != null && G.Has(id);
        }

        public static readonly Color W = ChalkTex.White;
        public static readonly Color Dim = new Color(1, 1, 1, 0.45f);
        static readonly Color Copper = new Color(1f, 0.6f, 0.3f, 0.95f);

        /// A generic sample square (for sketches).
        public static void Sample(ChalkShape S, Vector2 c, float w = 42)
        {
            S.Rect(c, new Vector2(w, w), W, 2.8f);
            S.HatchRect(c, new Vector2(w - 8, w - 8), new Color(1, 1, 1, 0.4f), 1.8f, 9f);
        }

        public static void Gear(ChalkShape S, Vector2 c, float r, float angle, Color col)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 24; i++) { float a = angle + i / 24f * Mathf.PI * 2; float rr = (i % 4 < 2) ? r : r * 0.72f; pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr); }
            S.Poly(pts, col, 2.2f, false, false);
            S.Circle(c, r * 0.3f, col, 2f);
        }

        public static void Clock(ChalkShape S, Vector2 c, float r, Color col)
        {
            S.Circle(c, r, col, 2.5f);
            S.Line(c + new Vector2(0, r), c + new Vector2(0, r + 6), col, 2.5f);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2; S.Line(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - 4), c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col, 2f); }
            S.Line(c, c + new Vector2(0, r * 0.6f), col, 2f);
            S.Line(c, c + new Vector2(r * 0.45f, -r * 0.2f), col, 2f);
        }

        static void Battery(ChalkShape S, Vector2 c, Color col)
        {
            S.Line(c + new Vector2(-6, -18), c + new Vector2(-6, 18), col, 4f);
            S.Line(c + new Vector2(6, -10), c + new Vector2(6, 10), col, 2.5f);
        }

        // ---------------- the electric bench: one circuit that grows ----------------
        /// An element of the circuit: where it sits along the top wire, and which socket (if any) holds a specimen.
        public struct Part { public string kind; public float x; public int socket; }
        public const float WireTop = 120, WireBot = 26;

        static List<Part> Circuit(IList<string> kinds)
        {
            var list = new List<Part>();
            const float step = 110;
            int n = kinds.Count;
            for (int i = 0; i < n; i++)
            {
                string k = kinds[i];
                list.Add(new Part { kind = k, x = (i - (n - 1) / 2f) * step, socket = k == "res" ? 0 : k == "cap" ? 1 : k == "coil" ? 2 : -1 });
            }
            return list;
        }

        /// The circuit on the bench: the battery, the resistor socket and the ammeter first, then every element found
        /// since, in the order it was found, and the switch that closes it all. It starts small and grows.
        public static List<Part> BenchCircuit(GameState g)
        {
            var k = new List<string> { "bat", "res", "amp" };
            if (g.Built("pow")) k.Add("lamp");
            if (g.Built("cap")) k.Add("cap");
            if (g.Built("coil")) k.Add("coil");
            k.Add("sw");
            return Circuit(k);
        }

        /// The puzzle sketch of an electric formula shows only the elements that formula is about.
        public static List<Part> SketchCircuit(string formula)
        {
            switch (formula)
            {
                case "pow": return Circuit(new[] { "bat", "amp", "lamp", "sw" });
                case "lamp": return Circuit(new[] { "bat", "lamp", "sw" });
                case "cap": return Circuit(new[] { "bat", "cap", "sw" });
                case "coil": return Circuit(new[] { "bat", "amp", "coil", "sw" });
                default: return Circuit(new[] { "bat", "res", "amp", "sw" });
            }
        }
        public static bool SketchClock(string formula) => formula == "charge" || formula == "lamp";

        public static float HalfWidth(string kind) => kind switch { "bat" => 6, "res" => 38, "amp" => 18, "lamp" => 16, "cap" => 40, "coil" => 50, _ => 24 };
        public static Vector2 ClockPos(List<Part> parts) => new Vector2(parts[parts.Count - 1].x, WireTop + 88);
        public static float CircuitLeft(List<Part> parts) => parts[0].x - 45;
        public static float CircuitRight(List<Part> parts) => parts[parts.Count - 1].x + 45;

        static void DrawCircuit(ChalkShape S, Vector2 o, Look k)
        {
            bool sketch = k.G == null;
            var parts = sketch ? SketchCircuit(k.upTo) : BenchCircuit(k.G);
            bool clock = sketch ? SketchClock(k.upTo) : k.G.Built("charge");
            float yT = o.y + WireTop, yB = o.y + WireBot;
            float xL = o.x + CircuitLeft(parts), xR = o.x + CircuitRight(parts);
            float wire = k.Perk("standx2") ? 4.5f : 3f;
            var bus = k.Perk("standx3") ? Copper : W;
            S.Line(new Vector2(xL + 14, o.y), new Vector2(xL + 14, yB), W, 3f);          // two legs on the floor
            S.Line(new Vector2(xR - 14, o.y), new Vector2(xR - 14, yB), W, 3f);
            S.Line(new Vector2(xL, yT), new Vector2(xL, yB), W, wire);
            S.Line(new Vector2(xR, yT), new Vector2(xR, yB), W, wire);
            S.Line(new Vector2(xL, yB), new Vector2(xR, yB), bus, wire);
            float cur = xL;
            foreach (var p in parts)
            {
                float x = o.x + p.x, hw = HalfWidth(p.kind);
                S.Line(new Vector2(cur, yT), new Vector2(x - hw, yT), W, wire);
                cur = x + hw;
                var c = new Vector2(x, yT);
                switch (p.kind)
                {
                    case "bat": Battery(S, c, W); break;
                    case "res":   // the socket of the resistor: two brackets, the specimen goes between them
                        S.Line(c + new Vector2(-38, -12), c + new Vector2(-38, 12), W, 2.5f); S.Line(c + new Vector2(-38, 12), c + new Vector2(-32, 12), W, 2.5f); S.Line(c + new Vector2(-38, -12), c + new Vector2(-32, -12), W, 2.5f);
                        S.Line(c + new Vector2(38, -12), c + new Vector2(38, 12), W, 2.5f); S.Line(c + new Vector2(38, 12), c + new Vector2(32, 12), W, 2.5f); S.Line(c + new Vector2(38, -12), c + new Vector2(32, -12), W, 2.5f);
                        break;
                    case "amp":
                        S.Circle(c, 18, W, 3f);
                        for (int i = 0; i < 5; i++) { float a = Mathf.PI * (0.2f + 0.15f * i); S.Line(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 16, Dim, 1.6f); }
                        break;
                    case "lamp":
                        S.Circle(c, 16, W, 3f);
                        S.Line(c + new Vector2(-11, -11), c + new Vector2(11, 11), Dim, 2f); S.Line(c + new Vector2(-11, 11), c + new Vector2(11, -11), Dim, 2f);
                        break;
                    case "cap":   // two tall plates; the second specimen stands between them
                        S.Line(c + new Vector2(-40, -44), c + new Vector2(-40, 44), W, k.Perk("capx2") ? 5.5f : 4f);
                        S.Line(c + new Vector2(40, -44), c + new Vector2(40, 44), W, k.Perk("capx2") ? 5.5f : 4f);
                        break;
                    case "coil":  // turns wound around the core: the third specimen sits inside
                        for (int i = 0; i < 6; i++)
                        {
                            float tx = -42 + i * 16.8f;
                            var turn = new List<Vector2>();
                            for (int j = 0; j <= 14; j++) { float a = -Mathf.PI / 2 + j / 14f * Mathf.PI; turn.Add(c + new Vector2(tx + Mathf.Cos(a) * 7, Mathf.Sin(a) * 40)); }
                            S.Poly(turn, W, 2.5f, false, false);
                        }
                        break;
                    case "sw":
                        S.Circle(c + new Vector2(-24, 0), 4, ChalkTex.Cyan, 2.5f);
                        S.Circle(c + new Vector2(24, 0), k.Perk("standx4") ? 6 : 4, ChalkTex.Cyan, 2.5f);
                        if (sketch) S.Line(c + new Vector2(-24, 0), c + new Vector2(24, 0), ChalkTex.Cyan, 4f);
                        break;
                }
                if (sketch && p.socket >= 0) Sample(S, c);
            }
            S.Line(new Vector2(cur, yT), new Vector2(xR, yT), W, wire);
            if (clock) Clock(S, o + ClockPos(parts), 16, W);                                    // the stopwatch that times the charge
        }

        /// The block of ice; dent = how deep the specimen has melted into its top, melt = how much it has shrunk.
        public static void Ice(ChalkShape S, Vector2 o, float dent, float melt, float at = 0f, float width = 40f)
        {
            float top = 76 - melt;
            var pts = new List<Vector2> { o + new Vector2(-58 + melt * 0.3f, 10), o + new Vector2(-61 + melt * 0.3f, top - 6) };
            for (int i = 0; i <= 16; i++)
            {
                float x = Mathf.Lerp(-50, 50, i / 16f);
                float d = (x - at) / Mathf.Max(8, width * 0.6f);
                pts.Add(o + new Vector2(x, top - dent * Mathf.Exp(-d * d) + Mathf.Sin(i * 1.7f) * 1.5f));
            }
            pts.Add(o + new Vector2(61 - melt * 0.3f, top - 6)); pts.Add(o + new Vector2(58 - melt * 0.3f, 10));
            var ice = new Color(0.75f, 0.93f, 1f, 0.9f);
            S.Poly(pts, ice, 3f, true);
            var cr = new Color(0.75f, 0.93f, 1f, 0.35f);                 // crystal lines inside
            S.Line(o + new Vector2(-40, 20), o + new Vector2(-18, 46), cr, 1.8f); S.Line(o + new Vector2(-18, 46), o + new Vector2(-30, top - 16), cr, 1.8f);
            S.Line(o + new Vector2(22, 18), o + new Vector2(38, 40), cr, 1.8f); S.Line(o + new Vector2(38, 40), o + new Vector2(28, top - 18), cr, 1.8f);
            S.Line(o + new Vector2(-4, 16), o + new Vector2(6, 32), cr, 1.6f);
        }

        public static void Draw(ChalkShape S, string st, Vector2 o, Look k)
        {
            switch (st)
            {
                case "fall":
                    {   // a small table with a ruler; the sample is pushed off its right edge
                        float sy = k.shelfY;
                        bool x2 = k.Perk("fallx2"), x3 = k.Perk("fallx3");
                        S.Line(new Vector2(o.x - 60, sy), new Vector2(o.x + 40, sy), ChalkTex.Cyan, x3 ? 6.5f : 4.5f);
                        S.Line(new Vector2(o.x - 55, sy), new Vector2(o.x - 55, o.y), W, 3f);
                        S.Line(new Vector2(o.x + 35, sy), new Vector2(o.x + 35, o.y), W, 3f);
                        S.Line(new Vector2(o.x - 55, sy - 30), new Vector2(o.x - 30, sy - 5), Dim, 2f);
                        if (x2) S.Line(new Vector2(o.x + 35, sy - 40), new Vector2(o.x + 10, sy - 5), W, 2.5f);        // a brace for heavy loads
                        if (x3) { S.Line(new Vector2(o.x - 55, sy - 70), new Vector2(o.x - 15, sy - 8), W, 2.5f); S.Line(new Vector2(o.x + 35, sy - 70), new Vector2(o.x - 5, sy - 8), W, 2.5f); }
                        if (k.Dev("hopper"))
                        {   // the pusher: a little motor box behind the shelf with a rod that shoves the sample off
                            S.Rect(new Vector2(o.x - 106, sy + 24), new Vector2(30, 40), W, 3f);
                            S.Line(new Vector2(o.x - 106, sy + 4), new Vector2(o.x - 106, sy), W, 3f);
                            S.Line(new Vector2(o.x - 120, sy), new Vector2(o.x - 92, sy), W, 3f);
                        }
                        if (k.sketch)
                        {
                            Sample(S, new Vector2(o.x + 12, sy + 21));
                            for (int i = 0; i <= 6; i++) { float q = i / 6f; S.Dashed(new Vector2(o.x + 60 + 14 * q, Mathf.Lerp(sy + 21, o.y + 21, q * q)), new Vector2(o.x + 60 + 14 * (q + 0.16f), Mathf.Lerp(sy + 21, o.y + 21, (q + 0.16f) * (q + 0.16f))), Dim, 1.5f, 5f, 5f); }
                        }
                        break;
                    }
                case "dyna":
                    {
                        S.Line(o + new Vector2(-60, 300), o + new Vector2(60, 300), W, 4f);
                        S.Line(o + new Vector2(0, 300), o + new Vector2(0, 270), W, 2.5f);
                        S.Rect(o + new Vector2(0, 200), new Vector2(50, 140), W, 3f);
                        int ticks = k.Perk("dynax2") ? 12 : 6; float step = k.Perk("dynax2") ? 9 : 18;
                        for (int i = 0; i < ticks; i++) S.Line(o + new Vector2(-14, 250 - i * step), o + new Vector2(i % 2 == 0 ? 0 : -6, 250 - i * step), Dim, 2f);
                        if (k.Perk("dynax3"))
                        {   // a vernier beside the scale
                            S.Line(o + new Vector2(32, 240), o + new Vector2(32, 170), W, 2f);
                            for (int i = 0; i < 6; i++) S.Line(o + new Vector2(32, 235 - i * 12), o + new Vector2(40, 235 - i * 12), Dim, 1.8f);
                        }
                        if (k.sketch)
                        {
                            float plateY = 120;
                            S.Zigzag(o + new Vector2(0, 270), o + new Vector2(0, plateY), 6, 10, W, 2.5f);
                            S.Line(o + new Vector2(-16, plateY - 8), o + new Vector2(16, plateY - 8), ChalkTex.Cyan, 4f);
                            S.Line(o + new Vector2(0, plateY - 8), o + new Vector2(0, plateY - 26), W, 2.5f);
                            S.Arc(o + new Vector2(0, plateY - 36), 10, Mathf.PI * 0.2f, Mathf.PI * 1.6f, ChalkTex.Cyan, 3f);
                            Sample(S, o + new Vector2(0, plateY - 67));
                        }
                        break;
                    }
                case "spring":
                    {
                        S.Line(o + new Vector2(-30, 0), o + new Vector2(30, 0), W, 4f);
                        S.Line(o + new Vector2(-24, 0), o + new Vector2(-24, 10), W, 3f);
                        S.Line(o + new Vector2(24, 0), o + new Vector2(24, 10), W, 3f);
                        if (k.Dev("cam")) { S.Rect(o + new Vector2(62, 60), new Vector2(30, 24), W, 2.5f); S.Line(o + new Vector2(47, 60), o + new Vector2(28, 60), Dim, 2f); }
                        if (k.sketch)
                        {
                            float plate = 8 + 110 * 0.7f;
                            S.Zigzag(o + new Vector2(0, 8), o + new Vector2(0, plate), 7, 14, W, 3f);
                            S.Line(o + new Vector2(-26, plate), o + new Vector2(26, plate), ChalkTex.Cyan, 5f);
                            Sample(S, o + new Vector2(0, plate + 21));
                            S.Dashed(o + new Vector2(-40, 118), o + new Vector2(40, 118), Dim, 1.5f, 6f, 6f);
                        }
                        break;
                    }
                case "pend":
                    {   // a gallows on the floor; the thread hangs from the pivot
                        var piv = o + new Vector2(40, 300);
                        S.Line(new Vector2(piv.x - 70, piv.y + 20), new Vector2(piv.x + 60, piv.y + 20), W, 4f);
                        S.Line(new Vector2(piv.x + 60, piv.y + 20), new Vector2(piv.x + 60, o.y), W, 3.5f);
                        S.Line(new Vector2(piv.x, piv.y + 20), new Vector2(piv.x, piv.y), W, 2.5f);
                        S.Circle(piv, 5, W, 3f);
                        S.Dashed(piv + new Vector2(0, -20), piv + new Vector2(0, -k.pendLen - 30), Dim, 1.5f, 8f, 8f);
                        if (k.Perk("pendx2")) Clock(S, new Vector2(piv.x - 50, piv.y - 20), 12, W);                    // a stopwatch on the frame
                        if (k.Perk("pendx3")) Clock(S, new Vector2(piv.x - 50, piv.y - 62), 17, W);                    // a chronometer below it
                        if (k.sketch)
                        {
                            float a = 0.35f; var d = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                            S.Line(piv, piv + d * k.pendLen, W, 2.5f);
                            Sample(S, piv + d * (k.pendLen + 21));
                            S.Arc(piv, k.pendLen + 40, -Mathf.PI / 2 - 0.4f, -Mathf.PI / 2 + 0.4f, Dim, 1.5f);
                        }
                        break;
                    }
                case "arch":
                    {
                        var tub = new List<Vector2> { o + new Vector2(-70, 130), o + new Vector2(-60, 12) };
                        for (int i = 0; i <= 6; i++) { float a = Mathf.PI + i / 6f * Mathf.PI / 2; tub.Add(o + new Vector2(-45, 12) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12); }
                        for (int i = 0; i <= 6; i++) { float a = -Mathf.PI / 2 + i / 6f * Mathf.PI / 2; tub.Add(o + new Vector2(165, 12) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12); }
                        tub.Add(o + new Vector2(180, 12)); tub.Add(o + new Vector2(190, 130));
                        S.Poly(tub, W, 4f);
                        if (k.Perk("archx2")) { S.Poly(new[] { o + new Vector2(-120, 0), o + new Vector2(-115, 60), o + new Vector2(-85, 60), o + new Vector2(-80, 0) }, W, 2.5f, true); for (int i = 1; i < 4; i++) S.Line(o + new Vector2(-88, i * 14), o + new Vector2(-82, i * 14), Dim, 1.8f); }   // a measuring cup
                        if (k.Perk("archx3")) { S.Rect(o + new Vector2(215, 80), new Vector2(14, 150), W, 2.5f); for (int i = 0; i < 8; i++) S.Line(o + new Vector2(215, 15 + i * 18), o + new Vector2(222, 15 + i * 18), Dim, 1.6f); S.Line(o + new Vector2(215, 5), o + new Vector2(215, -6), W, 2f); }   // a burette
                        if (k.sketch)
                        {
                            var w = new List<Vector2>();
                            for (int i = 0; i <= 14; i++) { float x = Mathf.Lerp(-67, 187, i / 14f); w.Add(o + new Vector2(x, 95 + Mathf.Sin(x * 0.05f) * 3)); }
                            S.Poly(w, new Color(0.45f, 0.86f, 1f, 0.6f), 2.5f, false, false);
                            Sample(S, o + new Vector2(60, 40));
                        }
                        break;
                    }
                case "density":
                    {   // a measuring cylinder on a scale
                        S.Rect(o + new Vector2(0, 14), new Vector2(110, 28), W, 3f);
                        S.Line(o + new Vector2(-40, 28), o + new Vector2(-40, 130), W, 3f);
                        S.Line(o + new Vector2(40, 28), o + new Vector2(40, 130), W, 3f);
                        S.Line(o + new Vector2(-40, 130), o + new Vector2(-50, 138), W, 3f);
                        int ticks = k.Perk("densx2") ? 9 : 4; float step = k.Perk("densx2") ? 11 : 22;
                        for (int i = 1; i <= ticks; i++) S.Line(o + new Vector2(30, 28 + i * step), o + new Vector2(40, 28 + i * step), Dim, 2f);
                        S.Circle(o + new Vector2(0, 165), 16, W, 2.5f);
                        if (k.sketch)
                        {
                            S.Line(o + new Vector2(-38, 98), o + new Vector2(38, 98), new Color(0.45f, 0.86f, 1f, 0.6f), 2.5f);
                            S.Dashed(o + new Vector2(-38, 122), o + new Vector2(38, 122), new Color(0.45f, 0.86f, 1f, 0.6f), 2f, 6f, 6f);
                            Sample(S, o + new Vector2(0, 55));
                            S.Line(o + new Vector2(0, 165), o + new Vector2(8, 175), ChalkTex.Red, 2.5f);
                        }
                        break;
                    }
                case "friction":
                    {   // a table with a fixed dynamometer on the right
                        S.Line(o + new Vector2(-80, 0), o + new Vector2(80, 0), W, 4f);
                        S.Rect(o + new Vector2(70, 26), new Vector2(26, 44), W, 2.5f);
                        S.Line(o + new Vector2(83, 26), o + new Vector2(95, 26), W, 2.5f);
                        S.Line(o + new Vector2(95, 10), o + new Vector2(95, 42), W, 3f);
                        if (k.sketch) { Sample(S, o + new Vector2(-40, 21)); S.Zigzag(o + new Vector2(-19, 26), o + new Vector2(50, 26), 4, 6, W, 2f); }
                        break;
                    }
                case "slide":
                    {   // a long table with a pusher at the left
                        bool x2 = k.Perk("slidex2"), x3 = k.Perk("slidex3");
                        S.Line(o + new Vector2(-90, 0), o + new Vector2(120, 0), W, x3 ? 7f : 4f);
                        if (x2) for (int i = 0; i < 12; i++) S.Line(o + new Vector2(-80 + i * 17, 2), o + new Vector2(-74 + i * 17, 9), Dim, 1.6f);   // sandpaper grit
                        S.Line(o + new Vector2(-90, 0), o + new Vector2(-90, 60), W, 3f);
                        S.Line(o + new Vector2(120, 0), o + new Vector2(120, 30), W, 3f);
                        S.Arrow(o + new Vector2(-70, 70), o + new Vector2(90, 70), Dim, 2f, 9f);
                        if (k.sketch) { Sample(S, o + new Vector2(0, 21)); S.Line(o + new Vector2(-70, 8), o + new Vector2(-70, 50), ChalkTex.Cyan, 5f); }
                        break;
                    }
                case "fly":
                    {
                        var c = o + new Vector2(60, 100); const float R = 55;
                        float wx = o.x - 50; float wyTop = o.y + 215;
                        S.Line(c + new Vector2(-60, -R - 25), c + new Vector2(60, -R - 25), W, 3.5f);
                        S.Line(c, c + new Vector2(-45, -R - 25), Dim, 2.5f);
                        S.Line(c, c + new Vector2(45, -R - 25), Dim, 2.5f);
                        S.Circle(new Vector2(wx, wyTop + 35), 10, W, 3f);
                        S.Line(new Vector2(wx, wyTop + 45), new Vector2(wx, wyTop + 65), W, 3f);
                        S.Line(new Vector2(wx - 40, wyTop + 65), new Vector2(wx + 40, wyTop + 65), W, 3.5f);
                        S.Line(new Vector2(wx, wyTop + 35), c + new Vector2(0, R), Dim, 1.5f);
                        if (k.Dev("winder")) { S.Rect(new Vector2(wx - 45, wyTop + 20), new Vector2(30, 24), W, 2.5f); S.Line(new Vector2(wx - 30, wyTop + 20), new Vector2(wx - 10, wyTop + 30), Dim, 2f); }
                        if (k.sketch)
                        {
                            S.Circle(c, R, W, 4f, false);
                            S.Circle(c, R - 10, W, 2.5f, false);
                            for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); S.Line(c + d * 10, c + d * (R - 10), W, 3f); }
                            S.Line(new Vector2(wx, wyTop + 35), new Vector2(wx, o.y + 150), W, 2f);
                            Sample(S, new Vector2(wx, o.y + 129));
                        }
                        break;
                    }
                case "stand": DrawCircuit(S, o, k); break;
                case "calor":
                    {   // the calorimeter: a double-walled vessel with a lid, a stirrer and a thermometer through the lid
                        S.Line(o + new Vector2(-62, 0), o + new Vector2(62, 0), W, 3.5f);
                        S.Line(o + new Vector2(-62, 0), o + new Vector2(-62, 150), W, 3.5f);
                        S.Line(o + new Vector2(62, 0), o + new Vector2(62, 150), W, 3.5f);
                        S.Line(o + new Vector2(-48, 12), o + new Vector2(48, 12), W, 2.5f);
                        S.Line(o + new Vector2(-48, 12), o + new Vector2(-48, 145), W, 2.5f);
                        S.Line(o + new Vector2(48, 12), o + new Vector2(48, 145), W, 2.5f);
                        for (int i = 0; i < 7; i++) { float y = 20 + i * 18; S.Line(o + new Vector2(-60, y), o + new Vector2(-50, y + 10), Dim, 1.6f); S.Line(o + new Vector2(50, y), o + new Vector2(60, y + 10), Dim, 1.6f); }   // the air gap that keeps the heat in
                        S.Line(o + new Vector2(-68, 152), o + new Vector2(68, 152), W, 3.5f);
                        S.Line(o + new Vector2(-10, 152), o + new Vector2(-10, 162), W, 2.5f); S.Line(o + new Vector2(-18, 162), o + new Vector2(-2, 162), W, 3f);
                        S.Line(o + new Vector2(-30, 40), o + new Vector2(-30, 185), W, 2.2f); S.Circle(o + new Vector2(-30, 192), 7, W, 2.2f);        // the stirrer
                        S.Rect(o + new Vector2(30, 128), new Vector2(12, 180), W, 2.2f); S.Circle(o + new Vector2(30, 32), 8, ChalkTex.Red, 2.5f); // the thermometer
                        for (int i = 0; i < 6; i++) S.Line(o + new Vector2(36, 160 + i * 9), o + new Vector2(41, 160 + i * 9), Dim, 1.5f);
                        if (k.Perk("calmx2")) S.Rect(o + new Vector2(0, 76), new Vector2(148, 164), Dim, 2f);                                     // double walls
                        if (k.Dev("autocal")) { S.Rect(o + new Vector2(90, 180), new Vector2(26, 24), W, 2.5f); S.Line(o + new Vector2(77, 180), o + new Vector2(-22, 190), Dim, 1.8f); }
                        if (k.sketch)
                        {
                            var w = new List<Vector2>();
                            for (int i = 0; i <= 10; i++) { float x = Mathf.Lerp(-46, 46, i / 10f); w.Add(o + new Vector2(x, 100 + Mathf.Sin(x * 0.12f) * 3)); }
                            S.Poly(w, new Color(0.45f, 0.86f, 1f, 0.6f), 2.5f, false, false);
                            Sample(S, o + new Vector2(-4, 34));
                            S.Line(o + new Vector2(30, 40), o + new Vector2(30, 170), ChalkTex.Red, 3f);
                        }
                        break;
                    }
                case "ice":
                    {   // a dish on the floor with a block of ice in it; the specimen melts its way in from the top
                        S.Line(o + new Vector2(-86, 4), o + new Vector2(86, 4), W, 3.5f);
                        S.Line(o + new Vector2(-86, 4), o + new Vector2(-92, 22), W, 3f);
                        S.Line(o + new Vector2(86, 4), o + new Vector2(92, 22), W, 3f);
                        if (k.Dev("icebox")) { S.Rect(o + new Vector2(118, 60), new Vector2(34, 48), W, 2.5f); for (int i = 0; i < 3; i++) S.Line(o + new Vector2(106, 48 + i * 12), o + new Vector2(130, 48 + i * 12), Dim, 1.6f); }
                        if (k.Perk("icex2")) for (int i = 0; i < 5; i++) { var p = o + new Vector2(-70 + i * 35, 100 + (i % 2) * 10); S.Line(p + new Vector2(-5, 0), p + new Vector2(5, 0), Dim, 1.8f); S.Line(p + new Vector2(0, -5), p + new Vector2(0, 5), Dim, 1.8f); }   // frost
                        if (k.sketch) { Ice(S, o, 0, 0); Sample(S, o + new Vector2(0, 95)); }
                        break;
                    }
                case "heater":
                    {   // a pot of water with an immersion heater
                        S.Poly(new[] { o + new Vector2(-60, 150), o + new Vector2(-52, 0), o + new Vector2(52, 0), o + new Vector2(60, 150) }, W, 3.5f);
                        S.Line(o + new Vector2(0, 220), o + new Vector2(0, 40), W, 3f);
                        var tp = o + new Vector2(-128, 0);                                 // the bench thermometer
                        S.Rect(tp + new Vector2(0, 112), new Vector2(18, 190), W, 2.5f);
                        S.Circle(tp + new Vector2(0, 12), 13, W, 2.5f);
                        for (int i = 0; i <= 8; i++) S.Line(tp + new Vector2(9, 30 + i * 20), tp + new Vector2(i % 2 == 0 ? 17 : 14, 30 + i * 20), Dim, 1.6f);
                        if (k.Dev("lagging")) for (int i = 0; i < 6; i++) S.Line(o + new Vector2(-66 - i * 0.6f, 12 + i * 24), o + new Vector2(-58, 22 + i * 24), Dim, 2f);
                        var coil = k.Perk("heatx3") ? ChalkTex.Red : ChalkTex.Cyan;
                        S.Zigzag(o + new Vector2(0, 40), o + new Vector2(0, 100), 4, 12, coil, 3f);
                        if (k.Perk("heatx2")) { S.Line(o + new Vector2(24, 220), o + new Vector2(24, 40), W, 2.5f); S.Zigzag(o + new Vector2(24, 40), o + new Vector2(24, 100), 4, 10, coil, 3f); }
                        if (k.Perk("heatx3")) S.Zigzag(o + new Vector2(-24, 40), o + new Vector2(-24, 100), 5, 10, coil, 3f);
                        if (k.sketch)
                        {
                            var w = new List<Vector2>();
                            for (int i = 0; i <= 10; i++) { float x = Mathf.Lerp(-50, 50, i / 10f); w.Add(o + new Vector2(x, 120 + Mathf.Sin(x * 0.1f) * 3)); }
                            S.Poly(w, new Color(0.45f, 0.86f, 1f, 0.6f), 2.5f, false, false);
                            Sample(S, o + new Vector2(-22, 31));
                            S.Rect(o + new Vector2(40, 100), new Vector2(8, 80), W, 2f); S.Circle(o + new Vector2(40, 56), 7, ChalkTex.Red, 2.5f); S.Line(o + new Vector2(40, 60), o + new Vector2(40, 110), ChalkTex.Red, 3f);
                        }
                        break;
                    }
                case "engine":
                    {
                        S.Rect(o + new Vector2(0, 60), new Vector2(120, 120), W, 3.5f);
                        if (k.Perk("enginex2")) S.Rect(o + new Vector2(-40, 80), new Vector2(120, 120), Dim, 2.5f);          // a second boiler behind
                        if (k.Perk("enginex3")) { S.Line(o + new Vector2(-30, 120), o + new Vector2(-30, 150), W, 2.5f); S.Zigzag(o + new Vector2(-30, 150), o + new Vector2(30, 150), 4, 8, W, 2.5f); S.Line(o + new Vector2(30, 150), o + new Vector2(30, 120), W, 2.5f); }   // a superheater loop
                        S.Rect(o + new Vector2(110, 40), new Vector2(60, 50), W, 3f);
                        S.Line(o + new Vector2(-60, 60), o + new Vector2(-70, 60), W, 3f);
                        if (k.Dev("valve")) { S.Rect(o + new Vector2(-98, 130), new Vector2(30, 26), W, 2.5f); S.Line(o + new Vector2(-98, 117), o + new Vector2(-98, 75), Dim, 2f); }
                        if (k.sketch)
                        {
                            S.Circle(o + new Vector2(-70, 60), 14, ChalkTex.Cyan, 3f);
                            S.Line(o + new Vector2(120, 40), o + new Vector2(140, 40), W, 3f);
                            S.Circle(o + new Vector2(190, 40), 30, W, 3f);
                            S.Line(o + new Vector2(190, 40), o + new Vector2(190, 66), W, 2.5f);
                            for (int i = 0; i < 3; i++) S.Arc(o + new Vector2(-30 + i * 30, -6), 12, Mathf.PI * 1.1f, Mathf.PI * 1.9f, ChalkTex.Red, 2.5f);
                            S.Dashed(o + new Vector2(-150, 100), o + new Vector2(-60, 100), new Color(1f, 0.5f, 0.35f, 0.6f), 2f, 8f, 8f);
                        }
                        break;
                    }
                case "piston":
                    {
                        S.Rect(o + new Vector2(0, 90), new Vector2(60, 180), W, 3.5f);
                        S.Line(o + new Vector2(-40, 0), o + new Vector2(40, 0), ChalkTex.Red, 4f);
                        for (int i = 0; i < 3; i++) S.Arc(o + new Vector2(-15 + i * 15, 8), 8, 0, Mathf.PI, ChalkTex.Red, 2f);
                        if (k.Dev("crank")) { S.Circle(o + new Vector2(62, 110), 16, W, 2.5f); S.Line(o + new Vector2(62, 110), o + new Vector2(30, 150), Dim, 2f); }
                        if (k.sketch)
                        {
                            float py = o.y + 100;
                            S.Line(o + new Vector2(-28, 100), o + new Vector2(28, 100), ChalkTex.Cyan, 5f);
                            S.Line(new Vector2(o.x, py), new Vector2(o.x, py + 60), W, 3f);
                            S.HatchRect(new Vector2(o.x, (o.y + 4 + py) / 2), new Vector2(50, py - o.y - 8), new Color(1f, 0.5f, 0.35f, 0.35f), 2f, 10f);
                            S.Dashed(o + new Vector2(-28, 150), o + new Vector2(28, 150), Dim, 1.5f, 6f, 6f);
                        }
                        break;
                    }
                case "psi":
                    {   // the universe: a spiral in a circle
                        S.Circle(o, 120, W, 3.5f);
                        var pts = new List<Vector2>();
                        for (int i = 0; i <= 80; i++) { float a = i / 80f * Mathf.PI * 6; float r = 4 + i / 80f * 100; pts.Add(o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
                        S.Poly(pts, Dim, 2f, false, false);
                        for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; S.Line(o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 126, o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 134, Dim, 1.6f); }
                        break;
                    }
            }
        }
    }
}
