using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class HintTests
    {
        [Test]
        public void Suggestion_is_always_a_legal_action_in_random_games()
        {
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var cfg = new GameConfig { MaxPly = 120, Squares = SquareMode.Revealed };
                var s = GameState.NewGame(cfg, seed);
                var rnd = new System.Random((int)seed);
                for (int ply = 0; ply < 40 && Rules.GetResult(s) == GameResult.Ongoing; ply++)
                {
                    var legal = MoveGen.LegalActions(s);
                    var hint = Hints.Suggest(s);
                    Assert.IsTrue(legal.Contains(hint), "seed " + seed + " ply " + ply + ": " + hint);
                    Assert.IsFalse(string.IsNullOrEmpty(Hints.Describe(s, hint)));
                    Rules.Apply(s, legal[rnd.Next(legal.Count)]);
                }
            }
        }

        [Test]
        public void Finds_mate_in_one_and_a_free_queen()
        {
            var mate = GameState.FromFen("6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1", GameConfig.Classic());
            var h = Hints.Suggest(mate);
            Assert.AreEqual(ActionKind.Move, h.Kind);
            Assert.AreEqual("a1", Sq.Name(h.From));
            Assert.AreEqual("a8", Sq.Name(h.To));
            Assert.AreEqual("Try Rook a1 to a8", Hints.Describe(mate, h));

            var queen = GameState.FromFen("4k3/8/8/3q4/8/8/8/3QK3 w - - 0 1", GameConfig.Classic());
            var q = Hints.Suggest(queen);
            Assert.AreEqual("d1", Sq.Name(q.From));
            Assert.AreEqual("d5", Sq.Name(q.To));
            StringAssert.Contains("takes Queen", Hints.Describe(queen, q));
        }

        [Test]
        public void Suggestion_is_deterministic()
        {
            var s = GameState.NewGame(new GameConfig(), 3);
            Assert.AreEqual(Hints.Suggest(s), Hints.Suggest(s));
        }

        [Test]
        public void Honours_a_forced_double_move_continuation()
        {
            var s = GameState.FromFen("4k3/8/8/8/8/8/R7/4K3 w - - 0 1", GameConfig.Classic());
            Rules.ApplyPickup(s, Side.White, Sq.Parse("a2"), PowerupKind.DoubleMove, null);
            var first = Hints.Suggest(s);
            Rules.Apply(s, first);
            if (s.ForcedMover >= 0)
            {
                var second = Hints.Suggest(s);
                Assert.AreEqual(s.ForcedMover, second.From, "the second step must move the forced piece");
                Assert.IsTrue(MoveGen.LegalActions(s).Contains(second));
            }
        }

        [Test]
        public void Describes_a_pending_pickup_decision()
        {
            var cfg = GameConfig.Classic();
            var s = GameState.FromFen("4k3/8/8/8/8/8/4P3/4K3 w - - 0 1", cfg);
            s.AddPerk(Side.White, PowerupKind.Reroll, 1);
            s.Mystery[Sq.Parse("e4")] = true;
            s.Content[Sq.Parse("e4")] = (byte)PowerupKind.Fireball;
            Rules.Apply(s, new GameAction(ActionKind.Move, Sq.Parse("e2"), Sq.Parse("e4"), 0));
            Assert.IsTrue(s.PendingSquare >= 0);
            var h = Hints.Suggest(s);
            Assert.IsTrue(h.Kind == ActionKind.KeepPickup || h.Kind == ActionKind.RerollPickup);
            Assert.IsTrue(Hints.Describe(s, h).Contains("powerup"));
        }

        [Test]
        public void Policy_counts_free_bonus_and_buyable_hints()
        {
            Assert.AreEqual(3, HintPolicy.Remaining(0, 0));
            Assert.AreEqual(1, HintPolicy.Remaining(2, 0));
            Assert.AreEqual(0, HintPolicy.Remaining(3, 0));
            Assert.AreEqual(0, HintPolicy.Remaining(9, 0));
            Assert.AreEqual(1, HintPolicy.Remaining(3, 1));
            Assert.IsTrue(HintPolicy.CanBuyHint(3, 0, true));
            Assert.IsFalse(HintPolicy.CanBuyHint(3, 0, false));
            Assert.IsFalse(HintPolicy.CanBuyHint(1, 0, true));
        }

        [Test]
        public void Hints_cost_twenty_percent_of_the_games_xp()
        {
            var clean = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2 };
            var hinted = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2, HintsUsed = 2 };
            var a = Progression.Award(new Profile(), clean);
            var b = Progression.Award(new Profile(), hinted);
            Assert.Less(b.XpGained, a.XpGained);
            Assert.AreEqual(1.0, HintPolicy.XpScale(0));
            Assert.AreEqual(0.8, HintPolicy.XpScale(1));
            // combined with a preset's half XP
            var both = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2, HintsUsed = 1, XpScale = 0.5 };
            var c = Progression.Award(new Profile(), both);
            Assert.Less(c.XpGained, b.XpGained);
        }
    }
}
