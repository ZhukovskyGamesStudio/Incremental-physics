using System;
using System.Collections.Generic;

namespace ChalkPhysics
{
    public enum Cur { J, Obs, C, Cal }

    public class LetterDef
    {
        public string id, sym, name, unit;
        public int domain;
        public Cur cost = Cur.J;
        public double baseV, step;
        public bool mul;                         // value = base * step^(L-1) instead of base + step*(L-1)
        public double min = double.MinValue, max = double.MaxValue;
        public double cost0, costK;
        public string fmt = "0.##";
        public string derivedFrom;               // formula id that produces this letter
        public bool perSample;                   // every sample has its own value (μ); nothing to upgrade
        public double openCost;                  // price to open the letter on the map (0 = free)
        public Cur openCur = Cur.J;              // what it is opened with: the steam era opens its first letters for heat
        public bool Derived => derivedFrom != null;
    }

    public enum FKind { Energy, Measure, Final }

    public class FormulaDef
    {
        public string id, title, riddle, station;
        public int domain;
        public FKind kind;
        public Cur yields = Cur.J;               // Energy formulas pay this currency
        public double obs;                       // ideas per firing (measuring instruments only)
        public string[] parts;                   // "{m}" = slot for letter m, anything else is literal text
        public string[] outputs = new string[0]; // derived letters produced
        public Func<GameState, double> eval;
        public string effect;                    // what the player does now
        public string[] needs = new string[0];   // sample properties that must be measured for full credit
        public string reveals;                   // sample property this experiment measures
        public bool sample;                      // the station takes a sample from the box
        public int slot;                         // which socket of a multi-socket instrument this formula uses
        public string icon;                      // icon glyph for the tree

        public IEnumerable<string> Slots()
        {
            foreach (var p in parts)
                if (p.StartsWith("{")) yield return p.Substring(1, p.Length - 2);
        }
    }

    public class DeviceDef
    {
        public string id, title, desc, requires, icon;
        public int domain;
        public Cur cost = Cur.Obs;
        public double price;
        public string automates;                 // station id this device runs automatically
        public float period;                     // seconds between automatic firings
    }

    public enum PerkKind { Actions, ObsMult, StationMult, AutoMult, Samples, CurMult, Flag }

    /// A one-shot node in the tree that gives something tangible. requires = a formula id, or "L:x" for a letter.
    public class PerkDef
    {
        public string id, title, desc, requires, icon;
        public int domain;
        public PerkKind kind;
        public double value;
        public string station;                     // for StationMult
        public Cur cur = Cur.Obs;                  // for CurMult: the currency multiplied
        public Cur cost = Cur.Obs;                 // what it is paid with (a few teasers ask for the next era's coin)
        public double price;
    }

    public class RevolutionDef { public int to; public double cost; public string[] freeLetters = new string[0]; }

    /// A gate on the trunk that sells a hint instead of a letter (the formula of everything hides behind it).
    public class GateDef { public string id, title, riddle, hint; public double cost; public int domain; }

    public static class Defs
    {
        public static readonly string[] DomainNames = { "Механика", "Электричество", "Тепло" };
        public static readonly double[] DomainMult = { 1, 2, 10 };
        /// Ideas have no written name: a chalk lightbulb is drawn next to the number instead.
        public static readonly string[] CurName = { "Дж", "", "Кл", "ккал" };
        public static readonly string[] CurLong = { "энергия", "идеи", "заряд", "теплота" };

