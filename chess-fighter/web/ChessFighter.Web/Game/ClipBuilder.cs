using System.Text.Json;
using ChessFighter.Core;

namespace ChessFighter.Web.Game;

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
                foreach (var e in ev) events.Add(new { k = e.Kind.ToString(), f = e.From, t = e.To, p = (int)e.Piece, s = (int)e.Side, u = (int)e.Powerup, c = (int)e.Card });
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
            v = 2,
            powerups = Enum.GetValues<PowerupKind>().ToDictionary(k => (int)k, k => Names.Name(k)),
            glyphs = Enum.GetValues<PowerupKind>().ToDictionary(k => (int)k, k => Glyph(k)),
            // the same icons the game board draws, as SVG; the replay paints them onto its canvas (? and +2/+4 stay text)
            icons = Enum.GetValues<PowerupKind>().Where(k => k != PowerupKind.None && k != PowerupKind.Combo2 && k != PowerupKind.Combo4)
                .ToDictionary(k => (int)k, k => Icons.Svg(Names.Icon(k)).Value),
            cards = Enum.GetValues<CardKind>().Where(k => k != CardKind.None).ToDictionary(k => (int)k, k => Names.CardName(k)),
            sprites = new[] { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King }
                .ToDictionary(t => (int)t, t => Sprites.Rows(t)),
            art = Sprites.HasArt ? Sprites.ArtForClip() : null,
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
            ActionKind.Harpoon => "Harpoon",
            ActionKind.Stun => "Stun",
            ActionKind.Steal => "Steal",
            ActionKind.Swap => "Swap",
            ActionKind.PlaceBoulder => "Boulder",
            ActionKind.ClearBoulder => "Smashed boulder",
            ActionKind.Detonate => "Bomb",
            ActionKind.KeepPickup => "Kept it",
            ActionKind.RerollPickup => "Rerolled",
            ActionKind.StunBeam => "Stun bolt",
            ActionKind.StealPick => "Steal",
            ActionKind.PlayCard => Names.CardName(Cards.KindOf(a.Aux)),
            ActionKind.UseNuhUh => "Nuh-uh!",
            ActionKind.AllowCard => "Let it through",
            _ => a.Kind.ToString(),
        };
    }

    /// <summary>Short label drawn on a powerup square in the clip.</summary>
    static string Glyph(PowerupKind k) => k switch
    {
        PowerupKind.None => "?",
        PowerupKind.Fireball => "FIRE",
        PowerupKind.Harpoon => "HOOK",
        PowerupKind.SmallBomb or PowerupKind.BigBomb => "BOMB",
        PowerupKind.Poison => "POIS",
        PowerupKind.Ghost => "GHOST",
        PowerupKind.Combo2 => "+2",
        PowerupKind.Combo4 => "+4",
        PowerupKind.StunBolt or PowerupKind.Stun => "ZAP",
        PowerupKind.StealGrab or PowerupKind.Steal => "STEAL",
        PowerupKind.DrawCard => "CARD",
        PowerupKind.SpikedArmor => "ARMOR",
        _ => Names.Name(k).ToUpperInvariant(),
    };

    /// <summary>Share of its starting material (39 points) a side still has, as a percent: the fight bar. The loser of a finished game is empty.</summary>
    static int Hp(GameState s, Side side, GameResult result)
    {
        if (result == GameResult.WhiteWins && side == Side.Black) return 0;
        if (result == GameResult.BlackWins && side == Side.White) return 0;
        int total = 0;
        for (int i = 0; i < 64; i++)
        {
            var p = s.Cells[i];
            if (p.IsEmpty || p.Side != side) continue;
            total += p.Type switch { PieceType.Pawn => 1, PieceType.Knight => 3, PieceType.Bishop => 3, PieceType.Rook => 5, PieceType.Queen => 9, _ => 0 };
        }
        return Math.Clamp(total * 100 / 39, 0, 100);
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
            if (!c.IsEmpty) pieces.Add(new[] { sq, (int)c.Type, (int)c.Side, (int)c.Buffs, c.ArmorTurns, c.StunTurns, c.GhostTurns, c.Tier });
            if (s.Boulders[sq]) boulders.Add(sq);
            if (s.Mystery[sq]) squares.Add(new[] { sq, s.Content[sq] });
            if (s.Portal[sq]) portals.Add(sq);
        }
        string why;
        var res = Rules.GetResult(s, out why);
        return new { p = pieces, b = boulders, m = squares, g = portals, ml = s.MissileLeft, ma = s.MissileAnchor, hp = new[] { Hp(s, Side.White, res), Hp(s, Side.Black, res) } };
    }
}
