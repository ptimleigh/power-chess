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
                    Assert.IsFalse(s.Mystery[i], "piece on an unopened square. " + context + "\n" + s);
                    if (p.Type == PieceType.King)
                        Assert.AreEqual(Buff.None, p.Buffs, "king received a buff. " + context + "\n" + s);
                }
                Assert.IsFalse(s.Boulders[i] && s.Mystery[i], "boulder on a mystery square");
            }
            Assert.AreEqual(1, whiteKings, "white kings. " + context + "\n" + s);
            Assert.AreEqual(1, blackKings, "black kings. " + context + "\n" + s);
        }

        static GameResult PlayRandomGame(ulong seed, Func<GameState, GameAction> chooser, int maxActions = 1500)
        {
            var s = GameState.NewGame(new GameConfig { MaxPly = 250 }, seed);
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
                Rules.Apply(s, action);
                AssertInvariants(s, "after " + action + " (seed " + seed + ")");
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
        public void Ai_finds_mate_in_one()
        {
            var s = T.Fen("6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1");
            var ai = new Ai(2);
            var a = ai.Choose(s);
            Assert.AreEqual("a1a8", a.ToString());
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