        public static readonly List<LetterDef> Letters = new List<LetterDef>
        {
            new LetterDef { id = "m", sym = "m", name = "средняя масса образцов", unit = "кг", baseV = 1, step = 1, cost0 = 10, costK = 2.1 },
            new LetterDef { id = "g", sym = "g", name = "гравитация", unit = "м/с²", baseV = 9.81, step = 1.15, mul = true, cost0 = 200, costK = 3.2 },
            new LetterDef { id = "h", sym = "h", name = "высота стеллажа", unit = "м", baseV = 1, step = 1, max = 20, cost0 = 20, costK = 2.1, fmt = "0", openCost = 15, openCur = Cur.Obs },
            new LetterDef { id = "F", sym = "F", name = "сила тяжести", unit = "Н", derivedFrom = "dyna", fmt = "0.#" },
            new LetterDef { id = "k", sym = "k", name = "жёсткость пружины", unit = "Н/м", baseV = 3000, step = 1000, cost0 = 400, costK = 2.3, fmt = "0", openCost = 800 },
            new LetterDef { id = "x", sym = "x", name = "сжатие пружины", unit = "м", baseV = 1, step = 0.2, max = 6, cost0 = 600, costK = 2.5, openCost = 1500 },
            new LetterDef { id = "l", sym = "l", name = "длина нити", unit = "м", baseV = 1.0, step = 0.85, mul = true, min = 0.05, cost0 = 1500, costK = 2.5, openCost = 30000 },
            new LetterDef { id = "T", sym = "T", name = "период маятника", unit = "с", derivedFrom = "pend", fmt = "0.00" },
            new LetterDef { id = "rho", sym = "ρ", name = "плотность жидкости", unit = "кг/м³", baseV = 1000, step = 250, cost0 = 4000, costK = 2.3, fmt = "0", openCost = 150000 },
            new LetterDef { id = "V", sym = "V", name = "средний объём образцов", unit = "м³", baseV = 0.01, step = 0.01, max = 5, cost0 = 6000, costK = 2.5, openCost = 400000 },
            new LetterDef { id = "Fa", sym = "Fа", name = "сила Архимеда", unit = "Н", derivedFrom = "arch", fmt = "0.#" },
            new LetterDef { id = "mu", sym = "μ", name = "шероховатость образца", unit = "", perSample = true, baseV = 0.5, step = 0, cost0 = 0, costK = 1, fmt = "0.00", openCost = 1e6 },
            new LetterDef { id = "d", sym = "d", name = "длина дорожки", unit = "м", baseV = 200, step = 50, max = 2000, cost0 = 2000, costK = 2.2, fmt = "0", openCost = 2.5e6 },
            new LetterDef { id = "I", sym = "I", name = "момент инерции", unit = "кг·м²", baseV = 20, step = 5, cost0 = 2e4, costK = 2.3, fmt = "0", openCost = 6e6 },
            new LetterDef { id = "w", sym = "ω", name = "угловая скорость", unit = "рад/с", baseV = 140, step = 12, max = 800, cost0 = 3e4, costK = 2.5, fmt = "0", openCost = 1.2e7 },
            // --- electricity (levels bought with coulombs) ---
            new LetterDef { id = "U", sym = "U", name = "напряжение", unit = "В", domain = 1, cost = Cur.C, baseV = 220, step = 60, cost0 = 60, costK = 2.8, fmt = "0" },
            new LetterDef { id = "R", sym = "R", name = "сопротивление", unit = "Ом", domain = 1, cost = Cur.C, baseV = 10, step = 0.88, mul = true, min = 0.5, cost0 = 100, costK = 3.0 },
            new LetterDef { id = "Ie", sym = "I", name = "сила тока", unit = "А", domain = 1, derivedFrom = "ohm", fmt = "0.##" },
            new LetterDef { id = "t", sym = "t", name = "время включения", unit = "с", domain = 1, cost = Cur.C, baseV = 1, step = 1, max = 30, cost0 = 150, costK = 3.0, fmt = "0" },
            new LetterDef { id = "P", sym = "P", name = "мощность", unit = "Вт", domain = 1, derivedFrom = "pow", fmt = "0.#" },
            new LetterDef { id = "Cc", sym = "C", name = "ёмкость", unit = "Ф", domain = 1, cost = Cur.C, baseV = 0.2, step = 0.1, cost0 = 200, costK = 2.8, fmt = "0.##", openCost = 4e6 },
            new LetterDef { id = "Lind", sym = "L", name = "индуктивность", unit = "Гн", domain = 1, cost = Cur.C, baseV = 6, step = 3, cost0 = 400, costK = 2.8, fmt = "0.#", openCost = 1.5e8 },
            // --- heat (levels bought with kilocalories) ---
            new LetterDef { id = "c", sym = "c", name = "теплоёмкость", unit = "Дж/(кг·К)", domain = 2, cost = Cur.Cal, baseV = 400, step = 200, cost0 = 4e4, costK = 2.4, fmt = "0" },
            new LetterDef { id = "dT", sym = "ΔT", name = "нагрев", unit = "К", domain = 2, cost = Cur.Cal, baseV = 10, step = 5, cost0 = 7e4, costK = 2.5, fmt = "0" },
            new LetterDef { id = "Q", sym = "Q", name = "количество теплоты", unit = "Дж", domain = 2, derivedFrom = "heat", fmt = "0" },
            new LetterDef { id = "Tn", sym = "Tн", name = "t° нагревателя", unit = "К", domain = 2, cost = Cur.Cal, baseV = 400, step = 50, max = 2000, cost0 = 1.5e5, costK = 2.5, fmt = "0", openCost = 1.5e5, openCur = Cur.Cal },
            new LetterDef { id = "Tx", sym = "Tх", name = "t° холодильника", unit = "К", domain = 2, cost = Cur.Cal, baseV = 300, step = 0.93, mul = true, min = 20, cost0 = 2.5e5, costK = 2.6, fmt = "0", openCost = 4e5, openCur = Cur.Cal },
            new LetterDef { id = "eta", sym = "η", name = "КПД", unit = "", domain = 2, derivedFrom = "carnot", fmt = "0%" },
            new LetterDef { id = "lam", sym = "λ", name = "теплота плавления", unit = "Дж/кг", domain = 2, cost = Cur.Cal, baseV = 3e4, step = 1.5e4, cost0 = 6e5, costK = 2.5, fmt = "0", openCost = 5e8 },
            new LetterDef { id = "p", sym = "p", name = "давление пара", unit = "Па", domain = 2, cost = Cur.Cal, baseV = 2e5, step = 1e5, cost0 = 2e6, costK = 2.5, fmt = "0", openCost = 4e9 },
            new LetterDef { id = "dV", sym = "ΔV", name = "ход поршня", unit = "м³", domain = 2, cost = Cur.Cal, baseV = 2, step = 1, cost0 = 4e6, costK = 2.6, openCost = 3e10, fmt = "0" },
        };

