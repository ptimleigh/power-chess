using System;
using System.Collections.Generic;

namespace ChessFighter.Core
{
    /// <summary>
    /// Cards sit in a visible bag (no hand limit) and are played at will, which costs the turn. Immediate cards fire the moment
    /// they are drawn from a Draw Card square. Nuh-uh is a reaction: whenever a card is activated, the rival may cancel it.
    /// See docs/POWERS.md.
    /// </summary>
    public enum CardKind : byte
    {
        None = 0,
        /// <summary>Removes every active power from every piece and every powerup square; squares return after one round.</summary>
        NoMorePowers,
        /// <summary>Switch any 2 of your pieces (not kings).</summary>
        RoleReversal,
        /// <summary>Sacrifice a knight to promote a pawn, bishop or other knight to a rook.</summary>
        HorseSacrifice,
        /// <summary>Immediate: promote one of your pieces by one level (pawn to knight or bishop, knight or bishop to rook, rook to queen).</summary>
        Promotion,
        /// <summary>Immediate: put a boulder on an empty square.</summary>
        PlaceBoulder,
        /// <summary>Remove one boulder.</summary>
        SmashBoulder,
        /// <summary>Bring one of your captured pieces back, anywhere on the board.</summary>
        SummonCaissa,
        /// <summary>Reaction: cancel the card a rival just activated.</summary>
        NuhUh,
        /// <summary>Undo one round of moves (the board goes back to the start of your previous turn).</summary>
        TakeBack,
        /// <summary>Mars shows you the best move at the start of your next 3 turns.</summary>
        SummonMars,
        /// <summary>A dragon burns every piece on one row (rows 3 to 6, not a row holding a king).</summary>
        DragonStrike,
        /// <summary>Shiva strikes up to 3 random rival pieces (never kings).</summary>
        SummonShiva,
        /// <summary>Immediate: every piece returns to its starting square, keeping its powers.</summary>
        HitRestart,
        /// <summary>Immediate: the board rotates 180 degrees and every piece changes sides, so you command the other army.</summary>
        WorldUpsideDown,
        /// <summary>A missile will strike a quadrant in 10 moves; evacuate or lose what is there (a king there loses the game).</summary>
        MissileStrike,
    }

    public static class CardInfo
    {
        /// <summary>Immediate cards are used straight away when drawn (if they can be); the rest wait in the bag.</summary>
        public static bool IsImmediate(CardKind k)
        {
            return k == CardKind.Promotion || k == CardKind.PlaceBoulder || k == CardKind.HitRestart || k == CardKind.WorldUpsideDown;
        }

        /// <summary>Reactions are never played from the bag on your own turn.</summary>
        public static bool IsReaction(CardKind k) { return k == CardKind.NuhUh; }
    }

    public static class Cards
    {
        public const int First = (int)CardKind.NoMorePowers;
        public const int Last = (int)CardKind.MissileStrike;

        /// <summary>A PlayCard action carries the card in the low byte of Aux and a card-specific choice (a piece type) above it.</summary>
        public static int Pack(CardKind kind, int extra) { return (int)kind | (extra << 8); }
        public static CardKind KindOf(int aux) { return (CardKind)(aux & 0xFF); }
        public static int ExtraOf(int aux) { return aux >> 8; }

        public static GameAction Play(CardKind kind, int from, int to, int extra = 0)
        {
            return new GameAction(ActionKind.PlayCard, from, to, Pack(kind, extra));
        }

        public static void Emit(List<GameEvent> ev, EventKind kind, int from, int to, Side side, CardKind card)
        {
            if (ev == null) return;
            ev.Add(new GameEvent { Kind = kind, From = from, To = to, Piece = PieceType.None, Side = side, Powerup = PowerupKind.None, Card = card });
        }

        // ---------- drawing ----------

