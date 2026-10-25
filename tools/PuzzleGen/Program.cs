// Generates verified daily puzzles into Assets/Core/PuzzleData.cs.
// usage: dotnet run --project tools/PuzzleGen -c Release -- [seconds] [outFile] [perTier]
using System.Diagnostics;
using System.Text;
using PowerChess.Core;

int seconds = args.Length > 0 ? int.Parse(args[0]) : 120;
string outFile = args.Length > 1 ? args[1] : "Assets/Core/PuzzleData.cs";
int perTier = args.Length > 2 ? int.Parse(args[2]) : 14;

var found = new List<(PuzzleDef def, string kind)>();
var seen = new HashSet<string>();
var lockObj = new object();
var sw = Stopwatch.StartNew();
int tried = 0;
int[] tierCount = new int[4];

var kinds = new[]
{
    "fireball", "fireball", "stun", "stun", "steal", "boulder", "swap", "smallbomb", "bigbomb", "ghost", "doublemove", "triplemove",
};

string[] Titles(string kind)
{
    switch (kind)
    {
        case "fireball": return new[] { "Trial by Fire", "Burn the Wall", "Dragon's Breath", "Ember Strike" };
        case "stun": return new[] { "Freeze Frame", "Cold Shoulder", "Stop Right There", "Statue Maker" };
        case "steal": return new[] { "Sticky Fingers", "Turncoat", "Changing Sides", "Grand Larceny" };
        case "boulder": return new[] { "Rock Solid", "Dead End", "Stone Cold", "Landslide" };
        case "swap": return new[] { "Switcheroo", "Musical Chairs", "Quick Change", "Bait and Swap" };
        case "smallbomb": return new[] { "Small Blast", "Light the Fuse", "Boom Room", "Spring Cleaning" };
        case "bigbomb": return new[] { "Big Bang", "Clear the Board", "Fallout", "Scorched Earth" };
        case "ghost": return new[] { "Phantom Menace", "Walk Through Walls", "Poltergeist", "Haunted" };
        case "doublemove": return new[] { "Two Steps Ahead", "Double Trouble", "Back to Back", "Twice as Nice" };
        default: return new[] { "Hat Trick", "Triple Threat", "Three's Company", "Third Time Lucky" };
    }
}

string Hint(string kind)
{
    switch (kind)
    {
        case "fireball": return "Tap the glowing piece, press Fireball, then pick a direction.";
        case "stun": return "Use Stun on an enemy piece, then finish the job.";
        case "steal": return "Steal an enemy piece you could capture. It becomes yours.";
        case "boulder": return "A boulder blocks a square: nothing can enter or cross it.";
        case "swap": return "Swap trades two of your pieces. It costs your turn.";
        case "smallbomb": return "The bomber can detonate instead of moving. Kings survive blasts.";
        case "bigbomb": return "A big bomb clears a 5x5 area. Mind your own pieces.";
        case "ghost": return "A ghost slides through pieces and boulders.";
        case "doublemove": return "The buffed piece must move twice in a row.";
        default: return "The buffed piece must move three times in a row.";
    }
}

GameState Build(Random rnd, out string perks, out string buffs, out string kind, out string fen)
{
    perks = ""; buffs = ""; fen = "";
    kind = kinds[rnd.Next(kinds.Length)];
    var cells = new char[64];
    for (int i = 0; i < 64; i++) cells[i] = '.';
    bool Free(int sq) => cells[sq] == '.';
    int Rand(int n) => rnd.Next(n);

    // black king biased to the edge
    int bk = rnd.NextDouble() < 0.75 ? EdgeSquare(rnd) : Rand(64);
    cells[bk] = 'k';
    int wk;
    int guard = 0;
    do { wk = Rand(64); guard++; } while ((Chebyshev(wk, bk) <= 1 || !Free(wk)) && guard < 200);
    if (guard >= 200) return null;
    cells[wk] = 'K';

    int whiteCount = 1 + Rand(3);
    var whiteSquares = new List<int>();
    string wp = "QRRBNQRBN";
    for (int i = 0; i < whiteCount; i++)
    {
        int sq = Rand(64);
        if (!Free(sq)) continue;
        char t = wp[Rand(wp.Length)];
        cells[sq] = t;
        whiteSquares.Add(sq);
    }
    if (whiteSquares.Count == 0) return null;
    if (rnd.NextDouble() < 0.4)
    {
        int sq = Rand(64);
        int rank = sq / 8;
        if (Free(sq) && rank >= 1 && rank <= 5) { cells[sq] = 'P'; }
    }
    int blackCount = Rand(4);
    string bp = "ppprnb";
    for (int i = 0; i < blackCount; i++)
    {
        // clustered near the black king
        int f = bk % 8 + Rand(5) - 2, r = bk / 8 + Rand(5) - 2;
        if (f < 0 || f > 7 || r < 0 || r > 7) continue;
        int sq = r * 8 + f;
        char t = bp[Rand(bp.Length)];
        if (!Free(sq)) continue;
        if (t == 'p' && (r == 0 || r == 7)) continue;
        cells[sq] = t;
    }
    var sb = new StringBuilder();
    for (int rank = 7; rank >= 0; rank--)
    {
        int empty = 0;
        for (int file = 0; file < 8; file++)
        {
            char c = cells[rank * 8 + file];
            if (c == '.') { empty++; continue; }
            if (empty > 0) { sb.Append(empty); empty = 0; }
            sb.Append(c);
        }
        if (empty > 0) sb.Append(empty);
        if (rank > 0) sb.Append('/');
    }
    fen = sb.ToString() + " w - - 0 1";

    var pieces = new List<int>();
    for (int i = 0; i < 64; i++) if (char.IsUpper(cells[i]) && cells[i] != 'K') pieces.Add(i);
    if (pieces.Count == 0) return null;
    int target = pieces[Rand(pieces.Count)];
    switch (kind)
    {
        case "stun": perks = "stun:1"; break;
        case "steal": perks = "steal:1"; break;
        case "boulder": perks = "boulder:1"; break;
        case "swap": perks = "swap:1"; break;
        case "ghost":
            if ("RBQ".IndexOf(cells[target]) < 0) return null;
            buffs = Sq.Name(target) + ":ghost"; break;
        default: buffs = Sq.Name(target) + ":" + kind; break;
    }
    var def = new PuzzleDef("x", "x", "x", 1, 1, fen, perks, buffs, "");
    var s = def.NewState();
    return s;
}

