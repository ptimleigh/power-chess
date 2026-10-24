using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class PowerupTests
    {
        // ---------- Poison ----------

        [Test]
        public void Poison_removes_the_capturer_too()
        {
            var s = T.Fen("4k3/8/8/8/3p4/8/8/3RK3 w - - 0 1");
            T.Give(s, "d4", Buff.Poison);
            T.Do(s, "d1d4");
            Assert.IsTrue(T.At(s, "d4").IsEmpty, "capturer and victim should both be gone");
            Assert.IsTrue(T.At(s, "d1").IsEmpty);
        }

        [Test]
        public void Poison_does_not_hurt_a_king()
        {
            var s = T.Fen("4k3/8/8/8/8/8/3p4/3K4 w - - 0 1");
            T.Give(s, "d2", Buff.Poison);
            T.Do(s, "d1d2");
            Assert.AreEqual(PieceType.King, T.At(s, "d2").Type);
        }

        // ---------- Spiked armor ----------

        [Test]
        public void Armor_blocks_captures_for_three_opponent_turns()
        {
            var s = T.Fen("7k/8/8/8/3n4/8/8/K2R4 b - - 0 1");
            Rules.ApplyPickup(s, Side.Black, Sq.Parse("d4"), PowerupKind.SpikedArmor, null);

            T.Do(s, "h8g8"); // pickup turn
            string[] white = { "a1a2", "a2a1", "a1a2" };
            string[] black = { "g8h8", "h8g8", "g8h8" };
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(T.IsLegal(s, "d1d4"), "armor should hold on opponent turn " + (i + 1));
                T.Do(s, white[i]);
                T.Do(s, black[i]);
            }
            Assert.IsTrue(T.IsLegal(s, "d1d4"), "armor should have expired");
        }

        // ---------- Stun ----------

        [Test]
        public void Stun_strike_freezes_target_for_three_turns_and_attacker_stays_put()
        {
            var s = T.Fen("r3k3/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Give(s, "a1", Buff.StunStrike);
            Assert.IsFalse(T.IsLegal(s, "a1a8"), "a stun-buffed attack is a stun strike, not a capture");
            T.Do(s, "StunStrike a1->a8");
            Assert.AreEqual(PieceType.Rook, T.At(s, "a1").Type);
            Assert.AreEqual(3, T.At(s, "a8").StunTurns);
            Assert.IsFalse(T.At(s, "a1").Has(Buff.StunStrike), "buff is one-shot");

            string[] black = { "e8d8", "d8e8", "e8d8" };
            string[] white = { "e1d1", "d1e1", "e1d1" };
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(MoveGen.LegalActions(s).Any(a => a.From == Sq.Parse("a8")), "stunned on black turn " + (i + 1));
                T.Do(s, black[i]);
                T.Do(s, white[i]);
            }
            Assert.IsTrue(MoveGen.LegalActions(s).Any(a => a.From == Sq.Parse("a8")), "stun should have worn off");
        }

        [Test]
        public void Stunned_defender_makes_checkmate()
        {
            // Rook a8 checks the king on h8; only the queen on b7 could capture it.
            string fen = "R6k/1q4pp/8/8/8/8/8/4K3 b - - 0 1";
            var free = T.Fen(fen);
            Assert.AreEqual(GameResult.Ongoing, Rules.GetResult(free));

            var stunned = T.Fen(fen);
            var q = stunned.Cells[Sq.Parse("b7")];
            q.StunTurns = 3;
            stunned.Cells[Sq.Parse("b7")] = q;
            Assert.AreEqual(GameResult.WhiteWins, Rules.GetResult(stunned));
        }

        // ---------- Result reasons ----------

        [Test]
        public void Results_explain_themselves()
        {
            string reason;
            Assert.AreEqual(GameResult.Draw, Rules.GetResult(T.Fen("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1"), out reason));
            Assert.AreEqual("Stalemate", reason);

            Assert.AreEqual(GameResult.Draw, Rules.GetResult(T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1"), out reason));
            Assert.AreEqual("Only kings left", reason);

            Assert.AreEqual(GameResult.WhiteWins, Rules.GetResult(T.Fen("R6k/6pp/8/8/8/8/8/4K3 b - - 0 1"), out reason));
            Assert.AreEqual("Checkmate", reason);

            Assert.AreEqual(GameResult.Ongoing, Rules.GetResult(T.Fen("4k3/8/8/8/8/8/4P3/4K3 w - - 0 1"), out reason));
            Assert.AreEqual("", reason);
        }

        // ---------- Double move ----------

        [Test]
        public void Double_move_forces_a_second_move_with_the_same_piece()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
            T.Give(s, "b1", Buff.DoubleMove);
            T.Do(s, "b1c3");
            Assert.AreEqual(Side.White, s.ToMove, "still white's turn");
            var legal = MoveGen.LegalActions(s);
            Assert.IsNotEmpty(legal);
            Assert.IsTrue(legal.All(a => a.From == Sq.Parse("c3")), "only the knight may move");
            T.Do(s, "c3d5");
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.IsFalse(T.At(s, "d5").Has(Buff.DoubleMove), "buff is spent");
        }

        [Test]
        public void Double_move_is_forfeited_when_the_first_move_gives_check()
        {
            var s = T.Fen("4k3/8/8/1N6/8/8/8/4K3 w - - 0 1");
            T.Give(s, "b5", Buff.DoubleMove);
            T.Do(s, "b5d6");
            Assert.AreEqual(Side.Black, s.ToMove, "check forfeits the second move");
        }

        // ---------- Fireball ----------

        [Test]
        public void Fireball_hits_only_the_first_enemy_piece_on_each_line()
        {
            var s = T.Fen("4k3/8/8/r7/8/8/p7/R3K3 w - - 0 1");
            T.Perk(s, Side.White, PowerupKind.Fireball);
            T.Do(s, "Fireball a1");
            Assert.IsTrue(T.At(s, "a2").IsEmpty, "first piece destroyed");
            Assert.AreEqual(PieceType.Rook, T.At(s, "a5").Type, "piece behind it is untouched");
            Assert.AreEqual(0, s.Perks(Side.White, PowerupKind.Fireball));
        }

        [Test]
        public void Fireball_never_hits_kings_or_own_pieces()
        {
            var s = T.Fen("k7/8/8/8/8/8/P7/R3K3 w - - 0 1");
            T.Perk(s, Side.White, PowerupKind.Fireball);
            Assert.IsFalse(T.IsLegal(s, "Fireball a1"), "own pawn blocks the file and nothing else is hit");
            var s2 = T.Fen("k7/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Perk(s2, Side.White, PowerupKind.Fireball);
            Assert.IsFalse(T.IsLegal(s2, "Fireball a1"), "king is immune");
        }

        [Test]
        public void Knight_fireball_hits_everything_on_its_three_square_path()
        {
            // Path from b1 to c3, long leg first: b2, b3, c3. The pawn on c2 is off the path.
            var s = T.Fen("4k3/8/8/8/8/1pp5/1pp5/1N2K3 w - - 0 1");
            T.Perk(s, Side.White, PowerupKind.Fireball);
            T.Do(s, "Fireball b1->c3");
            Assert.IsTrue(T.At(s, "b2").IsEmpty);
            Assert.IsTrue(T.At(s, "b3").IsEmpty);
            Assert.IsTrue(T.At(s, "c3").IsEmpty);
            Assert.AreEqual(PieceType.Pawn, T.At(s, "c2").Type);
        }

        // ---------- Steal ----------

        [Test]
        public void Steal_converts_an_attackable_piece_without_moving_the_thief()
        {
            var s = T.Fen("r3k3/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Perk(s, Side.White, PowerupKind.Steal);
            T.Do(s, "Steal a1->a8");
            Assert.AreEqual(Side.White, T.At(s, "a8").Side);
            Assert.AreEqual(PieceType.Rook, T.At(s, "a8").Type);
            Assert.AreEqual(PieceType.Rook, T.At(s, "a1").Type, "thief stays put");
        }

        [Test]
        public void Steal_cannot_target_kings_or_armored_pieces()
        {
            var s = T.Fen("R3k3/8/8/8/8/8/8/4K3 w - - 0 1");
            T.Perk(s, Side.White, PowerupKind.Steal);
            Assert.IsFalse(MoveGen.LegalActions(s).Any(a => a.Kind == ActionKind.Steal));

            var s2 = T.Fen("r3k3/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Perk(s2, Side.White, PowerupKind.Steal);
            T.Give(s2, "a8", Buff.Armor, 3);
            Assert.IsFalse(MoveGen.LegalActions(s2).Any(a => a.Kind == ActionKind.Steal));
        }

        // ---------- Bombs ----------

        [Test]
        public void Small_bomb_destroys_the_3x3_area_but_spares_kings_armor_and_the_bomber()
        {
            var s = T.Fen("8/8/8/2pPk3/3N4/4P3/8/K7 w - - 0 1");
            // c5 black pawn, d5 white pawn (armored), e5 black king, e3 own pawn, d4 bomber knight.
            T.Give(s, "d4", Buff.SmallBomb);
            T.Give(s, "d5", Buff.Armor, 3);
            s.Boulders[Sq.Parse("c3")] = true;
            T.Do(s, "Detonate d4");
            Assert.IsTrue(T.At(s, "c5").IsEmpty, "enemy pawn destroyed");
            Assert.IsTrue(T.At(s, "e3").IsEmpty, "own pawn destroyed too");
            Assert.AreEqual(PieceType.Pawn, T.At(s, "d5").Type, "armor survives");
            Assert.AreEqual(PieceType.King, T.At(s, "e5").Type, "kings survive");
            Assert.AreEqual(PieceType.Knight, T.At(s, "d4").Type, "bomber survives");
            Assert.IsFalse(s.Boulders[Sq.Parse("c3")], "boulder cleared");
            Assert.IsFalse(T.At(s, "d4").Has(Buff.SmallBomb), "bomb is spent");
        }

        [Test]
        public void Big_bomb_reaches_two_squares_out()
        {
            var s = T.Fen("k7/8/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.BigBomb);
            var far = Sq.Parse("f6");
            s.Cells[far] = new Piece(PieceType.Rook, Side.Black);
            var outside = Sq.Parse("g7");
            s.Cells[outside] = new Piece(PieceType.Rook, Side.Black);
            T.Do(s, "Detonate d4");
            Assert.IsTrue(s.Cells[far].IsEmpty);
            Assert.AreEqual(PieceType.Rook, s.Cells[outside].Type);
        }

        [Test]
        public void A_bomb_that_would_expose_your_own_king_is_illegal()
        {
            var s = T.Fen("6k1/4r3/8/8/8/3N4/4P3/4K3 w - - 0 1");
            T.Give(s, "d3", Buff.SmallBomb);
            Assert.IsFalse(T.IsLegal(s, "Detonate d3"));
        }

        // ---------- Boulders and rerolls ----------

        [Test]
        public void Boulder_can_block_a_check_and_blocks_movement()
        {
            var s = T.Fen("4r1k1/8/8/8/8/8/8/4K3 w - - 0 1");
            T.Perk(s, Side.White, PowerupKind.Boulder);
            Assert.IsTrue(T.IsLegal(s, "PlaceBoulder e4->e4"));
            Assert.IsFalse(T.IsLegal(s, "PlaceBoulder a4->a4"), "must resolve the check");

            T.Do(s, "PlaceBoulder e4->e4");
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.White));
            // The rook can no longer travel past or onto the boulder.
            T.Do(s, "g8g7");
            T.Do(s, "e1d1");
            Assert.IsFalse(T.IsLegal(s, "e8e4"));
            Assert.IsFalse(T.IsLegal(s, "e8e3"));
            Assert.IsTrue(T.IsLegal(s, "e8e5"));
        }

        [Test]
        public void Reroll_clears_a_boulder()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
            s.Boulders[Sq.Parse("e4")] = true;
            Assert.IsFalse(MoveGen.LegalActions(s).Any(a => a.Kind == ActionKind.ClearBoulder));
            T.Perk(s, Side.White, PowerupKind.Reroll);
            T.Do(s, "ClearBoulder e4->e4");
            Assert.IsFalse(s.Boulders[Sq.Parse("e4")]);
            Assert.AreEqual(0, s.Perks(Side.White, PowerupKind.Reroll));
        }

        // ---------- Mystery squares ----------

        static GameConfig Pool(params PowerupKind[] kinds)
        {
            var c = T.Cfg();
            c.Pool = kinds.Select(k => new WeightedKind(k, 1)).ToList();
            return c;
        }

        [Test]
        public void Landing_on_a_mystery_square_with_a_reroll_lets_you_keep_or_reroll()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Pool(PowerupKind.Poison, PowerupKind.Steal));
            s.Rng = new Rng(7);
            T.Perk(s, Side.White, PowerupKind.Reroll);
            s.Mystery[Sq.Parse("d4")] = true;

            T.Do(s, "b3d4");
            Assert.AreEqual(Side.White, s.ToMove, "turn is held while the pickup is decided");
            var options = MoveGen.LegalActions(s).Select(a => a.Kind).ToList();
            CollectionAssert.AreEquivalent(new[] { ActionKind.KeepPickup, ActionKind.RerollPickup }, options);

            T.Do(s, "RerollPickup");
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.AreEqual(0, s.Perks(Side.White, PowerupKind.Reroll), "the reroll was spent");
            Assert.IsFalse(s.Mystery[Sq.Parse("d4")]);
        }

        [Test]
        public void Keeping_a_pickup_applies_it_immediately()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Pool(PowerupKind.Poison));
            T.Perk(s, Side.White, PowerupKind.Reroll);
            s.Mystery[Sq.Parse("d4")] = true;
            T.Do(s, "b3d4");
            T.Do(s, "KeepPickup");
            Assert.IsTrue(T.At(s, "d4").Has(Buff.Poison));
            Assert.AreEqual(Side.Black, s.ToMove);
        }

        [Test]
        public void Without_a_reroll_the_pickup_applies_straight_away()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Pool(PowerupKind.Poison));
            s.Mystery[Sq.Parse("d4")] = true;
            T.Do(s, "b3d4");
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.IsTrue(T.At(s, "d4").Has(Buff.Poison));
        }

        [Test]
        public void Kings_only_ever_collect_perks()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var s = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", Pool(PowerupKind.Poison, PowerupKind.DoubleMove, PowerupKind.Steal));
                s.Rng = new Rng(seed);
                s.Mystery[Sq.Parse("e2")] = true;
                T.Do(s, "e1e2");
                Assert.AreEqual(Buff.None, T.At(s, "e2").Buffs, "kings are immune to buffs");
                Assert.AreEqual(1, s.Perks(Side.White, PowerupKind.Steal));
            }
        }

        [Test]
        public void Transform_changes_the_piece_type_and_is_never_a_king()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Pool(PowerupKind.TransformQueen));
            s.Mystery[Sq.Parse("d4")] = true;
            T.Do(s, "b3d4");
            Assert.AreEqual(PieceType.Queen, T.At(s, "d4").Type);
        }

        [Test]
        public void Spawning_places_mirrored_pairs_on_the_middle_ranks()
        {
            var s = GameState.NewGame(new GameConfig { InitialPairs = 3 }, 99);
            Assert.AreEqual(6, s.MysteryCount());
            for (int sq = 0; sq < 64; sq++)
            {
                if (!s.Mystery[sq]) continue;
                Assert.IsTrue(s.Mystery[Sq.Mirror(sq)], "mirrored");
                Assert.That(Sq.Rank(sq), Is.InRange(2, 5));
                Assert.IsTrue(s.Cells[sq].IsEmpty);
            }
        }

        [Test]
        public void Castling_rights_are_lost_when_a_rook_is_stolen()
        {
            // Black's rook on h8 steals the white rook on h1 down the open h-file.
            var s = T.Fen("4k2r/8/8/8/8/8/8/4K2R b Kk - 0 1");
            T.Perk(s, Side.Black, PowerupKind.Steal);
            T.Do(s, "Steal h8->h1");
            Assert.AreEqual(Side.Black, T.At(s, "h1").Side);
            Assert.AreEqual(0, s.Castling & GameState.CastleWK, "white lost its kingside right");
            Assert.AreNotEqual(0, s.Castling & GameState.CastleBK);
        }
    }
}
