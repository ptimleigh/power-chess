// Tiny WebAudio synth and haptics so the prototype has game feel without any asset files.
window.pcfx = (function () {
    let ctx = null;
    let muted = false;

    function ensure() {
        if (!ctx) {
            const AC = window.AudioContext || window.webkitAudioContext;
            if (!AC) return null;
            ctx = new AC();
        }
        if (ctx.state === 'suspended') ctx.resume();
        return ctx;
    }

    function tone(freq, dur, type, vol, slideTo, delay) {
        const c = ensure();
        if (!c || muted) return;
        const t0 = c.currentTime + (delay || 0);
        const o = c.createOscillator();
        const g = c.createGain();
        o.type = type || 'sine';
        o.frequency.setValueAtTime(freq, t0);
        if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t0 + dur);
        g.gain.setValueAtTime(0.0001, t0);
        g.gain.exponentialRampToValueAtTime(vol || 0.15, t0 + 0.01);
        g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
        o.connect(g).connect(c.destination);
        o.start(t0);
        o.stop(t0 + dur + 0.02);
    }

    function noise(dur, vol, delay) {
        const c = ensure();
        if (!c || muted) return;
        const t0 = c.currentTime + (delay || 0);
        const len = Math.floor(c.sampleRate * dur);
        const buf = c.createBuffer(1, len, c.sampleRate);
        const d = buf.getChannelData(0);
        for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / len);
        const s = c.createBufferSource();
        const g = c.createGain();
        s.buffer = buf;
        g.gain.value = vol || 0.2;
        s.connect(g).connect(c.destination);
        s.start(t0);
    }

    function buzz(ms) {
        try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) { }
    }

    const sounds = {
        click: () => tone(520, 0.05, 'triangle', 0.08),
        move: () => { tone(300, 0.09, 'triangle', 0.12, 180); },
        capture: () => { noise(0.14, 0.22); tone(160, 0.16, 'square', 0.12, 70); buzz(25); },
        pickup: () => { tone(660, 0.09, 'sine', 0.14); tone(880, 0.09, 'sine', 0.14, null, 0.08); tone(1320, 0.16, 'sine', 0.14, null, 0.16); },
        spawn: () => { tone(900, 0.12, 'sine', 0.06, 1200); },
        fire: () => { noise(0.5, 0.25); tone(220, 0.5, 'sawtooth', 0.12, 700); buzz(60); },
        bomb: () => { noise(0.7, 0.4); tone(110, 0.6, 'sine', 0.3, 30); buzz([40, 30, 80]); },
        steal: () => { tone(400, 0.12, 'square', 0.1, 900); tone(900, 0.12, 'square', 0.1, 400, 0.12); },
        stun: () => { tone(1000, 0.08, 'square', 0.1); tone(700, 0.08, 'square', 0.1, null, 0.08); tone(1000, 0.08, 'square', 0.1, null, 0.16); },
        poison: () => { tone(300, 0.4, 'sawtooth', 0.1, 80); noise(0.2, 0.1); buzz(40); },
        slash: () => { noise(0.1, 0.28); tone(1600, 0.12, 'sawtooth', 0.09, 260); buzz(18); },
        warp: () => { tone(300, 0.35, 'sine', 0.12, 1400); tone(1400, 0.25, 'triangle', 0.08, 300, 0.15); },
        thud: () => { tone(90, 0.2, 'sine', 0.3, 50); },
        rewind: () => { tone(900, 0.4, 'triangle', 0.12, 220); tone(600, 0.4, 'sine', 0.08, 150, 0.05); },
        win: () => { [523, 659, 784, 1046].forEach((f, i) => tone(f, 0.22, 'triangle', 0.14, null, i * 0.12)); },
        lose: () => { [392, 330, 262, 196].forEach((f, i) => tone(f, 0.28, 'sawtooth', 0.09, null, i * 0.16)); },
        select: () => { [660, 880, 660, 880, 1320].forEach((f, i) => tone(f, 0.07, 'square', 0.09, null, i * 0.075)); tone(1320, 0.28, 'square', 0.08, 1760, 0.4); buzz(30); },
        blip: () => tone(880, 0.05, 'square', 0.07),
        draw: () => { tone(440, 0.3, 'triangle', 0.1); tone(440, 0.3, 'triangle', 0.1, null, 0.25); },
    };

    // ---------- your own sound effects: any name listed in audio/sfx/manifest.json is loaded from audio/sfx/<name>.mp3 and replaces the synth sound ----------
    const overrides = {};
    function loadOverrides() {
        fetch('audio/sfx/manifest.json').then(r => r.ok ? r.json() : []).then(names => {
            const c = ensure();
            if (!c || !Array.isArray(names)) return;
            names.forEach(n => fetch('audio/sfx/' + n + '.mp3').then(r => r.arrayBuffer())
                .then(b => new Promise((res, rej) => c.decodeAudioData(b, res, rej)))
                .then(buf => { overrides[n] = buf; }).catch(() => { }));
        }).catch(() => { });
    }
    function playOverride(name) {
        const buf = overrides[name];
        if (!buf || muted) return false;
        const c = ensure();
        if (!c) return false;
        const src = c.createBufferSource(), g = c.createGain();
        src.buffer = buf; g.gain.value = 0.9;
        src.connect(g).connect(c.destination);
        src.start();
        return true;
    }
    setTimeout(loadOverrides, 1500);

    // ---------- menu music: one looping track while the start menu is open ----------
    const MUSIC_URL = 'audio/menu.mp3';
    const MUSIC_VOL = 0.5;
    let musicBuf = null, musicLoading = false;
    let musicSrc = null, musicGain = null;
    let wantMenu = false;

    function musicLoad(c) {
        if (musicBuf || musicLoading) return;
        musicLoading = true;
        fetch(MUSIC_URL).then(r => r.arrayBuffer()).then(b => new Promise((res, rej) => c.decodeAudioData(b, res, rej)))
            .then(buf => { musicBuf = buf; musicLoading = false; musicSync(); })
            .catch(() => { musicLoading = false; });
    }

    function musicStop(fade) {
        if (!musicSrc) return;
        const src = musicSrc, g = musicGain;
        musicSrc = null; musicGain = null;
        try {
            const t = ctx.currentTime;
            g.gain.cancelScheduledValues(t);
            g.gain.setValueAtTime(g.gain.value, t);
            g.gain.linearRampToValueAtTime(0, t + fade);
            src.stop(t + fade + 0.05);
        } catch (e) { try { src.stop(); } catch (e2) { } }
    }

    // Start or stop to match what the screen wants: music plays only while the menu is open, the sound is on and the page is visible.
    function musicSync() {
        const on = wantMenu && !muted && !document.hidden;
        if (!on) { musicStop(wantMenu && !muted ? 0.2 : 0.8); return; }
        const c = ensure();
        if (!c) return;
        if (!musicBuf) { musicLoad(c); return; }
        if (musicSrc) return;
        const src = c.createBufferSource();
        const g = c.createGain();
        src.buffer = musicBuf;
        src.loop = true;
        g.gain.setValueAtTime(0, c.currentTime);
        g.gain.linearRampToValueAtTime(MUSIC_VOL, c.currentTime + 1.5);
        src.connect(g).connect(c.destination);
        src.start();
        musicSrc = src; musicGain = g;
    }

    // Browsers keep audio locked until the first tap: remember that the menu wants music and start on the first touch.
    function unlock() { if (wantMenu) musicSync(); }
    ['pointerdown', 'keydown', 'touchend'].forEach(ev => window.addEventListener(ev, unlock, { passive: true }));
    document.addEventListener('visibilitychange', () => { if (document.hidden) { musicStop(0.1); if (ctx && ctx.state === 'running') ctx.suspend(); } else { musicSync(); } });

    // The menu is a Blazor element; watch for it appearing and disappearing instead of wiring every place that opens or closes it.
    function watchMenu() {
        let queued = false;
        const check = () => {
            queued = false;
            const open = !!document.querySelector('.start-menu');
            if (open !== wantMenu) { wantMenu = open; musicSync(); }
        };
        new MutationObserver(() => { if (!queued) { queued = true; requestAnimationFrame(check); } })
            .observe(document.body, { childList: true, subtree: true });
        check();
    }
    if (document.body) watchMenu(); else document.addEventListener('DOMContentLoaded', watchMenu);

    return {
        today: function () { const d = new Date(); return Math.floor((Date.UTC(d.getFullYear(), d.getMonth(), d.getDate())) / 86400000); },
        // Share text through the native sheet, the Web Share API, or the clipboard. Resolves to shared | copied | cancelled | failed.
        shareText: async function (title, text, url) {
            try {
                const cap = window.Capacitor;
                if (cap && cap.isNativePlatform && cap.isNativePlatform() && cap.Plugins && cap.Plugins.Share) {
                    await cap.Plugins.Share.share({ title, text, url: url || undefined, dialogTitle: title });
                    return 'shared';
                }
                if (navigator.share) {
                    await navigator.share(url ? { title, text, url } : { title, text });
                    return 'shared';
                }
            } catch (e) {
                if (e && (e.name === 'AbortError' || /cancel/i.test(String(e.message || e)))) return 'cancelled';
            }
            try { await navigator.clipboard.writeText(url ? text + '\n' + url : text); return 'copied'; } catch (e) { return 'failed'; }
        },
        // Swipeable carousel: tells .NET which slide is showing once the scroll settles, and can scroll to one.
        watchCarousel: function (el, ref) {
            if (!el) return;
            let timer = 0, last = -1;
            const onScroll = () => {
                clearTimeout(timer);
                timer = setTimeout(() => {
                    const i = Math.round(el.scrollLeft / Math.max(1, el.clientWidth + 10));
                    if (i !== last) { last = i; ref.invokeMethodAsync('SlideChanged', i); }
                }, 80);
            };
            el.addEventListener('scroll', onScroll, { passive: true });
        },
        carouselTo: function (el, i) { if (el) el.scrollTo({ left: i * (el.clientWidth + 10), behavior: 'smooth' }); },
        copy: function (text) { try { return navigator.clipboard.writeText(text).then(() => true, () => false); } catch (e) { return false; } },
        play: function (name) { try { if (playOverride(name)) return; const f = sounds[name]; if (f) f(); } catch (e) { } },
        setMuted: function (m) { muted = !!m; musicSync(); },
        /** For tests and diagnostics: what the menu music is doing. */
        music: function () { return { want: wantMenu, playing: !!musicSrc, loaded: !!musicBuf, ctx: ctx ? ctx.state : 'none', muted: muted }; },
    };
})();
