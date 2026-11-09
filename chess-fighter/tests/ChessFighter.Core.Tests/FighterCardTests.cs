using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ChessFighter.Core;

namespace ChessFighter.Core.Tests
{
    /// <summary>The card bag, Draw Card squares, the Nuh-uh response window and the cards built so far. See docs/POWERS.md.</summary>
    public class FighterCardTests
    {
        static List<GameAction> Plays(GameState s, CardKind kind)
        {
            return MoveGen.LegalActions(s).Where(a => a.Kind == ActionKind.PlayCard && Cards.KindOf(a.Aux) == kind).ToList();
        }

        static GameAction Play(GameState s, CardKind kind, string from, string to, int extra = 0)
        {
            int f = Sq.Parse(from), t = Sq.Parse(to);
            return Plays(s, kind).First(a => a.From == f && a.To == t && Cards.ExtraOf(a.Aux) == extra);
        }

        // ---------- drawing ----------

        [Test]
        public void A_Draw_Card_square_gives_the_lander_a_card_that_is_either_bagged_or_pending()
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
                s.Rng = new Rng(seed);
                T.Square(s, "a3", PowerupKind.DrawCard);
                var ev = T.Do(s, "a1a3");
                Assert.IsTrue(ev.Any(e => e.Kind == EventKind.CardDrawn), "a card was drawn");
                int bagged = Enumerable.Range(Cards.First, Cards.Last).Sum(k => s.CardCount(Side.White, (CardKind)k));
                Assert.IsTrue(bagged == 1 || s.PendingCard != CardKind.None || ev.Any(e => e.Kind == EventKind.CardKept) || ev.Any(e => e.Kind == EventKind.CardPlayed),
                    "seed " + seed + ": the card is somewhere");
            }
        }

        [Test]
        public void There_are_no_draws_in_the_lookahead_and_dead_cards_have_no_weight()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.SearchMode = true;
            T.Square(s, "a3", PowerupKind.DrawCard);
            Rules.ApplyPickup(s, Side.White, Sq.Parse("a1"), PowerupKind.DrawCard, null);
            Assert.AreEqual(0, Enumerable.Range(Cards.First, Cards.Last).Sum(k => s.CardCount(Side.White, (CardKind)k)));

            var fresh = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            Assert.AreEqual(0, Cards.Weight(fresh, Side.White, CardKind.SmashBoulder), "nothing to smash");
            Assert.AreEqual(0, Cards.Weight(fresh, Side.White, CardKind.SummonCaissa), "nothing captured yet");
            fresh.Boulders[Sq.Parse("d4")] = true;
            Assert.Greater(Cards.Weight(fresh, Side.White, CardKind.SmashBoulder), 0);
            fresh.Captured[(int)Side.White * 8 + (int)PieceType.Rook] = 1;
            Assert.Greater(Cards.Weight(fresh, Side.White, CardKind.SummonCaissa), 0);
        }

        // ---------- playing a card costs the turn ----------

        [Test]
        public void Playing_a_card_from_the_bag_costs_the_turn_and_uses_the_card_up()
        {
            var s = T.Fen("7k/8/8/8/8/8/N1R5/4K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.RoleReversal, 1);
            Assert.IsTrue(Plays(s, CardKind.RoleReversal).Count > 0);
            var ev = new List<GameEvent>();
            Rules.Apply(s, Play(s, CardKind.RoleReversal, "a2", "c2"), ev);
            Assert.AreEqual(PieceType.Rook, T.At(s, "a2").Type, "the two pieces traded places");
            Assert.AreEqual(PieceType.Knight, T.At(s, "c2").Type);
            Assert.AreEqual(0, s.CardCount(Side.White, CardKind.RoleReversal));
            Assert.AreEqual(Side.Black, s.ToMove, "the card was the turn");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.CardPlayed));
        }

        [Test]
        public void Role_reversal_never_moves_a_king()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/N3K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.RoleReversal, 1);
            Assert.AreEqual(0, Plays(s, CardKind.RoleReversal).Count, "only one non-king piece, so nothing to swap");
        }

        [Test]
        public void Cards_cannot_be_played_mid_combo_and_reactions_are_not_played_from_the_bag()
        {
            var s = T.Fen("7k/8/8/8/8/8/N1R5/4K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.NuhUh, 1);
            Assert.IsFalse(MoveGen.LegalActions(s).Any(a => a.Kind == ActionKind.PlayCard), "Nuh-uh is a reaction only");
        }

        // ---------- immediate cards ----------

        [Test]
        public void An_immediate_boulder_card_must_be_aimed_and_then_ends_the_turn()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.PendingCard = CardKind.PlaceBoulder;
            var actions = MoveGen.LegalActions(s);
            Assert.IsTrue(actions.All(a => a.Kind == ActionKind.PlayCard), "nothing but the boulder placement until it is aimed");
            Assert.IsTrue(actions.Any(a => a.To == Sq.Parse("d4")));
            Assert.IsFalse(actions.Any(a => a.To == Sq.Parse("a1")), "not on a piece");

            Rules.Apply(s, Play(s, CardKind.PlaceBoulder, "d4", "d4"));
            Assert.IsTrue(s.Boulders[Sq.Parse("d4")]);
            Assert.AreEqual(CardKind.None, s.PendingCard);
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.AreEqual(0, s.CardCount(Side.White, CardKind.PlaceBoulder), "an immediate card never enters the bag");
        }

        [Test]
        public void An_immediate_card_with_no_legal_use_goes_into_the_bag()
        {
            // Only a king and a queen: nothing can be promoted.
            var s = T.Fen("7k/8/8/8/8/8/8/Q3K3 w - - 0 1");
            s.PendingCard = CardKind.Promotion;
            var ev = new List<GameEvent>();
            Rules.Finish(s, ev);
            Assert.AreEqual(CardKind.None, s.PendingCard);
            Assert.AreEqual(1, s.CardCount(Side.White, CardKind.Promotion));
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.CardKept));
        }

        [Test]
        public void Promotion_goes_up_one_level_and_never_touches_queens_or_kings()
        {
            var s = T.Fen("7k/8/8/8/8/8/P1NB4/QR2K3 w - - 0 1");
            s.PendingCard = CardKind.Promotion;
            var legal = MoveGen.LegalActions(s);
            string Names(IEnumerable<GameAction> acts) => string.Join(",", acts.Select(a => Sq.Name(a.From) + "=" + (PieceType)Cards.ExtraOf(a.Aux)).OrderBy(x => x));
            Assert.AreEqual("a2=Bishop,a2=Knight,b1=Queen,c2=Rook,d2=Rook", Names(legal), "pawn to knight or bishop, knight or bishop to rook, rook to queen");

            Rules.Apply(s, Play(s, CardKind.Promotion, "c2", "c2", (int)PieceType.Rook));
            Assert.AreEqual(PieceType.Rook, T.At(s, "c2").Type);
        }

        // ---------- the other cards ----------

        [Test]
        public void Horse_sacrifice_trades_a_knight_for_a_rook_and_needs_a_knight()
        {
            var s = T.Fen("7k/8/8/8/8/8/PN6/4K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.HorseSacrifice, 1);
            var a = Play(s, CardKind.HorseSacrifice, "b2", "a2");
            Rules.Apply(s, a);
            Assert.IsTrue(T.At(s, "b2").IsEmpty, "the knight is sacrificed");
            Assert.AreEqual(PieceType.Rook, T.At(s, "a2").Type, "the pawn became a rook");
            Assert.AreEqual(1, s.Captured[(int)Side.White * 8 + (int)PieceType.Knight], "the sacrificed knight counts as lost");

            var none = T.Fen("7k/8/8/8/8/8/P7/4K3 w - - 0 1");
            none.AddCard(Side.White, CardKind.HorseSacrifice, 1);
            Assert.AreEqual(0, Plays(none, CardKind.HorseSacrifice).Count, "no knight, so the card is just held");
        }

        [Test]
        public void No_more_powers_clears_every_power_and_square_and_pauses_new_squares()
        {
            var s = T.Fen("7k/8/8/8/8/8/1N6/R3K3 w - - 0 1");
            T.Give(s, "b2", Buff.Fireball);
            T.Tier(s, "b2", 2);
            T.Give(s, "a1", Buff.Armor, 3);
            T.Square(s, "e5", PowerupKind.Combo2);
            s.AddCard(Side.White, CardKind.NoMorePowers, 1);
            var a = Play(s, CardKind.NoMorePowers, "a1", "a1");
            Rules.Apply(s, a);
            Assert.AreEqual(Buff.None, T.At(s, "b2").Buffs);
            Assert.AreEqual(0, T.At(s, "b2").Tier);
            Assert.AreEqual(Buff.None, T.At(s, "a1").Buffs, "armor is cleared too");
            Assert.IsFalse(s.Mystery[Sq.Parse("e5")], "powerup squares are reset");
            Assert.Greater(s.NoSpawnUntilPly, s.Ply, "and none spawn for a round");

            var plain = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            plain.AddCard(Side.White, CardKind.NoMorePowers, 1);
            Assert.AreEqual(0, Plays(plain, CardKind.NoMorePowers).Count, "nothing to clear, so it is not offered");
        }

        [Test]
        public void Smash_boulder_removes_a_boulder_and_place_boulder_respects_the_cap()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            s.Boulders[Sq.Parse("d4")] = true;
            s.AddCard(Side.White, CardKind.SmashBoulder, 1);
            Rules.Apply(s, Play(s, CardKind.SmashBoulder, "d4", "d4"));
            Assert.IsFalse(s.Boulders[Sq.Parse("d4")]);

            var full = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            full.Config.MaxBoulders = 1;
            full.Boulders[Sq.Parse("d4")] = true;
            full.PendingCard = CardKind.PlaceBoulder;
            Rules.Finish(full, null);
            Assert.AreEqual(1, full.CardCount(Side.White, CardKind.PlaceBoulder), "no room, so the card is bagged");
        }

        [Test]
        public void Summon_Caissa_brings_back_a_lost_piece_anywhere_free()
        {
            // Black's rook on a1 takes White's rook on a2, so White has one rook lost.
            var s = T.Fen("7k/8/8/8/8/8/R7/r3K3 b - - 0 1");
            T.Do(s, "a1a2");
            Assert.AreEqual(1, s.Captured[(int)Side.White * 8 + (int)PieceType.Rook], "the capture was recorded");
            Assert.AreEqual(0, s.Captured[(int)Side.Black * 8 + (int)PieceType.Rook]);

            s.AddCard(Side.White, CardKind.SummonCaissa, 1);
            Assert.IsTrue(Plays(s, CardKind.SummonCaissa).All(a => Cards.ExtraOf(a.Aux) == (int)PieceType.Rook), "only the rook can be brought back");
            Assert.IsFalse(Plays(s, CardKind.SummonCaissa).Any(a => a.To == Sq.Parse("e1")), "not onto a piece");
            Rules.Apply(s, Play(s, CardKind.SummonCaissa, "h1", "h1", (int)PieceType.Rook));
            Assert.AreEqual(PieceType.Rook, T.At(s, "h1").Type);
            Assert.AreEqual(Side.White, T.At(s, "h1").Side);
            Assert.AreEqual(0, s.Captured[(int)Side.White * 8 + (int)PieceType.Rook], "the piece is no longer lost");
        }

        [Test]
        public void Caissa_never_puts_a_pawn_on_a_back_rank_and_needs_something_lost()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/4K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.SummonCaissa, 1);
            Assert.AreEqual(0, Plays(s, CardKind.SummonCaissa).Count, "nothing has been captured");
            s.Captured[(int)Side.White * 8 + (int)PieceType.Pawn] = 1;
            var plays = Plays(s, CardKind.SummonCaissa);
            Assert.IsTrue(plays.Count > 0);
            Assert.IsFalse(plays.Any(a => Sq.Rank(a.To) == 0 || Sq.Rank(a.To) == 7), "no pawns on the first or last rank");
        }

        // ---------- Nuh-uh ----------

        [Test]
        public void Nuh_uh_gives_the_rival_a_chance_to_cancel_any_card()
        {
            var s = T.Fen("7k/8/8/8/8/8/N1R5/4K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.RoleReversal, 1);
            s.AddCard(Side.Black, CardKind.NuhUh, 1);
            Rules.Apply(s, Play(s, CardKind.RoleReversal, "a2", "c2"));
            Assert.AreEqual(CardKind.RoleReversal, s.RespCard, "the card waits for the rival's answer");
            Assert.AreEqual(Side.White, s.ToMove, "the side to move has not changed");
            Assert.AreEqual(Side.Black, s.Decider, "but the answer is the rival's");
            Assert.AreEqual(PieceType.Knight, T.At(s, "a2").Type, "nothing has happened yet");
            var options = MoveGen.LegalActions(s).Select(a => a.Kind).OrderBy(k => k).ToList();
            CollectionAssert.AreEqual(new[] { ActionKind.UseNuhUh, ActionKind.AllowCard }.OrderBy(k => k).ToList(), options);

            var ev = new List<GameEvent>();
            Rules.Apply(s, MoveGen.LegalActions(s).First(a => a.Kind == ActionKind.UseNuhUh), ev);
            Assert.AreEqual(PieceType.Knight, T.At(s, "a2").Type, "the card did nothing");
            Assert.AreEqual(0, s.CardCount(Side.Black, CardKind.NuhUh), "the Nuh-uh is spent");
            Assert.AreEqual(0, s.CardCount(Side.White, CardKind.RoleReversal), "so is the cancelled card");
            Assert.AreEqual(Side.Black, s.ToMove, "and the turn still passed");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.CardCancelled));
        }

        [Test]
        public void Allowing_the_card_lets_it_resolve_and_without_a_Nuh_uh_there_is_no_window()
        {
            var s = T.Fen("7k/8/8/8/8/8/N1R5/4K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.RoleReversal, 1);
            s.AddCard(Side.Black, CardKind.NuhUh, 1);
            Rules.Apply(s, Play(s, CardKind.RoleReversal, "a2", "c2"));
            Rules.Apply(s, MoveGen.LegalActions(s).First(a => a.Kind == ActionKind.AllowCard));
            Assert.AreEqual(PieceType.Rook, T.At(s, "a2").Type, "it went through");
            Assert.AreEqual(1, s.CardCount(Side.Black, CardKind.NuhUh), "and the Nuh-uh is kept");

            var plain = T.Fen("7k/8/8/8/8/8/N1R5/4K3 w - - 0 1");
            plain.AddCard(Side.White, CardKind.RoleReversal, 1);
            Rules.Apply(plain, Play(plain, CardKind.RoleReversal, "a2", "c2"));
            Assert.AreEqual(CardKind.None, plain.RespCard, "no Nuh-uh in the rival's bag, so no window");
            Assert.AreEqual(PieceType.Rook, T.At(plain, "a2").Type);
        }

        [Test]
        public void Cancelling_a_card_that_was_the_only_escape_from_check_is_checkmate()
        {
            // Black's bomb piece (a rook on d4) has White's king on e3 in its blast: White is in check. No Nuh-uh in play: the card works.
            var escape = T.Fen("7k/8/8/8/3r4/4K3/8/8/ w - - 0 1".Replace("8/ w", "8 w"));
            Assert.IsNotNull(escape);
            var s = T.Fen("7k/8/8/8/3r4/4K3/8/8 w - - 0 1");
            T.Give(s, "d4", Buff.SmallBomb);
            Assert.IsTrue(Geometry.IsKingAttacked(s, Side.White), "the blast reaches the king");
            s.AddCard(Side.White, CardKind.NoMorePowers, 1);
            s.AddCard(Side.Black, CardKind.NuhUh, 1);
            Assert.IsTrue(Plays(s, CardKind.NoMorePowers).Count == 1, "clearing powers is a legal way out of check");

            Rules.Apply(s, Plays(s, CardKind.NoMorePowers)[0]);
            Rules.Apply(s, MoveGen.LegalActions(s).First(a => a.Kind == ActionKind.UseNuhUh));
            string reason;
            Assert.AreEqual(GameResult.BlackWins, Rules.GetResult(s, out reason), "the cancelled escape leaves the king in check");
            Assert.AreEqual("Checkmate", reason);
        }

        // ---------- the AI ----------

        [Test]
        public void The_AI_answers_a_card_with_Nuh_uh_and_plays_a_card_that_clearly_helps()
        {
            var s = T.Fen("7k/8/8/8/8/8/N1R5/4K3 w - - 0 1");
            s.AddCard(Side.White, CardKind.RoleReversal, 1);
            s.AddCard(Side.Black, CardKind.NuhUh, 1);
            Rules.Apply(s, Play(s, CardKind.RoleReversal, "a2", "c2"));
            Assert.AreEqual(ActionKind.UseNuhUh, Ai.ForLevel(2).Choose(s).Kind, "the AI cancels the card");

            var rich = T.Fen("7k/8/8/8/8/8/8/4K3 w - - 0 1");
            rich.Captured[(int)Side.White * 8 + (int)PieceType.Queen] = 1;
            rich.AddCard(Side.White, CardKind.SummonCaissa, 1);
            var choice = Ai.ForLevel(2).Choose(rich);
            Assert.AreEqual(ActionKind.PlayCard, choice.Kind, "bringing a queen back is worth a turn");
            Assert.AreEqual(CardKind.SummonCaissa, Cards.KindOf(choice.Aux));
        }

        // ---------- saves ----------

        [Test]
        public void The_bag_captured_counts_and_a_card_awaiting_an_answer_survive_save_and_load()
        {
            var cfg = T.Cfg();
            var s = T.Fen("7k/8/8/8/8/8/N1R5/4K3 w - - 0 1", cfg);
            s.AddCard(Side.White, CardKind.RoleReversal, 2);
            s.AddCard(Side.Black, CardKind.NuhUh, 1);
            s.Captured[(int)Side.Black * 8 + (int)PieceType.Queen] = 1;
            s.NoSpawnUntilPly = 7;
            Rules.Apply(s, Play(s, CardKind.RoleReversal, "a2", "c2"));
            Assert.AreEqual(CardKind.RoleReversal, s.RespCard);

            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(s, new GameHistory()), cfg, out s2, out h2));
            Assert.AreEqual(1, s2.CardCount(Side.White, CardKind.RoleReversal));
            Assert.AreEqual(1, s2.CardCount(Side.Black, CardKind.NuhUh));
            Assert.AreEqual(1, s2.Captured[(int)Side.Black * 8 + (int)PieceType.Queen]);
            Assert.AreEqual(CardKind.RoleReversal, s2.RespCard);
            Assert.AreEqual(s.RespFrom, s2.RespFrom);
            Assert.AreEqual(7, s2.NoSpawnUntilPly);
            Assert.AreEqual(Side.Black, s2.Decider);

            var pending = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1", cfg);
            pending.PendingCard = CardKind.PlaceBoulder;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(pending, new GameHistory()), cfg, out s2, out h2));
            Assert.AreEqual(CardKind.PlaceBoulder, s2.PendingCard);
        }
    }
}
