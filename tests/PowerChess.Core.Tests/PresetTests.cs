using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class PresetTests
    {
        [Test]
        public void Frequency_sets_spawn_and_cap()
        {
            var cfg = new GameConfig();
            Presets.Apply(cfg, Presets.Classic, Frequency.Chaos, "", 5);
            Assert.AreEqual(1, cfg.SpawnInterval);
            Assert.AreEqual(12, cfg.MaxMysterySquares);
            Presets.Apply(cfg, Presets.Classic, Frequency.Rare, "", 5);
            Assert.AreEqual(5, cfg.SpawnInterval);
            Assert.AreEqual(5, cfg.MaxMysterySquares);
        }

        [Test]
        public void Locked_choices_fall_back_to_standard()
        {
            var cfg = new GameConfig();
            Presets.Apply(cfg, "bigbombs", Frequency.Chaos, "", 3);
            Assert.AreEqual(3, cfg.SpawnInterval);
            Assert.Greater(cfg.Pool.Count, 5);
            Assert.IsTrue(Presets.IsStandard("bigbombs", Frequency.Chaos, 3));
            Assert.IsFalse(Presets.Available("custom", 9));
            Assert.IsTrue(Presets.Available("custom", 10));
        }

        [Test]
        public void Big_bombs_pool_is_only_big_bombs_even_below_its_unlock_level()
        {
            var cfg = new GameConfig();
            Presets.Apply(cfg, "bigbombs", Frequency.Normal, "", 6);
            Assert.AreEqual(1, cfg.Pool.Count);
            Assert.AreEqual(PowerupKind.BigBomb, cfg.Pool[0].Kind);
        }

        [Test]
        public void Every_preset_has_a_pool_and_unlocks_one_per_level_from_five()
        {
            foreach (var p in Presets.All)
            {
                var cfg = new GameConfig();
                Presets.Apply(cfg, p.Id, Frequency.Normal, "", 10);
                Assert.Greater(cfg.Pool.Count, 0, p.Id);
            }
            Assert.AreEqual(5, Presets.All.Min(p => p.UnlockLevel));
            Assert.AreEqual(9, Presets.All.Max(p => p.UnlockLevel));
        }

        [Test]
        public void Ghost_town_adds_teleporters()
        {
            var cfg = new GameConfig();
            Presets.Apply(cfg, "ghosttown", Frequency.Normal, "", 7);
            Assert.Greater(cfg.PortalChance, 0);
        }

        [Test]
        public void Custom_builder_toggles_and_weights()
        {
            var kinds = Presets.CustomKinds();
            string c = Presets.DefaultCustom();
            int fire = kinds.IndexOf(PowerupKind.Fireball);
            c = Presets.SetCustom(c, fire, 0);
            int steal = kinds.IndexOf(PowerupKind.Steal);
            c = Presets.SetCustom(c, steal, 3);
            var pool = Presets.CustomPool(c);
            Assert.IsFalse(pool.Any(w => w.Kind == PowerupKind.Fireball));
            Assert.AreEqual(27, pool.First(w => w.Kind == PowerupKind.Steal).Weight);
            var cfg = new GameConfig();
            Presets.Apply(cfg, "custom", Frequency.Normal, c, 10);
            Assert.AreEqual(pool.Count, cfg.Pool.Count);
        }

        [Test]
        public void Custom_with_everything_off_still_draws_something()
        {
            string c = new string('0', Presets.CustomKinds().Count);
            var cfg = new GameConfig();
            Presets.Apply(cfg, "custom", Frequency.Normal, c, 10);
            Assert.Greater(cfg.Pool.Count, 0);
        }

        [Test]
        public void Non_standard_games_pay_half_xp()
        {
            Assert.AreEqual(1.0, Presets.XpScale(Presets.Classic, Frequency.Normal, 10));
            Assert.AreEqual(0.5, Presets.XpScale("bigbombs", Frequency.Normal, 10));
            Assert.AreEqual(0.5, Presets.XpScale(Presets.Classic, Frequency.Chaos, 10));
            Assert.AreEqual(1.0, Presets.XpScale("bigbombs", Frequency.Normal, 2));

            var full = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2 };
            var half = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2, XpScale = 0.5 };
            var a = Progression.Award(new Profile(), full);
            var b = Progression.Award(new Profile(), half);
            Assert.Less(b.XpGained, a.XpGained);
        }

        [Test]
        public void Random_games_with_each_preset_terminate_and_stay_legal()
        {
            foreach (var p in Presets.All)
            {
                var cfg = new GameConfig { Squares = SquareMode.Revealed };
                Presets.Apply(cfg, p.Id, Frequency.Normal, "", 10);
                cfg.MaxPly = 250;
                for (ulong seed = 1; seed <= 6; seed++)
                    SimulationTests.PlayRandomGame(seed, null, 1500, cfg);
            }
        }
    }
}