        public static readonly List<FormulaDef> Formulas = new List<FormulaDef>
        {
            // ---------- mechanics ----------
            new FormulaDef { id = "fall", title = "Падение с полки", station = "fall", kind = FKind.Energy, sample = true, icon = "fall",
                riddle = "Образец лежит на полке. Какая энергия в нём запасена? Она зависит от массы, гравитации и высоты.",
                parts = new[] { "E = ", "{m}", "·", "{g}", "·", "{h}" },
                eval = s => s.V("m") * s.V("g") * s.V("h"),
                effect = "Перетащи образец на полку — он падает и отдаёт энергию. Тяжёлый даёт больше" },
            new FormulaDef { id = "dyna", title = "Динамометр", station = "dyna", kind = FKind.Measure, obs = 6, sample = true, reveals = "m", icon = "dyna",
                riddle = "Подвесим образец на пружинные весы. С какой силой его тянет Земля? Заодно узнаем его массу.",
                parts = new[] { "F = ", "{m}", "·", "{g}" },
                outputs = new[] { "F" }, eval = s => s.V("m") * s.V("g"),
                effect = "Перетащи образец на весы — узнаешь его массу. Изученный образец засчитывается в опытах полностью" },
            new FormulaDef { id = "spring", title = "Пружина", station = "spring", kind = FKind.Energy, sample = true, icon = "spring",
                riddle = "Положим образец на пружину, сожмём её и отпустим. Сколько энергии она запасла?",
                parts = new[] { "E = ½·", "{k}", "·", "{x}", "²" },
                eval = s => 0.5 * s.V("k") * s.V("x") * s.V("x"),
                effect = "Положи образец на пружину и отпусти — она вернёт запасённую энергию" },
            new FormulaDef { id = "pend", title = "Маятник", station = "pend", kind = FKind.Measure, obs = 7, sample = true, icon = "pend",
                riddle = "Качнём образец на нити и засечём период. От чего он зависит?",
                parts = new[] { "T = 2π·√(", "{l}", " / ", "{g}", ")" },
                outputs = new[] { "T" }, eval = s => 2 * Math.PI * Math.Sqrt(s.V("l") / s.V("g")),
                effect = "Маятник качается сам. Но образец на нити приедается: чем дольше висит, тем меньше идей — меняй его" },
            new FormulaDef { id = "arch", title = "Ванна Архимеда", station = "arch", kind = FKind.Measure, obs = 15, sample = true, icon = "arch",
                riddle = "Опустим образец в воду. С какой силой вода его выталкивает? Сила зависит от объёма образца — а измерять объём мы пока не умеем.",
                parts = new[] { "Fа = ", "{rho}", "·", "{g}", "·", "{V}" },
                outputs = new[] { "Fa" }, eval = s => s.V("rho") * s.V("g") * s.V("V"),
                effect = "Перетащи образец в ванну: он плюхается в воду. Тонущий образец не даёт ничего — нужен тот, что всплывает" },
            new FormulaDef { id = "density", title = "Мензурка", station = "density", kind = FKind.Measure, obs = 15, sample = true, reveals = "V", icon = "density",
                riddle = "Опустим образец в мензурку с водой: сколько воды он вытеснит — таков его объём. А зная массу, узнаем и плотность.",
                parts = new[] { "ρобр = ", "{m}", " / ", "{V}" },
                eval = s => s.V("m") / s.V("V"),
                effect = "Перетащи образец в мензурку — узнаешь его объём. Взвешенный образец даёт больше идей" },
            new FormulaDef { id = "slide", title = "Работа трения", station = "slide", kind = FKind.Energy, sample = true, icon = "slide",
                riddle = "Протащим образец через весь стол. Какую работу совершит сила трения? Шершавый и тяжёлый образец греет стол сильнее — но насколько он шершавый?",
                parts = new[] { "A = ", "{mu}", "·", "{m}", "·", "{g}", "·", "{d}" },
                eval = s => (s.ActiveSample != null ? s.ActiveSample.mu : 0.5) * s.V("m") * s.V("g") * s.V("d"),
                effect = "Перетащи образец на длинный стол — работа трения зачтётся. Пока μ не измерена, засчитывается половина" },
            new FormulaDef { id = "friction", title = "Стол трения", station = "friction", kind = FKind.Measure, obs = 9, sample = true, reveals = "mu", icon = "friction",
                riddle = "Потянем образец по столу за динамометр. Сила трения зависит от веса и от того, насколько образец шершавый — так мы её и измерим.",
                parts = new[] { "Fтр = ", "{mu}", "·", "{m}", "·", "{g}" },
                eval = s => s.ActiveSample != null ? s.ActiveSample.mu : 0.5,
                effect = "Перетащи образец на стол — узнаешь его шероховатость μ. Взвешенный образец даёт больше" },
            new FormulaDef { id = "fly", title = "Маховик", station = "fly", kind = FKind.Energy, sample = true, icon = "fly",
                riddle = "Повесим образец на верёвку — падая, он раскрутит тяжёлое колесо. Энергия вращения: вместо массы — момент инерции, вместо скорости — угловая.",
                parts = new[] { "E = ½·", "{I}", "·", "{w}", "²" },
                eval = s => 0.5 * s.V("I") * s.V("w") * s.V("w"),
                effect = "Повесь образец на верёвку маховика — он раскрутит колесо" },

            // ---------- electricity: one circuit that grows, with a socket for every new element ----------
            new FormulaDef { id = "ohm", title = "Закон Ома", station = "stand", domain = 1, kind = FKind.Measure, obs = 18, sample = true, slot = 0, icon = "stand",
                riddle = "Соберём цепь: батарея, амперметр, а вместо резистора — образец. Замкнём рубильник: какой ток потечёт?",
                parts = new[] { "I = ", "{U}", " / ", "{R}" },
                outputs = new[] { "Ie" }, eval = s => s.V("U") / s.V("R"),
                effect = "Вставь образец в гнездо резистора и замкни рубильник — амперметр даёт идеи" },
            new FormulaDef { id = "charge", title = "Заряд", station = "stand", domain = 1, kind = FKind.Energy, yields = Cur.C, sample = true, slot = 0, icon = "charge",
                riddle = "Пока рубильник замкнут, по цепи течёт ток. Сколько заряда протекает за время включения?",
                parts = new[] { "q = ", "{Ie}", "·", "{t}" },
                eval = s => s.V("Ie") * s.V("t"),
                effect = "Каждое замыкание даёт кулоны — за них качаются буквы электричества" },
            new FormulaDef { id = "pow", title = "Мощность тока", station = "stand", domain = 1, kind = FKind.Measure, obs = 18, slot = 0, icon = "lamp",
                riddle = "Подключим лампочку. Какую мощность она берёт из цепи?",
                parts = new[] { "P = ", "{U}", "·", "{Ie}" },
                outputs = new[] { "P" }, eval = s => s.V("U") * s.V("Ie"),
                effect = "К цепи добавилась лампочка — она загорается при каждом замыкании" },
            new FormulaDef { id = "lamp", title = "Работа тока", station = "stand", domain = 1, kind = FKind.Energy, slot = 0, icon = "lamp",
                riddle = "Лампочка горит t секунд. Сколько энергии она выдала?",
                parts = new[] { "A = ", "{P}", "·", "{t}" },
                eval = s => s.V("P") * s.V("t"),
                effect = "Каждое замыкание приносит энергию" },
            new FormulaDef { id = "cap", title = "Конденсатор", station = "stand", domain = 1, kind = FKind.Energy, sample = true, slot = 1, icon = "cap",
                riddle = "Врежем в цепь две пластины, а между ними — второй образец. Зарядим до напряжения U: сколько энергии запасёт конденсатор?",
                parts = new[] { "E = ½·", "{Cc}", "·", "{U}", "²" },
                eval = s => 0.5 * s.V("Cc") * s.V("U") * s.V("U"),
                effect = "В цепи появилось второе гнездо: без образца между пластинами схема не запустится" },
            new FormulaDef { id = "coil", title = "Катушка", station = "stand", domain = 1, kind = FKind.Energy, sample = true, slot = 2, icon = "coil",
                riddle = "Добавим в цепь катушку, а внутрь неё — третий образец-сердечник. Сколько энергии запасёт магнитное поле?",
                parts = new[] { "E = ½·", "{Lind}", "·", "{Ie}", "²" },
                eval = s => 0.5 * s.V("Lind") * s.V("Ie") * s.V("Ie"),
                effect = "Третье гнездо схемы: сердечник в катушке. Теперь для запуска нужны три образца" },

            // ---------- heat ----------
            new FormulaDef { id = "heat", title = "Кипятильник", station = "heater", domain = 2, kind = FKind.Energy, yields = Cur.Cal, sample = true, icon = "heater",
                riddle = "Опустим кипятильник в воду. Сколько тепла нужно, чтобы нагреть её на ΔT?",
                parts = new[] { "Q = ", "{c}", "·", "{m}", "·", "{dT}" },
                outputs = new[] { "Q" }, eval = s => s.V("c") * s.V("m") * s.V("dT"),
                effect = "Перетащи образец в котелок — его греют, капают килокалории. Каждый нагрев поднимает температуру стенда" },
            new FormulaDef { id = "calm", title = "Калориметр", station = "calor", domain = 2, kind = FKind.Measure, obs = 30, sample = true, reveals = "m", icon = "calor",
                riddle = "Опустим горячий образец в калориметр и подождём. По тому, сколько тепла он отдал, узнаем его массу.",
                parts = new[] { "m = ", "{Q}", " / (", "{c}", "·", "{dT}", ")" },
                eval = s => s.V("Q") / Math.Max(1e-6, s.V("c") * s.V("dT")),
                effect = "Перетащи образец в калориметр — узнаешь его массу. Единственный прибор эпохи, который изучает образцы" },
            new FormulaDef { id = "carnot", title = "Паровая машина", station = "engine", domain = 2, kind = FKind.Measure, obs = 24, icon = "engine",
                riddle = "Пар из котла крутит машину. Какая доля тепла станет работой? Зависит от температур нагревателя и холодильника.",
                parts = new[] { "η = 1 - ", "{Tx}", " / ", "{Tn}" },
                outputs = new[] { "eta" }, eval = s => Math.Min(0.9, 1 - s.V("Tx") / s.V("Tn")),
                effect = "Открой клапан — машина делает ход, КПД измерен. Чем горячее стенд, тем сильнее ход" },
            new FormulaDef { id = "engine", title = "Работа пара", station = "engine", domain = 2, kind = FKind.Energy, icon = "engine",
                riddle = "Машина превращает тепло кипятильника в работу. Сколько получится?",
                parts = new[] { "A = ", "{eta}", "·", "{Q}" },
                eval = s => s.V("eta") * s.V("Q") * 20,
                effect = "Каждый ход машины приносит энергию — тем больше, чем горячее стенд" },
            new FormulaDef { id = "ice", title = "Плавление", station = "ice", domain = 2, kind = FKind.Energy, yields = Cur.Cal, sample = true, icon = "ice",
                riddle = "Положим раскалённый образец на лёд. Сколько тепла уйдёт на то, чтобы его расплавить?",
                parts = new[] { "Q = ", "{lam}", "·", "{m}" },
                eval = s => s.V("lam") * s.V("m"),
                effect = "Клади образец на лёд — он протапливает лунку. Чем горячее стенд, тем больше тепла успеет уйти в лёд" },
            new FormulaDef { id = "gas", title = "Поршень", station = "piston", domain = 2, kind = FKind.Energy, icon = "piston",
                riddle = "Нагретый газ толкает поршень. Какую работу он совершает?",
                parts = new[] { "A = ", "{p}", "·", "{dV}" },
                eval = s => s.V("p") * s.V("dV"),
                effect = "Клик по поршню — газ толкает его, работа зачтётся. Холодный стенд толкает слабо" },

            new FormulaDef { id = "psi", title = "Формула всего", station = "", domain = 2, kind = FKind.Final, icon = "psi",
                riddle = "Собери в одну формулу всё, что измерили приборы всех эпох. Что будет, если понять всё сразу?..",
                parts = new[] { "Ψ = ", "{F}", "·", "{T}", "·", "{Fa}", "·", "{Ie}", "·", "{P}", "·", "{eta}" },
                eval = s => 1 },
        };

