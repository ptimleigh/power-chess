using System;
using System.Collections.Generic;
using System.Linq;
using PowerChess.Core;

namespace PowerChess.Core.Tests
{
    static class T
    {
        public static GameConfig Cfg()
        {
            return new GameConfig { SpawnInterval = 0, InitialPairs = 0, StartRerolls = 0 };
        }

        public static GameState Fen(string fen, GameConfig cfg = null)
        {
            return GameState.FromFen(fen, cfg ?? Cfg());
        }

        public static void Give(GameState s, string sq, Buff b, int armorTurns = 0)
        {
            int i = Sq.Parse(sq);
            var p = s.Cells[i];
            p.Buffs |= b;
            if (b == Buff.Armor) p.ArmorTurns = (byte)armorTurns;
            s.Cells[i] = p;
        }

        public static Piece At(GameState s, string sq) { return s.Cells[Sq.Parse(sq)]; }

        public static void Perk(GameState s, Side side, PowerupKind k, int n = 1) { s.AddPerk(side, k, n); }

        /// <summary>Finds a legal action by its ToString, e.g. "e2e4" or "Fireball a1".</summary>
        public static GameAction Find(GameState s, string text)
        {
            foreach (var a in MoveGen.LegalActions(s))
            {
                if (string.Equals(a.ToString(), text, StringComparison.OrdinalIgnoreCase)) return a;
            }
            throw new InvalidOperationException("Not legal: " + text + "\n" + s + "\nLegal: " +
                string.Join(", ", MoveGen.LegalActions(s).Select(x => x.ToString())));
        }

        public static bool IsLegal(GameState s, string text)
        {
            return MoveGen.LegalActions(s).Any(a => string.Equals(a.ToString(), text, StringComparison.OrdinalIgnoreCase));
        }

        public static List<GameEvent> Do(GameState s, string text)
        {
            var ev = new List<GameEvent>();
            Rules.Apply(s, Find(s, text), ev);
            return ev;
        }
    }
}
