// Android back button: close whatever is open (a sheet, the replay window, a tutorial), otherwise leave the app.
(function () {
    var Cap = window.Capacitor;
    if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;
    var App = Cap.Plugins && Cap.Plugins.App;
    if (!App || !App.addListener) return;
    App.addListener('backButton', function () {
        // the most specific thing first: a close button, then the backdrop of a sheet that closes on tap
        var closers = ['[data-action$="close"]', '[data-action="tutorial-ok"]', '[data-action="log-close"]', 'button.x', '.modal .btn.primary'];
        for (var i = 0; i < closers.length; i++) {
            var b = document.querySelector(closers[i]);
            if (b && b.offsetParent !== null && !b.disabled) { b.click(); return; }
        }
        var modal = document.querySelector('.modal');
        if (modal) { modal.click(); return; }
        App.exitApp();
    });
})();