        public static readonly List<DeviceDef> Devices = new List<DeviceDef>
        {
            // ---- mechanics ----
            new DeviceDef { id = "hopper", title = "Толкатель", desc = "толкатель сам сталкивает выданный образец с полки при каждом действии", requires = "fall", price = 5, automates = "fall", period = 2f, icon = "pusher" },
            new DeviceDef { id = "hook", title = "Автоподвес", desc = "динамометр сам взвешивает выданный ему образец при каждом действии", requires = "dyna", price = 10, automates = "dyna", period = 3f, icon = "hook" },
            new DeviceDef { id = "lens1", title = "Лупа", desc = "идей ×1.5", requires = "pend", price = 35, icon = "lens" },
            new DeviceDef { id = "cam", title = "Кулачок", desc = "вал сам сжимает пружину с выданным образцом при каждом действии", requires = "spring", price = 120, automates = "spring", period = 2.5f, icon = "cam" },
            new DeviceDef { id = "clock", title = "Хронометр", desc = "образец на маятнике приедается вдвое медленнее", requires = "pend", price = 160, icon = "clock" },
            new DeviceDef { id = "hopper2", title = "Стопка грузов", desc = "полка роняет по два груза за раз", requires = "density", price = 720, icon = "stack" },
            new DeviceDef { id = "scoop", title = "Черпак", desc = "черпак сам окунает выданный образец при каждом действии", requires = "arch", price = 1080, automates = "arch", period = 3f, icon = "scoop" },
            new DeviceDef { id = "autodens", title = "Лаборант-мензурка", desc = "мензурка сама проверяет выданный образец при каждом действии", requires = "density", price = 900, automates = "density", period = 3f },
            new DeviceDef { id = "tug", title = "Тягач", desc = "мотор сам тянет выданный образец при каждом действии", requires = "friction", price = 1260, automates = "friction", period = 3f },
            new DeviceDef { id = "conveyor", title = "Транспортёр", desc = "дорожка сама толкает выданный образец при каждом действии", requires = "slide", price = 1560, automates = "slide", period = 3f },
            new DeviceDef { id = "lens2", title = "Микроскоп", desc = "идей ×1.5", requires = "slide", price = 1800, icon = "lens" },
            new DeviceDef { id = "winder", title = "Автоподъём", desc = "верёвка маховика сама поднимает выданный образец при каждом действии", requires = "fly", price = 6000, automates = "fly", period = 5f },
            new DeviceDef { id = "hopper3", title = "Штабель", desc = "полка роняет по четыре груза за раз", requires = "friction", price = 9600, icon = "stack" },
            // ---- electricity: the circuit cannot be automated, so its devices sharpen it instead ----
            new DeviceDef { id = "lab", title = "Лаборант", desc = "сам докупает дешёвые буквы (до ¼ запаса)", requires = "ohm", domain = 1, price = 120, icon = "eye" },
            new DeviceDef { id = "lens3", title = "Осциллограф", desc = "идей ×1.5", requires = "pow", domain = 1, price = 1200, icon = "lens" },
            new DeviceDef { id = "lens5", title = "Гальванометр", desc = "идей ×1.5", requires = "cap", domain = 1, price = 10000, icon = "lens" },
            // ---- heat ----
            new DeviceDef { id = "thermo", title = "Терморегулятор", desc = "котелок сам греет выданный образец при каждом действии", requires = "heat", domain = 2, price = 4000, automates = "heater", period = 3f },
            new DeviceDef { id = "autocal", title = "Автокалориметр", desc = "калориметр сам изучает выданный образец при каждом действии", requires = "calm", domain = 2, price = 9000, automates = "calor", period = 3f },
            new DeviceDef { id = "valve", title = "Автоклапан", desc = "клапан машины открывается сам при каждом действии", requires = "carnot", domain = 2, price = 24000, automates = "engine", period = 4f },
            new DeviceDef { id = "lens4", title = "Тепловизор", desc = "идей ×1.5", requires = "carnot", domain = 2, price = 40000, icon = "lens" },
            new DeviceDef { id = "icebox", title = "Ледогенератор", desc = "лёд сам подставляется под выданный образец при каждом действии", requires = "ice", domain = 2, price = 90000, automates = "ice", period = 3f },
            new DeviceDef { id = "lagging", title = "Обшивка котла", desc = "стенд остывает вдвое медленнее", requires = "heat", domain = 2, price = 30000, icon = "heater" },
            new DeviceDef { id = "crank", title = "Кривошип", desc = "поршень ходит сам при каждом действии", requires = "gas", domain = 2, price = 260000, automates = "piston", period = 3f },
        };