        /// <summary>
        /// Draw weight. A card that would be dead (Smash with no boulder on the board, Caissa with nothing captured, a boulder
        /// when the board is full of them) is not drawn at all, so nobody ends up sitting on a pile of unplayable cards.
        /// </summary>
        public static int Weight(GameState s, Side drawer, CardKind k)
        {
            switch (k)
            {
                case CardKind.NoMorePowers: return 5;
                case CardKind.RoleReversal: return 6;
                case CardKind.HorseSacrifice: return 5;
                case CardKind.Promotion: return 7;
                case CardKind.PlaceBoulder: return s.BoulderCount() < s.Config.MaxBoulders ? 6 : 0;
                case CardKind.SmashBoulder: return s.BoulderCount() > 0 ? 6 : 0;
                case CardKind.SummonCaissa: return CapturedTotal(s, drawer) > 0 ? 5 : 0;
                case CardKind.NuhUh: return 6;
                case CardKind.TakeBack: return s.TurnStarts != null && s.TurnStarts[0] != null ? 4 : 0;
                case CardKind.SummonMars: return 5;
                case CardKind.DragonStrike: return 5;
                case CardKind.SummonShiva: return RivalTargets(s, drawer) > 0 ? 5 : 0;
                case CardKind.HitRestart: return 4;
                case CardKind.WorldUpsideDown: return 4;
                case CardKind.MissileStrike: return s.MissileLeft == 0 ? 4 : 0;
                default: return 0;
            }
        }

        /// <summary>Rival pieces a card strike could take (not kings, not armored).</summary>
        public static int RivalTargets(GameState s, Side me)
        {
            int n = 0;
            for (int i = 0; i < 64; i++) if (Geometry.IsVulnerable(s.Cells[i], me)) n++;
            return n;
        }

        public static int CapturedTotal(GameState s, Side side)
        {
            int n = 0;
            for (int t = (int)PieceType.Pawn; t <= (int)PieceType.Queen; t++) n += s.Captured[(int)side * 8 + t];
            return n;
        }

        public static CardKind Draw(GameState s, Side drawer)
        {
            int total = 0;
            for (int k = First; k <= Last; k++) total += Weight(s, drawer, (CardKind)k);
            if (total <= 0) return CardKind.None;
            int roll = s.Rng.Range(total);
            for (int k = First; k <= Last; k++)
            {
                roll -= Weight(s, drawer, (CardKind)k);
                if (roll < 0) return (CardKind)k;
            }
            return CardKind.None;
        }

        // ---------- what a card can do right now (before king safety is checked) ----------

        static bool Free(GameState s, int sq)
        {
            return s.Cells[sq].IsEmpty && !s.Boulders[sq] && !s.Mystery[sq] && !s.Portal[sq];
        }

        public static bool AnyActivePower(GameState s)
        {
            for (int i = 0; i < 64; i++)
            {
                if (s.Mystery[i]) return true;
                var p = s.Cells[i];
                if (!p.IsEmpty && p.Buffs != Buff.None) return true;
            }
            return false;
        }

