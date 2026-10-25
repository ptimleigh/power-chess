using System.Collections.Generic;

namespace PowerChess.Core
{
    /// <summary>Attack detection and line/area geometry shared by move generation and powerup effects.</summary>
    public static class Geometry
    {
        // Directions 0-3 are orthogonal, 4-7 diagonal.
        public static readonly int[] DirFile = { 1, -1, 0, 0, 1, 1, -1, -1 };
        public static readonly int[] DirRank = { 0, 0, 1, -1, 1, -1, 1, -1 };
        public static readonly int[] KnightFile = { 1, 2, 2, 1, -1, -2, -2, -1 };
        public static readonly int[] KnightRank = { 2, 1, -1, -2, -2, -1, 1, 2 };

        public static int FindKing(GameState s, Side side)
        {
            for (int i = 0; i < 64; i++)
            {
                var p = s.Cells[i];
                if (p.Type == PieceType.King && p.Side == side) return i;
            }
            return -1;
        }

        public static bool IsKingAttacked(GameState s, Side side)
        {
            int k = FindKing(s, side);
            if (k < 0) return false;
            if (s.Cells[k].Has(Buff.Armor)) return false; // an armored king cannot be checked
            return IsAttacked(s, k, side.Opponent());
        }

        /// <summary>Is <paramref name="sq"/> attacked by <paramref name="by"/>? Stunned pieces exert no attacks; boulders block lines.</summary>
        public static bool IsAttacked(GameState s, int sq, Side by)
        {
            int f = Sq.File(sq), r = Sq.Rank(sq);

            int pawnRank = r - (by == Side.White ? 1 : -1);
            if (pawnRank >= 0 && pawnRank < 8)
            {
                for (int df = -1; df <= 1; df += 2)
                {
                    int pf = f + df;
                    if (pf < 0 || pf > 7) continue;
                    var p = s.Cells[Sq.Make(pf, pawnRank)];
                    if (p.Type == PieceType.Pawn && p.Side == by && p.StunTurns == 0) return true;
                }
            }

            for (int i = 0; i < 8; i++)
            {
                int nf = f + KnightFile[i], nr = r + KnightRank[i];
                if (!Sq.OnBoard(nf, nr)) continue;
                var p = s.Cells[Sq.Make(nf, nr)];
                if (p.Type == PieceType.Knight && p.Side == by && p.StunTurns == 0) return true;
            }

            for (int d = 0; d < 8; d++)
            {
                int cf = f + DirFile[d], cr = r + DirRank[d];
                bool first = true;
                while (Sq.OnBoard(cf, cr))
                {
                    int idx = Sq.Make(cf, cr);
                    if (s.Boulders[idx]) break;
                    var p = s.Cells[idx];
                    if (!p.IsEmpty)
                    {
                        if (p.Side == by && p.StunTurns == 0)
                        {
                            if (p.Type == PieceType.Queen) return true;
                            if (d < 4 && p.Type == PieceType.Rook) return true;
                            if (d >= 4 && p.Type == PieceType.Bishop) return true;
                            if (first && p.Type == PieceType.King) return true;
                        }
                        break;
                    }
                    first = false;
                    cf += DirFile[d];
                    cr += DirRank[d];
                }
            }
            return false;
        }

        /// <summary>Can this piece be harmed by hostile effects (capture, stun, steal, fireball, bomb)?</summary>
        public static bool IsVulnerable(Piece target, Side attacker)
        {
            return !target.IsEmpty && target.Side != attacker
                && target.Type != PieceType.King && !target.Has(Buff.Armor);
        }

        /// <summary>Bombs destroy any piece except kings and armored pieces, regardless of owner.</summary>
        public static bool IsVulnerableToBomb(Piece target)
        {
            return !target.IsEmpty && target.Type != PieceType.King && !target.Has(Buff.Armor);
        }

