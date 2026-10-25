using PowerChess.Core;

// Plays each adjacent pair of AI levels against each other on plain chess (no powerups), colours swapped, and reports the score.
// usage: dotnet run --project tools/LadderSim -c Release -- [gamesPerPair] [timeScale] [outFile] [firstLowLevel] [lastLowLevel]
// timeScale shrinks the Expert/Master time budgets (default 0.1) so a run finishes in minutes; the order of strength is what matters.
int games = args.Length > 0 ? int.Parse(args[0]) : 16;
double scale = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 0.1;
string outFile = args.Length > 2 ? args[2] : null;
var rnd = new Random(2024);
var lines = new List<string>();

Ai Make(int level)
{
    var ai = Ai.ForLevel(level);
    if (ai.TimeMs > 0) ai.TimeMs = Math.Max(50, (int)(ai.TimeMs * scale));
    return ai;
}

int first = args.Length > 3 ? int.Parse(args[3]) : 1, last = args.Length > 4 ? int.Parse(args[4]) : 4;
for (int low = first; low <= last; low++)
{
    int high = low + 1;
    double score = 0; int wins = 0, draws = 0, losses = 0;
    for (int g = 0; g < games; g++)
    {
        bool highIsWhite = g % 2 == 0;
        var s = GameState.NewGame(GameConfig.Classic(), (ulong)(500 + g / 2));
        // the same random opening for both colour assignments of a pair of games
        var open = new Random(900 + g / 2);
        for (int i = 0; i < 4; i++)
        {
            var legal = MoveGen.LegalActions(s);
            Rules.Apply(s, legal[open.Next(legal.Count)]);
        }
        var aiWhite = Make(highIsWhite ? high : low);
        var aiBlack = Make(highIsWhite ? low : high);
        GameResult result;
        while ((result = Rules.GetResult(s)) == GameResult.Ongoing && s.Ply < 200)
        {
            var a = (s.ToMove == Side.White ? aiWhite : aiBlack).Choose(s);
            Rules.Apply(s, a);
        }
        double r = result == GameResult.Draw || result == GameResult.Ongoing ? 0.5
            : (result == GameResult.WhiteWins) == highIsWhite ? 1 : 0;
        if (r == 1) wins++; else if (r == 0.5) draws++; else losses++;
        score += r;
    }
    string line = $"{Ai.LevelName(high)} vs {Ai.LevelName(low)}: {wins} wins, {draws} draws, {losses} losses ({score}/{games})";
    Console.WriteLine(line);
    lines.Add(line);
}
if (outFile != null) File.WriteAllLines(outFile, lines);
