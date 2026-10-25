using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class AdTests
    {
        const long Min = 60 * 1000;

        [Test]
        public void No_interstitial_in_the_first_games_of_an_install()
        {
            var p = new AdPolicy();
            Assert.IsFalse(p.ShouldShowInterstitial(0, 100 * Min, false));
            Assert.IsFalse(p.ShouldShowInterstitial(1, 100 * Min, false));
            Assert.IsFalse(p.ShouldShowInterstitial(2, 100 * Min, false));
            Assert.IsTrue(p.ShouldShowInterstitial(3, 100 * Min, false));
        }

        [Test]
        public void Never_before_a_daily_puzzle()
        {
            Assert.IsFalse(new AdPolicy().ShouldShowInterstitial(50, 100 * Min, true));
        }

        [Test]
        public void Needs_three_games_and_four_minutes_since_the_last_one()
        {
            var p = new AdPolicy();
            p.RecordShown(3, 100 * Min);
            Assert.IsFalse(p.ShouldShowInterstitial(4, 200 * Min, false), "only one game since");
            Assert.IsFalse(p.ShouldShowInterstitial(6, 102 * Min, false), "too soon in time");
            Assert.IsTrue(p.ShouldShowInterstitial(6, 105 * Min, false));
        }

        [Test]
        public void Rewarded_double_xp_applies_once()
        {
            var profile = new Profile();
            var m = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2 };
            var r = Progression.Award(profile, m);
            int xp = profile.Xp, gained = r.XpGained;
            Assert.IsTrue(RewardedXp.Double(profile, r));
            Assert.AreEqual(xp + gained, profile.Xp);
            Assert.AreEqual(gained * 2, r.XpGained);
            Assert.IsFalse(RewardedXp.Double(profile, r));
            Assert.AreEqual(xp + gained, profile.Xp);
        }

        [Test]
        public void Doubling_can_level_you_up_and_lists_the_new_unlocks()
        {
            var profile = new Profile { Xp = 50 };
            var r = new MatchReward { OldLevel = profile.Level, XpGained = 20 };
            Assert.AreEqual(1, r.OldLevel);
            Assert.IsTrue(RewardedXp.Double(profile, r));
            Assert.AreEqual(profile.Level, r.NewLevel);
            Assert.Greater(r.NewLevel, r.OldLevel);
            Assert.IsTrue(r.NewPowerups.Contains(PowerupKind.SmallBomb));
        }
    }
}
