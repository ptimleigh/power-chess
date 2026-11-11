// The browser's back button (Chrome on a phone): close what is open, then open the menu, and only then leave the page.
// The installed app has its own handler (backbutton.js), so this stays out of its way.
(function () {
    var Cap = window.Capacitor;
    if (Cap && Cap.isNativePlatform && Cap.isNativePlatform()) return;
    if (!window.history || !history.pushState) return;
    function visible(el) { return el && el.offsetParent !== null && !el.disabled; }
    function push() { try { history.pushState({ cf: 1 }, ''); } catch (e) { } }
    push();
    // Chrome on a phone skips history entries that were added before the person touched the page ("history manipulation" protection),
    // so the entry made at load can be skipped and Back would leave the site. Add one more on the first real tap, and again after
    // each handled Back. Extra entries are harmless: on the menu, Back keeps stepping back until the page is left.
    var needPush = true;
    function ensure() { if (needPush) { needPush = false; push(); } }
    ['click', 'touchend', 'keydown'].forEach(function (ev) { window.addEventListener(ev, ensure, { capture: true, passive: true }); });
    window.addEventListener('popstate', function () {
        needPush = true;
        var modal = document.querySelector('.modal');
        if (modal) {
            // a safe close button first, then the backdrop of a sheet that closes when tapped; never "Play again" and the like
            var closers = ['[data-action$="close"]', '[data-action="options-done"]', '[data-action="resign-no"]', '[data-action="spotlight-done"]', '[data-action="tutorial-ok"]', 'button.x'];
            for (var i = 0; i < closers.length; i++) {
                var b = document.querySelector('.modal ' + closers[i]) || (closers[i] === '[data-action="tutorial-ok"]' ? document.querySelector(closers[i]) : null);
                if (visible(b)) { b.click(); push(); return; }
            }
            modal.click();
            push();
            return;
        }
        var tut = document.querySelector('[data-action="tutorial-ok"]');
        if (visible(tut)) { tut.click(); push(); return; }
        if (document.querySelector('.start-menu')) { history.back(); return; }   // on the menu: back leaves, as people expect
        var menu = document.querySelector('[data-action="menu"]');
        if (visible(menu)) { menu.click(); push(); return; }
        history.back();
    });
})();
