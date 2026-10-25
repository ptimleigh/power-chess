using System;
using System.Collections.Generic;

namespace PowerChess.Core
{
    /// <summary>How often powerup squares appear. Unlocked at <see cref="Presets.FrequencyLevel"/>.</summary>
    public enum Frequency { Rare = 0, Normal = 1, Frequent = 2, Chaos = 3 }

    /// <summary>A bespoke powerup experience a player unlocks by levelling up, e.g. "Oops! All Big Bombs".</summary>
    public sealed class RulePreset
    {
        public string Id;
        public string Name;
        public string Blurb;
        public int UnlockLevel;
        /// <summary>The fixed pool, or null to use the default pool.</summary>
        public Func<List<WeightedKind>> Pool;
        /// <summary>Frequency this preset forces, or null to follow the player's choice.</summary>
        public Frequency? Freq;
        /// <summary>Teleporter chance per spawn; negative keeps the game's setting.</summary>
        public double Portal = -1;
    }

    /// <summary>Frequency settings, the preset list and the custom builder. Pure data, so every client gets the same rules.</summary>
    public static class Presets
    {
        public const string Classic = "classic";
        public const string Custom = "custom";
        public const int FrequencyLevel = 5;
        public const int CustomLevel = 10;

        static List<WeightedKind> Only(params PowerupKind[] kinds)
        {
            var l = new List<WeightedKind>();
            foreach (var k in kinds) l.Add(new WeightedKind(k, 1));
            return l;
        }

        public static readonly RulePreset[] All =
        {
            new RulePreset { Id = "chaos", Name = "Chaos Mode", Blurb = "A new square every turn. Everything, everywhere.", UnlockLevel = 5, Freq = Frequency.Chaos },
            new RulePreset { Id = "calm", Name = "Calm Mode", Blurb = "Few squares. Every pickup matters.", UnlockLevel = 5, Freq = Frequency.Rare },
            new RulePreset { Id = "bigbombs", Name = "Oops! All Big Bombs", Blurb = "Every square is a Big Bomb. Tread carefully.", UnlockLevel = 6, Pool = () => Only(PowerupKind.BigBomb) },
            new RulePreset { Id = "ghosttown", Name = "Ghost Town", Blurb = "Ghosts and teleporters only.", UnlockLevel = 7, Pool = () => Only(PowerupKind.Ghost), Portal = 0.45 },
            new RulePreset { Id = "fireworks", Name = "Fireworks", Blurb = "Fireball, Steal and Stun. Pure attack.", UnlockLevel = 8, Pool = () => Only(PowerupKind.Fireball, PowerupKind.Steal, PowerupKind.Stun) },
            new RulePreset { Id = "royal", Name = "Royal Court", Blurb = "Armor, Ghost, Triple Move and bombs: gifts a king can use.", UnlockLevel = 9,
                Pool = () => Only(PowerupKind.SpikedArmor, PowerupKind.Ghost, PowerupKind.TripleMove, PowerupKind.SmallBomb, PowerupKind.BigBomb) },
        };

        public static RulePreset Find(string id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return null;
        }

        public static string FrequencyName(Frequency f)
        {
            switch (f)
            {
                case Frequency.Rare: return "Rare";
                case Frequency.Frequent: return "Frequent";
                case Frequency.Chaos: return "Chaos";
                default: return "Normal";
            }
        }

        public static void ApplyFrequency(GameConfig cfg, Frequency f)
        {
            switch (f)
            {
                case Frequency.Rare: cfg.SpawnInterval = 5; cfg.MaxMysterySquares = 5; break;
                case Frequency.Frequent: cfg.SpawnInterval = 2; cfg.MaxMysterySquares = 10; break;
                case Frequency.Chaos: cfg.SpawnInterval = 1; cfg.MaxMysterySquares = 12; break;
                default: cfg.SpawnInterval = 3; cfg.MaxMysterySquares = 8; break;
            }
        }

        /// <summary>The powerups the custom builder can switch, in a fixed order that the saved string depends on.</summary>
        public static List<PowerupKind> CustomKinds()
        {
            var l = new List<PowerupKind>();
            foreach (var w in GameConfig.DefaultPool()) l.Add(w.Kind);
            return l;
        }

