#!/usr/bin/env node
// Bundles the tool into single HTML files that need no server and no other files:
//   dist/piece-lathe.html           double-click / open on a phone: a complete page
//   dist/piece-lathe.artifact.html  the same page without <html>/<head>/<body>, for publishing as a claude.ai Artifact
//                                   (the publish step wraps it, and saving then goes through the `downloads` capability)
//   npm i esbuild   (once, anywhere on the module path)   then:   node build-single.js
const fs = require('fs'), path = require('path');
const esbuild = require('esbuild');
const dir = __dirname;
const bundle = esbuild.buildSync({ entryPoints: [path.join(dir, 'app.js')], bundle: true, minify: true, format: 'iife', write: false, alias: { three: path.join(dir, 'vendor/three.module.js') } }).outputFiles[0].text;
const presets = {};
for (const f of JSON.parse(fs.readFileSync(path.join(dir, 'pieces/index.json'), 'utf8'))) presets[f] = JSON.parse(fs.readFileSync(path.join(dir, 'pieces', f), 'utf8'));
presets['index.json'] = Object.keys(presets);
const safe = (t) => t.replace(/<\/script/gi, '<\\/script');
const scripts = `<script>window.__PRESETS__=${safe(JSON.stringify(presets))};</script>\n<script>${safe(bundle)}</script>`;
const src = fs.readFileSync(path.join(dir, 'index.html'), 'utf8');
const strip = (h) => h.replace(/<script type="importmap">.*?<\/script>\s*/s, '').replace('<script type="module" src="app.js"></script>', () => scripts);

fs.mkdirSync(path.join(dir, 'dist'), { recursive: true });
fs.writeFileSync(path.join(dir, 'dist/piece-lathe.html'), strip(src));

const title = src.match(/<title>.*?<\/title>/s)[0];
const style = src.match(/<style>.*?<\/style>/s)[0];            // the unnamed one; #standalone-only is left out on purpose
const body = strip(src.match(/<body>(.*)<\/body>/s)[1]);
const art = `${title}\n<meta name="claude:viewport-fit-sides" content="cover">\n${style}\n${body}\n`;
fs.writeFileSync(path.join(dir, 'dist/piece-lathe.artifact.html'), art);
console.log('wrote dist/piece-lathe.html', (fs.statSync(path.join(dir, 'dist/piece-lathe.html')).size / 1024).toFixed(0) + ' KB;',
  'dist/piece-lathe.artifact.html', (art.length / 1024).toFixed(0) + ' KB');
