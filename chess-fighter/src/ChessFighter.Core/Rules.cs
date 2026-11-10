using System;
using System.Collections.Generic;

namespace ChessFighter.Core
{
    /// <summary>Applies actions to a <see cref="GameState"/> and resolves powerups. Assumes the action is legal.</summary>
    public static class Rules
    {
        const int ArmorStartTurns = 4; // 1 for the pickup turn itself + 3 more owner turns
        const int StunDuration = 3;
        /// <summary>Owner turns a Stun square's bolt freezes its target. Change this one number to tune it.</summary>
        public const int StunBoltTurns = 2;
        /// <summary>Turns a rival already next to a Harpoon shooter is stunned.</summary>
        public const int HarpoonStunTurns = 1;
        /// <summary>Most extra moves a stack of Combo squares can owe at once.</summary>
        public const int MaxComboMoves = 24;
        /// <summary>A poison fuse: the pickup turn plus 5 more owner turns.</summary>
        const int PoisonStartTurns = 6;

        public static void Apply(GameState s, GameAction a, List<GameEvent> ev = null)
        {
            Side me = s.ToMove;
            if (a.Kind != ActionKind.Move && a.Kind != ActionKind.KeepPickup && a.Kind != ActionKind.RerollPickup)
                s.EnPassant = -1;

            switch (a.Kind)
            {
                case ActionKind.Move:
                    ApplyMove(s, a, ev);
                    return;

                case ActionKind.KeepPickup:
                {
                    var kind = s.PendingKind;
                    int sq = s.PendingSquare;
                    s.PendingSquare = -1;
                    s.PendingKind = PowerupKind.None;
                    ApplyPickup(s, me, sq, kind, ev);
                    Finish(s, ev);
                    return;
                }

                case ActionKind.RerollPickup:
                {
                    int sq = s.PendingSquare;
                    s.PendingSquare = -1;
                    s.PendingKind = PowerupKind.None;
                    s.AddPerk(me, PowerupKind.Reroll, -1);
                    var kind = Draw(s, s.Cells[sq], sq);
                    Emit(ev, EventKind.PickupRerolled, sq, sq, s.Cells[sq].Type, me, kind);
                    ApplyPickup(s, me, sq, kind, ev);
                    Finish(s, ev);
                    return;
                }

                case ActionKind.Stun:
                {
                    s.AddPerk(me, PowerupKind.Stun, -1);
                    var target = s.Cells[a.To];
                    target.StunTurns = StunDuration;
                    s.Cells[a.To] = target;
                    Emit(ev, EventKind.StunStruck, a.To, a.To, target.Type, me, PowerupKind.Stun);
                    Finish(s, ev);
                    return;
                }

                case ActionKind.Swap:
                {
                    s.AddPerk(me, PowerupKind.Swap, -1);
                    var pa = s.Cells[a.From];
                    var pb = s.Cells[a.To];
                    Place(s, a.From, pb);
                    Place(s, a.To, pa);
                    Emit(ev, EventKind.Swapped, a.From, a.To, pa.Type, me, PowerupKind.Swap);
                    PromoteIfAtEnd(s, a.To, ev, me);
                    PromoteIfAtEnd(s, a.From, ev, me);
                    Finish(s, ev);
                    return;
                }

                case ActionKind.Fireball:
                {
                    var shooter = s.Cells[a.From];
                    // Work out the hit before spending the fireball: the victims depend on the shooter's tier.
                    var victims = new List<int>(4);
                    Geometry.FireballVictims(s, a.From, a.Aux, victims);
                    shooter.Buffs &= ~Buff.Fireball;
                    shooter.Tier = 0;
                    s.Cells[a.From] = shooter;
                    Emit(ev, EventKind.FireballFired, a.From, a.To, s.Cells[a.From].Type, me, PowerupKind.Fireball);
                    foreach (int v in victims) Destroy(s, v, ev);
                    s.ClockReset = true;
                    Finish(s, ev);
                    return;
                }

                case ActionKind.Harpoon:
                {
                    var shooter = s.Cells[a.From];
                    for (int dir = 4; dir < 8; dir++)
                    {
                        if (a.Aux != 8 && a.Aux != dir) continue;
                        int target = Geometry.HarpoonTarget(s, a.From, dir);
                        if (target < 0) continue;
                        int landing = Geometry.HarpoonLanding(s, a.From, target, dir);
                        Emit(ev, EventKind.HarpoonFired, a.From, target, shooter.Type, me, PowerupKind.Harpoon);
                        var hooked = s.Cells[target];
                        if (target == landing)
                        {
                            // It cannot be pulled any closer (already next to the shooter, or powerup squares fill the way): stunned instead.
                            hooked.StunTurns = HarpoonStunTurns;
                            s.Cells[target] = hooked;
                            Emit(ev, EventKind.StunStruck, target, target, hooked.Type, me, PowerupKind.Harpoon);
                        }
                        else
                        {
                            Remove(s, target);
                            Place(s, landing, hooked);
                            Emit(ev, EventKind.PulledIn, target, landing, hooked.Type, me, PowerupKind.Harpoon);
                        }
                    }
                    shooter.Buffs &= ~Buff.Harpoon;
                    shooter.Tier = 0;
                    s.Cells[a.From] = shooter;
                    s.ClockReset = true;
                    Finish(s, ev);
                    return;
                }

                case ActionKind.PlayCard:
                    ActivateCard(s, me, a, ev);
                    return;

                case ActionKind.UseNuhUh:
                {
                    // The rival (not the side to move) cancels the card.
                    var cancelled = s.RespCard;
                    Side rival = me.Opponent();
                    s.AddCard(rival, CardKind.NuhUh, -1);
                    s.RespCard = CardKind.None;
                    Cards.Emit(ev, EventKind.CardCancelled, s.RespFrom, s.RespTo, rival, cancelled);
                    // A card played to get out of check, then cancelled, leaves the king in check at the end of the turn: that is mate.
                    if (!s.Probe && Geometry.IsKingAttacked(s, me)) s.LostSide = (int)me;
                    Finish(s, ev);
                    return;
                }

                case ActionKind.AllowCard:
                {
                    var through = new GameAction(ActionKind.PlayCard, s.RespFrom, s.RespTo, s.RespAux);
                    s.RespCard = CardKind.None;
                    Cards.Resolve(s, me, through, ev);
                    Finish(s, ev);
                    return;
                }

                case ActionKind.StunBeam:
                {
                    var target = s.Cells[a.To];
                    Emit(ev, EventKind.ZapFired, a.From, a.To, s.Cells[a.From].Type, me, PowerupKind.StunBolt);
                    target.StunTurns = StunBoltTurns;
                    s.Cells[a.To] = target;
                    Emit(ev, EventKind.StunStruck, a.To, a.To, target.Type, me, PowerupKind.StunBolt);
                    s.EffectKind = PowerupKind.None;
                    s.EffectSquare = -1;
                    Finish(s, ev);
                    return;
                }

                case ActionKind.StealPick:
                {
                    var stolen = new Piece(s.Cells[a.To].Type, me) { Origin = s.Cells[a.To].Origin };
                    Place(s, a.To, stolen);
                    Emit(ev, EventKind.Converted, a.From, a.To, stolen.Type, me, PowerupKind.StealGrab);
                    s.ClockReset = true;
                    s.EffectKind = PowerupKind.None;
                    s.EffectSquare = -1;
                    Finish(s, ev);
                    return;
                }

                case ActionKind.Steal:
                {
                    s.AddPerk(me, PowerupKind.Steal, -1);
                    var stolen = new Piece(s.Cells[a.To].Type, me) { Origin = s.Cells[a.To].Origin };
                    Place(s, a.To, stolen);
                    Emit(ev, EventKind.Converted, a.From, a.To, stolen.Type, me, PowerupKind.Steal);
                    s.ClockReset = true;
                    Finish(s, ev);
                    return;
                }

                case ActionKind.PlaceBoulder:
                    s.AddPerk(me, PowerupKind.Boulder, -1);
                    s.Boulders[a.To] = true;
                    Emit(ev, EventKind.BoulderPlaced, a.To, a.To, PieceType.None, me, PowerupKind.Boulder);
                    Finish(s, ev);
                    return;

                case ActionKind.ClearBoulder:
                    s.AddPerk(me, PowerupKind.Reroll, -1);
                    s.Boulders[a.To] = false;
                    Emit(ev, EventKind.BoulderCleared, a.To, a.To, PieceType.None, me, PowerupKind.Reroll);
                    Finish(s, ev);
                    return;

                case ActionKind.Detonate:
                {
                    // The bomber and every other bomb piece the blast reaches go off; the bombers themselves are destroyed too.
                    var zone = new bool[64];
                    var order = new List<int>(4);
                    Geometry.BlastChain(s, a.From, zone, order);
                    foreach (int b in order)
                    {
                        var piece = s.Cells[b];
                        Emit(ev, EventKind.Detonated, b, b, piece.Type, me,
                            Geometry.BombRadius(piece) > 1 ? PowerupKind.BigBomb : PowerupKind.SmallBomb);
                    }
                    for (int t = 0; t < 64; t++)
                    {
                        if (!zone[t]) continue;
                        if (Geometry.IsVulnerableToBomb(s.Cells[t])) Destroy(s, t, ev);
                        if (s.Boulders[t])
                        {
                            s.Boulders[t] = false;
                            Emit(ev, EventKind.BoulderCleared, t, t, PieceType.None, me, PowerupKind.SmallBomb);
                        }
                    }
                    // An armored bomb piece survives its own blast but the bomb is spent.
                    foreach (int b in order)
                    {
                        var left = s.Cells[b];
                        if (left.IsEmpty) continue;
                        left.Buffs &= ~(Buff.SmallBomb | Buff.BigBomb);
                        left.Tier = 0;
                        s.Cells[b] = left;
                    }
                    s.ClockReset = true;
                    Finish(s, ev);
                    return;
                }
            }
        }