        /// <summary>One digit per custom kind: 0 off, 1 low, 2 normal, 3 high. Missing digits mean normal.</summary>
        public static string DefaultCustom()
        {
            return new string('2', CustomKinds().Count);
        }

        public static int CustomLevelOf(string custom, int index)
        {
            if (custom == null || index >= custom.Length) return 2;
            char c = custom[index];
            return c >= '0' && c <= '3' ? c - '0' : 2;
        }

        public static string SetCustom(string custom, int index, int value)
        {
            var chars = (custom ?? "").PadRight(CustomKinds().Count, '2').ToCharArray();
            chars[index] = (char)('0' + Math.Max(0, Math.Min(3, value)));
            return new string(chars);
        }

        public static List<WeightedKind> CustomPool(string custom)
        {
            var pool = new List<WeightedKind>();
            var defaults = GameConfig.DefaultPool();
            for (int i = 0; i < defaults.Count; i++)
            {
                int v = CustomLevelOf(custom, i);
                if (v == 0) continue;
                int w = v == 1 ? Math.Max(1, defaults[i].Weight / 3) : v == 2 ? defaults[i].Weight : defaults[i].Weight * 3;
                pool.Add(new WeightedKind(defaults[i].Kind, w));
            }
            return pool;
        }

        /// <summary>
        /// True if the player at this level may use this choice. <paramref name="level"/> below zero means everything is open
        /// (scenarios and tests).
        /// </summary>
        public static bool Available(string id, int level)
        {
            if (level < 0 || string.IsNullOrEmpty(id) || id == Classic) return true;
            if (id == Custom) return level >= CustomLevel;
            var p = Find(id);
            return p != null && level >= p.UnlockLevel;
        }

        public static bool FrequencyAvailable(int level) { return level < 0 || level >= FrequencyLevel; }

        /// <summary>
        /// Rewrites the config for a preset, frequency and custom pool. Unknown ids and locked choices fall back to the plain
        /// game. A preset's pool ignores the player's level-based unlocks, because the player chose it. An empty custom pool
        /// falls back to the default pool so a game always has something to draw.
        /// </summary>
        public static void Apply(GameConfig cfg, string id, Frequency freq, string custom, int level)
        {
            if (!Available(id, level)) id = Classic;
            if (!FrequencyAvailable(level)) freq = Frequency.Normal;
            if (string.IsNullOrEmpty(id)) id = Classic;

            var preset = Find(id);
            if (preset != null && preset.Freq.HasValue) freq = preset.Freq.Value;
            ApplyFrequency(cfg, freq);

            if (preset != null)
            {
                if (preset.Pool != null) cfg.Pool = preset.Pool();
                if (preset.Portal >= 0) cfg.PortalChance = preset.Portal;
            }
            else if (id == Custom)
            {
                var pool = CustomPool(custom);
                cfg.Pool = pool.Count > 0 ? pool : GameConfig.DefaultPool();
            }
        }

        /// <summary>Whether this setup counts as a non-standard game (and pays reduced XP).</summary>
        public static bool IsStandard(string id, Frequency freq, int level)
        {
            bool presetOn = Available(id, level) && !string.IsNullOrEmpty(id) && id != Classic;
            bool freqOn = FrequencyAvailable(level) && freq != Frequency.Normal;
            return !presetOn && !freqOn;
        }

        /// <summary>Non-standard games pay half XP, so a strong preset cannot be farmed for unlocks.</summary>
        public static double XpScale(string id, Frequency freq, int level)
        {
            return IsStandard(id, freq, level) ? 1.0 : 0.5;
        }

        public static string Label(string id, Frequency freq, int level)
        {
            if (Available(id, level) && id == Custom) return "Custom rules";
            var p = Available(id, level) ? Find(id) : null;
            if (p != null) return p.Name;
            if (FrequencyAvailable(level) && freq != Frequency.Normal) return FrequencyName(freq) + " squares";
            return "";
        }
    }
}
