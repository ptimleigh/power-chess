using Microsoft.AspNetCore.Components;

namespace ChessFighter.Web.Game;

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
            "hadouken" => "<g transform=\"rotate(-90 12 12)\"><path d=\"M12 2c1 4 5 6 5 11a5 5 0 0 1-10 0c0-2 1-3 2-4 0 2 1 3 2 3 0-4-1-6 1-10z\"/></g>",
            "checker" => "<ellipse cx=\"12\" cy=\"16.5\" rx=\"9.5\" ry=\"4.2\"/><ellipse cx=\"12\" cy=\"12.5\" rx=\"9.5\" ry=\"4.2\" opacity=\".8\"/><ellipse cx=\"12\" cy=\"8.5\" rx=\"9.5\" ry=\"4.2\"/><ellipse cx=\"12\" cy=\"8.5\" rx=\"5\" ry=\"2\" fill=\"none\" stroke=\"#000\" stroke-width=\"1.2\" opacity=\".5\"/>",
            "flag" => "<path d=\"M6 3v18\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\" stroke-linecap=\"round\"/><path d=\"M7 4h11l-3 4 3 4H7z\"/>",
            "menu" => "<path d=\"M4 6h16v2.6H4zM4 10.7h16v2.6H4zM4 15.4h16V18H4z\"/>",
            "hook" => "<path d=\"M12 2v12\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\"/><path d=\"M5 13a7 7 0 0 0 14 0\" stroke=\"currentColor\" stroke-width=\"2.8\" fill=\"none\" stroke-linecap=\"round\"/><path d=\"M2 14l3-4 3 4zM16 14l3-4 3 4z\"/><circle cx=\"12\" cy=\"3.2\" r=\"2\"/>",
            "bolt" => "<path d=\"M13 2L4 14h6l-1 8 9-12h-6z\"/>",
            "card" => "<rect x=\"4\" y=\"2\" width=\"16\" height=\"20\" rx=\"2\"/><path d=\"M12 7l1.8 3.6 4 .6-2.9 2.8.7 4L12 16l-3.6 2 .7-4-2.9-2.8 4-.6z\" fill=\"#10061f\"/>",
            "ban" => "<circle cx=\"12\" cy=\"12\" r=\"8\" stroke=\"currentColor\" stroke-width=\"3\" fill=\"none\"/><path d=\"M6 18L18 6\" stroke=\"currentColor\" stroke-width=\"3\"/>",
            "hammer" => "<rect x=\"3\" y=\"3\" width=\"12\" height=\"7\" rx=\"1\"/><path d=\"M12 10l8 10\" stroke=\"currentColor\" stroke-width=\"3.4\" stroke-linecap=\"round\"/>",
            "hand" => "<path d=\"M8 22v-9L5 9l2-1 2 3V3h2.4v7h1.2V4h2.4v7h1V6h2.4v10c0 3-2 6-6 6z\"/>",
            "plus2" => "<text x=\"12\" y=\"17\" font-size=\"14\" font-weight=\"900\" text-anchor=\"middle\" font-family=\"Arial,Helvetica,sans-serif\">+2</text>",
            "plus4" => "<text x=\"12\" y=\"17\" font-size=\"14\" font-weight=\"900\" text-anchor=\"middle\" font-family=\"Arial,Helvetica,sans-serif\">+4</text>",
            "rock" => "<polygon points=\"3,18 5,9 11,4 18,7 21,15 14,21\"/>",
            "steal" => "<path d=\"M4 8h13l-3-3M20 16H7l3 3\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>",
            "dice" => "<rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"4\"/><circle cx=\"8\" cy=\"8\" r=\"1.8\" fill=\"#10061f\"/><circle cx=\"16\" cy=\"8\" r=\"1.8\" fill=\"#10061f\"/><circle cx=\"12\" cy=\"12\" r=\"1.8\" fill=\"#10061f\"/><circle cx=\"8\" cy=\"16\" r=\"1.8\" fill=\"#10061f\"/><circle cx=\"16\" cy=\"16\" r=\"1.8\" fill=\"#10061f\"/>",
            "rewind" => "<path d=\"M12 5a7 7 0 1 1-6.6 9.3\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\" stroke-linecap=\"round\"/><path d=\"M3 4v6h6z\"/>",
            "first" => "<path d=\"M5 5h2.5v14H5zM19 5v14L9 12z\"/>",
            "prev" => "<path d=\"M17 5v14L6 12z\"/>",
            "next" => "<path d=\"M7 5v14l11-7z\"/>",
            "last" => "<path d=\"M16.5 5H19v14h-2.5zM5 5l10 7L5 19z\"/>",
            "history" => "<path d=\"M12 4a8 8 0 1 1-7.5 5.2\" stroke=\"currentColor\" stroke-width=\"2.2\" fill=\"none\" stroke-linecap=\"round\"/><path d=\"M3 3v6h6z\"/><path d=\"M12 8v4.5l3 2\" stroke=\"currentColor\" stroke-width=\"2.2\" fill=\"none\" stroke-linecap=\"round\"/>",
            "ghost" => "<path d=\"M12 2a7.5 7.5 0 0 0-7.5 7.5V21l2.8-2.2 2.4 2.2 2.3-2.2 2.4 2.2 2.8-2.2V9.5A7.5 7.5 0 0 0 12 2z\"/><circle cx=\"9.2\" cy=\"10\" r=\"1.7\" fill=\"#10061f\"/><circle cx=\"14.8\" cy=\"10\" r=\"1.7\" fill=\"#10061f\"/>",
            "swap" => "<path d=\"M7 4L3 8l4 4M3 8h10M17 20l4-4-4-4M21 16H11\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>",
            "portal" => "<ellipse cx=\"12\" cy=\"12\" rx=\"9\" ry=\"9\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\"/><ellipse cx=\"12\" cy=\"12\" rx=\"5\" ry=\"5\" stroke=\"currentColor\" stroke-width=\"2.4\" fill=\"none\"/><circle cx=\"12\" cy=\"12\" r=\"1.6\"/>",
            "x3" => "<text x=\"12\" y=\"17\" font-size=\"14\" font-weight=\"900\" text-anchor=\"middle\" font-family=\"Arial,Helvetica,sans-serif\">x3</text>",
            "lock" => "<rect x=\"5\" y=\"10\" width=\"14\" height=\"11\" rx=\"2\"/><path d=\"M8 10V7a4 4 0 0 1 8 0v3\" stroke=\"currentColor\" stroke-width=\"2.2\" fill=\"none\"/>",
            "crown" => "<path d=\"M3 19h18v2H3zM3 7l5 5 4-7 4 7 5-5-2 10H5z\"/>",
            "spark" => "<path d=\"M12 2l2.2 6.8L21 12l-6.8 2.2L12 22l-2.2-7.8L3 12l6.8-3.2z\"/>",
            "x2" => "<text x=\"12\" y=\"17\" font-size=\"14\" font-weight=\"900\" text-anchor=\"middle\" font-family=\"Arial,Helvetica,sans-serif\">x2</text>",
            _ => "<text x=\"12\" y=\"18\" font-size=\"18\" font-weight=\"900\" text-anchor=\"middle\" font-family=\"Arial,Helvetica,sans-serif\">?</text>",
        };
        return new MarkupString($"<svg viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\">{body}</svg>");
    }
}
