using System;
using System.Collections.Generic;

namespace PowerChess.Core
{
    /// <summary>
    /// One daily puzzle: a fixed position (White to move) with set powerups, and a goal of checkmating within
    /// <see cref="MaxMoves"/> White actions. A perk use or a step of a Double/Triple Move each count as one action.
    /// </summary>
    public sealed class PuzzleDef
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Hint;
        /// <summary>1 = easy (Mon-Tue), 2 = medium (Wed-Fri), 3 = hard (weekend).</summary>
        public readonly int Tier;
        public readonly int MaxMoves;
        public readonly string Fen;
        /// <summary>White's starting perks, e.g. "stun:1,swap:1".</summary>
        public readonly string Perks;
        /// <summary>Buffs on pieces, e.g. "d4:fireball,a1:ghost".</summary>
        public readonly string Buffs;
        /// <summary>Boulder squares, e.g. "b3,c6".</summary>
        public readonly string Boulders;

        public PuzzleDef(string id, string title, string hint, int tier, int maxMoves, string fen, string perks, string buffs, string boulders)
        {
            Id = id; Title = title; Hint = hint; Tier = tier; MaxMoves = maxMoves; Fen = fen;
            Perks = perks ?? ""; Buffs = buffs ?? ""; Boulders = boulders ?? "";
        }

        /// <summary>The rules every puzzle is played under: no spawns, no rerolls, no rewinds.</summary>
        public static GameConfig Config()
        {
            var c = new GameConfig { SpawnInterval = 0, InitialPairs = 0, StartRerolls = 0, MaxPly = 80 };
            c.RewindAllowed[0] = c.RewindAllowed[1] = false;
            return c;
        }

        public IEnumerable<KeyValuePair<PowerupKind, int>> PerkList()
        {
            foreach (var part in Perks.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = part.Split(':');
                int n = f.Length > 1 ? int.Parse(f[1]) : 1;
                yield return new KeyValuePair<PowerupKind, int>((PowerupKind)Enum.Parse(typeof(PowerupKind), f[0], true), n);
            }
        }