        /// <summary>A pawn swapped onto the far rank becomes a Queen.</summary>
        internal static void PromoteIfAtEnd(GameState s, int sq, List<GameEvent> ev, Side me)
        {
            var p = s.Cells[sq];
            if (p.Type != PieceType.Pawn) return;
            int last = p.Side == Side.White ? 7 : 0;
            if (Sq.Rank(sq) != last) return;
            p.Type = PieceType.Queen;
            Place(s, sq, p);
            Emit(ev, EventKind.Promoted, sq, sq, PieceType.Queen, me, PowerupKind.None);
        }

        static void ApplyMove(GameState s, GameAction a, List<GameEvent> ev)
        {
            Side me = s.ToMove;
            Piece mover = s.Cells[a.From];
            PieceType origType = mover.Type;
            bool wasForced = s.ForcedMover >= 0;
            s.ForcedMover = -1;
            int ep = s.EnPassant;
            s.EnPassant = -1;

            Piece target = s.Cells[a.To];
            int dFile = Sq.File(a.To) - Sq.File(a.From);
            Emit(ev, EventKind.Moved, a.From, a.To, origType, me, PowerupKind.None);

            if (origType == PieceType.King && Math.Abs(dFile) == 2 && !mover.Has(Buff.Ghost)) // a ghost king's two-square step is a move, not a castle
            {
                int rookFrom = dFile > 0 ? a.From + 3 : a.From - 4;
                int rookTo = dFile > 0 ? a.From + 1 : a.From - 1;
                Piece rook = s.Cells[rookFrom];
                Remove(s, rookFrom);
                Place(s, rookTo, rook);
                Emit(ev, EventKind.Castled, rookFrom, rookTo, PieceType.Rook, me, PowerupKind.None);
            }

            bool didCapture = false;
            Piece captured = default(Piece);
            if (origType == PieceType.Pawn && target.IsEmpty && dFile != 0)
            {
                int capSq = Sq.Make(Sq.File(a.To), Sq.Rank(a.From));
                captured = s.Cells[capSq];
                didCapture = true;
                NoteLost(s, captured);
                Remove(s, capSq);
                Emit(ev, EventKind.Captured, capSq, capSq, captured.Type, me, PowerupKind.None);
            }
            else if (!target.IsEmpty)
            {
                captured = target;
                didCapture = true;
                NoteLost(s, captured);
                Emit(ev, EventKind.Captured, a.To, a.To, captured.Type, me, PowerupKind.None);
            }

            Remove(s, a.From);
            if (origType == PieceType.Pawn && (Sq.Rank(a.To) == 0 || Sq.Rank(a.To) == 7))
            {
                mover.Type = a.Aux != 0 ? (PieceType)a.Aux : PieceType.Queen;
                Emit(ev, EventKind.Promoted, a.To, a.To, mover.Type, me, PowerupKind.None);
            }

            // Double Move owes one more move, Triple Move two. A forced follow-up counts the debt down.
            int owed = 0;
            if (wasForced) owed = s.MultiLeft - 1;
            else if (mover.Has(Buff.TripleMove)) owed = 2;
            else if (mover.Has(Buff.DoubleMove)) owed = 1;
            s.MultiLeft = 0;
            if (!wasForced) mover.Buffs &= ~(Buff.DoubleMove | Buff.TripleMove);
            // A ghost spends one of its moves each time it moves; standing still costs nothing.
            bool ghostFaded = false;
            if (mover.Has(Buff.Ghost) && mover.GhostTurns > 0)
            {
                mover.GhostTurns--;
                if (mover.GhostTurns == 0) { mover.Buffs &= ~Buff.Ghost; mover.Tier = 0; ghostFaded = true; }
            }
            Place(s, a.To, mover);
            if (ghostFaded) Emit(ev, EventKind.GhostFaded, a.To, a.To, mover.Type, me, PowerupKind.Ghost);

            if (didCapture || origType == PieceType.Pawn) s.ClockReset = true;
            if (origType == PieceType.Pawn && Math.Abs(Sq.Rank(a.To) - Sq.Rank(a.From)) == 2
                && s.Cells[(a.From + a.To) / 2].IsEmpty && !s.Boulders[(a.From + a.To) / 2])
                s.EnPassant = (a.From + a.To) / 2; // a ghost pawn's leap over a blocker sets none

            bool alive = true;
            if (didCapture && captured.Has(Buff.Poison) && mover.Type != PieceType.King && !mover.Has(Buff.Armor))
            {
                NoteLost(s, s.Cells[a.To]);
                Remove(s, a.To);
                alive = false;
                Emit(ev, EventKind.PoisonTriggered, a.To, a.To, mover.Type, me, PowerupKind.Poison);
            }

            int land = a.To;
            if (alive && s.Portal[a.To])
            {
                int dest = Sq.Mirror(a.To);
                s.Portal[a.To] = false;
                s.Portal[dest] = false;
                if (s.Cells[dest].IsEmpty && !s.Boulders[dest] && !s.Mystery[dest])
                {
                    Piece moved = s.Cells[a.To];
                    Remove(s, a.To);
                    Place(s, dest, moved);
                    land = dest;
                    Emit(ev, EventKind.Teleported, a.To, dest, moved.Type, me, PowerupKind.None);
                }
                else Emit(ev, EventKind.TeleportBlocked, a.To, dest, s.Cells[a.To].Type, me, PowerupKind.None);
            }

            if (alive && owed > 0)
            {
                s.DoubleSquare = land;
                s.MultiLeft = owed;
            }

            if (alive && s.Mystery[a.To])
            {
                var shown = (PowerupKind)s.Content[a.To];
                if (shown == PowerupKind.None)
                {
                    // A hidden mystery: the powerup is drawn now.
                    s.Mystery[a.To] = false;
                    if (!s.SearchMode)
                    {
                        var kind = Draw(s, s.Cells[a.To], a.To);
                        if (kind != PowerupKind.None)
                        {
                            Emit(ev, EventKind.PickupFound, a.To, a.To, s.Cells[a.To].Type, me, kind);
                            if (s.Perks(me, PowerupKind.Reroll) > 0)
                            {
                                s.PendingSquare = a.To;
                                s.PendingKind = kind;
                                return;
                            }
                            ApplyPickup(s, me, a.To, kind, ev);
                        }
                    }
                }
                else if (Eligible(s, s.Cells[a.To], a.To, shown))
                {
                    // A revealed square: the player knew what it held. The AI's lookahead applies it too (no randomness).
                    s.Mystery[a.To] = false;
                    s.Content[a.To] = 0;
                    if (s.SearchMode)
                    {
                        ApplyPickup(s, me, a.To, shown, null);
                    }
                    else
                    {
                        Emit(ev, EventKind.PickupFound, a.To, a.To, s.Cells[a.To].Type, me, shown);
                        // A king that is only legal on this square because the armor here shields it must take it.
                        bool armorSavesKing = shown == PowerupKind.SpikedArmor && s.Cells[a.To].Type == PieceType.King
                            && Geometry.IsAttacked(s, a.To, me.Opponent());
                        if (s.Perks(me, PowerupKind.Reroll) > 0 && !armorSavesKing)
                        {
                            s.PendingSquare = a.To;
                            s.PendingKind = shown;
                            return;
                        }
                        ApplyPickup(s, me, a.To, shown, ev);
                    }
                }
                else if (KingIncompatible(s.Cells[a.To], shown))
                {
                    // A king stepped on Poison or a Transform: it cannot use it, and the square is used up.
                    s.Mystery[a.To] = false;
                    s.Content[a.To] = 0;
                    Emit(ev, EventKind.PickupRejected, a.To, a.To, PieceType.King, me, shown);
                }
                // Otherwise the piece cannot use what the square holds (a capped perk, a duplicate buff, ...):
                // the square is left untouched, so it is still there when something that can use it arrives.
            }

            Finish(s, ev);
        }

