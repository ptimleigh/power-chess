namespace PowerChess.Web.Game;

/// <summary>Which build a tester is running. The deploy workflows replace "dev" with the commit and date before publishing.</summary>
public static class BuildInfo
{
    public const string Label = "dev";
    public const string Url = "https://ptimleigh.github.io/power-chess/";
    public const string ApkUrl = "https://github.com/ptimleigh/power-chess/releases/tag/android-latest";
}
