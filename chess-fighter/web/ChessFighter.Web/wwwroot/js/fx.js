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
        draw: () => { tone(440, 0.3, 'triangle', 0.1); tone(440, 0.3, 'triangle', 0.1, null, 0.25); },
    };

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
        copy: function (text) { try { return navigator.clipboard.writeText(text).then(() => true, () => false); } catch (e) { return false; } },
        play: function (name) { try { const f = sounds[name]; if (f) f(); } catch (e) { } },
        setMuted: function (m) { muted = !!m; },
    };
})();