        /// <summary>Everything that happens once an action and its pickup are fully resolved: pending aims are checked, then the turn ends.</summary>
        public static void Finish(GameState s, List<GameEvent> ev)
        {
            if (s.PendingCard != CardKind.None)
            {
                // An immediate card was drawn: its owner must aim it now. With no legal use it goes into the bag instead.
                var card = s.PendingCard;
                if (!s.Probe && MoveGen.LegalActions(s).Count > 0) return;
                s.PendingCard = CardKind.None;
                if (!s.Probe)
                {
                    s.AddCard(s.ToMove, card, 1);
                    Cards.Emit(ev, EventKind.CardKept, 0, 0, s.ToMove, card);
                }
            }

            if (s.EffectKind != PowerupKind.None)
            {
                // A Stun or Steal square: the mover must aim it now. With no legal target it fizzles.
                var kind = s.EffectKind;
                int at = s.EffectSquare;
                if (!s.Probe && MoveGen.LegalActions(s).Count > 0) return;
                s.EffectKind = PowerupKind.None;
                s.EffectSquare = -1;
                if (!s.Probe) Emit(ev, EventKind.EffectFizzled, at, at, s.Cells[at].Type, s.ToMove, kind);
            }

            if (s.ComboGain > 0)
            {
                // A Combo square: the piece that landed on it owes that many extra moves, added to any it already owed.
                if (s.DoubleSquare != s.ComboSquare) { s.DoubleSquare = s.ComboSquare; s.MultiLeft = 0; }
                s.MultiLeft = Math.Min(MaxComboMoves, s.MultiLeft + s.ComboGain);
            }
            s.ComboGain = 0;
            s.ComboSquare = -1;

            if (s.DoubleSquare >= 0)
            {
                int sq = s.DoubleSquare;
                s.DoubleSquare = -1;
                if (!s.Probe)
                {
                    Side me = s.ToMove;
                    var p = s.Cells[sq];
                    if (!p.IsEmpty && p.Side == me && !p.IsStunned)
                    {
                        bool check = Geometry.IsKingAttacked(s, me.Opponent());
                        if (check)
                        {
                            Emit(ev, EventKind.DoubleMoveForfeited, sq, sq, p.Type, me, PowerupKind.DoubleMove);
                        }
                        else if (MoveGen.HasLegalMoveFrom(s, sq))
                        {
                            s.ForcedMover = sq;
                            return;
                        }
                    }
                }
            }
            EndTurn(s, ev);
        }

