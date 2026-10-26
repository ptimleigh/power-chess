using System;

namespace PowerChess.Core
{
    /// <summary>Limits and XP effect of hints. Pure rules so the web app, the native shell and a later Unity client agree.</summary>
    public static class HintPolicy
    {
        public const int FreePerGame = 3;
        /// <summary>A game where hints were used pays this share of its XP.</summary>
        public const double HintedXpScale = 0.8;

        public static int Remaining(int used, int bonus)
        {
            return Math.Max(0, FreePerGame + bonus - used);
        }

        public static double XpScale(int hintsUsed)
        {
            return hintsUsed > 0 ? HintedXpScale : 1.0;
        }

        /// <summary>True when the free hints are gone but an ad could buy another one.</summary>
        public static bool CanBuyHint(int used, int bonus, bool adsAvailable)
        {
            return adsAvailable && Remaining(used, bonus) == 0;
        }
    }

    /// <summary>The best move for the side to move, found with the strong search, plus a short caption saying what it is.</summary>
    public static class Hints
    {
        static readonly Ai hintAi = Ai.ForLevel(3);

        /// <summary>The best action by the Hard search. Deterministic. Throws if there is no legal action.</summary>
        public static GameAction Suggest(GameState state)
        {
            return hintAi.Choose(state);
        }

        static string Name(PieceType t)
        {
            return t == PieceType.None ? "piece" : t.ToString();
        }

        public static string Describe(GameState s, GameAction a)
        {
            switch (a.Kind)
            {
                case ActionKind.Move:
                {
                    var p = s.Cells[a.From];
                    string cap = !s.Cells[a.To].IsEmpty ? " (takes " + Name(s.Cells[a.To].Type) + ")" : "";
                    string promo = a.Aux != 0 ? ", promoting to " + Name((PieceType)a.Aux) : "";
                    return "Try " + Name(p.Type) + " " + Sq.Name(a.From) + " to " + Sq.Name(a.To) + cap + promo;
                }
                case ActionKind.Fireball:
                    return "Fire the fireball from " + Sq.Name(a.From) + " at " + Sq.Name(a.To);
                case ActionKind.Detonate:
                    return "Detonate the bomb on " + Sq.Name(a.From);
                case ActionKind.Stun:
                    return "Stun the piece on " + Sq.Name(a.To);
                case ActionKind.Steal:
                    return "Steal the piece on " + Sq.Name(a.To) + " with " + Sq.Name(a.From);
                case ActionKind.Swap:
                    return "Swap " + Sq.Name(a.From) + " and " + Sq.Name(a.To);
                case ActionKind.PlaceBoulder:
                    return "Drop a boulder on " + Sq.Name(a.To);
                case ActionKind.ClearBoulder:
                    return "Smash the boulder on " + Sq.Name(a.To);
                case ActionKind.KeepPickup:
                    return "Keep the powerup";
                case ActionKind.RerollPickup:
                    return "Reroll the powerup";
                default:
                    return "Try " + a.Kind;
            }
        }
    }
}
