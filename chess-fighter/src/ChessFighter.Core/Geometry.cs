using System.Collections.Generic;

namespace ChessFighter.Core
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
                            // A banked Fireball threatens along its lines like a rook (I) or a queen (II): the first piece it meets is hit.
                            if (p.Has(Buff.Fireball) && d <= FireballMaxDir(p)) return true;
                        }
                        break;
                    }
                    first = false;
                    cf += DirFile[d];
                    cr += DirRank[d];
                }
            }
            return AttackedBySpecials(s, sq, by);
        }

        public static bool HasBomb(Piece p)
        {
            return p.Has(Buff.SmallBomb) || p.Has(Buff.BigBomb);
        }

        /// <summary>
        /// A bomb detonation starting at the bomb piece on <paramref name="start"/>: its blast area, plus the blast of every other
        /// bomb piece caught inside it (either side), and so on. <paramref name="zone"/> receives every square a blast reaches,
        /// each bomber's own square included; <paramref name="order"/> lists the detonating pieces in the order they go off.
        /// </summary>
        public static void BlastChain(GameState s, int start, bool[] zone, List<int> order)
        {
            System.Array.Clear(zone, 0, 64);
            order.Clear();
            var seen = new bool[64];
            var queue = new List<int>(4) { start };
            seen[start] = true;
            for (int qi = 0; qi < queue.Count; qi++)
            {
                int sq = queue[qi];
                order.Add(sq);
                int radius = BombRadius(s.Cells[sq]);
                if (radius < 1) radius = 1;
                int f = Sq.File(sq), r = Sq.Rank(sq);
                for (int df = -radius; df <= radius; df++)
                {
                    for (int dr = -radius; dr <= radius; dr++)
                    {
                        if (!Sq.OnBoard(f + df, r + dr)) continue;
                        int i = Sq.Make(f + df, r + dr);
                        zone[i] = true;
                        if (!seen[i] && HasBomb(s.Cells[i])) { seen[i] = true; queue.Add(i); }
                    }
                }
            }
        }

        /// <summary>
        /// Squares threatened by side <paramref name="by"/>'s ghost and bomb pieces, which the plain line scan above cannot see.
        /// A ghost passes through pieces and boulders, so it threatens along its whole line (sliders), a ghost knight also
        /// threatens a king's step, and a ghost king reaches two squares. A bomb piece threatens everything in its blast,
        /// chain reactions included. Stunned pieces start nothing.
        /// </summary>
        static bool AttackedBySpecials(GameState s, int sq, Side by)
        {
            bool ghost = false;
            List<int> bombers = null;
            for (int i = 0; i < 64; i++)
            {
                var g = s.Cells[i];
                if (g.IsEmpty || g.Side != by || g.StunTurns != 0) continue;
                if (g.Has(Buff.Ghost)) ghost = true;
                if (HasBomb(g)) { if (bombers == null) bombers = new List<int>(2); bombers.Add(i); }
            }
            if (!ghost && bombers == null) return false;

            if (bombers != null)
            {
                var zone = new bool[64];
                var order = new List<int>(4);
                foreach (int b in bombers)
                {
                    BlastChain(s, b, zone, order);
                    if (zone[sq]) return true;
                }
            }
            if (!ghost) return false;

            int f = Sq.File(sq), r = Sq.Rank(sq);
            for (int d = 0; d < 8; d++)
            {
                int cf = f + DirFile[d], cr = r + DirRank[d];
                int dist = 1;
                while (Sq.OnBoard(cf, cr))
                {
                    var p = s.Cells[Sq.Make(cf, cr)];
                    if (!p.IsEmpty && p.Side == by && p.Has(Buff.Ghost) && p.StunTurns == 0)
                    {
                        if (p.Type == PieceType.Queen) return true;
                        if (d < 4 && p.Type == PieceType.Rook) return true;
                        if (d >= 4 && p.Type == PieceType.Bishop) return true;
                        if (dist == 1 && p.Type == PieceType.Knight) return true;
                        if (dist == 2 && p.Type == PieceType.King) return true;
                    }
                    cf += DirFile[d];
                    cr += DirRank[d];
                    dist++;
                }
            }
            return false;
        }

        /// <summary>Would a blast from one of side <paramref name="by"/>'s bomb pieces (chains included) reach this square? Used to explain a bomb check.</summary>
        public static bool BombThreatens(GameState s, int sq, Side by)
        {
            var zone = new bool[64];
            var order = new List<int>(4);
            for (int i = 0; i < 64; i++)
            {
                var g = s.Cells[i];
                if (g.IsEmpty || g.Side != by || g.StunTurns != 0 || !HasBomb(g)) continue;
                BlastChain(s, i, zone, order);
                if (zone[sq]) return true;
            }
            return false;
        }

        /// <summary>
        /// Where a checkers-style Leap from <paramref name="from"/> along diagonal <paramref name="dir"/> (4-7) would land, or -1.
        /// The next square must hold a rival piece that can be captured (not a king, not armored) and the square beyond it must be on the
        /// board, empty, and plain (no boulder, powerup or teleporter), so nothing triggers mid-jump. A pawn never leaps onto the last rank.
        /// Whether the jump leaves your own king in check is decided by the usual legality check, not here.
        /// </summary>
        public static int LeapLanding(GameState s, int from, int dir)
        {
            if (dir < 4 || dir > 7) return -1;
            var me = s.Cells[from];
            if (me.IsEmpty) return -1;
            int f = Sq.File(from), r = Sq.Rank(from);
            int nf = f + DirFile[dir], nr = r + DirRank[dir], lf = f + 2 * DirFile[dir], lr = r + 2 * DirRank[dir];
            if (!Sq.OnBoard(lf, lr)) return -1;
            int mid = Sq.Make(nf, nr), land = Sq.Make(lf, lr);
            if (s.Boulders[mid] || !IsVulnerable(s.Cells[mid], me.Side)) return -1;
            if (!s.Cells[land].IsEmpty || s.Boulders[land] || s.Mystery[land] || s.Portal[land]) return -1;
            if (me.Type == PieceType.Pawn && (lr == 0 || lr == 7)) return -1;
            return land;
        }

        /// <summary>The square a leap from <paramref name="from"/> to <paramref name="land"/> jumps over (they are two diagonal steps apart).</summary>
        public static int LeapOver(int from, int land)
        {
            return Sq.Make((Sq.File(from) + Sq.File(land)) / 2, (Sq.Rank(from) + Sq.Rank(land)) / 2);
        }

        /// <summary>The first piece along direction <paramref name="dir"/> (0-7) from <paramref name="from"/>, or -1. Boulders stop the line.</summary>
        public static int FirstPieceInLine(GameState s, int from, int dir)
        {
            int cf = Sq.File(from) + DirFile[dir], cr = Sq.Rank(from) + DirRank[dir];
            while (Sq.OnBoard(cf, cr))
            {
                int idx = Sq.Make(cf, cr);
                if (s.Boulders[idx]) return -1;
                if (!s.Cells[idx].IsEmpty) return idx;
                cf += DirFile[dir];
                cr += DirRank[dir];
            }
            return -1;
        }

        /// <summary>The square next to <paramref name="from"/> along diagonal <paramref name="dir"/>.</summary>
        public static int NextTo(int from, int dir)
        {
            return Sq.Make(Sq.File(from) + DirFile[dir], Sq.Rank(from) + DirRank[dir]);
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

        /// <summary>Highest direction index a Fireball can fire along: Fireball I is orthogonal only (0-3), Fireball II all 8 (0-7).</summary>
        public static int FireballMaxDir(Piece p)
        {
            return p.Tier >= 2 ? 7 : 3;
        }

        /// <summary>
        /// The enemy piece a Fireball fired from <paramref name="from"/> in direction <paramref name="dir"/> (see
        /// <see cref="DirFile"/>) would destroy: the first piece on the line, and only if it is a vulnerable enemy. A friendly
        /// piece, a king, an armored piece or a boulder stops the shot without destroying anything. Directions beyond the
        /// piece's tier fire nothing.
        /// </summary>
        public static void FireballVictims(GameState s, int from, int dir, List<int> result)
        {
            var shooter = s.Cells[from];
            if (dir > FireballMaxDir(shooter)) return;
            int cf = Sq.File(from) + DirFile[dir], cr = Sq.Rank(from) + DirRank[dir];
            while (Sq.OnBoard(cf, cr))
            {
                int idx = Sq.Make(cf, cr);
                if (s.Boulders[idx]) return;
                var p = s.Cells[idx];
                if (!p.IsEmpty)
                {
                    if (IsVulnerable(p, shooter.Side)) result.Add(idx);
                    return;
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
