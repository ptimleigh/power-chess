using System;
using System.Collections.Generic;

namespace PowerChess.Core
{
    /// <summary>What a finished match looked like from one player's point of view, built from the engine's event stream.</summary>
    public sealed class MatchSummary
    {
        public Side Me;
        public GameResult Result;
        public string Reason = "";
        /// <summary>0 = two players, 1 = easy, 2 = normal, 3 = hard.</summary>
        public int AiLevel;
        public int Captures;
        public int PiecesLost;
        public int PowerupsCollected;
        public int FireballKills;
        public int BestFireballShot;
        public int BestBombKills;
        public int Steals;
        public int Teleports;
        public bool CollectedGhost;

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

        public void Observe(IList<GameEvent> events)
        {
            Side me = s.Me;
            int shot = -1, bomb = -1;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Kind)
                {
                    case EventKind.Captured:
                        if (e.Side == me) s.Captures++; else s.PiecesLost++;
                        shot = bomb = -1;
                        break;
                    case EventKind.PoisonTriggered:
                        if (e.Side == me) s.PiecesLost++; else s.Captures++;
                        break;
                    case EventKind.FireballFired:
                        shot = e.Side == me ? 0 : -1;
                        bomb = -1;
                        break;
                    case EventKind.Detonated:
                        bomb = e.Side == me ? 0 : -1;
                        shot = -1;
                        break;
                    case EventKind.Destroyed:
                        if (e.Side == me) s.PiecesLost++;
                        else
                        {
                            s.Captures++;
                            if (shot >= 0) { shot++; s.FireballKills++; if (shot > s.BestFireballShot) s.BestFireballShot = shot; }
                            if (bomb >= 0) { bomb++; if (bomb > s.BestBombKills) s.BestBombKills = bomb; }
                        }
                        break;
                    case EventKind.Converted:
                        if (e.Side == me) s.Steals++;
                        break;
                    case EventKind.Teleported:
                        if (e.Side == me) s.Teleports++;
                        break;
                    case EventKind.BuffGained:
                    case EventKind.PerkGained:
                    case EventKind.Transformed:
                        if (e.Side == me)
                        {
                            s.PowerupsCollected++;
                            if (e.Powerup == PowerupKind.Ghost) s.CollectedGhost = true;
                        }
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
        /// <summary>One bit per <see cref="Trophies.All"/> index.</summary>
        public uint TrophyBits;

        public int Level { get { return Progression.LevelForXp(Xp); } }
        public bool Has(Trophy t) { return (TrophyBits & (1u << t.Index)) != 0; }
    }

    public sealed class MatchReward
    {
        public int XpGained;
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
            new Trophy(3, "Pyro", "Burn 2 or more pieces with one fireball.", (m, p) => m.BestFireballShot >= 2),
            new Trophy(4, "Demolition", "Blow up 3 or more enemy pieces with one bomb.", (m, p) => m.BestBombKills >= 3),
            new Trophy(5, "Collector", "Collect 5 powerups in one game.", (m, p) => m.PowerupsCollected >= 5),
            new Trophy(6, "Thief", "Steal an enemy piece.", (m, p) => m.Steals > 0),
            new Trophy(7, "Warp Drive", "Jump through a teleporter.", (m, p) => m.Teleports > 0),
            new Trophy(8, "Phantom", "Win a game after collecting a Ghost.", (m, p) => m.Won && m.CollectedGhost),
            new Trophy(9, "Hard Won", "Beat the Hard opponent.", (m, p) => m.Won && m.AiLevel >= 3),
            new Trophy(10, "Regular", "Play 10 games.", (m, p) => p.Games >= 10),
        };
    }

    public static class Progression
    {
        public const int MaxLevel = 10;

        /// <summary>Total XP needed to reach a level (level 1 = 0).</summary>
        public static int XpForLevel(int level)
        {
            if (level <= 1) return 0;
            return 50 * (level - 1) * level; // 2: 100, 3: 300, 4: 600, 5: 1000 ...
        }

        public static int LevelForXp(int xp)
        {
            int level = 1;
            while (level < MaxLevel && xp >= XpForLevel(level + 1)) level++;
            return level;
        }

