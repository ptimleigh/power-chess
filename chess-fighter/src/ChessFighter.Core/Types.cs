using System;

namespace ChessFighter.Core
{
    public enum Side : byte { White = 0, Black = 1 }

    public static class SideExt
    {
        public static Side Opponent(this Side s) { return s == Side.White ? Side.Black : Side.White; }
    }

    public enum PieceType : byte { None = 0, Pawn, Knight, Bishop, Rook, Queen, King }

    /// <summary>Abilities that live on a piece and are lost when it is destroyed.</summary>
    [Flags]
    public enum Buff : ushort
    {
        None = 0,
        DoubleMove = 1,
        Poison = 2,
        Armor = 4,
        SmallBomb = 8,
        BigBomb = 16,
        /// <summary>One-shot: this piece can fire a Fireball instead of moving. Lost if the piece is destroyed.</summary>
        Fireball = 32,
        /// <summary>The piece slides through pieces and boulders for a few turns (it still cannot capture its own side).</summary>
        Ghost = 64,
        /// <summary>Like Double Move, but three moves in a row.</summary>
        TripleMove = 128,
        /// <summary>Banked: fire a rope and bishop-shaped hook along a diagonal and pull the first rival piece in line next to this piece.</summary>
        Leap = 256,
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
        Ghost,
        TripleMove,
        Swap,
        /// <summary>Instant: the piece that lands on it moves again, 2 more times in a row (3 moves in all). Stacks.</summary>
        Combo2,
        /// <summary>Instant: like Combo2 but 4 more moves (5 in all).</summary>
        Combo4,
        /// <summary>Banked checkers jump: leap over an adjacent rival on a diagonal and capture it. Tier II chains two jumps, tier III any number.</summary>
        Leap,
        /// <summary>Instant: the lander immediately fires a stun bolt in any direction at the first enemy piece in line (a king can be hit). 1 turn.</summary>
        StunBolt,
        /// <summary>Instant: the lander immediately takes over a rival piece (not a king) in the 3x3 around it.</summary>
        StealGrab,
        /// <summary>Instant: draw a card into your bag (immediate cards are used straight away).</summary>
        DrawCard,
    }

    public static class PowerupInfo
    {
        /// <summary>Banked moves live on a piece until used. A piece holds one category at a time; landing on a different one replaces it.</summary>
        public static bool IsBanked(PowerupKind k)
        {
            return k == PowerupKind.Fireball || k == PowerupKind.Poison || k == PowerupKind.SmallBomb
                || k == PowerupKind.BigBomb || k == PowerupKind.Ghost || k == PowerupKind.Leap;
        }

        public static bool IsCombo(PowerupKind k) { return k == PowerupKind.Combo2 || k == PowerupKind.Combo4; }

        /// <summary>Instant powers that make the lander choose a target right after landing.</summary>
        public static bool IsTargeted(PowerupKind k) { return k == PowerupKind.StunBolt || k == PowerupKind.StealGrab; }

