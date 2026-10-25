using Microsoft.JSInterop;

namespace PowerChess.Web.Game.Platform;

/// <summary>Ads as the game sees them. The browser build has none; the native shell supplies them through <c>window.pcads</c>.</summary>
public interface IAdService
{
    Task<bool> IsAvailableAsync();
    /// <summary>Shows a full-screen ad between games. Returns false if nothing was shown. Never throws.</summary>
    Task<bool> ShowInterstitialAsync();
    /// <summary>Shows a rewarded ad. Returns true only if the player earned the reward. Never throws.</summary>
    Task<bool> ShowRewardedAsync();
}

public sealed class JsAdService : IAdService
{
    readonly IJSRuntime js;
    public JsAdService(IJSRuntime js) => this.js = js;

    public async Task<bool> IsAvailableAsync()
    {
        try { return await js.InvokeAsync<bool>("pcads.isAvailable"); } catch { return false; }
    }

    public async Task<bool> ShowInterstitialAsync()
    {
        try { return await js.InvokeAsync<bool>("pcads.interstitial"); } catch { return false; }
    }

    public async Task<bool> ShowRewardedAsync()
    {
        try { return await js.InvokeAsync<bool>("pcads.rewarded"); } catch { return false; }
    }
}
