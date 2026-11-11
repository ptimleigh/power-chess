// The title screen's synthwave background: banded sky, a stepped sun, and a grid floor sliding toward you.
// It is drawn at a low resolution and scaled up, so it stays chunky like the rest of the game.
window.pcsynth = (function () {
    function start(id) {
        var cv = document.getElementById(id);
        if (!cv || cv.__synth) return;
        cv.__synth = true;
        var ctx = cv.getContext('2d');
        var reduce = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
        var bands = ['#14216e', '#1c2a86', '#2b3aa8', '#4a3fb0', '#7a3f9e', '#b84a8c', '#e0604a', '#ff7a4a', '#ffa44a', '#ffc15a'];
        function size() {
            var w = 200, h = Math.max(200, Math.round(200 * cv.clientHeight / Math.max(1, cv.clientWidth)));
            if (cv.width !== w || cv.height !== h) { cv.width = w; cv.height = h; }
        }
        function draw(t) {
            size();
            var W = cv.width, H = cv.height, hz = Math.round(H * .44);
            var bh = hz / bands.length;
            for (var i = 0; i < bands.length; i++) { ctx.fillStyle = bands[i]; ctx.fillRect(0, Math.floor(i * bh), W, Math.ceil(bh) + 1); }
            var R = Math.round(W * .5);
            for (var dy = 0; dy < R; dy += 4) {
                var hw = Math.sqrt(R * R - dy * dy);
                ctx.fillStyle = dy < R / 3 ? '#fff3a0' : dy < 2 * R / 3 ? '#ffb347' : '#ff8a3a';
                ctx.fillRect(Math.round(W / 2 - hw), hz - dy - 4, Math.round(2 * hw), 4);
            }
            // slits cut into the lower half of the sun, like a retro poster
            ctx.fillStyle = bands[Math.min(bands.length - 1, 8)];
            [14, 26, 40, 58, 76].forEach(function (d, k) { if (d < R) ctx.fillRect(Math.round(W / 2 - R), hz - d, 2 * R, 1 + (k > 1 ? 1 : 0)); });
            ctx.fillStyle = '#2a1632'; ctx.fillRect(0, hz, W, H - hz);
            ctx.fillStyle = '#12081c'; ctx.fillRect(0, hz + 2, W, H - hz);
            ctx.fillStyle = '#ff8ad8'; ctx.fillRect(0, hz, W, 1);
            ctx.strokeStyle = 'rgba(255,200,120,.3)'; ctx.lineWidth = 1;
            var off = reduce ? 0 : (t / 1100) % 1;
            for (var j = 0; j < 12; j++) {
                var k2 = (j + off) / 12;
                var y = Math.round(hz + 3 + Math.pow(k2, 2) * (H - hz));
                ctx.beginPath(); ctx.moveTo(0, y + .5); ctx.lineTo(W, y + .5); ctx.stroke();
            }
            for (var m = -9; m <= 9; m++) { ctx.beginPath(); ctx.moveTo(W / 2 + m * 3 + .5, hz + 3); ctx.lineTo(W / 2 + m * 42 + .5, H); ctx.stroke(); }
        }
        function frame(t) {
            if (!document.getElementById(id)) return;       // the menu closed: stop
            draw(t);
            if (!reduce) requestAnimationFrame(frame);
        }
        requestAnimationFrame(frame);
    }
    return { start: start };
})();
