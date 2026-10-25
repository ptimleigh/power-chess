// Replay clip renderer: plays a timeline built by ClipBuilder (C#) onto a 9:16 canvas with extra drama, and can record it
// (video + synth audio) for sharing. It never runs chess rules; it animates the engine events it is given, so a clip can't
// drift from the real game. The same code draws the in-app preview and the exported video.
window.pcclip = (function () {
    const W = 720, H = 1280;
    const BOARD = 648, SQ = BOARD / 8, BX = (W - BOARD) / 2, BY = 372;
    const PIECE_NAMES = ['', 'Pawn', 'Knight', 'Bishop', 'Rook', 'Queen', 'King'];

    // ---------------------------------------------------------------- piece art (same shapes as PieceSvg.razor)
    const rr = (x, y, w, h, r) => { const p = new Path2D(); p.roundRect(x, y, w, h, r); return p; };
    const circ = (cx, cy, r) => { const p = new Path2D(); p.arc(cx, cy, r, 0, Math.PI * 2); return p; };
    const ell = (cx, cy, rx, ry) => { const p = new Path2D(); p.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2); return p; };
    const d = s => new Path2D(s);
    const ART = {
        1: [['b', rr(24, 80, 52, 12, 6)], ['b', d('M34 82 C34 64 42 54 50 48 C58 54 66 64 66 82 Z')], ['b', rr(36, 44, 28, 8, 4)], ['b', circ(50, 30, 15)], ['s', circ(45, 25, 4)]],
        4: [['b', rr(22, 80, 56, 12, 6)], ['b', d('M30 82 L34 42 L66 42 L70 82 Z')], ['b', d('M26 42 L26 18 L38 18 L38 26 L45 26 L45 18 L55 18 L55 26 L62 26 L62 18 L74 18 L74 42 Z')], ['s', rr(38, 50, 8, 22, 3)]],
        2: [['b', rr(22, 80, 56, 12, 6)], ['b', d('M28 82 C27 62 34 50 44 44 C38 43 30 46 24 53 L18 46 C24 36 34 28 42 24 L42 12 L52 19 C68 19 80 34 78 56 C77 66 76 74 76 82 Z')], ['e', circ(55, 33, 3.5)], ['l', d('M58 24 C66 28 70 36 70 46')], ['l', d('M24 53 L32 55')]],
        3: [['b', rr(26, 80, 48, 12, 6)], ['b', d('M36 82 C37 68 42 62 50 58 C58 62 63 68 64 82 Z')], ['b', ell(50, 40, 16, 22)], ['b', circ(50, 13, 7)], ['l', d('M50 26 L58 38')], ['s', ell(44, 34, 4, 8)]],
        5: [['b', rr(22, 80, 56, 12, 6)], ['b', d('M28 82 L22 36 L38 54 L50 28 L62 54 L78 36 L72 82 Z')], ['b', circ(22, 32, 6)], ['b', circ(38, 22, 6)], ['b', circ(50, 16, 6.5)], ['b', circ(62, 22, 6)], ['b', circ(78, 32, 6)], ['s', rr(34, 62, 32, 6, 3)]],
        6: [['b', rr(22, 80, 56, 12, 6)], ['b', d('M30 82 C30 64 38 52 50 46 C62 52 70 64 70 82 Z')], ['b', rr(32, 42, 36, 9, 4.5)], ['b', rr(45, 8, 10, 32, 4)], ['b', rr(35, 18, 30, 10, 4)], ['s', rr(38, 64, 24, 6, 3)]],
    };

    let gradW = null, gradB = null;
    function grads(ctx) {
        if (!gradW) {
            gradW = ctx.createLinearGradient(0, 0, 0, 100);
            gradW.addColorStop(0, '#ffffff'); gradW.addColorStop(.55, '#cfe9ff'); gradW.addColorStop(1, '#7fb8ff');
            gradB = ctx.createLinearGradient(0, 0, 0, 100);
            gradB.addColorStop(0, '#6b3fd1'); gradB.addColorStop(.55, '#3a1b80'); gradB.addColorStop(1, '#1f0c4d');
        }
    }

    function drawPieceArt(ctx, type, side, size, tint) {
        grads(ctx);
        ctx.save();
        ctx.scale(size / 100, size / 100);
        const white = side === 0;
        for (const [kind, path] of ART[type]) {
            if (kind === 'b') {
                ctx.fillStyle = tint || (white ? gradW : gradB);
                ctx.fill(path);
                ctx.lineWidth = 3;
                ctx.strokeStyle = white ? '#6fb6ff' : '#ff4fd8';
                ctx.stroke(path);
            } else if (kind === 's') {
                ctx.fillStyle = 'rgba(255,255,255,.5)'; ctx.fill(path);
            } else if (kind === 'e') {
                ctx.fillStyle = white ? '#2a1f66' : '#38f9d7'; ctx.fill(path);
            } else {
                ctx.lineWidth = 2.5; ctx.strokeStyle = white ? '#6b3fd1' : '#ff9bf0'; ctx.stroke(path);
            }
        }
        ctx.restore();
    }

    // ---------------------------------------------------------------- sound (synth, routed to speakers and/or a recorder)
    function makeSound(record) {
        const AC = window.AudioContext || window.webkitAudioContext;
        if (!AC) return { dest: null, tone() { }, noise() { }, close() { } };
        const ctx = new AC();
        const master = ctx.createGain();
        master.gain.value = 0.9;
        let dest = null;
        if (record) { dest = ctx.createMediaStreamDestination(); master.connect(dest); }
        else master.connect(ctx.destination);
        const api = {
            ctx, dest,
            tone(freq, dur, type, vol, slideTo, delay) {
                const t0 = ctx.currentTime + (delay || 0);
                const o = ctx.createOscillator(), g = ctx.createGain();
                o.type = type || 'sine';
                o.frequency.setValueAtTime(freq, t0);
                if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t0 + dur);
                g.gain.setValueAtTime(0.0001, t0);
                g.gain.exponentialRampToValueAtTime(vol || 0.15, t0 + 0.01);
                g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
                o.connect(g).connect(master);
                o.start(t0); o.stop(t0 + dur + 0.05);
            },
            noise(dur, vol, delay, lp) {
                const t0 = ctx.currentTime + (delay || 0);
                const len = Math.floor(ctx.sampleRate * dur);
                const buf = ctx.createBuffer(1, len, ctx.sampleRate);
                const dd = buf.getChannelData(0);
                for (let i = 0; i < len; i++) dd[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 2);
                const s = ctx.createBufferSource(), g = ctx.createGain();
                s.buffer = buf; g.gain.value = vol || 0.2;
                if (lp) { const f = ctx.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = lp; s.connect(f).connect(g); } else s.connect(g);
                g.connect(master);
                s.start(t0);
            },
            close() { try { ctx.close(); } catch (e) { } },
        };
        return api;
    }

    // ---------------------------------------------------------------- the clip
    function Clip(canvas, data, opts) {
        opts = opts || {};
        const ctx = canvas.getContext('2d');
        canvas.width = W; canvas.height = H;
        const snd = opts.sound === false ? { tone() { }, noise() { }, close() { }, dest: null } : makeSound(!!opts.record);
        const names = data.powerups || {};

        let pieces = [];            // alive and dying piece objects
        let nextId = 1;
        let deco = { b: [], m: [], g: [] };
        let fx = [];
        let clock = 0;              // scaled game time (ms)
        let real = 0;               // real time (ms)
        let scale = 1;              // time scale (slow-mo)
        let freezeLeft = 0;         // real ms of hit-stop left
        let scaleTarget = 1, scaleRamp = 0;
        let shake = 0, flash = 0, flashColor = '255,255,255', vignette = 0, zoom = 1, zoomTarget = 1, camX = 0.5, camY = 0.5;
        let timers = [];            // {at, fn} in game time
        let hud = { moveNo: 0, label: '', mover: '', banner: '', bannerAt: -1e9 };
        let phase = 'intro';
        let finished = false, stopped = false, resolveDone = null;
        let startSnap = data.start;
        const total = data.beats.length;

        const sqXY = sq => ({ x: BX + (sq & 7) * SQ, y: BY + (7 - (sq >> 3)) * SQ });
        const sqC = sq => { const p = sqXY(sq); return { x: p.x + SQ / 2, y: p.y + SQ / 2 }; };
        const later = (ms, fn) => timers.push({ at: clock + ms, fn });
        const addFx = o => { o.t0 = clock; fx.push(o); return o; };
        const youSide = 'w';
        const who = m => data.vsAi ? (m === youSide ? 'YOU' : 'RIVAL') : (m === 'w' ? 'WHITE' : 'BLACK');

        function loadSnap(s) {
            pieces = s.p.map(a => ({ id: nextId++, sq: a[0], type: a[1], side: a[2], buffs: a[3], armor: a[4], stun: a[5], ghost: a[6], x: sqC(a[0]).x, y: sqC(a[0]).y, s: 1, a: 1, burn: 0, dying: false, lift: 0 }));
            deco = { b: s.b.slice(), m: s.m.slice(), g: s.g.slice() };
        }
        loadSnap(startSnap);

        const alive = () => pieces.filter(p => !p.dying);
        const at = sq => pieces.find(p => !p.dying && p.sq === sq);

        // ---- tweening helpers (game time)
        function tween(ms, fn, ease) {
            const t0 = clock;
            const o = { t0, ms, fn, ease: ease || (x => x), done: false };
            tweens.push(o);
            return o;
        }
        const tweens = [];
        const easeOut = x => 1 - Math.pow(1 - x, 3);
        const easeIn = x => x * x * x;
        const easeInOut = x => x < .5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2;

        function slide(p, toSq, ms, ease, lift) {
            const a = { x: p.x, y: p.y }, b = sqC(toSq);
            p.sq = toSq;
            tween(ms, k => { p.x = a.x + (b.x - a.x) * k; p.y = a.y + (b.y - a.y) * k - Math.sin(k * Math.PI) * (lift || 0); }, ease || easeInOut);
        }

        function kill(p, how) {
            p.dying = true; p.how = how || 'fade';
            const t0 = clock;
            tween(how === 'fire' ? 700 : 420, k => { p.a = 1 - k; p.s = how === 'fire' ? 1 + k * .1 : 1 - k * .5; p.burn = how === 'fire' ? Math.min(1, k * 2) : 0; });
            later(how === 'fire' ? 720 : 440, () => { pieces = pieces.filter(q => q !== p); });
        }

        function burst(x, y, color, n, speed) {
            const parts = [];
            for (let i = 0; i < (n || 22); i++) {
                const ang = Math.random() * Math.PI * 2, sp = (speed || 380) * (.35 + Math.random() * .8);
                parts.push({ vx: Math.cos(ang) * sp, vy: Math.sin(ang) * sp, r: 4 + Math.random() * 7 });
            }
            addFx({ kind: 'burst', x, y, dur: 700, color, parts });
        }
        function callout(text, x, y, color, big) { addFx({ kind: 'text', x, y, dur: big ? 1500 : 950, text, color: color || '#ffd24a', big: !!big }); }
        function doShake(n) { shake = Math.max(shake, n); }
        function doFlash(color, a) { flash = a || 1; flashColor = color || '255,255,255'; }
        function freeze(ms) { freezeLeft = Math.max(freezeLeft, ms); }
        function slowmo(sc, rampMs) { scale = sc; scaleTarget = 1; scaleRamp = rampMs; }

        // ---- sounds
        const S = {
            move: () => snd.tone(300, .09, 'triangle', .12, 180),
            slash: () => { snd.noise(.18, .25, 0, 6000); snd.tone(900, .12, 'sawtooth', .08, 200); },
            hit: (heavy) => { snd.noise(heavy ? .5 : .25, heavy ? .4 : .28, 0, 2200); snd.tone(heavy ? 90 : 140, heavy ? .5 : .25, 'square', heavy ? .22 : .14, 40); },
            fire: () => { snd.noise(.6, .28, 0, 3000); snd.tone(220, .6, 'sawtooth', .12, 800); },
            bomb: () => { snd.noise(.9, .45, 0, 1200); snd.tone(110, .8, 'sine', .35, 30); },
            ding: () => { snd.tone(660, .1, 'sine', .13); snd.tone(990, .16, 'sine', .13, null, .08); },
            riser: () => { snd.tone(80, 1.3, 'sawtooth', .07, 600); snd.noise(1.3, .08, 0, 4000); },
            boom: () => { snd.noise(1.1, .5, 0, 900); snd.tone(60, 1.2, 'sine', .45, 28); },
            chord: () => { [392, 494, 587, 784].forEach((f, i) => snd.tone(f, 1.4, 'triangle', .1, null, i * .09)); },
            lose: () => { [330, 262, 196].forEach((f, i) => snd.tone(f, 1.0, 'triangle', .1, null, i * .18)); },
        };

        // ---- beat scripting
        function applyBeat(beat, index, isLast) {
            let T = 0;                       // game-time cursor within the beat
            const evs = beat.ev;
            let mover = null;                // the piece that moved this beat
            hud.moveNo = index + 1; hud.label = beat.label; hud.mover = beat.mover;
            hud.bannerAt = clock;
            const dramatic = beat.ev.some(e => e.k === 'Captured' || e.k === 'Destroyed' || e.k === 'Converted' || e.k === 'FireballFired' || e.k === 'Detonated');

            for (let i = 0; i < evs.length; i++) {
                const e = evs[i];
                const next = evs[i + 1];
                switch (e.k) {
                    case 'Moved': {
                        const p = at(e.f);
                        if (!p) break;
                        mover = p;
                        const striking = next && next.k === 'Captured';
                        const dur = striking ? 240 : 430;
                        const t = T;
                        const finalStrike = striking && isLast && beat.mate;
                        later(t, () => {
                            S.move();
                            if (striking) addFx({ kind: 'streak', from: sqC(e.f), to: sqC(e.t), dur: 320 });
                            if (finalStrike) { slowmo(.4, 900); zoomTarget = 1.2; const c = sqC(e.t); camX = c.x / W; camY = (c.y - 0) / H; }
                            slide(p, e.t, striking ? (finalStrike ? 520 : 240) : 430, striking ? easeIn : easeInOut, striking ? 0 : 14);
                        });
                        T += striking ? (finalStrike ? 520 : 240) : 430;
                        break;
                    }
                    case 'Castled': {
                        const p = at(e.f);
                        if (p) { const t = T - 300; later(Math.max(0, t), () => slide(p, e.t, 380, easeInOut, 0)); }
                        break;
                    }
                    case 'Teleported': {
                        const p = mover;
                        const t = T;
                        later(t, () => {
                            if (!p) return;
                            addFx({ kind: 'ring', x: sqC(e.f).x, y: sqC(e.f).y, dur: 500, color: '#b46bff', max: SQ * 1.4 });
                            tween(180, k => { p.s = 1 - k; });
                            later(190, () => { p.sq = e.t; const c = sqC(e.t); p.x = c.x; p.y = c.y; addFx({ kind: 'ring', x: c.x, y: c.y, dur: 500, color: '#b46bff', max: SQ * 1.4 }); tween(200, k => { p.s = k; }); });
                        });
                        callout('WARP!', sqC(e.t).x, sqC(e.t).y - 40, '#c58bff');
                        T += 480;
                        break;
                    }
                    case 'Swapped': {
                        const a = at(e.f), b = at(e.t);
                        const t = T;
                        later(t, () => { if (a) slide(a, e.t, 450, easeInOut, 22); if (b) slide(b, e.f, 450, easeInOut, -22); addFx({ kind: 'ring', x: sqC(e.f).x, y: sqC(e.f).y, dur: 500, color: '#ff4fd8', max: SQ * 1.3 }); addFx({ kind: 'ring', x: sqC(e.t).x, y: sqC(e.t).y, dur: 500, color: '#ff4fd8', max: SQ * 1.3 }); });
                        callout('SWAP!', sqC(e.t).x, sqC(e.t).y - 40, '#ff7be5');
                        T += 480;
                        break;
                    }
                    case 'Captured': {
                        const victim = pieces.find(p => !p.dying && p.sq === e.t && p !== mover);
                        const heavy = victim && (victim.type === 5 || victim.type === 4);
                        const finalStrike = isLast && beat.mate;
                        const hit = victim ? ({ 1: 60, 2: 120, 3: 120, 4: 150, 5: 240 }[victim.type] || 100) : 80;
                        const t = T;
                        const c = sqC(e.t);
                        const word = victim ? ({ 2: 'STRIKE!', 3: 'STRIKE!', 4: 'CRUSHED!', 5: 'SLAIN!' }[victim.type] || '') : '';
                        later(t, () => {
                            freeze(finalStrike ? 520 : hit);
                            addFx({ kind: 'slash', x: c.x, y: c.y, dur: 380, flip: e.t % 2 === 0 });
                            S.slash();
                            if (finalStrike) { doFlash('255,255,255', .9); }
                        });
                        later(t + 40, () => {
                            if (victim) kill(victim, 'fade');
                            burst(c.x, c.y, e.s === 0 ? '#ffb04a' : '#ff4fd8', heavy ? 34 : 22, heavy ? 520 : 380);
                            doShake(heavy ? 22 : 12);
                            S.hit(heavy);
                            if (word && !finalStrike) callout(word, c.x, c.y - 70, heavy ? '#ff5a5a' : '#ffd24a', heavy);
                        });
                        T += 200 + hit * .5;
                        break;
                    }
                    case 'PoisonTriggered': {
                        const t = T; const c = sqC(e.f);
                        later(t, () => { if (mover) kill(mover, 'fade'); burst(c.x, c.y, '#7dff6a', 26, 360); callout('POISON!', c.x, c.y - 60, '#7dff6a', true); doShake(14); S.hit(false); });
                        T += 450;
                        break;
                    }
                    case 'FireballFired': {
                        const victims = [];
                        for (let j = i + 1; j < evs.length && evs[j].k === 'Destroyed'; j++) victims.push(evs[j].f);
                        const dist = (a, b) => Math.max(Math.abs((a & 7) - (b & 7)), Math.abs((a >> 3) - (b >> 3)));
                        let far = 1, target = e.f;
                        for (const v of victims) { const dd = dist(e.f, v); if (dd >= far) { far = dd; target = v; } }
                        const dur = Math.max(420, far * 130);
                        const t = T;
                        later(t, () => { addFx({ kind: 'fireball', from: sqC(e.f), to: sqC(target), dur }); S.fire(); callout('FIREBALL!', sqC(e.f).x, sqC(e.f).y - 70, '#ff9a3c', true); });
                        for (const v of victims) {
                            const delay = dur * dist(e.f, v) / far;
                            later(t + delay, () => { const p = at(v); if (p) { kill(p, 'fire'); } const c = sqC(v); burst(c.x, c.y, '#ff7a1c', 26, 420); doShake(12); S.hit(false); });
                            // mark so the generic Destroyed handler below skips them
                        }
                        evs.fireHandled = (evs.fireHandled || []).concat(victims);
                        T += dur + 650;
                        break;
                    }
                    case 'Destroyed': {
                        if (evs.fireHandled && evs.fireHandled.indexOf(e.f) >= 0) break;
                        const t = T; const c = sqC(e.f);
                        later(t, () => { const p = at(e.f); if (p) kill(p, 'fade'); burst(c.x, c.y, '#ffb04a', 26, 440); doShake(10); S.hit(false); });
                        break;
                    }
                    case 'Detonated': {
                        const r = e.u === 6 ? 2 : 1;      // BigBomb id; checked below via names for safety
                        const big = (names[e.u] || '').toLowerCase().indexOf('big') >= 0;
                        const t = T; const c = sqC(e.f);
                        later(t, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 700, color: '#ff8c3a', max: SQ * (big ? 3.1 : 1.9) }); doFlash('255,160,60', .8); doShake(big ? 30 : 20); S.bomb(); callout(big ? 'BIG BOMB!' : 'BOOM!', c.x, c.y - 90, '#ff9a3c', true); freeze(120); });
                        T += 320;
                        break;
                    }
                    case 'Converted': {
                        const t = T; const c = sqC(e.t);
                        later(t, () => { const p = at(e.t); if (p) p.side = e.s; addFx({ kind: 'beam', from: sqC(e.f), to: c, dur: 500, color: '#ff4fd8' }); addFx({ kind: 'ring', x: c.x, y: c.y, dur: 600, color: '#ff4fd8', max: SQ * 1.5 }); callout('STOLEN!', c.x, c.y - 60, '#ff7be5', true); S.ding(); });
                        T += 650;
                        break;
                    }
                    case 'StunStruck': {
                        const t = T; const c = sqC(e.t);
                        later(t, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 600, color: '#9fb4ff', max: SQ * 1.4 }); callout('STUNNED!', c.x, c.y - 60, '#bcd0ff', true); doShake(8); S.hit(false); });
                        T += 550;
                        break;
                    }
                    case 'Promoted': {
                        const t = T; const c = sqC(e.t);
                        later(t, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 700, color: '#ffd24a', max: SQ * 1.8 }); callout('PROMOTED!', c.x, c.y - 70, '#ffd24a', true); S.ding(); });
                        T += 450;
                        break;
                    }
                    case 'Transformed': case 'BuffGained': case 'PerkGained': {
                        const t = T; const c = sqC(e.f);
                        const nm = names[e.u] || '';
                        later(t, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 600, color: '#ffd24a', max: SQ * 1.5 }); if (nm) callout(nm.toUpperCase() + '!', c.x, c.y - 60, '#ffd24a', false); S.ding(); });
                        T += 350;
                        break;
                    }
                    case 'BoulderPlaced': case 'BoulderCleared': {
                        const t = T; const c = sqC(e.t);
                        later(t, () => { burst(c.x, c.y, '#9a8fb5', 18, 300); doShake(10); S.hit(false); });
                        T += 300;
                        break;
                    }
                    default: break;
                }
            }

            // end of beat: reconcile with the engine's position
            later(T + 40, () => reconcile(beat));
            const endAt = T + 40 + (dramatic ? 520 : 340);
            if (isLast && beat.mate) {
                later(T + 160, () => { S.boom(); doFlash('255,60,90', .75); doShake(30); vignette = 1; callout('CHECKMATE!', W / 2, BY + BOARD / 2, '#ff5a6e', true); });
                return endAt + 1350;
            }
            if (beat.check && !beat.mate) later(Math.max(0, T - 100), () => { callout('CHECK!', W / 2, BY - 40, '#ff5a5a', true); S.ding(); vignette = Math.max(vignette, .5); });
            return endAt;
        }

        function reconcile(beat) {
            const seen = new Set();
            for (const a of beat.after.p) {
                const p = pieces.find(q => !q.dying && q.sq === a[0] && !seen.has(q));
                if (p) {
                    seen.add(p);
                    p.type = a[1]; p.side = a[2]; p.buffs = a[3]; p.armor = a[4]; p.stun = a[5]; p.ghost = a[6];
                    p.s = 1; p.a = 1; const c = sqC(a[0]); p.x = c.x; p.y = c.y;
                } else {
                    const c = sqC(a[0]);
                    const np = { id: nextId++, sq: a[0], type: a[1], side: a[2], buffs: a[3], armor: a[4], stun: a[5], ghost: a[6], x: c.x, y: c.y, s: 0, a: 1, burn: 0, dying: false, lift: 0 };
                    pieces.push(np); seen.add(np);
                    tween(260, k => { np.s = k; }, easeOut);
                }
            }
            for (const p of pieces) if (!p.dying && !seen.has(p)) kill(p, 'fade');
            deco = { b: beat.after.b.slice(), m: beat.after.m.slice(), g: beat.after.g.slice() };
        }

        // ---- the script: intro, beats, outro
        let beatIndex = -1, beatEndAt = 0, outroAt = -1;
        const INTRO_MS = 1700;
        function startIntro() {
            phase = 'intro';
            S.riser();
            vignette = 0;
            // cold open on the dark board, title slams in
            later(0, () => addFx({ kind: 'title', dur: INTRO_MS }));
            later(INTRO_MS - 150, () => { doFlash('255,255,255', .5); S.hit(true); });
            later(INTRO_MS, () => { phase = 'beats'; nextBeat(); });
        }
        function nextBeat() {
            beatIndex++;
            if (beatIndex >= total) { startOutro(); return; }
            const isLast = beatIndex === total - 1;
            const endAt = applyBeat(data.beats[beatIndex], beatIndex, isLast);
            later(endAt, nextBeat);
        }
        function startOutro() {
            phase = 'outro';
            const win = data.result === 'w' || data.result === 'b';
            const youWon = data.vsAi ? data.result === 'w' : win;
            if (data.result === 'd') S.lose(); else if (youWon) S.chord(); else S.lose();
            outroAt = clock;
            zoomTarget = 1; camX = .5; camY = .5;
            addFx({ kind: 'outro', dur: 2600 });
            later(2900, () => finish());
        }
        function finish() {
            if (finished) return;
            finished = true;
            if (resolveDone) resolveDone(true);
        }

        // ---------------------------------------------------------------- drawing
        function drawBackground() {
            const g = ctx.createLinearGradient(0, 0, 0, H);
            g.addColorStop(0, '#1a1245'); g.addColorStop(.5, '#0d0b1e'); g.addColorStop(1, '#07041a');
            ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
            // slow neon grid lines
            ctx.save();
            ctx.globalAlpha = .12; ctx.strokeStyle = '#7a5cff'; ctx.lineWidth = 2;
            const off = (real / 40) % 80;
            for (let y = -80 + off; y < H; y += 80) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(W, y); ctx.stroke(); }
            ctx.restore();
        }

        function drawBoard() {
            // frame glow
            ctx.save();
            ctx.shadowColor = '#ff4fd8'; ctx.shadowBlur = 40;
            const fg = ctx.createLinearGradient(BX, BY, BX + BOARD, BY + BOARD);
            fg.addColorStop(0, '#ff4fd8'); fg.addColorStop(1, '#38f9d7');
            ctx.strokeStyle = fg; ctx.lineWidth = 8;
            ctx.beginPath(); ctx.roundRect(BX - 10, BY - 10, BOARD + 20, BOARD + 20, 22); ctx.stroke();
            ctx.restore();
            ctx.save();
            ctx.beginPath(); ctx.roundRect(BX, BY, BOARD, BOARD, 14); ctx.clip();
            for (let r = 0; r < 8; r++) for (let f = 0; f < 8; f++) {
                const light = (f + r) % 2 === 1;
                ctx.fillStyle = light ? '#3a2f78' : '#241a55';
                ctx.fillRect(BX + f * SQ, BY + (7 - r) * SQ, SQ, SQ);
            }
            // powerup squares
            const pulse = .5 + .5 * Math.sin(real / 260);
            for (const [sq, content] of deco.m) {
                const p = sqXY(sq);
                const kind = content ? (names[content] || '') : '';
                const perk = ['Steal', 'Stun', 'Boulder', 'Reroll', 'Rewind', 'Swap'].indexOf(kind) >= 0;
                const col = !content ? '170,120,255' : perk ? '56,249,215' : '255,200,70';
                ctx.fillStyle = `rgba(${col},${.35 + pulse * .2})`;
                ctx.fillRect(p.x, p.y, SQ, SQ);
                ctx.strokeStyle = `rgba(${col},.9)`; ctx.lineWidth = 3; ctx.strokeRect(p.x + 3, p.y + 3, SQ - 6, SQ - 6);
                ctx.fillStyle = '#fff'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
                ctx.font = `900 ${content ? 17 : 44}px system-ui, sans-serif`;
                ctx.fillText(content ? kind.replace('Become ', '').slice(0, 7).toUpperCase() : '?', p.x + SQ / 2, p.y + SQ / 2 + 2);
            }
            for (const sq of deco.g) {
                const c = sqC(sq);
                ctx.strokeStyle = `rgba(190,120,255,${.6 + pulse * .3})`; ctx.lineWidth = 5;
                ctx.beginPath(); ctx.arc(c.x, c.y, SQ * .32, 0, Math.PI * 2); ctx.stroke();
                ctx.beginPath(); ctx.arc(c.x, c.y, SQ * .18, 0, Math.PI * 2); ctx.stroke();
            }
            for (const sq of deco.b) {
                const p = sqXY(sq);
                ctx.fillStyle = '#6b6580'; ctx.beginPath(); ctx.roundRect(p.x + 10, p.y + 12, SQ - 20, SQ - 22, 16); ctx.fill();
                ctx.fillStyle = 'rgba(255,255,255,.18)'; ctx.beginPath(); ctx.roundRect(p.x + 18, p.y + 18, SQ - 46, 16, 8); ctx.fill();
            }
            ctx.restore();
        }

        function drawPiece(p) {
            const size = SQ * .96 * p.s;
            if (size <= 0 || p.a <= 0) return;
            ctx.save();
            ctx.globalAlpha = Math.max(0, Math.min(1, p.a)) * (p.buffs & 64 ? .72 : 1);
            ctx.translate(p.x, p.y + (p.buffs & 64 ? Math.sin(real / 300 + p.id) * 3 : 0));
            ctx.shadowColor = p.side === 0 ? 'rgba(120,190,255,.7)' : 'rgba(255,79,216,.7)';
            ctx.shadowBlur = 16;
            ctx.translate(-size / 2, -size / 2);
            const tint = p.burn > 0 ? '#ff8a1c' : (p.stun > 0 ? '#9aa0b8' : null);
            drawPieceArt(ctx, p.type, p.side, size, tint);
            ctx.restore();
            // badges
            ctx.save();
            ctx.globalAlpha = Math.max(0, Math.min(1, p.a));
            const bx = p.x + SQ * .26, by = p.y + SQ * .26;
            const tags = [];
            if (p.buffs & 1) tags.push(['x2', '#ffd24a', '#241a55']);
            if (p.buffs & 128) tags.push(['x3', '#ffd24a', '#241a55']);
            if (p.buffs & 2) tags.push(['P', '#7dff6a', '#10240a']);
            if (p.buffs & 32) tags.push(['F', '#ff9a3c', '#2a1000']);
            if (p.buffs & 8) tags.push(['B', '#ff6a3c', '#2a0a00']);
            if (p.buffs & 16) tags.push(['BB', '#ff3c3c', '#2a0000']);
            if (p.buffs & 64) tags.push(['G', '#c9b3ff', '#241a55']);
            if (p.stun > 0) tags.push(['Z', '#9fb4ff', '#10143a']);
            tags.forEach((t, i) => {
                const x = p.x - SQ * .32 + i * 22, y = p.y + SQ * .34;
                ctx.fillStyle = t[1]; ctx.beginPath(); ctx.arc(x, y, 11, 0, Math.PI * 2); ctx.fill();
                ctx.fillStyle = t[2]; ctx.font = '900 12px system-ui, sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
                ctx.fillText(t[0], x, y + 1);
            });
            if (p.buffs & 4) {
                ctx.strokeStyle = '#38f9d7'; ctx.fillStyle = '#38f9d7'; ctx.lineWidth = 4;
                ctx.beginPath(); ctx.arc(p.x, p.y, SQ * .42, 0, Math.PI * 2); ctx.stroke();
                const rot = real / 1600;
                for (let i = 0; i < 12; i++) {
                    const a = rot + i * Math.PI / 6;
                    ctx.beginPath();
                    ctx.moveTo(p.x + Math.cos(a - .12) * SQ * .42, p.y + Math.sin(a - .12) * SQ * .42);
                    ctx.lineTo(p.x + Math.cos(a) * SQ * .53, p.y + Math.sin(a) * SQ * .53);
                    ctx.lineTo(p.x + Math.cos(a + .12) * SQ * .42, p.y + Math.sin(a + .12) * SQ * .42);
                    ctx.fill();
                }
            }
            ctx.restore();
        }

        function drawFx(f) {
            const k = Math.min(1, (clock - f.t0) / f.dur);
            if (k < 0) return;
            ctx.save();
            switch (f.kind) {
                case 'burst': {
                    const t = k * f.dur / 1000;
                    for (const q of f.parts) {
                        ctx.globalAlpha = 1 - k; ctx.fillStyle = f.color;
                        ctx.beginPath(); ctx.arc(f.x + q.vx * t, f.y + q.vy * t + 200 * t * t, q.r * (1 - k * .6), 0, Math.PI * 2); ctx.fill();
                    }
                    ctx.globalAlpha = (1 - k) * .8; ctx.strokeStyle = f.color; ctx.lineWidth = 8 * (1 - k);
                    ctx.beginPath(); ctx.arc(f.x, f.y, SQ * .3 + k * SQ * .8, 0, Math.PI * 2); ctx.stroke();
                    break;
                }
                case 'ring': {
                    ctx.globalAlpha = 1 - k; ctx.strokeStyle = f.color; ctx.lineWidth = 10 * (1 - k) + 2;
                    ctx.beginPath(); ctx.arc(f.x, f.y, k * f.max, 0, Math.PI * 2); ctx.stroke();
                    break;
                }
                case 'slash': {
                    const ext = SQ * 1.5, sgn = f.flip ? -1 : 1;
                    ctx.globalAlpha = k < .6 ? 1 : 1 - (k - .6) / .4;
                    ctx.strokeStyle = '#fff'; ctx.lineWidth = 10 * (1 - k * .6); ctx.lineCap = 'round';
                    ctx.shadowColor = '#fff'; ctx.shadowBlur = 24;
                    const p = Math.min(1, k * 2.2);
                    ctx.beginPath();
                    ctx.moveTo(f.x - ext * sgn, f.y - ext);
                    ctx.lineTo(f.x - ext * sgn + 2 * ext * sgn * p, f.y - ext + 2 * ext * p);
                    ctx.stroke();
                    break;
                }
                case 'streak': {
                    ctx.globalAlpha = (1 - k) * .7; ctx.strokeStyle = '#fff'; ctx.lineWidth = 16 * (1 - k); ctx.lineCap = 'round';
                    ctx.beginPath(); ctx.moveTo(f.from.x, f.from.y); ctx.lineTo(f.from.x + (f.to.x - f.from.x) * Math.min(1, k * 3), f.from.y + (f.to.y - f.from.y) * Math.min(1, k * 3)); ctx.stroke();
                    break;
                }
                case 'beam': {
                    ctx.globalAlpha = 1 - k; ctx.strokeStyle = f.color; ctx.lineWidth = 12; ctx.shadowColor = f.color; ctx.shadowBlur = 24; ctx.lineCap = 'round';
                    ctx.beginPath(); ctx.moveTo(f.from.x, f.from.y); ctx.lineTo(f.from.x + (f.to.x - f.from.x) * Math.min(1, k * 2), f.from.y + (f.to.y - f.from.y) * Math.min(1, k * 2)); ctx.stroke();
                    break;
                }
                case 'fireball': {
                    const x = f.from.x + (f.to.x - f.from.x) * k, y = f.from.y + (f.to.y - f.from.y) * k;
                    for (let i = 8; i >= 1; i--) {
                        const kk = Math.max(0, k - i * .025);
                        const tx = f.from.x + (f.to.x - f.from.x) * kk, ty = f.from.y + (f.to.y - f.from.y) * kk;
                        ctx.globalAlpha = .5 - i * .05; ctx.fillStyle = '#ff7a1c';
                        ctx.beginPath(); ctx.arc(tx, ty, 26 - i * 2, 0, Math.PI * 2); ctx.fill();
                    }
                    ctx.globalAlpha = 1; ctx.shadowColor = '#ff9a3c'; ctx.shadowBlur = 36;
                    const g = ctx.createRadialGradient(x, y, 2, x, y, 30);
                    g.addColorStop(0, '#fff'); g.addColorStop(.4, '#ffd24a'); g.addColorStop(1, '#ff5a1c');
                    ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, 30, 0, Math.PI * 2); ctx.fill();
                    break;
                }
                case 'text': {
                    const rise = f.big ? 40 : 70;
                    const pop = k < .15 ? .6 + (k / .15) * .6 : 1.2 - Math.min(.2, (k - .15) * .4);
                    ctx.globalAlpha = k > .75 ? 1 - (k - .75) / .25 : 1;
                    ctx.translate(f.x, f.y - k * rise); ctx.scale(pop, pop); ctx.rotate(-0.06);
                    ctx.font = `900 ${f.big ? 64 : 46}px system-ui, sans-serif`;
                    ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.lineJoin = 'round';
                    ctx.lineWidth = 12; ctx.strokeStyle = '#150a3a'; ctx.strokeText(f.text, 0, 0);
                    ctx.fillStyle = f.color; ctx.shadowColor = f.color; ctx.shadowBlur = 22; ctx.fillText(f.text, 0, 0);
                    break;
                }
                case 'title': {
                    // dark card with the slammed-in title, fading to reveal the board
                    const a = k < .75 ? 1 : 1 - (k - .75) / .25;
                    ctx.globalAlpha = a * .86; ctx.fillStyle = '#07041a'; ctx.fillRect(0, 0, W, H);
                    const slam = k < .2 ? 2.4 - (k / .2) * 1.4 : 1;
                    ctx.globalAlpha = a;
                    ctx.translate(W / 2, H / 2 - 60); ctx.scale(slam, slam);
                    ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.lineJoin = 'round';
                    ctx.font = '900 70px system-ui, sans-serif';
                    ctx.fillStyle = '#ffd24a'; ctx.shadowColor = '#ffd24a'; ctx.shadowBlur = 30;
                    ctx.fillText(data.title || 'REPLAY', 0, 0);
                    ctx.shadowBlur = 0; ctx.font = '800 32px system-ui, sans-serif'; ctx.fillStyle = '#c9b3ff';
                    ctx.fillText('POWER CHESS', 0, 72);
                    break;
                }
                case 'outro': {
                    const a = Math.min(1, k * 4);
                    ctx.globalAlpha = a * .82; ctx.fillStyle = '#07041a'; ctx.fillRect(0, 0, W, H);
                    ctx.globalAlpha = a;
                    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
                    const win = data.result === 'w' || data.result === 'b';
                    const youWon = data.vsAi ? data.result === 'w' : win;
                    const head = data.result === 'd' ? 'DRAW' : data.vsAi ? (youWon ? 'VICTORY' : 'DEFEAT') : (data.result === 'w' ? 'WHITE WINS' : 'BLACK WINS');
                    const col = data.result === 'd' ? '#c9b3ff' : youWon ? '#ffd24a' : '#ff5a8a';
                    const pop = k < .2 ? .7 + k / .2 * .3 : 1;
                    ctx.save(); ctx.translate(W / 2, 470); ctx.scale(pop, pop);
                    ctx.font = '900 110px system-ui, sans-serif'; ctx.lineWidth = 16; ctx.lineJoin = 'round'; ctx.strokeStyle = '#150a3a'; ctx.strokeText(head, 0, 0);
                    ctx.fillStyle = col; ctx.shadowColor = col; ctx.shadowBlur = 36; ctx.fillText(head, 0, 0);
                    ctx.restore();
                    ctx.font = '800 38px system-ui, sans-serif'; ctx.fillStyle = '#e8e0ff';
                    if (data.reason) ctx.fillText(data.reason.toUpperCase(), W / 2, 570);
                    ctx.font = '900 62px system-ui, sans-serif'; ctx.fillStyle = '#ffffff'; ctx.shadowColor = '#ff4fd8'; ctx.shadowBlur = 24;
                    ctx.fillText('POWER CHESS', W / 2, 800);
                    ctx.shadowBlur = 0; ctx.font = '700 32px system-ui, sans-serif'; ctx.fillStyle = '#38f9d7';
                    ctx.fillText('Chess with powerup squares', W / 2, 860);
                    ctx.fillStyle = '#c9b3ff'; ctx.font = '700 28px system-ui, sans-serif';
                    ctx.fillText('ptimleigh.github.io/power-chess', W / 2, 1000);
                    break;
                }
            }
            ctx.restore();
        }

        function drawHud() {
            // top band: ticker and who is moving
            ctx.save();
            ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
            ctx.font = '900 40px system-ui, sans-serif'; ctx.fillStyle = '#ffd24a'; ctx.shadowColor = '#ffd24a'; ctx.shadowBlur = 16;
            ctx.fillText('POWER CHESS', W / 2, 78);
            ctx.shadowBlur = 0;
            ctx.font = '800 26px system-ui, sans-serif'; ctx.fillStyle = '#c9b3ff';
            ctx.fillText(data.title || '', W / 2, 128);
            // move pips
            const pipW = 34, gap = 10, tw = total * pipW + (total - 1) * gap;
            for (let i = 0; i < total; i++) {
                ctx.fillStyle = i < hud.moveNo ? '#38f9d7' : 'rgba(255,255,255,.18)';
                ctx.beginPath(); ctx.roundRect(W / 2 - tw / 2 + i * (pipW + gap), 160, pipW, 10, 5); ctx.fill();
            }
            ctx.font = '700 26px system-ui, sans-serif'; ctx.fillStyle = 'rgba(201,179,255,.85)';
            ctx.fillText('Play free: ptimleigh.github.io/power-chess', W / 2, 1190);
            if (phase === 'beats' && hud.moveNo > 0) {
                const age = Math.min(1, (clock - hud.bannerAt) / 260);
                ctx.globalAlpha = age;
                const side = hud.mover;
                ctx.font = '900 44px system-ui, sans-serif'; ctx.fillStyle = side === 'w' ? '#9fd0ff' : '#ff7be5';
                ctx.fillText(`MOVE ${hud.moveNo} · ${who(side)}`, W / 2, 230 + (1 - age) * 16);
                ctx.font = '700 30px system-ui, sans-serif'; ctx.fillStyle = '#e8e0ff';
                ctx.fillText(hud.label, W / 2, 284 + (1 - age) * 16);
            }
            ctx.restore();
        }

        function render() {
            drawBackground();
            ctx.save();
            // camera: shake + zoom around a focus point
            const sx = shake > 0 ? (Math.random() - .5) * shake : 0, sy = shake > 0 ? (Math.random() - .5) * shake : 0;
            ctx.translate(W / 2 + sx, H / 2 + sy);
            ctx.scale(zoom, zoom);
            const fx0 = (camX - .5) * W * (zoom - 1) / zoom * .9, fy0 = (camY - .5) * H * (zoom - 1) / zoom * .9;
            ctx.translate(-W / 2 - fx0, -H / 2 - fy0);
            drawBoard();
            for (const p of pieces.slice().sort((a, b) => a.y - b.y)) drawPiece(p);
            for (const f of fx) if (f.kind !== 'title' && f.kind !== 'outro' && f.kind !== 'text') drawFx(f);
            for (const f of fx) if (f.kind === 'text') drawFx(f);
            ctx.restore();
            // screen-space layers
            const live = fx.filter(f => f.kind === 'title' || f.kind === 'outro');
            drawHud();
            if (vignette > 0 || phase === 'beats') {
                const v = ctx.createRadialGradient(W / 2, H / 2, H * .3, W / 2, H / 2, H * .75);
                v.addColorStop(0, 'rgba(0,0,0,0)'); v.addColorStop(1, `rgba(${vignette > .3 ? '120,0,30' : '0,0,0'},${.45 + vignette * .4})`);
                ctx.fillStyle = v; ctx.fillRect(0, 0, W, H);
            }
            // film grain
            ctx.save(); ctx.globalAlpha = .05;
            for (let i = 0; i < 70; i++) { ctx.fillStyle = Math.random() > .5 ? '#fff' : '#000'; ctx.fillRect(Math.random() * W, Math.random() * H, 3, 3); }
            ctx.restore();
            for (const f of live) drawFx(f);
            if (flash > 0) { ctx.fillStyle = `rgba(${flashColor},${flash})`; ctx.fillRect(0, 0, W, H); }
        }

        // ---------------------------------------------------------------- main loop
        let last = 0, raf = 0;
        function frame(ts) {
            if (stopped) return;
            if (!last) last = ts;
            let dt = Math.min(50, ts - last); last = ts;
            real += dt;
            // hit-stop freezes game time but not the screen effects
            let gdt = dt * scale;
            if (freezeLeft > 0) { freezeLeft -= dt; gdt = 0; }
            if (scale !== scaleTarget) {
                scale += (scaleTarget - scale) * Math.min(1, dt / Math.max(1, scaleRamp));
                if (Math.abs(scale - scaleTarget) < .02) scale = scaleTarget;
            }
            clock += gdt;
            // timers
            for (let i = 0; i < timers.length;) { if (timers[i].at <= clock) { const t = timers.splice(i, 1)[0]; try { t.fn(); } catch (e) { console.error(e); } } else i++; }
            // tweens
            for (const t of tweens) {
                if (t.done) continue;
                const k = Math.max(0, Math.min(1, (clock - t.t0) / t.ms));
                t.fn(t.ease(k));
                if (k >= 1) t.done = true;
            }
            for (let i = tweens.length - 1; i >= 0; i--) if (tweens[i].done) tweens.splice(i, 1);
            fx = fx.filter(f => clock - f.t0 < f.dur + 30);
            shake *= Math.pow(.003, dt / 1000); if (shake < .3) shake = 0;
            if (flash > 0) flash = Math.max(0, flash - dt / 420);
            if (vignette > 0) vignette = Math.max(0, vignette - dt / 1500);
            zoom += (zoomTarget - zoom) * Math.min(1, dt / 260);
            render();
            raf = requestAnimationFrame(frame);
        }

        const api = {
            canvas, snd,
            play() {
                return new Promise(res => { resolveDone = res; startIntro(); raf = requestAnimationFrame(frame); });
            },
            stop() { stopped = true; cancelAnimationFrame(raf); snd.close(); if (resolveDone) resolveDone(false); },
        };
        return api;
    }

    // ---------------------------------------------------------------- public API (called from Blazor)
    let preview = null;
    let lastClip = null;   // { blob, mime, name }

    function parse(json) { return typeof json === 'string' ? JSON.parse(json) : json; }

    function start(canvasId, json, withSound) {
        stop();
        const canvas = document.getElementById(canvasId);
        if (!canvas) return false;
        preview = Clip(canvas, parse(json), { sound: withSound !== false });
        preview.play().then(() => { });
        return true;
    }

    function stop() {
        if (preview) { preview.stop(); preview = null; }
    }

    function pickMime() {
        const list = ['video/mp4;codecs=avc1.42E01E,mp4a.40.2', 'video/mp4;codecs=avc1', 'video/mp4', 'video/webm;codecs=vp9,opus', 'video/webm;codecs=vp8,opus', 'video/webm'];
        if (typeof MediaRecorder === 'undefined') return '';
        for (const m of list) { try { if (MediaRecorder.isTypeSupported(m)) return m; } catch (e) { } }
        return '';
    }

    // Plays the clip off-screen at real time while recording the canvas and the synth audio. Resolves with a summary.
    async function record(json) {
        stop();
        const mime = pickMime();
        if (!mime || !HTMLCanvasElement.prototype.captureStream) return { ok: false, error: 'This browser cannot record video.' };
        const canvas = document.createElement('canvas');
        canvas.style.cssText = 'position:fixed;left:-99999px;top:0;width:360px;height:640px';
        document.body.appendChild(canvas);
        const clip = Clip(canvas, parse(json), { sound: true, record: true });
        try {
            const stream = canvas.captureStream(30);
            if (clip.snd.dest) clip.snd.dest.stream.getAudioTracks().forEach(t => stream.addTrack(t));
            const rec = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: 5000000 });
            const chunks = [];
            rec.ondataavailable = e => { if (e.data && e.data.size) chunks.push(e.data); };
            const stopped = new Promise(res => { rec.onstop = res; });
            rec.start(250);
            await clip.play();
            await new Promise(r => setTimeout(r, 200));
            rec.stop();
            await stopped;
            const type = mime.split(';')[0];
            const blob = new Blob(chunks, { type });
            const ext = type.indexOf('mp4') >= 0 ? 'mp4' : 'webm';
            lastClip = { blob, mime: type, name: 'power-chess-clip.' + ext };
            return { ok: true, mime: type, size: blob.size, name: lastClip.name };
        } catch (e) {
            return { ok: false, error: String(e && e.message || e) };
        } finally {
            clip.stop();
            canvas.remove();
        }
    }

    function blobToBase64(blob) {
        return new Promise((resolve, reject) => {
            const r = new FileReader();
            r.onload = () => resolve(String(r.result).split(',')[1]);
            r.onerror = reject;
            r.readAsDataURL(blob);
        });
    }

    // Native app: write the file and open the system share sheet. Browser: Web Share with a file, else a download.
    async function share() {
        if (!lastClip) return 'none';
        const { blob, mime, name } = lastClip;
        const cap = window.Capacitor;
        try {
            if (cap && cap.isNativePlatform && cap.isNativePlatform() && cap.Plugins && cap.Plugins.Filesystem && cap.Plugins.Share) {
                const data = await blobToBase64(blob);
                const saved = await cap.Plugins.Filesystem.writeFile({ path: name, data, directory: 'CACHE' });
                await cap.Plugins.Share.share({ title: 'Power Chess', text: 'My Power Chess finish!', files: [saved.uri], dialogTitle: 'Share clip' });
                return 'shared';
            }
            const file = new File([blob], name, { type: mime });
            if (navigator.canShare && navigator.canShare({ files: [file] })) {
                await navigator.share({ files: [file], title: 'Power Chess', text: 'My Power Chess finish!' });
                return 'shared';
            }
        } catch (e) {
            if (e && e.name === 'AbortError') return 'cancelled';
        }
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = name; document.body.appendChild(a); a.click(); a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 10000);
        return 'downloaded';
    }

    function clipInfo() { return lastClip ? { mime: lastClip.mime, size: lastClip.blob.size, name: lastClip.name } : null; }

    return { start, stop, record, share, clipInfo, supported: () => !!pickMime() && !!HTMLCanvasElement.prototype.captureStream };
})();
