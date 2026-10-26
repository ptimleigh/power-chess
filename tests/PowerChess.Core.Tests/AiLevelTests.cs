using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class AiLevelTests
    {
        static GameState Classic(string fen) { return GameState.FromFen(fen, GameConfig.Classic()); }

        [Test]
        public void Normal_and_up_do_not_walk_into_scholars_mate()
        {
            // 1.e4 e5 2.Bc4 Nc6 3.Qh5 and Black to move: f7 is attacked twice and only defended by the king.
            for (int level = 2; level <= 4; level++)
            {
                var s = Classic("r1bqkbnr/pppp1ppp/2n5/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR b KQkq - 0 1");
                var ai = Ai.ForLevel(level);
                var a = ai.Choose(s);
                var after = s.Clone();
                Rules.Apply(after, a);
                after.ToMove = Side.White;
                GameAction mate;
                Assert.IsFalse(PuzzleSolver.FindSolution(after, 1, out mate), $"level {level} played {a} and allowed {mate}");
            }
        }

        [Test]
        public void The_old_depth_two_blindness_is_fixed_a_mate_in_one_threat_is_seen_at_the_horizon()
        {
            // Black to move; White threatens Qxf7#. Any move that does not stop it loses at once.
            var s = Classic("r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR b KQkq - 0 1");
            var a = Ai.ForLevel(2).Choose(s);
            var after = s.Clone();
            Rules.Apply(after, a);
            GameAction mate;
            Assert.IsFalse(PuzzleSolver.FindSolution(after, 1, out mate), "played " + a);
        }

        [Test]
        public void Higher_levels_take_a_free_queen_and_see_a_mate_in_two()
        {
            var free = Classic("4k3/8/8/3q4/8/8/8/3RK3 w - - 0 1");
            foreach (int level in new[] { 2, 3, 4, 5 })
                Assert.AreEqual("d1d5", Ai.ForLevel(level).Choose(free).ToString(), "level " + level);

            // Ladder mate in two: Black king on h8 boxed by its own pawns' absence; Ra7 then Rb8#.
            var m2 = Classic("7k/8/8/8/8/8/R7/1R2K3 w - - 0 1");
            Assert.IsTrue(PuzzleSolver.CanForceMate(m2, 2), "the position really is a mate in two");
            foreach (int level in new[] { 3, 4, 5 })
            {
                var a = Ai.ForLevel(level).Choose(m2);
                Assert.IsTrue(PuzzleSolver.IsWinningFirstMove(m2, a, 2), $"level {level} played {a}");
            }
        }

        [Test]
        public void Every_level_answers_within_its_time_on_the_start_position()
        {
            foreach (int level in new[] { 1, 2, 3, 4, 5 })
            {
                var s = GameState.NewGame(new GameConfig { SpawnInterval = 3 }, 7);
                var sw = Stopwatch.StartNew();
                Ai.ForLevel(level).Choose(s);
                long limit = level >= 4 ? (level == 4 ? 3000 : 5000) : 2500;
                Assert.Less(sw.ElapsedMilliseconds, limit, $"level {level} took {sw.ElapsedMilliseconds} ms");
                TestContext.Out.WriteLine($"level {level}: {sw.ElapsedMilliseconds} ms");
            }
        }

        [Test]
        public void Level_names_and_xp_multipliers_are_ordered()
        {
            Assert.AreEqual("Master", Ai.LevelName(5));
            Assert.AreEqual("2 players", Ai.LevelName(0));
            for (int l = 1; l < 5; l++) Assert.Less(Ai.XpMultiplier(l), Ai.XpMultiplier(l + 1));
            Assert.Less(Ai.XpMultiplier(0), Ai.XpMultiplier(1));
        }

        [Test]
        public void Easy_sometimes_plays_a_random_move_but_always_a_legal_one()
        {
            var s = GameState.NewGame(new GameConfig { SpawnInterval = 0 }, 3);
            var ai = Ai.ForLevel(1);
            for (int i = 0; i < 40; i++)
            {
                var a = ai.Choose(s);
                Assert.IsTrue(MoveGen.LegalActions(s).Contains(a));
            }
        }

        [Test]
        public void Normal_slips_never_hang_a_queen_walk_into_mate_or_miss_a_mate()
        {
            var scholar = Classic("r1bqkbnr/pppp1ppp/2n5/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR b KQkq - 0 1");
            var free = Classic("4k3/8/8/3q4/8/8/8/3RK3 w - - 0 1");
            var ai = new Ai(2) { QuiescenceDepth = 2, CheckExtension = true, SlipChance = 1.0, SlipMarginCp = 80 };
            for (int i = 0; i < 40; i++)
            {
                var a = ai.Choose(scholar);
                var after = scholar.Clone();
                Rules.Apply(after, a);
                GameAction mate;
                Assert.IsFalse(PuzzleSolver.FindSolution(after, 1, out mate), "slip " + a + " allowed " + mate);
                Assert.AreEqual("d1d5", ai.Choose(free).ToString());
            }
            // A mate in one is always taken.
            var m1 = Classic("k7/8/1K6/8/8/8/8/7R w - - 0 1");
            for (int i = 0; i < 20; i++) Assert.AreEqual("h1h8", ai.Choose(m1).ToString());
        }

        [Test]
        public void Normal_slips_stay_within_the_margin_and_do_vary()
        {
            var start = GameState.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", GameConfig.Classic());
            var ai = new Ai(2) { QuiescenceDepth = 2, CheckExtension = true, SlipChance = 1.0, SlipMarginCp = 80 };
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 30; i++)
            {
                var a = ai.Choose(start);
                Assert.IsTrue(MoveGen.LegalActions(start).Contains(a));
                seen.Add(a.ToString());
            }
            Assert.Greater(seen.Count, 1, "a slipping AI should not always play the same move");
        }

        [Test]
        public void Levels_other_than_normal_never_slip()
        {
            foreach (int level in new[] { 1, 3, 4, 5 }) Assert.AreEqual(0, Ai.ForLevel(level).SlipChance);
            Assert.Greater(Ai.ForLevel(2).SlipChance, 0);
        }
    }
}
