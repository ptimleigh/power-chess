// Android back button: close whatever is open (a sheet, the replay window, a tutorial), then open the menu, and only from the menu leave the app.
(function () {
    var Cap = window.Capacitor;
    if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;
    var App = Cap.Plugins && Cap.Plugins.App;
    if (!App || !App.addListener) return;
    function visible(el) { return el && el.offsetParent !== null && !el.disabled; }
    App.addListener('backButton', function () {
        var modal = document.querySelector('.modal');
        if (modal) {
            // a safe close button first, then the backdrop of a sheet that closes when tapped; never "Play again" and the like
            var closers = ['[data-action$="close"]', '[data-action="options-done"]', '[data-action="resign-no"]', '[data-action="spotlight-done"]', 'button.x'];
            for (var i = 0; i < closers.length; i++) {
                var b = document.querySelector('.modal ' + closers[i]);
                if (visible(b)) { b.click(); return; }
            }
            modal.click();
            return;
        }
        var tut = document.querySelector('[data-action="tutorial-ok"]');
        if (visible(tut)) { tut.click(); return; }
        if (document.querySelector('.start-menu')) { App.exitApp(); return; }   // on the menu: back leaves the app
        var menu = document.querySelector('[data-action="menu"]');
        if (visible(menu)) { menu.click(); return; }
        App.exitApp();
    });
})();
