using System;
using NUnit.Framework;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    public class CodecTests
    {
        static void AssertSame(GameState a, GameState b, string context)
        {
            for (int i = 0; i < 64; i++)
            {
                Assert.AreEqual(a.Cells[i].Type, b.Cells[i].Type, context + " type " + Sq.Name(i));
                Assert.AreEqual(a.Cells[i].Side, b.Cells[i].Side, context + " side " + Sq.Name(i));
                Assert.AreEqual(a.Cells[i].Buffs, b.Cells[i].Buffs, context + " buffs " + Sq.Name(i));
                Assert.AreEqual(a.Cells[i].ArmorTurns, b.Cells[i].ArmorTurns, context + " armor " + Sq.Name(i));
                Assert.AreEqual(a.Cells[i].StunTurns, b.Cells[i].StunTurns, context + " stun " + Sq.Name(i));
                Assert.AreEqual(a.Boulders[i], b.Boulders[i], context + " boulder " + Sq.Name(i));
                Assert.AreEqual(a.Mystery[i], b.Mystery[i], context + " mystery " + Sq.Name(i));
            }
            CollectionAssert.AreEqual(a.Inventory, b.Inventory, context + " inventory");
            Assert.AreEqual(a.ToMove, b.ToMove, context);
            Assert.AreEqual(a.Castling, b.Castling, context);
            Assert.AreEqual(a.EnPassant, b.EnPassant, context);
            Assert.AreEqual(a.HalfmoveClock, b.HalfmoveClock, context);
            Assert.AreEqual(a.Ply, b.Ply, context);
            Assert.AreEqual(a.Rng.State, b.Rng.State, context + " rng");
            Assert.AreEqual(a.PendingSquare, b.PendingSquare, context);
            Assert.AreEqual(a.PendingKind, b.PendingKind, context);
            Assert.AreEqual(a.ForcedMover, b.ForcedMover, context);
        }

        [Test]
        public void Games_in_progress_round_trip_exactly_including_history()
        {
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var cfg = new GameConfig();
                var s = GameState.NewGame(cfg, seed);
                var h = new GameHistory();
                var rnd = new Random((int)seed);
                int stopAt = 10 + rnd.Next(120);
                for (int n = 0; n < stopAt && Rules.GetResult(s) == GameResult.Ongoing; n++)
                {
                    var legal = MoveGen.LegalActions(s);
                    var a = legal[rnd.Next(legal.Count)];
                    h.Record(s, a);
                    Rules.Apply(s, a);
                }

                var text = GameStateCodec.EncodeGame(s, h);
                GameState s2;
                GameHistory h2;
                Assert.IsTrue(GameStateCodec.TryDecodeGame(text, cfg, out s2, out h2), "decode, seed " + seed);
                AssertSame(s, s2, "seed " + seed + " current");
                Assert.AreEqual(h.Count, h2.Count);
                for (int i = 0; i < h.Count; i++)
                {
                    AssertSame(h[i].Before, h2[i].Before, "seed " + seed + " history " + i);
                    Assert.AreEqual(h[i].Action, h2[i].Action);
                }

                // A restored game plays on exactly like the original.
                var legalA = MoveGen.LegalActions(s);
                var legalB = MoveGen.LegalActions(s2);
                CollectionAssert.AreEqual(legalA, legalB, "same legal actions after restore, seed " + seed);
            }
        }

        [Test]
        public void A_restored_game_can_still_rewind()
        {
            var cfg = T.Cfg();
            var s = T.Fen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", cfg);
            var h = new GameHistory();
            foreach (var m in new[] { "e2e4", "e7e5" })
            {
                var a = T.Find(s, m);
                h.Record(s, a);
                Rules.Apply(s, a);
            }
            T.Perk(s, Side.White, PowerupKind.Rewind);

            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(s, h), cfg, out s2, out h2));
            Assert.IsTrue(h2.CanRewind(s2, Side.White));
            int index;
            var back = h2.Rewind(s2, Side.White, out index);
            Assert.AreEqual(PieceType.Pawn, back.Cells[Sq.Parse("e2")].Type);
        }

        [Test]
        public void Garbage_and_wrong_versions_are_rejected_without_throwing()
        {
            GameState s;
            GameHistory h;
            var cfg = T.Cfg();
            Assert.IsFalse(GameStateCodec.TryDecodeGame("", cfg, out s, out h));
            Assert.IsFalse(GameStateCodec.TryDecodeGame("not base64 at all!", cfg, out s, out h));
            Assert.IsFalse(GameStateCodec.TryDecodeGame(Convert.ToBase64String(new byte[] { 99, 1, 2, 3 }), cfg, out s, out h), "wrong version");

            var good = GameStateCodec.EncodeGame(GameState.NewGame(cfg, 1), new GameHistory());
            var bytes = Convert.FromBase64String(good);
            Assert.IsFalse(GameStateCodec.TryDecodeGame(Convert.ToBase64String(bytes, 0, bytes.Length - 5), cfg, out s, out h), "truncated");
            bytes[1] = 77; // corrupt the first piece's type
            Assert.IsFalse(GameStateCodec.TryDecodeGame(Convert.ToBase64String(bytes), cfg, out s, out h), "corrupt piece");
        }
    }
}
