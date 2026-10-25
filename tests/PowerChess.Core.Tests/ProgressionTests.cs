using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class ProgressionTests
    {
        [Test]
        public void Levels_follow_the_xp_curve_and_cap_out()
        {
            Assert.AreEqual(1, Progression.LevelForXp(0));
            Assert.AreEqual(1, Progression.LevelForXp(99));
            Assert.AreEqual(2, Progression.LevelForXp(100));
            Assert.AreEqual(3, Progression.LevelForXp(300));
            Assert.AreEqual(Progression.MaxLevel, Progression.LevelForXp(int.MaxValue / 2));
            for (int l = 2; l <= Progression.MaxLevel; l++)
                Assert.AreEqual(l, Progression.LevelForXp(Progression.XpForLevel(l)));
        }

        [Test]
        public void The_pool_only_contains_what_a_level_has_unlocked()
        {
            var l1 = Progression.PoolFor(1).Select(w => w.Kind).ToList();
            CollectionAssert.DoesNotContain(l1, PowerupKind.Fireball);
            CollectionAssert.DoesNotContain(l1, PowerupKind.Ghost);
            CollectionAssert.Contains(l1, PowerupKind.Poison);
            Assert.AreEqual(GameConfig.DefaultPool().Count, Progression.PoolFor(Progression.MaxLevel).Count, "everything is unlocked at max level");
            var l2 = Progression.PoolFor(2).Select(w => w.Kind).ToList();
            CollectionAssert.Contains(l2, PowerupKind.Fireball);
            CollectionAssert.DoesNotContain(l2, PowerupKind.SmallBomb);
        }

        [Test]
        public void A_level_one_game_never_draws_a_locked_powerup()
        {
            var cfg = new GameConfig { Pool = Progression.PoolFor(1), Squares = SquareMode.Revealed };
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var s = GameState.NewGame(cfg, seed);
                for (int i = 0; i < 20; i++)
                {
                    Rules.TrySpawnPair(s, null);
                    for (int sq = 0; sq < 64; sq++)
                        if (s.Mystery[sq] && s.Content[sq] != 0)
                            Assert.IsTrue(Progression.Unlocked((PowerupKind)s.Content[sq], 1), "locked powerup " + (PowerupKind)s.Content[sq]);
                    for (int sq = 0; sq < 64; sq++) { s.Mystery[sq] = false; s.Content[sq] = 0; }
                }
            }
        }

        [Test]
        public void The_tracker_counts_a_fireball_volley_and_a_bomb()
        {
            var s = T.Fen("4k3/3p4/3p4/3p4/8/3P4/8/3QK3 w - - 0 1");
            T.Give(s, "d1", Buff.Fireball);
            var tracker = new MatchTracker(Side.White, 2);
            var ev = T.Do(s, "Fireball d1->d5");
            tracker.Observe(ev);
            Assert.AreEqual(3, tracker.Summary.BestFireballShot, "a queen's line burned all three");
            Assert.AreEqual(3, tracker.Summary.FireballKills);
            Assert.AreEqual(3, tracker.Summary.Captures);
            Assert.AreEqual(0, tracker.Summary.PiecesLost);

            var s2 = T.Fen("4k3/8/8/2ppp3/2pNp3/2ppp3/8/4K3 w - - 0 1");
            T.Give(s2, "d4", Buff.SmallBomb);
            var t2 = new MatchTracker(Side.White, 2);
            t2.Observe(T.Do(s2, "Detonate d4"));
            Assert.AreEqual(8, t2.Summary.BestBombKills);
        }

        [Test]
        public void The_tracker_counts_losses_for_the_tracked_side()
        {
            var s = T.Fen("4k3/8/8/8/3p4/8/8/3RK3 b - - 0 1");
            var tr = new MatchTracker(Side.White, 2);
            // Black plays d4 to d3?? then white loses nothing; instead black takes via pawn promotion capture lines: use a capture.
            var s2 = T.Fen("4k3/8/8/8/8/2p5/1R6/4K3 b - - 0 1");
            tr.Observe(T.Do(s2, "c3b2"));
            Assert.AreEqual(1, tr.Summary.PiecesLost);
            Assert.AreEqual(0, tr.Summary.Captures);
        }

        [Test]
        public void Winning_awards_xp_trophies_and_can_level_up()
        {
            var p = new Profile();
            var m = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2, Captures = 3, PowerupsCollected = 2 };
            var r = Progression.Award(p, m);
            Assert.AreEqual(1, r.OldLevel);
            Assert.AreEqual(2, r.NewLevel);
            Assert.IsTrue(r.NewPowerups.Contains(PowerupKind.Fireball));
            Assert.IsTrue(r.NewPowerups.Contains(PowerupKind.Steal));
            var names = r.NewTrophies.Select(t => t.Name).ToList();
            CollectionAssert.IsSubsetOf(new[] { "First Win", "Checkmate", "Flawless" }, names);
            Assert.AreEqual(100 + 15 + 20 + 50 + 25 * r.NewTrophies.Count, r.XpGained);
            Assert.AreEqual(1, p.Games);
            Assert.AreEqual(1, p.Mates);
            Assert.IsTrue(p.Has(Trophies.All[0]));
        }

        [Test]
        public void A_trophy_is_awarded_only_once_and_losses_still_pay_a_little()
        {
            var p = new Profile();
            var win = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2 };
            Progression.Award(p, win);
            var r2 = Progression.Award(p, win);
            Assert.IsFalse(r2.NewTrophies.Any(t => t.Name == "First Win"));
            var loss = new MatchSummary { Me = Side.White, Result = GameResult.BlackWins, Reason = "Checkmate", AiLevel = 2 };
            var r3 = Progression.Award(p, loss);
            Assert.AreEqual(25, r3.XpGained);
            Assert.AreEqual(1, p.Losses);
        }

        [Test]
        public void Harder_opponents_pay_more_and_two_player_games_pay_less()
        {
            Assert.Greater(Award(3), Award(2));
            Assert.Greater(Award(2), Award(1));
            Assert.Greater(Award(1), Award(0));
        }

        static int Award(int ai)
        {
            var m = new MatchSummary { Me = Side.White, Result = GameResult.Draw, Reason = "Stalemate", AiLevel = ai };
            m.Result = GameResult.WhiteWins;
            m.Reason = "Resign";
            return Progression.Award(new Profile(), m).Breakdown[0].Value;
        }

        [Test]
        public void The_profile_survives_the_codec_and_bad_data_is_ignored()
        {
            var p = new Profile { Xp = 1234, Games = 9, Wins = 5, Losses = 3, Draws = 1, Mates = 4, Captures = 40, Collected = 22, FireballKills = 7, Steals = 2, Teleports = 1, TrophyBits = 0b1011 };
            var q = ProfileCodec.Decode(ProfileCodec.Encode(p));
            Assert.AreEqual(1234, q.Xp);
            Assert.AreEqual(5, q.Wins);
            Assert.AreEqual(0b1011u, q.TrophyBits);
            Assert.AreEqual(7, q.FireballKills);
            foreach (var bad in new[] { null, "", "!!!", "AAAA", System.Convert.ToBase64String(new byte[] { 9, 1, 2 }) })
            {
                var f = ProfileCodec.Decode(bad);
                Assert.AreEqual(0, f.Xp);
                Assert.AreEqual(0, f.Games);
            }
            var good = System.Convert.FromBase64String(ProfileCodec.Encode(p)).Concat(new byte[] { 1 }).ToArray();
            Assert.AreEqual(0, ProfileCodec.Decode(System.Convert.ToBase64String(good)).Xp, "trailing bytes are rejected");
        }
    }
}
