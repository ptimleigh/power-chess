using System;
using System.Collections.Generic;

namespace ChessFighter.Core
{
    /// <summary>What a finished match looked like from one player's point of view, built from the engine's event stream.</summary>
    public sealed class MatchSummary
    {
        public Side Me;
        public GameResult Result;
        public string Reason = "";
        /// <summary>0 = two players, 1 = easy, 2 = normal, 3 = hard.</summary>
        public int AiLevel;
        /// <summary>1 for a standard game, 0.5 when custom powerup rules were on (see <see cref="Presets.XpScale"/>).</summary>
        public double XpScale = 1.0;
        /// <summary>Hints the player used this game.</summary>
        public int HintsUsed;
        public int Captures;
        public int PiecesLost;
        public int PowerupsCollected;
        public int FireballKills;
        public int BestFireballShot;
        public int BestBombKills;
        public int Steals;
        public int Teleports;
        public bool CollectedGhost;
        /// <summary>Cards the player played, cards they cancelled with Nuh-uh, and which of the big ones they used.</summary>
        public int CardsPlayed, CardsCancelled;
        public bool UsedWorld, UsedTakeBack, RestoredQueen;
        public int HarpoonHooks, Upgrades;
        public bool FrozeKing;
        /// <summary>The longest run of the player's own moves with no rival move between (a combo makes these).</summary>
        public int BestRun;
        /// <summary>Most rival pieces one Dragon Strike burned, and most pieces one Shiva summon struck.</summary>
        public int BestDragonBurn, BestSmite;
        /// <summary>The player's level once this match's XP is counted (set while awarding, for the level trophies).</summary>
        public int LevelAfter;

        public bool Won { get { return Result == (Me == Side.White ? GameResult.WhiteWins : GameResult.BlackWins); } }
        public bool Lost { get { return Result == (Me == Side.White ? GameResult.BlackWins : GameResult.WhiteWins); } }
        public bool Draw { get { return Result == GameResult.Draw; } }
    }

    /// <summary>Watches the event stream of a game and counts the things progression cares about. The engine never reads it.</summary>
    public sealed class MatchTracker
    {
        readonly MatchSummary s;

        public MatchTracker(Side me, int aiLevel)
        {
            s = new MatchSummary { Me = me, AiLevel = aiLevel };
        }

        public MatchSummary Summary { get { return s; } }

        int run;