        static void EndTurn(GameState s, List<GameEvent> ev)
        {
            if (s.KeepTurn)
            {
                // Take back: the rewound position is the player's own turn start, so they move again.
                s.KeepTurn = false;
                s.MultiLeft = 0;
                if (!s.SearchMode) PushTurnStart(s);
                return;
            }
            Side me = s.ToMove;
            s.MultiLeft = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                var p = s.Cells[sq];
                if (p.IsEmpty || p.Side != me) continue;
                bool changed = false;
                if (p.StunTurns > 0) { p.StunTurns--; changed = true; }
                if (p.Has(Buff.Poison))
                {
                    p.PoisonTurns--;
                    changed = true;
                    if (p.PoisonTurns == 0)
                    {
                        p.Buffs &= ~Buff.Poison;
                        p.Tier = 0;
                        Emit(ev, EventKind.PoisonFaded, sq, sq, p.Type, me, PowerupKind.Poison);
                    }
                }
                if (p.Has(Buff.Armor))
                {
                    p.ArmorTurns--;
                    changed = true;
                    if (p.ArmorTurns == 0)
                    {
                        p.Buffs &= ~Buff.Armor;
                        Emit(ev, EventKind.ArmorExpired, sq, sq, p.Type, me, PowerupKind.SpikedArmor);
                    }
                }
                if (changed) s.Cells[sq] = p;
            }

