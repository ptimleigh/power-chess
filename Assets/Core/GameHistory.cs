using System;
using System.Collections.Generic;

namespace PowerChess.Core
{
    /// <summary>
    /// Position history. Powers the read-only move review and the Rewind perk (which takes back your last turn).
    /// Entry i holds the position <em>before</em> action i was applied.
    /// </summary>
    public sealed class GameHistory
    {
        public struct Entry
        {
            public GameState Before;
            public GameAction Action;
            public Side Mover;
        }

        readonly List<Entry> entries = new List<Entry>();

        public int Count { get { return entries.Count; } }

        public Entry this[int index] { get { return entries[index]; } }

        public void Clear() { entries.Clear(); }

        /// <summary>Remember the position about to have <paramref name="action"/> applied to it.</summary>
        public void Record(GameState before, GameAction action)
        {
            entries.Add(new Entry { Before = before.Clone(), Action = action, Mover = before.ToMove });
        }

        public void TruncateTo(int count)
        {
            if (count < entries.Count) entries.RemoveRange(count, entries.Count - count);
        }

        /// <summary>
        /// Index of the first action of <paramref name="side"/>'s most recent turn, or -1. A turn can span several
        /// actions (pickup decision, Double Move), but only its first action starts from a clean position.
        /// </summary>
        public int LastTurnStart(Side side)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.Mover == side && e.Before.PendingSquare < 0 && e.Before.ForcedMover < 0) return i;
            }
            return -1;
        }

        public bool CanRewind(GameState current, Side side)
        {
            return current.ToMove == side
                && current.PendingSquare < 0 && current.ForcedMover < 0
                && current.Perks(side, PowerupKind.Rewind) > 0
                && LastTurnStart(side) >= 0;
        }

        /// <summary>
        /// Takes back the side's previous turn (and whatever the opponent did since). Spends one Rewind. Returns the
        /// restored position; history from <paramref name="index"/> on is discarded. Anything gained since that turn,
        /// including perks, is undone. The random stream keeps going, so re-opened mystery squares can draw differently.
        /// </summary>
        public GameState Rewind(GameState current, Side side, out int index)
        {
            if (!CanRewind(current, side)) throw new InvalidOperationException("Cannot rewind");
            index = LastTurnStart(side);
            var restored = entries[index].Before.Clone();
            restored.Rng = current.Rng;
            int keep = Math.Max(0, current.Perks(side, PowerupKind.Rewind) - 1);
            restored.AddPerk(side, PowerupKind.Rewind, keep - restored.Perks(side, PowerupKind.Rewind));
            TruncateTo(index);
            return restored;
        }
    }
}
