using System.Collections.Generic;

namespace ChessFighter.Core
{
    public enum UnlockType { Powerup, Transforms, Teleporters, Frequency, Preset, Custom, Cards, Upgrade }

    /// <summary>One thing a level-up makes available. The UI turns it into a preview card; tests and the Profile sheet share this list.</summary>
    public sealed class UnlockItem
    {
        public UnlockType Type;
        public int Level;
        /// <summary>For <see cref="UnlockType.Powerup"/>: the powerup (the demo and name come from it).</summary>
        public PowerupKind Kind;
        /// <summary>For <see cref="UnlockType.Preset"/>: the preset id.</summary>
        public string PresetId;
        /// <summary>For <see cref="UnlockType.Cards"/>: the cards that join the deck at this level.</summary>
        public List<CardKind> CardKinds = new List<CardKind>();
        public string Name;
        public string Blurb;
    }

    /// <summary>
    /// The Chess Fighter level plan: twenty levels, each opening something new (a power, a pack of cards, a power's second tier or a
    /// rule variant). A level below zero means everything is open (tests, debug links, scenarios).
    /// </summary>
    public static class FighterUnlocks
    {
        public const int MaxLevel = 20;
        public const int TeleporterLevel = 4;
        public const int FrequencyLevel = 10;
        public const int CustomLevel = 20;

        public static int PowerLevel(PowerupKind k)
        {
            switch (k)
            {
                case PowerupKind.SmallBomb: return 2;
                case PowerupKind.Ghost: return 3;
                case PowerupKind.StunBolt: return 5;
                case PowerupKind.Combo2: return 6;
                case PowerupKind.DrawCard: return 7;
                case PowerupKind.Leap: return 8;
                case PowerupKind.StealGrab: return 11;
                case PowerupKind.BigBomb: return 13;
                case PowerupKind.Combo4: return 15;
                default: return 1; // Fireball, Armor, Poison: the starter kit
            }
        }

        public static int CardLevel(CardKind k)
        {
            switch (k)
            {
                case CardKind.PlaceBoulder:
                case CardKind.SmashBoulder:
                case CardKind.Promotion:
                    return 7;
                case CardKind.NuhUh:
                case CardKind.TakeBack:
                case CardKind.HorseSacrifice:
                case CardKind.RoleReversal:
                    return 12;
                case CardKind.SummonMars:
                case CardKind.SummonCaissa:
                case CardKind.NoMorePowers:
                    return 16;
                case CardKind.DragonStrike:
                case CardKind.SummonDeath:
                    return 18;
                default: // Hit restart, World turned upside down, Missile strike
                    return 19;
            }
        }

        /// <summary>The level at which landing on a banked power a second time upgrades it, instead of using the square up.</summary>
        public static int TierTwoLevel(PowerupKind k)
        {
            switch (k)
            {
                case PowerupKind.Fireball: return 9;
                case PowerupKind.SmallBomb:
                case PowerupKind.BigBomb:
                case PowerupKind.Leap:
                    return 13;
                default: return 1;
            }
        }

        /// <summary>Leap is the only power with a third tier (an unlimited chain).</summary>
        public static int TierThreeLevel(PowerupKind k) { return k == PowerupKind.Leap ? 16 : int.MaxValue; }
        public static bool TierThreeOpen(PowerupKind k, int level) { return level < 0 ? k == PowerupKind.Leap : level >= TierThreeLevel(k); }

        public static bool PowerOpen(PowerupKind k, int level) { return level < 0 || level >= PowerLevel(k); }
        public static bool CardOpen(CardKind k, int level) { return level < 0 || level >= CardLevel(k); }
        public static bool TierTwoOpen(PowerupKind k, int level) { return level < 0 || level >= TierTwoLevel(k); }

        /// <summary>The fighter pool restricted to the powers this level has opened.</summary>
        public static List<WeightedKind> PoolFor(int level)
        {
            var pool = new List<WeightedKind>();
            foreach (var w in GameConfig.FighterPool())
                if (PowerOpen(w.Kind, level)) pool.Add(w);
            return pool;
        }
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
            foreach (var w in GameConfig.FighterPool())
            {
                if (FighterUnlocks.PowerLevel(w.Kind) != level) continue;
                list.Add(new UnlockItem { Type = UnlockType.Powerup, Level = level, Kind = w.Kind });
            }
            var cards = new UnlockItem { Type = UnlockType.Cards, Level = level, Name = "New cards", Blurb = "These cards can now turn up from Draw Card squares." };
            for (int c = Cards.First; c <= Cards.Last; c++)
                if (FighterUnlocks.CardLevel((CardKind)c) == level) cards.CardKinds.Add((CardKind)c);
            if (cards.CardKinds.Count > 0) list.Add(cards);
            if (level == FighterUnlocks.TierTwoLevel(PowerupKind.Fireball))
                list.Add(new UnlockItem { Type = UnlockType.Upgrade, Level = level, Kind = PowerupKind.Fireball, Name = "Fireball II", Blurb = "Land on a second Fireball square: the fireball now also fires diagonally." });
            if (level == FighterUnlocks.TierTwoLevel(PowerupKind.Leap))
            {
                list.Add(new UnlockItem { Type = UnlockType.Upgrade, Level = level, Kind = PowerupKind.SmallBomb, Name = "Bomb II", Blurb = "Land on a second Bomb square: the blast grows from 3x3 to 5x5." });
                list.Add(new UnlockItem { Type = UnlockType.Upgrade, Level = level, Kind = PowerupKind.Leap, Name = "Checkers II", Blurb = "Land on a second Checkers square: the piece can chain two jumps in a row." });
            }
            if (level == FighterUnlocks.TierThreeLevel(PowerupKind.Leap))
                list.Add(new UnlockItem { Type = UnlockType.Upgrade, Level = level, Kind = PowerupKind.Leap, Name = "Checkers III", Blurb = "Land on a third Checkers square: the piece can chain jumps for as long as there are rivals to jump." });
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
