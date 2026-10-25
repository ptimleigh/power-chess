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

        /// <summary>Search depth in actions (a perk use or a bomb counts as one). With a time budget it is the deepest iteration tried.</summary>
        public int Depth = 2;
        /// <summary>Extra plies of captures-only search after the main depth, so pieces are not left hanging at the horizon.</summary>
        public int QuiescenceDepth;
        /// <summary>Look one more ply when the side to move is in check at the horizon, so mates and forced replies are seen.</summary>
        public bool CheckExtension;
        /// <summary>Use the fuller evaluation (king safety, pawn structure, rook files, bishop pair).</summary>
        public bool FullEval;
        /// <summary>Iterative deepening stops after this many milliseconds (0 = search exactly <see cref="Depth"/>).</summary>
        public int TimeMs;
        /// <summary>Chance of playing a random legal action instead (beginner levels).</summary>
        public double RandomChance;

        readonly Random rng = new Random();
        System.Diagnostics.Stopwatch clock;
        long nodes;
        bool canAbort, aborted;

        public Ai(int depth = 2) { Depth = depth; }

        /// <summary>The five opponent levels: 1 Easy, 2 Normal, 3 Hard, 4 Expert, 5 Master.</summary>
        public static Ai ForLevel(int level)
        {
            switch (level)
            {
                case 1: return new Ai(1) { RandomChance = 0.25 };
                case 3: return new Ai(3) { QuiescenceDepth = 4, CheckExtension = true, FullEval = true };
                case 4: return new Ai(6) { QuiescenceDepth = 4, CheckExtension = true, FullEval = true, TimeMs = 1000 };
                case 5: return new Ai(8) { QuiescenceDepth = 6, CheckExtension = true, FullEval = true, TimeMs = 2000 };
                default: return new Ai(2) { QuiescenceDepth = 2, CheckExtension = true };
            }
        }

        public static string LevelName(int level)
        {
            switch (level)
            {
                case 0: return "2 players";
                case 1: return "Easy";
                case 2: return "Normal";
                case 3: return "Hard";
                case 4: return "Expert";
                default: return "Master";
            }
        }

        public static double XpMultiplier(int level)
        {
            switch (level)
            {
                case 0: return 0.5;
                case 1: return 0.7;
                case 3: return 1.4;
                case 4: return 1.8;
                case 5: return 2.2;
                default: return 1.0;
            }
        }

        public GameAction Choose(GameState real)
        {
            var actions = MoveGen.LegalActions(real);
            if (actions.Count == 0) throw new InvalidOperationException("No legal actions");
            if (real.PendingSquare >= 0) return ChoosePickup(real, actions);
            if (RandomChance > 0 && rng.NextDouble() < RandomChance) return actions[rng.Next(actions.Count)];

            var root = real.Clone();
            root.SearchMode = true;
            Order(root, actions);

            clock = System.Diagnostics.Stopwatch.StartNew();
            nodes = 0;
            aborted = false;
            GameAction best = actions[0];
            int firstDepth = TimeMs > 0 ? 1 : Depth;
            for (int d = firstDepth; d <= Depth; d++)
            {
                canAbort = TimeMs > 0 && d > 1;
                int alpha = -Inf;
                GameAction iterBest = actions[0];
                bool complete = true;
                foreach (var a in actions)
                {
                    var child = root.Clone();
                    Rules.Apply(child, a);
                    int score;
                    GameResult ended = child.ToMove != root.ToMove ? Rules.GetResult(child) : GameResult.Ongoing;
                    if (ended == GameResult.Ongoing) score = ScoreChild(root, child, d - 1, alpha, Inf, 0);
                    else score = ended == GameResult.Draw ? 0 : Mate; // take a mate, and never stalemate a won position away
                    if (aborted) { complete = false; break; }
                    if (score > alpha)
                    {
                        alpha = score;
                        iterBest = a;
                    }
                }
                if (!complete) break;
                best = iterBest;
                actions.Remove(best);
                actions.Insert(0, best); // search the best move first next iteration
                if (alpha >= Mate - 100) break; // a forced mate: no need to look deeper
                if (TimeMs > 0 && clock.ElapsedMilliseconds * 3 > TimeMs) break; // the next iteration would not finish
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

        int ScoreChild(GameState parent, GameState child, int depth, int alpha, int beta, int ext)
        {
            // A Double Move first step leaves the same side to move, so the sign does not flip.
            if (child.ToMove == parent.ToMove) return Search(child, depth, alpha, beta, ext);
            return -Search(child, depth, -beta, -alpha, ext);
        }

        bool TimeUp()
        {
            if (!canAbort) return false;
            if ((++nodes & 127) != 0) return false;
            if (clock.ElapsedMilliseconds > TimeMs) aborted = true;
            return aborted;
        }

        int Search(GameState s, int depth, int alpha, int beta, int ext)
        {
            if (aborted || TimeUp()) return 0;
            if (s.Ply >= s.Config.MaxPly || s.HalfmoveClock >= 100) return 0;
            if (depth <= 0)
            {
                if (CheckExtension && ext < 2 && Geometry.IsKingAttacked(s, s.ToMove)) { depth = 1; ext++; }
                else return Quiesce(s, alpha, beta, QuiescenceDepth);
            }

            var actions = MoveGen.LegalActions(s);
            if (actions.Count == 0)
                return Geometry.IsKingAttacked(s, s.ToMove) ? -(Mate - s.Ply) : 0;

            Order(s, actions);
            int best = -Inf;
            foreach (var a in actions)
            {
                var child = s.Clone();
                Rules.Apply(child, a);
                int score = ScoreChild(s, child, depth - 1, alpha, beta, ext);
                if (aborted) return 0;
                if (score > best) best = score;
                if (best > alpha) alpha = best;
                if (alpha >= beta) break;
            }
            return best;
        }

        /// <summary>Captures, shots and blasts only, so a position is judged once the tactics have settled.</summary>
        int Quiesce(GameState s, int alpha, int beta, int qd)
        {
            if (aborted || TimeUp()) return 0;
            bool inCheck = Geometry.IsKingAttacked(s, s.ToMove);
            if (!inCheck)
            {
                int stand = Evaluate(s, FullEval);
                if (stand >= beta) return stand;
                if (stand > alpha) alpha = stand;
                if (qd <= 0) return alpha;
            }
            else if (qd <= 0) return Evaluate(s, FullEval);

            var actions = MoveGen.LegalActions(s, !inCheck);
            if (inCheck && actions.Count == 0) return -(Mate - s.Ply);
            Order(s, actions);
            int best = inCheck ? -Inf : alpha;
            foreach (var a in actions)
            {
                var child = s.Clone();
                Rules.Apply(child, a);
                int score = child.ToMove == s.ToMove
                    ? Quiesce(child, alpha, beta, qd - 1)
                    : -Quiesce(child, -beta, -alpha, qd - 1);
                if (aborted) return 0;
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
                case ActionKind.Swap: return -50;
                default: return 0;
            }
        }

        /// <summary>Static score from the side-to-move's point of view.</summary>
        public static int Evaluate(GameState s) { return Evaluate(s, false); }

        public static int Evaluate(GameState s, bool full)
        {
            int score = 0;
            int mysteryCount = 0;
            var mystery = new int[8];
            var weight = new int[8];
            for (int sq = 0; sq < 64 && mysteryCount < 8; sq++)
            {
                if (!s.Mystery[sq]) continue;
                weight[mysteryCount] = SquareWeight((PowerupKind)s.Content[sq]);
                mystery[mysteryCount++] = sq;
            }
            int pullWhite = 0, pullBlack = 0;

            for (int sq = 0; sq < 64; sq++)
            {
                var p = s.Cells[sq];
                if (p.IsEmpty) continue;

                if (mysteryCount > 0 && p.Type != PieceType.King)
                {
                    for (int m = 0; m < mysteryCount; m++)
                    {
                        int d = Math.Max(Math.Abs(Sq.File(sq) - Sq.File(mystery[m])), Math.Abs(Sq.Rank(sq) - Sq.Rank(mystery[m])));
                        int pull = d <= 3 ? (4 - d) * weight[m] : 0;
                        if (p.Side == Side.White) { if (pull > pullWhite) pullWhite = pull; }
                        else if (pull > pullBlack) pullBlack = pull;
                    }
                }
                int v = PieceValue[(int)p.Type];
                int f = Sq.File(sq), r = Sq.Rank(sq);
                int centre = 6 - (Math.Abs(2 * f - 7) + Math.Abs(2 * r - 7)) / 2;

                if (p.Type == PieceType.Knight || p.Type == PieceType.Bishop) v += 4 * centre;
                else if (p.Type == PieceType.Pawn) v += 6 * (p.Side == Side.White ? r : 7 - r) + 2 * centre;

                if (p.Has(Buff.DoubleMove)) v += 35;
                if (p.Has(Buff.TripleMove)) v += 55;
                if (p.Has(Buff.Ghost)) v += 8 * p.GhostTurns;
                if (p.Has(Buff.Fireball)) v += 90;
                if (p.Has(Buff.Poison)) v += 30;
                if (p.Has(Buff.Armor)) v += 15 * p.ArmorTurns;
                if (p.Has(Buff.SmallBomb)) v += 50;
                if (p.Has(Buff.BigBomb)) v += 110;
                if (p.IsStunned) v -= (PieceValue[(int)p.Type] / 6) * p.StunTurns / 3 + 10;

                score += p.Side == Side.White ? v : -v;
            }

            score += PerkValue(s, Side.White) - PerkValue(s, Side.Black);
            if (full) score += Structure(s);
            score += pullWhite - pullBlack;
            return s.ToMove == Side.White ? score : -score;
        }

        /// <summary>Pawn structure, rook files, bishop pair and king shelter, from White's point of view.</summary>
        static int Structure(GameState s)
        {
            var pawns = new int[2, 8];
            var lowest = new int[2, 8];   // most advanced pawn per file and side, counted from its own side (-1 none)
            var leastAdv = new int[2, 8]; // least advanced pawn per file and side
            for (int side = 0; side < 2; side++) for (int f = 0; f < 8; f++) { lowest[side, f] = -1; leastAdv[side, f] = 99; }
            var bishops = new int[2];
            int[] king = { -1, -1 };
            int nonPawn = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                var p = s.Cells[sq];
                if (p.IsEmpty) continue;
                int side = (int)p.Side, f = Sq.File(sq), r = Sq.Rank(sq);
                if (p.Type == PieceType.Pawn)
                {
                    pawns[side, f]++;
                    int adv = side == 0 ? r : 7 - r;
                    if (adv > lowest[side, f]) lowest[side, f] = adv;
                    if (adv < leastAdv[side, f]) leastAdv[side, f] = adv;
                }
                else if (p.Type != PieceType.King) nonPawn++;
                if (p.Type == PieceType.Bishop) bishops[side]++;
                if (p.Type == PieceType.King) king[side] = sq;
            }
            int score = 0;
            for (int side = 0; side < 2; side++)
            {
                int sign = side == 0 ? 1 : -1;
                int other = 1 - side;
                int v = 0;
                for (int f = 0; f < 8; f++)
                {
                    int n = pawns[side, f];
                    if (n == 0) continue;
                    if (n > 1) v -= 12 * (n - 1);
                    bool isolated = (f == 0 || pawns[side, f - 1] == 0) && (f == 7 || pawns[side, f + 1] == 0);
                    if (isolated) v -= 10;
                    // passed: no enemy pawn ahead on this or an adjacent file
                    bool passed = true;
                    for (int g = Math.Max(0, f - 1); g <= Math.Min(7, f + 1) && passed; g++)
                    {
                        if (pawns[other, g] == 0) continue;
                        int mineAdv = lowest[side, f];
                        if (7 - leastAdv[other, g] > mineAdv) passed = false; // an enemy pawn still stands in front of ours
                    }
                    if (passed) v += 15 + 5 * lowest[side, f];
                }
                if (bishops[side] >= 2) v += 25;
                // rooks on open / half-open files
                for (int sq = 0; sq < 64; sq++)
                {
                    var p = s.Cells[sq];
                    if (p.Type != PieceType.Rook || (int)p.Side != side) continue;
                    int f = Sq.File(sq);
                    if (pawns[side, f] == 0) v += pawns[other, f] == 0 ? 14 : 7;
                }
                // king shelter while there is still material to attack with
                if (king[side] >= 0 && nonPawn >= 4)
                {
                    int kf = Sq.File(king[side]), kr = Sq.Rank(king[side]);
                    int dir = side == 0 ? 1 : -1;
                    int shield = 0;
                    for (int df = -1; df <= 1; df++)
                    {
                        int f = kf + df;
                        if (f < 0 || f > 7) continue;
                        for (int step = 1; step <= 2; step++)
                        {
                            int r = kr + dir * step;
                            if (r < 0 || r > 7) break;
                            var q = s.Cells[Sq.Make(f, r)];
                            if (q.Type == PieceType.Pawn && (int)q.Side == side) { shield++; break; }
                        }
                    }
                    v += 7 * Math.Min(shield, 3);
                    if (pawns[side, kf] == 0) v -= 14;
                }
                score += sign * v;
            }
            return score;
        }

        /// <summary>
        /// Small reward for the piece closest to an unopened mystery square, so the AI goes after powerups. Capped well
        /// below a pawn so it never gives up material for one.
        /// </summary>
        static int MysteryPull(int distance)
        {
            return distance <= 3 ? (4 - distance) * 4 : 0;
        }

        /// <summary>Per-step pull of a powerup square: hidden ones get a flat amount, revealed ones scale with their worth.</summary>
        static int SquareWeight(PowerupKind kind)
        {
            switch (kind)
            {
                case PowerupKind.None: return 4;
                case PowerupKind.Steal: return 8;
                case PowerupKind.BigBomb: return 6;
                case PowerupKind.Stun: return 6;
                case PowerupKind.Fireball: return 5;
                case PowerupKind.SmallBomb: return 4;
                case PowerupKind.SpikedArmor: return 3;
                default: return PowerupInfo.IsTransform(kind) ? 6 : 2;
            }
        }

        static int PerkValue(GameState s, Side side)
        {
            return 150 * s.Perks(side, PowerupKind.Steal)
                + 110 * s.Perks(side, PowerupKind.Stun)
                + 40 * s.Perks(side, PowerupKind.Boulder)
                + 25 * s.Perks(side, PowerupKind.Reroll);
        }
    }
}