static int Chebyshev(int a, int b) => Math.Max(Math.Abs(a % 8 - b % 8), Math.Abs(a / 8 - b / 8));
static int EdgeSquare(Random rnd)
{
    int side = rnd.Next(4), i = rnd.Next(8);
    return side switch { 0 => i, 1 => 56 + i, 2 => i * 8, _ => i * 8 + 7 };
}

void Worker(int seed)
{
    var rnd = new Random(seed);
    while (sw.Elapsed.TotalSeconds < seconds)
    {
        lock (lockObj) { if (tierCount[1] >= perTier && tierCount[2] >= perTier && tierCount[3] >= perTier) return; }
        var s = Build(rnd, out var perks, out var buffs, out var kind, out var fen);
        Interlocked.Increment(ref tried);
        if (s == null) continue;
        if (Geometry.IsKingAttacked(s, Side.Black)) continue;
        if (Geometry.IsKingAttacked(s, Side.White)) continue;
        if (MoveGen.LegalActions(s).Count == 0) continue;
        var bs = s.Clone(); bs.ToMove = Side.Black;
        if (MoveGen.LegalActions(bs).Count == 0) continue; // already stalemate-ish

        int m = 0;
        GameAction first;
        PuzzleSolver.NodeLimit = 400000;
        for (int budget = 1; budget <= 3 && m == 0; budget++)
        {
            PuzzleSolver.NodeLimit = budget == 3 ? 3000000 : 400000;
            if (PuzzleSolver.FindSolution(s, budget, out first)) m = budget;
            if (PuzzleSolver.Nodes > PuzzleSolver.NodeLimit) break;
        }
        PuzzleSolver.NodeLimit = long.MaxValue;
        if (m == 0) continue;
        var def0 = new PuzzleDef("x", "x", "x", 1, m, fen, perks, buffs, "");
        // the powerup must be what makes it work
        var plain = def0.NewStateWithoutPowerups();
        PuzzleSolver.NodeLimit = 3000000;
        bool easy = PuzzleSolver.CanForceMate(plain, m);
        PuzzleSolver.NodeLimit = long.MaxValue;
        if (easy) continue;
        // and no shorter solution exists
        if (m > 1 && PuzzleSolver.CanForceMate(def0.NewState(), m - 1)) continue;

        int tier = Math.Min(m, 3);
        lock (lockObj)
        {
            if (tierCount[tier] >= perTier || !seen.Add(fen)) continue;
            tierCount[tier]++;
            var t = Titles(kind);
            string title = t[(tierCount[tier] + kind.Length) % t.Length];
            string id = $"d{tier}-{tierCount[tier]}";
            found.Add((new PuzzleDef(id, title, Hint(kind), tier, m, fen, perks, buffs, ""), kind));
            Console.WriteLine($"[{sw.Elapsed.TotalSeconds:0}s] tier {tier} #{tierCount[tier]} {kind} mate in {m}: {fen} {perks} {buffs}  (tried {tried})");
        }
    }
}

var threads = Enumerable.Range(0, Math.Max(2, Environment.ProcessorCount)).Select(i => new Thread(() => Worker(1000 + i * 7919))).ToList();
threads.ForEach(t => t.Start());
threads.ForEach(t => t.Join());

found.Sort((a, b) => a.def.Tier != b.def.Tier ? a.def.Tier.CompareTo(b.def.Tier) : string.CompareOrdinal(a.def.Id, b.def.Id));
var o = new StringBuilder();
o.AppendLine("namespace PowerChess.Core");
o.AppendLine("{");
o.AppendLine("    /// <summary>Generated by tools/PuzzleGen. Every puzzle is verified: a forced mate within MaxMoves exists, none within one fewer, and none without the powerups.</summary>");
o.AppendLine("    public static class PuzzleData");
o.AppendLine("    {");
o.AppendLine("        public static readonly PuzzleDef[] All =");
o.AppendLine("        {");
foreach (var (d, _) in found)
    o.AppendLine($"            new PuzzleDef(\"{d.Id}\", \"{d.Title}\", \"{d.Hint}\", {d.Tier}, {d.MaxMoves}, \"{d.Fen}\", \"{d.Perks}\", \"{d.Buffs}\", \"{d.Boulders}\"),");
o.AppendLine("        };");
o.AppendLine("    }");
o.AppendLine("}");
File.WriteAllText(outFile, o.ToString());
Console.WriteLine($"wrote {found.Count} puzzles ({tierCount[1]}/{tierCount[2]}/{tierCount[3]}) to {outFile}; tried {tried} positions in {sw.Elapsed.TotalSeconds:0}s");
