using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ChessFighter.Core;

namespace ChessFighter.Core.Tests
{
    /// <summary>Chess Fighter's banked moves (Fireball I/II, Bomb tiers) and instant Combo squares. See docs/POWERS.md.</summary>
    public class FighterPowerTests
    {
        // ---------- Fireball I and II ----------

        [Test]
        public void Fireball_I_fires_only_along_ranks_and_files()
        {
            // Knight on d4 with a rook to hit straight up the file and a bishop on the diagonal.
            var s = T.Fen("3rk3/6b1/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.Fireball);
            T.Tier(s, "d4", 1);
            Assert.IsTrue(T.IsLegal(s, "Fireball d4->d8"), "up the d-file");
            Assert.IsFalse(T.IsLegal(s, "Fireball d4->g7"), "the diagonal is out of reach at tier I");
        }

        [Test]
        public void Fireball_II_also_fires_diagonally()
        {
            var s = T.Fen("4k3/6b1/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.Fireball);
            T.Tier(s, "d4", 2);
            T.Do(s, "Fireball d4->g7");
            Assert.IsTrue(T.At(s, "g7").IsEmpty);
            Assert.AreEqual(PieceType.Knight, T.At(s, "d4").Type, "the shooter stays where it is");
            Assert.AreEqual(Side.Black, s.ToMove, "firing uses the turn");
        }

        [Test]
        public void Firing_spends_the_fireball_and_its_tier()
        {
            var s = T.Fen("3rk3/8/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.Fireball);
            T.Tier(s, "d4", 2);
            T.Do(s, "Fireball d4->d8");
            Assert.IsFalse(T.At(s, "d4").Has(Buff.Fireball));
            Assert.AreEqual(0, T.At(s, "d4").Tier);
        }

        [Test]
        public void A_fireball_never_removes_a_king_and_is_stopped_by_boulders()
        {
            // The black king is the first piece up the d-file: there is nothing to fire at.
            var king = T.Fen("3k4/8/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(king, "d4", Buff.Fireball);
            Assert.IsFalse(MoveGen.LegalActions(king).Any(a => a.Kind == ActionKind.Fireball));

            var rock = T.Fen("3rk3/8/8/8/3N4/8/8/4K3 w - - 0 1");
            rock.Boulders[Sq.Parse("d6")] = true;
            T.Give(rock, "d4", Buff.Fireball);
            Assert.IsFalse(T.IsLegal(rock, "Fireball d4->d8"), "a boulder stops the shot");
        }

        [Test]
        public void A_friendly_piece_in_front_stops_the_shot()
        {
            var s = T.Fen("3rk3/8/3P4/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.Fireball);
            Assert.IsFalse(T.IsLegal(s, "Fireball d4->d8"));
        }

        // ---------- a banked fireball threatens like a rook (I) or a queen (II) ----------

        [Test]
        public void Fireball_I_gives_check_along_its_lines_like_a_rook()
        {
            // White knight on e3 and the black king on e8 share an open file.
            var s = T.Fen("4k3/8/8/8/8/4N3/8/4K3 b - - 0 1");
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.Black), "no fireball, no threat");
            T.Give(s, "e3", Buff.Fireball);
            T.Tier(s, "e3", 1);
            Assert.IsTrue(Geometry.IsKingAttacked(s, Side.Black), "a banked fireball down the open file is check");
        }

        [Test]
        public void Fireball_I_does_not_threaten_diagonals_but_II_does()
        {
            // Black king a8, white knight h1: they share the long diagonal.
            var d = T.Fen("k7/8/8/8/8/8/8/4K2N b - - 0 1");
            T.Give(d, "h1", Buff.Fireball);
            T.Tier(d, "h1", 1);
            Assert.IsFalse(Geometry.IsKingAttacked(d, Side.Black), "Fireball I does not reach diagonals");
            T.Tier(d, "h1", 2);
            Assert.IsTrue(Geometry.IsKingAttacked(d, Side.Black), "Fireball II reaches the long diagonal like a queen");
        }

        [Test]
        public void A_blocker_resolves_a_fireball_check_and_a_stunned_piece_threatens_nothing()
        {
            var s = T.Fen("4k3/4p3/8/8/8/4N3/8/4K3 b - - 0 1");
            T.Give(s, "e3", Buff.Fireball);
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.Black), "the pawn on e7 is the first piece in line");

            var chk = T.Fen("4k3/8/8/8/8/4N3/8/4K3 b - - 0 1");
            T.Give(chk, "e3", Buff.Fireball);
            Assert.IsTrue(Geometry.IsKingAttacked(chk, Side.Black));
            var p = chk.Cells[Sq.Parse("e3")];
            p.StunTurns = 1;
            chk.Cells[Sq.Parse("e3")] = p;
            Assert.IsFalse(Geometry.IsKingAttacked(chk, Side.Black), "a stunned piece cannot fire");
        }

        [Test]
        public void A_king_cannot_step_onto_a_square_a_banked_fireball_covers()
        {
            // White knight on d1 holds Fireball I and covers the whole d-file; black king on e8 may not go to d8.
            var s = T.Fen("4k3/8/8/8/8/8/8/3NK3 b - - 0 1");
            T.Give(s, "d1", Buff.Fireball);
            Assert.IsFalse(T.IsLegal(s, "e8d8"), "d8 is on the fireball's file");
            Assert.IsTrue(T.IsLegal(s, "e8e7"), "e7 is not");
        }

        [Test]
        public void A_fireball_can_give_checkmate()
        {
            // Black king a8 boxed in by its own pawns; a white knight on h8 holding Fireball I covers the eighth rank.
            var s = T.Fen("k6N/pp6/8/8/8/8/8/K7 b - - 0 1");
            T.Give(s, "h8", Buff.Fireball);
            T.Tier(s, "h8", 1);
            string reason;
            Assert.AreEqual(GameResult.WhiteWins, Rules.GetResult(s, out reason));
            Assert.AreEqual("Checkmate", reason);
        }

        // ---------- banking, tiers and replacement ----------

        [Test]
        public void Landing_on_Fireball_banks_I_then_II_then_maxes_out_and_uses_up_the_square()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Fireball);
            T.Square(s, "a5", PowerupKind.Fireball);
            T.Square(s, "a6", PowerupKind.Fireball);

