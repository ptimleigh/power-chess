using System.Collections.Generic;

namespace PowerChess.Core
{
    public enum UnlockType { Powerup, Transforms, Teleporters, Frequency, Preset, Custom }

    /// <summary>One thing a level-up makes available. The UI turns it into a preview card; tests and the Profile sheet share this list.</summary>
    public sealed class UnlockItem
    {
        public UnlockType Type;
        public int Level;
        /// <summary>For <see cref="UnlockType.Powerup"/>: the powerup (the demo and name come from it).</summary>
        public PowerupKind Kind;
        /// <summary>For <see cref="UnlockType.Preset"/>: the preset id.</summary>
        public string PresetId;
        public string Name;
        public string Blurb;
    }

    public static class Unlocks
    {
        static bool IsBasicTransform(PowerupKind k)
        {
            return k == PowerupKind.TransformPawn || k == PowerupKind.TransformKnight
                || k == PowerupKind.TransformBishop || k == PowerupKind.TransformRook;
        }

        /// <summary>Everything unlocked at exactly this level, in a stable order: powerups, teleporters, frequency, presets, builder.</summary>
        public static List<UnlockItem> AtLevel(int level)
        {
            var list = new List<UnlockItem>();
            bool transforms = false;
            foreach (var w in GameConfig.DefaultPool())
            {
                var k = w.Kind;
                if (Progression.UnlockLevel(k) != level) continue;
                if (IsBasicTransform(k))
                {
                    if (transforms) continue;
                    transforms = true;
                    list.Add(new UnlockItem
                    {
                        Type = UnlockType.Transforms, Level = level, Kind = PowerupKind.TransformQueen,
                        Name = "Transform squares",
                        Blurb = "A piece that opens one changes into another piece on the spot. Never a king.",
                    });
                    continue;
                }
                list.Add(new UnlockItem { Type = UnlockType.Powerup, Level = level, Kind = k });
            }
            if (level == Progression.TeleporterLevel)
                list.Add(new UnlockItem { Type = UnlockType.Teleporters, Level = level, Name = "Teleporters", Blurb = "Land on one teleporter square to jump to its twin on the other side." });
            if (level == Presets.FrequencyLevel)
                list.Add(new UnlockItem { Type = UnlockType.Frequency, Level = level, Name = "Square frequency", Blurb = "Choose Rare, Normal, Frequent or Chaos in the New game sheet." });
            foreach (var p in Presets.All)
                if (p.UnlockLevel == level)
                    list.Add(new UnlockItem { Type = UnlockType.Preset, Level = level, PresetId = p.Id, Name = p.Name, Blurb = p.Blurb });
            if (level == Presets.CustomLevel)
                list.Add(new UnlockItem { Type = UnlockType.Custom, Level = level, Name = "Custom rules", Blurb = "Switch every powerup on or off and set how often it appears." });
            return list;
        }

        /// <summary>Unlocks gained by going from <paramref name="oldLevel"/> up to <paramref name="newLevel"/> (exclusive of the old one).</summary>
        public static List<UnlockItem> Between(int oldLevel, int newLevel)
        {
            var list = new List<UnlockItem>();
            for (int l = oldLevel + 1; l <= newLevel && l <= Progression.MaxLevel; l++) list.AddRange(AtLevel(l));
            return list;
        }

        /// <summary>The next level above <paramref name="level"/> that unlocks anything, or 0 at the top.</summary>
        public static int NextUnlockLevel(int level)
        {
            for (int l = level + 1; l <= Progression.MaxLevel; l++)
                if (AtLevel(l).Count > 0) return l;
            return 0;
        }
    }
}