        public static readonly List<PerkDef> Perks = new List<PerkDef>
        {
            // ---------- mechanics ----------
            new PerkDef { id = "act1", title = "Долгая смена", desc = "+2 действия за эксперимент", requires = "fall", kind = PerkKind.Actions, value = 2, price = 60 },
            new PerkDef { id = "box1", title = "Коробка побольше", desc = "+2 образца в эксперименте", requires = "dyna", kind = PerkKind.Samples, value = 2, price = 25 },
            new PerkDef { id = "box2", title = "Ящик образцов", desc = "+3 образца в эксперименте", requires = "arch", kind = PerkKind.Samples, value = 3, price = 840 },
            new PerkDef { id = "autoweigh", title = "Лаборант-весовщик", desc = "все образцы взвешены к началу эксперимента", requires = "slide", kind = PerkKind.Flag, value = 1, price = 2400, icon = "scales" },
            new PerkDef { id = "fallx2", title = "Тяжёлые грузы", desc = "падение с полки ×2", requires = "fall", kind = PerkKind.StationMult, station = "fall", value = 2, price = 15 },
            new PerkDef { id = "dynax2", title = "Точная шкала", desc = "динамометр ×2 идей", requires = "dyna", kind = PerkKind.StationMult, station = "dyna", value = 2, price = 20 },
            new PerkDef { id = "act2", title = "Ночная смена", desc = "+2 действия за эксперимент", requires = "friction", kind = PerkKind.Actions, value = 2, price = 900 },
            new PerkDef { id = "springx2", title = "Стальная пружина", desc = "пружина ×2", requires = "spring", kind = PerkKind.StationMult, station = "spring", value = 2, price = 140 },
            new PerkDef { id = "auto1", title = "Слаженная работа", desc = "автоматика срабатывает 2 раза за действие", requires = "spring", kind = PerkKind.AutoMult, value = 2, price = 600 },
            new PerkDef { id = "act3", title = "Сверхурочные", desc = "+2 действия за эксперимент", requires = "density", kind = PerkKind.Actions, value = 2, price = 9000 },
            new PerkDef { id = "pendx2", title = "Секундомер", desc = "маятник ×2 идей", requires = "pend", kind = PerkKind.StationMult, station = "pend", value = 2, price = 840 },
            new PerkDef { id = "archx2", title = "Мерный стакан", desc = "ванна ×2 идей", requires = "arch", kind = PerkKind.StationMult, station = "arch", value = 2, price = 2400 },
            new PerkDef { id = "slidex2", title = "Наждак", desc = "работа трения ×2", requires = "slide", kind = PerkKind.StationMult, station = "slide", value = 2, price = 2100 },
            new PerkDef { id = "densx2", title = "Точная мензурка", desc = "мензурка ×2 идей", requires = "density", kind = PerkKind.StationMult, station = "density", value = 2, price = 1350 },
            new PerkDef { id = "flyx2", title = "Литой обод", desc = "маховик ×2", requires = "fly", kind = PerkKind.StationMult, station = "fly", value = 2, price = 12000 },
            new PerkDef { id = "fallx3", title = "Чугунные грузы", desc = "падение с полки ещё ×3", requires = "fall", kind = PerkKind.StationMult, station = "fall", value = 3, price = 90 },
            new PerkDef { id = "dynax3", title = "Нониус", desc = "динамометр ещё ×2 идей", requires = "dyna", kind = PerkKind.StationMult, station = "dyna", value = 2, price = 120 },
            new PerkDef { id = "springx3", title = "Рессора", desc = "пружина ещё ×3", requires = "spring", kind = PerkKind.StationMult, station = "spring", value = 3, price = 2520 },
            new PerkDef { id = "pendx3", title = "Хронограф", desc = "маятник ещё ×2 идей", requires = "pend", kind = PerkKind.StationMult, station = "pend", value = 2, price = 4800 },
            new PerkDef { id = "archx3", title = "Бюретка", desc = "ванна ещё ×2 идей", requires = "arch", kind = PerkKind.StationMult, station = "arch", value = 2, price = 12000 },
            new PerkDef { id = "slidex3", title = "Асфальт", desc = "работа трения ещё ×3", requires = "slide", kind = PerkKind.StationMult, station = "slide", value = 3, price = 12600 },
            // lab tricks: they hang off the letters and bend the rules a little
            new PerkDef { id = "gold1", title = "Счастливый мел", desc = "+6% к шансу золотого образца в коробке", requires = "L:g", kind = PerkKind.Flag, value = 0.06, price = 40, icon = "nugget" },
            new PerkDef { id = "hands", title = "Пульт запуска", desc = "приборы больше не срабатывают сами: расставь 2 образца и жми «Пуск» — запустятся все сразу", requires = "L:m", kind = PerkKind.Flag, value = 2, price = 400, icon = "console" },
            new PerkDef { id = "heavy", title = "Плотная партия", desc = "образцы в коробке на четверть тяжелее", requires = "L:k", kind = PerkKind.Flag, value = 1.25, price = 900, icon = "stack" },
            new PerkDef { id = "swings", title = "Долгий маятник", desc = "маятник качается на 2 периода дольше за действие", requires = "L:l", kind = PerkKind.Flag, value = 2, price = 700, icon = "pend" },
            new PerkDef { id = "gold2", title = "Золотая жила", desc = "ещё +8% к шансу золотого образца", requires = "L:rho", kind = PerkKind.Flag, value = 0.08, price = 900, icon = "nugget" },
            new PerkDef { id = "census", title = "Полная опись", desc = "+4% к итогу эксперимента за каждый изученный образец, и полностью изученный образец даёт ×1.25", requires = "L:mu", kind = PerkKind.Flag, value = 0.04, price = 2500, icon = "list" },
            new PerkDef { id = "hands2", title = "Третья рука", desc = "перед «Пуском» можно переставить 3 образца вместо 2", requires = "L:d", kind = PerkKind.Flag, value = 3, price = 3000, icon = "hand" },
            // a taste of what the next era pays with: these two are priced in coulombs, and mechanics has none
            new PerkDef { id = "teaser1", title = "Медная обмотка", desc = "маховик ещё ×4", requires = "fly", kind = PerkKind.StationMult, station = "fly", value = 4, cost = Cur.C, price = 400, icon = "coil" },
            new PerkDef { id = "teaser2", title = "Электрозатяжка", desc = "работа трения ещё ×4", requires = "friction", kind = PerkKind.StationMult, station = "slide", value = 4, cost = Cur.C, price = 300, icon = "bolt" },

            // ---------- electricity ----------
            new PerkDef { id = "box4", title = "Ящик заготовок", desc = "+3 образца в эксперименте", requires = "ohm", domain = 1, kind = PerkKind.Samples, value = 3, price = 120 },
            new PerkDef { id = "standx2", title = "Толстые провода", desc = "вся схема ×2", requires = "charge", domain = 1, kind = PerkKind.StationMult, station = "stand", value = 2, price = 400 },
            new PerkDef { id = "act5", title = "Научный грант", desc = "+3 действия за эксперимент", requires = "ohm", domain = 1, kind = PerkKind.Actions, value = 3, price = 700 },
            new PerkDef { id = "coulx2", title = "Медный анод", desc = "кулонов ×2", requires = "charge", domain = 1, kind = PerkKind.CurMult, cur = Cur.C, value = 2, price = 900, icon = "charge" },
            new PerkDef { id = "gold3", title = "Золотые контакты", desc = "+10% к шансу золотого образца", requires = "L:U", domain = 1, kind = PerkKind.Flag, value = 0.1, price = 1000, icon = "nugget" },
            new PerkDef { id = "crit", title = "Критический опыт", desc = "каждый десятый опыт срабатывает вдвое", requires = "L:t", domain = 1, kind = PerkKind.Flag, value = 2, price = 2200, icon = "bolt" },
            new PerkDef { id = "standx3", title = "Медная шина", desc = "вся схема ещё ×3", requires = "charge", domain = 1, kind = PerkKind.StationMult, station = "stand", value = 3, price = 4000 },
            new PerkDef { id = "act7", title = "Ночное дежурство", desc = "+3 действия за эксперимент", requires = "pow", domain = 1, kind = PerkKind.Actions, value = 3, price = 7000 },
            new PerkDef { id = "coulx3", title = "Электролиз", desc = "кулонов ещё ×3", requires = "cap", domain = 1, kind = PerkKind.CurMult, cur = Cur.C, value = 3, price = 12000, icon = "charge" },
            new PerkDef { id = "standx4", title = "Серебряные контакты", desc = "вся схема ещё ×3", requires = "coil", domain = 1, kind = PerkKind.StationMult, station = "stand", value = 3, price = 25000 },
            // the teaser of the steam era
            new PerkDef { id = "teaser3", title = "Паровой привод", desc = "вся схема ещё ×5", requires = "coil", domain = 1, kind = PerkKind.StationMult, station = "stand", value = 5, cost = Cur.Cal, price = 9000, icon = "heater" },

            // ---------- heat ----------
            new PerkDef { id = "box5", title = "Склад образцов", desc = "+4 образца в эксперименте", requires = "heat", domain = 2, kind = PerkKind.Samples, value = 4, price = 3000 },
            new PerkDef { id = "heatx2", title = "Мощный ТЭН", desc = "кипятильник ×2", requires = "heat", domain = 2, kind = PerkKind.StationMult, station = "heater", value = 2, price = 5000 },
            new PerkDef { id = "act6", title = "Госзаказ", desc = "+3 действия за эксперимент", requires = "heat", domain = 2, kind = PerkKind.Actions, value = 3, price = 12000 },
            new PerkDef { id = "hands3", title = "Паровой пульт", desc = "приборы больше не срабатывают сами: расставь 2 образца и жми «Пуск» — запустятся все сразу", requires = "L:c", domain = 2, kind = PerkKind.Flag, value = 2, price = 9000, icon = "console" },
            new PerkDef { id = "calmx2", title = "Двойные стенки", desc = "калориметр ×2 идей", requires = "calm", domain = 2, kind = PerkKind.StationMult, station = "calor", value = 2, price = 9000 },
            new PerkDef { id = "auto2", title = "Конвейер", desc = "автоматика срабатывает ещё +1 раз за действие", requires = "calm", domain = 2, kind = PerkKind.AutoMult, value = 1.5, price = 24000 },
            new PerkDef { id = "enginex2", title = "Двойной котёл", desc = "паровая машина ×2", requires = "carnot", domain = 2, kind = PerkKind.StationMult, station = "engine", value = 2, price = 30000 },
            new PerkDef { id = "stoke", title = "Кочегар", desc = "каждый нагрев поднимает температуру стенда вдвое сильнее", requires = "L:dT", domain = 2, kind = PerkKind.Flag, value = 2, price = 40000, icon = "heater" },
            new PerkDef { id = "census2", title = "Инвентаризация", desc = "+5% к итогу эксперимента за каждый изученный образец, и полностью изученный образец даёт ×1.25", requires = "calm", domain = 2, kind = PerkKind.Flag, value = 0.05, price = 60000, icon = "list" },
            new PerkDef { id = "hands4", title = "Механическая рука", desc = "перед «Пуском» можно переставить 3 образца вместо 2", requires = "L:Tn", domain = 2, kind = PerkKind.Flag, value = 3, price = 90000, icon = "hand" },
            new PerkDef { id = "icex2", title = "Сухой лёд", desc = "плавление ×3", requires = "ice", domain = 2, kind = PerkKind.StationMult, station = "ice", value = 3, price = 120000 },
            new PerkDef { id = "shine", title = "Золотая лихорадка", desc = "золотой образец даёт ×5 вместо ×3", requires = "L:lam", domain = 2, kind = PerkKind.Flag, value = 5, price = 150000, icon = "spark" },
            new PerkDef { id = "heatx3", title = "Спираль из нихрома", desc = "кипятильник ещё ×3", requires = "heat", domain = 2, kind = PerkKind.StationMult, station = "heater", value = 3, price = 200000 },
            new PerkDef { id = "auto3", title = "Паровой привод", desc = "автоматика срабатывает ещё +1 раз за действие", requires = "gas", domain = 2, kind = PerkKind.AutoMult, value = 1.5, price = 300000 },
            new PerkDef { id = "enginex3", title = "Пароперегреватель", desc = "паровая машина ещё ×3", requires = "carnot", domain = 2, kind = PerkKind.StationMult, station = "engine", value = 3, price = 450000 },
            new PerkDef { id = "gasx2", title = "Длинный шатун", desc = "поршень ×3", requires = "gas", domain = 2, kind = PerkKind.StationMult, station = "piston", value = 3, price = 700000 },
        };

