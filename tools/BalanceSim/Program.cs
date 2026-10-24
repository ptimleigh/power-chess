using System.Text;
using PowerChess.Core;

// (optional 4th argument: spawn interval in plies, default 4; optional 5th: "revealed" or "mystery")
// Plays AI-vs-AI games with every powerup enabled and reports game length, how games end, and how each powerup
// correlates with winning. Usage: dotnet run --project tools/BalanceSim -c Release -- [games] [depth] [outFile]
//
// Reading the numbers: the AI is shallow, so absolute win rates mean little. What matters are outliers, i.e. a
// powerup whose holders win far more (or less) often than the baseline, or one that barely ever gets used.

int games = args.Length > 0 ? int.Parse(args[0]) : 300;
int depth = args.Length > 1 ? int.Parse(args[1]) : 1;
string outFile = args.Length > 2 && args[2] != "-" ? args[2] : null;
int spawn = args.Length > 3 ? int.Parse(args[3]) : 4;
bool revealed = args.Length > 4 && args[4].Equals("revealed", StringComparison.OrdinalIgnoreCase);

var kinds = Enum.GetValues(typeof(PowerupKind)).Cast<PowerupKind>().Where(k => k != PowerupKind.None && k != PowerupKind.Rewind).ToList();
var obtained = kinds.ToDictionary(k => k, _ => new Outcome());
var uses = new Dictionary<string, int>();
var plies = new List<int>();
var endings = new Dictionary<string, int>();
var overall = new Outcome();
int squaresOpened = 0;
var rnd = new Random(12345);

for (int g = 0; g < games; g++)
{
    var cfg = new GameConfig { MaxPly = 300, SpawnInterval = spawn, Squares = revealed ? SquareMode.Revealed : SquareMode.Mystery };
    cfg.RewindAllowed[0] = cfg.RewindAllowed[1] = false; // the AI cannot use Rewind
    var s = GameState.NewGame(cfg, (ulong)(1000 + g));
    var ai = new Ai(depth);
    var got = new HashSet<(Side, PowerupKind)>();
    int actions = 0;
    string reason;
    GameResult result;
    while ((result = Rules.GetResult(s, out reason)) == GameResult.Ongoing)
    {
        var legal = MoveGen.LegalActions(s);
        // A few random opening actions so the games differ; after that the AI plays both sides.
        var action = actions < 4 ? legal[rnd.Next(legal.Count)] : ai.Choose(s);
        var events = new List<GameEvent>();
        Rules.Apply(s, action, events);
        actions++;
        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case EventKind.BuffGained:
                case EventKind.PerkGained:
                case EventKind.Transformed:
                    var k = PowerupInfo.IsTransform(e.Powerup) ? e.Powerup : e.Powerup;
                    got.Add((e.Side, k));
                    break;
                case EventKind.PickupFound: squaresOpened++; break;
                case EventKind.FireballFired: Count(uses, "Fireball fired"); break;
                case EventKind.Detonated: Count(uses, e.Powerup == PowerupKind.BigBomb ? "Big Bomb detonated" : "Small Bomb detonated"); break;
                case EventKind.StunStruck: Count(uses, "Stun used"); break;
                case EventKind.Converted: Count(uses, "Steal used"); break;
                case EventKind.BoulderPlaced: Count(uses, "Boulder placed"); break;
                case EventKind.BoulderCleared: Count(uses, "Boulder cleared"); break;
                case EventKind.PoisonTriggered: Count(uses, "Poison triggered"); break;
                case EventKind.DoubleMoveForfeited: Count(uses, "Double Move cancelled by check"); break;
            }
        }
    }

    plies.Add(s.Ply);
    Count(endings, result == GameResult.Draw ? "Draw: " + reason : "Win: " + reason);
    foreach (var side in new[] { Side.White, Side.Black })
    {
        var outcome = result == GameResult.Draw ? 0 : ((result == GameResult.WhiteWins) == (side == Side.White) ? 1 : -1);
        overall.Add(outcome);
        foreach (var k in kinds)
        {
            if (got.Contains((side, k))) obtained[k].Add(outcome);
        }
    }
}

plies.Sort();
var sb = new StringBuilder();
sb.AppendLine("# Balance report");
sb.AppendLine();
sb.AppendLine($"{games} AI-vs-AI games at search depth {depth}, all powerups enabled (default pool), spawn every {spawn} plies, {(revealed ? "revealed squares (20% stay hidden)" : "mystery squares")}, 300-ply limit, 4 random opening actions.");
sb.AppendLine("The AI is shallow, so treat absolute numbers loosely and look for outliers. \"Win rate when obtained\" is how often a side won in games where it picked that powerup up at least once; it is a correlation (stronger sides also grab more squares), not proof of cause.");
sb.AppendLine();
sb.AppendLine("## Game length and endings");
sb.AppendLine();
sb.AppendLine($"- Plies per game: median {plies[plies.Count / 2]}, 25th percentile {plies[plies.Count / 4]}, 75th percentile {plies[plies.Count * 3 / 4]}, longest {plies[^1]}");
sb.AppendLine($"- Mystery squares opened per game: {(double)squaresOpened / games:0.0}");
foreach (var kv in endings.OrderByDescending(e => e.Value)) sb.AppendLine($"- {kv.Key}: {kv.Value} ({100.0 * kv.Value / games:0.0}%)");
sb.AppendLine();
sb.AppendLine("## Powerups");
sb.AppendLine();
sb.AppendLine($"Baseline for any side: win {overall.WinPct:0.0}%, draw {overall.DrawPct:0.0}%, loss {overall.LossPct:0.0}%.");
sb.AppendLine();
sb.AppendLine("| Powerup | Times a side obtained it | Win rate when obtained | Draw | Loss | Win vs baseline |");
sb.AppendLine("|---|---|---|---|---|---|");
foreach (var k in kinds.OrderByDescending(k => obtained[k].WinPct - overall.WinPct))
{
    var o = obtained[k];
    if (o.Count == 0) { sb.AppendLine($"| {k} | 0 | - | - | - | - |"); continue; }
    sb.AppendLine($"| {k} | {o.Count} | {o.WinPct:0.0}% | {o.DrawPct:0.0}% | {o.LossPct:0.0}% | {o.WinPct - overall.WinPct:+0.0;-0.0}% |");
}
sb.AppendLine();
sb.AppendLine("## Uses");
sb.AppendLine();
foreach (var kv in uses.OrderByDescending(e => e.Value)) sb.AppendLine($"- {kv.Key}: {kv.Value} total, {(double)kv.Value / games:0.00} per game");

Console.WriteLine(sb.ToString());
if (outFile != null) File.WriteAllText(outFile, sb.ToString());

static void Count(Dictionary<string, int> d, string key) => d[key] = d.GetValueOrDefault(key) + 1;

sealed class Outcome
{
    public int Count, Wins, Draws, Losses;
    public void Add(int outcome)
    {
        Count++;
        if (outcome > 0) Wins++; else if (outcome < 0) Losses++; else Draws++;
    }
    public double WinPct => Count == 0 ? 0 : 100.0 * Wins / Count;
    public double DrawPct => Count == 0 ? 0 : 100.0 * Draws / Count;
    public double LossPct => Count == 0 ? 0 : 100.0 * Losses / Count;
}