        public static bool IsPerk(PowerupKind k)
        {
            return k == PowerupKind.Steal || k == PowerupKind.Stun || k == PowerupKind.Boulder
                || k == PowerupKind.Reroll || k == PowerupKind.Rewind || k == PowerupKind.Swap;
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
        /// <summary>Owner turns of ghosting left.</summary>
        public byte GhostTurns;
        /// <summary>Tier of the banked move on this piece: 0 = none, 1 = I, 2 = II. Fireball I shoots like a rook, II like a queen.</summary>
        public byte Tier;
        /// <summary>Owner turns of poison left; the poison fades when it reaches 0.</summary>
        public byte PoisonTurns;
        /// <summary>The square this piece started the game on, plus one (0 = unknown). Hit restart sends pieces back to it.</summary>
        public byte Origin;
        /// <summary>1 while this piece fights for the side that did not start with it (it was stolen). If it dies, the loss counts for its first owner.</summary>
        public byte Stolen;

        public Piece(PieceType type, Side side)
        {
            Type = type;
            Side = side;
            Buffs = Buff.None;
            ArmorTurns = 0;
            StunTurns = 0;
            GhostTurns = 0;
            Tier = 0;
            PoisonTurns = 0;
            Origin = 0;
            Stolen = 0;
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
        /// <summary>Swap two of your own pieces (From, To). Costs the turn.</summary>
        Swap,
        /// <summary>A checkers jump: From jumps over the rival on the next diagonal square and lands on To (two squares away; Aux = diagonal 4-7), capturing it. Also each further jump of a chain.</summary>
        Leap,
        /// <summary>Resolve a pending Stun square: From = the lander, To = the piece hit, Aux = direction (0-7).</summary>
        StunBeam,
        /// <summary>Resolve a pending Steal square: From = the lander, To = the rival taken over.</summary>
        StealPick,
        /// <summary>Play a card from the bag (or an immediate one just drawn). Aux packs the card and a choice, see Cards.Pack. Costs the turn.</summary>
        PlayCard,
        /// <summary>The rival cancels the card you just activated with a Nuh-uh.</summary>
        UseNuhUh,
        /// <summary>The rival lets the card you just activated through.</summary>
        AllowCard,
        /// <summary>End a Leap chain early (the piece has jumped at least once and may jump again, but stays where it is).</summary>
        LeapStop,
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
        GhostFaded,
        PortalSpawned,
        Teleported,
        Swapped,
        /// <summary>A king landed on a powerup square it cannot use (Poison, Transform). The square is used up.</summary>
        PickupRejected,
        /// <summary>A teleporter was used but its twin was blocked, so the piece stayed put and both squares vanished.</summary>
        TeleportBlocked,
        /// <summary>A piece landed on a power it already holds at its top tier. The square is used up and nothing changes.</summary>
        PowerMaxed,
        /// <summary>A piece upgraded the banked move it already held (Fireball I to II, Bomb I to II).</summary>
        PowerUpgraded,
        /// <summary>A piece leapt: From is where it jumped from, To where it landed. The rival it jumped is destroyed (Destroyed event).</summary>
        Leaped,
        /// <summary>A hooked piece was dragged next to the shooter: From is where it was, To where it landed.</summary>
        PulledIn,
        /// <summary>A poisoned piece's poison wore off.</summary>
        PoisonFaded,
        /// <summary>A stun bolt was fired: From is the shooter, To the piece it hit.</summary>
        ZapFired,
        /// <summary>An instant power had no legal target, so it was wasted.</summary>
        EffectFizzled,
        /// <summary>A card was drawn from a Draw Card square.</summary>
        CardDrawn,
        /// <summary>A card was activated (before the rival's chance to cancel it).</summary>
        CardPlayed,
        /// <summary>A card was cancelled by Nuh-uh.</summary>
        CardCancelled,
        /// <summary>An immediate card had no legal use, so it went into the bag instead.</summary>
        CardKept,
        /// <summary>Summon Caissa brought a piece back.</summary>
        PieceRestored,
        /// <summary>No more powers wiped every power and powerup square.</summary>
        PowersCleared,
        /// <summary>World turned upside down: the board rotated 180 degrees and every piece changed sides.</summary>
        BoardFlipped,
        /// <summary>Hit restart: pieces went back to their starting squares.</summary>
        BoardRestarted,
        /// <summary>Take back undid a round of moves.</summary>
        TurnRewound,
        /// <summary>Dragon Strike scorched a row: From and To are its two ends.</summary>
        DragonFire,
        /// <summary>Summon Death struck a piece.</summary>
        DeathStrike,
        /// <summary>A missile was launched at a quadrant (From is its corner square).</summary>
        MissileLaunched,
        /// <summary>The missile landed.</summary>
        MissileStruck,
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
        /// <summary>The card involved in a card event.</summary>
        public CardKind Card;
    }
}
