using System.Linq;
using NUnit.Framework;
using ChessFighter.Core;

namespace ChessFighter.Core.Tests
{
    public class FighterLevelTests
    {
        static GameState WithLevel(int level)
        {
            var cfg = new GameConfig { Pool = FighterUnlocks.PoolFor(level), UnlockLevel = level, SpawnInterval = 0, InitialPairs = 0 };
            return GameState.FromFen("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", cfg);
        }

        [Test]
        public void Locked_cards_have_no_draw_weight_and_open_ones_do()
        {
            var s = WithLevel(6);
            Assert.AreEqual(0, Cards.Weight(s, Side.White, CardKind.Promotion), "cards start at level 7");
            Assert.AreEqual(0, Cards.Weight(s, Side.White, CardKind.MissileStrike));
            s = WithLevel(7);
            Assert.Greater(Cards.Weight(s, Side.White, CardKind.Promotion), 0);
            Assert.AreEqual(0, Cards.Weight(s, Side.White, CardKind.NuhUh), "Nuh-uh is a level 12 card");
            s = WithLevel(-1);
            Assert.Greater(Cards.Weight(s, Side.White, CardKind.MissileStrike), 0, "a level below zero opens everything");
        }

        [Test]
        public void A_second_fireball_square_upgrades_only_once_tier_two_is_open()
        {
            foreach (var (level, upgraded) in new[] { (8, false), (9, true) })
            {
                var s = WithLevel(level);
                var p = s.Cells[Sq.Parse("a1")]; p.Buffs |= Buff.Fireball; p.Tier = 1; s.Cells[Sq.Parse("a1")] = p;
                s.Mystery[Sq.Parse("a4")] = true; s.Content[Sq.Parse("a4")] = (byte)PowerupKind.Fireball;
                T.Do(s, "a1a4");
                Assert.AreEqual(upgraded ? 2 : 1, s.Cells[Sq.Parse("a4")].Tier, "level " + level);
            }
        }
    }
}
