using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class RevealedTests
    {
        static GameConfig Revealed(params PowerupKind[] pool)
        {
            var c = T.Cfg();
            c.Squares = SquareMode.Revealed;
            if (pool.Length > 0) c.Pool = pool.Select(k => new WeightedKind(k, 1)).ToList();
            return c;
        }

        static void Show(GameState s, string sq, PowerupKind kind)
        {
            s.Mystery[Sq.Parse(sq)] = true;
            s.Content[Sq.Parse(sq)] = (byte)kind;
        }

        [Test]
        public void Spawned_pairs_show_the_same_powerup_on_both_squares()
        {
            var cfg = Revealed();
            cfg.MysteryChance = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var s = GameState.FromFen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", cfg);
                s.Rng = new Rng(seed);
                Assert.IsTrue(Rules.TrySpawnPair(s, null));
                var squares = Enumerable.Range(0, 64).Where(i => s.Mystery[i]).ToList();
                Assert.AreEqual(2, squares.Count);
                Assert.AreNotEqual((byte)0, s.Content[squares[0]], "revealed");
                Assert.AreEqual(s.Content[squares[0]], s.Content[squares[1]], "a mirrored pair holds the same powerup");
                Assert.AreEqual(Sq.Mirror(squares[0]), squares[1]);
            }
        }

        [Test]
        public void The_hidden_fraction_follows_mystery_chance_and_classic_mode_never_reveals()
        {
            int hidden = 0, total = 0;
            var cfg = Revealed();
            cfg.MysteryChance = 0.2;
            for (ulong seed = 1; seed <= 400; seed++)
            {
                var s = GameState.FromFen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", cfg);
                s.Rng = new Rng(seed * 7919);
                Rules.TrySpawnPair(s, null);
                int sq = Enumerable.Range(0, 64).First(i => s.Mystery[i]);
                total++;
                if (s.Content[sq] == 0) hidden++;
            }
            double fraction = (double)hidden / total;
            Assert.That(fraction, Is.InRange(0.12, 0.28), "about one pair in five stays a mystery, got " + fraction);

            var classic = T.Cfg();
            var c = GameState.FromFen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", classic);
            Rules.TrySpawnPair(c, null);
            Assert.IsTrue(Enumerable.Range(0, 64).All(i => c.Content[i] == 0), "classic mode shows nothing");
        }

        [Test]
        public void Landing_on_a_revealed_square_applies_exactly_what_it_showed()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Revealed(PowerupKind.Steal));
            Show(s, "d4", PowerupKind.Poison);
            T.Do(s, "b3d4");
            Assert.IsTrue(T.At(s, "d4").Has(Buff.Poison), "the shown powerup, not a random draw");
            Assert.IsFalse(s.Mystery[Sq.Parse("d4")]);
            Assert.AreEqual((byte)0, s.Content[Sq.Parse("d4")]);
        }

        [Test]
        public void A_king_walks_over_a_buff_square_without_using_it_up()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", Revealed());
            Show(s, "e2", PowerupKind.Poison);
            T.Do(s, "e1e2");
            Assert.AreEqual(Buff.None, T.At(s, "e2").Buffs, "kings are immune to buffs");
            Assert.IsTrue(s.Mystery[Sq.Parse("e2")], "the square is still there");
            Assert.AreEqual((byte)PowerupKind.Poison, s.Content[Sq.Parse("e2")]);

            // Once the king steps off, a piece that can use it still can.
            T.Do(s, "e8d8");
            T.Do(s, "e2d2");
            Assert.IsTrue(s.Mystery[Sq.Parse("e2")]);
        }

        [Test]
        public void A_king_does_collect_a_revealed_perk()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", Revealed());
            Show(s, "e2", PowerupKind.Steal);
            T.Do(s, "e1e2");
            Assert.AreEqual(1, s.Perks(Side.White, PowerupKind.Steal));
            Assert.IsFalse(s.Mystery[Sq.Parse("e2")]);
        }

        [Test]
        public void A_capped_perk_or_a_pointless_transform_leaves_the_square_alone()
        {
            var cfg = Revealed();
            var capped = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", cfg);
            T.Perk(capped, Side.White, PowerupKind.Steal, cfg.MaxPerks);
            Show(capped, "d4", PowerupKind.Steal);
            T.Do(capped, "b3d4");
            Assert.IsTrue(capped.Mystery[Sq.Parse("d4")], "already holding the maximum, so it stays");
            Assert.AreEqual(cfg.MaxPerks, capped.Perks(Side.White, PowerupKind.Steal));

            var same = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", cfg);
            Show(same, "d4", PowerupKind.TransformKnight);
            T.Do(same, "b3d4");
            Assert.IsTrue(same.Mystery[Sq.Parse("d4")], "a knight becoming a knight does nothing, so it stays");
        }

        [Test]
        public void Reroll_still_lets_you_swap_a_revealed_powerup()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Revealed(PowerupKind.Steal));
            T.Perk(s, Side.White, PowerupKind.Reroll);
            Show(s, "d4", PowerupKind.Poison);
            T.Do(s, "b3d4");
            Assert.AreEqual(PowerupKind.Poison, s.PendingKind, "you see what it holds before deciding");
            T.Do(s, "RerollPickup");
            Assert.AreEqual(1, s.Perks(Side.White, PowerupKind.Steal), "the re-draw came from the pool");
            Assert.IsFalse(T.At(s, "d4").Has(Buff.Poison));
        }

        [Test]
        public void The_ai_lookahead_sees_what_a_revealed_square_gives_without_consuming_randomness()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Revealed());
            Show(s, "d4", PowerupKind.Fireball);
            var probe = s.Clone();
            probe.SearchMode = true;
            ulong rng = probe.Rng.State;
            Rules.Apply(probe, T.Find(s, "b3d4"));
            Assert.IsTrue(T.At(probe, "d4").Has(Buff.Fireball));
            Assert.AreEqual(rng, probe.Rng.State, "no random draw in search");
        }

        [Test]
        public void The_ai_goes_for_a_valuable_revealed_powerup()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Revealed());
            Show(s, "d4", PowerupKind.Fireball);
            Assert.AreEqual("b3d4", new Ai(2).Choose(s).ToString());
        }
    }
}