        public static void Generate(GameState s, Side me, CardKind kind, List<GameAction> o)
        {
            switch (kind)
            {
                case CardKind.PlaceBoulder:
                    if (s.BoulderCount() >= s.Config.MaxBoulders) break;
                    for (int sq = 0; sq < 64; sq++) if (Free(s, sq)) o.Add(Play(kind, sq, sq));
                    break;

                case CardKind.SmashBoulder:
                    for (int sq = 0; sq < 64; sq++) if (s.Boulders[sq]) o.Add(Play(kind, sq, sq));
                    break;

                case CardKind.Promotion:
                    for (int sq = 0; sq < 64; sq++)
                    {
                        var p = s.Cells[sq];
                        if (p.IsEmpty || p.Side != me) continue;
                        switch (p.Type)
                        {
                            case PieceType.Pawn:
                                o.Add(Play(kind, sq, sq, (int)PieceType.Knight));
                                o.Add(Play(kind, sq, sq, (int)PieceType.Bishop));
                                break;
                            case PieceType.Knight:
                            case PieceType.Bishop:
                                o.Add(Play(kind, sq, sq, (int)PieceType.Rook));
                                break;
                            case PieceType.Rook:
                                o.Add(Play(kind, sq, sq, (int)PieceType.Queen));
                                break;
                        }
                    }
                    break;

                case CardKind.RoleReversal:
                    for (int a = 0; a < 64; a++)
                    {
                        var pa = s.Cells[a];
                        if (pa.IsEmpty || pa.Side != me || pa.Type == PieceType.King) continue;
                        for (int b = a + 1; b < 64; b++)
                        {
                            var pb = s.Cells[b];
                            if (pb.IsEmpty || pb.Side != me || pb.Type == PieceType.King) continue;
                            if (pa.Type == pb.Type && pa.Buffs == pb.Buffs) continue;
                            // A pawn may land on the far rank (it promotes) but never on its own back rank.
                            if (pa.Type == PieceType.Pawn && Sq.Rank(b) == (pa.Side == Side.White ? 0 : 7)) continue;
                            if (pb.Type == PieceType.Pawn && Sq.Rank(a) == (pb.Side == Side.White ? 0 : 7)) continue;
                            o.Add(Play(kind, a, b));
                        }
                    }
                    break;

                case CardKind.HorseSacrifice:
                    for (int k = 0; k < 64; k++)
                    {
                        var knight = s.Cells[k];
                        if (knight.Type != PieceType.Knight || knight.Side != me) continue;
                        for (int t = 0; t < 64; t++)
                        {
                            if (t == k) continue;
                            var target = s.Cells[t];
                            if (target.IsEmpty || target.Side != me) continue;
                            if (target.Type == PieceType.Pawn || target.Type == PieceType.Bishop || target.Type == PieceType.Knight)
                                o.Add(Play(kind, k, t));
                        }
                    }
                    break;

                case CardKind.NoMorePowers:
                    if (AnyActivePower(s)) o.Add(Play(kind, 0, 0));
                    break;

                case CardKind.TakeBack:
                    if (s.TurnStarts != null && s.TurnStarts[0] != null) o.Add(Play(kind, 0, 0));
                    break;

                case CardKind.SummonMars:
                    if (s.Mars[(int)me] == 0) o.Add(Play(kind, 0, 0));
                    break;

                case CardKind.DragonStrike:
                    for (int r = 2; r <= 5; r++)
                    {
                        bool king = false, target = false;
                        for (int f = 0; f < 8; f++)
                        {
                            var p = s.Cells[Sq.Make(f, r)];
                            if (p.Type == PieceType.King) king = true;
                            if (Geometry.IsVulnerableToBomb(p)) target = true;
                        }
                        if (!king && target) o.Add(Play(kind, Sq.Make(0, r), Sq.Make(0, r)));
                    }
                    break;

                case CardKind.SummonShiva:
                    if (RivalTargets(s, me) > 0) o.Add(Play(kind, 0, 0));
                    break;

                case CardKind.HitRestart:
                    for (int i = 0; i < 64; i++)
                    {
                        var p = s.Cells[i];
                        if (!p.IsEmpty && p.Origin > 0 && p.Origin - 1 != i) { o.Add(Play(kind, 0, 0)); break; }
                    }
                    break;

                case CardKind.WorldUpsideDown:
                    o.Add(Play(kind, 0, 0));
                    break;

                case CardKind.MissileStrike:
                    if (s.MissileLeft == 0)
                        foreach (int anchor in new[] { 0, 4, 32, 36 }) o.Add(Play(kind, anchor, anchor));
                    break;

                case CardKind.SummonCaissa:
                    for (int t = (int)PieceType.Pawn; t <= (int)PieceType.Queen; t++)
                    {
                        if (s.Captured[(int)me * 8 + t] == 0) continue;
                        for (int sq = 0; sq < 64; sq++)
                        {
                            if (!Free(s, sq)) continue;
                            if (t == (int)PieceType.Pawn && (Sq.Rank(sq) == 0 || Sq.Rank(sq) == 7)) continue;
                            o.Add(Play(kind, sq, sq, t));
                        }
                    }
                    break;
            }
        }

