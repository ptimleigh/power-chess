using System;
using System.Collections.Generic;

namespace PowerChess.Core
{
    /// <summary>Picks which part of a finished game a replay clip covers. A "ply" here is one move or power use; a pickup decision
    /// (keep or reroll) belongs to the move that found the powerup, so it does not count as its own ply.</summary>
    public static class ReplayPlan
    {
        public const int MaxPlies = 10;

        static bool IsPickupDecision(GameAction a)
        {
            return a.Kind == ActionKind.KeepPickup || a.Kind == ActionKind.RerollPickup;
        }

        /// <summary>History index where each ply starts, oldest first.</summary>
        public static List<int> PlyStarts(GameHistory history)
        {
            var starts = new List<int>();
            for (int i = 0; i < history.Count; i++)
                if (!IsPickupDecision(history[i].Action) || starts.Count == 0) starts.Add(i);
            return starts;
        }

        /// <summary>How many plies a clip can cover: everything played so far, capped at <see cref="MaxPlies"/>.</summary>
        public static int Available(GameHistory history)
        {
            return Math.Min(MaxPlies, PlyStarts(history).Count);
        }

        /// <summary>Clamps a requested length to 1..Available.</summary>
        public static int Clamp(GameHistory history, int plies)
        {
            return Math.Max(1, Math.Min(plies, Math.Max(1, Available(history))));
        }
    }
}
