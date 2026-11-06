#!/usr/bin/env node
// Bundles the tool into ONE html file (dist/piece-lathe.html) that works by double-click, no server needed.
//   npm i esbuild   (once, anywhere on the module path)   then:   node build-single.js
const fs = require('fs'), path = require('path');
const esbuild = require('esbuild');
const dir = __dirname;
const out = esbuild.buildSync({ entryPoints: [path.join(dir, 'app.js')], bundle: true, minify: true, format: 'iife', write: false, alias: { three: path.join(dir, 'vendor/three.module.js') } }).outputFiles[0].text;
const presets = {};
for (const f of JSON.parse(fs.readFileSync(path.join(dir, 'pieces/index.json'), 'utf8'))) presets[f] = JSON.parse(fs.readFileSync(path.join(dir, 'pieces', f), 'utf8'));
presets['index.json'] = Object.keys(presets);
let html = fs.readFileSync(path.join(dir, 'index.html'), 'utf8');
html = html.replace(/<script type="importmap">.*?<\/script>\s*/s, '');
const safe = (t) => t.replace(/<\/script/gi, '<\\/script');
html = html.replace('<script type="module" src="app.js"></script>',
  () => `<script>window.__PRESETS__=${safe(JSON.stringify(presets))};</script>\n<script>${safe(out)}</script>`);
fs.mkdirSync(path.join(dir, 'dist'), { recursive: true });
fs.writeFileSync(path.join(dir, 'dist/piece-lathe.html'), html);
console.log('wrote dist/piece-lathe.html', (html.length / 1024).toFixed(0) + ' KB');