        // ---------- effects ----------

        public static void Resolve(GameState s, Side me, GameAction a, List<GameEvent> ev)
        {
            var kind = KindOf(a.Aux);
            int extra = ExtraOf(a.Aux);
            switch (kind)
            {
                case CardKind.PlaceBoulder:
                    s.Boulders[a.To] = true;
                    Rules.Emit(ev, EventKind.BoulderPlaced, a.To, a.To, PieceType.None, me, PowerupKind.None);
                    break;

                case CardKind.SmashBoulder:
                    s.Boulders[a.To] = false;
                    Rules.Emit(ev, EventKind.BoulderCleared, a.To, a.To, PieceType.None, me, PowerupKind.None);
                    break;

                case CardKind.Promotion:
                {
                    var p = s.Cells[a.From];
                    p.Type = (PieceType)extra;
                    Rules.Place(s, a.From, p);
                    s.ClockReset = true;
                    Rules.Emit(ev, EventKind.Promoted, a.From, a.From, p.Type, me, PowerupKind.None);
                    break;
                }

                case CardKind.RoleReversal:
                {
                    var pa = s.Cells[a.From];
                    var pb = s.Cells[a.To];
                    Rules.Place(s, a.From, pb);
                    Rules.Place(s, a.To, pa);
                    Rules.Emit(ev, EventKind.Swapped, a.From, a.To, pa.Type, me, PowerupKind.None);
                    Rules.PromoteIfAtEnd(s, a.To, ev, me);
                    Rules.PromoteIfAtEnd(s, a.From, ev, me);
                    break;
                }

                case CardKind.HorseSacrifice:
                {
                    Rules.Destroy(s, a.From, ev);
                    var p = s.Cells[a.To];
                    p.Type = PieceType.Rook;
                    Rules.Place(s, a.To, p);
                    s.ClockReset = true;
                    Rules.Emit(ev, EventKind.Promoted, a.To, a.To, PieceType.Rook, me, PowerupKind.None);
                    break;
                }

                case CardKind.NoMorePowers:
                    for (int i = 0; i < 64; i++)
                    {
                        var p = s.Cells[i];
                        if (!p.IsEmpty && p.Buffs != Buff.None)
                        {
                            p.Buffs = Buff.None;
                            p.Tier = 0;
                            p.GhostTurns = 0;
                            p.PoisonTurns = 0;
                            p.ArmorTurns = 0;
                            s.Cells[i] = p;
                        }
                        s.Mystery[i] = false;
                        s.Content[i] = 0;
                    }
                    // Squares start generating again after one round of moves.
                    s.NoSpawnUntilPly = s.Ply + 3;
                    Rules.Emit(ev, EventKind.PowersCleared, 0, 0, PieceType.None, me, PowerupKind.None);
                    break;

                case CardKind.TakeBack:
                {
                    // The board returns to the start of your previous turn. Bags and the random sequence are kept; lost pieces come back with it.
                    var snap = s.TurnStarts[0];
                    Array.Copy(snap.Cells, s.Cells, 64);
                    Array.Copy(snap.Boulders, s.Boulders, 64);
                    Array.Copy(snap.Mystery, s.Mystery, 64);
                    Array.Copy(snap.Content, s.Content, 64);
                    Array.Copy(snap.Portal, s.Portal, 64);
                    Array.Copy(snap.Captured, s.Captured, 16);
                    s.Castling = snap.Castling;
                    s.EnPassant = snap.EnPassant;
                    s.HalfmoveClock = snap.HalfmoveClock;
                    s.MissileLeft = snap.MissileLeft;
                    s.MissileAnchor = snap.MissileAnchor;
                    s.TurnStarts = new GameState[3];
                    Rules.Emit(ev, EventKind.TurnRewound, 0, 0, PieceType.None, me, PowerupKind.None);
                    break;
                }

                case CardKind.SummonMars:
                    s.Mars[(int)me] = 4; // the turn this card costs uses one; three turns of hints follow
                    Rules.Emit(ev, EventKind.BuffGained, 0, 0, PieceType.None, me, PowerupKind.None);
                    break;

                case CardKind.DragonStrike:
                {
                    int rank = Sq.Rank(a.To);
                    Rules.Emit(ev, EventKind.DragonFire, Sq.Make(0, rank), Sq.Make(7, rank), PieceType.None, me, PowerupKind.None);
                    for (int f = 0; f < 8; f++)
                    {
                        int sq = Sq.Make(f, rank);
                        if (Geometry.IsVulnerableToBomb(s.Cells[sq])) Rules.Destroy(s, sq, ev);
                    }
                    s.ClockReset = true;
                    break;
                }

                case CardKind.SummonShiva:
                {
                    // Three random rival pieces, never one whose removal would expose your own king. Randomness is drawn from a copy
                    // of the generator in lookahead and legality probes, so those never disturb the real sequence.
                    var rng = s.Rng;
                    var pool = new List<int>();
                    for (int i = 0; i < 64; i++) if (Geometry.IsVulnerable(s.Cells[i], me)) pool.Add(i);
                    int struck = 0;
                    while (struck < 3 && pool.Count > 0)
                    {
                        int pick = rng.Range(pool.Count);
                        int t = pool[pick];
                        pool.RemoveAt(pick);
                        var saved = s.Cells[t];
                        s.Cells[t] = default(Piece);
                        bool exposes = Geometry.IsKingAttacked(s, me);
                        s.Cells[t] = saved;
                        if (exposes) continue;
                        Rules.Emit(ev, EventKind.ShivaStrike, t, t, saved.Type, me, PowerupKind.None);
                        Rules.Destroy(s, t, ev);
                        struck++;
                    }
                    if (!s.Probe && !s.SearchMode) s.Rng = rng;
                    s.ClockReset = true;
                    break;
                }

                case CardKind.HitRestart:
                    RestartBoard(s, ev, me);
                    break;

                case CardKind.WorldUpsideDown:
                    FlipBoard(s, ev, me);
                    break;

                case CardKind.MissileStrike:
                    s.MissileLeft = 11; // this turn's end uses one, leaving 10 plies: 5 turns each
                    s.MissileAnchor = a.To;
                    Rules.Emit(ev, EventKind.MissileLaunched, a.To, a.To, PieceType.None, me, PowerupKind.None);
                    break;

                case CardKind.SummonCaissa:
                {
                    var type = (PieceType)extra;
                    int idx = (int)me * 8 + extra;
                    if (s.Captured[idx] > 0) s.Captured[idx]--;
                    Rules.Place(s, a.To, new Piece(type, me));
                    s.ClockReset = true;
                    Rules.Emit(ev, EventKind.PieceRestored, a.To, a.To, type, me, PowerupKind.None);
                    break;
                }
            }
        }

