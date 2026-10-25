using System;
using System.Collections.Generic;

namespace PowerChess.Core
{
    /// <summary>Applies actions to a <see cref="GameState"/> and resolves powerups. Assumes the action is legal.</summary>
    public static class Rules
    {
        const int ArmorStartTurns = 4; // 1 for the pickup turn itself + 3 more owner turns
        const int StunDuration = 3;

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
                    shooter.Buffs &= ~Buff.Fireball;
                    s.Cells[a.From] = shooter;
                    var victims = new List<int>(4);
                    Geometry.FireballVictims(s, a.From, a.Aux, victims);
                    Emit(ev, EventKind.FireballFired, a.From, a.To, s.Cells[a.From].Type, me, PowerupKind.Fireball);
                    foreach (int v in victims) Destroy(s, v, ev);
                    s.ClockReset = true;
                    Finish(s, ev);
                    return;
                }

                case ActionKind.Steal:
                {
                    s.AddPerk(me, PowerupKind.Steal, -1);
                    var stolen = new Piece(s.Cells[a.To].Type, me);
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
                    var bomber = s.Cells[a.From];
                    int radius = Geometry.BombRadius(bomber);
                    var area = new List<int>(24);
                    Geometry.AreaAround(a.From, radius, area);
                    Emit(ev, EventKind.Detonated, a.From, a.From, bomber.Type, me,
                        radius > 1 ? PowerupKind.BigBomb : PowerupKind.SmallBomb);
                    foreach (int t in area)
                    {
                        if (Geometry.IsVulnerableToBomb(s.Cells[t])) Destroy(s, t, ev);
                        if (s.Boulders[t])
                        {
                            s.Boulders[t] = false;
                            Emit(ev, EventKind.BoulderCleared, t, t, PieceType.None, me, PowerupKind.SmallBomb);
                        }
                    }
                    bomber.Buffs &= ~(Buff.SmallBomb | Buff.BigBomb);
                    s.Cells[a.From] = bomber;
                    s.ClockReset = true;
                    Finish(s, ev);
                    return;
                }
            }
        }

        /// <summary>A pawn swapped onto the far rank becomes a Queen.</summary>
        static void PromoteIfAtEnd(GameState s, int sq, List<GameEvent> ev, Side me)
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
                Remove(s, capSq);
                Emit(ev, EventKind.Captured, capSq, capSq, captured.Type, me, PowerupKind.None);
            }
            else if (!target.IsEmpty)
            {
                captured = target;
                didCapture = true;
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
            Place(s, a.To, mover);

            if (didCapture || origType == PieceType.Pawn) s.ClockReset = true;
            if (origType == PieceType.Pawn && Math.Abs(Sq.Rank(a.To) - Sq.Rank(a.From)) == 2
                && s.Cells[(a.From + a.To) / 2].IsEmpty && !s.Boulders[(a.From + a.To) / 2])
                s.EnPassant = (a.From + a.To) / 2; // a ghost pawn's leap over a blocker sets none

            bool alive = true;
            if (didCapture && captured.Has(Buff.Poison) && mover.Type != PieceType.King && !mover.Has(Buff.Armor))
            {
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

        /// <summary>Everything that happens once an action and its pickup are fully resolved.</summary>
        static void Finish(GameState s, List<GameEvent> ev)
        {
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
            Side me = s.ToMove;
            s.MultiLeft = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                var p = s.Cells[sq];
                if (p.IsEmpty || p.Side != me) continue;
                bool changed = false;
                if (p.StunTurns > 0) { p.StunTurns--; changed = true; }
                if (p.Has(Buff.Ghost))
                {
                    p.GhostTurns--;
                    changed = true;
                    if (p.GhostTurns == 0)
                    {
                        p.Buffs &= ~Buff.Ghost;
                        Emit(ev, EventKind.GhostFaded, sq, sq, p.Type, me, PowerupKind.Ghost);
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

            if (!s.SearchMode && s.Config.SpawnInterval > 0 && s.Ply % s.Config.SpawnInterval == 0)
                TrySpawnPair(s, ev);
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

        static void Place(GameState s, int sq, Piece p)
        {
            s.Cells[sq] = p;
            Touch(s, sq);
        }

        static void Destroy(GameState s, int sq, List<GameEvent> ev)
        {
            var p = s.Cells[sq];
            Remove(s, sq);
            Emit(ev, EventKind.Destroyed, sq, sq, p.Type, p.Side, PowerupKind.None);
        }

        // ---- powerup squares ----

        /// <summary>Weighted draw from the pool, filtered for what this piece can actually receive.</summary>
        public static PowerupKind Draw(GameState s, Piece p, int sq)
        {
            var pool = s.Config.Pool;
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (Eligible(s, p, sq, pool[i].Kind)) total += pool[i].Weight;
            }
            if (total <= 0) return PowerupKind.None;
            int roll = s.Rng.Range(total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (!Eligible(s, p, sq, pool[i].Kind)) continue;
                roll -= pool[i].Weight;
                if (roll < 0) return pool[i].Kind;
            }
            return PowerupKind.None;
        }

        /// <summary>Kings take every powerup except Poison and Transform, which make no sense for them.</summary>
        public static bool KingIncompatible(Piece p, PowerupKind kind)
        {
            return p.Type == PieceType.King && (kind == PowerupKind.Poison || PowerupInfo.IsTransform(kind));
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
            if (kind == PowerupKind.Ghost && p.Has(Buff.Ghost)) return false;
            if (kind == PowerupKind.SpikedArmor && p.Has(Buff.Armor)) return false;
            if (kind == PowerupKind.Swap && !s.Config.SwapAllowed[(int)p.Side]) return false;
            if (kind == PowerupKind.Fireball && p.Has(Buff.Fireball)) return false;
            if (kind == PowerupKind.Rewind && !s.Config.RewindAllowed[(int)p.Side]) return false;
            return true;
        }

        public static void ApplyPickup(GameState s, Side me, int sq, PowerupKind kind, List<GameEvent> ev)
        {
            var p = s.Cells[sq];
            switch (kind)
            {
                case PowerupKind.DoubleMove: p.Buffs |= Buff.DoubleMove; break;
                case PowerupKind.TripleMove: p.Buffs |= Buff.TripleMove; break;
                case PowerupKind.Ghost:
                    p.Buffs |= Buff.Ghost;
                    p.GhostTurns = (byte)s.Config.GhostTurns;
                    break;
                case PowerupKind.Poison: p.Buffs |= Buff.Poison; break;
                case PowerupKind.Fireball: p.Buffs |= Buff.Fireball; break;
                case PowerupKind.SpikedArmor:
                    p.Buffs |= Buff.Armor;
                    p.ArmorTurns = ArmorStartTurns;
                    break;
                case PowerupKind.SmallBomb:
                    if (!p.Has(Buff.BigBomb)) p.Buffs |= Buff.SmallBomb;
                    break;
                case PowerupKind.BigBomb:
                    p.Buffs &= ~Buff.SmallBomb;
                    p.Buffs |= Buff.BigBomb;
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
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Kind == PowerupKind.Rewind && !rewind) continue;
                if (pool[i].Kind == PowerupKind.Swap && !swap) continue;
                total += pool[i].Weight;
            }
            if (total <= 0) return PowerupKind.None;
            int roll = s.Rng.Range(total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Kind == PowerupKind.Rewind && !rewind) continue;
                if (pool[i].Kind == PowerupKind.Swap && !swap) continue;
                roll -= pool[i].Weight;
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
            if (s.PendingSquare < 0 && s.ForcedMover < 0)
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

        static void Emit(List<GameEvent> ev, EventKind kind, int from, int to, PieceType piece, Side side, PowerupKind power)
        {
            if (ev == null) return;
            ev.Add(new GameEvent { Kind = kind, From = from, To = to, Piece = piece, Side = side, Powerup = power });
        }
    }
}