        public static readonly RevolutionDef[] Revolutions =
        {
            new RevolutionDef { to = 1, cost = 1e7, freeLetters = new[] { "U", "R", "t" } },
            new RevolutionDef { to = 2, cost = 2e10, freeLetters = new[] { "c", "dT", "m" } },
        };

        public static readonly List<GateDef> Gates = new List<GateDef>
        {
            new GateDef { id = "final", title = "Формула всего", cost = 5e11, domain = 2,
                riddle = "Что, если сложить всё, что измерили приборы всех эпох?.. Говорят, мир тогда объяснит сам себя.",
                hint = "Ψ складывается из F, T, Fа, I, P и η — всех величин, которые измерили приборы" },
        };

        /// The map: layers of the tree. The first node of a layer sits on the trunk, the rest hang below it.
        /// "L:x" letter, "F:x" formula, "R:n" revolution, "G:x" gate. A layer appears when the previous one is done
        /// (and, after a formula, once the player has run an experiment with it). Each epoch is a tree of its own:
        /// a revolution wipes the whole board, and then every epoch reached starts again from its own root, side by side.
        static readonly string[][] TreeRaw =
        {
            // --- mechanics ---
            // a gentle start: g and m build the dynamometer, its first ideas buy h, h builds the shelf, and only
            // once the shelf has brought the first joules does the rest of the lab open up
            new[] { "L:g" },
            new[] { "L:m" },
            new[] { "F:dyna" },
            new[] { "L:h" },
            new[] { "F:fall" },
            new[] { "L:k", "L:x" },
            new[] { "F:spring" },
            new[] { "L:l" },
            new[] { "F:pend" },
            new[] { "L:rho", "L:V" },
            new[] { "F:arch" },
            new[] { "F:density" },
            new[] { "L:mu", "L:d" },
            new[] { "F:slide" },
            new[] { "F:friction" },
            new[] { "L:I", "L:w" },
            new[] { "F:fly" },
            new[] { "R:1" },
            // --- electricity: one circuit, a new socket with every theory ---
            new[] { "L:U", "L:R" },
            new[] { "L:t" },
            new[] { "F:ohm", "F:charge" },
            new[] { "F:pow", "F:lamp" },
            new[] { "L:Cc" },
            new[] { "F:cap" },
            new[] { "L:Lind" },
            new[] { "F:coil" },
            new[] { "R:2" },
            // --- heat ---
            new[] { "L:c", "L:dT" },
            new[] { "F:heat" },
            new[] { "L:Tn", "L:Tx" },
            new[] { "F:carnot", "F:engine" },
            new[] { "F:calm" },
            new[] { "L:lam" },
            new[] { "F:ice" },
            new[] { "L:p", "L:dV" },
            new[] { "F:gas" },
            new[] { "G:final" },
            new[] { "F:psi" },
        };

