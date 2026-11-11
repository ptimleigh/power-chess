namespace ChessFighter.Core
{
    /// <summary>
    /// Rare powers and the big cards get likelier as a game goes on, and likelier still for the side that is behind in material.
    /// The factor never passes 2, and common powers are untouched, so nothing becomes the only thing that spawns.
    /// Time: 1x until ply 30 (move 15), rising to 2x by ply 100 (move 50). Catch-up: +0.1 for each point of material behind past the first, at most +0.5.
    /// </summary>
    public static class LateRamp
    {
        public const double MaxFactor = 2.0;
        /// <summary>The ply where the time ramp starts and where it reaches its full strength (the simulator changes these to compare settings).</summary>
        public static int StartPly = 30, FullPly = 100;
        public const int Scale = 20; // weights are multiplied by this so a factor like 1.45 stays whole

        public static bool RarePower(PowerupKind k)
        {
            return k == PowerupKind.Combo4 || k == PowerupKind.StealGrab || k == PowerupKind.Leap;
        }

        public static bool RareCard(CardKind k)
        {
            return k == CardKind.MissileStrike || k == CardKind.DragonStrike || k == CardKind.SummonShiva
                || k == CardKind.WorldUpsideDown || k == CardKind.HitRestart;
        }

        public static int Material(GameState s, Side side)
        {
            int total = 0;
            for (int i = 0; i < 64; i++)
            {
                var p = s.Cells[i];
                if (p.IsEmpty || p.Side != side) continue;
                switch (p.Type)
                {
                    case PieceType.Pawn: total += 1; break;
                    case PieceType.Knight:
                    case PieceType.Bishop: total += 3; break;
                    case PieceType.Rook: total += 5; break;
                    case PieceType.Queen: total += 9; break;
                }
            }
            return total;
        }

        /// <summary>The multiplier for rare draws. <paramref name="side"/> is the drawer, or null for a square that belongs to nobody yet.</summary>
        public static double Factor(GameState s, Side? side)
        {
            if (s.Config == null || !s.Config.LateRamp) return 1.0;
            double f = 1.0 + System.Math.Max(0, System.Math.Min(1, (s.Ply - StartPly) / (double)System.Math.Max(1, FullPly - StartPly)));
            if (side is Side me)
            {
                int behind = Material(s, me.Opponent()) - Material(s, me);
                if (behind > 1) f += System.Math.Min(0.5, (behind - 1) * 0.1);
            }
            return System.Math.Min(MaxFactor, f);
        }

        /// <summary>A weight scaled by <see cref="Scale"/>, with the rare factor applied when <paramref name="rare"/>.</summary>
        public static int Weigh(int weight, bool rare, double factor)
        {
            return rare ? System.Math.Max(1, (int)System.Math.Round(weight * factor * Scale)) : weight * Scale;
        }
    }
}
