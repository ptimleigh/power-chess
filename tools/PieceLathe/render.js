#!/usr/bin/env node
// Headless renderer so Claude (or CI) can look at a piece without a human clicking around.
//   node render.js pieces/pawn.json [more.json ...] [--out=out.png] [--view=iso|front|side|top] [--glb=out.glb] [--size=900x700]
// Several files => side-by-side lineup (original vs variant).
const http = require('http'), fs = require('fs'), path = require('path');
const { chromium } = require('playwright');

const args = process.argv.slice(2);
const opt = (k, d) => (args.find((a) => a.startsWith(`--${k}=`)) || `--${k}=${d}`).split('=').slice(1).join('=');
const files = args.filter((a) => !a.startsWith('--'));
if (!files.length) { console.error('usage: node render.js piece.json [more.json] [--out=..] [--view=..] [--glb=..] [--size=WxH]'); process.exit(1); }
const [W, H] = opt('size', '900x700').split('x').map(Number);
const root = __dirname;
const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json' };

const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(req.url.split('?')[0]).replace(/^\/$/, '/index.html'));
  if (!p.startsWith(root) || !fs.existsSync(p)) { res.writeHead(404); return res.end(); }
  res.writeHead(200, { 'content-type': types[path.extname(p)] || 'application/octet-stream' });
  fs.createReadStream(p).pipe(res);
});

(async () => {
  await new Promise((r) => server.listen(0, r));
  const browser = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: W + 380, height: H } });
  page.on('pageerror', (e) => console.error('page error:', e.message));
  page.on('console', (m) => m.type() === 'error' && console.error('console:', m.text()));
  await page.goto(`http://localhost:${server.address().port}/`);
  await page.waitForFunction(() => window.pieceLatheReady);
  const states = files.map((f) => JSON.parse(fs.readFileSync(f, 'utf8')));
  await page.evaluate(([s, v]) => { window.pieceLathe.setState(s[0]); if (s.length > 1) window.pieceLathe.lineup(s); window.pieceLathe.setView(v, [0, 0.85, 0], 5.8 + (s.length > 1 ? s.length * 0.9 : 0)); }, [states, opt('view', 'iso')]);
  await page.waitForTimeout(300);
  const out = opt('out', 'piece.png');
  const url = await page.evaluate(() => window.pieceLathe.screenshot());
  fs.writeFileSync(out, Buffer.from(url.split(',')[1], 'base64'));
  console.log('wrote', out);
  if (opt('glb', '')) {
    await page.evaluate((s) => window.pieceLathe.setState(s), states[0]);
    const b64 = await page.evaluate(async () => { const b = new Uint8Array(await window.pieceLathe.exportGLB()); let s = ''; b.forEach((c) => (s += String.fromCharCode(c))); return btoa(s); });
    fs.writeFileSync(opt('glb', ''), Buffer.from(b64, 'base64')); console.log('wrote', opt('glb', ''));
  }
  await browser.close(); server.close();
})();
