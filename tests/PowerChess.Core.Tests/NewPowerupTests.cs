using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class NewPowerupTests
    {
        static void GiveGhost(GameState s, string sq, int turns = 3)
        {
            int i = Sq.Parse(sq);
            var p = s.Cells[i];
            p.Buffs |= Buff.Ghost;
            p.GhostTurns = (byte)turns;
            s.Cells[i] = p;
        }

        // ---------- Triple move ----------

        [Test]
        public void Triple_move_forces_three_moves_with_the_same_piece()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
            T.Give(s, "b1", Buff.TripleMove);
            T.Do(s, "b1c3");
            Assert.AreEqual(Side.White, s.ToMove);
            Assert.IsTrue(MoveGen.LegalActions(s).All(a => a.From == Sq.Parse("c3")));
            T.Do(s, "c3d5");
            Assert.AreEqual(Side.White, s.ToMove, "second move still owes a third");
            Assert.IsTrue(MoveGen.LegalActions(s).All(a => a.From == Sq.Parse("d5")));
            T.Do(s, "d5f4");
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.IsFalse(T.At(s, "f4").Has(Buff.TripleMove), "spent");
        }

        [Test]
        public void Triple_move_is_forfeited_when_a_move_gives_check()
        {
            var s = T.Fen("4k3/8/8/8/8/2N5/8/4K3 w - - 0 1");
            T.Give(s, "c3", Buff.TripleMove);
            T.Do(s, "c3b5");
            Assert.AreEqual(Side.White, s.ToMove);
            T.Do(s, "b5d6"); // checks the king on e8
            Assert.AreEqual(Side.Black, s.ToMove, "check forfeits the remaining move");
            Assert.AreEqual(0, s.MultiLeft);
        }

        [Test]
        public void Double_and_triple_do_not_stack_on_one_piece()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
            var p = T.At(s, "b1");
            T.Give(s, "b1", Buff.TripleMove);
            p = T.At(s, "b1");
            Assert.IsFalse(Rules.Eligible(s, p, Sq.Parse("b1"), PowerupKind.DoubleMove));
            Assert.IsFalse(Rules.Eligible(s, p, Sq.Parse("b1"), PowerupKind.TripleMove));
        }

        // ---------- Ghost ----------

        [Test]
        public void Ghost_slides_through_pieces_and_boulders_but_not_onto_its_own()
        {
            var s = T.Fen("4k3/8/8/8/8/8/P7/R3K3 w - - 0 1");
            GiveGhost(s, "a1");
            s.Boulders[Sq.Parse("c1")] = true;
            Assert.IsTrue(T.IsLegal(s, "a1a3"), "ghost passes through its own pawn");
            Assert.IsTrue(T.IsLegal(s, "a1d1"), "ghost passes a boulder");
            Assert.IsFalse(T.IsLegal(s, "a1a2"), "never lands on its own piece");
            Assert.IsFalse(T.IsLegal(s, "a1c1"), "never lands on a boulder");
        }

        [Test]
        public void Ghost_can_capture_past_a_blocker_and_fades_after_its_turns()
        {
            var s = T.Fen("4k3/8/8/p7/8/8/P7/R3K3 w - - 0 1");
            GiveGhost(s, "a1", 2);
            T.Do(s, "a1a5");
            Assert.AreEqual(PieceType.Rook, T.At(s, "a5").Type);
            Assert.AreEqual(1, T.At(s, "a5").GhostTurns);
            T.Do(s, "e8d8");
            var ev = T.Do(s, "a5a6");
            Assert.IsFalse(T.At(s, "a6").Has(Buff.Ghost));
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.GhostFaded));
        }

        [Test]
        public void Ghost_cannot_be_given_to_knights_pawns_or_kings()
        {
            var s = T.Fen("4k3/8/8/8/8/8/P7/RNB1K3 w - - 0 1");
            Assert.IsTrue(Rules.Eligible(s, T.At(s, "a1"), 0, PowerupKind.Ghost));
            Assert.IsTrue(Rules.Eligible(s, T.At(s, "c1"), 2, PowerupKind.Ghost));
            Assert.IsFalse(Rules.Eligible(s, T.At(s, "b1"), 1, PowerupKind.Ghost));
            Assert.IsFalse(Rules.Eligible(s, T.At(s, "a2"), 8, PowerupKind.Ghost));
            Assert.IsFalse(Rules.Eligible(s, T.At(s, "e1"), 4, PowerupKind.Ghost));
        }

        // ---------- Swap ----------

        [Test]
        public void Swap_trades_two_own_pieces_buffs_included_and_costs_the_turn()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/R2BK3 w - - 0 1");
            T.Give(s, "a1", Buff.Poison);
            T.Perk(s, Side.White, PowerupKind.Swap);
            T.Do(s, "Swap a1->d1");
            Assert.AreEqual(PieceType.Bishop, T.At(s, "a1").Type);
            Assert.AreEqual(PieceType.Rook, T.At(s, "d1").Type);
            Assert.IsTrue(T.At(s, "d1").Has(Buff.Poison), "the buff travels with its piece");
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.AreEqual(0, s.Perks(Side.White, PowerupKind.Swap));
        }

        [Test]
        public void Swap_never_involves_kings_and_never_strands_a_pawn_on_the_back_rank()
        {
            var s = T.Fen("4k3/8/8/8/8/8/P7/R3K3 w - - 0 1");
            T.Perk(s, Side.White, PowerupKind.Swap);
            var swaps = MoveGen.LegalActions(s).Where(a => a.Kind == ActionKind.Swap).ToList();
            Assert.IsEmpty(swaps, "a pawn on a2 may not move to a1");
        }

        [Test]
        public void Swap_cannot_expose_the_own_king()
        {
            var s = T.Fen("4k3/8/8/8/8/8/4B3/r3R1K1 w - - 0 1");
            s.Cells[Sq.Parse("e2")] = new Piece(PieceType.Rook, Side.White);
            T.Perk(s, Side.White, PowerupKind.Swap);
            Assert.IsFalse(MoveGen.LegalActions(s).Any(a => a.Kind == ActionKind.Swap && a.From == Sq.Parse("e1") && a.To == Sq.Parse("e2")));
        }

        // ---------- Teleporters ----------

        static GameConfig PortalCfg()
        {
            var c = T.Cfg();
            c.PortalChance = 1;
            return c;
        }

        [Test]
        public void Portal_pairs_spawn_mirrored_and_visible()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var s = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", PortalCfg());
                s.Rng = new Rng(seed);
                Assert.IsTrue(Rules.TrySpawnPair(s, null));
                var squares = Enumerable.Range(0, 64).Where(i => s.Portal[i]).ToList();
                Assert.AreEqual(2, squares.Count);
                Assert.AreEqual(Sq.Mirror(squares[0]), squares[1]);
                Assert.AreEqual(0, s.MysteryCount());
            }
        }

        [Test]
        public void Landing_on_a_portal_teleports_to_the_partner_and_both_vanish()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", T.Cfg());
            int a = Sq.Parse("a4");
            s.Portal[a] = true;
            s.Portal[Sq.Mirror(a)] = true; // h5
            var ev = T.Do(s, "a1a4");
            Assert.AreEqual(PieceType.Rook, T.At(s, "h5").Type);
            Assert.IsTrue(T.At(s, "a4").IsEmpty);
            Assert.IsFalse(s.Portal[a]);
            Assert.IsFalse(s.Portal[Sq.Mirror(a)]);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Teleported && e.From == a && e.To == Sq.Mirror(a)));
        }

        [Test]
        public void A_blocked_portal_exit_leaves_the_piece_where_it_landed()
        {
            var s = T.Fen("4k3/8/8/7p/8/8/8/R3K3 w - - 0 1", T.Cfg());
            int a = Sq.Parse("a4");
            s.Portal[a] = true;
            s.Portal[Sq.Mirror(a)] = true;
            T.Do(s, "a1a4");
            Assert.AreEqual(PieceType.Rook, T.At(s, "a4").Type);
            Assert.AreEqual(PieceType.Pawn, T.At(s, "h5").Type);
        }

        [Test]
        public void Teleporting_keeps_a_double_move_going_from_the_new_square()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", T.Cfg());
            T.Give(s, "a1", Buff.DoubleMove);
            int a = Sq.Parse("a4");
            s.Portal[a] = true;
            s.Portal[Sq.Mirror(a)] = true;
            T.Do(s, "a1a4");
            Assert.AreEqual(Side.White, s.ToMove);
            Assert.IsTrue(MoveGen.LegalActions(s).All(x => x.From == Sq.Parse("h5")));
        }

        [Test]
        public void New_state_survives_the_save_codec()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", T.Cfg());
            GiveGhost(s, "a1", 2);
            s.Portal[Sq.Parse("c3")] = true;
            s.Portal[Sq.Mirror(Sq.Parse("c3"))] = true;
            T.Perk(s, Side.White, PowerupKind.Swap, 2);
            T.Give(s, "e1", Buff.None);
            s.MultiLeft = 0;
            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(s, new GameHistory()), T.Cfg(), out s2, out h2));
            Assert.AreEqual(2, s2.Cells[0].GhostTurns);
            Assert.IsTrue(s2.Cells[0].Has(Buff.Ghost));
            Assert.IsTrue(s2.Portal[Sq.Parse("c3")]);
            Assert.AreEqual(2, s2.Perks(Side.White, PowerupKind.Swap));
        }
    }
}