        public void Observe(IList<GameEvent> events)
        {
            Side me = s.Me;
            int shot = -1, bomb = -1, dragon = -1, smite = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Kind)
                {
                    case EventKind.Moved:
                        if (e.Side == me) { run++; if (run > s.BestRun) s.BestRun = run; } else run = 0;
                        break;
                    case EventKind.Captured:
                        if (e.Side == me) s.Captures++; else s.PiecesLost++;
                        shot = bomb = dragon = -1;
                        break;
                    case EventKind.PoisonTriggered:
                        if (e.Side == me) s.PiecesLost++; else s.Captures++;
                        break;
                    case EventKind.FireballFired:
                        shot = e.Side == me ? 0 : -1;
                        bomb = dragon = -1;
                        break;
                    case EventKind.Detonated:
                        bomb = e.Side == me ? 0 : -1;
                        shot = dragon = -1;
                        break;
                    case EventKind.DragonFire:
                        dragon = e.Side == me ? 0 : -1;
                        shot = bomb = -1;
                        break;
                    case EventKind.Destroyed:
                        if (e.Side == me) s.PiecesLost++;
                        else
                        {
                            s.Captures++;
                            if (shot >= 0) { shot++; s.FireballKills++; if (shot > s.BestFireballShot) s.BestFireballShot = shot; }
                            if (bomb >= 0) { bomb++; if (bomb > s.BestBombKills) s.BestBombKills = bomb; }
                            if (dragon >= 0) { dragon++; if (dragon > s.BestDragonBurn) s.BestDragonBurn = dragon; }
                        }
                        break;
                    case EventKind.Converted:
                        if (e.Side == me) s.Steals++;
                        break;
                    case EventKind.Teleported:
                        if (e.Side == me) s.Teleports++;
                        break;
                    case EventKind.PickupFound:
                        if (e.Side == me)
                        {
                            s.PowerupsCollected++;
                            if (e.Powerup == PowerupKind.Ghost) s.CollectedGhost = true;
                        }
                        break;
                    case EventKind.PickupRerolled:
                        if (e.Side == me && s.PowerupsCollected > 0) s.PowerupsCollected--;
                        break;
                    case EventKind.HarpoonFired:
                        if (e.Side == me) s.HarpoonHooks++;
                        break;
                    case EventKind.StunStruck:
                        if (e.Side == me && e.Powerup == PowerupKind.StunBolt && e.Piece == PieceType.Queen) s.FrozeKing = true;
                        break;
                    case EventKind.PowerUpgraded:
                        if (e.Side == me) s.Upgrades++;
                        break;
                    case EventKind.CardPlayed:
                        if (e.Side == me)
                        {
                            s.CardsPlayed++;
                            if (e.Card == CardKind.WorldUpsideDown) s.UsedWorld = true;
                            if (e.Card == CardKind.TakeBack) s.UsedTakeBack = true;
                        }
                        break;
                    case EventKind.CardCancelled:
                        if (e.Side == me) s.CardsCancelled++;
                        break;
                    case EventKind.PieceRestored:
                        if (e.Side == me && e.Piece == PieceType.Queen) s.RestoredQueen = true;
                        break;
                    case EventKind.ShivaStrike:
                        if (e.Side == me) { smite++; if (smite > s.BestSmite) s.BestSmite = smite; }
                        break;
                }
            }
        }

        public MatchSummary Finish(GameResult result, string reason)
        {
            s.Result = result;
            s.Reason = reason ?? "";
            return s;
        }
    }

    public sealed class Trophy
    {
        public readonly int Index;
        public readonly string Name;
        public readonly string Description;
        public readonly Func<MatchSummary, Profile, bool> Earned;

        public Trophy(int index, string name, string description, Func<MatchSummary, Profile, bool> earned)
        {
            Index = index;
            Name = name;
            Description = description;
            Earned = earned;
        }
    }

    /// <summary>A player's account: XP, level, lifetime stats and trophies. Stored with <see cref="ProfileCodec"/>.</summary>
    public sealed class Profile
    {
        public int Xp;
        public int Games, Wins, Losses, Draws, Mates;
        public int Captures, Collected, FireballKills, Steals, Teleports;
        /// <summary>Consecutive days with a solved daily challenge, the best run, the last solved day number and the total solved.</summary>
        public int DailyStreak, BestStreak, LastDailyDay, DailySolved;
        /// <summary>One bit per <see cref="Trophies.All"/> index.</summary>
        public uint TrophyBits;

        public int Level { get { return Progression.LevelForXp(Xp); } }
        public bool Has(Trophy t) { return (TrophyBits & (1u << t.Index)) != 0; }
    }

    public sealed class MatchReward
    {
        public int XpGained;
        /// <summary>True once a rewarded ad has doubled this match's XP.</summary>
        public bool Doubled;
        public int OldLevel, NewLevel;
        public List<PowerupKind> NewPowerups = new List<PowerupKind>();
        public bool NewTeleporters;
        public List<Trophy> NewTrophies = new List<Trophy>();
        public List<KeyValuePair<string, int>> Breakdown = new List<KeyValuePair<string, int>>();
    }

    public static class Trophies
    {
        public static readonly Trophy[] All =
        {
            new Trophy(0, "First Win", "Win a game.", (m, p) => m.Won),
            new Trophy(1, "Checkmate", "Win by checkmate.", (m, p) => m.Won && m.Reason == "Checkmate"),
            new Trophy(2, "Flawless", "Win without losing a single piece.", (m, p) => m.Won && m.PiecesLost == 0),
            new Trophy(3, "Pyro", "Burn 3 pieces with fireballs in one game.", (m, p) => m.FireballKills >= 3),
            new Trophy(4, "Demolition", "Blow up 3 or more enemy pieces with one bomb.", (m, p) => m.BestBombKills >= 3),
            new Trophy(5, "Collector", "Collect 5 powerups in one game.", (m, p) => m.PowerupsCollected >= 5),
            new Trophy(6, "Thief", "Steal an enemy piece.", (m, p) => m.Steals > 0),
            new Trophy(7, "Warp Drive", "Jump through a teleporter.", (m, p) => m.Teleports > 0),
            new Trophy(8, "Phantom", "Win a game after collecting a Ghost.", (m, p) => m.Won && m.CollectedGhost),
            new Trophy(9, "Advanced Win", "Beat the Advanced opponent or better.", (m, p) => m.Won && Ai.Rank(m.AiLevel) >= 4),
            new Trophy(10, "Regular", "Play 10 games.", (m, p) => p.Games >= 10),
            new Trophy(11, "Daily Dose", "Solve a daily challenge.", (m, p) => p.DailySolved >= 1),
            new Trophy(12, "On a Roll", "Solve the daily challenge 3 days in a row.", (m, p) => p.BestStreak >= 3),
            new Trophy(13, "Week Warrior", "Solve the daily challenge 7 days in a row.", (m, p) => p.BestStreak >= 7),
            new Trophy(14, "Devoted", "Solve the daily challenge 30 days in a row.", (m, p) => p.BestStreak >= 30),
            new Trophy(15, "Grandmaster Slayer", "Beat the Master opponent.", (m, p) => m.Won && Ai.Rank(m.AiLevel) >= 6),
            new Trophy(16, "Nuh-uh!", "Cancel a rival's card with Nuh-uh.", (m, p) => m.CardsCancelled > 0),
            new Trophy(17, "Card Shark", "Play 5 cards in one game.", (m, p) => m.CardsPlayed >= 5),
            new Trophy(18, "Caïssa's Favor", "Bring back a queen with Summon Caïssa.", (m, p) => m.RestoredQueen),
            new Trophy(19, "Upside Down", "Win after playing World Turned Upside Down.", (m, p) => m.Won && m.UsedWorld),
            new Trophy(20, "Take Two", "Win after playing Take Back.", (m, p) => m.Won && m.UsedTakeBack),
            new Trophy(21, "Hook, Line", "Hook a rival with a Harpoon.", (m, p) => m.HarpoonHooks > 0),
            new Trophy(22, "Deep Freeze", "Freeze a queen with a Stun Bolt.", (m, p) => m.FrozeKing),
            new Trophy(23, "Combo Breaker", "Make 5 moves in a row with a +4 combo.", (m, p) => m.BestRun >= 5),
            new Trophy(24, "Level II", "Upgrade a power to its second tier.", (m, p) => m.Upgrades > 0),
            new Trophy(25, "Dragon's Breath", "Burn 3 or more rival pieces with one Dragon Strike.", (m, p) => m.BestDragonBurn >= 3),
            new Trophy(26, "Smite", "Strike three pieces with one Summon Shiva.", (m, p) => m.BestSmite >= 3),
            new Trophy(27, "Incoming!", "Win with a Missile Strike.", (m, p) => m.Won && m.Reason == "Missile strike"),
            new Trophy(28, "Veteran", "Reach level 10.", (m, p) => m.LevelAfter >= 10),
            new Trophy(29, "Master of Squares", "Reach level 20.", (m, p) => m.LevelAfter >= 20),
        };
    }

    public static class Progression
    {
        public const int MaxLevel = FighterUnlocks.MaxLevel;

        /// <summary>
        /// Total XP needed to reach a level (level 1 = 0). Going from level n to n+1 costs 200 + 30n XP, so a win (around 150 to 300
        /// XP) is about one level early on: 2: 230, 3: 490, 4: 780 ... 10: 3150 ... 20: 9500.
        /// </summary>
        public static int XpForLevel(int level)
        {
            if (level <= 1) return 0;
            return 200 * (level - 1) + 15 * (level - 1) * level;
        }

        public static int LevelForXp(int xp)
        {
            int level = 1;
            while (level < MaxLevel && xp >= XpForLevel(level + 1)) level++;
            return level;
        }

        /// <summary>The level at which each power becomes available (see <see cref="FighterUnlocks"/>).</summary>
        public static int UnlockLevel(PowerupKind k) { return FighterUnlocks.PowerLevel(k); }

        public const int TeleporterLevel = FighterUnlocks.TeleporterLevel;

        public static bool Unlocked(PowerupKind k, int level) { return FighterUnlocks.PowerOpen(k, level); }

        /// <summary>The fighter pool restricted to what this level has unlocked.</summary>
        public static List<WeightedKind> PoolFor(int level) { return FighterUnlocks.PoolFor(level); }

        static int Round(double v) { return (int)Math.Round(v); }

        /// <summary>Folds a finished match into the profile and reports what changed.</summary>
        public static MatchReward Award(Profile p, MatchSummary m)
        {
            var r = new MatchReward { OldLevel = p.Level };
            double mult = Ai.XpMultiplier(m.AiLevel);
            double scale = m.XpScale * HintPolicy.XpScale(m.HintsUsed);
            int baseXp = m.Won ? 100 : m.Draw ? 40 : 25;
            var parts = r.Breakdown;
            parts.Add(new KeyValuePair<string, int>(m.Won ? "Victory" : m.Draw ? "Draw" : "Played", Round(baseXp * mult)));
            int cap = Math.Min(m.Captures, 10) * 5;
            if (cap > 0) parts.Add(new KeyValuePair<string, int>("Captures", cap));
            int pu = Math.Min(m.PowerupsCollected, 6) * 10;
            if (pu > 0) parts.Add(new KeyValuePair<string, int>("Powerups", pu));
            if (m.Won && m.Reason == "Checkmate") parts.Add(new KeyValuePair<string, int>("Checkmate", 50));
            if (scale < 1.0)
                for (int i = 0; i < parts.Count; i++)
                    parts[i] = new KeyValuePair<string, int>(parts[i].Key, Round(parts[i].Value * scale));
            int xp = 0;
            foreach (var kv in parts) xp += kv.Value;

            p.Games++;
            if (m.Won) p.Wins++; else if (m.Draw) p.Draws++; else p.Losses++;
            if (m.Won && m.Reason == "Checkmate") p.Mates++;
            p.Captures += m.Captures;
            p.Collected += m.PowerupsCollected;
            p.FireballKills += m.FireballKills;
            p.Steals += m.Steals;
            p.Teleports += m.Teleports;

            m.LevelAfter = LevelForXp(p.Xp + xp);
            foreach (var t in Trophies.All)
            {
                if (p.Has(t) || !t.Earned(m, p)) continue;
                p.TrophyBits |= 1u << t.Index;
                r.NewTrophies.Add(t);
            }
            // A trophy is worth 25 XP, but a single game pays at most 50 so a first win is not front-loaded.
            int trophyXp = Math.Min(25 * r.NewTrophies.Count, 50);
            xp += trophyXp;
            if (trophyXp > 0) parts.Add(new KeyValuePair<string, int>("Trophies", trophyXp));

            Grant(p, r, xp);
            return r;
        }

        /// <summary>Adds XP and records the level change and what it unlocked.</summary>
        public static void Grant(Profile p, MatchReward r, int xp)
        {
            p.Xp += xp;
            r.XpGained = xp;
            r.NewLevel = p.Level;
            foreach (var w in GameConfig.FighterPool())
                if (UnlockLevel(w.Kind) > r.OldLevel && UnlockLevel(w.Kind) <= r.NewLevel) r.NewPowerups.Add(w.Kind);
            r.NewTeleporters = TeleporterLevel > r.OldLevel && TeleporterLevel <= r.NewLevel;
        }
    }

    /// <summary>Compact versioned save format for a <see cref="Profile"/>.</summary>
    public static class ProfileCodec
    {
        const byte Version = 2;

        public static string Encode(Profile p)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var w = new System.IO.BinaryWriter(ms))
            {
                w.Write(Version);
                w.Write(p.Xp);
                w.Write(p.Games); w.Write(p.Wins); w.Write(p.Losses); w.Write(p.Draws); w.Write(p.Mates);
                w.Write(p.Captures); w.Write(p.Collected); w.Write(p.FireballKills); w.Write(p.Steals); w.Write(p.Teleports);
                w.Write(p.TrophyBits);
                w.Write(p.DailyStreak); w.Write(p.BestStreak); w.Write(p.LastDailyDay); w.Write(p.DailySolved);
                w.Flush();
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>Returns a fresh profile for null, empty or invalid text; never throws.</summary>
        public static Profile Decode(string text)
        {
            var p = new Profile();
            if (string.IsNullOrEmpty(text)) return p;
            try
            {
                using (var ms = new System.IO.MemoryStream(Convert.FromBase64String(text)))
                using (var r = new System.IO.BinaryReader(ms))
                {
                    byte version = r.ReadByte();
                    if (version != 1 && version != 2) return new Profile();
                    var q = new Profile
                    {
                        Xp = r.ReadInt32(), Games = r.ReadInt32(), Wins = r.ReadInt32(), Losses = r.ReadInt32(),
                        Draws = r.ReadInt32(), Mates = r.ReadInt32(), Captures = r.ReadInt32(), Collected = r.ReadInt32(),
                        FireballKills = r.ReadInt32(), Steals = r.ReadInt32(), Teleports = r.ReadInt32(), TrophyBits = r.ReadUInt32(),
                    };
                    if (version >= 2)
                    {
                        q.DailyStreak = r.ReadInt32(); q.BestStreak = r.ReadInt32(); q.LastDailyDay = r.ReadInt32(); q.DailySolved = r.ReadInt32();
                        if (q.DailyStreak < 0 || q.BestStreak < 0 || q.DailySolved < 0 || q.LastDailyDay < 0) return new Profile();
                    }
                    if (ms.Position != ms.Length) return new Profile();
                    if (q.Xp < 0 || q.Games < 0 || q.Wins < 0 || q.Losses < 0 || q.Draws < 0 || q.Mates < 0) return new Profile();
                    if (q.Xp > 10000000) return new Profile();
                    return q;
                }
            }
            catch (Exception)
            {
                return new Profile();
            }
        }
    }
}
