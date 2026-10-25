using System.Collections.Generic;

namespace PowerChess.Core
{
    public static class MoveGen
    {
        static readonly PieceType[] PromotionTypes =
            { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

        /// <summary>Every legal action for the side to move, including perks, bombs and pickup decisions.</summary>
        public static List<GameAction> LegalActions(GameState s)
        {
            var result = new List<GameAction>(64);
            Side me = s.ToMove;

            if (s.PendingSquare >= 0)
            {
                result.Add(new GameAction(ActionKind.KeepPickup, s.PendingSquare, -1, 0));
                if (s.Perks(me, PowerupKind.Reroll) > 0)
                    result.Add(new GameAction(ActionKind.RerollPickup, s.PendingSquare, -1, 0));
                return result;
            }

            var pseudo = new List<GameAction>(96);
            if (s.ForcedMover >= 0)
            {
                GenPiece(s, s.ForcedMover, pseudo, false);
            }
            else
            {
                for (int sq = 0; sq < 64; sq++)
                {
                    var p = s.Cells[sq];
                    if (!p.IsEmpty && p.Side == me) GenPiece(s, sq, pseudo, true);
                }
                GenSpecials(s, pseudo);
            }

            foreach (var a in pseudo)
            {
                if (IsLegal(s, a)) result.Add(a);
            }
            return result;
        }

        /// <summary>Applies the action to a copy and verifies the mover's king is not left attacked.</summary>
        public static bool IsLegal(GameState s, GameAction a)
        {
            var c = s.Clone();
            c.SearchMode = true;
            c.Probe = true;
            Rules.Apply(c, a, null);
            return !Geometry.IsKingAttacked(c, s.ToMove);
        }

        /// <summary>Would a plain move from <paramref name="from"/> to <paramref name="to"/> exist if king safety were ignored? Used to explain blocked moves.</summary>
        public static bool HasPseudoMove(GameState s, int from, int to)
        {
            var list = new List<GameAction>(32);
            GenPiece(s, from, list, false);
            foreach (var a in list) if (a.To == to) return true;
            return false;
        }

        /// <summary>Used to decide whether a Double Move piece has a legal second move.</summary>
        public static bool HasLegalMoveFrom(GameState s, int sq)
        {
            var pseudo = new List<GameAction>(32);
            GenPiece(s, sq, pseudo, false);
            foreach (var a in pseudo)
            {
                if (IsLegal(s, a)) return true;
            }
            return false;
        }

        static void GenPiece(GameState s, int from, List<GameAction> o, bool castle)
        {
            var p = s.Cells[from];
            if (p.IsEmpty || p.Side != s.ToMove || p.IsStunned) return;
            int f = Sq.File(from), r = Sq.Rank(from);

            switch (p.Type)
            {
                case PieceType.Pawn:
                    GenPawn(s, p, from, o);
                    break;
                case PieceType.Knight:
                    for (int i = 0; i < 8; i++)
                    {
                        int nf = f + Geometry.KnightFile[i], nr = r + Geometry.KnightRank[i];
                        if (Sq.OnBoard(nf, nr)) AddTarget(s, p, from, Sq.Make(nf, nr), o);
                    }
                    if (p.Has(Buff.Ghost))
                    {
                        // A ghost knight can also take a king's step.
                        for (int d = 0; d < 8; d++)
                        {
                            int nf = f + Geometry.DirFile[d], nr = r + Geometry.DirRank[d];
                            if (Sq.OnBoard(nf, nr)) AddTarget(s, p, from, Sq.Make(nf, nr), o);
                        }
                    }
                    break;
                case PieceType.King:
                    for (int d = 0; d < 8; d++)
                    {
                        int nf = f + Geometry.DirFile[d], nr = r + Geometry.DirRank[d];
                        if (Sq.OnBoard(nf, nr)) AddTarget(s, p, from, Sq.Make(nf, nr), o);
                    }
                    if (p.Has(Buff.Ghost))
                    {
                        // A ghost king reaches two squares out in any direction, through whatever is in between.
                        for (int d = 0; d < 8; d++)
                        {
                            int nf = f + 2 * Geometry.DirFile[d], nr = r + 2 * Geometry.DirRank[d];
                            if (Sq.OnBoard(nf, nr)) AddTarget(s, p, from, Sq.Make(nf, nr), o);
                        }
                    }
                    if (castle && !p.Has(Buff.Ghost)) GenCastle(s, p, from, o);
                    break;
                default:
                    int first = p.Type == PieceType.Bishop ? 4 : 0;
                    int last = p.Type == PieceType.Rook ? 3 : 7;
                    bool ghost = p.Has(Buff.Ghost);
                    for (int d = first; d <= last; d++)
                    {
                        int cf = f + Geometry.DirFile[d], cr = r + Geometry.DirRank[d];
                        while (Sq.OnBoard(cf, cr))
                        {
                            int to = Sq.Make(cf, cr);
                            if (s.Boulders[to])
                            {
                                if (!ghost) break;
                                cf += Geometry.DirFile[d];
                                cr += Geometry.DirRank[d];
                                continue;
                            }
                            bool empty = s.Cells[to].IsEmpty;
                            AddTarget(s, p, from, to, o);
                            if (!empty && !ghost) break;
                            cf += Geometry.DirFile[d];
                            cr += Geometry.DirRank[d];
                        }
                    }
                    break;
            }
        }

        static void AddTarget(GameState s, Piece p, int from, int to, List<GameAction> o)
        {
            if (s.Boulders[to]) return;
            var t = s.Cells[to];
            if (t.IsEmpty)
            {
                o.Add(GameAction.Move(from, to));
                return;
            }
            if (!Geometry.IsVulnerable(t, p.Side)) return;
            o.Add(GameAction.Move(from, to));
        }

        static void GenPawn(GameState s, Piece p, int from, List<GameAction> o)
        {
            int dir = p.Side == Side.White ? 1 : -1;
            int startRank = p.Side == Side.White ? 1 : 6;
            int lastRank = p.Side == Side.White ? 7 : 0;
            int f = Sq.File(from), r = Sq.Rank(from);
            int r1 = r + dir;
            if (r1 < 0 || r1 > 7) return;

            int to1 = Sq.Make(f, r1);
            if (p.Has(Buff.Ghost) && !(s.Cells[to1].IsEmpty && !s.Boulders[to1]))
            {
                // A ghost pawn leapfrogs whatever blocks it and lands one square beyond, if that is free.
                int r2 = r + 2 * dir;
                if (r2 >= 0 && r2 <= 7)
                {
                    int to2 = Sq.Make(f, r2);
                    if (s.Cells[to2].IsEmpty && !s.Boulders[to2]) AddPawnMove(from, to2, r2 == lastRank, o);
                }
            }
            if (s.Cells[to1].IsEmpty && !s.Boulders[to1])
            {
                AddPawnMove(from, to1, r1 == lastRank, o);
                if (r == startRank)
                {
                    int to2 = Sq.Make(f, r + 2 * dir);
                    if (s.Cells[to2].IsEmpty && !s.Boulders[to2]) o.Add(GameAction.Move(from, to2));
                }
            }

            for (int df = -1; df <= 1; df += 2)
            {
                int nf = f + df;
                if (nf < 0 || nf > 7) continue;
                int to = Sq.Make(nf, r1);
                var t = s.Cells[to];
                if (!t.IsEmpty)
                {
                    if (!Geometry.IsVulnerable(t, p.Side)) continue;
                    AddPawnMove(from, to, r1 == lastRank, o);
                }
                else if (to == s.EnPassant && !s.Boulders[to])
                {
                    var victim = s.Cells[Sq.Make(nf, r)];
                    if (victim.Type == PieceType.Pawn && victim.Side != p.Side && !victim.Has(Buff.Armor))
                        o.Add(GameAction.Move(from, to));
                }
            }
        }

        static void AddPawnMove(int from, int to, bool promotes, List<GameAction> o)
        {
            if (!promotes)
            {
                o.Add(GameAction.Move(from, to));
                return;
            }
            foreach (var t in PromotionTypes) o.Add(GameAction.Move(from, to, t));
        }

        static bool IsFree(GameState s, int sq)
        {
            return s.Cells[sq].IsEmpty && !s.Boulders[sq];
        }

        static void GenCastle(GameState s, Piece king, int from, List<GameAction> o)
        {
            Side me = king.Side;
            int home = me == Side.White ? 4 : 60;
            if (from != home) return;
            Side them = me.Opponent();
            if (Geometry.IsAttacked(s, from, them)) return;

            int kBit = me == Side.White ? GameState.CastleWK : GameState.CastleBK;
            int qBit = me == Side.White ? GameState.CastleWQ : GameState.CastleBQ;

            if ((s.Castling & kBit) != 0 && RookReady(s, home + 3, me)
                && IsFree(s, home + 1) && IsFree(s, home + 2)
                && !Geometry.IsAttacked(s, home + 1, them) && !Geometry.IsAttacked(s, home + 2, them))
                o.Add(GameAction.Move(from, home + 2));

            if ((s.Castling & qBit) != 0 && RookReady(s, home - 4, me)
                && IsFree(s, home - 1) && IsFree(s, home - 2) && IsFree(s, home - 3)
                && !Geometry.IsAttacked(s, home - 1, them) && !Geometry.IsAttacked(s, home - 2, them))
                o.Add(GameAction.Move(from, home - 2));
        }

        static bool RookReady(GameState s, int sq, Side me)
        {
            var rook = s.Cells[sq];
            return rook.Type == PieceType.Rook && rook.Side == me && !rook.IsStunned;
        }

        /// <summary>Perk uses and bomb detonations. Each one is spent instead of a move.</summary>
        static void GenSpecials(GameState s, List<GameAction> o)
        {
            Side me = s.ToMove;
            bool hasSteal = s.Perks(me, PowerupKind.Steal) > 0;
            bool hasStun = s.Perks(me, PowerupKind.Stun) > 0;
            var scratch = new List<int>(16);

            for (int sq = 0; sq < 64; sq++)
            {
                var p = s.Cells[sq];
                if (p.IsEmpty || p.Side != me) continue;

                if (p.Has(Buff.Fireball))
                {
                    // The piece that earned the fireball can fire it in any of 8 directions; only offer shots that hit something.
                    for (int dir = 0; dir < 8; dir++)
                    {
                        scratch.Clear();
                        Geometry.FireballVictims(s, sq, dir, scratch);
                        if (scratch.Count > 0) o.Add(new GameAction(ActionKind.Fireball, sq, scratch[0], dir));
                    }
                }

                if (hasSteal)
                {
                    scratch.Clear();
                    Geometry.AttackedPieces(s, sq, scratch);
                    foreach (int t in scratch)
                    {
                        if (Geometry.IsVulnerable(s.Cells[t], me)) o.Add(new GameAction(ActionKind.Steal, sq, t, 0));
                    }
                }

                int radius = Geometry.BombRadius(p);
                if (radius > 0)
                {
                    scratch.Clear();
                    Geometry.AreaAround(sq, radius, scratch);
                    bool any = false;
                    foreach (int t in scratch)
                    {
                        if (s.Boulders[t] || Geometry.IsVulnerableToBomb(s.Cells[t])) { any = true; break; }
                    }
                    if (any) o.Add(new GameAction(ActionKind.Detonate, sq, -1, 0));
                }
            }

            if (hasStun)
            {
                for (int sq = 0; sq < 64; sq++)
                {
                    var t = s.Cells[sq];
                    if (Geometry.IsVulnerable(t, me) && !t.IsStunned)
                        o.Add(new GameAction(ActionKind.Stun, sq, sq, 0));
                }
            }

            if (s.Perks(me, PowerupKind.Boulder) > 0 && s.BoulderCount() < s.Config.MaxBoulders)
            {
                for (int sq = 0; sq < 64; sq++)
                {
                    if (s.Cells[sq].IsEmpty && !s.Boulders[sq] && !s.Mystery[sq] && !s.Portal[sq])
                        o.Add(new GameAction(ActionKind.PlaceBoulder, sq, sq, 0));
                }
            }

            if (s.Perks(me, PowerupKind.Swap) > 0)
            {
                for (int a = 0; a < 64; a++)
                {
                    var pa = s.Cells[a];
                    if (pa.IsEmpty || pa.Side != me || pa.Type == PieceType.King) continue;
                    for (int b = a + 1; b < 64; b++)
                    {
                        var pb = s.Cells[b];
                        if (pb.IsEmpty || pb.Side != me || pb.Type == PieceType.King) continue;
                        if (pa.Type == pb.Type && pa.Buffs == pb.Buffs) continue;
                        // A pawn may land on the far rank (it promotes) but never on its own back rank.
                        if (pa.Type == PieceType.Pawn && Sq.Rank(b) == (pa.Side == Side.White ? 0 : 7)) continue;
                        if (pb.Type == PieceType.Pawn && Sq.Rank(a) == (pb.Side == Side.White ? 0 : 7)) continue;
                        o.Add(new GameAction(ActionKind.Swap, a, b, 0));
                    }
                }
            }

            if (s.Perks(me, PowerupKind.Reroll) > 0)
            {
                for (int sq = 0; sq < 64; sq++)
                {
                    if (s.Boulders[sq]) o.Add(new GameAction(ActionKind.ClearBoulder, sq, sq, 0));
                }
            }
        }
    }
}
