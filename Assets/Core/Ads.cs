using System;

namespace PowerChess.Core
{
    /// <summary>
    /// When an interstitial may be shown. Pure rules so every client (web shell, Android, later Unity) behaves the same:
    /// only between games, never in the first games of an install, never for a daily puzzle, and never too often.
    /// </summary>
    public sealed class AdPolicy
    {
        public const int FreeGames = 2;
        public const int EveryGames = 3;
        public const long CooldownMs = 4 * 60 * 1000;

        long lastShownMs = long.MinValue / 2;
        int lastShownGames;

        /// <param name="lifetimeGames">Games this install has finished (the profile's game count).</param>
        /// <param name="nowMs">Any monotonic clock in milliseconds.</param>
        /// <param name="puzzle">The next thing to play is a daily puzzle.</param>
        public bool ShouldShowInterstitial(int lifetimeGames, long nowMs, bool puzzle)
        {
            if (puzzle) return false;
            if (lifetimeGames < FreeGames) return false;
            if (lifetimeGames - lastShownGames < EveryGames) return false;
            return nowMs - lastShownMs >= CooldownMs;
        }

        public void RecordShown(int lifetimeGames, long nowMs)
        {
            lastShownGames = lifetimeGames;
            lastShownMs = nowMs;
        }
    }

    public static class RewardedXp
    {
        /// <summary>
        /// Doubles the XP of a finished match after a rewarded ad. Works once per reward; returns false if it was already
        /// applied. Updates the level and the unlock lists the results card reads.
        /// </summary>
        public static bool Double(Profile p, MatchReward r)
        {
            if (r.Doubled) return false;
            int extra = r.XpGained;
            if (extra <= 0) return false;
            r.Doubled = true;
            p.Xp += extra;
            r.XpGained += extra;
            r.Breakdown.Add(new System.Collections.Generic.KeyValuePair<string, int>("Ad bonus", extra));
            r.NewLevel = p.Level;
            foreach (var w in GameConfig.DefaultPool())
            {
                var k = w.Kind;
                if (Progression.UnlockLevel(k) > r.OldLevel && Progression.UnlockLevel(k) <= r.NewLevel && !r.NewPowerups.Contains(k)) r.NewPowerups.Add(k);
            }
            r.NewTeleporters = Progression.TeleporterLevel > r.OldLevel && Progression.TeleporterLevel <= r.NewLevel;
            return true;
        }
    }
}
