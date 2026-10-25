using System.Collections.Generic;

namespace PowerChess.Core
{
    public struct WeightedKind
    {
        public PowerupKind Kind;
        public int Weight;

        public WeightedKind(PowerupKind kind, int weight)
        {
            Kind = kind;
            Weight = weight;
        }
    }

    /// <summary>How powerup squares present their contents.</summary>
    public enum SquareMode
    {
        /// <summary>Every square is a "?" and its powerup is drawn when a piece lands on it.</summary>
        Mystery,
        /// <summary>The powerup is chosen when the square spawns and shown on it; an occasional square stays a mystery.</summary>
        Revealed,
    }

    /// <summary>Tunable rules. Account level can gate <see cref="Pool"/> to unlock powerups gradually.</summary>
    public sealed class GameConfig
    {
        public SquareMode Squares = SquareMode.Mystery;
        /// <summary>In Revealed mode, the chance that a spawned pair stays a hidden mystery.</summary>
        public double MysteryChance = 0.2;
        /// <summary>A new mirrored pair of mystery squares appears every this many plies. 0 disables spawning.</summary>
        public int SpawnInterval = 4;
        public int InitialPairs = 2;
        public int MaxMysterySquares = 6;
        public int StartRerolls = 1;
        /// <summary>Cap per perk type per player.</summary>
        public int MaxPerks = 3;
        public int MaxBoulders = 4;
        /// <summary>Plies after which the game is drawn.</summary>
        public int MaxPly = 400;
        /// <summary>Which sides can draw the Rewind perk (an AI opponent cannot use it, so switch it off for them).</summary>
        public bool[] RewindAllowed = { true, true };
        /// <summary>Which sides can draw Swap (the AI does not use it).</summary>
        public bool[] SwapAllowed = { true, true };
        /// <summary>Chance a spawn is a pair of teleporter squares instead of powerup squares. 0 = never.</summary>
        public double PortalChance = 0;
        public int MaxPortalPairs = 1;
        public int GhostTurns = 4;
        public List<WeightedKind> Pool = DefaultPool();

        public static List<WeightedKind> DefaultPool()
        {
            return new List<WeightedKind>
            {
                new WeightedKind(PowerupKind.DoubleMove, 10),
                new WeightedKind(PowerupKind.Poison, 8),
                new WeightedKind(PowerupKind.SpikedArmor, 8),
                new WeightedKind(PowerupKind.Stun, 8),
                new WeightedKind(PowerupKind.SmallBomb, 5),
                new WeightedKind(PowerupKind.BigBomb, 2),
                new WeightedKind(PowerupKind.TransformPawn, 2),
                new WeightedKind(PowerupKind.TransformKnight, 3),
                new WeightedKind(PowerupKind.TransformBishop, 3),
                new WeightedKind(PowerupKind.TransformRook, 3),
                new WeightedKind(PowerupKind.TransformQueen, 1),
                new WeightedKind(PowerupKind.Fireball, 8),
                new WeightedKind(PowerupKind.Steal, 6),
                new WeightedKind(PowerupKind.Boulder, 6),
                new WeightedKind(PowerupKind.Reroll, 8),
                new WeightedKind(PowerupKind.Rewind, 5),
                new WeightedKind(PowerupKind.Ghost, 6),
                new WeightedKind(PowerupKind.TripleMove, 3),
                new WeightedKind(PowerupKind.Swap, 6),
            };
        }

        /// <summary>Plain chess: no mystery squares and no starting perks. Used by perft tests.</summary>
        public static GameConfig Classic()
        {
            return new GameConfig { SpawnInterval = 0, InitialPairs = 0, StartRerolls = 0 };
        }
    }
}
