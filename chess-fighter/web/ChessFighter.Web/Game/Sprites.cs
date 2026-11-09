using ChessFighter.Core;

namespace ChessFighter.Web.Game;

/// <summary>
/// 12-wide pixel-art sprites for the chess pieces. Rows are bottom-aligned inside a 12x12 grid.
/// Legend: '.' empty, 'o' outline, 'b' body, 'h' highlight, 's' shade, 'e' eye/gem accent.
/// Colours per side live in fighter.css (.px-o, .px-b ... under .piece-svg.w / .piece-svg.b).
/// </summary>
public static class Sprites
{
    public const int Size = 12;

    static readonly string[] Pawn =
    {
        "....oooo....",
        "...obhbbo...",
        "...obbbbo...",
        "....obbo....",
        "..oobbbboo..",
        "....obbo....",
        "....obbo....",
        "...obbbbo...",
        "..obbbbbbo..",
        ".obbbbbbbbo.",
        ".oooooooooo.",
    };

    static readonly string[] Rook =
    {
        ".oo..oo..oo.",
        ".obo.obo.obo",
        ".obbbbbbbbo.",
        "..obbbbbbo..",
        "...obhbbo...",
        "...obhbbo...",
        "...obhbbo...",
        "...obbbbo...",
        "..obbbbbbo..",
        ".obbbbbbbbo.",
        ".oooooooooo.",
    };

    static readonly string[] Knight =
    {
        "...o..oo....",
        "..obooboo...",
        ".obhbbbbbo..",
        "obebbbbbbbo.",
        "obbbbbbbbbo.",
        ".oooobbbbbo.",
        "....obbbbbo.",
        "...obbbbbbo.",
        "..obbbbbbbo.",
        "..obbbbbbbo.",
        ".obbbbbbbbbo",
        ".ooooooooooo",
    };

    static readonly string[] Bishop =
    {
        ".....oo.....",
        "....obbo....",
        "...obhbbo...",
        "...obbsbo...",
        "...obsbbo...",
        "....obbo....",
        "...oobboo...",
        "....obbo....",
        "...obbbbo...",
        "..obbbbbbo..",
        "..oooooooo..",
    };

    static readonly string[] Queen =
    {
        "o..o.oo.o..o",
        "obo.obbo.obo",
        "obbobbbbobbo",
        ".obbbhbbbbo.",
        "..obbbbbbo..",
        "...obebbo...",
        "....obbo....",
        "...obbbbo...",
        "..oobbbboo..",
        "..obbbbbbo..",
        ".obbbbbbbbo.",
        ".oooooooooo.",
    };

    static readonly string[] King =
    {
        ".....oo.....",
        "..ooobbooo..",
        "..obbbbbbo..",
        "..ooobbooo..",
        "....obbo....",
        "..oobbbboo..",
        ".obbhbbbbbo.",
        ".obbbbbbbbo.",
        "..obbbbbbo..",
        "...obbbbo...",
        "..obbbbbbo..",
        ".oooooooooo.",
    };

    public static string[] Rows(PieceType t)
    {
        switch (t)
        {
            case PieceType.Pawn: return Pawn;
            case PieceType.Rook: return Rook;
            case PieceType.Knight: return Knight;
            case PieceType.Bishop: return Bishop;
            case PieceType.Queen: return Queen;
            case PieceType.King: return King;
            default: return System.Array.Empty<string>();
        }
    }

    /// <summary>Horizontal runs of one colour, so a sprite is a few dozen rects instead of one per pixel.</summary>
    public static IEnumerable<(int X, int Y, int W, char C)> Runs(PieceType t)
    {
        var rows = Rows(t);
        int top = Size - rows.Length;
        for (int r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            int x = 0;
            while (x < row.Length)
            {
                char c = row[x];
                if (c == '.') { x++; continue; }
                int start = x;
                while (x < row.Length && row[x] == c) x++;
                yield return (start, top + r, x - start, c);
            }
        }
    }
}
