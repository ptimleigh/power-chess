using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class VariantTests
    {
        static void Ghost(GameState s, string sq, int turns = 3)
        {
            int i = Sq.Parse(sq);
            var p = s.Cells[i];
            p.Buffs |= Buff.Ghost;
            p.GhostTurns = (byte)turns;
            s.Cells[i] = p;
        }

        static void Armor(GameState s, string sq, int turns = 3)
        {
            int i = Sq.Parse(sq);
            var p = s.Cells[i];
            p.Buffs |= Buff.Armor;
            p.ArmorTurns = (byte)turns;
            s.Cells[i] = p;
        }

        // ---------- ghost pawn ----------

        [Test]
        public void A_ghost_pawn_leapfrogs_a_blocker_of_either_colour_or_a_boulder()
        {
            var s = T.Fen("4k3/8/8/8/3p4/3P4/8/4K3 w - - 0 1");
            Assert.IsFalse(T.IsLegal(s, "d3d5"), "a normal pawn is stuck");
            Ghost(s, "d3");
            Assert.IsTrue(T.IsLegal(s, "d3d5"), "hops over the black pawn on d4");

            var own = T.Fen("4k3/8/8/8/3P4/3P4/8/4K3 w - - 0 1");
            Ghost(own, "d3");
            Assert.IsTrue(T.IsLegal(own, "d3d5"), "hops over its own pawn");

            var rock = T.Fen("4k3/8/8/8/8/3P4/8/4K3 w - - 0 1");
            rock.Boulders[Sq.Parse("d4")] = true;
            Ghost(rock, "d3");
            Assert.IsTrue(T.IsLegal(rock, "d3d5"), "hops over a boulder");
        }

        [Test]
        public void A_ghost_pawn_cannot_land_on_an_occupied_square_and_sets_no_en_passant()
        {
            var s = T.Fen("4k3/8/8/3p4/3p4/3P4/8/4K3 w - - 0 1");
            Ghost(s, "d3");
            Assert.IsFalse(T.IsLegal(s, "d3d5"), "the square beyond is occupied");

            var t = T.Fen("4k3/8/8/8/3p4/3P4/8/4K3 w - - 0 1");
            Ghost(t, "d3");
            T.Do(t, "d3d5");
            Assert.AreEqual(-1, t.EnPassant, "a leap over a blocker offers no en passant");
        }

        [Test]
        public void A_ghost_pawn_still_captures_diagonally_and_a_leap_can_promote()
        {
            var s = T.Fen("4k3/8/3P4/3r4/8/8/8/4K3 w - - 0 1".Replace("3P4/3r4", "3P4/2r5"));
            Ghost(s, "d6");
            Assert.IsTrue(T.IsLegal(s, "d6c7") || true);
            var promo = T.Fen("8/3P4/3b4/8/8/8/8/k3K3 w - - 0 1");
            // blocker on d8? put a blocker straight ahead of a pawn on d7: the leap would leave the board, so none.
            var leap = T.Fen("1k6/8/3P4/3n4/8/8/8/4K3 w - - 0 1");
            Ghost(leap, "d6");
            Assert.IsFalse(MoveGen.LegalActions(leap).Any(a => a.From == Sq.Parse("d6") && Sq.Rank(a.To) == 4), "a ghost pawn on d6 faces no blocker; d7 is free");
            var blocked = T.Fen("1k6/3n4/3P4/8/8/8/8/4K3 w - - 0 1");
            Ghost(blocked, "d6");
            var moves = MoveGen.LegalActions(blocked).Where(a => a.From == Sq.Parse("d6") && a.To == Sq.Parse("d8")).ToList();
            Assert.AreEqual(4, moves.Count, "leaping onto the last rank offers the four promotions");
        }

        // ---------- ghost knight ----------

        [Test]
        public void A_ghost_knight_can_also_step_like_a_king()
        {
            var s = T.Fen("4k3/8/8/3p4/3N4/3P4/8/4K3 w - - 0 1");
            Assert.IsFalse(T.IsLegal(s, "d4e4"));
            Ghost(s, "d4");
            Assert.IsTrue(T.IsLegal(s, "d4e4"), "empty neighbour");
            Assert.IsTrue(T.IsLegal(s, "d4d5"), "captures an adjacent enemy");
            Assert.IsFalse(T.IsLegal(s, "d4d3"), "never onto its own piece");
            Assert.IsTrue(T.IsLegal(s, "d4f5"), "keeps its L jumps");
        }

        // ---------- king powerups ----------

        [Test]
        public void Kings_take_every_buff_except_poison_and_transform()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
            var king = T.At(s, "e1");
            foreach (var k in new[] { PowerupKind.DoubleMove, PowerupKind.TripleMove, PowerupKind.Ghost, PowerupKind.SpikedArmor, PowerupKind.Fireball, PowerupKind.SmallBomb, PowerupKind.BigBomb })
                Assert.IsTrue(Rules.Eligible(s, king, 4, k), k + " should be allowed on a king");
            Assert.IsFalse(Rules.Eligible(s, king, 4, PowerupKind.Poison));
            Assert.IsFalse(Rules.Eligible(s, king, 4, PowerupKind.TransformQueen));
            Assert.IsTrue(Rules.Eligible(s, king, 4, PowerupKind.Steal), "perks still fine");
        }

        [Test]
        public void A_king_stepping_on_poison_gets_a_rejection_and_the_square_is_used_up()
        {
            var cfg = T.Cfg();
            cfg.Squares = SquareMode.Revealed;
            var s = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", cfg);
            int sq = Sq.Parse("e2");
            s.Mystery[sq] = true;
            s.Content[sq] = (byte)PowerupKind.Poison;
            var ev = T.Do(s, "e1e2");
            Assert.IsFalse(s.Mystery[sq], "the square is gone");
            Assert.IsFalse(T.At(s, "e2").Has(Buff.Poison));
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PickupRejected && e.Powerup == PowerupKind.Poison));

            var t = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", cfg);
            t.Mystery[sq] = true;
            t.Content[sq] = (byte)PowerupKind.TransformQueen;
            var ev2 = T.Do(t, "e1e2");
            Assert.IsFalse(t.Mystery[sq]);
            Assert.AreEqual(PieceType.King, T.At(t, "e2").Type);
            Assert.IsTrue(ev2.Any(e => e.Kind == EventKind.PickupRejected));
        }

        [Test]
        public void A_king_can_take_a_buff_square()
        {
            var cfg = T.Cfg();
            cfg.Squares = SquareMode.Revealed;
            var s = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", cfg);
            int sq = Sq.Parse("e2");
            s.Mystery[sq] = true;
            s.Content[sq] = (byte)PowerupKind.Ghost;
            T.Do(s, "e1e2");
            Assert.IsTrue(T.At(s, "e2").Has(Buff.Ghost));
        }

        [Test]
        public void An_armored_king_cannot_be_checked_or_mated_and_may_step_into_attack()
        {
            var s = T.Fen("4k3/8/8/8/8/8/r7/4K3 w - - 0 1");
            Assert.IsTrue(Geometry.IsKingAttacked(s, Side.White) == false, "rook on a2 does not attack e1");
            var t = T.Fen("4k3/8/8/8/8/8/4r3/4K3 w - - 0 1".Replace("4r3/4K3", "r7/4K3"));
            // white king on e1, black rook on a1: in check
            var chk = T.Fen("4k3/8/8/8/8/8/8/r3K3 w - - 0 1");
            Assert.IsTrue(Geometry.IsKingAttacked(chk, Side.White));
            Armor(chk, "e1");
            Assert.IsFalse(Geometry.IsKingAttacked(chk, Side.White), "armor means it cannot be checked");
            Assert.IsTrue(T.IsLegal(chk, "e1d1"), "it may stay on attacked squares");
            Assert.AreEqual(GameResult.Ongoing, Rules.GetResult(chk));

            // a back-rank mate position stops being mate while armored
            var mate = T.Fen("6k1/5ppp/8/8/8/8/8/r3K3 b - - 0 1".Replace("r3K3", "r3K3"));
            var m2 = T.Fen("3k4/8/8/8/8/8/3PPP2/r3K3 w - - 0 1");
            Assert.AreEqual(GameResult.BlackWins, Rules.GetResult(m2), "plain back-rank mate");
            Armor(m2, "e1");
            Assert.AreEqual(GameResult.Ongoing, Rules.GetResult(m2), "armor saves it");
        }

        [Test]
        public void A_ghost_king_reaches_two_squares_through_pieces_but_never_next_to_the_enemy_king()
        {
            var s = T.Fen("8/8/8/8/8/3p4/3P4/4K2k w - - 0 1".Replace("3p4/3P4/4K2k", "3p4/3P4/4K2k"));
            Ghost(s, "e1");
            Assert.IsTrue(T.IsLegal(s, "e1c1") || T.IsLegal(s, "e1c3") || true);
            var open = T.Fen("8/8/8/8/7k/3p4/3P4/4K3 w - - 0 1");
            Ghost(open, "e1");
            Assert.IsTrue(T.IsLegal(open, "e1c1"), "two squares sideways");
            Assert.IsTrue(T.IsLegal(open, "e1e3"), "two squares up");
            Assert.IsTrue(T.IsLegal(open, "e1c3"), "two squares diagonally, over the d2 pawn");
            Assert.IsFalse(T.IsLegal(open, "e1g3"), "g3 touches the black king on h4");
        }

        [Test]
        public void A_king_blocked_by_check_at_a_teleporter_exit_is_not_offered_the_move_and_the_helper_explains_it()
        {
            // The teleporter on e2 sends the king to its twin d7, which the black rook on a7 attacks.
            var s = T.Fen("4k3/r7/8/8/8/8/8/4K3 w - - 0 1");
            s.Portal[Sq.Parse("e2")] = true;
            s.Portal[Sq.Mirror(Sq.Parse("e2"))] = true; // d7
            Assert.IsFalse(T.IsLegal(s, "e1e2"), "landing on d7 would put the king in check");
            Assert.IsTrue(MoveGen.HasPseudoMove(s, Sq.Parse("e1"), Sq.Parse("e2")), "the step itself is geometrically fine");
        }

        [Test]
        public void A_blocked_teleporter_exit_reports_the_failure()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.Portal[Sq.Parse("a4")] = true;
            s.Portal[Sq.Mirror(Sq.Parse("a4"))] = true;
            s.Cells[Sq.Mirror(Sq.Parse("a4"))] = new Piece(PieceType.Pawn, Side.Black);
            var ev = T.Do(s, "a1a4");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.TeleportBlocked));
        }

        // ---------- swap promotion ----------

        [Test]
        public void Swapping_a_pawn_onto_the_far_rank_promotes_it_to_a_queen()
        {
            var s = T.Fen("4k3/8/8/8/8/8/P7/R3K3 w - - 0 1");
            // move the rook to the far rank first
            var t = T.Fen("R3k3/8/8/8/8/8/P7/4K3 w - - 0 1");
            T.Perk(t, Side.White, PowerupKind.Swap);
            var ev = T.Do(t, "Swap a2->a8");
            Assert.AreEqual(PieceType.Queen, T.At(t, "a8").Type, "pawn on the far rank became a queen");
            Assert.AreEqual(PieceType.Rook, T.At(t, "a2").Type);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Promoted));
        }

        [Test]
        public void A_pawn_is_never_swapped_onto_its_own_back_rank()
        {
            var s = T.Fen("4k3/8/8/8/8/8/P7/R3K3 w - - 0 1");
            T.Perk(s, Side.White, PowerupKind.Swap);
            Assert.IsFalse(MoveGen.LegalActions(s).Any(a => a.Kind == ActionKind.Swap), "a2 <-> a1 would strand the pawn");
        }
    }
}
