using System;
using System.Text;

namespace ChessFighter.Core
{
    public sealed class GameState
    {
        public const int CastleWK = 1, CastleWQ = 2, CastleBK = 4, CastleBQ = 8;
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public readonly Piece[] Cells = new Piece[64];
        public readonly bool[] Boulders = new bool[64];
        /// <summary>Unopened powerup squares. Contents are drawn when a piece lands on one.</summary>
        public readonly bool[] Mystery = new bool[64];
        /// <summary>The powerup shown on a revealed square (0 = a hidden mystery). Only meaningful where <see cref="Mystery"/> is set.</summary>
        public readonly byte[] Content = new byte[64];
        /// <summary>Teleporter squares. Partners are point-mirrors of each other; using one removes both.</summary>
        public readonly bool[] Portal = new bool[64];
        /// <summary>Perk counts, indexed side * 32 + (int)PowerupKind.</summary>
        public readonly byte[] Inventory = new byte[64];

        public Side ToMove;
        public int Castling;
        public int EnPassant = -1;
        public int HalfmoveClock;
        public int Ply;
        public Rng Rng;
        public GameConfig Config;

        /// <summary>A just-drawn pickup waiting for the mover to keep it or spend a Reroll on it.</summary>
        public int PendingSquare = -1;
        public PowerupKind PendingKind;

        /// <summary>The card bag: counts indexed side * 32 + (int)CardKind. Visible to both players.</summary>
        public readonly byte[] Bag = new byte[64];
        /// <summary>Pieces each side has lost, indexed side * 8 + (int)PieceType (kings excluded). Summon Caissa draws on this.</summary>
        public readonly byte[] Captured = new byte[16];
        /// <summary>An immediate card just drawn that its owner must now aim.</summary>
        public CardKind PendingCard;
        /// <summary>A card just activated, waiting for the rival's answer (Nuh-uh or allow), with the activation's fields.</summary>
        public CardKind RespCard;
        public int RespFrom, RespTo, RespAux;
        /// <summary>Powerup squares do not spawn before this ply (No more powers).</summary>
        public int NoSpawnUntilPly;

        /// <summary>The side (as an int, -1 for none) that ended its turn with its king still in check, after its only escape was cancelled by Nuh-uh. It loses.</summary>
        public int LostSide = -1;

        /// <summary>Whose decision the game is waiting for: normally the side to move, but the rival while a card awaits an answer.</summary>
        public Side Decider { get { return RespCard != CardKind.None ? ToMove.Opponent() : ToMove; } }

        public int CardCount(Side side, CardKind kind) { return Bag[(int)side * 32 + (int)kind]; }

        public void AddCard(Side side, CardKind kind, int n)
        {
            int i = (int)side * 32 + (int)kind;
            Bag[i] = (byte)Math.Max(0, Math.Min(255, Bag[i] + n));
        }

        /// <summary>An instant targeted power (Stun, Steal) the mover must now aim, and the square of the piece that landed on it.</summary>
        public PowerupKind EffectKind;
        public int EffectSquare = -1;

        /// <summary>After the first move of a Double Move piece, the only piece allowed to move.</summary>
        public int ForcedMover = -1;

        // Transient flags used while an action is being resolved.
        public int DoubleSquare = -1;
        /// <summary>Forced follow-up moves still owed by the ForcedMover (Double Move: 1, Triple Move: 2 after the first).</summary>
        public int MultiLeft;
        public bool ClockReset;
        /// <summary>Extra moves a Combo pickup just granted, and the square of the piece that gets them. Resolved in Rules.Finish.</summary>
        public int ComboGain;
        public int ComboSquare = -1;

        /// <summary>AI lookahead: no random draws and no spawns, so search never peeks at hidden information.</summary>
        public bool SearchMode;
        /// <summary>Legality probe: skip the Double Move follow-up so the check is not recursive.</summary>
        public bool Probe;

        public GameState(GameConfig config)
        {
            Config = config ?? new GameConfig();
        }

        public int Perks(Side side, PowerupKind kind) { return Inventory[(int)side * 32 + (int)kind]; }

        public void AddPerk(Side side, PowerupKind kind, int n)
        {
            int i = (int)side * 32 + (int)kind;
            Inventory[i] = (byte)Math.Max(0, Inventory[i] + n);
        }

        public int BoulderCount()
        {
            int n = 0;
            for (int i = 0; i < 64; i++) if (Boulders[i]) n++;
            return n;
        }

        public int PortalPairs()
        {
            int n = 0;
            for (int i = 0; i < 64; i++) if (Portal[i]) n++;
            return n / 2;
        }

