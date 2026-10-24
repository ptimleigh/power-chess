using System;

namespace PowerChess.Core
{
    public enum Side : byte { White = 0, Black = 1 }

    public static class SideExt
    {
        public static Side Opponent(this Side s) { return s == Side.White ? Side.Black : Side.White; }
    }

    public enum PieceType : byte { None = 0, Pawn, Knight, Bishop, Rook, Queen, King }

    /// <summary>Abilities that live on a piece and are lost when it is destroyed.</summary>
    [Flags]
    public enum Buff : byte
    {
        None = 0,
        DoubleMove = 1,
        Poison = 2,
        Armor = 4,
        SmallBomb = 8,
        BigBomb = 16,
        /// <summary>One-shot: this piece can fire a Fireball instead of moving. Lost if the piece is destroyed.</summary>
        Fireball = 32,
    }

    /// <summary>What a mystery square can contain. Perks go to the player's inventory, the rest apply to the piece.</summary>
    public enum PowerupKind : byte
    {
        None = 0,
        DoubleMove,
        Poison,
        SpikedArmor,
        Stun,
        SmallBomb,
        BigBomb,
        TransformPawn,
        TransformKnight,
        TransformBishop,
        TransformRook,
        TransformQueen,
        Fireball,
        Steal,
        Boulder,
        Reroll,
        Rewind,
    }

    public static class PowerupInfo
    {
        public static bool IsPerk(PowerupKind k)
        {
            return k == PowerupKind.Steal || k == PowerupKind.Stun || k == PowerupKind.Boulder
                || k == PowerupKind.Reroll || k == PowerupKind.Rewind;
        }

        public static bool IsTransform(PowerupKind k)
        {
            return k >= PowerupKind.TransformPawn && k <= PowerupKind.TransformQueen;
        }

        public static PieceType TransformTarget(PowerupKind k)
        {
            switch (k)
            {
                case PowerupKind.TransformPawn: return PieceType.Pawn;
                case PowerupKind.TransformKnight: return PieceType.Knight;
                case PowerupKind.TransformBishop: return PieceType.Bishop;
                case PowerupKind.TransformRook: return PieceType.Rook;
                case PowerupKind.TransformQueen: return PieceType.Queen;
                default: return PieceType.None;
            }
        }
    }

    public struct Piece
    {
        public PieceType Type;
        public Side Side;
        public Buff Buffs;
        /// <summary>Owner turns of protection left (counts down at the end of each of the owner's turns).</summary>
        public byte ArmorTurns;
        /// <summary>Owner turns this piece still cannot move.</summary>
        public byte StunTurns;

        public Piece(PieceType type, Side side)
        {
            Type = type;
            Side = side;
            Buffs = Buff.None;
            ArmorTurns = 0;
            StunTurns = 0;
        }

        public bool IsEmpty { get { return Type == PieceType.None; } }
        public bool IsStunned { get { return StunTurns > 0; } }
        public bool Has(Buff b) { return (Buffs & b) != 0; }
    }

    /// <summary>Square helpers. Squares are 0..63 with a1 = 0, h1 = 7, a8 = 56.</summary>
    public static class Sq
    {
        public static int File(int s) { return s & 7; }
        public static int Rank(int s) { return s >> 3; }
        public static int Make(int file, int rank) { return rank * 8 + file; }
        public static bool OnBoard(int file, int rank) { return file >= 0 && file < 8 && rank >= 0 && rank < 8; }
        /// <summary>Point mirror, used so powerup squares spawn symmetrically for both players.</summary>
        public static int Mirror(int s) { return 63 - s; }
        public static int Parse(string name) { return (name[1] - '1') * 8 + (name[0] - 'a'); }
        public static string Name(int s) { return "" + (char)('a' + File(s)) + (char)('1' + Rank(s)); }
    }

    public enum ActionKind : byte
    {
        Move,
        Stun,
        Fireball,
        Steal,
        PlaceBoulder,
        ClearBoulder,
        Detonate,
        KeepPickup,
        RerollPickup,
    }

    public struct GameAction : IEquatable<GameAction>
    {
        public ActionKind Kind;
        public int From;
        /// <summary>Target square. Fireball: the first piece in the chosen direction. Stun/PlaceBoulder: the square itself. Fireball From: the piece that earned it.</summary>
        public int To;
        /// <summary>Promotion piece type for pawn moves; direction index (0-7) for a Fireball.</summary>
        public int Aux;

        public GameAction(ActionKind kind, int from, int to, int aux)
        {
            Kind = kind;
            From = from;
            To = to;
            Aux = aux;
        }

        public static GameAction Move(int from, int to, PieceType promotion = PieceType.None)
        {
            return new GameAction(ActionKind.Move, from, to, (int)promotion);
        }

        public bool Equals(GameAction o)
        {
            return Kind == o.Kind && From == o.From && To == o.To && Aux == o.Aux;
        }

        public override bool Equals(object obj) { return obj is GameAction && Equals((GameAction)obj); }

        public override int GetHashCode()
        {
            return ((((int)Kind * 397) ^ From) * 397 ^ To) * 397 ^ Aux;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case ActionKind.Move:
                    return Sq.Name(From) + Sq.Name(To) + (Aux != 0 ? ((PieceType)Aux).ToString()[0].ToString().ToLowerInvariant() : "");
                case ActionKind.KeepPickup:
                case ActionKind.RerollPickup:
                    return Kind.ToString();
                default:
                    return Kind + " " + Sq.Name(From) + (To >= 0 ? "->" + Sq.Name(To) : "");
            }
        }
    }

    public enum GameResult { Ongoing, WhiteWins, BlackWins, Draw }

    public enum EventKind : byte
    {
        Moved,
        Captured,
        Promoted,
        Castled,
        StunStruck,
        Destroyed,
        Converted,
        Transformed,
        BuffGained,
        PerkGained,
        PickupFound,
        PickupRerolled,
        BoulderPlaced,
        BoulderCleared,
        FireballFired,
        Detonated,
        PoisonTriggered,
        SquareSpawned,
        DoubleMoveForfeited,
        ArmorExpired,
    }

    /// <summary>Describes what happened so the view layer can animate it. The engine itself never reads these.</summary>
    public struct GameEvent
    {
        public EventKind Kind;
        public int From;
        public int To;
        public PieceType Piece;
        public Side Side;
        public PowerupKind Powerup;
    }
}
