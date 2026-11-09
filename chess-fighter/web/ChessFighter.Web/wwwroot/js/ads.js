// The browser build has no ads. The native shell replaces this object with one backed by AdMob (see app/native).
window.pcads = window.pcads || {
    isAvailable: () => false,
    interstitial: async () => false,
    rewarded: async () => false,
};