            T.Do(s, "a1a3");
            Assert.IsTrue(T.At(s, "a3").Has(Buff.Fireball));
            Assert.AreEqual(1, T.At(s, "a3").Tier);
            Assert.IsFalse(s.Mystery[Sq.Parse("a3")], "square used up");

            T.Do(s, "h8h7");
            var ev = T.Do(s, "a3a5");
            Assert.AreEqual(2, T.At(s, "a5").Tier, "landing again upgrades to II");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PowerUpgraded));

            T.Do(s, "h7h8");
            ev = T.Do(s, "a5a6");
            Assert.AreEqual(2, T.At(s, "a6").Tier, "II is the top");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PowerMaxed));
            Assert.IsFalse(s.Mystery[Sq.Parse("a6")], "the square goes back to normal even though nothing changed");
        }

        [Test]
        public void Landing_on_a_different_banked_power_replaces_the_old_one_at_tier_I()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Give(s, "a1", Buff.Fireball);
            T.Tier(s, "a1", 2);
            T.Square(s, "a3", PowerupKind.Poison);

            T.Do(s, "a1a3");
            var rook = T.At(s, "a3");
            Assert.IsFalse(rook.Has(Buff.Fireball), "the fireball is lost");
            Assert.IsTrue(rook.Has(Buff.Poison));
            Assert.AreEqual(1, rook.Tier, "the new power starts at tier I");
        }

        [Test]
        public void Ghost_and_Poison_stay_at_I_when_landed_on_again()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Ghost);
            T.Square(s, "a5", PowerupKind.Ghost);
            T.Do(s, "a1a3");
            Assert.IsTrue(T.At(s, "a3").Has(Buff.Ghost));
            T.Do(s, "h8h7");
            var ev = T.Do(s, "a3a5");
            Assert.IsTrue(T.At(s, "a5").Has(Buff.Ghost));
            Assert.AreEqual(1, T.At(s, "a5").Tier);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PowerMaxed));
            Assert.IsFalse(s.Mystery[Sq.Parse("a5")]);
        }

        [Test]
        public void Bomb_upgrades_to_the_big_blast_on_a_second_landing_and_kings_refuse_it()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.SmallBomb);
            T.Square(s, "a5", PowerupKind.SmallBomb);
            T.Do(s, "a1a3");
            Assert.IsTrue(T.At(s, "a3").Has(Buff.SmallBomb));
            T.Do(s, "h8h7");
            T.Do(s, "a3a5");
            Assert.IsTrue(T.At(s, "a5").Has(Buff.BigBomb));
            Assert.IsFalse(T.At(s, "a5").Has(Buff.SmallBomb));
            Assert.AreEqual(2, T.At(s, "a5").Tier);

            var k = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(k, "e2", PowerupKind.SmallBomb);
            var ev = T.Do(k, "e1e2");
            Assert.IsFalse(T.At(k, "e2").Has(Buff.SmallBomb), "a king cannot take a bomb");
            Assert.IsFalse(k.Mystery[Sq.Parse("e2")], "and the square is used up");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PickupRejected));
        }

        // ---------- Combo ----------

        static int MovesInTurn(GameState s, Side side, string firstMove)
        {
            int count = 0;
            T.Do(s, firstMove);
            count++;
            while (s.ToMove == side)
            {
                var m = MoveGen.LegalActions(s).First(a => a.Kind == ActionKind.Move && (s.ForcedMover < 0 || a.From == s.ForcedMover));
                Rules.Apply(s, m, null);
                count++;
                Assert.Less(count, 20, "runaway combo");
            }
            return count;
        }

        [Test]
        public void Combo_plus_2_gives_three_moves_in_all_with_the_same_piece()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Combo2);
            T.Do(s, "a1a3");
            Assert.AreEqual(Side.White, s.ToMove, "still White's turn");
            Assert.AreEqual(Sq.Parse("a3"), s.ForcedMover, "only the piece that landed may move");
            Assert.IsFalse(T.IsLegal(s, "e1e2"), "other pieces are locked out");
            Assert.AreEqual(3, 1 + CountForcedMoves(s));
        }

        static int CountForcedMoves(GameState s)
        {
            int n = 0;
            while (s.ToMove == Side.White && s.ForcedMover >= 0)
            {
                var m = MoveGen.LegalActions(s).First(a => a.Kind == ActionKind.Move);
                Rules.Apply(s, m, null);
                n++;
                Assert.Less(n, 20);
            }
            return n;
        }

        [Test]
        public void Combo_plus_4_gives_five_moves_in_all()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Combo4);
            Assert.AreEqual(5, MovesInTurn(s, Side.White, "a1a3"));
        }

        [Test]
        public void Landing_on_another_Combo_adds_to_the_move_total()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Combo2);
            T.Square(s, "a4", PowerupKind.Combo2);
            // a1-a3 (+2 owed), a3-a4 lands on another +2, so 1 + 1 + (1 owed left + 2) = 5 moves.
            T.Do(s, "a1a3");
            T.Do(s, "a3a4");
            Assert.AreEqual(Side.White, s.ToMove);
            Assert.AreEqual(5, 2 + CountForcedMoves(s));
        }

        [Test]
        public void Giving_check_ends_a_combo()
        {
            // Rook lands on a Combo square on the eighth rank and checks the king, so the turn passes at once.
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a8", PowerupKind.Combo4);
            var ev = T.Do(s, "a1a8");
            Assert.AreEqual(Side.Black, s.ToMove, "check cancels the rest of the combo");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.DoubleMoveForfeited));
        }

        // ---------- save and load ----------

        [Test]
        public void A_banked_tier_survives_save_and_load()
        {
            var cfg = T.Cfg();
            var s = T.Fen("7k/8/8/8/3N4/8/8/4K3 w - - 0 1", cfg);
            T.Give(s, "d4", Buff.Fireball);
            T.Tier(s, "d4", 2);
            var code = GameStateCodec.EncodeGame(s, new GameHistory());
            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(code, cfg, out s2, out h2));
            Assert.AreEqual(2, T.At(s2, "d4").Tier);
            Assert.IsTrue(T.At(s2, "d4").Has(Buff.Fireball));
        }

        // ---------- the Chess Fighter pool ----------

        [Test]
        public void The_fighter_pool_holds_only_powers_the_engine_supports()
        {
            var pool = GameConfig.FighterPool();
            Assert.IsTrue(pool.Any(w => w.Kind == PowerupKind.Fireball));
            Assert.IsTrue(pool.Any(w => w.Kind == PowerupKind.Combo2));
            Assert.IsTrue(pool.Any(w => w.Kind == PowerupKind.Combo4));
            Assert.IsFalse(pool.Any(w => PowerupInfo.IsTransform(w.Kind)), "transform squares are gone");
            Assert.IsFalse(pool.Any(w => w.Kind == PowerupKind.Reroll || w.Kind == PowerupKind.Rewind || w.Kind == PowerupKind.Swap || w.Kind == PowerupKind.Boulder));
            Assert.IsFalse(pool.Any(w => w.Kind == PowerupKind.DoubleMove || w.Kind == PowerupKind.TripleMove), "replaced by Combo");
            Assert.IsTrue(pool.All(w => w.Weight > 0));
        }

        // ---------- whole-game stability ----------

        [Test]
        public void Random_games_with_the_fighter_pool_keep_invariants_and_terminate()
        {
            var results = new Dictionary<GameResult, int>();
            for (ulong seed = 1; seed <= 80; seed++)
            {
                var cfg = new GameConfig
                {
                    MaxPly = 250,
                    Pool = GameConfig.FighterPool(),
                    Squares = (seed % 2 == 0) ? SquareMode.Revealed : SquareMode.Mystery,
                    StartRerolls = 0,
                    SpawnInterval = 2,
                    PortalChance = 0.1,
                };
                var r = SimulationTests.PlayRandomGame(seed, null, 1500, cfg);
                results[r] = results.ContainsKey(r) ? results[r] + 1 : 1;
            }
            TestContext.Out.WriteLine(string.Join(", ", results.Select(kv => kv.Key + "=" + kv.Value)));
        }
    }
}
