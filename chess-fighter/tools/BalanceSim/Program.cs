using System.Collections.Concurrent;
using System.Text;
using ChessFighter.Core;

// Plays computer-vs-computer Chess Fighter games and reports what the powers and cards do.
// Usage: dotnet run -c Release --project tools/BalanceSim -- [games=200] [aiLevel=2] [unlockLevel=20] [maxPly=300] [seed=1]
int games = Arg(0, 200), aiLevel = Arg(1, 2), unlock = Arg(2, 20), maxPly = Arg(3, 300), seed0 = Arg(4, 1), ramp = Arg(5, 1), rampStart = Arg(6, 30), rampFull = Arg(7, 100), aiLevelB = Arg(8, 0);
LateRamp.StartPly = rampStart; LateRamp.FullPly = rampFull;
static int Arg(int i, int d) { var a = Environment.GetCommandLineArgs(); return a.Length > i + 1 && int.TryParse(a[i + 1], out var v) ? v : d; }

var results = new ConcurrentBag<Game>();
int done = 0;
var sw = System.Diagnostics.Stopwatch.StartNew();
Parallel.For(0, games, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
{
    results.Add(PlayOne((ulong)(seed0 + i), aiLevel, aiLevelB == 0 ? aiLevel : aiLevelB, unlock, maxPly, ramp != 0));
    int n = Interlocked.Increment(ref done);
    if (n % 20 == 0) Console.Error.WriteLine($"  {n}/{games} games, {sw.Elapsed.TotalSeconds:0}s");
});

var all = results.ToList();
var sb = new StringBuilder();
void P(string s = "") { sb.AppendLine(s); Console.WriteLine(s); }

int wWin = all.Count(g => g.Result == GameResult.WhiteWins), bWin = all.Count(g => g.Result == GameResult.BlackWins), draws = all.Count(g => g.Result != GameResult.WhiteWins && g.Result != GameResult.BlackWins);
P($"# Chess Fighter balance run");
P($"{games} games, AI level {aiLevel} ({Ai.LevelName(aiLevel)}) on both sides, unlock level {unlock}, ply limit {maxPly}, late-game ramp {(ramp != 0 ? $"on (ply {rampStart} to {rampFull})" : "off")}, {sw.Elapsed.TotalSeconds:0}s");
P();
P("## Overall");
P($"- White wins {wWin} ({Pct(wWin, games)}), Black wins {bWin} ({Pct(bWin, games)}), draws {draws} ({Pct(draws, games)})");
P($"- Average length {all.Average(g => g.Plies):0} plies (median {Median(all.Select(g => g.Plies))}), longest {all.Max(g => g.Plies)}");
foreach (var grp in all.GroupBy(g => g.Reason).OrderByDescending(g => g.Count())) P($"- Ended by {(grp.Key == "" ? "ply limit / draw" : grp.Key)}: {grp.Count()} ({Pct(grp.Count(), games)})");
P($"- Average power squares picked up per game: {all.Average(g => g.PickedW.Values.Sum() + g.PickedB.Values.Sum()):0.0}; cards played per game: {all.Average(g => g.CardsW.Values.Sum() + g.CardsB.Values.Sum()):0.0}");
P();

var decided = all.Where(g => g.Result == GameResult.WhiteWins || g.Result == GameResult.BlackWins).ToList();
P("## Powers (decided games only)");
P("Win% is the win rate of the side that collected more of that power than its opponent. 50% is neutral; the sample is the number of such games.");
P();
{
    int ahead0 = 0, won0 = 0;
    foreach (var g in decided)
    {
        int w = g.PickedW.Values.Sum(), b = g.PickedB.Values.Sum();
        if (w == b) continue;
        ahead0++;
        if ((g.Result == GameResult.WhiteWins) == (w > b)) won0++;
    }
    P($"Baseline: the side that picked up more power squares overall wins {Pct(won0, ahead0)} of the time ({ahead0} games). Compare each power with that, not with 50%.");
    P();
}
P("| Power | Games with it | Picked per game | Win% when ahead | Sample |");
P("|---|---|---|---|---|");
foreach (var k in GameConfig.FighterPool().Select(w => w.Kind))
{
    int appear = all.Count(g => g.PickedW.GetValueOrDefault(k) + g.PickedB.GetValueOrDefault(k) > 0);
    double per = all.Average(g => g.PickedW.GetValueOrDefault(k) + g.PickedB.GetValueOrDefault(k));
    int ahead = 0, won = 0;
    foreach (var g in decided)
    {
        int w = g.PickedW.GetValueOrDefault(k), b = g.PickedB.GetValueOrDefault(k);
        if (w == b) continue;
        ahead++;
        bool whiteAhead = w > b;
        if ((g.Result == GameResult.WhiteWins) == whiteAhead) won++;
    }
    P($"| {k} | {Pct(appear, games)} | {per:0.00} | {(ahead > 0 ? Pct(won, ahead) : "-")} | {ahead} |");
}
P();
P("## Cards (decided games only)");
P("Win% is the win rate of the side that played more of that card than its opponent.");
P();
P("| Card | Played per game | Win% when ahead | Sample |");
P("|---|---|---|---|");
for (int c = Cards.First; c <= Cards.Last; c++)
{
    var k = (CardKind)c;
    double per = all.Average(g => g.CardsW.GetValueOrDefault(k) + g.CardsB.GetValueOrDefault(k));
    int ahead = 0, won = 0;
    foreach (var g in decided)
    {
        int w = g.CardsW.GetValueOrDefault(k), b = g.CardsB.GetValueOrDefault(k);
        if (w == b) continue;
        ahead++;
        if ((g.Result == GameResult.WhiteWins) == (w > b)) won++;
    }
    P($"| {k} | {per:0.00} | {(ahead > 0 ? Pct(won, ahead) : "-")} | {ahead} |");
}
P();
P("## How often powers are actually used (per game)");
P($"- Fireball shots {all.Average(g => g.FireShots):0.00}, Harpoon shots {all.Average(g => g.HarpoonShots):0.00}, Stun bolts {all.Average(g => g.Zaps):0.00}, Bomb detonations {all.Average(g => g.Detonations):0.00}, Steals {all.Average(g => g.Grabs):0.00}");
P();
P("## Pieces destroyed by");
P($"- Normal captures per game: {all.Average(g => g.Captures):0.0}; fireball {all.Average(g => g.Fireball):0.00}, bombs {all.Average(g => g.Bomb):0.00}, dragon {all.Average(g => g.Dragon):0.00}, shiva {all.Average(g => g.Shiva):0.00}, missile {all.Average(g => g.Missile):0.00}, poison {all.Average(g => g.Poison):0.00}");
P($"- Games with a missile strike: {Pct(all.Count(g => g.Missile > 0 || g.Reason == "Missile strike"), games)}; ended by missile: {Pct(all.Count(g => g.Reason == "Missile strike"), games)}");

