using PowerChess.Core;

namespace PowerChess.Web.Game;

/// <summary>
/// Start-up options read from the URL query string, so a specific situation can be reproduced for testing, e.g.
/// <c>?seed=7&amp;ai=0&amp;fen=...&amp;perks=w:fireball:2&amp;buffs=e4:smallbomb&amp;mystery=d4,e5</c>.
/// </summary>
public sealed class GameOptions
{
    public ulong Seed = (ulong)DateTime.UtcNow.Ticks;
    /// <summary>0 = hot-seat (no AI), 1 = easy, 2 = normal, 3 = hard.</summary>
    public int Ai = 2;
    public string? Fen;
    public List<PowerupKind> Only = new();
    public int Pairs = -1;
    public int Spawn = -1;
    public int Rerolls = 1;
    public List<(Side side, PowerupKind kind, int n)> Perks = new();
    public List<(string sq, PowerupKind kind)> Buffs = new();
    public List<string> Mystery = new();
    /// <summary>Show powerup tutorials the first time each is selected.</summary>
    public bool Tutorials = true;
    /// <summary>The URL asked for tutorials explicitly (<c>tut=1</c>), even alongside scenario options.</summary>
    public bool TutorialsExplicit;
    /// <summary>Open this powerup's tutorial straight away (<c>demo=fireball</c>).</summary>
    public PowerupKind? Demo;
    /// <summary>Start on the small training board (one-shot; cleared once the game is created).</summary>
    public bool Training;

    public static GameOptions FromQuery(string query)
    {
        var o = new GameOptions();
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            string key = kv[0].ToLowerInvariant();
            string val = Uri.UnescapeDataString((kv.Length > 1 ? kv[1] : "").Replace('+', ' '));
            switch (key)
            {
                case "seed": if (ulong.TryParse(val, out var seed)) o.Seed = seed; break;
                case "ai": if (int.TryParse(val, out var ai)) o.Ai = Math.Clamp(ai, 0, 3); break;
                case "fen": o.Fen = val; break;
                case "pairs": if (int.TryParse(val, out var pairs)) o.Pairs = pairs; break;
                case "spawn": if (int.TryParse(val, out var spawn)) o.Spawn = spawn; break;
                case "rerolls": if (int.TryParse(val, out var rr)) o.Rerolls = rr; break;
                case "only":
                    foreach (var name in val.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        if (Enum.TryParse<PowerupKind>(name, true, out var k)) o.Only.Add(k);
                    break;
                case "perks": // w:fireball:2,b:steal
                    foreach (var item in val.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var f = item.Split(':');
                        if (f.Length >= 2 && Enum.TryParse<PowerupKind>(f[1], true, out var pk))
                            o.Perks.Add((f[0].StartsWith("b") ? Side.Black : Side.White, pk, f.Length > 2 && int.TryParse(f[2], out var n) ? n : 1));
                    }
                    break;
                case "buffs": // e4:smallbomb,d4:poison
                    foreach (var item in val.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var f = item.Split(':');
                        if (f.Length == 2 && Enum.TryParse<PowerupKind>(f[1], true, out var bk)) o.Buffs.Add((f[0], bk));
                    }
                    break;
                case "mystery": o.Mystery.AddRange(val.Split(',', StringSplitOptions.RemoveEmptyEntries)); break;
                case "tut": o.Tutorials = val != "0"; o.TutorialsExplicit = val == "1"; break;
                case "demo": if (Enum.TryParse<PowerupKind>(val, true, out var dk)) o.Demo = dk; break;
                case "training": o.Training = val != "0"; break;
            }
        }
        return o;
    }
}