        public static readonly string[][] Tree;
        public static readonly bool[] IsBranch;
        /// The epoch a layer belongs to: it is on the board only while the game is in that epoch.
        public static readonly int[] LayerEra;

        static Defs()
        {
            Tree = new string[TreeRaw.Length][];
            IsBranch = new bool[TreeRaw.Length];
            LayerEra = new int[TreeRaw.Length];
            for (int k = 0; k < TreeRaw.Length; k++)
            {
                IsBranch[k] = TreeRaw[k][0].StartsWith("$");
                Tree[k] = new string[TreeRaw[k].Length];
                for (int i = 0; i < TreeRaw[k].Length; i++) Tree[k][i] = TreeRaw[k][i].TrimStart('$');
                int era = 9;
                foreach (var c in Tree[k]) era = Math.Min(era, EraOf(c));
                LayerEra[k] = era;
            }
            // what an experiment needs measured is exactly what its own formula shows: a quantity that is not
            // written in the formula never makes the result count by half
            foreach (var f in Formulas)
            {
                var need = new List<string>();
                foreach (var l in f.Slots()) if ((l == "m" || l == "V" || l == "mu") && l != f.reveals) need.Add(l);
                f.needs = need.ToArray();
            }
        }

        static int EraOf(string code)
        {
            switch (code[0])
            {
                case 'L': return L(Id(code)).domain;
                case 'F': return F(Id(code)).domain;
                case 'R': return int.Parse(Id(code)) - 1;     // a revolution closes the epoch it stands at the end of
                case 'G': return Gt(Id(code)).domain;
            }
            return 0;
        }

