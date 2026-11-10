using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ChessFighter.Core;

namespace ChessFighter.Core.Tests
{
    public class TrophyTests
    {
        static GameEvent E(EventKind k, Side side, PieceType piece = PieceType.None, PowerupKind power = PowerupKind.None, CardKind card = CardKind.None, int from = 0, int to = 0)
        {
            return new GameEvent { Kind = k, Side = side, Piece = piece, Powerup = power, Card = card, From = from, To = to };
        }

        static MatchSummary Play(params GameEvent[] events)
        {
            var t = new MatchTracker(Side.White, 2);
            t.Observe(events.ToList());
            return t.Finish(GameResult.WhiteWins, "Checkmate");
        }

        static bool Earns(string name, MatchSummary m, Profile p = null)
        {
            var t = Trophies.All.First(x => x.Name == name);
            return t.Earned(m, p ?? new Profile());
        }

        [Test]
        public void Trophies_fit_in_the_profile_bits_and_have_unique_indexes()
        {
            Assert.LessOrEqual(Trophies.All.Length, 32);
            Assert.AreEqual(Trophies.All.Length, Trophies.All.Select(t => t.Index).Distinct().Count());
            for (int i = 0; i < Trophies.All.Length; i++) Assert.AreEqual(i, Trophies.All[i].Index);
        }

        [Test]
        public void Collector_counts_every_pickup_including_instant_squares_and_not_rerolled_ones()
        {
            var ev = new List<GameEvent>();
            foreach (var k in new[] { PowerupKind.Combo2, PowerupKind.StunBolt, PowerupKind.DrawCard, PowerupKind.StealGrab, PowerupKind.Fireball })
                ev.Add(E(EventKind.PickupFound, Side.White, power: k));
            Assert.IsTrue(Earns("Collector", Play(ev.ToArray())));
            ev.Add(E(EventKind.PickupRerolled, Side.White));
            Assert.IsFalse(Earns("Collector", Play(ev.ToArray())), "a reroll gives one back");
        }

        [Test]
        public void Pyro_needs_three_fireball_kills_in_a_game()
        {
            var one = new[] { E(EventKind.FireballFired, Side.White), E(EventKind.Destroyed, Side.Black, PieceType.Pawn) };
            Assert.IsFalse(Earns("Pyro", Play(one)));
            var three = one.Concat(one).Concat(one).ToArray();
            Assert.IsTrue(Earns("Pyro", Play(three)));
        }

        [Test]
        public void Card_trophies_track_the_player_not_the_rival()
        {
            Assert.IsTrue(Earns("Nuh-uh!", Play(E(EventKind.CardCancelled, Side.White, card: CardKind.Promotion))));
            Assert.IsFalse(Earns("Nuh-uh!", Play(E(EventKind.CardCancelled, Side.Black, card: CardKind.Promotion))));
            var five = Enumerable.Range(0, 5).Select(_ => E(EventKind.CardPlayed, Side.White, card: CardKind.Promotion)).ToArray();
            Assert.IsTrue(Earns("Card Shark", Play(five)));
            Assert.IsFalse(Earns("Card Shark", Play(five.Take(4).ToArray())));
            Assert.IsTrue(Earns("Upside Down", Play(E(EventKind.CardPlayed, Side.White, card: CardKind.WorldUpsideDown))));
            Assert.IsTrue(Earns("Take Two", Play(E(EventKind.CardPlayed, Side.White, card: CardKind.TakeBack))));
            Assert.IsTrue(Earns("Caïssa's Favor", Play(E(EventKind.PieceRestored, Side.White, PieceType.Queen))));
            Assert.IsFalse(Earns("Caïssa's Favor", Play(E(EventKind.PieceRestored, Side.White, PieceType.Pawn))));
        }

        [Test]
        public void Power_trophies_hook_freeze_upgrade_and_combo()
        {
            Assert.IsTrue(Earns("Hook, Line", Play(E(EventKind.HarpoonFired, Side.White))));
            Assert.IsTrue(Earns("Deep Freeze", Play(E(EventKind.StunStruck, Side.White, PieceType.King, PowerupKind.StunBolt))));
            Assert.IsFalse(Earns("Deep Freeze", Play(E(EventKind.StunStruck, Side.White, PieceType.Rook, PowerupKind.StunBolt))));
            Assert.IsTrue(Earns("Level II", Play(E(EventKind.PowerUpgraded, Side.White))));
            var combo = Enumerable.Range(0, 5).Select(_ => E(EventKind.Moved, Side.White)).ToList();
            Assert.IsTrue(Earns("Combo Breaker", Play(combo.ToArray())));
            combo.Insert(2, E(EventKind.Moved, Side.Black));
            Assert.IsFalse(Earns("Combo Breaker", Play(combo.ToArray())), "a rival move breaks the run");
        }

        [Test]
        public void Dragon_smite_missile_and_level_trophies()
        {
            var burn = new List<GameEvent> { E(EventKind.DragonFire, Side.White) };
            burn.AddRange(Enumerable.Range(0, 3).Select(_ => E(EventKind.Destroyed, Side.Black, PieceType.Pawn)));
            Assert.IsTrue(Earns("Dragon's Breath", Play(burn.ToArray())));
            burn.Insert(1, E(EventKind.Destroyed, Side.White, PieceType.Pawn)); // own pieces do not count
            burn.RemoveAt(burn.Count - 1);
            Assert.IsFalse(Earns("Dragon's Breath", Play(burn.ToArray())));
            var smite = Enumerable.Range(0, 3).Select(_ => E(EventKind.ShivaStrike, Side.White)).ToArray();
            Assert.IsTrue(Earns("Smite", Play(smite)));
            var t = new MatchTracker(Side.White, 2);
            Assert.IsTrue(Earns("Incoming!", t.Finish(GameResult.WhiteWins, "Missile strike")));
            Assert.IsFalse(Earns("Incoming!", new MatchTracker(Side.White, 2).Finish(GameResult.WhiteWins, "Checkmate")));
        }

        [Test]
        public void A_first_win_pays_at_most_fifty_xp_in_trophies_and_level_trophies_use_the_new_level()
        {
            var p = new Profile();
            var m = new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2, Captures = 3, PowerupsCollected = 2 };
            var r = Progression.Award(p, m);
            Assert.GreaterOrEqual(r.NewTrophies.Count, 3);
            Assert.AreEqual(50, r.Breakdown.First(b => b.Key == "Trophies").Value);

            var big = new Profile { Xp = Progression.XpForLevel(10) - 10 };
            var r2 = Progression.Award(big, new MatchSummary { Me = Side.White, Result = GameResult.WhiteWins, Reason = "Checkmate", AiLevel = 2 });
            Assert.IsTrue(r2.NewTrophies.Any(t => t.Name == "Veteran"));
            Assert.IsFalse(r2.NewTrophies.Any(t => t.Name == "Master of Squares"));
        }
    }
}
