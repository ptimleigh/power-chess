using PowerChess.Core;

namespace PowerChess.Web.Game;

/// <summary>Mutable stage for one looping demo: pieces, effects, highlights and a caption. Scripts drive it step by step.</summary>
public sealed class DemoCtx
{
    int nextId = 1;
    public List<PieceView> Pieces { get; } = new();
    public List<FxItem> Effects { get; } = new();
    public HashSet<int> Boulders { get; } = new();
    public HashSet<int> Mysteries { get; } = new();
    public HashSet<int> Aims { get; } = new();
    public string Caption { get; private set; } = "";
    readonly Dictionary<FxItem, long> expiry = new();
    readonly Dictionary<PieceView, long> dying = new();

    static long Now => Environment.TickCount64;

    public DemoCtx Clear()
    {
        Pieces.Clear();
        Effects.Clear();
        Boulders.Clear();
        Mysteries.Clear();
        Aims.Clear();
        expiry.Clear();
        dying.Clear();
        Caption = "";
        return this;
    }

    PieceView? At(string sq)
    {
        int s = Sq.Parse(sq);
        return Pieces.FirstOrDefault(p => !p.Dying && p.Square == s);
    }

    public DemoCtx Place(string sq, PieceType type, Side side, Buff buffs = Buff.None, int stun = 0, int armor = 0, int ghost = 0)
    {
        Pieces.Add(new PieceView { Id = nextId++, Type = type, Side = side, Square = Sq.Parse(sq), Buffs = buffs, Stun = stun, Armor = armor, Ghost = ghost });
        return this;
    }

    public DemoCtx Swap(string a, string b)
    {
        var pa = At(a);
        var pb = At(b);
        if (pa != null && pb != null)
        {
            (pa.Square, pb.Square) = (pb.Square, pa.Square);
            pa.AnimSeq++;
            pb.AnimSeq++;
        }
        return this;
    }

    public DemoCtx Move(string from, string to)
    {
        var p = At(from);
        if (p != null) { p.Square = Sq.Parse(to); p.AnimSeq++; }
        return this;
    }

    public DemoCtx Kill(string sq, bool burning = false)
    {
        var p = At(sq);
        if (p != null) { p.Dying = true; p.Burning = burning; dying[p] = Now + 800; }
        return this;
    }

    public DemoCtx Flip(string sq) { var p = At(sq); if (p != null) { p.Side = p.Side.Opponent(); p.Buffs = Buff.None; } return this; }
    public DemoCtx Become(string sq, PieceType type) { var p = At(sq); if (p != null) p.Type = type; return this; }
    public DemoCtx Stun(string sq, int turns) { var p = At(sq); if (p != null) p.Stun = turns; return this; }
    public DemoCtx Strip(string sq, Buff buff) { var p = At(sq); if (p != null) p.Buffs &= ~buff; return this; }
    public DemoCtx Boulder(string sq, bool on) { if (on) Boulders.Add(Sq.Parse(sq)); else Boulders.Remove(Sq.Parse(sq)); return this; }
    public DemoCtx Mystery(string sq, bool on) { if (on) Mysteries.Add(Sq.Parse(sq)); else Mysteries.Remove(Sq.Parse(sq)); return this; }

    public DemoCtx Aim(params string[] squares)
    {
        Aims.Clear();
        foreach (var s in squares) Aims.Add(Sq.Parse(s));
        return this;
    }

    public DemoCtx Say(string text) { Caption = text; return this; }

    public DemoCtx Fx(string kind, string sq, string to = "", int radius = 0, string css = "", int durMs = 0)
    {
        var fx = new FxItem { Id = nextId++, Kind = kind, Sq = Sq.Parse(sq), To = to == "" ? -1 : Sq.Parse(to), Radius = radius, Css = css, DurMs = durMs };
        Effects.Add(fx);
        expiry[fx] = Now + Math.Max(1100, durMs + 500);
        return this;
    }

    /// <summary>Remove expired effects and fully faded pieces. Returns true if anything changed.</summary>
    public bool Sweep()
    {
        bool changed = false;
        long now = Now;
        foreach (var kv in expiry.Where(k => k.Value <= now).ToList()) { Effects.Remove(kv.Key); expiry.Remove(kv.Key); changed = true; }
        foreach (var kv in dying.Where(k => k.Value <= now).ToList()) { Pieces.Remove(kv.Key); dying.Remove(kv.Key); changed = true; }
        return changed;
    }
}

public sealed class DemoScript
{
    public int Cols = 6;
    public Action<DemoCtx> Setup = _ => { };
    public List<(int Ms, Action<DemoCtx> Act)> Steps = new();
    public int EndPauseMs = 1800;

    public DemoScript Then(int ms, Action<DemoCtx> act) { Steps.Add((ms, act)); return this; }
}

/// <summary>One short looping animation per powerup. Squares are on a 6x6 board (a1 bottom-left, f6 top-right).</summary>
public static class Tutorials
{
    const Side W = Side.White, B = Side.Black;