        /// <summary>The level at which each powerup becomes available (a staged unlock keeps the first games simple).</summary>
        public static int UnlockLevel(PowerupKind k)
        {
            switch (k)
            {
                case PowerupKind.DoubleMove:
                case PowerupKind.Poison:
                case PowerupKind.SpikedArmor:
                case PowerupKind.Stun:
                case PowerupKind.Boulder:
                case PowerupKind.Reroll:
                    return 1;
                case PowerupKind.Fireball:
                case PowerupKind.Steal:
                    return 2;
                case PowerupKind.SmallBomb:
                case PowerupKind.TransformPawn:
                case PowerupKind.TransformKnight:
                case PowerupKind.TransformBishop:
                case PowerupKind.TransformRook:
                    return 3;
                case PowerupKind.BigBomb:
                case PowerupKind.TransformQueen:
                case PowerupKind.Ghost:
                case PowerupKind.Swap:
                    return 4;
                default: // Triple Move, Rewind
                    return 5;
            }
        }

        public const int TeleporterLevel = 5;

        public static bool Unlocked(PowerupKind k, int level) { return UnlockLevel(k) <= level; }

        /// <summary>The default pool restricted to what this level has unlocked.</summary>
        public static List<WeightedKind> PoolFor(int level)
        {
            var pool = new List<WeightedKind>();
            foreach (var w in GameConfig.DefaultPool())
                if (Unlocked(w.Kind, level)) pool.Add(w);
            return pool;
        }

        static int Round(double v) { return (int)Math.Round(v); }

        /// <summary>Folds a finished match into the profile and reports what changed.</summary>
        public static MatchReward Award(Profile p, MatchSummary m)
        {
            var r = new MatchReward { OldLevel = p.Level };
            double mult = m.AiLevel == 0 ? 0.5 : m.AiLevel == 1 ? 0.7 : m.AiLevel == 3 ? 1.4 : 1.0;
            int baseXp = m.Won ? 100 : m.Draw ? 40 : 25;
            var parts = r.Breakdown;
            parts.Add(new KeyValuePair<string, int>(m.Won ? "Victory" : m.Draw ? "Draw" : "Played", Round(baseXp * mult)));
            int cap = Math.Min(m.Captures, 10) * 5;
            if (cap > 0) parts.Add(new KeyValuePair<string, int>("Captures", cap));
            int pu = Math.Min(m.PowerupsCollected, 6) * 10;
            if (pu > 0) parts.Add(new KeyValuePair<string, int>("Powerups", pu));
            if (m.Won && m.Reason == "Checkmate") parts.Add(new KeyValuePair<string, int>("Checkmate", 50));
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

            foreach (var t in Trophies.All)
            {
                if (p.Has(t) || !t.Earned(m, p)) continue;
                p.TrophyBits |= 1u << t.Index;
                r.NewTrophies.Add(t);
                xp += 25;
            }
            if (r.NewTrophies.Count > 0) parts.Add(new KeyValuePair<string, int>("Trophies", 25 * r.NewTrophies.Count));

            p.Xp += xp;
            r.XpGained = xp;
            r.NewLevel = p.Level;
            foreach (var w in GameConfig.DefaultPool())
                if (UnlockLevel(w.Kind) > r.OldLevel && UnlockLevel(w.Kind) <= r.NewLevel) r.NewPowerups.Add(w.Kind);
            r.NewTeleporters = TeleporterLevel > r.OldLevel && TeleporterLevel <= r.NewLevel;
            return r;
        }
    }

    /// <summary>Compact versioned save format for a <see cref="Profile"/>.</summary>
    public static class ProfileCodec
    {
        const byte Version = 1;

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
                    if (r.ReadByte() != Version) return new Profile();
                    var q = new Profile
                    {
                        Xp = r.ReadInt32(), Games = r.ReadInt32(), Wins = r.ReadInt32(), Losses = r.ReadInt32(),
                        Draws = r.ReadInt32(), Mates = r.ReadInt32(), Captures = r.ReadInt32(), Collected = r.ReadInt32(),
                        FireballKills = r.ReadInt32(), Steals = r.ReadInt32(), Teleports = r.ReadInt32(), TrophyBits = r.ReadUInt32(),
                    };
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
