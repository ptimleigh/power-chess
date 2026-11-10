using System.Linq;
using NUnit.Framework;
using ChessFighter.Core;

namespace ChessFighter.Core.Tests
{
    public class UnlocksTests
    {
        [Test]
        public void Level_one_has_no_unlock_cards_because_those_are_the_starting_set()
        {
            // Between 0 and 1 is the whole starting set; Between(1, 1) is nothing.
            Assert.AreEqual(0, Unlocks.Between(1, 1).Count);
        }

        [Test]
        public void Twenty_levels_each_unlock_something_and_nothing_twice()
        {
            Assert.AreEqual(20, Progression.MaxLevel);
            for (int l = 2; l <= 20; l++) Assert.Greater(Unlocks.AtLevel(l).Count, 0, "level " + l);
            var all = Unlocks.Between(0, 20);
            foreach (var w in GameConfig.FighterPool())
                Assert.AreEqual(1, all.Count(i => i.Type == UnlockType.Powerup && i.Kind == w.Kind), w.Kind.ToString());
            for (int c = Cards.First; c <= Cards.Last; c++)
                if ((CardKind)c != CardKind.None)
                    Assert.AreEqual(1, all.Count(i => i.Type == UnlockType.Cards && i.CardKinds.Contains((CardKind)c)), ((CardKind)c).ToString());
        }

        [Test]
        public void The_plan_puts_each_thing_on_its_level()
        {
            CollectionAssert.AreEquivalent(new[] { PowerupKind.Fireball, PowerupKind.SpikedArmor, PowerupKind.Poison }, Unlocks.AtLevel(1).Where(i => i.Type == UnlockType.Powerup).Select(i => i.Kind).ToList());
            Assert.IsTrue(Unlocks.AtLevel(2).Any(i => i.Kind == PowerupKind.SmallBomb));
            Assert.IsTrue(Unlocks.AtLevel(3).Any(i => i.Kind == PowerupKind.Ghost));
            Assert.IsTrue(Unlocks.AtLevel(4).Any(i => i.Type == UnlockType.Teleporters));
            Assert.IsTrue(Unlocks.AtLevel(5).Any(i => i.Kind == PowerupKind.StunBolt));
            Assert.IsTrue(Unlocks.AtLevel(6).Any(i => i.Kind == PowerupKind.Combo2));
            Assert.IsTrue(Unlocks.AtLevel(7).Any(i => i.Kind == PowerupKind.DrawCard));
            Assert.IsTrue(Unlocks.AtLevel(7).Any(i => i.Type == UnlockType.Cards && i.CardKinds.Contains(CardKind.Promotion)));
            Assert.IsTrue(Unlocks.AtLevel(8).Any(i => i.Kind == PowerupKind.Harpoon));
            Assert.IsTrue(Unlocks.AtLevel(9).Any(i => i.Type == UnlockType.Upgrade && i.Name == "Fireball II"));
            Assert.IsTrue(Unlocks.AtLevel(10).Any(i => i.Type == UnlockType.Frequency));
            Assert.IsTrue(Unlocks.AtLevel(10).Any(i => i.PresetId == "chaos") && Unlocks.AtLevel(10).Any(i => i.PresetId == "calm"));
            Assert.IsTrue(Unlocks.AtLevel(11).Any(i => i.Kind == PowerupKind.StealGrab));
            Assert.IsTrue(Unlocks.AtLevel(12).Any(i => i.Type == UnlockType.Cards && i.CardKinds.Contains(CardKind.NuhUh) && i.CardKinds.Contains(CardKind.TakeBack)));
            Assert.AreEqual(2, Unlocks.AtLevel(13).Count(i => i.Type == UnlockType.Upgrade));
            Assert.IsTrue(Unlocks.AtLevel(14).Any(i => i.PresetId == "ghosttown"));
            Assert.IsTrue(Unlocks.AtLevel(15).Any(i => i.Kind == PowerupKind.Combo4));
            Assert.IsTrue(Unlocks.AtLevel(16).Any(i => i.Type == UnlockType.Cards && i.CardKinds.Contains(CardKind.SummonMars)));
            Assert.IsTrue(Unlocks.AtLevel(17).Any(i => i.PresetId == "fireworks") && Unlocks.AtLevel(17).Any(i => i.PresetId == "royal"));
            Assert.IsTrue(Unlocks.AtLevel(18).Any(i => i.Type == UnlockType.Cards && i.CardKinds.Contains(CardKind.DragonStrike)));
            Assert.IsTrue(Unlocks.AtLevel(19).Any(i => i.Type == UnlockType.Cards && i.CardKinds.Contains(CardKind.MissileStrike)));
            Assert.IsTrue(Unlocks.AtLevel(20).Any(i => i.PresetId == "bigbombs") && Unlocks.AtLevel(20).Any(i => i.Type == UnlockType.Custom));
        }

        [Test]
        public void Multi_level_jumps_collect_everything_in_order_and_stop_at_the_top()
        {
            var items = Unlocks.Between(1, 4);
            Assert.IsTrue(items.Select(i => i.Level).SequenceEqual(items.Select(i => i.Level).OrderBy(l => l)));
            Assert.IsTrue(items.Any(i => i.Type == UnlockType.Teleporters));
            Assert.AreEqual(0, Unlocks.Between(20, 99).Count);
        }

        [Test]
        public void Next_unlock_level_skips_empty_levels_and_ends_at_zero()
        {
            Assert.AreEqual(2, Unlocks.NextUnlockLevel(1));
            Assert.AreEqual(0, Unlocks.NextUnlockLevel(Progression.MaxLevel));
            for (int l = 1; l < Progression.MaxLevel; l++)
            {
                int next = Unlocks.NextUnlockLevel(l);
                Assert.Greater(next, l);
                Assert.Greater(Unlocks.AtLevel(next).Count, 0);
            }
        }
    }
}
