using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class ReplayPlanTests
    {
        static GameHistory PlayMoves(int n)
        {
            var h = new GameHistory();
            var s = GameState.NewGame(GameConfig.Classic(), 1);
            for (int i = 0; i < n; i++)
            {
                var a = MoveGen.LegalActions(s)[0];
                h.Record(s, a);
                Rules.Apply(s, a);
            }
            return h;
        }

        [Test]
        public void Available_is_capped_at_ten_and_at_the_game_length()
        {
            Assert.AreEqual(0, ReplayPlan.Available(PlayMoves(0)));
            Assert.AreEqual(3, ReplayPlan.Available(PlayMoves(3)));
            Assert.AreEqual(10, ReplayPlan.Available(PlayMoves(25)));
        }

        [Test]
        public void Clamp_stays_within_one_and_available()
        {
            var h = PlayMoves(7);
            Assert.AreEqual(1, ReplayPlan.Clamp(h, 0));
            Assert.AreEqual(6, ReplayPlan.Clamp(h, 6));
            Assert.AreEqual(7, ReplayPlan.Clamp(h, 50));
        }

        [Test]
        public void A_pickup_decision_joins_the_move_that_found_the_powerup()
        {
            var cfg = GameConfig.Classic();
            var s = GameState.FromFen("4k3/8/8/8/8/8/4P3/4K3 w - - 0 1", cfg);
            s.AddPerk(Side.White, PowerupKind.Reroll, 1);
            s.Mystery[Sq.Parse("e4")] = true;
            s.Content[Sq.Parse("e4")] = (byte)PowerupKind.Fireball;
            var h = new GameHistory();
            // e2-e4 lands on the revealed square and opens a pickup decision
            var move = new GameAction(ActionKind.Move, Sq.Parse("e2"), Sq.Parse("e4"), 0);
            h.Record(s, move); Rules.Apply(s, move);
            Assert.IsTrue(s.PendingSquare >= 0, "the move should open a pickup");
            var keep = new GameAction(ActionKind.KeepPickup, 0, 0, 0);
            h.Record(s, keep); Rules.Apply(s, keep);
            Assert.AreEqual(2, h.Count);
            Assert.AreEqual(1, ReplayPlan.Available(h), "move plus keep is one ply");
            Assert.AreEqual(1, ReplayPlan.PlyStarts(h).Count);
        }
    }
}
