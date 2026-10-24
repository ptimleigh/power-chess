using System;
using System.IO;

namespace PowerChess.Core
{
    /// <summary>
    /// Compact, versioned save format for a game in progress: the current position plus the position history (so move
    /// review and Rewind survive a reload). The <see cref="GameConfig"/> is not stored; the caller supplies it.
    /// </summary>
    public static class GameStateCodec
    {
        const byte Version = 1;
        const int MaxHistory = 5000;

        public static string EncodeGame(GameState current, GameHistory history)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);
                WriteState(w, current);
                w.Write(history.Count);
                for (int i = 0; i < history.Count; i++)
                {
                    var e = history[i];
                    WriteState(w, e.Before);
                    w.Write((byte)e.Action.Kind);
                    w.Write((short)e.Action.From);
                    w.Write((short)e.Action.To);
                    w.Write((short)e.Action.Aux);
                }
                w.Flush();
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>Returns false (never throws) for anything that is not a valid save of this version.</summary>
        public static bool TryDecodeGame(string text, GameConfig config, out GameState current, out GameHistory history)
        {
            current = null;
            history = null;
            try
            {
                var bytes = Convert.FromBase64String(text);
                using (var ms = new MemoryStream(bytes))
                using (var r = new BinaryReader(ms))
                {
                    if (r.ReadByte() != Version) return false;
                    var state = ReadState(r, config);
                    int count = r.ReadInt32();
                    if (count < 0 || count > MaxHistory) return false;
                    var h = new GameHistory();
                    for (int i = 0; i < count; i++)
                    {
                        var before = ReadState(r, config);
                        var action = new GameAction((ActionKind)r.ReadByte(), r.ReadInt16(), r.ReadInt16(), r.ReadInt16());
                        if (action.Kind > ActionKind.RerollPickup) return false;
                        h.Record(before, action);
                    }
                    if (ms.Position != ms.Length) return false;
                    current = state;
                    history = h;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void WriteState(BinaryWriter w, GameState s)
        {
            for (int i = 0; i < 64; i++)
            {
                var p = s.Cells[i];
                w.Write((byte)p.Type);
                w.Write((byte)p.Side);
                w.Write((byte)p.Buffs);
                w.Write(p.ArmorTurns);
                w.Write(p.StunTurns);
            }
            w.Write(Pack(s.Boulders));
            w.Write(Pack(s.Mystery));
            w.Write(s.Inventory);
            w.Write((byte)s.ToMove);
            w.Write((byte)s.Castling);
            w.Write((short)s.EnPassant);
            w.Write(s.HalfmoveClock);
            w.Write(s.Ply);
            w.Write(s.Rng.State);
            w.Write((short)s.PendingSquare);
            w.Write((byte)s.PendingKind);
            w.Write((short)s.ForcedMover);
            w.Write((short)s.DoubleSquare);
            w.Write(s.ClockReset);
        }

        static GameState ReadState(BinaryReader r, GameConfig config)
        {
            var s = new GameState(config);
            for (int i = 0; i < 64; i++)
            {
                var type = r.ReadByte();
                var side = r.ReadByte();
                var buffs = r.ReadByte();
                if (type > (byte)PieceType.King || side > 1 || buffs > 63) throw new InvalidDataException("bad piece");
                s.Cells[i] = new Piece((PieceType)type, (Side)side)
                {
                    Buffs = (Buff)buffs,
                    ArmorTurns = r.ReadByte(),
                    StunTurns = r.ReadByte(),
                };
            }
            Unpack(r.ReadUInt64(), s.Boulders);
            Unpack(r.ReadUInt64(), s.Mystery);
            var inv = r.ReadBytes(64);
            if (inv.Length != 64) throw new InvalidDataException("bad inventory");
            Array.Copy(inv, s.Inventory, 64);
            var toMove = r.ReadByte();
            if (toMove > 1) throw new InvalidDataException("bad side");
            s.ToMove = (Side)toMove;
            s.Castling = r.ReadByte() & 15;
            s.EnPassant = CheckSquare(r.ReadInt16());
            s.HalfmoveClock = r.ReadInt32();
            s.Ply = r.ReadInt32();
            s.Rng = new Rng { State = r.ReadUInt64() };
            s.PendingSquare = CheckSquare(r.ReadInt16());
            s.PendingKind = (PowerupKind)r.ReadByte();
            s.ForcedMover = CheckSquare(r.ReadInt16());
            s.DoubleSquare = CheckSquare(r.ReadInt16());
            s.ClockReset = r.ReadBoolean();
            return s;
        }

        static int CheckSquare(short v)
        {
            if (v < -1 || v > 63) throw new InvalidDataException("bad square");
            return v;
        }

        static ulong Pack(bool[] flags)
        {
            ulong v = 0;
            for (int i = 0; i < 64; i++) if (flags[i]) v |= 1UL << i;
            return v;
        }

        static void Unpack(ulong v, bool[] flags)
        {
            for (int i = 0; i < 64; i++) flags[i] = (v & (1UL << i)) != 0;
        }
    }
}
