// Renders the app icons from an inline SVG with the preinstalled Chromium.
// usage: node tools/make-icons.js web/PowerChess.Web/wwwroot/icons
const { chromium } = require('playwright');
const path = require('path');
const out = process.argv[2] || 'web/PowerChess.Web/wwwroot/icons';

// A white knight on a neon tile. `pad` leaves a safe zone for maskable icons.
const svg = (pad) => `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#2a1f66"/><stop offset="1" stop-color="#0d0b1e"/></linearGradient>
    <linearGradient id="rim" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ff4fd8"/><stop offset="1" stop-color="#38f9d7"/></linearGradient>
    <linearGradient id="k" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff"/><stop offset=".55" stop-color="#cfe9ff"/><stop offset="1" stop-color="#7fb8ff"/></linearGradient>
  </defs>
  <rect width="512" height="512" fill="url(#bg)"/>
  ${pad ? '' : '<rect x="14" y="14" width="484" height="484" rx="96" fill="none" stroke="url(#rim)" stroke-width="14"/>'}
  <g transform="translate(256 262) scale(${pad ? 0.78 : 1}) translate(-256 -262)">
    <rect x="140" y="388" width="232" height="38" rx="14" fill="url(#k)" stroke="#6b3fd1" stroke-width="8"/>
    <path d="M170 366c6-70 28-108 66-146-22 4-44 18-60 40l-24-12c16-52 52-94 114-108l16-34 28 40c52 22 82 74 82 140 0 38-4 80-4 120z" fill="url(#k)" stroke="#6b3fd1" stroke-width="8" stroke-linejoin="round"/>
    <circle cx="300" cy="196" r="12" fill="#2a1f66"/>
  </g>
  <g fill="#ffd24a" transform="translate(388 118)"><path d="M0-34l9 25 26 1-21 16 8 26-22-15-22 15 8-26-21-16 26-1z"/></g>
</svg>`;

(async () => {
  const b = await chromium.launch();
  const render = async (name, size, pad) => {
    const p = await b.newPage({ viewport: { width: size, height: size } });
    await p.setContent(`<body style="margin:0">${svg(pad).replace('width="512" height="512"', `width="${size}" height="${size}"`)}</body>`);
    await p.screenshot({ path: path.join(out, name) });
    await p.close();
  };
  await render('icon-192.png', 192, false);
  await render('icon-512.png', 512, false);
  await render('icon-maskable-512.png', 512, true);
  await render('apple-touch-icon.png', 180, true);
  await b.close();
})();
