using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
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
        public void Level_two_unlocks_bombs_ghost_and_one_transforms_card()
        {
            var items = Unlocks.AtLevel(2);
            Assert.IsTrue(items.Any(i => i.Type == UnlockType.Powerup && i.Kind == PowerupKind.SmallBomb));
            Assert.IsTrue(items.Any(i => i.Type == UnlockType.Powerup && i.Kind == PowerupKind.Ghost));
            Assert.AreEqual(1, items.Count(i => i.Type == UnlockType.Transforms), "the four basic transforms share one card");
        }

        [Test]
        public void Level_three_unlocks_big_bomb_queen_swap_and_teleporters()
        {
            var items = Unlocks.AtLevel(3);
            Assert.IsTrue(items.Any(i => i.Kind == PowerupKind.BigBomb));
            Assert.IsTrue(items.Any(i => i.Kind == PowerupKind.TransformQueen && i.Type == UnlockType.Powerup));
            Assert.IsTrue(items.Any(i => i.Kind == PowerupKind.Swap));
            Assert.IsTrue(items.Any(i => i.Type == UnlockType.Teleporters));
        }

        [Test]
        public void Level_five_unlocks_frequency_chaos_and_calm()
        {
            var items = Unlocks.AtLevel(5);
            Assert.IsTrue(items.Any(i => i.Type == UnlockType.Frequency));
            Assert.IsTrue(items.Any(i => i.PresetId == "chaos"));
            Assert.IsTrue(items.Any(i => i.PresetId == "calm"));
        }

        [Test]
        public void Presets_and_builder_arrive_on_their_levels()
        {
            Assert.IsTrue(Unlocks.AtLevel(6).Any(i => i.PresetId == "bigbombs"));
            Assert.IsTrue(Unlocks.AtLevel(7).Any(i => i.PresetId == "ghosttown"));
            Assert.IsTrue(Unlocks.AtLevel(8).Any(i => i.PresetId == "fireworks"));
            Assert.IsTrue(Unlocks.AtLevel(9).Any(i => i.PresetId == "royal"));
            Assert.IsTrue(Unlocks.AtLevel(10).Any(i => i.Type == UnlockType.Custom));
        }

        [Test]
        public void Multi_level_jumps_collect_everything_in_order_and_stop_at_the_top()
        {
            var items = Unlocks.Between(1, 4);
            Assert.IsTrue(items.Select(i => i.Level).SequenceEqual(items.Select(i => i.Level).OrderBy(l => l)));
            Assert.IsTrue(items.Any(i => i.Kind == PowerupKind.TripleMove));
            Assert.AreEqual(0, Unlocks.Between(10, 99).Count);
        }

        [Test]
        public void Every_powerup_unlock_is_listed_exactly_once_across_levels()
        {
            var all = Unlocks.Between(0, Progression.MaxLevel);
            foreach (var w in GameConfig.DefaultPool())
            {
                var k = w.Kind;
                bool basicTransform = k == PowerupKind.TransformPawn || k == PowerupKind.TransformKnight || k == PowerupKind.TransformBishop || k == PowerupKind.TransformRook;
                int n = all.Count(i => i.Type == UnlockType.Powerup && i.Kind == k);
                Assert.AreEqual(basicTransform ? 0 : 1, n, k.ToString());
            }
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
