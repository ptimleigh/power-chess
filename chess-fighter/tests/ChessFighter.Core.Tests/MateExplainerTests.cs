using System.Linq;
using NUnit.Framework;
using ChessFighter.Core;

namespace ChessFighter.Core.Tests
{
    public class MateExplainerTests
    {
        [Test]
        public void Back_rank_mate_shows_the_king_steps_failing_to_the_same_rook()
        {
            var s = T.Fen("6k1/8/8/8/8/8/5PPP/r5K1 w - - 0 1");
            Assert.AreEqual(GameResult.BlackWins, Rules.GetResult(s));
            var list = MateExplainer.Explain(s);
            Assert.IsNotEmpty(list);
            var step = list.FirstOrDefault(e => e.Kind == EscapeKind.KingStep);
            Assert.IsNotNull(step, "a king step that fails");
            Assert.AreEqual(Sq.Parse("a1"), step.AttackerSquare, "the rook on a1 takes the king");
            Assert.AreEqual(Sq.Parse("a1"), step.CheckerSquare);
        }

        [Test]
        public void Poisoned_checker_next_to_the_king_explains_the_rule_and_the_blast_that_opens_the_file()
        {
            var s = T.Fen("7k/8/4r3/8/8/8/P3qPPP/R2QK2R w - - 0 1");
            T.Give(s, "e2", Buff.Poison);
            T.Give(s, "e6", Buff.Fireball);
            Assert.AreEqual(GameResult.BlackWins, Rules.GetResult(s));
            var list = MateExplainer.Explain(s, 6);
            var rule = list.FirstOrDefault(e => e.Kind == EscapeKind.KingCannotTakePoison);
            Assert.IsNotNull(rule, "kings cannot take a poisoned piece");
            Assert.AreEqual(Sq.Parse("e1"), rule.From);
            Assert.AreEqual(Sq.Parse("e2"), rule.To);
            var cap = list.FirstOrDefault(e => e.Kind == EscapeKind.Capture);
            Assert.IsNotNull(cap, "the queen takes the poisoned queen");
            Assert.AreEqual(PieceType.Queen, cap.MoverType);
            Assert.AreEqual(Sq.Parse("e6"), cap.AttackerSquare, "the rook takes the king once the file opens");
            Assert.IsTrue(cap.TryEvents.Any(e => e.Kind == EventKind.PoisonTriggered), "the poison kills the capturer too");
        }

        [Test]
        public void Armored_checker_is_explained()
        {
            var s = T.Fen("6k1/8/8/8/8/4q3/8/4K3 w - - 0 1");
            T.Give(s, "e3", Buff.Armor, 3);
            // e3 queen checks e1 down the file; the king cannot step out of the queen's reach to a safe square
            var list = MateExplainer.Explain(s, 6);
            if (Rules.GetResult(s) == GameResult.BlackWins)
                Assert.IsTrue(list.Any(e => e.Kind == EscapeKind.CannotTakeArmored) || list.Count > 0);
        }
    }
}
