using PowerChess.Core;

namespace PowerChess.Web.Game;

/// <summary>Which powerup tutorials the player has seen, and whether they switched them all off. Stored in localStorage.</summary>
public sealed class TutorialState
{
    public bool Off { get; set; }
    public HashSet<PowerupKind> Seen { get; } = new();

    public bool ShouldShow(PowerupKind kind)
    {
        return !Off && Tutorials.Has(kind) && !Seen.Contains(Tutorials.Normalize(kind));
    }

    public void MarkSeen(PowerupKind kind) => Seen.Add(Tutorials.Normalize(kind));

    public string Serialize() => (Off ? "1" : "0") + ":" + string.Join(",", Seen.Select(k => (int)k));

    public static TutorialState Parse(string? text)
    {
        var s = new TutorialState();
        if (string.IsNullOrEmpty(text)) return s;
        var parts = text.Split(':');
        s.Off = parts[0] == "1";
        if (parts.Length > 1)
        {
            foreach (var n in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(n, out var v) && v > 0 && v < 32) s.Seen.Add((PowerupKind)v);
        }
        return s;
    }

    public void Reset()
    {
        Off = false;
        Seen.Clear();
    }
}
