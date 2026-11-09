// Replay clip renderer: plays a timeline built by ClipBuilder (C#) onto a 9:16 canvas with extra drama, and can record it
// (video + synth audio) for sharing. It never runs chess rules; it animates the engine events it is given, so a clip can't
// drift from the real game. The same code draws the in-app preview and the exported video.
window.pcclip = (function () {
    const W = 720, H = 1280;
    const BOARD = 648, SQ = BOARD / 8, BX = (W - BOARD) / 2, BY = 372;
    const PIECE_NAMES = ['', 'Pawn', 'Knight', 'Bishop', 'Rook', 'Queen', 'King'];
    const PX = '"Press Start 2P", "Courier New", monospace';

    // ---------------------------------------------------------------- 16-bit look: palettes (same ramps as fighter.css)
    const PAL = {
        w: { k: '#0a1230', '1': '#2a3a8a', '2': '#4f6bc8', '3': '#86a3ee', '4': '#c4d6ff', '5': '#f4f8ff', e: '#2d6bff' },
        b: { k: '#1a0308', '1': '#5a0a1c', '2': '#9c1530', '3': '#d6283a', '4': '#ff6a55', '5': '#ffb8a0', e: '#ffe14a' },
        gold: { a: '#6b4400', b: '#b87a10', c: '#ffd23f', d: '#fff2a0', h: '#ffffff' },
    };
    const GOLD = '#ffd23f', RED = '#ff3b2f', CYAN = '#3df2ff', INK = '#0a0420';

    // flames: [light, mid, dark]
    const FLAME = { blue: ['#eaffff', '#3df2ff', '#2d6bff'], gold: ['#fffbd0', '#ffe23a', '#ffa400'], purple: ['#f6e1ff', '#b05cff', '#5a1fb0'] };

    // Make sure the pixel font is loaded before the first frame: the canvas silently falls back otherwise.
    function fontReady() {
        if (!document.fonts || !document.fonts.load) return Promise.resolve();
        return Promise.race([document.fonts.load('20px "Press Start 2P"'), new Promise(r => setTimeout(r, 1500))]).catch(() => { });
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
        let deco = { b: [], m: [], g: [], ml: 0, ma: -1 };
        let fx = [];
        let clock = 0;              // scaled game time (ms)
        let real = 0;               // real time (ms)
        let scale = 1;              // time scale (slow-mo)
        let freezeLeft = 0;         // real ms of hit-stop left
        let scaleTarget = 1, scaleRamp = 0;
        let shake = 0, flash = 0, flashColor = '255,255,255', vignette = 0, zoom = 1, zoomTarget = 1, camX = 0.5, camY = 0.5;
        let timers = [];            // {at, fn} in game time
        let hud = { moveNo: 0, label: '', mover: '', banner: '', bannerAt: -1e9, hpShow: [100, 100], hpTarget: [100, 100] };
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
            pieces = s.p.map(a => pieceFrom(a, sqC(a[0])));
            setDeco(s);
            if (s.hp) { hud.hpShow = s.hp.slice(); hud.hpTarget = s.hp.slice(); }
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
            let burnAll = false;             // a dragon burned this beat: destroyed pieces go up in flames
            const handled = [];              // squares whose destruction a fireball already animated
            hud.moveNo = index + 1; hud.label = beat.label; hud.mover = beat.mover;
            hud.bannerAt = clock;
            const BIG = ['Captured', 'Destroyed', 'Converted', 'FireballFired', 'Detonated', 'MissileStruck', 'DragonFire', 'ShivaStrike', 'HarpoonFired', 'ZapFired', 'BoardFlipped'];
            const dramatic = evs.some(e => BIG.indexOf(e.k) >= 0);
            const cardName = e => ((data.cards && data.cards[e.c]) || '').toUpperCase();
            const mid = { x: W / 2, y: BY + BOARD / 2 };

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
                            if (finalStrike) { slowmo(.4, 900); zoomTarget = 1.2; const c = sqC(e.t); camX = c.x / W; camY = c.y / H; }
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
                        later(T, () => {
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
                        later(T, () => { if (a) slide(a, e.t, 450, easeInOut, 22); if (b) slide(b, e.f, 450, easeInOut, -22); addFx({ kind: 'ring', x: sqC(e.f).x, y: sqC(e.f).y, dur: 500, color: RED, max: SQ * 1.3 }); addFx({ kind: 'ring', x: sqC(e.t).x, y: sqC(e.t).y, dur: 500, color: RED, max: SQ * 1.3 }); });
                        callout('SWAP!', sqC(e.t).x, sqC(e.t).y - 40, '#ff8a7a');
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
                            if (finalStrike) doFlash('255,255,255', .9);
                        });
                        later(t + 40, () => {
                            if (victim) kill(victim, 'fade');
                            burst(c.x, c.y, e.s === 0 ? '#ffd23f' : '#ff6a55', heavy ? 34 : 22, heavy ? 520 : 380);
                            doShake(heavy ? 22 : 12);
                            S.hit(heavy);
                            if (word && !finalStrike) callout(word, c.x, c.y - 70, heavy ? RED : GOLD, heavy);
                        });
                        T += 200 + hit * .5;
                        break;
                    }
                    case 'PoisonTriggered': {
                        const c = sqC(e.f);
                        later(T, () => { if (mover) kill(mover, 'fade'); burst(c.x, c.y, '#b05cff', 26, 360); callout('POISON!', c.x, c.y - 60, '#c98aff', true); doShake(14); S.hit(false); });
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
                        const shooter = at(e.f);
                        const gold = shooter && (shooter.tier || 1) >= 2;
                        later(t, () => { addFx({ kind: 'fireball', from: sqC(e.f), to: sqC(target), dur, gold }); S.fire(); callout(gold ? 'FIREBALL II!' : 'FIREBALL!', sqC(e.f).x, sqC(e.f).y - 70, gold ? GOLD : '#ff9a3c', true); });
                        for (const v of victims) {
                            const delay = dur * dist(e.f, v) / far;
                            later(t + delay, () => { const p = at(v); if (p) kill(p, 'fire'); const c = sqC(v); burst(c.x, c.y, '#ff7a1c', 26, 420); doShake(12); S.hit(false); });
                        }
                        handled.push(...victims);
                        T += dur + 650;
                        break;
                    }
                    case 'Destroyed': {
                        if (handled.indexOf(e.f) >= 0) break;
                        const t = T; const c = sqC(e.f);
                        later(t, () => { const p = at(e.f); if (p) kill(p, burnAll ? 'fire' : 'fade'); burst(c.x, c.y, burnAll ? '#ff7a1c' : '#ffd23f', 26, 440); doShake(10); S.hit(false); });
                        break;
                    }
                    case 'Detonated': {
                        const big = (names[e.u] || '').toLowerCase().indexOf('big') >= 0;
                        const c = sqC(e.f);
                        later(T, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 700, color: '#ff8c3a', max: SQ * (big ? 3.1 : 1.9) }); doFlash('255,160,60', .8); doShake(big ? 30 : 20); S.bomb(); callout(big ? 'BOMB II!' : 'BOOM!', c.x, c.y - 90, '#ff9a3c', true); freeze(120); });
                        T += 320;
                        break;
                    }
                    case 'Converted': {
                        const c = sqC(e.t);
                        later(T, () => { const p = at(e.t); if (p) p.side = e.s; addFx({ kind: 'beam', from: sqC(e.f), to: c, dur: 500, color: RED }); addFx({ kind: 'ring', x: c.x, y: c.y, dur: 600, color: RED, max: SQ * 1.5 }); callout('STOLEN!', c.x, c.y - 60, '#ff8a7a', true); S.ding(); });
                        T += 650;
                        break;
                    }
                    case 'StunStruck': {
                        const c = sqC(e.t);
                        later(T, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 600, color: '#9fb4ff', max: SQ * 1.4 }); callout('STUNNED!', c.x, c.y - 60, '#bcd0ff', true); doShake(8); S.hit(false); });
                        T += 550;
                        break;
                    }
                    case 'ZapFired': {
                        const a = sqC(e.f), b = sqC(e.t);
                        later(T, () => { addFx({ kind: 'zap', from: a, to: b, dur: 520, seed: e.t }); callout('ZAP!', a.x, a.y - 70, '#ffe23a', true); doFlash('255,240,120', .5); S.slash(); S.ding(); });
                        T += 480;
                        break;
                    }
                    case 'HarpoonFired': {
                        const a = sqC(e.f), b = sqC(e.t);
                        later(T, () => { addFx({ kind: 'rope', from: a, to: b, dur: 560 }); callout('HARPOON!', a.x, a.y - 70, GOLD, true); S.slash(); });
                        T += 420;
                        break;
                    }
                    case 'PulledIn': {
                        later(T, () => { const p = at(e.f); if (p) slide(p, e.t, 380, easeIn, 0); doShake(10); S.hit(false); });
                        T += 450;
                        break;
                    }
                    case 'Promoted': {
                        const c = sqC(e.t);
                        later(T, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 700, color: GOLD, max: SQ * 1.8 }); callout('PROMOTED!', c.x, c.y - 70, GOLD, true); S.ding(); });
                        T += 450;
                        break;
                    }
                    case 'Transformed': case 'BuffGained': case 'PerkGained': {
                        const c = sqC(e.f);
                        const nm = names[e.u] || '';
                        later(T, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 600, color: GOLD, max: SQ * 1.5 }); if (nm) callout(nm.toUpperCase() + '!', c.x, c.y - 60, GOLD, false); S.ding(); });
                        T += 350;
                        break;
                    }
                    case 'PowerUpgraded': {
                        const c = sqC(e.f);
                        const nm = (names[e.u] || '').toUpperCase();
                        later(T, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 700, color: GOLD, max: SQ * 2 }); burst(c.x, c.y, GOLD, 22, 360); callout((nm ? nm + ' ' : '') + 'II!', c.x, c.y - 70, GOLD, true); S.chord(); });
                        T += 450;
                        break;
                    }
                    case 'PowerMaxed': {
                        const c = sqC(e.f);
                        later(T, () => { callout('MAXED!', c.x, c.y - 50, '#c9b3ff', false); S.ding(); });
                        T += 250;
                        break;
                    }
                    case 'BoulderPlaced': case 'BoulderCleared': {
                        const c = sqC(e.t);
                        later(T, () => { burst(c.x, c.y, '#9a8fb5', 18, 300); doShake(10); S.hit(false); });
                        T += 300;
                        break;
                    }
                    case 'CardDrawn': {
                        const nm = cardName(e);
                        later(T, () => { callout('CARD: ' + nm, mid.x, BY + 50, GOLD, false); S.ding(); });
                        T += 350;
                        break;
                    }
                    case 'CardPlayed': {
                        const nm = cardName(e);
                        later(T, () => { addFx({ kind: 'banner', text: nm, dur: 900, color: GOLD }); S.ding(); });
                        T += 520;
                        break;
                    }
                    case 'CardCancelled': {
                        later(T, () => { callout('NUH-UH!', mid.x, mid.y, RED, true); doFlash('255,60,90', .6); doShake(18); S.hit(true); });
                        T += 650;
                        break;
                    }
                    case 'PieceRestored': {
                        const c = sqC(e.t);
                        later(T, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 700, color: GOLD, max: SQ * 1.8 }); burst(c.x, c.y, GOLD, 22, 360); callout('CAISSA!', c.x, c.y - 70, GOLD, true); S.chord(); });
                        T += 550;
                        break;
                    }
                    case 'PowersCleared': {
                        later(T, () => { doFlash('255,255,255', .7); doShake(22); callout('NO MORE POWERS', mid.x, mid.y, '#c9b3ff', true); S.boom(); });
                        T += 600;
                        break;
                    }
                    case 'BoardFlipped': case 'BoardRestarted': case 'TurnRewound': {
                        const label = e.k === 'BoardFlipped' ? 'THE WORLD TURNS!' : e.k === 'BoardRestarted' ? 'HIT RESTART!' : 'TAKE BACK!';
                        later(T, () => { doFlash('140,220,255', .9); doShake(24); callout(label, mid.x, mid.y, CYAN, true); S.chord(); });
                        later(T + 260, () => rebuild(beat));
                        T += 900;
                        break;
                    }
                    case 'DragonFire': {
                        burnAll = true;
                        const row = e.f >> 3;
                        later(T, () => { addFx({ kind: 'dragon', y: sqC(e.f).y, dur: 900 }); callout('DRAGON STRIKE!', mid.x, sqC(e.f).y - 60, '#ff9a3c', true); doShake(20); S.fire(); });
                        T += 520;
                        break;
                    }
                    case 'ShivaStrike': {
                        const c = sqC(e.f);
                        later(T, () => { addFx({ kind: 'drop', x: c.x, y: c.y, dur: 520 }); S.slash(); });
                        later(T + 420, () => { callout('SMITE!', c.x, c.y - 70, GOLD, true); doShake(12); });
                        T += 330;
                        break;
                    }
                    case 'MissileLaunched': {
                        later(T, () => { callout('MISSILE INCOMING!', mid.x, mid.y, RED, true); doFlash('255,60,60', .4); S.riser(); });
                        T += 650;
                        break;
                    }
                    case 'MissileStruck': {
                        const f0 = (e.f & 7), r0 = (e.f >> 3);
                        const c = { x: BX + (f0 + 2) * SQ, y: BY + (7 - r0 - 1.5) * SQ };
                        later(T, () => { addFx({ kind: 'ring', x: c.x, y: c.y, dur: 900, color: '#ff8c3a', max: SQ * 4.5 }); burst(c.x, c.y, '#ff7a1c', 40, 600); doFlash('255,170,70', .95); doShake(40); S.boom(); callout('MISSILE STRIKE!', c.x, c.y, RED, true); freeze(160); });
                        T += 800;
                        break;
                    }
                    default: break;
                }
            }

            // end of beat: reconcile with the engine's position
            later(T + 40, () => reconcile(beat));
            const endAt = T + 40 + (dramatic ? 520 : 340);
            if (isLast && beat.mate) {
                later(T + 160, () => { S.boom(); doFlash('255,60,90', .75); doShake(30); vignette = 1; callout('K.O.!', W / 2, BY + BOARD / 2, RED, true); });
                return endAt + 1350;
            }
            if (beat.check && !beat.mate) later(Math.max(0, T - 100), () => { callout('CHECK!', W / 2, BY - 40, RED, true); S.ding(); vignette = Math.max(vignette, .5); });
            return endAt;
        }

        function pieceFrom(a, c) {
            return { id: nextId++, sq: a[0], type: a[1], side: a[2], buffs: a[3], armor: a[4], stun: a[5], ghost: a[6], tier: a[7] || 0, x: c.x, y: c.y, s: 1, a: 1, burn: 0, dying: false, lift: 0 };
        }

        function setDeco(after) {
            deco = { b: after.b.slice(), m: after.m.slice(), g: after.g.slice(), ml: after.ml || 0, ma: after.ma == null ? -1 : after.ma };
            if (after.hp) hud.hpTarget = after.hp.slice();
        }

        function reconcile(beat) {
            const seen = new Set();
            for (const a of beat.after.p) {
                const p = pieces.find(q => !q.dying && q.sq === a[0] && !seen.has(q));
                if (p) {
                    seen.add(p);
                    p.type = a[1]; p.side = a[2]; p.buffs = a[3]; p.armor = a[4]; p.stun = a[5]; p.ghost = a[6]; p.tier = a[7] || 0;
                    p.s = 1; p.a = 1; const c = sqC(a[0]); p.x = c.x; p.y = c.y;
                } else {
                    const c = sqC(a[0]);
                    const np = pieceFrom(a, c); np.s = 0;
                    pieces.push(np); seen.add(np);
                    tween(260, k => { np.s = k; }, easeOut);
                }
            }
            for (const p of pieces) if (!p.dying && !seen.has(p)) kill(p, 'fade');
            setDeco(beat.after);
        }

        // Everything moved at once (flip, restart, take back): redraw the whole board and pop the pieces in.
        function rebuild(beat) {
            pieces = beat.after.p.map(a => { const o = pieceFrom(a, sqC(a[0])); o.s = 0; return o; });
            for (const o of pieces) tween(320, k => { o.s = k; }, easeOut);
            setDeco(beat.after);
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

        // ---------------------------------------------------------------- drawing (16-bit look)
        const spriteCache = {};
        // A piece sprite from the game's own 32x32 grids, drawn once per (type, side, tint) and scaled with no smoothing.
        function sprite(type, side, tint) {
            const key = type + ':' + side + ':' + (tint || '');
            if (spriteCache[key]) return spriteCache[key];
            const c = document.createElement('canvas'); c.width = 32; c.height = 32;
            const g = c.getContext('2d');
            const rows = (data.sprites && data.sprites[type]) || [];
            const top = 32 - rows.length;
            const pal = side === 0 ? PAL.w : PAL.b;
            for (let r = 0; r < rows.length; r++) {
                for (let x = 0; x < rows[r].length; x++) {
                    const ch = rows[r][x];
                    if (ch === '.') continue;
                    g.fillStyle = pal[ch] || PAL.gold[ch] || '#ff00ff';
                    g.fillRect(x, top + r, 1, 1);
                }
            }
            if (tint) { g.globalCompositeOperation = 'source-atop'; g.globalAlpha = .55; g.fillStyle = tint; g.fillRect(0, 0, 32, 32); }
            spriteCache[key] = c;
            return c;
        }

        const patCache = {};
        function hatch(a, b) {
            const key = a + b;
            if (patCache[key]) return patCache[key];
            const c = document.createElement('canvas'); c.width = c.height = 16;
            const g = c.getContext('2d');
            g.fillStyle = a; g.fillRect(0, 0, 16, 16);
            g.fillStyle = b;
            g.beginPath(); g.moveTo(0, 8); g.lineTo(8, 0); g.lineTo(16, 0); g.lineTo(0, 16); g.closePath(); g.fill();
            g.beginPath(); g.moveTo(8, 16); g.lineTo(16, 8); g.lineTo(16, 16); g.closePath(); g.fill();
            return patCache[key] = ctx.createPattern(c, 'repeat');
        }
        const stripe = () => hatch('#b8570a', GOLD);

        // Pixel text with a hard black outline and drop shadow.
        function ptext(text, x, y, size, fill, align) {
            ctx.save();
            ctx.font = `${size}px ${PX}`; ctx.textAlign = align || 'center'; ctx.textBaseline = 'middle';
            ctx.lineJoin = 'miter'; ctx.lineWidth = Math.max(4, size / 3); ctx.strokeStyle = INK; ctx.strokeText(text, x, y);
            ctx.fillStyle = INK; ctx.fillText(text, x + size / 8, y + size / 8);
            ctx.fillStyle = fill; ctx.fillText(text, x, y);
            ctx.restore();
        }

        // The arcade logo: three flat bands of colour inside a black outline.
        function logo(text, x, y, size) {
            ctx.save();
            ctx.font = `${size}px ${PX}`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
            ctx.lineJoin = 'miter'; ctx.lineWidth = size / 2.6; ctx.strokeStyle = INK; ctx.strokeText(text, x, y);
            ctx.fillStyle = INK; ctx.fillText(text, x + size / 10, y + size / 10);
            [['#fff6a8', 0, .3], [GOLD, .3, .6], ['#ff7a1f', .6, 1]].forEach(([col, a, b]) => {
                ctx.save();
                ctx.beginPath(); ctx.rect(x - W, y - size / 2 + a * size, 2 * W, (b - a) * size + 1); ctx.clip();
                ctx.fillStyle = col; ctx.fillText(text, x, y);
                ctx.restore();
            });
            ctx.restore();
        }

        function drawBackground() {
            const hz = Math.round(H * .6);
            const bands = ['#14216e', '#1c2a86', '#2b3aa8', '#4a3fb0', '#7a3f9e', '#b84a8c', '#e0604a', '#ff7a4a', '#ffa44a', '#ffc15a'];
            const bh = hz / bands.length;
            bands.forEach((c, i) => { ctx.fillStyle = c; ctx.fillRect(0, Math.floor(i * bh), W, Math.ceil(bh) + 1); });
            // a stepped sun on the horizon
            for (let dy = 0; dy < 150; dy += 10) {
                const hw = Math.sqrt(150 * 150 - dy * dy);
                ctx.fillStyle = dy < 50 ? '#fff3a0' : dy < 100 ? '#ffb347' : '#ff8a3a';
                ctx.fillRect(W / 2 - hw, hz - dy - 10, 2 * hw, 10);
            }
            // the floor, with lines that slide toward the viewer
            ctx.fillStyle = '#2a1632'; ctx.fillRect(0, hz, W, H - hz);
            ctx.fillStyle = '#12081c'; ctx.fillRect(0, hz + 6, W, H - hz);
            ctx.strokeStyle = 'rgba(255,200,120,.22)'; ctx.lineWidth = 3;
            const off = (real / 900) % 1;
            for (let i = 0; i < 12; i++) {
                const k = (i + off) / 12;
                const y = hz + 8 + Math.pow(k, 2) * (H - hz);
                ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(W, y); ctx.stroke();
            }
            for (let i = -8; i <= 8; i++) { ctx.beginPath(); ctx.moveTo(W / 2 + i * 8, hz + 8); ctx.lineTo(W / 2 + i * 110, H); ctx.stroke(); }
        }

        function drawBoard() {
            const fw = 16;
            ctx.fillStyle = '#000'; ctx.fillRect(BX - fw - 8, BY - fw - 8, BOARD + 2 * (fw + 8), BOARD + 2 * (fw + 8));
            ctx.fillStyle = stripe(); ctx.fillRect(BX - fw - 4, BY - fw - 4, BOARD + 2 * (fw + 4), BOARD + 2 * (fw + 4));
            ctx.fillStyle = '#000'; ctx.fillRect(BX - 4, BY - 4, BOARD + 8, BOARD + 8);
            for (let r = 0; r < 8; r++) for (let f = 0; f < 8; f++) {
                ctx.fillStyle = (f + r) % 2 === 1 ? '#7a6bb8' : '#3f3280';
                ctx.fillRect(BX + f * SQ, BY + (7 - r) * SQ, SQ, SQ);
            }
            // powerup squares: hatched, with a hard white edge and a short label
            const blink = Math.floor(real / 320) % 2 === 0;
            for (const [sq, content] of deco.m) {
                const p = sqXY(sq);
                ctx.fillStyle = content ? hatch('#e07a00', '#ffb400') : hatch('#4a2bb0', '#6a45e6');
                ctx.fillRect(p.x, p.y, SQ, SQ);
                ctx.lineWidth = 5; ctx.strokeStyle = '#000'; ctx.strokeRect(p.x + 3, p.y + 3, SQ - 6, SQ - 6);
                ctx.lineWidth = 3; ctx.strokeStyle = blink ? '#fff' : '#ffe9a8'; ctx.strokeRect(p.x + 3, p.y + 3, SQ - 6, SQ - 6);
                const label = content ? ((data.glyphs && data.glyphs[content]) || names[content] || '') : '?';
                ptext(label, p.x + SQ / 2, p.y + SQ / 2, label.length > 4 ? 12 : label.length > 2 ? 15 : content ? 24 : 38, '#fff');
            }
            for (const sq of deco.g) {
                const p = sqXY(sq);
                ctx.lineWidth = 6;
                ctx.strokeStyle = '#000'; ctx.strokeRect(p.x + 12, p.y + 12, SQ - 24, SQ - 24);
                ctx.strokeStyle = blink ? CYAN : '#b8fff4'; ctx.strokeRect(p.x + 14, p.y + 14, SQ - 28, SQ - 28);
                ctx.fillStyle = CYAN; ctx.fillRect(p.x + SQ / 2 - 8, p.y + SQ / 2 - 8, 16, 16);
            }
            for (const sq of deco.b) {
                const p = sqXY(sq);
                ctx.fillStyle = '#000'; ctx.fillRect(p.x + 8, p.y + 10, SQ - 16, SQ - 16);
                ctx.fillStyle = '#6b6580'; ctx.fillRect(p.x + 12, p.y + 14, SQ - 24, SQ - 24);
                ctx.fillStyle = '#9a93b5'; ctx.fillRect(p.x + 12, p.y + 14, SQ - 24, 10);
                ctx.fillStyle = '#4a4560'; ctx.fillRect(p.x + 12, p.y + SQ - 22, SQ - 24, 8);
            }
            // a missile in flight: the quadrant it will hit, with the countdown
            if (deco.ml > 0 && deco.ma >= 0) {
                const f0 = deco.ma & 7, r0 = deco.ma >> 3;
                const zx = BX + f0 * SQ, zy = BY + (7 - (r0 + 3)) * SQ;
                ctx.save();
                ctx.globalAlpha = .5; ctx.fillStyle = hatch('#7a0f22', '#ff2e4d'); ctx.fillRect(zx, zy, SQ * 4, SQ * 4);
                ctx.globalAlpha = 1; ctx.lineWidth = 6; ctx.strokeStyle = Math.floor(real / 250) % 2 ? '#fff' : RED; ctx.strokeRect(zx + 3, zy + 3, SQ * 4 - 6, SQ * 4 - 6);
                ctx.restore();
                ptext(String(deco.ml), zx + SQ * 2, zy + SQ * 2, 72, '#fff');
            }
        }

        function flames(x, y, cols, n, hMax, w) {
            const xs = n === 4 ? [-.9, -.3, .3, .9] : [-1, -.5, 0, .5, 1];
            xs.forEach((fx, i) => {
                const h = Math.round(hMax * (.65 + .35 * Math.sin(real / 110 + i * 1.9)) * (1 - Math.abs(fx) * .25));
                const px = Math.round(x + fx * SQ * .4 - w / 2), base = Math.round(y + SQ * .4);
                ctx.fillStyle = cols[2]; ctx.fillRect(px, base - h * .42, w, h * .42);
                ctx.fillStyle = cols[1]; ctx.fillRect(px + w * .1, base - h * .76, w * .8, h * .34);
                ctx.fillStyle = cols[0]; ctx.fillRect(px + w * .25, base - h, w * .5, h * .24);
            });
        }

        function drawAura(p, x, y) {
            if (p.buffs & 32) {
                const gold = (p.tier || 1) >= 2;
                flames(x, y, gold ? FLAME.gold : FLAME.blue, gold ? 5 : 4, gold ? SQ * 1.15 : SQ * .85, gold ? 16 : 14);
            }
            if (p.buffs & 2) flames(x, y, FLAME.purple, 5, SQ * .8, 13);
        }

        function badge(text, x, y, bg, fg) {
            const w = text.length * 10 + 8;
            ctx.fillStyle = '#000'; ctx.fillRect(x - 2, y - 2, w + 4, 22);
            ctx.fillStyle = bg; ctx.fillRect(x, y, w, 18);
            ctx.font = `10px ${PX}`; ctx.textAlign = 'left'; ctx.textBaseline = 'middle'; ctx.fillStyle = fg;
            ctx.fillText(text, x + 4, y + 10);
        }

        function drawPiece(p) {
            const size = Math.round(SQ * p.s);
            if (size <= 0 || p.a <= 0) return;
            const ghost = p.buffs & 64;
            const bob = ghost ? Math.round(Math.sin(real / 300 + p.id) * 3) : 0;
            const tint = p.burn > 0 ? '#ff8a1c' : (p.stun > 0 ? '#9aa0b8' : null);
            const x = Math.round(p.x), y = Math.round(p.y);
            ctx.save();
            ctx.imageSmoothingEnabled = false;
            ctx.globalAlpha = Math.max(0, Math.min(1, p.a));
            if (p.s > .5) drawAura(p, x, y);
            ctx.globalAlpha = Math.max(0, Math.min(1, p.a)) * (ghost ? .7 : 1);
            ctx.fillStyle = 'rgba(0,0,0,.35)'; ctx.fillRect(x - size * .3, y + size * .38, size * .6, 6);
            ctx.drawImage(sprite(p.type, p.side, tint), x - size / 2, y - size / 2 + bob, size, size);
            ctx.restore();

            // markers on top: tiers, bomb fuse, orbiting hooks, armor ring, stun
            ctx.save();
            ctx.imageSmoothingEnabled = false;
            ctx.globalAlpha = Math.max(0, Math.min(1, p.a));
            const tags = [];
            if (p.buffs & 32) tags.push([(p.tier || 1) >= 2 ? 'II' : 'I', (p.tier || 1) >= 2 ? GOLD : '#2d6bff', (p.tier || 1) >= 2 ? '#3a1b00' : '#fff']);
            if (p.buffs & 256) tags.push([(p.tier || 1) >= 2 ? 'II' : 'I', GOLD, '#3a1b00']);
            if (p.buffs & 8) tags.push(['I', RED, '#fff']);
            if (p.buffs & 16) tags.push(['II', RED, '#fff']);
            if (p.buffs & 2) tags.push(['PZ', '#b05cff', '#fff']);
            if (p.buffs & 64) tags.push(['G' + (p.ghost || ''), '#c9b3ff', '#241a55']);
            if (p.stun > 0) tags.push(['Z' + p.stun, '#9fb4ff', '#10143a']);
            let tx = Math.round(p.x - SQ * .44);
            for (const t of tags) { badge(t[0], tx, Math.round(p.y + SQ * .26), t[1], t[2]); tx += t[0].length * 10 + 14; }
            if (p.buffs & 24) {
                const big = (p.buffs & 16) !== 0;
                const wy = y - Math.round(SQ * (big ? .72 : .62));
                ctx.fillStyle = '#000'; ctx.fillRect(x - 5, wy - 2, 10, big ? 28 : 22);
                ctx.fillStyle = '#d9a35a'; ctx.fillRect(x - 3, wy, 6, big ? 24 : 18);
                const sp = big ? 20 : 14, on = Math.floor(real / 180) % 2 === 0;
                ctx.fillStyle = '#000'; ctx.fillRect(x - sp / 2 - 3, wy - sp - 3, sp + 6, sp + 6);
                ctx.fillStyle = on ? '#ffe23a' : '#fff'; ctx.fillRect(x - sp / 2, wy - sp, sp, sp);
            }
            if (p.buffs & 256) {
                const n = (p.tier || 1) >= 2 ? 4 : 1;
                for (let i = 0; i < n; i++) {
                    const a = real / 700 + i * Math.PI / 2;
                    const hx = x + Math.cos(a) * SQ * .5, hy = y + Math.sin(a) * SQ * .5;
                    ctx.drawImage(sprite(3, p.side, null), Math.round(hx - 12), Math.round(hy - 12), 24, 24);
                }
            }
            if (p.buffs & 4) {
                for (let i = 0; i < 12; i++) {
                    const a = real / 1600 + i * Math.PI / 6;
                    ctx.fillStyle = '#000'; ctx.fillRect(Math.round(x + Math.cos(a) * SQ * .46) - 6, Math.round(y + Math.sin(a) * SQ * .46) - 6, 12, 12);
                    ctx.fillStyle = CYAN; ctx.fillRect(Math.round(x + Math.cos(a) * SQ * .46) - 4, Math.round(y + Math.sin(a) * SQ * .46) - 4, 8, 8);
                }
            }
            ctx.restore();
        }

        // Evenly spaced stripes along a line, for beams and ropes.
        function stripeLine(from, to, width, colors, seg, offset) {
            const dx = to.x - from.x, dy = to.y - from.y, len = Math.hypot(dx, dy);
            if (len < 1) return;
            ctx.save();
            ctx.translate(from.x, from.y); ctx.rotate(Math.atan2(dy, dx));
            ctx.fillStyle = '#000'; ctx.fillRect(-4, -width / 2 - 4, len + 8, width + 8);
            for (let i = 0, n = 0; i < len; i += seg, n++) {
                ctx.fillStyle = colors[(n + (offset || 0)) % colors.length];
                ctx.fillRect(i, -width / 2, Math.min(seg, len - i), width);
            }
            ctx.restore();
        }

        function drawFx(f) {
            const k = Math.min(1, (clock - f.t0) / f.dur);
            if (k < 0) return;
            ctx.save();
            ctx.imageSmoothingEnabled = false;
            switch (f.kind) {
                case 'burst': {
                    const t = k * f.dur / 1000;
                    for (const q of f.parts) {
                        const s = Math.max(4, Math.round(q.r * (1 - k * .6) * 1.6 / 4) * 4);
                        ctx.globalAlpha = k > .8 ? 1 - (k - .8) / .2 : 1; ctx.fillStyle = '#000';
                        const px = Math.round((f.x + q.vx * t) / 4) * 4, py = Math.round((f.y + q.vy * t + 200 * t * t) / 4) * 4;
                        ctx.fillRect(px - s / 2 - 2, py - s / 2 - 2, s + 4, s + 4);
                        ctx.fillStyle = f.color; ctx.fillRect(px - s / 2, py - s / 2, s, s);
                    }
                    break;
                }
                case 'ring': {
                    const ks = Math.floor(k * 6) / 6;
                    const r = Math.max(4, ks * f.max);
                    ctx.globalAlpha = 1 - ks * .8;
                    ctx.lineWidth = Math.max(4, 12 * (1 - ks)); ctx.strokeStyle = '#000'; ctx.strokeRect(f.x - r - 3, f.y - r - 3, 2 * r + 6, 2 * r + 6);
                    ctx.lineWidth = Math.max(2, 8 * (1 - ks)); ctx.strokeStyle = f.color; ctx.strokeRect(f.x - r, f.y - r, 2 * r, 2 * r);
                    break;
                }
                case 'slash': {
                    const ext = SQ * 1.5, sgn = f.flip ? -1 : 1;
                    ctx.globalAlpha = k < .6 ? 1 : 1 - (k - .6) / .4;
                    const p = Math.min(1, k * 2.2);
                    const x0 = f.x - ext * sgn, y0 = f.y - ext, x1 = x0 + 2 * ext * sgn * p, y1 = y0 + 2 * ext * p;
                    ctx.lineCap = 'butt';
                    ctx.strokeStyle = '#000'; ctx.lineWidth = 22; ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke();
                    ctx.strokeStyle = '#fff'; ctx.lineWidth = 12 * (1 - k * .5); ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke();
                    break;
                }
                case 'streak': {
                    ctx.globalAlpha = (1 - k) * .8; ctx.strokeStyle = '#fff'; ctx.lineWidth = 16 * (1 - k); ctx.lineCap = 'butt';
                    ctx.beginPath(); ctx.moveTo(f.from.x, f.from.y); ctx.lineTo(f.from.x + (f.to.x - f.from.x) * Math.min(1, k * 3), f.from.y + (f.to.y - f.from.y) * Math.min(1, k * 3)); ctx.stroke();
                    break;
                }
                case 'beam': {
                    ctx.globalAlpha = 1 - k * k;
                    const q = Math.min(1, k * 2);
                    stripeLine(f.from, { x: f.from.x + (f.to.x - f.from.x) * q, y: f.from.y + (f.to.y - f.from.y) * q }, 14, [f.color, '#fff'], 14, 0);
                    break;
                }
                case 'zap': {
                    ctx.globalAlpha = k < .7 ? 1 : 1 - (k - .7) / .3;
                    const segs = 7, pts = [];
                    const dx = f.to.x - f.from.x, dy = f.to.y - f.from.y, len = Math.hypot(dx, dy) || 1, nx = -dy / len, ny = dx / len;
                    const flick = Math.floor(real / 70);
                    for (let i = 0; i <= segs; i++) {
                        const j = i === 0 || i === segs ? 0 : (((f.seed * 31 + i * 17 + flick * 13) % 21) - 10) * 2.2;
                        pts.push({ x: f.from.x + dx * i / segs + nx * j, y: f.from.y + dy * i / segs + ny * j });
                    }
                    ctx.lineJoin = 'miter'; ctx.lineCap = 'butt';
                    [[26, '#000'], [16, '#ffe23a'], [6, '#fff']].forEach(([w, c]) => {
                        ctx.lineWidth = w; ctx.strokeStyle = c; ctx.beginPath(); pts.forEach((q, i) => i ? ctx.lineTo(q.x, q.y) : ctx.moveTo(q.x, q.y)); ctx.stroke();
                    });
                    break;
                }
                case 'rope': {
                    const q = Math.min(1, k * 1.8);
                    const tip = { x: f.from.x + (f.to.x - f.from.x) * q, y: f.from.y + (f.to.y - f.from.y) * q };
                    stripeLine(f.from, tip, 8, ['#d9a35a', '#6b3d12'], 10, 0);
                    ctx.drawImage(sprite(3, 0, null), Math.round(tip.x - 20), Math.round(tip.y - 20), 40, 40);
                    break;
                }
                case 'fireball': {
                    const x = f.from.x + (f.to.x - f.from.x) * k, y = f.from.y + (f.to.y - f.from.y) * k;
                    const cols = f.gold ? FLAME.gold : ['#fff', '#ffd24a', '#ff5a1c'];
                    for (let i = 8; i >= 1; i--) {
                        const kk = Math.max(0, k - i * .025);
                        const tx = f.from.x + (f.to.x - f.from.x) * kk, ty = f.from.y + (f.to.y - f.from.y) * kk;
                        const s = Math.round((52 - i * 4) / 4) * 4;
                        ctx.globalAlpha = .75 - i * .07; ctx.fillStyle = cols[2];
                        ctx.fillRect(Math.round(tx / 4) * 4 - s / 2, Math.round(ty / 4) * 4 - s / 2, s, s);
                    }
                    ctx.globalAlpha = 1;
                    ctx.fillStyle = '#000'; ctx.fillRect(x - 32, y - 32, 64, 64);
                    ctx.fillStyle = cols[2]; ctx.fillRect(x - 28, y - 28, 56, 56);
                    ctx.fillStyle = cols[1]; ctx.fillRect(x - 20, y - 20, 40, 40);
                    ctx.fillStyle = cols[0]; ctx.fillRect(x - 10, y - 10, 20, 20);
                    break;
                }
                case 'dragon': {
                    const grow = Math.min(1, k * 3);
                    ctx.globalAlpha = k < .7 ? 1 : 1 - (k - .7) / .3;
                    const x1 = BX + BOARD * grow;
                    stripeLine({ x: BX, y: f.y }, { x: x1, y: f.y }, SQ * .95, ['#fffbd0', '#ffe23a', '#ffa400', '#ff3b2f'], 22, Math.floor(real / 60));
                    break;
                }
                case 'drop': {
                    const ky = k < .8 ? k / .8 : 1;
                    const y = f.y - 420 * (1 - ky * ky);
                    ctx.globalAlpha = k < .85 ? 1 : 1 - (k - .85) / .15;
                    ctx.fillStyle = '#000'; ctx.fillRect(f.x - 12, y - 68, 24, 100); ctx.fillRect(f.x - 26, y + 24, 52, 14);
                    ctx.fillStyle = '#dfe9ff'; ctx.fillRect(f.x - 8, y - 64, 16, 90);
                    ctx.fillStyle = GOLD; ctx.fillRect(f.x - 22, y + 26, 44, 10);
                    break;
                }
                case 'banner': {
                    const a = k < .15 ? k / .15 : k > .8 ? 1 - (k - .8) / .2 : 1;
                    const y = BY + BOARD / 2 - 60;
                    ctx.globalAlpha = a;
                    ctx.fillStyle = '#000'; ctx.fillRect(0, y - 6, W, 132);
                    ctx.fillStyle = '#150e3c'; ctx.fillRect(0, y, W, 120);
                    ctx.fillStyle = f.color; ctx.fillRect(0, y, W, 6); ctx.fillRect(0, y + 114, W, 6);
                    ctx.font = `30px ${PX}`;
                    const w = ctx.measureText(f.text).width, sc = Math.min(1, 660 / w);
                    ctx.save(); ctx.translate(W / 2 + (1 - a) * 80, y + 62); ctx.scale(sc, sc);
                    ptext(f.text, 0, 0, 30, f.color);
                    ctx.restore();
                    ptext('CARD PLAYED', W / 2, y + 100, 12, '#aab4ff');
                    break;
                }
                case 'text': {
                    const rise = f.big ? 40 : 70;
                    const pop = k < .15 ? .6 + (k / .15) * .6 : 1.2 - Math.min(.2, (k - .15) * .4);
                    ctx.globalAlpha = k > .75 ? 1 - (k - .75) / .25 : 1;
                    const base = f.big ? 44 : 28;
                    ctx.font = `${base}px ${PX}`;
                    const sc = Math.min(1, 640 / Math.max(1, ctx.measureText(f.text).width));
                    ctx.translate(f.x, f.y - k * rise); ctx.scale(pop * sc, pop * sc);
                    ptext(f.text, 0, 0, base, f.color);
                    break;
                }
                case 'title': {
                    const a = k < .78 ? 1 : 1 - (k - .78) / .22;
                    ctx.globalAlpha = a * .9; ctx.fillStyle = '#07041a'; ctx.fillRect(0, 0, W, H);
                    ctx.globalAlpha = a;
                    const slam = k < .2 ? 2.4 - (k / .2) * 1.4 : 1;
                    if (k < .55) {
                        ctx.save(); ctx.translate(W / 2, H / 2 - 120); ctx.scale(slam, slam);
                        logo('CHESS', 0, -50, 70); logo('FIGHTER', 0, 40, 70);
                        ctx.restore();
                        ptext((data.title || 'REPLAY').toUpperCase(), W / 2, H / 2 + 40, 22, '#aab4ff');
                    } else {
                        const k2 = (k - .55) / .45, s2 = k2 < .25 ? 2.2 - (k2 / .25) * 1.2 : 1;
                        ctx.save(); ctx.translate(W / 2, H / 2 - 40); ctx.scale(s2, s2);
                        logo('FIGHT!', 0, 0, 88);
                        ctx.restore();
                    }
                    break;
                }
                case 'outro': {
                    const a = Math.min(1, k * 4);
                    ctx.globalAlpha = a * .86; ctx.fillStyle = '#07041a'; ctx.fillRect(0, 0, W, H);
                    ctx.globalAlpha = a;
                    const win = data.result === 'w' || data.result === 'b';
                    const youWon = data.vsAi ? data.result === 'w' : win;
                    const head = data.result === 'd' ? 'DRAW GAME' : data.vsAi ? (youWon ? 'YOU WIN' : 'YOU LOSE') : (data.result === 'w' ? 'P1 WINS' : 'P2 WINS');
                    const col = data.result === 'd' ? '#c9b3ff' : youWon ? GOLD : RED;
                    const pop = k < .2 ? .6 + k / .2 * .4 : 1;
                    const ko = /checkmate|missile|both/i.test(data.reason || '') && data.result !== 'd';
                    ctx.save(); ctx.translate(W / 2, 410); ctx.scale(pop, pop);
                    if (ko) logo('K.O.', 0, -130, 90);
                    logo(head, 0, 0, head.length > 8 ? 56 : 72);
                    ctx.restore();
                    if (data.reason) ptext(data.reason.toUpperCase(), W / 2, 560, 18, '#e8e0ff');
                    ctx.save(); ctx.translate(W / 2, 800); logo('CHESS', 0, -36, 52); logo('FIGHTER', 0, 28, 52); ctx.restore();
                    ptext('16-BIT CHESS WITH POWERUPS', W / 2, 900, 14, CYAN);
                    ptext('ptimleigh.github.io/power-chess/fighter', W / 2, 1000, 13, '#c9b3ff');
                    break;
                }
            }
            ctx.restore();
        }

        function bar(x, y, w, h, pct, fromRight) {
            ctx.fillStyle = '#000'; ctx.fillRect(x - 8, y - 8, w + 16, h + 16);
            ctx.fillStyle = '#fff'; ctx.fillRect(x - 4, y - 4, w + 8, h + 8);
            ctx.fillStyle = '#e0202c'; ctx.fillRect(x, y, w, h);
            const fw = Math.round(w * Math.max(0, Math.min(100, pct)) / 100 / 4) * 4;
            const fx = fromRight ? x + w - fw : x;
            ctx.fillStyle = '#f0a800'; ctx.fillRect(fx, y, fw, h);
            ctx.fillStyle = '#ffe23a'; ctx.fillRect(fx, y, fw, Math.round(h * .65));
            ctx.fillStyle = '#fffbd0'; ctx.fillRect(fx, y, fw, Math.round(h * .25));
        }

        function drawHud() {
            ctx.save();
            logo('CHESS FIGHTER', W / 2, 42, 28);
            // fight bars: P1 on the left, the rival on the right, K.O. between them
            bar(48, 92, 250, 30, hud.hpShow[0], false);
            bar(422, 92, 250, 30, hud.hpShow[1], true);
            ctx.fillStyle = '#000'; ctx.fillRect(W / 2 - 52, 78, 104, 62);
            ctx.fillStyle = '#fff'; ctx.fillRect(W / 2 - 48, 82, 96, 54);
            ctx.fillStyle = '#b8101c'; ctx.fillRect(W / 2 - 44, 86, 88, 46);
            ptext('K.O.', W / 2, 109, 22, '#ffe23a');
            ptext('P1', 48, 150, 14, '#fff', 'left');
            ptext(data.vsAi ? 'CPU' : 'P2', 672, 150, 14, '#fff', 'right');
            ptext(String(Math.max(0, hud.moveNo)).padStart(2, '0'), W / 2, 164, 14, '#ffe23a');
            // move pips
            const pipW = 26, gap = 8, tw = total * pipW + (total - 1) * gap;
            for (let i = 0; i < total; i++) {
                const x = Math.round(W / 2 - tw / 2 + i * (pipW + gap));
                ctx.fillStyle = '#000'; ctx.fillRect(x - 2, 190, pipW + 4, 14);
                ctx.fillStyle = i < hud.moveNo ? CYAN : '#3a3470'; ctx.fillRect(x, 192, pipW, 10);
            }
            ptext('Play free: ptimleigh.github.io/power-chess/fighter', W / 2, 1200, 12, 'rgba(201,179,255,.95)');
            if (phase === 'beats' && hud.moveNo > 0) {
                const age = Math.min(1, (clock - hud.bannerAt) / 260);
                ctx.globalAlpha = age;
                const side = hud.mover;
                ptext(`MOVE ${hud.moveNo} - ${who(side)}`, W / 2, 238 + (1 - age) * 16, 22, side === 'w' ? '#9fd0ff' : '#ff8a7a');
                ctx.font = `15px ${PX}`;
                const lw = ctx.measureText(hud.label).width, size = lw > 660 ? Math.max(9, Math.floor(15 * 660 / lw)) : 15;
                ptext(hud.label, W / 2, 292 + (1 - age) * 16, size, '#e8e0ff');
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
            for (const f of fx) if (f.kind !== 'title' && f.kind !== 'outro' && f.kind !== 'text' && f.kind !== 'banner') drawFx(f);
            for (const f of fx) if (f.kind === 'banner' || f.kind === 'text') drawFx(f);
            ctx.restore();
            // screen-space layers
            const live = fx.filter(f => f.kind === 'title' || f.kind === 'outro');
            drawHud();
            if (vignette > 0) {
                const v = ctx.createRadialGradient(W / 2, H / 2, H * .3, W / 2, H / 2, H * .75);
                v.addColorStop(0, 'rgba(0,0,0,0)'); v.addColorStop(1, `rgba(${vignette > .3 ? '120,0,30' : '0,0,0'},${.25 + vignette * .4})`);
                ctx.fillStyle = v; ctx.fillRect(0, 0, W, H);
            }
            for (const f of live) drawFx(f);
            if (flash > 0) { ctx.fillStyle = `rgba(${flashColor},${flash})`; ctx.fillRect(0, 0, W, H); }
            // CRT scanlines over everything
            ctx.save(); ctx.fillStyle = 'rgba(0,0,0,.16)';
            for (let y = 0; y < H; y += 4) ctx.fillRect(0, y, W, 1);
            ctx.restore();
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
            for (let i = 0; i < 2; i++) {
                const diff = hud.hpTarget[i] - hud.hpShow[i];
                hud.hpShow[i] += Math.sign(diff) * Math.min(Math.abs(diff), dt * .05);
            }
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

    let startToken = 0;
    function start(canvasId, json, withSound) {
        stop();
        const canvas = document.getElementById(canvasId);
        if (!canvas) return false;
        const token = ++startToken;
        fontReady().then(() => {
            if (token !== startToken || !document.getElementById(canvasId)) return;
            preview = Clip(canvas, parse(json), { sound: withSound !== false });
            preview.play().then(() => { });
        });
        return true;
    }

    function stop() {
        startToken++;
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
        await fontReady();
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
            lastClip = { blob, mime: type, name: 'chess-fighter-clip.' + ext };
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
                await cap.Plugins.Share.share({ title: 'Chess Fighter', text: 'My Chess Fighter finish!', files: [saved.uri], dialogTitle: 'Share clip' });
                return 'shared';
            }
            const file = new File([blob], name, { type: mime });
            if (navigator.canShare && navigator.canShare({ files: [file] })) {
                await navigator.share({ files: [file], title: 'Chess Fighter', text: 'My Chess Fighter finish!' });
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
