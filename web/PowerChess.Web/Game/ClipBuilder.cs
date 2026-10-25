using System.Text.Json;
using PowerChess.Core;

namespace PowerChess.Web.Game;

/// <summary>
/// Turns the tail of a game's history into a replay "clip" for the canvas renderer (<c>js/clip.js</c>): the position the clip
/// starts from, then one beat per turn with the engine's events and the position after it. The renderer never runs chess
/// rules, it only animates what the engine already decided, so the clip can't drift from the real game.
/// </summary>
public static class ClipBuilder
{
    public static int Available(GameHistory history) => ReplayPlan.Available(history);

    public static string Build(GameHistory history, GameState finalState, int plies, string resultReason, GameResult result, bool vsAi, string title)
    {
        var starts = ReplayPlan.PlyStarts(history);
        plies = ReplayPlan.Clamp(history, plies);
        int firstPly = starts.Count - plies;
        int from = starts.Count == 0 ? 0 : starts[firstPly];

        var beats = new List<object>();
        GameState? start = history.Count > from ? history[from].Before : finalState;
        for (int g = firstPly; g < starts.Count; g++)
        {
            int a = starts[g];
            int b = g + 1 < starts.Count ? starts[g + 1] : history.Count;
            var s = history[a].Before.Clone();
            var events = new List<object>();
            var label = new List<string>();
            Side mover = history[a].Mover;
            for (int i = a; i < b; i++)
            {
                var ev = new List<GameEvent>();
                Rules.Apply(s, history[i].Action, ev);
                foreach (var e in ev) events.Add(new { k = e.Kind.ToString(), f = e.From, t = e.To, p = (int)e.Piece, s = (int)e.Side, u = (int)e.Powerup });
                label.Add(Describe(history[i], vsAi));
            }
            string reason;
            var res = Rules.GetResult(s, out reason);
            bool check = Geometry.IsKingAttacked(s, s.ToMove);
            beats.Add(new
            {
                mover = mover == Side.White ? "w" : "b",
                label = string.Join(" + ", label),
                ev = events,
                after = Snapshot(s),
                check = check && res == GameResult.Ongoing,
                mate = res != GameResult.Ongoing && res != GameResult.Draw,
            });
        }

        var clip = new
        {
            v = 1,
            powerups = Enum.GetValues<PowerupKind>().ToDictionary(k => (int)k, k => Names.Name(k)),
            title,
            plies,
            vsAi,
            result = result switch { GameResult.WhiteWins => "w", GameResult.BlackWins => "b", GameResult.Draw => "d", _ => "" },
            reason = resultReason,
            start = Snapshot(start!),
            beats,
        };
        return JsonSerializer.Serialize(clip);
    }

    static string Describe(GameHistory.Entry e, bool vsAi)
    {
        var a = e.Action;
        return a.Kind switch
        {
            ActionKind.Move => $"{e.Before.Cells[a.From].Type} {Sq.Name(a.From)} to {Sq.Name(a.To)}",
            ActionKind.Fireball => "Fireball",
            ActionKind.Stun => "Stun",
            ActionKind.Steal => "Steal",
            ActionKind.Swap => "Swap",
            ActionKind.PlaceBoulder => "Boulder",
            ActionKind.ClearBoulder => "Smashed boulder",
            ActionKind.Detonate => "Bomb",
            ActionKind.KeepPickup => "Kept it",
            ActionKind.RerollPickup => "Rerolled",
            _ => a.Kind.ToString(),
        };
    }

    static object Snapshot(GameState s)
    {
        var pieces = new List<int[]>();
        var boulders = new List<int>();
        var squares = new List<int[]>();
        var portals = new List<int>();
        for (int sq = 0; sq < 64; sq++)
        {
            var c = s.Cells[sq];
            if (!c.IsEmpty) pieces.Add(new[] { sq, (int)c.Type, (int)c.Side, (int)c.Buffs, c.ArmorTurns, c.StunTurns, c.GhostTurns });
            if (s.Boulders[sq]) boulders.Add(sq);
            if (s.Mystery[sq]) squares.Add(new[] { sq, s.Content[sq] });
            if (s.Portal[sq]) portals.Add(sq);
        }
        return new { p = pieces, b = boulders, m = squares, g = portals };
    }
}
