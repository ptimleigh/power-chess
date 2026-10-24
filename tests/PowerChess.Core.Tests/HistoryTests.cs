using System.Linq;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class HistoryTests
    {
        static void Play(GameHistory h, GameState s, string text)
        {
            var a = T.Find(s, text);
            h.Record(s, a);
            Rules.Apply(s, a);
        }

        [Test]
        public void History_keeps_the_position_before_every_action_for_review()
        {
            var s = T.Fen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
            var h = new GameHistory();
            Play(h, s, "e2e4");
            Play(h, s, "e7e5");
            Assert.AreEqual(2, h.Count);
            Assert.AreEqual(PieceType.Pawn, h[0].Before.Cells[Sq.Parse("e2")].Type, "before 1.e4 the pawn is still on e2");
            Assert.IsTrue(h[0].Before.Cells[Sq.Parse("e4")].IsEmpty);
            Assert.AreEqual(PieceType.Pawn, h[1].Before.Cells[Sq.Parse("e4")].Type, "before 1...e5 the e4 pawn has moved");
            Assert.AreEqual(Side.Black, h[1].Mover);
            Assert.AreEqual(PieceType.Pawn, s.Cells[Sq.Parse("e5")].Type, "reviewing never touches the live game");
        }

        [Test]
        public void Rewind_takes_back_your_last_turn_and_the_reply_and_spends_the_perk()
        {
            var s = T.Fen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
            var h = new GameHistory();
            Play(h, s, "e2e4");
            Play(h, s, "e7e5");
            Assert.IsFalse(h.CanRewind(s, Side.White), "no Rewind perk yet");

            T.Perk(s, Side.White, PowerupKind.Rewind);
            Assert.IsTrue(h.CanRewind(s, Side.White));
            int index;
            var back = h.Rewind(s, Side.White, out index);

            Assert.AreEqual(0, index);
            Assert.AreEqual(0, h.Count, "history after the rewind point is discarded");
            Assert.AreEqual(Side.White, back.ToMove);
            Assert.AreEqual(PieceType.Pawn, back.Cells[Sq.Parse("e2")].Type);
            Assert.IsTrue(back.Cells[Sq.Parse("e4")].IsEmpty);
            Assert.IsTrue(back.Cells[Sq.Parse("e5")].IsEmpty, "the opponent's reply is undone too");
            Assert.AreEqual(0, back.Perks(Side.White, PowerupKind.Rewind), "the perk was spent");
        }

        [Test]
        public void Rewind_is_unavailable_on_the_first_turn_or_mid_turn()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Pool(PowerupKind.Poison));
            var h = new GameHistory();
            T.Perk(s, Side.White, PowerupKind.Rewind);
            Assert.IsFalse(h.CanRewind(s, Side.White), "nothing to take back on the first turn");

            T.Perk(s, Side.White, PowerupKind.Reroll);
            s.Mystery[Sq.Parse("d4")] = true;
            Play(h, s, "b3d4");
            Assert.IsTrue(s.PendingSquare >= 0, "pickup decision pending");
            Assert.IsFalse(h.CanRewind(s, Side.White), "cannot rewind in the middle of a turn");
        }

        [Test]
        public void Rewind_undoes_a_pickup_and_leaves_the_square_unopened()
        {
            var s = T.Fen("4k3/8/8/8/8/1N6/8/4K3 w - - 0 1", Pool(PowerupKind.Poison));
            var h = new GameHistory();
            s.Mystery[Sq.Parse("d4")] = true;
            Play(h, s, "b3d4");
            Assert.IsTrue(T.At(s, "d4").Has(Buff.Poison));
            Play(h, s, "e8d8");
            T.Perk(s, Side.White, PowerupKind.Rewind, 2);

            int index;
            var back = h.Rewind(s, Side.White, out index);
            Assert.IsTrue(back.Mystery[Sq.Parse("d4")], "the square is back");
            Assert.AreEqual(PieceType.Knight, back.Cells[Sq.Parse("b3")].Type);
            Assert.AreEqual(Buff.None, back.Cells[Sq.Parse("d4")].Buffs);
            Assert.AreEqual(1, back.Perks(Side.White, PowerupKind.Rewind), "one of two Rewinds spent");
        }

        [Test]
        public void Rewind_treats_a_double_move_as_one_turn()
        {
            var s = T.Fen("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
            var h = new GameHistory();
            T.Give(s, "b1", Buff.DoubleMove);
            Play(h, s, "b1c3");
            Assert.AreEqual(Side.White, s.ToMove, "second move still to come");
            Play(h, s, "c3d5");
            Play(h, s, "e8d8");
            T.Perk(s, Side.White, PowerupKind.Rewind);

            int index;
            var back = h.Rewind(s, Side.White, out index);
            Assert.AreEqual(0, index, "back to before the first half of the double move");
            Assert.AreEqual(PieceType.Knight, back.Cells[Sq.Parse("b1")].Type);
            Assert.IsTrue(back.Cells[Sq.Parse("c3")].IsEmpty);
        }

        [Test]
        public void The_ai_side_never_draws_rewind_when_disabled()
        {
            var cfg = Pool(PowerupKind.Rewind, PowerupKind.Poison);
            cfg.RewindAllowed[(int)Side.Black] = false;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var s = T.Fen("4k3/8/8/8/8/8/8/4K3 b - - 0 1", cfg);
                s.Rng = new Rng(seed);
                s.Mystery[Sq.Parse("e7")] = true;
                T.Do(s, "e8e7");
                Assert.AreEqual(0, s.Perks(Side.Black, PowerupKind.Rewind));
            }
        }

        static GameConfig Pool(params PowerupKind[] kinds)
        {
            var c = T.Cfg();
            c.Pool = kinds.Select(k => new WeightedKind(k, 1)).ToList();
            return c;
        }
    }
}
