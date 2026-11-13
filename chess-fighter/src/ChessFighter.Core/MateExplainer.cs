using System.Collections.Generic;

namespace ChessFighter.Core
{
    /// <summary>Why one way out of a checkmate fails.</summary>
    public enum EscapeKind
    {
        /// <summary>The king steps to a safe-looking square, but it is attacked there.</summary>
        KingStep,
        /// <summary>A piece takes the piece giving check, but that leaves the king attacked (a pin, a poison blast, a second attacker).</summary>
        Capture,
        /// <summary>Another piece moves, but the king stays attacked.</summary>
        Other,
        /// <summary>A rule forbids it: a king cannot capture a poisoned piece.</summary>
        KingCannotTakePoison,
        /// <summary>A rule forbids it: an armored piece cannot be captured.</summary>
        CannotTakeArmored,
        /// <summary>The only piece that could take the checker is frozen.</summary>
        Frozen,
    }

    public sealed class Escape
    {
        public EscapeKind Kind;
        /// <summary>The attempted action (for the rule kinds, From is the piece that is not allowed to act and To the target).</summary>
        public GameAction Try;
        public int From, To;
        public PieceType MoverType;
        /// <summary>The piece that gives check and the square it stands on.</summary>
        public int CheckerSquare = -1;
        /// <summary>Events produced by the attempt, for animation (a poisoned capture kills the capturer too).</summary>
        public List<GameEvent> TryEvents = new List<GameEvent>();
        /// <summary>After the attempt: the rival piece that takes the king, where it stands now, and where the king is.</summary>
        public int AttackerSquare = -1, KingSquare = -1;
        public PieceType AttackerType;
    }

    /// <summary>
    /// For a position where the side to move is checkmated: the escapes the player might expect to have, each with the reason it fails, so the
    /// game can show the refutation instead of describing it.
    /// </summary>
    public static class MateExplainer
    {
        public static List<Escape> Explain(GameState mated, int max = 4)
        {
            var result = new List<Escape>();
            Side me = mated.ToMove;
            int king = Geometry.FindKing(mated, me);
            if (king < 0) return result;

            int checker = FindAttacker(mated, king, me.Opponent());
            var all = new List<Escape>();

            // Rule-based blocks first: they are the surprising ones.
            if (checker >= 0)
            {
                var ch = mated.Cells[checker];
                if (ch.Has(Buff.Poison) && Adjacent(king, checker))
                    all.Add(new Escape { Kind = EscapeKind.KingCannotTakePoison, From = king, To = checker, MoverType = PieceType.King, CheckerSquare = checker, KingSquare = king, AttackerSquare = checker, AttackerType = ch.Type });
                if (ch.Has(Buff.Armor))
                    all.Add(new Escape { Kind = EscapeKind.CannotTakeArmored, From = -1, To = checker, MoverType = PieceType.None, CheckerSquare = checker, KingSquare = king, AttackerSquare = checker, AttackerType = ch.Type });
                for (int sq = 0; sq < 64; sq++)
                {
                    var p = mated.Cells[sq];
                    if (p.IsEmpty || p.Side != me || !p.IsStunned) continue;
                    var c = mated.Clone();
                    var q = c.Cells[sq];
                    q.StunTurns = 0;
                    c.Cells[sq] = q;
                    if (MoveGen.HasPseudoMove(c, sq, checker) && MoveGen.IsLegal(c, GameAction.Move(sq, checker)))
                        all.Add(new Escape { Kind = EscapeKind.Frozen, From = sq, To = checker, MoverType = p.Type, CheckerSquare = checker, KingSquare = king, AttackerSquare = checker, AttackerType = ch.Type });
                }
            }

            // Then every ordinary try, with the reply that wins.
            foreach (var a in MoveGen.PseudoActions(mated))
            {
                if (a.Kind != ActionKind.Move && a.Kind != ActionKind.Fireball && a.Kind != ActionKind.Leap && a.Kind != ActionKind.Detonate) continue;
                var c = mated.Clone();
                c.SearchMode = true;
                c.Probe = true;
                var ev = new List<GameEvent>();
                Rules.Apply(c, a, ev);
                if (!Geometry.IsKingAttacked(c, me)) continue; // not a failed escape (should not happen in a mate)
                int k2 = Geometry.FindKing(c, me);
                int att = k2 >= 0 ? FindAttacker(c, k2, me.Opponent()) : -1;
                var mover = mated.Cells[a.From];
                var kind = mover.Type == PieceType.King && a.Kind == ActionKind.Move ? EscapeKind.KingStep
                         : (a.To == checker && a.Kind == ActionKind.Move) ? EscapeKind.Capture : EscapeKind.Other;
                if (a.Kind == ActionKind.Fireball || a.Kind == ActionKind.Leap || a.Kind == ActionKind.Detonate)
                    kind = a.To == checker || a.Kind == ActionKind.Detonate ? EscapeKind.Capture : EscapeKind.Other;
                all.Add(new Escape
                {
                    Kind = kind, Try = a, From = a.From, To = a.To, MoverType = mover.Type, CheckerSquare = checker,
                    TryEvents = ev, AttackerSquare = att, KingSquare = k2, AttackerType = att >= 0 ? c.Cells[att].Type : PieceType.None,
                });
            }

            // Choose a short, varied selection: rule blocks, one capture of the checker, one king step, one other; then fill.
            var picked = new HashSet<Escape>();
            void Take(System.Func<Escape, bool> f)
            {
                foreach (var e in all) { if (result.Count >= max) return; if (!picked.Contains(e) && f(e)) { picked.Add(e); result.Add(e); return; } }
            }
            Take(e => e.Kind == EscapeKind.KingCannotTakePoison);
            Take(e => e.Kind == EscapeKind.CannotTakeArmored);
            Take(e => e.Kind == EscapeKind.Frozen);
            Take(e => e.Kind == EscapeKind.Capture && e.MoverType != PieceType.King);
            Take(e => e.Kind == EscapeKind.KingStep);
            Take(e => e.Kind == EscapeKind.Other);
            // A second capture or king step if there is room and it is a different story (different winning attacker).
            foreach (var e in all)
            {
                if (result.Count >= max) break;
                if (picked.Contains(e)) continue;
                if (e.Kind != EscapeKind.Capture && e.Kind != EscapeKind.KingStep) continue;
                bool dup = false;
                foreach (var r in result) if (r.Kind == e.Kind && r.AttackerSquare == e.AttackerSquare) dup = true;
                if (!dup) { picked.Add(e); result.Add(e); }
            }
            return result;
        }

        static bool Adjacent(int a, int b)
        {
            return System.Math.Abs(Sq.File(a) - Sq.File(b)) <= 1 && System.Math.Abs(Sq.Rank(a) - Sq.Rank(b)) <= 1;
        }

        /// <summary>
        /// A rival piece that attacks <paramref name="king"/> right now. Each candidate is tested alone: the other rival pieces stay on the board as
        /// neutral blockers (so a rook hidden behind a queen does not count), which covers every kind of attack (lines, knights, fireballs, bombs).
        /// </summary>
        public static int FindAttacker(GameState s, int king, Side by)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                var p = s.Cells[sq];
                if (p.IsEmpty || p.Side != by) continue;
                var c = s.Clone();
                for (int o = 0; o < 64; o++)
                {
                    if (o == sq) continue;
                    var q = c.Cells[o];
                    if (!q.IsEmpty && q.Side == by) c.Cells[o] = new Piece(PieceType.Pawn, by.Opponent());
                }
                if (Geometry.IsAttacked(c, king, by)) return sq;
            }
            return -1;
        }
    }
}