        public static char Kind(string code) => code[0];
        public static string Id(string code) => code.Substring(2);
        static Dictionary<string, int> _layerOf;
        /// Layer of a node code ("F:fall"), or -1.
        public static int LayerOf(string code)
        {
            if (_layerOf == null) { _layerOf = new Dictionary<string, int>(); for (int i = 0; i < Tree.Length; i++) foreach (var c in Tree[i]) _layerOf[c] = i; }
            return _layerOf.TryGetValue(code, out var k) ? k : -1;
        }

        static Dictionary<string, LetterDef> _l;
        static Dictionary<string, FormulaDef> _f;
        static Dictionary<string, DeviceDef> _d;
        static Dictionary<string, PerkDef> _p;
        static Dictionary<string, GateDef> _g;
        /// Returns null for an id that no longer exists, so a save written by an older build still loads.
        public static PerkDef Pk(string id) { if (_p == null) { _p = new Dictionary<string, PerkDef>(); foreach (var x in Perks) _p[x.id] = x; } return _p.TryGetValue(id, out var v) ? v : null; }
        public static LetterDef L(string id) { if (_l == null) { _l = new Dictionary<string, LetterDef>(); foreach (var x in Letters) _l[x.id] = x; } return _l[id]; }
        public static bool HasLetter(string id) { if (_l == null) L("m"); return _l.ContainsKey(id); }
        public static FormulaDef F(string id) { if (_f == null) { _f = new Dictionary<string, FormulaDef>(); foreach (var x in Formulas) _f[x.id] = x; } return _f[id]; }
        public static DeviceDef D(string id) { if (_d == null) { _d = new Dictionary<string, DeviceDef>(); foreach (var x in Devices) _d[x.id] = x; } return _d.TryGetValue(id, out var v) ? v : null; }
        public static GateDef Gt(string id) { if (_g == null) { _g = new Dictionary<string, GateDef>(); foreach (var x in Gates) _g[x.id] = x; } return _g[id]; }

        /// The icon of the instrument a station holds (used for the icons of its devices and perks).
        public static string StationIcon(string station)
        {
            foreach (var f in Formulas) if (f.station == station && f.icon != null) return f.icon;
            return "gear";
        }

        /// The device that automates a station, if any.
        public static DeviceDef AutoDevice(string station)
        {
            foreach (var d in Devices) if (d.automates == station) return d;
            return null;
        }

        /// How many sockets the circuit of the electric bench has: one more with every element added to it.
        public static int StandSlots(GameState g)
        {
            int n = 0;
            foreach (var f in Formulas) if (f.station == "stand" && f.sample && g.Built(f.id)) n = Math.Max(n, f.slot + 1);
            return n;
        }
    }
}