            if (s.ClockReset)
            {
                s.HalfmoveClock = 0;
                s.ClockReset = false;
            }
            else s.HalfmoveClock++;

            s.Ply++;
            s.ToMove = me.Opponent();

            if (s.Mars[(int)me] > 0) s.Mars[(int)me]--;
            if (s.MissileLeft > 0 && --s.MissileLeft == 0) ResolveMissile(s, ev);

            if (!s.SearchMode && s.Config.SpawnInterval > 0 && s.Ply % s.Config.SpawnInterval == 0 && s.Ply >= s.NoSpawnUntilPly)
                TrySpawnPair(s, ev);

            if (!s.SearchMode) PushTurnStart(s);
        }

        /// <summary>Remembers the position at the start of this turn (the last three are kept) for Take back.</summary>
        static void PushTurnStart(GameState s)
        {
            var snap = s.Clone();
            snap.TurnStarts = null;
            var old = s.TurnStarts;
            s.TurnStarts = new[] { old == null ? null : old[1], old == null ? null : old[2], snap };
        }

        /// <summary>
        /// The missile lands on its quadrant: every piece there is destroyed (armored pieces and kings are not), boulders and
        /// powerup squares are cleared. A king left in the quadrant loses the game; both kings means a draw.
        /// </summary>
        static void ResolveMissile(GameState s, List<GameEvent> ev)
        {
            int f0 = Sq.File(s.MissileAnchor), r0 = Sq.Rank(s.MissileAnchor);
            bool whiteKing = false, blackKing = false;
            Emit(ev, EventKind.MissileStruck, s.MissileAnchor, s.MissileAnchor, PieceType.None, s.ToMove, PowerupKind.None);
            for (int f = f0; f < f0 + 4; f++)
            {
                for (int r = r0; r < r0 + 4; r++)
                {
                    int sq = Sq.Make(f, r);
                    var p = s.Cells[sq];
                    if (p.Type == PieceType.King) { if (p.Side == Side.White) whiteKing = true; else blackKing = true; }
                    else if (Geometry.IsVulnerableToBomb(p)) Destroy(s, sq, ev);
                    s.Boulders[sq] = false;
                    s.Mystery[sq] = false;
                    s.Content[sq] = 0;
                    if (s.Portal[sq]) { s.Portal[sq] = false; s.Portal[Sq.Mirror(sq)] = false; }
                }
            }
            s.MissileAnchor = -1;
            s.ClockReset = true;
            s.LostWhy = 1;
            if (whiteKing && blackKing) s.LostSide = 2;
            else if (whiteKing) s.LostSide = (int)Side.White;
            else if (blackKing) s.LostSide = (int)Side.Black;
            else s.LostWhy = 0;
        }

        // ---- board mutation helpers that keep castling rights honest ----

        static void Touch(GameState s, int sq)
        {
            switch (sq)
            {
                case 4: s.Castling &= ~(GameState.CastleWK | GameState.CastleWQ); break;
                case 0: s.Castling &= ~GameState.CastleWQ; break;
                case 7: s.Castling &= ~GameState.CastleWK; break;
                case 60: s.Castling &= ~(GameState.CastleBK | GameState.CastleBQ); break;
                case 56: s.Castling &= ~GameState.CastleBQ; break;
                case 63: s.Castling &= ~GameState.CastleBK; break;
            }
        }

        static void Remove(GameState s, int sq)
        {
            s.Cells[sq] = default(Piece);
            Touch(s, sq);
        }

        internal static void Place(GameState s, int sq, Piece p)
        {
            s.Cells[sq] = p;
            Touch(s, sq);
        }

        /// <summary>
        /// Activates a card: it leaves the bag (or, for an immediate card, stops being pending), the rival may cancel it with
        /// Nuh-uh, and otherwise it resolves. Playing a card costs the turn. Legality probes skip the reaction window.
        /// </summary>
        static void ActivateCard(GameState s, Side me, GameAction a, List<GameEvent> ev)
        {
            var kind = Cards.KindOf(a.Aux);
            if (s.PendingCard == kind) s.PendingCard = CardKind.None;
            else s.AddCard(me, kind, -1);
            Cards.Emit(ev, EventKind.CardPlayed, a.From, a.To, me, kind);
            if (!s.Probe && kind != CardKind.NuhUh && s.CardCount(me.Opponent(), CardKind.NuhUh) > 0)
            {
                s.RespCard = kind;
                s.RespFrom = a.From;
                s.RespTo = a.To;
                s.RespAux = a.Aux;
                return;
            }
            Cards.Resolve(s, me, a, ev);
            Finish(s, ev);
        }

        /// <summary>Records a lost piece (kings excluded) so Summon Caissa can bring it back.</summary>
        internal static void NoteLost(GameState s, Piece p)
        {
            if (p.Type == PieceType.None || p.Type == PieceType.King) return;
            int i = (int)p.Side * 8 + (int)p.Type;
            if (s.Captured[i] < 255) s.Captured[i]++;
        }

