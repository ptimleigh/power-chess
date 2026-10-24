using System;
using System.Collections.Generic;

namespace PowerChess.Core
{
    /// <summary>
    /// Alpha-beta opponent. Search runs in <see cref="GameState.SearchMode"/> so it never peeks at hidden
    /// powerup draws; mystery squares are simply consumed without effect during lookahead.
    /// </summary>
    public sealed class Ai
    {
        const int Mate = 100000;
        const int Inf = 1000000;
        static readonly int[] PieceValue = { 0, 100, 320, 330, 500, 900, 0 };

        /// <summary>Search depth in actions (a perk use or a bomb counts as one).</summary>
        public int Depth = 2;

        public Ai(int depth = 2) { Depth = depth; }

        public GameAction Choose(GameState real)
        {
            var actions = MoveGen.LegalActions(real);
            if (actions.Count == 0) throw new InvalidOperationException("No legal actions");
            if (real.PendingSquare >= 0) return ChoosePickup(real, actions);

            var root = real.Clone();
            root.SearchMode = true;
            Order(root, actions);

            int alpha = -Inf;
            GameAction best = actions[0];
            foreach (var a in actions)
            {
                var child = root.Clone();
                Rules.Apply(child, a);
                int score = ScoreChild(root, child, Depth - 1, alpha, Inf);
                if (score > alpha)
                {
                    alpha = score;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>Keep the drawn powerup unless it is a downgrade Transform and a Reroll is available.</summary>
        static GameAction ChoosePickup(GameState s, List<GameAction> actions)
        {
            var piece = s.Cells[s.PendingSquare];
            bool downgrade = PowerupInfo.IsTransform(s.PendingKind)
                && PieceValue[(int)PowerupInfo.TransformTarget(s.PendingKind)] < PieceValue[(int)piece.Type];
            foreach (var a in actions)
            {
                if (downgrade && a.Kind == ActionKind.RerollPickup) return a;
                if (!downgrade && a.Kind == ActionKind.KeepPickup) return a;
            }
            return actions[0];
        }

        static int ScoreChild(GameState parent, GameState child, int depth, int alpha, int beta)
        {
            // A Double Move first step leaves the same side to move, so the sign does not flip.
            if (child.ToMove == parent.ToMove) return Search(child, depth, alpha, beta);
            return -Search(child, depth, -beta, -alpha);
        }

        static int Search(GameState s, int depth, int alpha, int beta)
        {
            if (s.Ply >= s.Config.MaxPly || s.HalfmoveClock >= 100) return 0;
            if (depth <= 0) return Evaluate(s);

            var actions = MoveGen.LegalActions(s);
            if (actions.Count == 0)
                return Geometry.IsKingAttacked(s, s.ToMove) ? -(Mate - s.Ply) : 0;

            Order(s, actions);
            int best = -Inf;
            foreach (var a in actions)
            {
                var child = s.Clone();
                Rules.Apply(child, a);
                int score = ScoreChild(s, child, depth - 1, alpha, beta);
                if (score > best) best = score;
                if (best > alpha) alpha = best;
                if (alpha >= beta) break;
            }
            return best;
        }

        static void Order(GameState s, List<GameAction> actions)
        {
            var keys = new int[actions.Count];
            for (int i = 0; i < actions.Count; i++) keys[i] = -OrderKey(s, actions[i]);
            var arr = actions.ToArray();
            Array.Sort(keys, arr);
            for (int i = 0; i < arr.Length; i++) actions[i] = arr[i];
        }

        static int OrderKey(GameState s, GameAction a)
        {
            switch (a.Kind)
            {
                case ActionKind.Move:
                {
                    var victim = s.Cells[a.To];
                    if (victim.IsEmpty) return s.Mystery[a.To] ? 50 : 0;
                    return 10 * PieceValue[(int)victim.Type] - PieceValue[(int)s.Cells[a.From].Type] / 10;
                }
                case ActionKind.Fireball: return 500;
                case ActionKind.Steal: return 450;
                case ActionKind.Stun: return 300;
                case ActionKind.Detonate: return 200;
                default: return 0;
            }
        }

        /// <summary>Static score from the side-to-move's point of view.</summary>
        public static int Evaluate(GameState s)
        {
            int score = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                var p = s.Cells[sq];
                if (p.IsEmpty) continue;
                int v = PieceValue[(int)p.Type];
                int f = Sq.File(sq), r = Sq.Rank(sq);
                int centre = 6 - (Math.Abs(2 * f - 7) + Math.Abs(2 * r - 7)) / 2;

                if (p.Type == PieceType.Knight || p.Type == PieceType.Bishop) v += 4 * centre;
                else if (p.Type == PieceType.Pawn) v += 6 * (p.Side == Side.White ? r : 7 - r) + 2 * centre;

                if (p.Has(Buff.DoubleMove)) v += 35;
                if (p.Has(Buff.Poison)) v += 30;
                if (p.Has(Buff.Armor)) v += 15 * p.ArmorTurns;
                if (p.Has(Buff.SmallBomb)) v += 50;
                if (p.Has(Buff.BigBomb)) v += 110;
                if (p.IsStunned) v -= (PieceValue[(int)p.Type] / 6) * p.StunTurns / 3 + 10;

                score += p.Side == Side.White ? v : -v;
            }

            score += PerkValue(s, Side.White) - PerkValue(s, Side.Black);
            return s.ToMove == Side.White ? score : -score;
        }

        static int PerkValue(GameState s, Side side)
        {
            return 90 * s.Perks(side, PowerupKind.Fireball)
                + 150 * s.Perks(side, PowerupKind.Steal)
                + 110 * s.Perks(side, PowerupKind.Stun)
                + 40 * s.Perks(side, PowerupKind.Boulder)
                + 25 * s.Perks(side, PowerupKind.Reroll);
        }
    }
}
