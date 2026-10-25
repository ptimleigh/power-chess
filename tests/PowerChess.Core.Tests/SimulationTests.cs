using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class SimulationTests
    {
        static void AssertInvariants(GameState s, string context)
        {
            int whiteKings = 0, blackKings = 0;
            for (int i = 0; i < 64; i++)
            {
                var p = s.Cells[i];
                if (p.Type == PieceType.King) { if (p.Side == Side.White) whiteKings++; else blackKings++; }
                if (p.Type == PieceType.Pawn)
                    Assert.That(Sq.Rank(i), Is.InRange(1, 6), "pawn on a back rank. " + context + "\n" + s);
                if (!p.IsEmpty)
                {
                    Assert.IsFalse(s.Boulders[i], "piece on a boulder. " + context + "\n" + s);
                    // Only a revealed square can have a piece standing on it (a piece that cannot use what it shows).
                    if (s.Mystery[i]) Assert.AreNotEqual((byte)0, s.Content[i], "piece on a hidden mystery square. " + context + "\n" + s);
                    if (p.Type == PieceType.King)
                        Assert.IsFalse(p.Has(Buff.Poison), "king was poisoned. " + context + "\n" + s);
                }
                Assert.IsFalse(s.Boulders[i] && s.Mystery[i], "boulder on a mystery square");
            }
            Assert.AreEqual(1, whiteKings, "white kings. " + context + "\n" + s);
            Assert.AreEqual(1, blackKings, "black kings. " + context + "\n" + s);
        }

        internal static GameResult PlayRandomGame(ulong seed, Func<GameState, GameAction> chooser, int maxActions = 1500, GameConfig cfg = null)
        {
            var s = GameState.NewGame(cfg ?? new GameConfig { MaxPly = 250 }, seed);
            var rnd = new Random((int)seed);
            for (int n = 0; n < maxActions; n++)
            {
                var result = Rules.GetResult(s);
                if (result != GameResult.Ongoing) return result;

                var legal = MoveGen.LegalActions(s);
                Assert.IsNotEmpty(legal, "ongoing game must have a legal action");
                var action = chooser != null ? chooser(s) : legal[rnd.Next(legal.Count)];
                Assert.IsTrue(legal.Contains(action), "chooser returned an illegal action " + action);

                Side mover = s.ToMove;
                int kingSq = Geometry.FindKing(s, mover);
                bool armored = kingSq >= 0 && s.Cells[kingSq].Has(Buff.Armor); // armor may lapse at the end of the turn
                Rules.Apply(s, action);
                AssertInvariants(s, "after " + action + " (seed " + seed + ")");
                if (!armored)
                    Assert.IsFalse(Geometry.IsKingAttacked(s, mover), "mover left own king in check after " + action + "\n" + s);
            }
            Assert.Fail("game did not terminate, seed " + seed);
            return GameResult.Ongoing;
        }

        [Test]
        public void Random_games_with_every_powerup_keep_invariants_and_terminate()
        {
            var results = new Dictionary<GameResult, int>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var r = PlayRandomGame(seed, null);
                results[r] = results.ContainsKey(r) ? results[r] + 1 : 1;
            }
            TestContext.Out.WriteLine(string.Join(", ", results.Select(kv => kv.Key + "=" + kv.Value)));
        }

        [Test]
        public void Random_games_in_revealed_mode_keep_invariants_and_terminate()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var cfg = new GameConfig { MaxPly = 250, Squares = SquareMode.Revealed };
                PlayRandomGame(seed, null, 1500, cfg);
            }
        }

        [Test]
        public void Random_games_with_teleporters_and_fast_spawns_keep_invariants_and_terminate()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var cfg = new GameConfig { MaxPly = 250, Squares = SquareMode.Revealed, PortalChance = 0.5, MaxPortalPairs = 2, SpawnInterval = 2 };
                PlayRandomGame(seed, null, 1500, cfg);
            }
        }

        [Test]
        public void Ai_finds_mate_in_one()
        {
            var s = T.Fen("6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1");
            var ai = new Ai(2);
            var a = ai.Choose(s);
            Assert.AreEqual("a1a8", a.ToString());
        }

        [Test]
        public void Ai_mates_instead_of_stalemating_even_at_the_easiest_depth()
        {
            // Qc7 is stalemate; Qc8 is checkmate. A depth-1 search that cannot see the reply used to be able to pick Qc7.
            var s = T.Fen("k7/8/1K6/8/8/8/8/2Q5 w - - 0 1");
            var a = new Ai(1).Choose(s);
            var after = s.Clone();
            Rules.Apply(after, a);
            Assert.AreEqual(GameResult.WhiteWins, Rules.GetResult(after), "chose " + a);
        }

        [Test]
        public void Ai_heads_for_an_unopened_mystery_square_when_nothing_else_matters()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
            s.Mystery[Sq.Parse("d4")] = true;
            Assert.AreEqual("b1c3", new Ai(2).Choose(s).ToString());
        }

        [Test]
        public void Ai_will_not_give_up_a_piece_for_a_mystery_square()
        {
            // The knight could step onto the mystery square on d5, but the pawn on e6 would simply capture it.
            var s = T.Fen("4k3/8/4p3/8/8/2N5/8/4K3 w - - 0 1");
            s.Mystery[Sq.Parse("d5")] = true;
            Assert.AreNotEqual("c3d5", new Ai(2).Choose(s).ToString());
        }

        [Test]
        public void Ai_wins_material_instead_of_hanging_pieces()
        {
            // White queen can take an undefended rook; it should not walk into the pawn's capture.
            var s = T.Fen("4k3/8/2p5/8/3r4/8/8/3QK3 w - - 0 1");
            var a = new Ai(2).Choose(s);
            Assert.AreEqual("d1d4", a.ToString());
        }

        [Test]
        public void Ai_beats_a_random_mover_with_powerups_on()
        {
            int aiWins = 0, games = 8;
            for (ulong seed = 1; seed <= (ulong)games; seed++)
            {
                var ai = new Ai(2);
                var rnd = new Random((int)seed * 31);
                var aiSide = seed % 2 == 0 ? Side.White : Side.Black;
                var r = PlayRandomGame(seed, s =>
                {
                    if (s.ToMove == aiSide) return ai.Choose(s);
                    var legal = MoveGen.LegalActions(s);
                    return legal[rnd.Next(legal.Count)];
                });
                if ((r == GameResult.WhiteWins && aiSide == Side.White) || (r == GameResult.BlackWins && aiSide == Side.Black)) aiWins++;
            }
            TestContext.Out.WriteLine("AI won " + aiWins + "/" + games);
            Assert.GreaterOrEqual(aiWins, games - 2, "AI should clearly beat random play");
        }
    }
}
