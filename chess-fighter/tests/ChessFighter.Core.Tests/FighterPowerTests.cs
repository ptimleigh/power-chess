using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ChessFighter.Core;

namespace ChessFighter.Core.Tests
{
    /// <summary>Chess Fighter's banked moves (Fireball I/II, Bomb tiers) and instant Combo squares. See docs/POWERS.md.</summary>
    public class FighterPowerTests
    {
        // ---------- Fireball I and II ----------

        [Test]
        public void Fireball_I_fires_only_along_ranks_and_files()
        {
            // Knight on d4 with a rook to hit straight up the file and a bishop on the diagonal.
            var s = T.Fen("3rk3/6b1/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.Fireball);
            T.Tier(s, "d4", 1);
            Assert.IsTrue(T.IsLegal(s, "Fireball d4->d8"), "up the d-file");
            Assert.IsFalse(T.IsLegal(s, "Fireball d4->g7"), "the diagonal is out of reach at tier I");
        }

        [Test]
        public void Fireball_II_also_fires_diagonally()
        {
            var s = T.Fen("4k3/6b1/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.Fireball);
            T.Tier(s, "d4", 2);
            T.Do(s, "Fireball d4->g7");
            Assert.IsTrue(T.At(s, "g7").IsEmpty);
            Assert.AreEqual(PieceType.Knight, T.At(s, "d4").Type, "the shooter stays where it is");
            Assert.AreEqual(Side.Black, s.ToMove, "firing uses the turn");
        }

        [Test]
        public void Firing_spends_the_fireball_and_its_tier()
        {
            var s = T.Fen("3rk3/8/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.Fireball);
            T.Tier(s, "d4", 2);
            T.Do(s, "Fireball d4->d8");
            Assert.IsFalse(T.At(s, "d4").Has(Buff.Fireball));
            Assert.AreEqual(0, T.At(s, "d4").Tier);
        }

        [Test]
        public void A_fireball_never_removes_a_king_and_is_stopped_by_boulders()
        {
            // The black king is the first piece up the d-file: there is nothing to fire at.
            var king = T.Fen("3k4/8/8/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(king, "d4", Buff.Fireball);
            Assert.IsFalse(MoveGen.LegalActions(king).Any(a => a.Kind == ActionKind.Fireball));

            var rock = T.Fen("3rk3/8/8/8/3N4/8/8/4K3 w - - 0 1");
            rock.Boulders[Sq.Parse("d6")] = true;
            T.Give(rock, "d4", Buff.Fireball);
            Assert.IsFalse(T.IsLegal(rock, "Fireball d4->d8"), "a boulder stops the shot");
        }

        [Test]
        public void A_friendly_piece_in_front_stops_the_shot()
        {
            var s = T.Fen("3rk3/8/3P4/8/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.Fireball);
            Assert.IsFalse(T.IsLegal(s, "Fireball d4->d8"));
        }

        // ---------- a banked fireball threatens like a rook (I) or a queen (II) ----------

        [Test]
        public void Fireball_I_gives_check_along_its_lines_like_a_rook()
        {
            // White knight on e3 and the black king on e8 share an open file.
            var s = T.Fen("4k3/8/8/8/8/4N3/8/4K3 b - - 0 1");
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.Black), "no fireball, no threat");
            T.Give(s, "e3", Buff.Fireball);
            T.Tier(s, "e3", 1);
            Assert.IsTrue(Geometry.IsKingAttacked(s, Side.Black), "a banked fireball down the open file is check");
        }

        [Test]
        public void Fireball_I_does_not_threaten_diagonals_but_II_does()
        {
            // Black king a8, white knight h1: they share the long diagonal.
            var d = T.Fen("k7/8/8/8/8/8/8/4K2N b - - 0 1");
            T.Give(d, "h1", Buff.Fireball);
            T.Tier(d, "h1", 1);
            Assert.IsFalse(Geometry.IsKingAttacked(d, Side.Black), "Fireball I does not reach diagonals");
            T.Tier(d, "h1", 2);
            Assert.IsTrue(Geometry.IsKingAttacked(d, Side.Black), "Fireball II reaches the long diagonal like a queen");
        }

        [Test]
        public void A_blocker_resolves_a_fireball_check_and_a_stunned_piece_threatens_nothing()
        {
            var s = T.Fen("4k3/4p3/8/8/8/4N3/8/4K3 b - - 0 1");
            T.Give(s, "e3", Buff.Fireball);
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.Black), "the pawn on e7 is the first piece in line");

            var chk = T.Fen("4k3/8/8/8/8/4N3/8/4K3 b - - 0 1");
            T.Give(chk, "e3", Buff.Fireball);
            Assert.IsTrue(Geometry.IsKingAttacked(chk, Side.Black));
            var p = chk.Cells[Sq.Parse("e3")];
            p.StunTurns = 1;
            chk.Cells[Sq.Parse("e3")] = p;
            Assert.IsFalse(Geometry.IsKingAttacked(chk, Side.Black), "a stunned piece cannot fire");
        }

        [Test]
        public void A_king_cannot_step_onto_a_square_a_banked_fireball_covers()
        {
            // White knight on d1 holds Fireball I and covers the whole d-file; black king on e8 may not go to d8.
            var s = T.Fen("4k3/8/8/8/8/8/8/3NK3 b - - 0 1");
            T.Give(s, "d1", Buff.Fireball);
            Assert.IsFalse(T.IsLegal(s, "e8d8"), "d8 is on the fireball's file");
            Assert.IsTrue(T.IsLegal(s, "e8e7"), "e7 is not");
        }

        [Test]
        public void A_fireball_can_give_checkmate()
        {
            // Black king a8 boxed in by its own pawns; a white knight on h8 holding Fireball I covers the eighth rank.
            var s = T.Fen("k6N/pp6/8/8/8/8/8/K7 b - - 0 1");
            T.Give(s, "h8", Buff.Fireball);
            T.Tier(s, "h8", 1);
            string reason;
            Assert.AreEqual(GameResult.WhiteWins, Rules.GetResult(s, out reason));
            Assert.AreEqual("Checkmate", reason);
        }

        // ---------- banking, tiers and replacement ----------

        [Test]
        public void Landing_on_Fireball_banks_I_then_II_then_maxes_out_and_uses_up_the_square()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Fireball);
            T.Square(s, "a5", PowerupKind.Fireball);
            T.Square(s, "a6", PowerupKind.Fireball);