        internal static void Destroy(GameState s, int sq, List<GameEvent> ev)
        {
            var p = s.Cells[sq];
            NoteLost(s, p);
            Remove(s, sq);
            Emit(ev, EventKind.Destroyed, sq, sq, p.Type, p.Side, PowerupKind.None);
        }

        // ---- powerup squares ----

        /// <summary>Weighted draw from the pool, filtered for what this piece can actually receive.</summary>
        public static PowerupKind Draw(GameState s, Piece p, int sq)
        {
            var pool = s.Config.Pool;
            double f = LateRamp.Factor(s, p.Side);
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (Eligible(s, p, sq, pool[i].Kind)) total += LateRamp.Weigh(pool[i].Weight, LateRamp.RarePower(pool[i].Kind), f);
            }
            if (total <= 0) return PowerupKind.None;
            int roll = s.Rng.Range(total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (!Eligible(s, p, sq, pool[i].Kind)) continue;
                roll -= LateRamp.Weigh(pool[i].Weight, LateRamp.RarePower(pool[i].Kind), f);
                if (roll < 0) return pool[i].Kind;
            }
            return PowerupKind.None;
        }

        /// <summary>Kings take every powerup except Poison, Bombs and Transform, which make no sense for them. The square is used up.</summary>
        public static bool KingIncompatible(Piece p, PowerupKind kind)
        {
            return p.Type == PieceType.King && (kind == PowerupKind.Poison || kind == PowerupKind.SmallBomb
                || kind == PowerupKind.BigBomb || PowerupInfo.IsTransform(kind));
        }

        /// <summary>Can this piece receive this powerup right now? Kings only take perks, caps and duplicates apply.</summary>
        public static bool Eligible(GameState s, Piece p, int sq, PowerupKind kind)
        {
            bool perk = PowerupInfo.IsPerk(kind);
            if (KingIncompatible(p, kind)) return false;
            if (perk && s.Perks(p.Side, kind) >= s.Config.MaxPerks) return false;
            if (PowerupInfo.IsTransform(kind))
            {
                var target = PowerupInfo.TransformTarget(kind);
                if (target == p.Type) return false;
                if (target == PieceType.Pawn && (Sq.Rank(sq) == 0 || Sq.Rank(sq) == 7)) return false;
            }
            if (kind == PowerupKind.DoubleMove && (p.Has(Buff.DoubleMove) || p.Has(Buff.TripleMove))) return false;
            if (kind == PowerupKind.TripleMove && (p.Has(Buff.DoubleMove) || p.Has(Buff.TripleMove))) return false;
            if (kind == PowerupKind.SpikedArmor && p.Has(Buff.Armor)) return false;
            if (kind == PowerupKind.Swap && !s.Config.SwapAllowed[(int)p.Side]) return false;
            if (kind == PowerupKind.Rewind && !s.Config.RewindAllowed[(int)p.Side]) return false;
            return true;
        }

        public static void ApplyPickup(GameState s, Side me, int sq, PowerupKind kind, List<GameEvent> ev)
        {
            var before = s.Cells[sq];
            int mark = ev == null ? 0 : ev.Count;
            ApplyPickupCore(s, me, sq, kind, ev);
            // A bomb makes its holder part of any enemy blast chain, which can put your own king in check. Refuse it then.
            if ((kind == PowerupKind.SmallBomb || kind == PowerupKind.BigBomb) && Geometry.IsKingAttacked(s, me))
            {
                s.Cells[sq] = before;
                if (ev != null)
                {
                    ev.RemoveRange(mark, ev.Count - mark);
                    Emit(ev, EventKind.PickupRejected, sq, sq, before.Type, me, kind);
                }
            }
        }

        static void ApplyPickupCore(GameState s, Side me, int sq, PowerupKind kind, List<GameEvent> ev)
        {
            var p = s.Cells[sq];
            switch (kind)
            {
                case PowerupKind.DoubleMove: p.Buffs |= Buff.DoubleMove; break;
                case PowerupKind.TripleMove: p.Buffs |= Buff.TripleMove; break;
                case PowerupKind.Ghost:
                    if (p.Has(Buff.Ghost)) { MaxOut(s, sq, p, kind, ev); return; }
                    ClearBanked(ref p);
                    p.Buffs |= Buff.Ghost;
                    p.GhostTurns = (byte)s.Config.GhostTurns;
                    p.Tier = 1;
                    break;
                case PowerupKind.Poison:
                    if (p.Has(Buff.Poison)) { MaxOut(s, sq, p, kind, ev); return; }
                    ClearBanked(ref p);
                    p.Buffs |= Buff.Poison;
                    p.PoisonTurns = PoisonStartTurns;
                    p.Tier = 1;
                    break;
                case PowerupKind.Fireball:
                    if (p.Has(Buff.Fireball))
                    {
                        if (p.Tier >= 2 || !FighterUnlocks.TierTwoOpen(kind, s.Config.UnlockLevel)) { MaxOut(s, sq, p, kind, ev); return; }
                        p.Tier = 2;
                        s.Cells[sq] = p;
                        Emit(ev, EventKind.PowerUpgraded, sq, sq, p.Type, me, kind);
                        return;
                    }
                    ClearBanked(ref p);
                    p.Buffs |= Buff.Fireball;
                    p.Tier = 1;
                    break;
                case PowerupKind.SpikedArmor:
                    p.Buffs |= Buff.Armor;
                    p.ArmorTurns = ArmorStartTurns;
                    break;
                case PowerupKind.Harpoon:
                    if (p.Has(Buff.Harpoon))
                    {
                        if (p.Tier >= 2 || !FighterUnlocks.TierTwoOpen(kind, s.Config.UnlockLevel)) { MaxOut(s, sq, p, kind, ev); return; }
                        p.Tier = 2;
                        s.Cells[sq] = p;
                        Emit(ev, EventKind.PowerUpgraded, sq, sq, p.Type, me, kind);
                        return;
                    }
                    ClearBanked(ref p);
                    p.Buffs |= Buff.Harpoon;
                    p.Tier = 1;
                    break;
                case PowerupKind.SmallBomb:
                    if (p.Has(Buff.BigBomb)) { MaxOut(s, sq, p, kind, ev); return; }
                    if (p.Has(Buff.SmallBomb) && !FighterUnlocks.TierTwoOpen(kind, s.Config.UnlockLevel)) { MaxOut(s, sq, p, kind, ev); return; }
                    if (p.Has(Buff.SmallBomb))
                    {
                        // Landing on Bomb again upgrades it to the big blast.
                        p.Buffs = (p.Buffs & ~Buff.SmallBomb) | Buff.BigBomb;
                        p.Tier = 2;
                        s.Cells[sq] = p;
                        Emit(ev, EventKind.PowerUpgraded, sq, sq, p.Type, me, kind);
                        return;
                    }
                    ClearBanked(ref p);
                    p.Buffs |= Buff.SmallBomb;
                    p.Tier = 1;
                    break;
                case PowerupKind.BigBomb:
                    if (p.Has(Buff.BigBomb)) { MaxOut(s, sq, p, kind, ev); return; }
                    ClearBanked(ref p);
                    p.Buffs |= Buff.BigBomb;
                    p.Tier = 2;
                    break;
                case PowerupKind.DrawCard:
                {
                    // No hidden draws in the AI's lookahead, and none in legality probes.
                    if (s.SearchMode) break;
                    var card = Cards.Draw(s, me);
                    if (card == CardKind.None) break;
                    Cards.Emit(ev, EventKind.CardDrawn, sq, sq, me, card);
                    if (CardInfo.IsImmediate(card)) s.PendingCard = card;
                    else s.AddCard(me, card, 1);
                    break;
                }
                case PowerupKind.StunBolt:
                case PowerupKind.StealGrab:
                    s.EffectKind = kind;
                    s.EffectSquare = sq;
                    break;
                case PowerupKind.Combo2:
                    s.ComboGain += 2;
                    s.ComboSquare = sq;
                    break;
                case PowerupKind.Combo4:
                    s.ComboGain += 4;
                    s.ComboSquare = sq;
                    break;
                case PowerupKind.Rewind:
                case PowerupKind.Swap:
                case PowerupKind.Steal:
                case PowerupKind.Stun:
                case PowerupKind.Boulder:
                case PowerupKind.Reroll:
                    s.AddPerk(me, kind, 1);
                    Emit(ev, EventKind.PerkGained, sq, sq, p.Type, me, kind);
                    return;
                default:
                    if (PowerupInfo.IsTransform(kind))
                    {
                        p.Type = PowerupInfo.TransformTarget(kind);
                        Place(s, sq, p);
                        s.ClockReset = true;
                        Emit(ev, EventKind.Transformed, sq, sq, p.Type, me, kind);
                    }
                    return;
            }
            s.Cells[sq] = p;
            Emit(ev, EventKind.BuffGained, sq, sq, p.Type, me, kind);
        }

        const Buff BankedMask = Buff.Fireball | Buff.Poison | Buff.SmallBomb | Buff.BigBomb | Buff.Ghost | Buff.Harpoon;

        /// <summary>A piece holds one banked move at a time, so taking a different one wipes the old one.</summary>
        static void ClearBanked(ref Piece p)
        {
            p.Buffs &= ~BankedMask;
            p.GhostTurns = 0;
            p.PoisonTurns = 0;
            p.Tier = 0;
        }

        /// <summary>Landed on a power the piece already holds at its top tier: the square is used up, nothing changes.</summary>
        static void MaxOut(GameState s, int sq, Piece p, PowerupKind kind, List<GameEvent> ev)
        {
            Emit(ev, EventKind.PowerMaxed, sq, sq, p.Type, p.Side, kind);
        }

        /// <summary>Adds one mirrored pair of mystery squares on the middle ranks. Returns false if there is no room.</summary>
        public static bool TrySpawnPair(GameState s, List<GameEvent> ev, bool allowPortals = true)
        {
            if (allowPortals && s.Config.PortalChance > 0 && s.PortalPairs() < s.Config.MaxPortalPairs
                && s.Rng.Range(1000) < (int)(s.Config.PortalChance * 1000))
            {
                if (TrySpawnPortals(s, ev)) return true;
            }
            if (s.MysteryCount() + 2 > s.Config.MaxMysterySquares) return false;
            var candidates = new List<int>(32);
            for (int sq = 0; sq < 64; sq++)
            {
                int rank = Sq.Rank(sq);
                if (rank < 2 || rank > 5) continue;
                int mirror = Sq.Mirror(sq);
                if (sq > mirror) continue;
                if (IsSpawnable(s, sq) && IsSpawnable(s, mirror)) candidates.Add(sq);
            }
            if (candidates.Count == 0) return false;
            int pick = candidates[s.Rng.Range(candidates.Count)];
            s.Mystery[pick] = true;
            s.Mystery[Sq.Mirror(pick)] = true;
            var shown = PowerupKind.None;
            if (s.Config.Squares == SquareMode.Revealed && s.Rng.Range(1000) >= (int)(s.Config.MysteryChance * 1000))
            {
                // Both squares of a mirrored pair hold the same powerup, so neither side is favoured.
                shown = DrawForSquare(s);
                s.Content[pick] = (byte)shown;
                s.Content[Sq.Mirror(pick)] = (byte)shown;
            }
            Emit(ev, EventKind.SquareSpawned, pick, Sq.Mirror(pick), PieceType.None, s.ToMove, shown);
            return true;
        }

        static bool TrySpawnPortals(GameState s, List<GameEvent> ev)
        {
            var candidates = new List<int>(32);
            for (int sq = 0; sq < 64; sq++)
            {
                int rank = Sq.Rank(sq);
                if (rank < 2 || rank > 5) continue;
                int mirror = Sq.Mirror(sq);
                if (sq > mirror) continue;
                if (IsSpawnable(s, sq) && IsSpawnable(s, mirror)) candidates.Add(sq);
            }
            if (candidates.Count == 0) return false;
            int pick = candidates[s.Rng.Range(candidates.Count)];
            s.Portal[pick] = true;
            s.Portal[Sq.Mirror(pick)] = true;
            Emit(ev, EventKind.PortalSpawned, pick, Sq.Mirror(pick), PieceType.None, s.ToMove, PowerupKind.None);
            return true;
        }

        /// <summary>Weighted draw from the pool for a revealed square (no particular piece in mind).</summary>
        public static PowerupKind DrawForSquare(GameState s)
        {
            var pool = s.Config.Pool;
            bool rewind = s.Config.RewindAllowed[0] || s.Config.RewindAllowed[1];
            bool swap = s.Config.SwapAllowed[0] || s.Config.SwapAllowed[1];
            double f = LateRamp.Factor(s, null);
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Kind == PowerupKind.Rewind && !rewind) continue;
                if (pool[i].Kind == PowerupKind.Swap && !swap) continue;
                total += LateRamp.Weigh(pool[i].Weight, LateRamp.RarePower(pool[i].Kind), f);
            }
            if (total <= 0) return PowerupKind.None;
            int roll = s.Rng.Range(total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Kind == PowerupKind.Rewind && !rewind) continue;
                if (pool[i].Kind == PowerupKind.Swap && !swap) continue;
                roll -= LateRamp.Weigh(pool[i].Weight, LateRamp.RarePower(pool[i].Kind), f);
                if (roll < 0) return pool[i].Kind;
            }
            return PowerupKind.None;
        }

        static bool IsSpawnable(GameState s, int sq)
        {
            return s.Cells[sq].IsEmpty && !s.Boulders[sq] && !s.Mystery[sq] && !s.Portal[sq];
        }

        // ---- game end ----

        public static GameResult GetResult(GameState s)
        {
            string reason;
            return GetResult(s, out reason);
        }

        /// <summary>Result plus a short human-readable reason ("Checkmate", "Stalemate", ...). Empty while ongoing.</summary>
        public static GameResult GetResult(GameState s, out string reason)
        {
            reason = "";
            if (s.LostSide == 2)
            {
                reason = "Both kings were hit";
                return GameResult.Draw;
            }
            if (s.LostSide >= 0)
            {
                reason = s.LostWhy == 1 ? "Missile strike" : "Checkmate";
                return s.LostSide == (int)Side.White ? GameResult.BlackWins : GameResult.WhiteWins;
            }
            if (s.PendingSquare < 0 && s.ForcedMover < 0 && s.EffectKind == PowerupKind.None && s.PendingCard == CardKind.None && s.RespCard == CardKind.None)
            {
                // Insufficient material: bare kings.
                bool onlyKings = true;
                for (int i = 0; i < 64 && onlyKings; i++)
                {
                    var p = s.Cells[i];
                    if (!p.IsEmpty && p.Type != PieceType.King) onlyKings = false;
                }
                if (onlyKings)
                {
                    reason = "Only kings left";
                    return GameResult.Draw;
                }
            }

            if (s.HalfmoveClock >= 100)
            {
                reason = "50-move rule";
                return GameResult.Draw;
            }
            if (s.Ply >= s.Config.MaxPly)
            {
                reason = "Move limit reached";
                return GameResult.Draw;
            }

            if (MoveGen.LegalActions(s).Count == 0)
            {
                if (Geometry.IsKingAttacked(s, s.ToMove))
                {
                    reason = "Checkmate";
                    return s.ToMove == Side.White ? GameResult.BlackWins : GameResult.WhiteWins;
                }
                reason = "Stalemate";
                return GameResult.Draw;
            }
            return GameResult.Ongoing;
        }

        internal static void Emit(List<GameEvent> ev, EventKind kind, int from, int to, PieceType piece, Side side, PowerupKind power)
        {
            if (ev == null) return;
            ev.Add(new GameEvent { Kind = kind, From = from, To = to, Piece = piece, Side = side, Powerup = power });
        }
    }
}
