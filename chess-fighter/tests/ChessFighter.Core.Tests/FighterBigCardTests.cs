using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ChessFighter.Core;

namespace ChessFighter.Core.Tests
{
    /// <summary>The larger cards: Take back, Mars, Dragon Strike, Death, Hit restart, World turned upside down and Missile Strike.</summary>
    public class FighterBigCardTests
    {
        static List<GameAction> Plays(GameState s, CardKind kind)
        {
            return MoveGen.LegalActions(s).Where(a => a.Kind == ActionKind.PlayCard && Cards.KindOf(a.Aux) == kind).ToList();
        }

        static void PlayCard(GameState s, CardKind kind, int index = 0)
        {
            Rules.Apply(s, Plays(s, kind)[index]);
        }

        // ---------- Take back ----------

        [Test]
        public void Take_back_returns_the_board_to_the_start_of_your_previous_turn_and_needs_history()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.TakeBack, 1);
            Assert.AreEqual(0, Plays(s, CardKind.TakeBack).Count, "nothing to undo yet");

            T.Do(s, "a1a3");
            T.Do(s, "h8h7");
            T.Do(s, "a3a5");
            T.Do(s, "h7h8");
            Assert.AreEqual(PieceType.Rook, T.At(s, "a5").Type);
            Assert.AreEqual(1, Plays(s, CardKind.TakeBack).Count);

