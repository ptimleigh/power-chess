// Native ad bridge. The build script injects this ahead of js/ads.js, so the game talks to AdMob through window.pcads.
// Google's public TEST ad units are used until a real AdMob account exists; swap the ids below (and the app id in
// AndroidManifest.xml, patched by scripts/build-android.sh) for a release build.
(function () {
    var Cap = window.Capacitor;
    if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;
    var AdMob = Cap.Plugins && Cap.Plugins.AdMob;
    if (!AdMob) return;

    var INTERSTITIAL_ID = 'ca-app-pub-3940256099942544/1033173712';
    var REWARDED_ID = 'ca-app-pub-3940256099942544/5224354917';

    var ready = false;
    var initPromise = (async function () {
        try {
            try {
                var info = await AdMob.requestConsentInfo();
                if (info && info.isConsentFormAvailable && info.status === 'REQUIRED') await AdMob.showConsentForm();
            } catch (e) { /* consent is best effort in test builds */ }
            await AdMob.initialize({ initializeForTesting: true });
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
        interstitial: function () {
            return withTimeout((async function () {
                await initPromise;
                if (!ready) return false;
                await AdMob.prepareInterstitial({ adId: INTERSTITIAL_ID, isTesting: true });
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
                    await AdMob.prepareRewardVideoAd({ adId: REWARDED_ID, isTesting: true });
                    await AdMob.showRewardVideoAd();
                } finally { try { handle.remove(); } catch (e) { } }
                return earned;
            })(), 60000);
        }
    };
})();
