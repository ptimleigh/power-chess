// Native ad bridge. The build script injects this ahead of js/ads.js, so the game talks to AdMob through window.pcads.
// Ad unit ids come from window.PCADS_CONFIG, which the build script writes from the ADMOB_* environment variables.
// Without them the bridge uses Google's public TEST ad units, so a build can never show real ads by accident.
(function () {
    var Cap = window.Capacitor;
    if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;
    var AdMob = Cap.Plugins && Cap.Plugins.AdMob;
    if (!AdMob) return;

    var cfg = window.PCADS_CONFIG || {};
    var TEST_INTERSTITIAL = 'ca-app-pub-3940256099942544/1033173712';
    var TEST_REWARDED = 'ca-app-pub-3940256099942544/5224354917';
    var INTERSTITIAL_ID = cfg.interstitial || TEST_INTERSTITIAL;
    var REWARDED_ID = cfg.rewarded || TEST_REWARDED;
    var TESTING = !(cfg.interstitial && cfg.rewarded);

    var ready = false;
    var initPromise = (async function () {
        try {
            // Ask for consent first (UMP). Ads only start once the user has answered, or when no form is required.
            try {
                var info = await AdMob.requestConsentInfo();
                if (info && info.isConsentFormAvailable && info.status === 'REQUIRED') await AdMob.showConsentForm();
            } catch (e) { /* no form available: carry on */ }
            await AdMob.initialize({ initializeForTesting: TESTING });
            ready = true;
        } catch (e) { ready = false; }
    })();

    function withTimeout(promise, ms) {
        return new Promise(function (resolve) {
            var done = false;
            var t = setTimeout(function () { if (!done) { done = true; resolve(false); } }, ms);
            promise.then(function (v) { if (!done) { done = true; clearTimeout(t); resolve(v); } },
                function () { if (!done) { done = true; clearTimeout(t); resolve(false); } });
        });
    }

    window.pcads = {
        isAvailable: async function () { await initPromise; return ready; },
        // Lets the privacy link in the help sheet reopen the consent choices.
        privacyOptions: async function () {
            try { await AdMob.showPrivacyOptionsForm(); return true; } catch (e) { return false; }
        },
        interstitial: function () {
            return withTimeout((async function () {
                await initPromise;
                if (!ready) return false;
                await AdMob.prepareInterstitial({ adId: INTERSTITIAL_ID, isTesting: TESTING });
                await AdMob.showInterstitial();
                return true;
            })(), 15000);
        },
        rewarded: function () {
            return withTimeout((async function () {
                await initPromise;
                if (!ready) return false;
                var earned = false;
                var handle = await AdMob.addListener('onRewardedVideoAdReward', function () { earned = true; });
                try {
                    await AdMob.prepareRewardVideoAd({ adId: REWARDED_ID, isTesting: TESTING });
                    await AdMob.showRewardVideoAd();
                } finally { try { handle.remove(); } catch (e) { } }
                return earned;
            })(), 60000);
        }
    };
})();