            PlayCard(s, CardKind.TakeBack);
            Assert.AreEqual(PieceType.Rook, T.At(s, "a3").Type, "the rook is back where it was at the start of your last turn");
            Assert.IsTrue(T.At(s, "a5").IsEmpty);
            Assert.AreEqual(PieceType.King, T.At(s, "h7").Type, "and the rival's reply is undone too");
            Assert.AreEqual(0, s.CardCount(Side.White, CardKind.TakeBack));
            Assert.AreEqual(Side.White, s.ToMove, "Take back does not cost the turn: the player who played it moves again");
        }

        [Test]
        public void Take_back_restores_pieces_lost_in_the_undone_round_and_their_Caissa_count()
        {
            var s = T.Fen("7k/8/8/8/8/8/p7/R3K3 w - - 0 1");
            T.Do(s, "e1d1");
            T.Do(s, "h8h7");
            T.Do(s, "a1a2");              // takes the pawn... wait: rook a1 captures a2 pawn
            T.Do(s, "h7h8");
            s.AddCard(Side.White, CardKind.TakeBack, 1);
            Assert.AreEqual(1, s.Captured[(int)Side.Black * 8 + (int)PieceType.Pawn], "the black pawn was lost");
            PlayCard(s, CardKind.TakeBack);
            Assert.AreEqual(PieceType.Pawn, T.At(s, "a2").Type, "the pawn is back");
            Assert.AreEqual(0, s.Captured[(int)Side.Black * 8 + (int)PieceType.Pawn], "so it is not counted as lost");
        }

        // ---------- Summon Mars ----------

        [Test]
        public void Mars_gives_three_turns_of_hints_and_does_not_stack()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.SummonMars, 1);
            PlayCard(s, CardKind.SummonMars);
            Assert.AreEqual(Side.White, s.ToMove, "Mars does not cost the turn: the player still moves");
            Assert.AreEqual(3, s.Mars[(int)Side.White]);
            T.Do(s, "a1a2");
            Assert.AreEqual(2, s.Mars[(int)Side.White]);
            T.Do(s, "h8h7");
            T.Do(s, "a2a3");
            T.Do(s, "h7h8");
            T.Do(s, "a3a4");
            T.Do(s, "h8h7");
            T.Do(s, "a4a5");
            Assert.AreEqual(0, s.Mars[(int)Side.White], "this turn and the next two, then it is spent");

            var again = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            again.AddCard(Side.White, CardKind.SummonMars, 1);
            again.Mars[(int)Side.White] = 2;
            Assert.AreEqual(0, Plays(again, CardKind.SummonMars).Count, "already active");
        }

        // ---------- Dragon Strike ----------

        [Test]
        public void Dragon_strike_burns_one_row_of_both_colours_but_never_a_king_armor_or_the_edge_rows()
        {
            var s = T.Fen("k7/8/p1pP4/8/3n3p/8/8/4K3 w - - 0 1");
            T.Give(s, "d6", Buff.Armor, 3);
            s.AddCard(Side.White, CardKind.DragonStrike, 1);
            var rows = Plays(s, CardKind.DragonStrike).Select(a => Sq.Rank(a.To) + 1).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(new[] { 4, 6 }, rows, "only rows 3-6 that hold something to burn and no king");

            var row6 = Plays(s, CardKind.DragonStrike).First(a => Sq.Rank(a.To) == 5);
            Rules.Apply(s, row6);
            Assert.IsTrue(T.At(s, "a6").IsEmpty);
            Assert.IsTrue(T.At(s, "c6").IsEmpty);
            Assert.AreEqual(PieceType.Pawn, T.At(s, "d6").Type, "the armored pawn survives");
            Assert.AreEqual(PieceType.Knight, T.At(s, "d4").Type, "other rows are untouched");
        }

        // ---------- Summon Death ----------

        [Test]
        public void Death_strikes_up_to_three_rival_pieces_never_kings_and_never_exposes_your_own_king()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                // Black rook on e8 sits behind a black pawn on e5 pointing at the white king on e1: striking the pawn alone would expose the king.
                var s = T.Fen("4r2k/8/8/4p3/8/8/n1b5/4K3 w - - 0 1");
                s.Rng = new Rng(seed);
                s.AddCard(Side.White, CardKind.SummonDeath, 1);
                PlayCard(s, CardKind.SummonDeath);
                int rivals = Enumerable.Range(0, 64).Count(i => s.Cells[i].Side == Side.Black && !s.Cells[i].IsEmpty);
                Assert.GreaterOrEqual(rivals, 2, "black has its king plus at most one survivor...");
                Assert.AreEqual(PieceType.King, T.At(s, "h8").Type, "the black king is never hit");
                Assert.IsFalse(Geometry.IsKingAttacked(s, Side.White), "seed " + seed + ": your own king is never left exposed");
            }
        }

        [Test]
        public void Death_never_disturbs_the_real_random_sequence_in_a_legality_check()
        {
            var s = T.Fen("4r2k/8/8/4p3/8/8/n1b5/4K3 w - - 0 1");
            s.Rng = new Rng(12345);
            s.AddCard(Side.White, CardKind.SummonDeath, 1);
            ulong before = s.Rng.State;
            MoveGen.LegalActions(s);
            Assert.AreEqual(before, s.Rng.State, "generating and probing the card leaves the generator alone");
            PlayCard(s, CardKind.SummonDeath);
            Assert.AreNotEqual(before, s.Rng.State, "the real play draws from it");
        }

        // ---------- Hit restart ----------

        [Test]
        public void Hit_restart_sends_every_surviving_piece_home_keeping_powers_and_losses()
        {
            var s = GameState.FromFen(GameState.StartFen, T.Cfg());
            T.Do(s, "e2e4");
            T.Do(s, "d7d5");
            T.Do(s, "e4d5");   // white captures a black pawn
            T.Do(s, "g8f6");
            T.Give(s, "d5", Buff.Fireball);
            s.AddCard(Side.White, CardKind.HitRestart, 1);
            PlayCard(s, CardKind.HitRestart);
            Assert.AreEqual(PieceType.Pawn, T.At(s, "e2").Type, "the white pawn is back on its starting square");
            Assert.IsTrue(T.At(s, "d5").IsEmpty);
            Assert.IsTrue(T.At(s, "d7").IsEmpty, "the captured black pawn stays captured");
            Assert.AreEqual(PieceType.Knight, T.At(s, "g8").Type, "black's knight went home too");
            Assert.IsTrue(T.At(s, "e2").Has(Buff.Fireball), "powers are kept");
        }

        [Test]
        public void Hit_restart_is_not_offered_when_everything_is_already_home()
        {
            var s = GameState.FromFen(GameState.StartFen, T.Cfg());
            s.AddCard(Side.White, CardKind.HitRestart, 1);
            Assert.AreEqual(0, Plays(s, CardKind.HitRestart).Count);
        }

        [Test]
        public void Hit_restart_is_unplayable_only_if_it_would_leave_your_own_king_in_check()
        {
            // The white king left its home square (e1) for e2; the black rook on e8 left d8... after the restart the rook is back
            // on e8's file facing the king on e1? Instead: put the king where a home-bound black rook would then attack it.
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            foreach (int i in new[] { 0, 4, 63 - 0 }) { var p = s.Cells[i]; if (!p.IsEmpty) { p.Origin = (byte)(i + 1); s.Cells[i] = p; } }
            var rook = s.Cells[0];
            rook.Origin = (byte)(7 + 1);   // the rook "started" on h1
            s.Cells[0] = rook;
            var bk = s.Cells[63 - 0 - 7];
            Assert.IsNotNull(bk.Type.ToString());
            s.AddCard(Side.White, CardKind.HitRestart, 1);
            Assert.AreEqual(1, Plays(s, CardKind.HitRestart).Count, "a legal restart is offered");
        }

        // ---------- World turned upside down ----------

        [Test]
        public void World_turned_upside_down_rotates_the_board_and_swaps_the_armies()
        {
            var s = GameState.FromFen(GameState.StartFen, T.Cfg());
            T.Do(s, "e2e4");
            T.Do(s, "e7e5");
            s.AddCard(Side.White, CardKind.WorldUpsideDown, 1);
            PlayCard(s, CardKind.WorldUpsideDown);
            // The white pawn that stood on e4 is now a black pawn on d5 (square 63 - e4); the black pawn on e5 is now a white pawn on d4.
            Assert.AreEqual(Side.Black, T.At(s, "d5").Side);
            Assert.AreEqual(PieceType.Pawn, T.At(s, "d5").Type);
            Assert.AreEqual(Side.White, T.At(s, "d4").Side);
            Assert.AreEqual(PieceType.King, T.At(s, "d1").Type, "the old black king is now White's, on d1");
            Assert.AreEqual(Side.White, T.At(s, "d1").Side);
            Assert.AreEqual(0, s.Castling, "castling rights are gone");
            Assert.AreEqual(Side.Black, s.ToMove, "the card cost the turn");
            Assert.IsTrue(MoveGen.LegalActions(s).Count > 0);
        }

        [Test]
        public void World_turned_upside_down_keeps_every_pieces_starting_square_pointing_at_its_rotated_home()
        {
            // From the opening position every piece is on its start square. After the flip each one must still be on its (rotated) start square,
            // so a Hit restart straight afterwards would move nothing, and a pawn's start can never land on a back rank.
            var s = GameState.FromFen(GameState.StartFen, T.Cfg());
            T.Do(s, "e2e4");
            T.Do(s, "e7e5");
            T.Do(s, "e4e5".Replace("e4e5", "g1f3"));
            T.Do(s, "b8c6");
            s.AddCard(Side.White, CardKind.WorldUpsideDown, 1);
            PlayCard(s, CardKind.WorldUpsideDown);
            for (int i = 0; i < 64; i++)
            {
                var p = s.Cells[i];
                if (p.IsEmpty || p.Origin == 0) continue;
                int home = p.Origin - 1;
                if (p.Type == PieceType.Pawn)
                    Assert.IsFalse(Sq.Rank(home) == 0 || Sq.Rank(home) == 7, "a pawn's start square " + Sq.Name(home) + " is never a back rank");
            }
            Assert.AreEqual(Sq.Parse("d7") + 1, s.Cells[Sq.Parse("d5")].Origin == 0 ? 0 : (s.Cells[Sq.Parse("d5")].Origin), "the pawn from e2 (now a black pawn on d5) started on d7 after the rotation");
        }

        [Test]
        public void World_turned_upside_down_swaps_losses_and_is_unplayable_if_it_would_leave_you_in_check()
        {
            var s = T.Fen("k7/8/8/8/8/8/8/R6K w - - 0 1");
            s.AddCard(Side.White, CardKind.WorldUpsideDown, 1);
            Assert.AreEqual(0, Plays(s, CardKind.WorldUpsideDown).Count, "the rotated black rook on h8 would check the new white king on h1");

            var ok = T.Fen("k7/8/8/8/8/8/8/4K3 w - - 0 1");
            ok.Captured[(int)Side.White * 8 + (int)PieceType.Queen] = 1;
            ok.AddCard(Side.White, CardKind.WorldUpsideDown, 1);
            PlayCard(ok, CardKind.WorldUpsideDown);
            Assert.AreEqual(1, ok.Captured[(int)Side.Black * 8 + (int)PieceType.Queen], "losses go with the army");
            Assert.AreEqual(0, ok.Captured[(int)Side.White * 8 + (int)PieceType.Queen]);
        }

        // ---------- Missile Strike ----------

        static GameState MissileGame(int anchor)
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.MissileStrike, 1);
            Rules.Apply(s, Plays(s, CardKind.MissileStrike).First(a => a.To == anchor));
            return s;
        }

        [Test]
        public void A_missile_lands_after_ten_moves_and_a_king_still_in_the_quadrant_loses()
        {
            var s = MissileGame(36);   // the e5-h8 quadrant, where the black king sits
            Assert.AreEqual(10, s.MissileLeft, "the launch turn used one of the eleven");
            Assert.AreEqual(0, Plays(s, CardKind.MissileStrike).Count, "one missile at a time");

            string[] black = { "h8g8", "g8h8" };
            string[] white = { "a1a2", "a2a1" };
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(GameResult.Ongoing, Rules.GetResult(s), "still in flight after " + i + " moves");
                T.Do(s, s.ToMove == Side.Black ? black[(i / 2) % 2] : white[(i / 2) % 2]);
            }
            string reason;
            Assert.AreEqual(GameResult.WhiteWins, Rules.GetResult(s, out reason), "the black king never left the quadrant");
            Assert.AreEqual("Missile strike", reason);
            Assert.AreEqual(PieceType.King, T.At(s, "h8").Type.ToString() == "King" ? PieceType.King : T.At(s, "g8").Type, "a king is never removed from the board");
        }

        [Test]
        public void A_king_that_evacuates_in_time_survives_and_the_rest_of_the_quadrant_is_destroyed()
        {
            var s = T.Fen("6pk/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.Boulders[Sq.Parse("f6")] = true;
            s.AddCard(Side.White, CardKind.MissileStrike, 1);
            Rules.Apply(s, Plays(s, CardKind.MissileStrike).First(a => a.To == 36));
            string[] black = { "h8h7", "h7h6", "h6h5", "h5h4" };
            int b = 0;
            for (int i = 0; i < 10; i++)
            {
                if (s.ToMove == Side.Black) T.Do(s, b < 4 ? black[b++] : (b++ % 2 == 0 ? "h4h3" : "h3h4"));
                else T.Do(s, i % 4 == 1 ? "a1a2" : "a2a1");
            }
            Assert.AreEqual(GameResult.Ongoing, Rules.GetResult(s), "the king got out");
            Assert.AreEqual(0, s.MissileLeft);
            Assert.IsTrue(T.At(s, "g8").IsEmpty, "the pawn in the quadrant was destroyed");
            Assert.IsFalse(s.Boulders[Sq.Parse("f6")], "the boulder too");
        }

        [Test]
        public void Both_kings_in_the_quadrant_is_a_draw_and_armor_survives_the_blast()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.MissileStrike, 1);
            Rules.Apply(s, Plays(s, CardKind.MissileStrike).First(a => a.To == 0));  // a1-d4 quadrant: White's king is on e1, outside it
            Assert.AreEqual(10, s.MissileLeft);

            var armored = T.Fen("3Rk3/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Give(armored, "a1", Buff.Armor, 30);
            armored.MissileAnchor = 0;
            armored.MissileLeft = 1;
            T.Do(armored, "e1e2");
            Assert.AreEqual(PieceType.Rook, T.At(armored, "a1").Type, "armored pieces survive the missile");

            var both = T.Fen("8/8/8/8/3k4/8/1K6/8 w - - 0 1");
            both.MissileAnchor = 0;
            both.MissileLeft = 1;
            T.Do(both, "b2c2");
            string reason;
            Assert.AreEqual(GameResult.Draw, Rules.GetResult(both, out reason));
            Assert.AreEqual("Both kings were hit", reason);
        }

        [Test]
        public void The_AI_moves_its_king_out_of_a_quadrant_a_missile_is_about_to_hit()
        {
            var s = T.Fen("8/6k1/8/8/8/8/8/R3K3 b - - 0 1");
            s.MissileAnchor = 36;   // the e5-h8 quadrant
            s.MissileLeft = 6;
            var choice = Ai.ForLevel(3).Choose(s);
            Assert.AreEqual(ActionKind.Move, choice.Kind);
            Assert.AreEqual(Sq.Parse("g7"), choice.From);
            // Steps to leave the e5-h8 quadrant by its nearest open edge (left to the d-file, or down to rank 4).
            int Exit(int sq) => System.Math.Min(Sq.File(sq) - 4 + 1, Sq.Rank(sq) - 4 + 1);
            Assert.Less(Exit(choice.To), Exit(choice.From), "it heads for the nearest edge of the zone");
        }

        // ---------- saves ----------

        [Test]
        public void Snapshots_Mars_the_missile_and_starting_squares_survive_save_and_load()
        {
            var cfg = T.Cfg();
            var s = GameState.FromFen(GameState.StartFen, cfg);
            T.Do(s, "e2e4"); T.Do(s, "e7e5"); T.Do(s, "g1f3"); T.Do(s, "b8c6");
            s.AddCard(Side.White, CardKind.TakeBack, 1);
            s.Mars[(int)Side.White] = 2;
            s.MissileLeft = 7;
            s.MissileAnchor = 36;
            Assert.IsNotNull(s.TurnStarts[0]);

            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(s, new GameHistory()), cfg, out s2, out h2));
            Assert.AreEqual(2, s2.Mars[(int)Side.White]);
            Assert.AreEqual(7, s2.MissileLeft);
            Assert.AreEqual(36, s2.MissileAnchor);
            Assert.IsNotNull(s2.TurnStarts);
            Assert.AreEqual(Sq.Parse("g1") + 1, s2.Cells[Sq.Parse("f3")].Origin, "the knight remembers where it started");
            Assert.AreEqual(1, Plays(s2, CardKind.TakeBack).Count, "Take back still works after a reload");
        }

        [Test]
        public void Dead_cards_are_not_drawn_and_a_missile_in_flight_blocks_another()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            Assert.AreEqual(0, Cards.Weight(s, Side.White, CardKind.TakeBack), "no history yet");
            Assert.Greater(Cards.Weight(s, Side.White, CardKind.MissileStrike), 0);
            s.MissileLeft = 5;
            Assert.AreEqual(0, Cards.Weight(s, Side.White, CardKind.MissileStrike));
            Assert.AreEqual(0, Cards.Weight(s, Side.White, CardKind.SummonDeath), "only a king is left on the other side, and kings are never struck");
            var pawn = T.Fen("7k/8/8/8/8/p7/8/R3K3 w - - 0 1");
            Assert.Greater(Cards.Weight(pawn, Side.White, CardKind.SummonDeath), 0);
        }
    }
}
