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
    /// <summary>Revealed shows the powerup on each square (an occasional one stays a mystery); Mystery hides every one.</summary>
    public SquareMode Mode = SquareMode.Revealed;
    /// <summary>In Revealed mode, how often a spawned pair stays a hidden mystery.</summary>
    public double HiddenChance = 0.2;
    /// <summary>Squares to show as revealed, e.g. <c>reveal=d4:fireball,e5:steal</c>.</summary>
    public List<(string sq, PowerupKind kind)> Reveal = new();
    /// <summary>Teleporter pairs can spawn (<c>portals=0</c> turns them off).</summary>
    public bool Portals = true;
    /// <summary>Strike drama: dash streaks, slashes, hit-stop, callouts and bigger shakes on captures (<c>drama=0</c> turns it off).</summary>
    public bool Drama = true;
    /// <summary>Account level that decides which powerups can appear. -1 = everything unlocked (scenarios and tests).</summary>
    public int Level = -1;
    /// <summary>Override today's day number (<c>day=20000</c>), for testing the daily challenge.</summary>
    public int? Day;
    /// <summary>Play this daily puzzle instead of a normal game.</summary>
    public PuzzleDef? Puzzle;
    /// <summary>Pay XP and trophies even in a scenario (<c>rewards=1</c>), so progression can be tested.</summary>
    public bool Rewards;
    /// <summary>Teleporter squares to place now, e.g. <c>portal=c3</c> (its twin is the point mirror, f6).</summary>
    public List<string> PortalAt = new();
    /// <summary>Custom powerup rules: a preset id ("classic", "bigbombs", "custom", ...), how often squares appear, and the builder string.</summary>
    public string Preset = Presets.Classic;
    public Frequency Freq = Frequency.Normal;
    public string Custom = Presets.DefaultCustom();

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
                case "ai": if (int.TryParse(val, out var ai)) o.Ai = Math.Clamp(ai, 0, 5); break;
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
                case "day": if (int.TryParse(val, out var dn)) o.Day = dn; break;
                case "rewards": o.Rewards = val == "1"; break;
                case "level": if (int.TryParse(val, out var lv)) o.Level = Math.Clamp(lv, 1, Progression.MaxLevel); break;
                case "preset": o.Preset = val.ToLowerInvariant(); break;
                case "freq": if (int.TryParse(val, out var fq)) o.Freq = (Frequency)Math.Clamp(fq, 0, 3); break;
                case "custom": o.Custom = val; break;
                case "drama": o.Drama = val != "0"; break;
                case "portals": o.Portals = val != "0"; break;
                case "portal": o.PortalAt.AddRange(val.Split(',', StringSplitOptions.RemoveEmptyEntries)); break;
                case "mode": o.Mode = val.Equals("mystery", StringComparison.OrdinalIgnoreCase) ? SquareMode.Mystery : SquareMode.Revealed; break;
                case "hidden": if (double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hc)) o.HiddenChance = Math.Clamp(hc, 0, 1); break;
                case "reveal":
                    foreach (var item in val.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var f = item.Split(':');
                        if (f.Length == 2 && Enum.TryParse<PowerupKind>(f[1], true, out var rk)) o.Reveal.Add((f[0], rk));
                    }
                    break;
            }
        }
        return o;
    }
}