        /// <summary>Direction indices this piece fires/attacks along, and the max range (1 or 7).</summary>
        static void Lines(Piece p, out int first, out int last, out int range)
        {
            range = 7;
            switch (p.Type)
            {
                case PieceType.Rook: first = 0; last = 3; break;
                case PieceType.Bishop: first = 4; last = 7; break;
                case PieceType.Queen: first = 0; last = 7; break;
                case PieceType.King: first = 0; last = 7; range = 1; break;
                default: first = 4; last = 7; range = 1; break; // pawn: handled by caller
            }
        }

        /// <summary>
        /// Squares holding a piece that <paramref name="from"/> could attack (first piece along each line,
        /// boulders block). Does not filter by owner.
        /// </summary>
        public static void AttackedPieces(GameState s, int from, List<int> result)
        {
            var p = s.Cells[from];
            int f = Sq.File(from), r = Sq.Rank(from);
            if (p.Type == PieceType.Knight)
            {
                for (int i = 0; i < 8; i++)
                {
                    int nf = f + KnightFile[i], nr = r + KnightRank[i];
                    if (!Sq.OnBoard(nf, nr)) continue;
                    int to = Sq.Make(nf, nr);
                    if (!s.Cells[to].IsEmpty) result.Add(to);
                }
                return;
            }
            if (p.Type == PieceType.Pawn)
            {
                int nr = r + (p.Side == Side.White ? 1 : -1);
                for (int df = -1; df <= 1; df += 2)
                {
                    int nf = f + df;
                    if (!Sq.OnBoard(nf, nr)) continue;
                    int to = Sq.Make(nf, nr);
                    if (!s.Cells[to].IsEmpty) result.Add(to);
                }
                return;
            }
            int first, last, range;
            Lines(p, out first, out last, out range);
            for (int d = first; d <= last; d++)
            {
                int cf = f, cr = r;
                for (int step = 0; step < range; step++)
                {
                    cf += DirFile[d];
                    cr += DirRank[d];
                    if (!Sq.OnBoard(cf, cr)) break;
                    int idx = Sq.Make(cf, cr);
                    if (s.Boulders[idx]) break;
                    if (!s.Cells[idx].IsEmpty) { result.Add(idx); break; }
                }
            }
        }

        /// <summary>
        /// Enemy pieces a Fireball fired from <paramref name="from"/> in direction <paramref name="dir"/> (0-7, see
        /// <see cref="DirFile"/>) would destroy. Any piece can fire in any of the 8 directions with unlimited range.
        /// Most pieces destroy only the first piece on the line, and only if it is a vulnerable enemy. A queen's
        /// fireball destroys every vulnerable enemy on the whole line and flies over its own pieces, kings and armored
        /// pieces. Boulders stop it either way.
        /// </summary>
        public static void FireballVictims(GameState s, int from, int dir, List<int> result)
        {
            var shooter = s.Cells[from];
            bool pierce = shooter.Type == PieceType.Queen;
            int cf = Sq.File(from) + DirFile[dir], cr = Sq.Rank(from) + DirRank[dir];
            while (Sq.OnBoard(cf, cr))
            {
                int idx = Sq.Make(cf, cr);
                if (s.Boulders[idx]) break;
                var p = s.Cells[idx];
                if (!p.IsEmpty)
                {
                    if (IsVulnerable(p, shooter.Side)) result.Add(idx);
                    if (!pierce) break;
                }
                cf += DirFile[dir];
                cr += DirRank[dir];
            }
        }

        /// <summary>Squares within Chebyshev <paramref name="radius"/> of <paramref name="center"/>, excluding the center.</summary>
        public static void AreaAround(int center, int radius, List<int> result)
        {
            int f = Sq.File(center), r = Sq.Rank(center);
            for (int df = -radius; df <= radius; df++)
            {
                for (int dr = -radius; dr <= radius; dr++)
                {
                    if (df == 0 && dr == 0) continue;
                    if (Sq.OnBoard(f + df, r + dr)) result.Add(Sq.Make(f + df, r + dr));
                }
            }
        }

        public static int BombRadius(Piece p)
        {
            if (p.Has(Buff.BigBomb)) return 2;
            if (p.Has(Buff.SmallBomb)) return 1;
            return 0;
        }
    }
}
