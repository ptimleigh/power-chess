using System;
using System.IO;

namespace ChessFighter.Core
{
    /// <summary>
    /// Compact, versioned save format for a game in progress: the current position plus the position history (so move
    /// review and Rewind survive a reload). The <see cref="GameConfig"/> is not stored; the caller supplies it.
    /// </summary>
    public static class GameStateCodec
    {
        const byte Version = 9;
        const int MaxHistory = 5000;

        public static string EncodeGame(GameState current, GameHistory history)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);
                WriteState(w, current);
                // The last three turn-start snapshots (oldest first) so Take back survives a reload.
                var snaps = current.TurnStarts;
                int have = 0;
                if (snaps != null) for (int i = 0; i < 3; i++) if (snaps[i] != null) have++;
                w.Write((byte)have);
                if (snaps != null) for (int i = 0; i < 3; i++) if (snaps[i] != null) { w.Write((byte)i); WriteState(w, snaps[i]); }
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
                    byte version = r.ReadByte();
                    if (version < 1 || version > 9) return false; // version 1 saves predate revealed squares
                    var state = ReadState(r, config, version);
                    if (version >= 8)
                    {
                        int have = r.ReadByte();
                        if (have > 3) return false;
                        if (have > 0) state.TurnStarts = new GameState[3];
                        for (int i = 0; i < have; i++)
                        {
                            int slot = r.ReadByte();
                            if (slot > 2) return false;
                            state.TurnStarts[slot] = ReadState(r, config, version);
                        }
                    }
                    int count = r.ReadInt32();
                    if (count < 0 || count > MaxHistory) return false;
                    var h = new GameHistory();
                    for (int i = 0; i < count; i++)
                    {
                        var before = ReadState(r, config, version);
                        var action = new GameAction((ActionKind)r.ReadByte(), r.ReadInt16(), r.ReadInt16(), r.ReadInt16());
                        if (action.Kind > ActionKind.AllowCard) return false;
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
                w.Write((ushort)p.Buffs);
                w.Write(p.ArmorTurns);
                w.Write(p.StunTurns);
                w.Write(p.GhostTurns);
                w.Write(p.Tier);
                w.Write(p.PoisonTurns);
                w.Write(p.Origin);
                w.Write(p.Stolen);
            }
            w.Write(Pack(s.Boulders));
            w.Write(Pack(s.Mystery));
            w.Write(Pack(s.Portal));
            w.Write((byte)s.MultiLeft);
            w.Write(s.Content);
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
            w.Write((byte)s.EffectKind);
            w.Write((short)s.EffectSquare);
            w.Write(s.Bag);
            w.Write(s.Captured);
            w.Write((byte)s.PendingCard);
            w.Write((byte)s.RespCard);
            w.Write((short)s.RespFrom);
            w.Write((short)s.RespTo);
            w.Write(s.RespAux);
            w.Write(s.NoSpawnUntilPly);
            w.Write((sbyte)s.LostSide);
            w.Write(s.LostWhy);
            w.Write(s.Mars);
            w.Write((short)s.MissileLeft);
            w.Write((short)s.MissileAnchor);
        }

        static GameState ReadState(BinaryReader r, GameConfig config, byte version)
        {
            var s = new GameState(config);
            for (int i = 0; i < 64; i++)
            {
                var type = r.ReadByte();
                var side = r.ReadByte();
                var buffs = version >= 5 ? r.ReadUInt16() : (ushort)r.ReadByte();
                if (type > (byte)PieceType.King || side > 1 || false) throw new InvalidDataException("bad piece");
                var piece = new Piece((PieceType)type, (Side)side)
                {
                    Buffs = (Buff)buffs,
                    ArmorTurns = r.ReadByte(),
                    StunTurns = r.ReadByte(),
                    GhostTurns = version >= 3 ? r.ReadByte() : (byte)0,
                    Tier = version >= 4 ? r.ReadByte() : (byte)0,
                    PoisonTurns = version >= 5 ? r.ReadByte() : (byte)0,
                    Origin = version >= 8 ? r.ReadByte() : (byte)0,
                    Stolen = version >= 9 ? r.ReadByte() : (byte)0,
                };
                if (piece.Origin > 64) throw new InvalidDataException("bad origin");
                // Saves from before poison wore off: give an existing poison a full fuse.
                if (version < 5 && piece.Has(Buff.Poison)) piece.PoisonTurns = 5;
                s.Cells[i] = piece;
            }
            Unpack(r.ReadUInt64(), s.Boulders);
            Unpack(r.ReadUInt64(), s.Mystery);
            if (version >= 3)
            {
                Unpack(r.ReadUInt64(), s.Portal);
                s.MultiLeft = r.ReadByte();
                if (s.MultiLeft > Rules.MaxComboMoves) throw new InvalidDataException("bad multi");
            }
            if (version >= 2)
            {
                var content = r.ReadBytes(64);
                if (content.Length != 64) throw new InvalidDataException("bad content");
                for (int i = 0; i < 64; i++)
                {
                    if (content[i] > (byte)PowerupKind.DrawCard) throw new InvalidDataException("bad content kind");
                    s.Content[i] = content[i];
                }
            }
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
            if (version >= 6)
            {
                var effect = r.ReadByte();
                if (effect != 0 && effect != (byte)PowerupKind.StunBolt && effect != (byte)PowerupKind.StealGrab && effect != (byte)PowerupKind.Leap) throw new InvalidDataException("bad effect");
                s.EffectKind = (PowerupKind)effect;
                s.EffectSquare = CheckSquare(r.ReadInt16());
                if (s.EffectKind != PowerupKind.None && s.EffectSquare < 0) throw new InvalidDataException("bad effect square");
            }
            if (version >= 7)
            {
                var bag = r.ReadBytes(64);
                var lost = r.ReadBytes(16);
                if (bag.Length != 64 || lost.Length != 16) throw new InvalidDataException("bad cards");
                Array.Copy(bag, s.Bag, 64);
                Array.Copy(lost, s.Captured, 16);
                var pending = r.ReadByte();
                var resp = r.ReadByte();
                if (pending > (byte)CardKind.MissileStrike || resp > (byte)CardKind.MissileStrike) throw new InvalidDataException("bad card");
                s.PendingCard = (CardKind)pending;
                s.RespCard = (CardKind)resp;
                s.RespFrom = CheckSquare(r.ReadInt16());
                s.RespTo = CheckSquare(r.ReadInt16());
                s.RespAux = r.ReadInt32();
                s.NoSpawnUntilPly = r.ReadInt32();
                var loser = r.ReadSByte();
                if (loser < -1 || loser > 1) throw new InvalidDataException("bad lost side");
                s.LostSide = loser;
            }
            if (version >= 8)
            {
                s.LostWhy = r.ReadByte();
                var mars = r.ReadBytes(2);
                if (mars.Length != 2) throw new InvalidDataException("bad mars");
                Array.Copy(mars, s.Mars, 2);
                s.MissileLeft = r.ReadInt16();
                s.MissileAnchor = CheckSquare(r.ReadInt16());
                if (s.MissileLeft < 0 || s.MissileLeft > 11) throw new InvalidDataException("bad missile");
            }
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