        /// <summary>The starting state. Single source of truth for the web game, the tests and the generator.</summary>
        public GameState NewState(GameConfig cfg = null)
        {
            var s = GameState.FromFen(Fen, cfg ?? Config());
            foreach (var kv in PerkList()) s.AddPerk(Side.White, kv.Key, kv.Value);
            foreach (var part in Buffs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = part.Split(':');
                int sq = Sq.Parse(f[0]);
                var kind = (PowerupKind)Enum.Parse(typeof(PowerupKind), f[1], true);
                if (!s.Cells[sq].IsEmpty) Rules.ApplyPickup(s, s.Cells[sq].Side, sq, kind, null);
            }
            foreach (var part in Boulders.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                s.Boulders[Sq.Parse(part)] = true;
            return s;
        }

        /// <summary>The same position with every powerup removed (to check that the powerup is what makes it solvable).</summary>
        public GameState NewStateWithoutPowerups()
        {
            var s = GameState.FromFen(Fen, Config());
            foreach (var part in Boulders.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                s.Boulders[Sq.Parse(part)] = true;
            return s;
        }
    }

    /// <summary>Exhaustive AND/OR search for a forced checkmate within a number of White actions. For tests and the generator.</summary>
    public static class PuzzleSolver
    {
        /// <summary>Nodes searched so far by the current call (a safety valve for the generator).</summary>
        [ThreadStatic] public static long Nodes;
        [ThreadStatic] static long limit;
        /// <summary>Per-thread cap on searched nodes (0 or long.MaxValue = unlimited).</summary>
        public static long NodeLimit
        {
            get { return limit == 0 ? long.MaxValue : limit; }
            set { limit = value == long.MaxValue ? 0 : value; }
        }

        /// <summary>True if White (to move) can force mate within <paramref name="whiteMoves"/> actions against every defence.</summary>
        public static bool CanForceMate(GameState s, int whiteMoves)
        {
            Nodes = 0;
            return White(s, whiteMoves);
        }

        /// <summary>The first White action of a forced mate within the budget, or false if there is none.</summary>
        public static bool FindSolution(GameState s, int whiteMoves, out GameAction first)
        {
            Nodes = 0;
            first = default(GameAction);
            foreach (var a in MoveGen.LegalActions(s))
            {
                if (Works(s, a, whiteMoves)) { first = a; return true; }
            }
            return false;
        }

        /// <summary>Does this first White action lead to a forced mate within the budget?</summary>
        public static bool IsWinningFirstMove(GameState s, GameAction a, int whiteMoves)
        {
            Nodes = 0;
            return Works(s, a, whiteMoves);
        }

        static bool Works(GameState s, GameAction a, int left)
        {
            var c = s.Clone();
            Rules.Apply(c, a);
            var r = Rules.GetResult(c);
            if (r == GameResult.WhiteWins) return true;
            if (r != GameResult.Ongoing) return false;
            if (c.ToMove == Side.White) return White(c, left - 1);
            return left > 1 && Black(c, left - 1);
        }

        static bool White(GameState s, int left)
        {
            if (left <= 0) return false;
            if (++Nodes > NodeLimit) return false;
            foreach (var a in MoveGen.LegalActions(s))
                if (Works(s, a, left)) return true;
            return false;
        }

        static bool Black(GameState s, int left)
        {
            if (++Nodes > NodeLimit) return false;
            var actions = MoveGen.LegalActions(s);
            if (actions.Count == 0) return false;
            foreach (var a in actions)
            {
                var c = s.Clone();
                Rules.Apply(c, a);
                var r = Rules.GetResult(c);
                if (r == GameResult.WhiteWins) continue;
                if (r != GameResult.Ongoing) return false;
                if (!White(c, left)) return false;
            }
            return true;
        }
    }

    public static class Daily
    {
        public const int BaseXp = 60;
        public const int StreakXp = 15;
        public const int StreakCap = 10;

        /// <summary>Local calendar day as days since 1970-01-01.</summary>
        public static int DayNumber(DateTime date)
        {
            return (int)(new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
        }

        /// <summary>0 = Sunday ... 6 = Saturday (1970-01-01 was a Thursday).</summary>
        public static int Weekday(int day) { return (((day + 4) % 7) + 7) % 7; }

        /// <summary>Mon-Tue easy, Wed-Fri medium, Sat-Sun hard.</summary>
        public static int TierFor(int day)
        {
            int wd = Weekday(day);
            return wd == 1 || wd == 2 ? 1 : wd >= 3 && wd <= 5 ? 2 : 3;
        }

        static uint Mix(int day)
        {
            unchecked
            {
                uint x = (uint)day * 2654435761u;
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                return x;
            }
        }

        static List<PuzzleDef> OfTier(int tier)
        {
            var list = new List<PuzzleDef>();
            foreach (var p in PuzzleData.All) if (p.Tier == tier) list.Add(p);
            return list;
        }

        /// <summary>The puzzle for a day: deterministic, drawn from the weekday's tier, never the same as yesterday's.</summary>
        public static PuzzleDef PuzzleFor(int day)
        {
            int tier = TierFor(day);
            var list = OfTier(tier);
            if (list.Count == 0) return PuzzleData.All[(int)(Mix(day) % (uint)PuzzleData.All.Length)];
            int idx = (int)(Mix(day) % (uint)list.Count);
            if (list.Count > 1 && TierFor(day - 1) == tier && (int)(Mix(day - 1) % (uint)list.Count) == idx)
                idx = (idx + 1) % list.Count;
            return list[idx];
        }

        public static bool SolvedToday(Profile p, int today) { return p.LastDailyDay == today; }

        /// <summary>The streak to show: it lapses if yesterday was not solved.</summary>
        public static int ActiveStreak(Profile p, int today)
        {
            return p.LastDailyDay == today || p.LastDailyDay == today - 1 ? p.DailyStreak : 0;
        }

        public static int XpForStreak(int streak) { return BaseXp + StreakXp * Math.Min(Math.Max(streak, 1), StreakCap); }

        /// <summary>Records a solved daily. Returns null if today was already solved (nothing is paid twice).</summary>
        public static MatchReward AwardSolve(Profile p, int today)
        {
            if (p.LastDailyDay == today) return null;
            var r = new MatchReward { OldLevel = p.Level };
            p.DailyStreak = p.LastDailyDay == today - 1 ? p.DailyStreak + 1 : 1;
            if (p.DailyStreak > p.BestStreak) p.BestStreak = p.DailyStreak;
            p.LastDailyDay = today;
            p.DailySolved++;
            int xp = XpForStreak(p.DailyStreak);
            r.Breakdown.Add(new KeyValuePair<string, int>("Daily", BaseXp));
            r.Breakdown.Add(new KeyValuePair<string, int>("Streak x" + p.DailyStreak, xp - BaseXp));
            var none = new MatchSummary();
            foreach (var t in Trophies.All)
            {
                if (p.Has(t) || !t.Earned(none, p)) continue;
                p.TrophyBits |= 1u << t.Index;
                r.NewTrophies.Add(t);
                xp += 25;
            }
            if (r.NewTrophies.Count > 0) r.Breakdown.Add(new KeyValuePair<string, int>("Trophies", 25 * r.NewTrophies.Count));
            Progression.Grant(p, r, xp);
            return r;
        }
    }
}
