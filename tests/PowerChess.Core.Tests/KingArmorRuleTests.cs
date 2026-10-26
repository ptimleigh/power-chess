using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    /// <summary>Pins the deliberate rule: an armored king can stand next to the enemy king and give check or mate.</summary>
    public class KingArmorRuleTests
    {
        static GameState Armored(string fen, string kingSq)
        {
            var s = GameState.FromFen(fen, GameConfig.Classic());
            int k = Sq.Parse(kingSq);
            var p = s.Cells[k];
            p.Buffs |= Buff.Armor;
            p.ArmorTurns = 3;
            s.Cells[k] = p;
            return s;
        }

        [Test]
        public void Unarmored_king_may_not_step_next_to_the_enemy_king()
        {
            var s = GameState.FromFen("4k3/8/8/8/8/8/8/4K3 w - - 0 1", GameConfig.Classic());
            var s2 = GameState.FromFen("8/4k3/8/4K3/8/8/8/8 w - - 0 1", GameConfig.Classic());
            var legal = MoveGen.LegalActions(s2);
            Assert.IsFalse(legal.Exists(a => a.Kind == ActionKind.Move && Sq.Name(a.To) == "e6"), "next to the black king");
            Assert.IsTrue(legal.Exists(a => a.Kind == ActionKind.Move && Sq.Name(a.To) == "e4"));
        }

        [Test]
        public void Armored_king_may_step_next_to_the_enemy_king_and_gives_check()
        {
            var s = Armored("8/4k3/8/4K3/8/8/8/8 w - - 0 1", "e5");
            var step = MoveGen.LegalActions(s).Find(a => a.Kind == ActionKind.Move && Sq.Name(a.To) == "e6");
            Assert.AreEqual(ActionKind.Move, step.Kind, "an armored king may approach");
            Rules.Apply(s, step);
            Assert.IsTrue(Geometry.IsKingAttacked(s, Side.Black), "the black king is in check from the armored king");
        }

        [Test]
        public void Armored_king_next_to_a_boxed_in_king_is_checkmate()
        {
            // Black king e8 boxed in by its own pieces, White's armored king steps to e7's neighbour d7/e7/f7 squares: here e7 is a pawn,
            // so the armored king takes it and gives check; every black reply is blocked, so it is mate.
            var s = GameState.FromFen("rnbqkbnr/ppppKppp/8/8/8/8/8/8 b - - 0 1", GameConfig.Classic());
            int k = Sq.Parse("e7");
            var king = s.Cells[k];
            king.Buffs |= Buff.Armor;
            king.ArmorTurns = 3;
            s.Cells[k] = king;
            Assert.IsTrue(Geometry.IsKingAttacked(s, Side.Black), "the armored king gives check");
            Assert.IsFalse(MoveGen.LegalActions(s).Exists(a => a.Kind == ActionKind.Move && Sq.Name(a.From) == "e8" && Sq.Name(a.To) == "d8"), "d8 is occupied");
            Assert.AreEqual(GameResult.WhiteWins, Rules.GetResult(s), "no black action escapes the check");
        }

        [Test]
        public void Armored_king_is_never_in_check_even_when_attacked()
        {
            var s = Armored("4r3/8/8/8/8/8/8/4K2k w - - 0 1", "e1");
            Assert.IsTrue(Geometry.IsAttacked(s, Sq.Parse("e1"), Side.Black));
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.White));
        }
    }
}