    /// <summary>All Transform squares share one demo.</summary>
    public static PowerupKind Normalize(PowerupKind k) => PowerupInfo.IsTransform(k) ? PowerupKind.TransformQueen : k;

    public static bool Has(PowerupKind k) => Scripts.ContainsKey(Normalize(k));

    public static DemoScript For(PowerupKind k) => Scripts[Normalize(k)];

    static readonly Dictionary<PowerupKind, DemoScript> Scripts = new()
    {
        [PowerupKind.DoubleMove] = new DemoScript
        {
            Setup = c => c.Place("b1", PieceType.Knight, W, Buff.DoubleMove).Say("This knight has Double Move"),
        }
        .Then(900, c => c.Aim("c3").Say("Next time it moves..."))
        .Then(900, c => c.Aim().Move("b1", "c3").Strip("c3", Buff.DoubleMove).Say("...it must move twice in a row"))
        .Then(900, c => c.Aim("d5"))
        .Then(800, c => c.Aim().Move("c3", "d5").Fx("ring", "d5", css: "gold").Say("Giving check cancels the second move")),

        [PowerupKind.TripleMove] = new DemoScript
        {
            Setup = c => c.Place("b1", PieceType.Knight, W, Buff.TripleMove).Say("This knight has Triple Move"),
        }
        .Then(900, c => c.Aim("c3").Say("Next time it moves..."))
        .Then(800, c => c.Aim().Move("b1", "c3").Strip("c3", Buff.TripleMove).Say("...it must move three times in a row"))
        .Then(800, c => c.Move("c3", "d5").Fx("ring", "d5", css: "gold"))
        .Then(800, c => c.Move("d5", "f6").Fx("ring", "f6", css: "gold").Say("Giving check on any move cancels the rest")),

        [PowerupKind.Ghost] = new DemoScript
        {
            Setup = c => c.Place("a1", PieceType.Rook, W, Buff.Ghost, ghost: 3).Place("c1", PieceType.Pawn, W).Place("e1", PieceType.Pawn, B)
                .Boulder("d1", true).Say("A ghost slides through pieces and boulders"),
        }
        .Then(1000, c => c.Aim("e1"))
        .Then(700, c => c.Aim().Kill("e1").Move("a1", "e1").Fx("burst", "e1", css: "steal").Say("It captured past its own pawn and a boulder"))
        .Then(1200, c => c.Say("It stays a ghost for 3 turns")),

        [PowerupKind.Swap] = new DemoScript
        {
            Setup = c => c.Place("a1", PieceType.Rook, W).Place("d3", PieceType.Knight, W).Place("d6", PieceType.King, B).Say("Pick two of your pieces"),
        }
        .Then(900, c => c.Aim("a1"))
        .Then(900, c => c.Aim("d3"))
        .Then(800, c => c.Aim().Swap("a1", "d3").Fx("ring", "a1", css: "steal").Fx("ring", "d3", css: "steal").Say("They trade places. It costs your turn")),

        [PowerupKind.Poison] = new DemoScript
        {
            Setup = c => c.Place("a1", PieceType.Rook, W).Place("a4", PieceType.Pawn, B, Buff.Poison).Say("A poisoned pawn"),
        }
        .Then(900, c => c.Aim("a4").Say("Capture it..."))
        .Then(900, c => c.Aim().Kill("a4").Move("a1", "a4"))
        .Then(600, c => c.Fx("burst", "a4", css: "poison").Kill("a4").Say("...and the capturer dies too")),

        [PowerupKind.SpikedArmor] = new DemoScript
        {
            Setup = c => c.Place("a4", PieceType.Rook, W).Place("d4", PieceType.Rook, B, Buff.Armor, armor: 3).Say("Armored pieces can't be captured"),
        }
        .Then(900, c => c.Move("a4", "c4"))
        .Then(700, c => c.Fx("ring", "d4", radius: 0, css: "steal").Fx("ping", "d4", css: "steal").Say("Immune to captures and effects for 3 turns"))
        .Then(900, c => c.Move("c4", "a4")),

        [PowerupKind.Stun] = new DemoScript
        {
            Setup = c => c.Place("a1", PieceType.Queen, W).Place("e4", PieceType.Rook, B).Place("b5", PieceType.Pawn, B).Say("Pick any enemy piece"),
        }
        .Then(900, c => c.Aim("e4"))
        .Then(800, c => c.Aim().Fx("stars", "e4", css: "stun").Stun("e4", 3).Say("It can't move for 3 turns"))
        .Then(1200, c => c.Fx("ping", "e4", css: "steal").Say("A frozen piece can't give check either")),

        [PowerupKind.SmallBomb] = new DemoScript
        {
            Setup = c => c.Place("c3", PieceType.Knight, W, Buff.SmallBomb).Place("b4", PieceType.Pawn, B).Place("c4", PieceType.Pawn, B)
                .Place("d2", PieceType.Pawn, W).Place("d4", PieceType.King, B).Place("e5", PieceType.Pawn, B)
                .Say("A bomb piece can detonate instead of moving"),
        }
        .Then(1200, c => c.Fx("ring", "c3", radius: 1, css: "boom").Fx("flash", "c3", radius: 1, css: "boom")
            .Kill("b4").Kill("c4").Kill("d2").Strip("c3", Buff.SmallBomb)
            .Say("Everything around it is destroyed, even your own pieces. Kings survive")),

        [PowerupKind.BigBomb] = new DemoScript
        {
            Setup = c => c.Place("c3", PieceType.Rook, W, Buff.BigBomb).Place("a5", PieceType.Pawn, B).Place("e1", PieceType.Pawn, B)
                .Place("e3", PieceType.Pawn, W).Place("b4", PieceType.King, B).Place("f6", PieceType.Pawn, B)
                .Say("A Big Bomb reaches two squares out"),
        }
        .Then(1200, c => c.Fx("ring", "c3", radius: 2, css: "boom").Fx("flash", "c3", radius: 2, css: "boom")
            .Kill("a5").Kill("e1").Kill("e3").Strip("c3", Buff.BigBomb)
            .Say("Kings survive. Pieces outside the blast are safe")),

        [PowerupKind.Fireball] = new DemoScript
        {
            Setup = c => c.Place("a1", PieceType.Rook, W, Buff.Fireball).Place("a3", PieceType.Pawn, B).Place("a5", PieceType.Knight, B)
                .Say("This rook earned a fireball. Pick a direction"),
        }
        .Then(1000, c => c.Aim("a3"))
        .Then(700, c => c.Aim().Fx("fireball", "a1", "a3", css: "fire", durMs: 450).Strip("a1", Buff.Fireball).Say("It destroys the first enemy in line"))
        .Then(450, c => c.Fx("burst", "a3", css: "fire").Fx("flash", "a3", radius: 0, css: "boom").Kill("a3", burning: true))
        .Then(2200, c => c.Clear().Place("d1", PieceType.Queen, W, Buff.Fireball).Place("d2", PieceType.Pawn, W)
            .Place("d3", PieceType.Pawn, B).Place("d5", PieceType.Knight, B).Say("A Queen's fireball burns the whole line"))
        .Then(900, c => c.Aim("d3"))
        .Then(700, c => c.Aim().Fx("fireball", "d1", "d5", css: "fire", durMs: 600).Strip("d1", Buff.Fireball))
        .Then(300, c => c.Fx("burst", "d3", css: "fire").Kill("d3", burning: true))
        .Then(300, c => c.Fx("burst", "d5", css: "fire").Kill("d5", burning: true)),

        [PowerupKind.Steal] = new DemoScript
        {
            Setup = c => c.Place("a1", PieceType.Rook, W).Place("a4", PieceType.Rook, B).Say("Steal an enemy piece you could capture"),
        }
        .Then(900, c => c.Aim("a4"))
        .Then(800, c => c.Aim().Fx("beam", "a1", "a4", css: "steal").Fx("ring", "a4", radius: 0, css: "steal").Flip("a4")
            .Say("It switches sides. Your piece stays put")),

        [PowerupKind.Boulder] = new DemoScript
        {
            Setup = c => c.Place("a1", PieceType.Rook, B).Say("Drop a boulder on any empty square"),
        }
        .Then(900, c => c.Aim("d1"))
        .Then(800, c => c.Aim().Boulder("d1", true).Fx("burst", "d1", css: "rock").Say("Nothing can enter or cross it"))
        .Then(1100, c => c.Move("a1", "c1"))
        .Then(700, c => c.Fx("ping", "d1", css: "steal").Say("It blocks sliding pieces too")),

        [PowerupKind.Reroll] = new DemoScript
        {
            Setup = c => c.Place("a1", PieceType.Pawn, W).Boulder("c3", true).Say("A Reroll smashes a boulder..."),
        }
        .Then(1100, c => c.Boulder("c3", false).Fx("burst", "c3", css: "rock"))
        .Then(900, c => c.Mystery("e4", true).Say("...or re-draws a powerup you just found"))
        .Then(900, c => c.Fx("ring", "e4", css: "gold").Fx("ping", "e4", css: "gold")),

        [PowerupKind.Rewind] = new DemoScript
        {
            Setup = c => c.Place("a2", PieceType.Pawn, W).Place("b5", PieceType.Pawn, B).Say("You move, then your opponent replies"),
        }
        .Then(900, c => c.Move("a2", "a4"))
        .Then(700, c => c.Move("b5", "b4"))
        .Then(900, c => c.Fx("ring", "a4", css: "gold").Fx("ring", "b4", css: "gold").Say("Rewind takes back your turn and the reply"))
        .Then(700, c => c.Move("a4", "a2").Move("b4", "b5")),

        [PowerupKind.TransformQueen] = new DemoScript
        {
            Setup = c => c.Place("b2", PieceType.Pawn, W).Say("Some squares change a piece"),
        }
        .Then(1000, c => c.Fx("ring", "b2", css: "gold").Fx("ping", "b2", css: "gold"))
        .Then(300, c => c.Become("b2", PieceType.Queen).Say("It becomes another piece on the spot")),
    };
}