            T.Do(s, "a1a3");
            Assert.IsTrue(T.At(s, "a3").Has(Buff.Fireball));
            Assert.AreEqual(1, T.At(s, "a3").Tier);
            Assert.IsFalse(s.Mystery[Sq.Parse("a3")], "square used up");

            T.Do(s, "h8h7");
            var ev = T.Do(s, "a3a5");
            Assert.AreEqual(2, T.At(s, "a5").Tier, "landing again upgrades to II");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PowerUpgraded));

            T.Do(s, "h7h8");
            ev = T.Do(s, "a5a6");
            Assert.AreEqual(2, T.At(s, "a6").Tier, "II is the top");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PowerMaxed));
            Assert.IsFalse(s.Mystery[Sq.Parse("a6")], "the square goes back to normal even though nothing changed");
        }

        [Test]
        public void Landing_on_a_different_banked_power_replaces_the_old_one_at_tier_I()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Give(s, "a1", Buff.Fireball);
            T.Tier(s, "a1", 2);
            T.Square(s, "a3", PowerupKind.Poison);

            T.Do(s, "a1a3");
            var rook = T.At(s, "a3");
            Assert.IsFalse(rook.Has(Buff.Fireball), "the fireball is lost");
            Assert.IsTrue(rook.Has(Buff.Poison));
            Assert.AreEqual(1, rook.Tier, "the new power starts at tier I");
        }

        [Test]
        public void Ghost_and_Poison_stay_at_I_when_landed_on_again()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Ghost);
            T.Square(s, "a5", PowerupKind.Ghost);
            T.Do(s, "a1a3");
            Assert.IsTrue(T.At(s, "a3").Has(Buff.Ghost));
            T.Do(s, "h8h7");
            var ev = T.Do(s, "a3a5");
            Assert.IsTrue(T.At(s, "a5").Has(Buff.Ghost));
            Assert.AreEqual(1, T.At(s, "a5").Tier);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PowerMaxed));
            Assert.IsFalse(s.Mystery[Sq.Parse("a5")]);
        }

        [Test]
        public void Bomb_upgrades_to_the_big_blast_on_a_second_landing_and_kings_refuse_it()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.SmallBomb);
            T.Square(s, "a5", PowerupKind.SmallBomb);
            T.Do(s, "a1a3");
            Assert.IsTrue(T.At(s, "a3").Has(Buff.SmallBomb));
            T.Do(s, "h8h7");
            T.Do(s, "a3a5");
            Assert.IsTrue(T.At(s, "a5").Has(Buff.BigBomb));
            Assert.IsFalse(T.At(s, "a5").Has(Buff.SmallBomb));
            Assert.AreEqual(2, T.At(s, "a5").Tier);

            var k = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(k, "e2", PowerupKind.SmallBomb);
            var ev = T.Do(k, "e1e2");
            Assert.IsFalse(T.At(k, "e2").Has(Buff.SmallBomb), "a king cannot take a bomb");
            Assert.IsFalse(k.Mystery[Sq.Parse("e2")], "and the square is used up");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PickupRejected));
        }

        // ---------- Bomb: self-destruct, chains and check ----------

        [Test]
        public void An_armored_bomber_survives_its_own_blast_but_the_bomb_is_spent()
        {
            var s = T.Fen("k7/8/8/8/3N4/2p5/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.SmallBomb);
            T.Give(s, "d4", Buff.Armor, 3);
            T.Do(s, "Detonate d4");
            Assert.IsTrue(T.At(s, "c3").IsEmpty, "enemy pawn destroyed");
            Assert.AreEqual(PieceType.Knight, T.At(s, "d4").Type, "armor protects the bomber");
            Assert.IsFalse(T.At(s, "d4").Has(Buff.SmallBomb));
        }

        [Test]
        public void A_blast_sets_off_every_bomb_piece_it_reaches_in_a_chain()
        {
            // d4 white bomber (3x3). A black bomb piece on e5 is caught, and its own 3x3 reaches f6, outside the first blast.
            var s = T.Fen("k7/8/5r2/4n3/3N4/8/8/4K3 w - - 0 1");
            T.Give(s, "d4", Buff.SmallBomb);
            T.Give(s, "e5", Buff.SmallBomb);
            var far = s.Cells[Sq.Parse("g7")] = new Piece(PieceType.Rook, Side.Black);
            Assert.AreEqual(PieceType.Rook, far.Type);
            var ev = T.Do(s, "Detonate d4");
            Assert.IsTrue(T.At(s, "e5").IsEmpty, "the caught bomb piece is destroyed");
            Assert.IsTrue(T.At(s, "f6").IsEmpty, "its blast reached f6, beyond the first blast");
            Assert.AreEqual(PieceType.Rook, T.At(s, "g7").Type, "but not g7");
            Assert.AreEqual(2, ev.Count(e => e.Kind == EventKind.Detonated), "two detonations");
        }

        [Test]
        public void A_king_inside_a_bomb_blast_is_in_check()
        {
            // White rook on d4 is the bomber; the black king on e3 is inside the 3x3 around it but not attacked by the rook itself.
            var s = T.Fen("8/8/8/8/3R4/4k3/8/K7 b - - 0 1");
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.Black));
            T.Give(s, "d4", Buff.SmallBomb);
            Assert.IsTrue(Geometry.IsKingAttacked(s, Side.Black), "e3 is inside the 3x3 around d4");
            Assert.IsTrue(T.IsLegal(s, "e3f3"), "stepping out of the zone resolves it");
            Assert.IsTrue(T.IsLegal(s, "e3e2"), "so does stepping to e2");

            // An armored king cannot be checked.
            var armored = T.Fen("8/8/8/8/3R4/4k3/8/K7 b - - 0 1");
            T.Give(armored, "d4", Buff.SmallBomb);
            T.Give(armored, "e3", Buff.Armor, 3);
            Assert.IsFalse(Geometry.IsKingAttacked(armored, Side.Black));

            // A stunned bomber threatens nothing.
            var stunned = T.Fen("8/8/8/8/3R4/4k3/8/K7 b - - 0 1");
            T.Give(stunned, "d4", Buff.SmallBomb);
            var p = stunned.Cells[Sq.Parse("d4")];
            p.StunTurns = 1;
            stunned.Cells[Sq.Parse("d4")] = p;
            Assert.IsFalse(Geometry.IsKingAttacked(stunned, Side.Black));
        }

        [Test]
        public void A_chain_reaction_can_put_a_king_in_check()
        {
            // White bomber d4 (a rook) reaches black's own bomb piece on e4, whose blast reaches f5 where the black king stands.
            var s = T.Fen("8/8/8/5k2/3Rn3/8/8/K7 b - - 0 1");
            T.Give(s, "d4", Buff.SmallBomb);
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.Black), "f5 is outside the first blast and no chain yet");
            T.Give(s, "e4", Buff.SmallBomb);
            Assert.IsTrue(Geometry.IsKingAttacked(s, Side.Black), "the chain through e4 reaches f5");
            Assert.IsTrue(Geometry.BombThreatens(s, Sq.Parse("f5"), Side.White));
        }

        [Test]
        public void A_detonation_that_would_reach_your_own_king_is_illegal()
        {
            var near = T.Fen("k7/8/8/8/3NK3/8/8/8 w - - 0 1");
            T.Give(near, "d4", Buff.SmallBomb);
            Assert.IsFalse(T.IsLegal(near, "Detonate d4"), "the king stands next to the bomber");

            // Reached by a chain: white bomber d4, black bomb piece e5, white king f6 (inside e5's blast only).
            var chain = T.Fen("k7/8/5K2/4n3/3N4/8/8/8 w - - 0 1");
            T.Give(chain, "d4", Buff.SmallBomb);
            T.Give(chain, "e5", Buff.SmallBomb);
            Assert.IsFalse(T.IsLegal(chain, "Detonate d4"), "the chain would reach your own king");
        }

        [Test]
        public void A_bomb_pickup_that_would_put_your_own_king_in_check_is_refused()
        {
            // Black knight on c5 holds a bomb (blast b4-d6). White king e7 is outside it. If the white rook takes a Bomb square on d6,
            // the rook joins the chain and its blast (c5-e7) reaches the king.
            var s = T.Fen("k7/4K3/8/2n5/8/8/8/3R4 w - - 0 1");
            T.Give(s, "c5", Buff.SmallBomb);
            T.Square(s, "d6", PowerupKind.SmallBomb);
            var ev = T.Do(s, "d1d6");
            Assert.IsFalse(T.At(s, "d6").Has(Buff.SmallBomb), "refused");
            Assert.IsFalse(s.Mystery[Sq.Parse("d6")], "the square is used up");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PickupRejected));
            Assert.IsFalse(Geometry.IsKingAttacked(s, Side.White), "and the king is not left in check");
        }

        // ---------- Stun and Steal squares (instant, aimed right after landing) ----------

        [Test]
        public void Landing_on_a_Stun_square_makes_you_aim_a_bolt_at_the_first_enemy_in_any_direction()
        {
            // White rook a1 lands on a3. Enemies in line: a5 (up the file), d3 (along the rank), c5 (up the diagonal).
            var s = T.Fen("7k/8/8/p1n5/8/3p4/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.StunBolt);
            T.Do(s, "a1a3");
            Assert.AreEqual(PowerupKind.StunBolt, s.EffectKind, "the effect is pending");
            Assert.AreEqual(Side.White, s.ToMove, "still White's turn: aim first");
            var beams = MoveGen.LegalActions(s).Where(a => a.Kind == ActionKind.StunBeam).Select(a => Sq.Name(a.To)).OrderBy(x => x).ToList();
            Assert.AreEqual(new[] { "a5", "c5", "d3" }, beams.ToArray(), "first enemy on each of the eight lines (b4 is empty, c5 is on the diagonal)");
            Assert.IsFalse(MoveGen.LegalActions(s).Any(a => a.Kind == ActionKind.Move), "no other action until it is aimed");

            var ev = T.Do(s, "StunBeam a3->a5");
            Assert.IsTrue(T.At(s, "a5").IsStunned);
            Assert.AreEqual(Rules.StunBoltTurns, T.At(s, "a5").StunTurns);
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.AreEqual(PowerupKind.None, s.EffectKind);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.ZapFired));
        }

        [Test]
        public void A_stunned_piece_loses_its_next_two_turns()
        {
            var s = T.Fen("7k/8/8/p7/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.StunBolt);
            T.Do(s, "a1a3");
            T.Do(s, "StunBeam a3->a5");
            Assert.IsFalse(T.IsLegal(s, "a5a4"), "frozen on Black's first turn");
            T.Do(s, "h8h7");
            T.Do(s, "e1e2");
            Assert.IsFalse(T.IsLegal(s, "a5a4"), "and on the second");
            T.Do(s, "h7h8");
            T.Do(s, "e2e1");
            Assert.IsTrue(T.IsLegal(s, "a5a4"), "free again on the third");
            Assert.IsFalse(T.At(s, "a5").IsStunned);
        }

        [Test]
        public void A_king_cannot_be_stunned()
        {
            // Rook a1 lands on a8 (a Stun square) giving check; the king on h8 is in line but is not a legal target, so the square fizzles.
            var s = T.Fen("7k/7p/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a8", PowerupKind.StunBolt);
            T.Do(s, "a1a8");
            Assert.IsFalse(MoveGen.LegalActions(s).Any(a => a.Kind == ActionKind.StunBeam), "no bolt can be aimed at a king");
        }

        [Test]
        public void A_stun_square_fizzles_when_nothing_can_be_hit()
        {
            // Only an armored piece and a friend are in line, so there is no legal bolt.
            var s = T.Fen("7k/8/8/p7/8/8/8/R3K3 w - - 0 1");
            T.Give(s, "a5", Buff.Armor, 3);
            T.Square(s, "a3", PowerupKind.StunBolt);
            var ev = T.Do(s, "a1a3");
            Assert.AreEqual(PowerupKind.None, s.EffectKind);
            Assert.AreEqual(Side.Black, s.ToMove, "the turn simply passes");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.EffectFizzled));
            Assert.IsFalse(s.Mystery[Sq.Parse("a3")], "the square is used up");
        }

        [Test]
        public void A_stolen_piece_that_dies_counts_as_a_loss_for_the_side_it_started_with()
        {
            // White steals Black's a4 pawn; Black's rook on a8 then takes it. The pawn was Black's, so it is Black's loss (Caissa can bring it back).
            var s = T.Fen("r6k/8/8/8/p7/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.StealGrab);
            T.Do(s, "a1a3");
            T.Do(s, "StealPick a3->a4");
            T.Do(s, "a8a4");
            Assert.AreEqual(1, s.Captured[(int)Side.Black * 8 + (int)PieceType.Pawn]);
            Assert.AreEqual(0, s.Captured[(int)Side.White * 8 + (int)PieceType.Pawn]);
        }

        [Test]
        public void Landing_on_a_Steal_square_lets_you_take_a_neighbouring_rival_but_never_a_king_or_armor()
        {
            // Rook a1 lands on a3 next to a pawn (a4) and an armored rook (b4); a knight on c4 is two files away.
            var s = T.Fen("7k/8/8/8/prn5/8/8/R3K3 w - - 0 1");
            T.Give(s, "b4", Buff.Armor, 3);
            T.Square(s, "a3", PowerupKind.StealGrab);
            T.Do(s, "a1a3");
            Assert.AreEqual(PowerupKind.StealGrab, s.EffectKind);
            var picks = MoveGen.LegalActions(s).Where(a => a.Kind == ActionKind.StealPick).Select(a => Sq.Name(a.To)).OrderBy(x => x).ToList();
            Assert.AreEqual(new[] { "a4" }, picks.ToArray(), "only the pawn next to a3 that is not armored (b4 armored, c4 is two files away)");

            var ev = T.Do(s, "StealPick a3->a4");
            Assert.AreEqual(Side.White, T.At(s, "a4").Side, "converted");
            Assert.AreEqual(PieceType.Pawn, T.At(s, "a4").Type, "and it stays where it was");
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Converted));

            var king = T.Fen("8/8/8/8/1k6/8/8/R3K3 w - - 0 1");
            T.Square(king, "a3", PowerupKind.StealGrab);
            T.Do(king, "a1a3");
            Assert.AreEqual(PowerupKind.None, king.EffectKind, "a king cannot be stolen, so the square fizzles");
        }

        [Test]
        public void A_pending_choice_survives_save_and_load_and_the_AI_can_resolve_it()
        {
            var cfg = T.Cfg();
            var s = T.Fen("7k/8/8/p7/8/8/8/R3K3 w - - 0 1", cfg);
            T.Square(s, "a3", PowerupKind.StunBolt);
            T.Do(s, "a1a3");
            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(s, new GameHistory()), cfg, out s2, out h2));
            Assert.AreEqual(PowerupKind.StunBolt, s2.EffectKind);
            Assert.AreEqual(Sq.Parse("a3"), s2.EffectSquare);
            Assert.AreEqual(MoveGen.LegalActions(s).Count, MoveGen.LegalActions(s2).Count);

            var choice = Ai.ForLevel(2).Choose(s2);
            Assert.AreEqual(ActionKind.StunBeam, choice.Kind, "the AI aims the pending bolt");
        }

        // ---------- Combo ----------

        static int MovesInTurn(GameState s, Side side, string firstMove)
        {
            int count = 0;
            T.Do(s, firstMove);
            count++;
            while (s.ToMove == side)
            {
                var m = MoveGen.LegalActions(s).First(a => a.Kind == ActionKind.Move && (s.ForcedMover < 0 || a.From == s.ForcedMover));
                Rules.Apply(s, m, null);
                count++;
                Assert.Less(count, 20, "runaway combo");
            }
            return count;
        }

        [Test]
        public void Combo_plus_2_gives_three_moves_in_all_with_the_same_piece()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Combo2);
            T.Do(s, "a1a3");
            Assert.AreEqual(Side.White, s.ToMove, "still White's turn");
            Assert.AreEqual(Sq.Parse("a3"), s.ForcedMover, "only the piece that landed may move");
            Assert.IsFalse(T.IsLegal(s, "e1e2"), "other pieces are locked out");
            Assert.AreEqual(3, 1 + CountForcedMoves(s));
        }

        static int CountForcedMoves(GameState s)
        {
            int n = 0;
            while (s.ToMove == Side.White && s.ForcedMover >= 0)
            {
                var m = MoveGen.LegalActions(s).First(a => a.Kind == ActionKind.Move);
                Rules.Apply(s, m, null);
                n++;
                Assert.Less(n, 20);
            }
            return n;
        }

        [Test]
        public void Combo_plus_4_gives_five_moves_in_all()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Combo4);
            Assert.AreEqual(5, MovesInTurn(s, Side.White, "a1a3"));
        }

        [Test]
        public void Landing_on_another_Combo_adds_to_the_move_total()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Combo2);
            T.Square(s, "a4", PowerupKind.Combo2);
            // a1-a3 (+2 owed), a3-a4 lands on another +2, so 1 + 1 + (1 owed left + 2) = 5 moves.
            T.Do(s, "a1a3");
            T.Do(s, "a3a4");
            Assert.AreEqual(Side.White, s.ToMove);
            Assert.AreEqual(5, 2 + CountForcedMoves(s));
        }

        [Test]
        public void Giving_check_ends_a_combo()
        {
            // Rook lands on a Combo square on the eighth rank and checks the king, so the turn passes at once.
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a8", PowerupKind.Combo4);
            var ev = T.Do(s, "a1a8");
            Assert.AreEqual(Side.Black, s.ToMove, "check cancels the rest of the combo");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.DoubleMoveForfeited));
        }

        // ---------- save and load ----------

        [Test]
        public void A_banked_tier_survives_save_and_load()
        {
            var cfg = T.Cfg();
            var s = T.Fen("7k/8/8/8/3N4/8/8/4K3 w - - 0 1", cfg);
            T.Give(s, "d4", Buff.Fireball);
            T.Tier(s, "d4", 2);
            var code = GameStateCodec.EncodeGame(s, new GameHistory());
            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(code, cfg, out s2, out h2));
            Assert.AreEqual(2, T.At(s2, "d4").Tier);
            Assert.IsTrue(T.At(s2, "d4").Has(Buff.Fireball));
        }

        // ---------- the Chess Fighter pool ----------

        [Test]
        public void The_fighter_pool_holds_only_powers_the_engine_supports()
        {
            var pool = GameConfig.FighterPool();
            Assert.IsTrue(pool.Any(w => w.Kind == PowerupKind.Fireball));
            Assert.IsTrue(pool.Any(w => w.Kind == PowerupKind.Combo2));
            Assert.IsTrue(pool.Any(w => w.Kind == PowerupKind.Combo4));
            Assert.IsFalse(pool.Any(w => PowerupInfo.IsTransform(w.Kind)), "transform squares are gone");
            Assert.IsFalse(pool.Any(w => w.Kind == PowerupKind.Reroll || w.Kind == PowerupKind.Rewind || w.Kind == PowerupKind.Swap || w.Kind == PowerupKind.Boulder));
            Assert.IsFalse(pool.Any(w => w.Kind == PowerupKind.DoubleMove || w.Kind == PowerupKind.TripleMove), "replaced by Combo");
            Assert.IsFalse(pool.Any(w => w.Kind == PowerupKind.Stun || w.Kind == PowerupKind.Steal), "the perk versions are replaced by instant squares");
            Assert.IsTrue(pool.Any(w => w.Kind == PowerupKind.StunBolt) && pool.Any(w => w.Kind == PowerupKind.StealGrab));
            Assert.IsTrue(pool.All(w => w.Weight > 0));
        }

        // ---------- Poison ----------

        [Test]
        public void Poison_wears_off_after_the_pickup_turn_and_five_more_owner_turns()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Poison);
            T.Do(s, "a1a3");
            Assert.IsTrue(T.At(s, "a3").Has(Buff.Poison));
            Assert.AreEqual(5, T.At(s, "a3").PoisonTurns);

            string[] white = { "e1e2", "e2e1", "e1e2", "e2e1" };
            string[] black = { "h8h7", "h7h8", "h8h7", "h7h8" };
            T.Do(s, "h8h7");
            for (int i = 0; i < 4; i++)
            {
                T.Do(s, white[i]);
                if (i < 3) T.Do(s, black[(i + 1) % 4]);
            }
            Assert.IsTrue(T.At(s, "a3").Has(Buff.Poison), "still poisoned after four more owner turns");
            Assert.AreEqual(1, T.At(s, "a3").PoisonTurns);

            T.Do(s, "h8h7");
            var ev = T.Do(s, "e1e2");
            Assert.IsFalse(T.At(s, "a3").Has(Buff.Poison), "worn off at the end of the fifth");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PoisonFaded));
        }

        // ---------- Ghost ----------

        [Test]
        public void Ghost_spends_a_move_only_when_the_ghost_piece_itself_moves()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            Rules.ApplyPickup(s, Side.White, Sq.Parse("a1"), PowerupKind.Ghost, null);
            Assert.AreEqual(3, T.At(s, "a1").GhostTurns);

            T.Do(s, "e1e2");
            Assert.AreEqual(3, T.At(s, "a1").GhostTurns, "another piece moved, the ghost keeps its moves");
            T.Do(s, "h8h7");
            T.Do(s, "a1a3");
            Assert.AreEqual(2, T.At(s, "a3").GhostTurns);
            T.Do(s, "h7h8");
            T.Do(s, "a3a5");
            Assert.AreEqual(1, T.At(s, "a5").GhostTurns);
            T.Do(s, "h8h7");
            var ev = T.Do(s, "a5a6");
            Assert.IsFalse(T.At(s, "a6").Has(Buff.Ghost), "third ghost move used it up");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.GhostFaded));
        }

        [Test]
        public void A_ghost_threatens_check_through_blockers()
        {
            // Rook down the file through a pawn.
            var rook = T.Fen("k7/8/8/p7/8/8/8/R3K3 b - - 0 1");
            Assert.IsFalse(Geometry.IsKingAttacked(rook, Side.Black));
            T.Give(rook, "a1", Buff.Ghost);
            Assert.IsTrue(Geometry.IsKingAttacked(rook, Side.Black), "a ghost rook threatens through the pawn");

            // Bishop along a diagonal through a pawn.
            var bishop = T.Fen("7k/8/8/4p3/8/8/8/B3K3 b - - 0 1");
            T.Give(bishop, "a1", Buff.Ghost);
            Assert.IsTrue(Geometry.IsKingAttacked(bishop, Side.Black));

            // A ghost knight also threatens a king's step; a ghost king reaches two squares.
            var knight = T.Fen("k7/1N6/8/8/8/8/8/4K3 b - - 0 1");
            Assert.IsFalse(Geometry.IsKingAttacked(knight, Side.Black));
            T.Give(knight, "b7", Buff.Ghost);
            Assert.IsTrue(Geometry.IsKingAttacked(knight, Side.Black));

            var king = T.Fen("k7/8/K7/8/8/8/8/8 b - - 0 1");
            Assert.IsFalse(Geometry.IsKingAttacked(king, Side.Black));
            T.Give(king, "a6", Buff.Ghost);
            Assert.IsTrue(Geometry.IsKingAttacked(king, Side.Black), "a ghost king reaches two squares");

            // A stunned ghost threatens nothing.
            var p = rook.Cells[Sq.Parse("a1")];
            p.StunTurns = 1;
            rook.Cells[Sq.Parse("a1")] = p;
            Assert.IsFalse(Geometry.IsKingAttacked(rook, Side.Black));
        }

        // ---------- Leap (a checkers jump) ----------

        [Test]
        public void Leap_jumps_a_rival_on_the_next_diagonal_square_and_captures_it()
        {
            var s = T.Fen("7k/8/8/4n3/3R4/8/8/7K w - - 0 1");
            T.Give(s, "d4", Buff.Leap);
            T.Tier(s, "d4", 1);
            var ev = T.Do(s, "Leap d4->f6");
            Assert.IsTrue(T.At(s, "e5").IsEmpty, "the jumped knight is gone");
            Assert.AreEqual(PieceType.Rook, T.At(s, "f6").Type, "the jumper lands on the far side");
            Assert.IsTrue(T.At(s, "d4").IsEmpty);
            Assert.IsFalse(T.At(s, "f6").Has(Buff.Leap), "one use");
            Assert.AreEqual(Side.Black, s.ToMove, "leaping uses the turn");
            Assert.AreEqual(PowerupKind.None, s.EffectKind);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Leaped));
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Destroyed && e.Piece == PieceType.Knight));
        }

        [Test]
        public void Leap_needs_an_enemy_that_can_be_captured_and_an_empty_plain_square_behind_it()
        {
            System.Func<string, System.Action<GameState>, bool> hasLeap = (fen, setup) =>
            {
                var g = T.Fen(fen);
                T.Give(g, "d4", Buff.Leap);
                if (setup != null) setup(g);
                return MoveGen.LegalActions(g).Any(a => a.Kind == ActionKind.Leap);
            };
            Assert.IsTrue(hasLeap("7k/8/8/4n3/3R4/8/8/7K w - - 0 1", null));
            Assert.IsFalse(hasLeap("7k/8/8/4N3/3R4/8/8/7K w - - 0 1", null), "a friend cannot be jumped");
            Assert.IsFalse(hasLeap("8/8/8/4k3/3R4/8/8/7K w - - 0 1", null), "a king cannot be jumped");
            Assert.IsFalse(hasLeap("7k/8/5p2/4n3/3R4/8/8/7K w - - 0 1", null), "the landing square must be empty");
            Assert.IsFalse(hasLeap("7k/8/8/4n3/3R4/8/8/7K w - - 0 1", g => T.Give(g, "e5", Buff.Armor, 3)), "armor cannot be jumped");
            Assert.IsFalse(hasLeap("7k/8/8/4n3/3R4/8/8/7K w - - 0 1", g => g.Boulders[Sq.Parse("f6")] = true), "no landing on a boulder");
            Assert.IsFalse(hasLeap("7k/8/8/4n3/3R4/8/8/7K w - - 0 1", g => { var st = g.Cells[Sq.Parse("d4")]; st.StunTurns = 2; g.Cells[Sq.Parse("d4")] = st; }), "a stunned piece cannot leap");
        }

        [Test]
        public void A_leap_may_land_on_a_powerup_square_or_teleporter_and_triggers_it_and_ends_the_chain()
        {
            var s = T.Fen("k7/6n1/8/4n3/3R4/8/8/7K w - - 0 1");
            T.Give(s, "d4", Buff.Leap);
            T.Tier(s, "d4", 2);
            T.Square(s, "f6", PowerupKind.Fireball);
            T.Do(s, "Leap d4->f6");
            Assert.IsTrue(T.At(s, "f6").Has(Buff.Fireball), "the square's power was picked up");
            Assert.AreEqual(Side.Black, s.ToMove, "landing on a powerup square ends the chain");
            Assert.AreEqual(PowerupKind.None, s.EffectKind);

            var tp = T.Fen("7k/8/8/4n3/3R4/8/8/7K w - - 0 1");
            T.Give(tp, "d4", Buff.Leap);
            tp.Portal[Sq.Parse("f6")] = true;
            T.Do(tp, "Leap d4->f6");
            Assert.AreEqual(PieceType.Rook, T.At(tp, Sq.Name(Sq.Mirror(Sq.Parse("f6")))).Type, "teleported to its twin");
        }

        [Test]
        public void A_leap_may_not_leave_your_own_king_in_check()
        {
            // The rook on e4 is pinned against the rook on e8: leaping off the file is not allowed.
            var s = T.Fen("4r2k/8/8/5n2/4R3/8/8/4K3 w - - 0 1");
            T.Give(s, "e4", Buff.Leap);
            Assert.IsFalse(T.IsLegal(s, "Leap e4->g6"));
        }

        [Test]
        public void A_pawn_never_leaps_onto_the_last_rank()
        {
            var s = T.Fen("7k/8/2n5/1P6/8/8/8/7K w - - 0 1");
            T.Give(s, "b5", Buff.Leap);
            // b5 over c6 would land on d7, which is fine; but from d7 over e8 would be the last rank.
            Assert.IsTrue(MoveGen.LegalActions(s).Any(a => a.Kind == ActionKind.Leap));
            var end = T.Fen("4n3/3P4/8/8/8/8/8/k6K w - - 0 1");
            T.Give(end, "d7", Buff.Leap);
            Assert.IsFalse(MoveGen.LegalActions(end).Any(a => a.Kind == ActionKind.Leap), "f8 is the last rank");
        }

        [Test]
        public void Leap_II_chains_a_second_jump_and_the_player_may_stop()
        {
            var s = T.Fen("k7/6n1/8/4n3/3R4/8/8/7K w - - 0 1");
            T.Give(s, "d4", Buff.Leap);
            T.Tier(s, "d4", 2);
            T.Do(s, "Leap d4->f6");
            Assert.AreEqual(PowerupKind.Leap, s.EffectKind, "a chain is pending");
            Assert.AreEqual(Side.White, s.ToMove, "still the jumper's turn");
            var acts = MoveGen.LegalActions(s);
            Assert.IsTrue(acts.Any(a => a.Kind == ActionKind.Leap && a.To == Sq.Parse("h8")));
            Assert.IsTrue(acts.Any(a => a.Kind == ActionKind.LeapStop));
            Assert.IsFalse(acts.Any(a => a.Kind == ActionKind.Move), "only the chain may continue");

            var stop = s.Clone();
            T.Do(stop, "LeapStop f6->f6");
            Assert.AreEqual(Side.Black, stop.ToMove);
            Assert.AreEqual(PieceType.Knight, T.At(stop, "g7").Type, "the second knight is untouched");

            T.Do(s, "Leap f6->h8");
            Assert.AreEqual(Side.Black, s.ToMove, "tier II stops after two jumps");
            Assert.IsTrue(T.At(s, "g7").IsEmpty);
            Assert.AreEqual(PieceType.Rook, T.At(s, "h8").Type);
        }

        [Test]
        public void Leap_I_makes_one_jump_and_a_chain_ends_by_itself_when_no_jump_is_left()
        {
            var one = T.Fen("k7/6n1/8/4n3/3R4/8/8/7K w - - 0 1");
            T.Give(one, "d4", Buff.Leap);
            T.Tier(one, "d4", 1);
            T.Do(one, "Leap d4->f6");
            Assert.AreEqual(Side.Black, one.ToMove, "tier I never chains");

            var none = T.Fen("k7/8/8/4n3/3R4/8/8/7K w - - 0 1");
            T.Give(none, "d4", Buff.Leap);
            T.Tier(none, "d4", 2);
            T.Do(none, "Leap d4->f6");
            Assert.AreEqual(Side.Black, none.ToMove, "no second jump available, so the turn simply ends");
            Assert.AreEqual(PowerupKind.None, none.EffectKind);
            Assert.AreEqual(0, T.At(none, "f6").Tier);
        }

        [Test]
        public void Leap_III_chains_as_long_as_there_are_jumps()
        {
            // d2 over e3 to f4, over g5 to h6, then over g7 to f8: three jumps.
            var s = T.Fen("k7/6n1/8/6n1/8/4n3/3B4/7K w - - 0 1");
            T.Give(s, "d2", Buff.Leap);
            T.Tier(s, "d2", 3);
            T.Do(s, "Leap d2->f4");
            T.Do(s, "Leap f4->h6");
            Assert.AreEqual(PowerupKind.Leap, s.EffectKind, "a third jump is still on offer");
            T.Do(s, "Leap h6->f8");
            Assert.AreEqual(Side.Black, s.ToMove);
            Assert.AreEqual(0, Enumerable.Range(0, 64).Count(i => s.Cells[i].Type == PieceType.Knight), "all three knights were captured");
        }

        [Test]
        public void A_pending_leap_chain_survives_a_save()
        {
            var cfg = T.Cfg();
            var s = T.Fen("k7/6n1/8/4n3/3R4/8/8/7K w - - 0 1", cfg);
            T.Give(s, "d4", Buff.Leap);
            T.Tier(s, "d4", 2);
            T.Do(s, "Leap d4->f6");
            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(s, new GameHistory()), cfg, out s2, out h2));
            Assert.AreEqual(PowerupKind.Leap, s2.EffectKind);
            Assert.AreEqual(Sq.Parse("f6"), s2.EffectSquare);
            Assert.IsTrue(MoveGen.LegalActions(s2).Any(a => a.Kind == ActionKind.LeapStop));
        }

        [Test]
        public void Leap_tiers_upgrade_replace_and_a_king_can_hold_one()
        {
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Square(s, "a3", PowerupKind.Leap);
            T.Square(s, "a5", PowerupKind.Leap);
            T.Square(s, "a6", PowerupKind.Leap);
            T.Square(s, "a7", PowerupKind.Leap);
            T.Do(s, "a1a3");
            Assert.AreEqual(1, T.At(s, "a3").Tier);
            T.Do(s, "h8h7");
            T.Do(s, "a3a5");
            Assert.AreEqual(2, T.At(s, "a5").Tier);
            T.Do(s, "h7h8");
            T.Do(s, "a5a6");
            Assert.AreEqual(3, T.At(s, "a6").Tier, "Leap has a third tier");
            T.Do(s, "h8h7");
            var ev = T.Do(s, "a6a7");
            Assert.AreEqual(3, T.At(s, "a7").Tier);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.PowerMaxed));

            Assert.IsTrue(Rules.Eligible(s, T.At(s, "e1"), 4, PowerupKind.Leap), "kings can hold a leap");

            var fire = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1");
            T.Give(fire, "a1", Buff.Leap);
            T.Square(fire, "a3", PowerupKind.Fireball);
            T.Do(fire, "a1a3");
            Assert.IsFalse(T.At(fire, "a3").Has(Buff.Leap), "a different banked power replaces it");
            Assert.IsTrue(T.At(fire, "a3").Has(Buff.Fireball));
        }

        // ---------- saves must keep every new kind (regression: Combo squares and mid-combo states once failed to reload) ----------

        [Test]
        public void Saves_keep_combo_squares_mid_combo_state_leap_and_poison_fuses()
        {
            var cfg = T.Cfg();
            var s = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1", cfg);
            T.Square(s, "a3", PowerupKind.Combo4);
            T.Square(s, "c5", PowerupKind.Leap);
            T.Square(s, "d5", PowerupKind.Combo2);
            T.Do(s, "a1a3"); // mid-combo: four more moves owed
            Assert.AreEqual(4, s.MultiLeft);
            Assert.AreEqual(Sq.Parse("a3"), s.ForcedMover);

            GameState s2;
            GameHistory h2;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(s, new GameHistory()), cfg, out s2, out h2), "mid-combo save must reload");
            Assert.AreEqual(4, s2.MultiLeft);
            Assert.AreEqual(Sq.Parse("a3"), s2.ForcedMover);
            Assert.AreEqual((byte)PowerupKind.Combo2, s2.Content[Sq.Parse("d5")], "revealed Combo square survives");
            Assert.AreEqual((byte)PowerupKind.Leap, s2.Content[Sq.Parse("c5")], "revealed Leap square survives");

            var banked = T.Fen("7k/8/8/8/8/8/8/R3K3 w - - 0 1", cfg);
            T.Give(banked, "a1", Buff.Leap);
            T.Tier(banked, "a1", 2);
            var po = banked.Cells[Sq.Parse("e1")];
            po.Buffs |= Buff.Poison; po.PoisonTurns = 4;
            banked.Cells[Sq.Parse("e1")] = po;
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(banked, new GameHistory()), cfg, out s2, out h2));
            Assert.IsTrue(T.At(s2, "a1").Has(Buff.Leap), "the buff past the old 8-bit range survives");
            Assert.AreEqual(2, T.At(s2, "a1").Tier);
            Assert.AreEqual(4, T.At(s2, "e1").PoisonTurns);

            // And a history entry for the new action kind round-trips.
            var hist = new GameHistory();
            var before = T.Fen("7k/8/8/4n3/3R4/8/8/7K w - - 0 1", cfg);
            T.Give(before, "d4", Buff.Leap);
            hist.Record(before.Clone(), T.Find(before, "Leap d4->f6"));
            Assert.IsTrue(GameStateCodec.TryDecodeGame(GameStateCodec.EncodeGame(before, hist), cfg, out s2, out h2));
            Assert.AreEqual(1, h2.Count);
        }

        // ---------- whole-game stability ----------

        [Test]
        public void Random_games_with_the_fighter_pool_keep_invariants_and_terminate()
        {
            var results = new Dictionary<GameResult, int>();
            for (ulong seed = 1; seed <= 80; seed++)
            {
                var cfg = new GameConfig
                {
                    MaxPly = 250,
                    Pool = GameConfig.FighterPool(),
                    Squares = (seed % 2 == 0) ? SquareMode.Revealed : SquareMode.Mystery,
                    StartRerolls = 0,
                    SpawnInterval = 2,
                    PortalChance = 0.1,
                };
                var r = SimulationTests.PlayRandomGame(seed, null, 1500, cfg);
                results[r] = results.ContainsKey(r) ? results[r] + 1 : 1;
            }
            TestContext.Out.WriteLine(string.Join(", ", results.Select(kv => kv.Key + "=" + kv.Value)));
        }
    }
}
