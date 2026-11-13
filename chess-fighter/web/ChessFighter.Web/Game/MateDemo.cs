using ChessFighter.Core;

namespace ChessFighter.Web.Game;

/// <summary>
/// Turns one failed escape from a checkmate (see <see cref="MateExplainer"/>) into a short looping animation on the real 8x8 position:
/// try the move, then show the reply that takes the king. Text names pieces by owner and type, never by square.
/// </summary>
public static class MateDemo
{
    static string Name(PieceType t) => t switch
    {
        PieceType.Pawn => "pawn", PieceType.Knight => "knight", PieceType.Bishop => "bishop",
        PieceType.Rook => "rook", PieceType.Queen => "queen", PieceType.King => "king", _ => "piece",
    };

    /// <param name="owner">Possessive for a side: "your" / "their", or "White's" / "Black's" in a two-player game.</param>
    public static DemoScript Build(GameState mated, Escape e, Func<Side, string> owner)
    {
        Side me = mated.ToMove, them = me.Opponent();
        string myKing = owner(me) + " king";
        string Piece(int sq) { var p = mated.Cells[sq]; return owner(p.Side) + " " + Name(p.Type); }
        string checker = e.CheckerSquare >= 0 ? Piece(e.CheckerSquare) : owner(them) + " attacker";
        string mover = e.From >= 0 && e.MoverType != PieceType.None ? owner(me) + " " + Name(e.MoverType) : "";
        string attacker = e.AttackerSquare >= 0 ? owner(them) + " " + Name(e.AttackerType) : "";

        var script = new DemoScript { Cols = 8, EndPauseMs = 2600 };
        string K(int sq) => Sq.Name(sq).ToLowerInvariant();

        script.Setup = c =>
        {
            var involved = new HashSet<int> { e.KingSquare >= 0 ? Geometry.FindKing(mated, me) : -1, e.From, e.To, e.CheckerSquare, e.AttackerSquare };
            involved.Add(Geometry.FindKing(mated, me));
            for (int sq = 0; sq < 64; sq++)
            {
                var p = mated.Cells[sq];
                if (!p.IsEmpty) c.PlaceReal(sq, p, !involved.Contains(sq));
                if (mated.Boulders[sq]) c.Boulder(K(sq), true);
            }
        };

        int king = Geometry.FindKing(mated, me);
        void Show(int ms, Action<DemoCtx> act) => script.Then(ms, act);

        switch (e.Kind)
        {
            case EscapeKind.KingCannotTakePoison:
                script.Setup += c => c.Aim(K(king), K(e.To)).Say($"{myKing} is in check. Could it just take {checker}?");
                Show(1500, c => c.Fx("ring", K(e.To), radius: 1, css: "boom").Say($"No! A king can't take a poisoned piece."));
                break;

            case EscapeKind.CannotTakeArmored:
                script.Setup += c => c.Aim(K(e.To)).Say($"{checker} is giving check, and it's armored.");
                Show(1500, c => c.Fx("ring", K(e.To), radius: 1, css: "boom").Say("Armored pieces can't be taken."));
                break;

            case EscapeKind.Frozen:
                script.Setup += c => c.Aim(K(e.From), K(e.To)).Say($"{mover} could take {checker}...");
                Show(1500, c => c.Fx("ring", K(e.From), radius: 1, css: "boom").Say($"...but it's frozen and can't move."));
                break;

            default:
                bool capture = e.Kind == EscapeKind.Capture && mated.Cells[e.To].Side == them;
                bool kingStep = e.Kind == EscapeKind.KingStep;
                string what = kingStep ? $"{myKing} steps away?" : capture ? $"{mover} takes {checker}?" : $"{mover} moves?";
                script.Setup += c => c.Aim(K(e.From)).Say($"{myKing} is in check. What if {what}");
                Show(1300, c => c.Aim(K(e.From), K(e.To)));
                Show(1000, c =>
                {
                    c.Aim();
                    if (capture) c.Kill(K(e.To));
                    c.Move(K(e.From), K(e.To));
                    bool poison = false;
                    foreach (var ev in e.TryEvents) if (ev.Kind == EventKind.PoisonTriggered) poison = true;
                    if (poison) { c.Kill(K(e.To), true); c.Fx("burst", K(e.To), css: "boom"); c.Say("The poison kills it too!"); }
                    else c.Say(capture ? "It takes it..." : "It moves...");
                });
                if (e.AttackerSquare >= 0 && e.KingSquare >= 0)
                {
                    Show(1200, c => c.Aim(K(e.AttackerSquare), K(e.KingSquare)).Say($"{attacker} can now take {myKing}!"));
                    Show(900, c => { c.Aim().Move(K(e.AttackerSquare), K(e.KingSquare)); });
                    Show(300, c => { c.Kill(K(e.KingSquare), true).Fx("burst", K(e.KingSquare), css: "boom").Say("So that move is not allowed."); });
                }
                else
                {
                    Show(1200, c => c.Say($"{myKing} would still be in check."));
                }
                break;
        }
        return script;
    }
}