        /// <summary>Hit restart: every piece returns to the square it started on (or stays put when that is unknown or taken).</summary>
        static void RestartBoard(GameState s, List<GameEvent> ev, Side me)
        {
            var dest = new int[64];
            var claimed = new bool[64];
            for (int i = 0; i < 64; i++) dest[i] = -1;
            for (int i = 0; i < 64; i++)
            {
                var p = s.Cells[i];
                if (!p.IsEmpty && p.Origin == 0) { dest[i] = i; claimed[i] = true; }
            }
            for (int i = 0; i < 64; i++)
            {
                var p = s.Cells[i];
                if (p.IsEmpty || p.Origin == 0) continue;
                int want = p.Origin - 1;
                if (!claimed[want]) { dest[i] = want; claimed[want] = true; }
            }
            for (int i = 0; i < 64; i++)
            {
                if (s.Cells[i].IsEmpty || dest[i] >= 0) continue;
                if (!claimed[i]) { dest[i] = i; claimed[i] = true; continue; }
                for (int j = 0; j < 64; j++) if (!claimed[j]) { dest[i] = j; claimed[j] = true; break; }
            }
            var old = (Piece[])s.Cells.Clone();
            for (int i = 0; i < 64; i++) s.Cells[i] = default(Piece);
            for (int i = 0; i < 64; i++)
            {
                if (old[i].IsEmpty) continue;
                int d = dest[i];
                s.Cells[d] = old[i];
                // Landing squares are cleared of anything a piece cannot stand on.
                s.Boulders[d] = false;
                s.Mystery[d] = false;
                s.Content[d] = 0;
                if (s.Portal[d]) { s.Portal[d] = false; s.Portal[Sq.Mirror(d)] = false; }
            }
            s.EnPassant = -1;
            s.ClockReset = true;
            Rules.Emit(ev, EventKind.BoardRestarted, 0, 0, PieceType.None, me, PowerupKind.None);
        }

