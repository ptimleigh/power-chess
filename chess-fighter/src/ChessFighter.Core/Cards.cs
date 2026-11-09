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
    }

    public static class CardInfo
    {
        /// <summary>Immediate cards are used straight away when drawn (if they can be); the rest wait in the bag.</summary>
        public static bool IsImmediate(CardKind k) { return k == CardKind.Promotion || k == CardKind.PlaceBoulder; }

        /// <summary>Reactions are never played from the bag on your own turn.</summary>
        public static bool IsReaction(CardKind k) { return k == CardKind.NuhUh; }
    }

    public static class Cards
    {
        public const int First = (int)CardKind.NoMorePowers;
        public const int Last = (int)CardKind.NuhUh;

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
                default: return 0;
            }
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
