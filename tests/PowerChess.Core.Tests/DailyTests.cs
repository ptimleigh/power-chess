using System;
using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class DailyTests
    {
        [Test]
        public void The_solver_finds_a_mate_in_one_and_respects_the_budget()
        {
            var s = T.Fen("6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1", PuzzleDef.Config());
            GameAction first;
            Assert.IsTrue(PuzzleSolver.FindSolution(s, 1, out first));
            Assert.AreEqual("a1a8", first.ToString());
            Assert.IsFalse(PuzzleSolver.CanForceMate(T.Fen("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", PuzzleDef.Config()), 1));
        }

        [Test]
        public void A_puzzle_state_carries_its_powerups_and_the_plain_state_does_not()
        {
            var def = new PuzzleDef("t", "t", "h", 1, 1, "6k1/8/8/8/8/8/8/R3K1R1 w - - 0 1", "stun:2", "g1:fireball,a1:ghost", "c3");
            var s = def.NewState();
            Assert.IsTrue(s.Cells[Sq.Parse("g1")].Has(Buff.Fireball));
            Assert.IsTrue(s.Cells[Sq.Parse("a1")].Has(Buff.Ghost));
            Assert.AreEqual(2, s.Perks(Side.White, PowerupKind.Stun));
            Assert.IsTrue(s.Boulders[Sq.Parse("c3")]);
            Assert.AreEqual(0, s.Perks(Side.White, PowerupKind.Reroll), "puzzles give no free rerolls");
            var plain = def.NewStateWithoutPowerups();
            Assert.IsFalse(plain.Cells[Sq.Parse("g1")].Has(Buff.Fireball));
            Assert.AreEqual(0, plain.Perks(Side.White, PowerupKind.Stun));
            Assert.IsTrue(plain.Boulders[Sq.Parse("c3")], "boulders are part of the board, not a powerup");
        }

        [Test]
        public void Every_library_puzzle_is_valid_solvable_and_minimal()
        {
            Assert.GreaterOrEqual(PuzzleData.All.Length, 9);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var p in PuzzleData.All)
            {
                Assert.IsTrue(ids.Add(p.Id), "duplicate id " + p.Id);
                var s = p.NewState();
                Assert.AreEqual(Side.White, s.ToMove, p.Id);
                Assert.AreNotEqual(-1, Geometry.FindKing(s, Side.White), p.Id);
                Assert.AreNotEqual(-1, Geometry.FindKing(s, Side.Black), p.Id);
                Assert.IsFalse(Geometry.IsKingAttacked(s, Side.Black), p.Id + ": black starts in check");
                Assert.IsFalse(Geometry.IsKingAttacked(s, Side.White), p.Id + ": white starts in check");
                Assert.IsNotEmpty(MoveGen.LegalActions(s), p.Id);
                Assert.IsTrue(PuzzleSolver.CanForceMate(s, p.MaxMoves), p.Id + " is not solvable within " + p.MaxMoves);
                if (p.MaxMoves > 1) Assert.IsFalse(PuzzleSolver.CanForceMate(p.NewState(), p.MaxMoves - 1), p.Id + " has a shorter solution");
                Assert.IsFalse(PuzzleSolver.CanForceMate(p.NewStateWithoutPowerups(), p.MaxMoves), p.Id + " is solvable without its powerup");
            }
        }

        [Test]
        public void Every_tier_has_puzzles_and_the_day_picks_follow_the_weekday()
        {
            for (int tier = 1; tier <= 3; tier++) Assert.IsTrue(PuzzleData.All.Any(p => p.Tier == tier), "no tier " + tier);
            // 2026-10-26 is a Monday.
            int monday = Daily.DayNumber(new DateTime(2026, 10, 26));
            Assert.AreEqual(1, Daily.Weekday(monday));
            Assert.AreEqual(1, Daily.TierFor(monday));
            Assert.AreEqual(1, Daily.TierFor(monday + 1));
            Assert.AreEqual(2, Daily.TierFor(monday + 2));
            Assert.AreEqual(2, Daily.TierFor(monday + 4));
            Assert.AreEqual(3, Daily.TierFor(monday + 5));
            Assert.AreEqual(3, Daily.TierFor(monday + 6));
            for (int d = monday; d < monday + 120; d++)
            {
                var p = Daily.PuzzleFor(d);
                Assert.AreSame(p, Daily.PuzzleFor(d), "deterministic");
                Assert.AreEqual(Daily.TierFor(d), p.Tier);
                if (PuzzleData.All.Count(q => q.Tier == p.Tier) > 1 && Daily.TierFor(d - 1) == p.Tier)
                    Assert.AreNotSame(Daily.PuzzleFor(d - 1), p, "no back-to-back repeats at day " + d);
            }
        }

        [Test]
        public void Streaks_grow_on_consecutive_days_and_reset_after_a_gap()
        {
            var p = new Profile();
            var r1 = Daily.AwardSolve(p, 100);
            Assert.AreEqual(1, p.DailyStreak);
            Assert.AreEqual(Daily.XpForStreak(1) + 25 /* Daily Dose */, r1.XpGained);
            Assert.IsNull(Daily.AwardSolve(p, 100), "the same day pays once");
            Assert.AreEqual(1, p.DailySolved);

            Daily.AwardSolve(p, 101);
            var r3 = Daily.AwardSolve(p, 102);
            Assert.AreEqual(3, p.DailyStreak);
            Assert.IsTrue(r3.NewTrophies.Any(t => t.Name == "On a Roll"));
            Assert.AreEqual(3, Daily.ActiveStreak(p, 102));
            Assert.AreEqual(3, Daily.ActiveStreak(p, 103), "still alive the day after");
            Assert.AreEqual(0, Daily.ActiveStreak(p, 104), "lapsed");

            Daily.AwardSolve(p, 105);
            Assert.AreEqual(1, p.DailyStreak, "a gap restarts the streak");
            Assert.AreEqual(3, p.BestStreak);
        }

        [Test]
        public void Streak_xp_grows_and_caps()
        {
            Assert.AreEqual(75, Daily.XpForStreak(1));
            Assert.AreEqual(90, Daily.XpForStreak(2));
            Assert.AreEqual(210, Daily.XpForStreak(10));
            Assert.AreEqual(210, Daily.XpForStreak(40));
        }

        [Test]
        public void The_profile_codec_keeps_daily_fields_and_still_reads_version_one()
        {
            var p = new Profile { Xp = 50, DailyStreak = 4, BestStreak = 9, LastDailyDay = 20000, DailySolved = 12 };
            var q = ProfileCodec.Decode(ProfileCodec.Encode(p));
            Assert.AreEqual(4, q.DailyStreak);
            Assert.AreEqual(9, q.BestStreak);
            Assert.AreEqual(20000, q.LastDailyDay);
            Assert.AreEqual(12, q.DailySolved);

            // A version 1 profile: drop the four trailing ints and rewrite the version byte.
            var bytes = Convert.FromBase64String(ProfileCodec.Encode(p)).Take(1 + 4 * 11 + 4).ToArray();
            bytes[0] = 1;
            var v1 = ProfileCodec.Decode(Convert.ToBase64String(bytes));
            Assert.AreEqual(50, v1.Xp);
            Assert.AreEqual(0, v1.DailyStreak);
        }
    }
}