        /// <summary>
        /// World turned upside down: rotate the board 180 degrees and swap every piece's colour, so the player keeps their seat
        /// but now commands the other army, which has been rotated to face them. Powerup squares, boulders, losses and starting
        /// squares rotate with it. Castling rights are gone.
        /// </summary>
        static void FlipBoard(GameState s, List<GameEvent> ev, Side me)
        {
            var cells = (Piece[])s.Cells.Clone();
            var boulders = (bool[])s.Boulders.Clone();
            var mystery = (bool[])s.Mystery.Clone();
            var content = (byte[])s.Content.Clone();
            var portal = (bool[])s.Portal.Clone();
            for (int i = 0; i < 64; i++)
            {
                int j = 63 - i;
                var p = cells[i];
                if (!p.IsEmpty)
                {
                    p.Side = p.Side.Opponent();
                    if (p.Origin > 0) p.Origin = (byte)(64 - p.Origin);
                }
                s.Cells[j] = p;
                s.Boulders[j] = boulders[i];
                s.Mystery[j] = mystery[i];
                s.Content[j] = content[i];
                s.Portal[j] = portal[i];
            }
            for (int t = 0; t < 8; t++)
            {
                byte tmp = s.Captured[t];
                s.Captured[t] = s.Captured[8 + t];
                s.Captured[8 + t] = tmp;
            }
            s.Castling = 0;
            s.EnPassant = -1;
            s.ClockReset = true;
            Rules.Emit(ev, EventKind.BoardFlipped, 0, 0, PieceType.None, me, PowerupKind.None);
        }
    }

    /// <summary>
    /// A simple card player for the AI. Cards are kept out of its search (their outcomes include hidden information and a
    /// reaction window), so it plays one only when a one-ply look says it clearly helps, and always answers with Nuh-uh.
    /// </summary>
    static class CardAi
    {
        const int PlayThreshold = 150;

        public static GameAction Respond(GameState s, List<GameAction> actions)
        {
            foreach (var a in actions) if (a.Kind == ActionKind.UseNuhUh) return a;
            return actions[0];
        }

        public static GameAction ChooseImmediate(GameState s, List<GameAction> actions)
        {
            int best = int.MinValue;
            var pick = actions[0];
            foreach (var a in actions)
            {
                int score = Score(s, a);
                if (score > best) { best = score; pick = a; }
            }
            return pick;
        }

        public static bool TryPlay(GameState s, List<GameAction> plays, out GameAction play)
        {
            int baseline = Ai.Evaluate(s);
            int best = int.MinValue;
            play = default(GameAction);
            foreach (var a in plays)
            {
                int score = Score(s, a);
                if (score > best) { best = score; play = a; }
            }
            return best != int.MinValue && best - baseline >= PlayThreshold;
        }

        /// <summary>Static value for the mover after the card resolves (the reaction window is skipped).</summary>
        static int Score(GameState s, GameAction a)
        {
            var child = s.Clone();
            child.SearchMode = true;
            child.Probe = true;
            Rules.Apply(child, a);
            int v = Ai.Evaluate(child);
            return child.ToMove == s.ToMove ? v : -v;
        }
    }
}