var path = Path.Combine(AppContext.BaseDirectory, "balance-report.md");
File.WriteAllText(path, sb.ToString());
Console.Error.WriteLine("Report written to " + path);

static string Pct(int a, int b) => b == 0 ? "-" : (100.0 * a / b).ToString("0") + "%";
static int Median(IEnumerable<int> v) { var l = v.OrderBy(x => x).ToList(); return l.Count == 0 ? 0 : l[l.Count / 2]; }

static Game PlayOne(ulong seed, int aiLevel, int aiLevelB, int unlock, int maxPly, bool ramp)
{
    var cfg = new GameConfig { Pool = FighterUnlocks.PoolFor(unlock), UnlockLevel = unlock, Squares = SquareMode.Revealed, PortalChance = unlock >= FighterUnlocks.TeleporterLevel ? 0.12 : 0, MaxPly = maxPly, LateRamp = ramp };
    var s = GameState.NewGame(cfg, seed);
    var ais = new[] { Ai.ForLevel(aiLevel), Ai.ForLevel(aiLevelB) }; // White plays at aiLevel, Black at aiLevelB
    var g = new Game();
    var ev = new List<GameEvent>();
    int guard = 0;
    while (Rules.GetResult(s) == GameResult.Ongoing && s.Ply < maxPly && guard++ < maxPly * 12)
    {
        var act = ais[(int)s.Decider].Choose(s);
        ev.Clear();
        Rules.Apply(s, act, ev);
        foreach (var e in ev)
        {
            var picked = e.Side == Side.White ? g.PickedW : g.PickedB;
            var cards = e.Side == Side.White ? g.CardsW : g.CardsB;
            switch (e.Kind)
            {
                case EventKind.PickupFound: picked[e.Powerup] = picked.GetValueOrDefault(e.Powerup) + 1; break;
                case EventKind.CardPlayed: cards[e.Card] = cards.GetValueOrDefault(e.Card) + 1; break;
                case EventKind.CardCancelled: cards[CardKind.NuhUh] = cards.GetValueOrDefault(CardKind.NuhUh) + 1; break;
                case EventKind.Captured: g.Captures++; break;
                case EventKind.FireballFired: g.FireShots++; break;
                case EventKind.HarpoonFired: g.HarpoonShots++; break;
                case EventKind.ZapFired: g.Zaps++; break;
                case EventKind.Detonated: g.Detonations++; break;
                case EventKind.Converted: g.Grabs++; break;
            }
        }
        // Attribute destroyed pieces to what caused them, by walking the batch in order.
        string cause = "";
        foreach (var e in ev)
        {
            switch (e.Kind)
            {
                case EventKind.FireballFired: cause = "f"; break;
                case EventKind.Detonated: cause = "b"; break;
                case EventKind.DragonFire: cause = "d"; break;
                case EventKind.ShivaStrike: cause = "s"; break;
                case EventKind.MissileStruck: cause = "m"; break;
                case EventKind.PoisonTriggered: g.Poison++; break;
                case EventKind.Captured: cause = ""; break;
                case EventKind.Destroyed:
                    if (cause == "f") g.Fireball++; else if (cause == "b") g.Bomb++; else if (cause == "d") g.Dragon++; else if (cause == "s") g.Shiva++; else if (cause == "m") g.Missile++;
                    break;
            }
        }
    }
    string reason;
    g.Result = Rules.GetResult(s, out reason);
    g.Reason = g.Result == GameResult.WhiteWins || g.Result == GameResult.BlackWins ? reason : "";
    g.Plies = s.Ply;
    return g;
}

sealed class Game
{
    public GameResult Result;
    public string Reason = "";
    public int Plies;
    public Dictionary<PowerupKind, int> PickedW = new(), PickedB = new();
    public Dictionary<CardKind, int> CardsW = new(), CardsB = new();
    public int Captures, Fireball, Bomb, Dragon, Shiva, Missile, Poison;
    public int FireShots, HarpoonShots, Zaps, Detonations, Grabs, MovesW, MovesB;
}
