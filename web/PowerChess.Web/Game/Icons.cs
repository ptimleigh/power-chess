using Microsoft.AspNetCore.Components;

namespace PowerChess.Web.Game;

/// <summary>Tiny inline SVG icons (no font or emoji dependency, so they look the same everywhere).</summary>
public static class Icons
{
    public static MarkupString Svg(string key)
    {
        string body = key switch
        {
            "skull" => "<path d=\"M12 2a8 8 0 0 0-8 8c0 3 1.5 5 3 6v4h3v-2h4v2h3v-4c1.5-1 3-3 3-6a8 8 0 0 0-8-8z\"/><circle cx=\"9\" cy=\"11\" r=\"2\" fill=\"#10061f\"/><circle cx=\"15\" cy=\"11\" r=\"2\" fill=\"#10061f\"/>",
            "shield" => "<path d=\"M12 2l8 3v6c0 5-3.5 9-8 11-4.5-2-8-6-8-11V5z\"/>",
            "star" => "<path d=\"M12 2l3 7 7 .6-5.3 4.7 1.7 7.2L12 17.8 5.6 21.5l1.7-7.2L2 9.6 9 9z\"/>",
            "bomb" => "<circle cx=\"11\" cy=\"14\" r=\"7\"/><path d=\"M15 8l3-3\" stroke=\"currentColor\" stroke-width=\"2.2\" fill=\"none\"/><circle cx=\"19\" cy=\"4\" r=\"2\" fill=\"#ffd54a\"/>",
            "bigbomb" => "<circle cx=\"10.5\" cy=\"14.5\" r=\"8\"/><path d=\"M15 8l3-3\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\"/><circle cx=\"19\" cy=\"4\" r=\"2.4\" fill=\"#ffd54a\"/><circle cx=\"8\" cy=\"12\" r=\"1.6\" fill=\"#ffffff55\"/>",
            "fire" => "<path d=\"M12 2c1 4 5 6 5 11a5 5 0 0 1-10 0c0-2 1-3 2-4 0 2 1 3 2 3 0-4-1-6 1-10z\"/>",
            "rock" => "<polygon points=\"3,18 5,9 11,4 18,7 21,15 14,21\"/>",
            "steal" => "<path d=\"M4 8h13l-3-3M20 16H7l3 3\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>",
            "dice" => "<rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"4\"/><circle cx=\"8\" cy=\"8\" r=\"1.8\" fill=\"#10061f\"/><circle cx=\"16\" cy=\"8\" r=\"1.8\" fill=\"#10061f\"/><circle cx=\"12\" cy=\"12\" r=\"1.8\" fill=\"#10061f\"/><circle cx=\"8\" cy=\"16\" r=\"1.8\" fill=\"#10061f\"/><circle cx=\"16\" cy=\"16\" r=\"1.8\" fill=\"#10061f\"/>",
            "rewind" => "<path d=\"M12 5a7 7 0 1 1-6.6 9.3\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\" stroke-linecap=\"round\"/><path d=\"M3 4v6h6z\"/>",
            "first" => "<path d=\"M5 5h2.5v14H5zM19 5v14L9 12z\"/>",
            "prev" => "<path d=\"M17 5v14L6 12z\"/>",
            "next" => "<path d=\"M7 5v14l11-7z\"/>",
            "last" => "<path d=\"M16.5 5H19v14h-2.5zM5 5l10 7L5 19z\"/>",
            "history" => "<path d=\"M12 4a8 8 0 1 1-7.5 5.2\" stroke=\"currentColor\" stroke-width=\"2.2\" fill=\"none\" stroke-linecap=\"round\"/><path d=\"M3 3v6h6z\"/><path d=\"M12 8v4.5l3 2\" stroke=\"currentColor\" stroke-width=\"2.2\" fill=\"none\" stroke-linecap=\"round\"/>",
            "spark" => "<path d=\"M12 2l2.2 6.8L21 12l-6.8 2.2L12 22l-2.2-7.8L3 12l6.8-3.2z\"/>",
            "x2" => "<text x=\"12\" y=\"17\" font-size=\"14\" font-weight=\"900\" text-anchor=\"middle\" font-family=\"Arial,Helvetica,sans-serif\">x2</text>",
            _ => "<text x=\"12\" y=\"18\" font-size=\"18\" font-weight=\"900\" text-anchor=\"middle\" font-family=\"Arial,Helvetica,sans-serif\">?</text>",
        };
        return new MarkupString($"<svg viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\">{body}</svg>");
    }
}