        public int MysteryCount()
        {
            int n = 0;
            for (int i = 0; i < 64; i++) if (Mystery[i]) n++;
            return n;
        }

        public GameState Clone()
        {
            var c = new GameState(Config);
            Array.Copy(Cells, c.Cells, 64);
            Array.Copy(Boulders, c.Boulders, 64);
            Array.Copy(Mystery, c.Mystery, 64);
            Array.Copy(Content, c.Content, 64);
            Array.Copy(Portal, c.Portal, 64);
            c.MultiLeft = MultiLeft;
            Array.Copy(Inventory, c.Inventory, 64);
            c.ToMove = ToMove;
            c.Castling = Castling;
            c.EnPassant = EnPassant;
            c.HalfmoveClock = HalfmoveClock;
            c.Ply = Ply;
            c.Rng = Rng;
            c.PendingSquare = PendingSquare;
            c.PendingKind = PendingKind;
            c.EffectKind = EffectKind;
            Array.Copy(Bag, c.Bag, 64);
            Array.Copy(Captured, c.Captured, 16);
            c.PendingCard = PendingCard;
            c.RespCard = RespCard;
            c.RespFrom = RespFrom;
            c.RespTo = RespTo;
            c.RespAux = RespAux;
            c.NoSpawnUntilPly = NoSpawnUntilPly;
            c.LostSide = LostSide;
            c.EffectSquare = EffectSquare;
            c.ForcedMover = ForcedMover;
            c.DoubleSquare = DoubleSquare;
            c.ClockReset = ClockReset;
            c.ComboGain = ComboGain;
            c.ComboSquare = ComboSquare;
            c.SearchMode = SearchMode;
            c.Probe = Probe;
            return c;
        }

        public static GameState NewGame(GameConfig config, ulong seed)
        {
            var s = FromFen(StartFen, config);
            s.Rng = new Rng(seed);
            s.AddPerk(Side.White, PowerupKind.Reroll, s.Config.StartRerolls);
            s.AddPerk(Side.Black, PowerupKind.Reroll, s.Config.StartRerolls);
            for (int i = 0; i < s.Config.InitialPairs; i++) Rules.TrySpawnPair(s, null, false); // teleporters only appear once play is under way
            return s;
        }

        /// <summary>Parses FEN placement, side, castling and en passant. Halfmove/fullmove counters are ignored.</summary>
        public static GameState FromFen(string fen, GameConfig config = null)
        {
            var s = new GameState(config ?? GameConfig.Classic());
            var parts = fen.Split(' ');
            var rows = parts[0].Split('/');
            for (int i = 0; i < 8; i++)
            {
                int rank = 7 - i;
                int file = 0;
                foreach (char ch in rows[i])
                {
                    if (char.IsDigit(ch)) { file += ch - '0'; continue; }
                    var side = char.IsUpper(ch) ? Side.White : Side.Black;
                    PieceType type;
                    switch (char.ToLowerInvariant(ch))
                    {
                        case 'p': type = PieceType.Pawn; break;
                        case 'n': type = PieceType.Knight; break;
                        case 'b': type = PieceType.Bishop; break;
                        case 'r': type = PieceType.Rook; break;
                        case 'q': type = PieceType.Queen; break;
                        case 'k': type = PieceType.King; break;
                        default: throw new ArgumentException("Bad FEN piece: " + ch);
                    }
                    s.Cells[Sq.Make(file, rank)] = new Piece(type, side);
                    file++;
                }
            }
            s.ToMove = parts.Length > 1 && parts[1] == "b" ? Side.Black : Side.White;
            if (parts.Length > 2)
            {
                foreach (char ch in parts[2])
                {
                    if (ch == 'K') s.Castling |= CastleWK;
                    else if (ch == 'Q') s.Castling |= CastleWQ;
                    else if (ch == 'k') s.Castling |= CastleBK;
                    else if (ch == 'q') s.Castling |= CastleBQ;
                }
            }
            if (parts.Length > 3 && parts[3] != "-") s.EnPassant = Sq.Parse(parts[3]);
            return s;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int rank = 7; rank >= 0; rank--)
            {
                for (int file = 0; file < 8; file++)
                {
                    int sq = Sq.Make(file, rank);
                    var p = Cells[sq];
                    char c = '.';
                    if (!p.IsEmpty)
                    {
                        c = "?pnbrqk"[(int)p.Type];
                        if (p.Side == Side.White) c = char.ToUpperInvariant(c);
                    }
                    else if (Boulders[sq]) c = '#';
                    else if (Portal[sq]) c = '@';
                    else if (Mystery[sq]) c = Content[sq] != 0 ? '*' : '?';
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            sb.Append(ToMove).Append(" to move");
            return sb.ToString();
        }
    }
}
