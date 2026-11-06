import * as THREE from './vendor/three.module.js';
import { OrbitControls } from './vendor/OrbitControls.js';
import { GLTFExporter } from './vendor/GLTFExporter.js';

// ---------------------------------------------------------------- model ----
// A piece is plain JSON: { name, parts: [ lathe | extrude ] }
//   lathe   : { id, type:'lathe', profile:[[radius,height,corner?],...], smooth, segments, color, offset, scale, rotationY }
//   extrude : { id, type:'extrude', outline:[[x,y],...], depth, bevel, color, offset, scale, rotationY }
// Everything the UI does goes through setState(), and so does the window.pieceLathe API.

const clone = (o) => JSON.parse(JSON.stringify(o));
const r2 = (n) => Math.round(n * 100) / 100;

let state = { name: 'piece', parts: [] };
let selectedId = null;
const undoStack = [];

function normalize(s) {
  s = clone(s);
  s.name = s.name || 'piece';
  s.parts = Array.isArray(s.parts) ? s.parts : [];
  const seen = new Set();
  s.parts.forEach((p, i) => {
    p.type = p.type === 'extrude' ? 'extrude' : 'lathe';
    let id = p.id || `${p.type}${i + 1}`;
    while (seen.has(id)) id += '_';
    p.id = id; seen.add(id);
    p.color = p.color || '#e9e2d0';
    if (p.type === 'lathe') p.profile = p.profile || [[0, 0, 1], [0.4, 0], [0, 1, 1]];
    else { p.outline = p.outline || [[-0.3, 0], [0.3, 0], [0, 1]]; p.depth = p.depth ?? 0.4; p.bevel = p.bevel ?? 0.04; }
  });
  return s;
}

// ------------------------------------------------------------- 3D scene ----
const view = document.getElementById('view');
const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
view.appendChild(renderer.domElement);
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x20232b);
const camera = new THREE.PerspectiveCamera(35, 1, 0.05, 100);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
scene.add(new THREE.HemisphereLight(0xffffff, 0x445066, 1.1));
const sun = new THREE.DirectionalLight(0xffffff, 2.2);
sun.position.set(3, 5, 4); scene.add(sun);
const fill = new THREE.DirectionalLight(0x9bb4ff, 0.7);
fill.position.set(-4, 2, -3); scene.add(fill);
const floor = new THREE.Mesh(new THREE.CircleGeometry(6, 64), new THREE.MeshStandardMaterial({ color: 0x2b303b, roughness: 1 }));
floor.rotation.x = -Math.PI / 2; floor.position.y = -0.002; scene.add(floor);
const grid = new THREE.GridHelper(12, 24, 0x4a5163, 0x363c4a); scene.add(grid);
const pieceGroup = new THREE.Group(); scene.add(pieceGroup);

function setView(name, target = [0, 0.75, 0], dist = 5.8) {
  const t = new THREE.Vector3(...target);
  const dirs = { front: [0, 0.1, 1], side: [1, 0.1, 0], top: [0, 1, 0.001], iso: [0.8, 0.55, 1] };
  const d = new THREE.Vector3(...(dirs[name] || dirs.iso)).normalize();
  camera.position.copy(t).addScaledVector(d, dist);
  controls.target.copy(t); controls.update();
}

function resize() {
  const w = view.clientWidth, h = view.clientHeight;
  renderer.setSize(w, h); camera.aspect = w / h; camera.updateProjectionMatrix();
}
new ResizeObserver(resize).observe(view);

// Straight runs get Catmull-Rom smoothing between "corner" points (3rd value = 1).
function latheProfile(part) {
  const pts = part.profile.map((p) => ({ v: new THREE.Vector2(Math.max(0, p[0]), p[1]), corner: !!p[2] }));
  const smooth = part.smooth ?? 6;
  const runs = []; let cur = [];
  pts.forEach((p, i) => { cur.push(p.v); if (p.corner && i > 0 && i < pts.length - 1) { runs.push(cur); cur = [p.v]; } });
  runs.push(cur);
  const out = [];
  runs.forEach((run) => {
    let seg = run;
    if (smooth > 0 && run.length >= 3) {
      const curve = new THREE.CatmullRomCurve3(run.map((v) => new THREE.Vector3(v.x, v.y, 0)), false, 'centripetal');
      seg = curve.getPoints((run.length - 1) * smooth).map((v) => new THREE.Vector2(Math.max(0, v.x), v.y));
    }
    out.push(...(out.length ? seg.slice(1) : seg));
  });
  return out;
}

function buildPart(part) {
  let geo;
  if (part.type === 'lathe') {
    geo = new THREE.LatheGeometry(latheProfile(part), part.segments ?? state.segments ?? 48);
  } else {
    const shape = new THREE.Shape(part.outline.map((p) => new THREE.Vector2(p[0], p[1])));
    const b = part.bevel || 0;
    geo = new THREE.ExtrudeGeometry(shape, { depth: part.depth, bevelEnabled: b > 0, bevelThickness: b, bevelSize: b, bevelSegments: 3, curveSegments: 12 });
    geo.translate(0, 0, -part.depth / 2);
  }
  const mat = new THREE.MeshStandardMaterial({ color: part.color, roughness: 0.45, metalness: 0.08, side: THREE.DoubleSide });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.name = part.id;
  if (part.offset) mesh.position.set(...part.offset);
  const s = part.scale ?? 1;
  Array.isArray(s) ? mesh.scale.set(...s) : mesh.scale.setScalar(s);
  mesh.rotation.y = THREE.MathUtils.degToRad(part.rotationY || 0);
  return mesh;
}

function disposeGroup(g) {
  [...g.children].forEach((c) => { c.geometry?.dispose(); c.material?.dispose(); g.remove(c); });
}

function rebuild3D() {
  disposeGroup(pieceGroup);
  pieceGroup.name = state.name;
  state.parts.forEach((p) => pieceGroup.add(buildPart(p)));
}

(function loop() { controls.update(); renderer.render(scene, camera); requestAnimationFrame(loop); })();

// ------------------------------------------------------- 2D profile editor ----
const cv = document.getElementById('editor');
const ctx = cv.getContext('2d');
let drag = null;
const SCALE_UNITS = 1.9; // units visible vertically

const geomOf = (p) => (p.type === 'lathe' ? p.profile : p.outline);
const selPart = () => state.parts.find((p) => p.id === selectedId);
function frame() {
  const dpr = window.devicePixelRatio || 1;
  const w = cv.clientWidth, h = cv.clientHeight;
  if (cv.width !== w * dpr || cv.height !== h * dpr) { cv.width = w * dpr; cv.height = h * dpr; }
  const s = (h - 40) / SCALE_UNITS;
  const p = selPart();
  return { w, h, s, ox: p && p.type === 'lathe' ? w * 0.2 : w * 0.5, oy: h - 24, dpr };
}
const toPx = (f, x, y) => [f.ox + x * f.s, f.oy - y * f.s];
const fromPx = (f, px, py) => [(px - f.ox) / f.s, (f.oy - py) / f.s];

function drawEditor() {
  const f = frame(); const p = selPart();
  ctx.setTransform(f.dpr, 0, 0, f.dpr, 0, 0);
  ctx.clearRect(0, 0, f.w, f.h);
  ctx.fillStyle = '#171a20'; ctx.fillRect(0, 0, f.w, f.h);
  ctx.strokeStyle = '#2a2f3a'; ctx.lineWidth = 1; ctx.font = '10px sans-serif'; ctx.fillStyle = '#566';
  for (let v = -2; v <= 2; v += 0.25) {
    const [x] = toPx(f, v, 0); ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, f.h); ctx.stroke();
  }
  for (let v = 0; v <= SCALE_UNITS; v += 0.25) {
    const [, y] = toPx(f, 0, v); ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(f.w, y); ctx.stroke();
    if (v % 0.5 === 0) ctx.fillText(v.toFixed(1), 3, y - 2);
  }
  ctx.strokeStyle = '#5b6b8c'; ctx.beginPath(); ctx.moveTo(f.ox, 0); ctx.lineTo(f.ox, f.h); ctx.stroke();
  if (!p) { ctx.fillStyle = '#889'; ctx.fillText('Select or add a part', 12, 20); return; }
  const g = geomOf(p);
  const trace = (mirror) => {
    ctx.beginPath();
    g.forEach((pt, i) => { const [x, y] = toPx(f, mirror ? -pt[0] : pt[0], pt[1]); i ? ctx.lineTo(x, y) : ctx.moveTo(x, y); });
    if (p.type === 'extrude') ctx.closePath();
  };
  if (p.type === 'lathe') { ctx.strokeStyle = '#3d4a63'; trace(true); ctx.stroke(); }
  ctx.strokeStyle = '#7fb0ff'; ctx.lineWidth = 2; trace(false); ctx.stroke(); ctx.lineWidth = 1;
  g.forEach((pt, i) => {
    const [x, y] = toPx(f, pt[0], pt[1]);
    ctx.beginPath();
    if (pt[2]) ctx.rect(x - 5, y - 5, 10, 10); else ctx.arc(x, y, 5, 0, 7);
    ctx.fillStyle = drag && drag.i === i ? '#ffd24a' : pt[2] ? '#ff8a5b' : '#fff'; ctx.fill();
  });
}

function evPos(e) { const r = cv.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; }
function hit(f, g, px, py) {
  let best = -1, bd = 12;
  g.forEach((pt, i) => { const [x, y] = toPx(f, pt[0], pt[1]); const d = Math.hypot(x - px, y - py); if (d < bd) { bd = d; best = i; } });
  return best;
}
function nearestSegment(f, g, px, py, closed) {
  let best = g.length, bd = 1e9;
  const n = closed ? g.length : g.length - 1;
  for (let i = 0; i < n; i++) {
    const a = toPx(f, ...g[i].slice(0, 2)), b = toPx(f, ...g[(i + 1) % g.length].slice(0, 2));
    const dx = b[0] - a[0], dy = b[1] - a[1];
    const t = Math.max(0, Math.min(1, ((px - a[0]) * dx + (py - a[1]) * dy) / (dx * dx + dy * dy || 1)));
    const d = Math.hypot(a[0] + t * dx - px, a[1] + t * dy - py);
    if (d < bd) { bd = d; best = i + 1; }
  }
  return best;
}

cv.addEventListener('pointerdown', (e) => {
  const p = selPart(); if (!p || e.button !== 0) return;
  const f = frame(), [px, py] = evPos(e), g = geomOf(p);
  let i = hit(f, g, px, py);
  if (i < 0) {
    pushUndo();
    const [x, y] = fromPx(f, px, py);
    i = nearestSegment(f, g, px, py, p.type === 'extrude');
    g.splice(i, 0, [r2(p.type === 'lathe' ? Math.max(0, x) : x), r2(y)]);
    commit(false);
  } else pushUndo();
  drag = { i }; cv.setPointerCapture(e.pointerId);
});
cv.addEventListener('pointermove', (e) => {
  if (!drag) return;
  const p = selPart(), f = frame(), [px, py] = evPos(e), [x, y] = fromPx(f, px, py);
  const pt = geomOf(p)[drag.i];
  pt[0] = r2(p.type === 'lathe' ? Math.max(0, x) : x); pt[1] = r2(y);
  commit(false);
});
cv.addEventListener('pointerup', () => { if (drag) { drag = null; commit(true); } });
cv.addEventListener('dblclick', (e) => { // toggle sharp corner (lathe)
  const p = selPart(); if (!p || p.type !== 'lathe') return;
  const [px, py] = evPos(e), i = hit(frame(), p.profile, px, py); if (i < 0) return;
  pushUndo(); const pt = p.profile[i]; pt[2] ? pt.splice(2, 1) : (pt[2] = 1); commit(true);
});
cv.addEventListener('contextmenu', (e) => { // delete point
  e.preventDefault();
  const p = selPart(); if (!p) return;
  const g = geomOf(p), [px, py] = evPos(e), i = hit(frame(), g, px, py);
  if (i < 0 || g.length <= 3) return;
  pushUndo(); g.splice(i, 1); commit(true);
});
new ResizeObserver(drawEditor).observe(cv);

// ------------------------------------------------------------ panel UI ----
const $ = (id) => document.getElementById(id);
const partsEl = $('parts'), jsonEl = $('json'), propsEl = $('props'), nameEl = $('name');

function pushUndo() { undoStack.push(JSON.stringify(state)); if (undoStack.length > 100) undoStack.shift(); }

// commit(true) = full UI refresh; commit(false) = lightweight (during drag)
function commit(full = true) {
  rebuild3D(); drawEditor();
  if (full) { renderParts(); renderProps(); if (document.activeElement !== jsonEl) jsonEl.value = JSON.stringify(state, null, 2); }
}

function renderParts() {
  partsEl.innerHTML = '';
  state.parts.forEach((p) => {
    const b = document.createElement('button');
    b.className = 'part' + (p.id === selectedId ? ' sel' : '');
    b.innerHTML = `<span class="sw" style="background:${p.color}"></span>${p.id} <small>${p.type}</small>`;
    b.onclick = () => { selectedId = p.id; commit(true); };
    partsEl.appendChild(b);
  });
}

function numField(label, get, set, step = 0.01) {
  const l = document.createElement('label'); l.textContent = label;
  const i = document.createElement('input'); i.type = 'number'; i.step = step; i.value = get();
  i.onchange = () => { pushUndo(); set(parseFloat(i.value) || 0); commit(true); };
  l.appendChild(i); return l;
}

function renderProps() {
  propsEl.innerHTML = '';
  const p = selPart(); if (!p) return;
  p.offset = p.offset || [0, 0, 0];
  const idL = document.createElement('label'); idL.textContent = 'id';
  const idI = document.createElement('input'); idI.value = p.id;
  idI.onchange = () => { pushUndo(); p.id = idI.value || p.id; selectedId = p.id; commit(true); };
  idL.appendChild(idI); propsEl.appendChild(idL);
  const colL = document.createElement('label'); colL.textContent = 'color';
  const colI = document.createElement('input'); colI.type = 'color'; colI.value = p.color;
  colI.oninput = () => { p.color = colI.value; rebuild3D(); renderParts(); }; colI.onchange = () => { pushUndo(); commit(true); };
  colL.appendChild(colI); propsEl.appendChild(colL);
  ['x', 'y', 'z'].forEach((a, k) => propsEl.appendChild(numField('offset ' + a, () => p.offset[k], (v) => (p.offset[k] = v))));
  propsEl.appendChild(numField('scale', () => (Array.isArray(p.scale) ? p.scale[0] : p.scale ?? 1), (v) => (p.scale = v || 1)));
  propsEl.appendChild(numField('rotate Y°', () => p.rotationY || 0, (v) => (p.rotationY = v), 1));
  if (p.type === 'lathe') {
    propsEl.appendChild(numField('smooth', () => p.smooth ?? 6, (v) => (p.smooth = Math.max(0, Math.round(v))), 1));
    propsEl.appendChild(numField('segments', () => p.segments ?? 48, (v) => (p.segments = Math.max(3, Math.round(v))), 1));
  } else {
    propsEl.appendChild(numField('depth', () => p.depth, (v) => (p.depth = Math.max(0.01, v))));
    propsEl.appendChild(numField('bevel', () => p.bevel, (v) => (p.bevel = Math.max(0, v))));
  }
}

function newPart(type) {
  const id = (type === 'lathe' ? 'lathe' : 'extrude') + (state.parts.length + 1);
  return type === 'lathe'
    ? { id, type, color: '#cfd6e6', smooth: 6, profile: [[0, 0, 1], [0.4, 0], [0.3, 0.5], [0.4, 1], [0, 1, 1]] }
    : { id, type, color: '#cfd6e6', depth: 0.4, bevel: 0.04, outline: [[-0.4, 0], [0.4, 0], [0.4, 0.8], [0, 1.2], [-0.4, 0.8]] };
}

$('addLathe').onclick = () => api.addPart(newPart('lathe'));
$('addExtrude').onclick = () => api.addPart(newPart('extrude'));
$('dupPart').onclick = () => { const p = selPart(); if (p) api.addPart({ ...clone(p), id: p.id + '_copy' }); };
$('delPart').onclick = () => selectedId && api.removePart(selectedId);
$('undo').onclick = () => api.undo();
$('applyJson').onclick = () => { try { api.setState(JSON.parse(jsonEl.value)); } catch (e) { alert('Bad JSON: ' + e.message); } };
$('exportGlb').onclick = async () => download(new Blob([await api.exportGLB()], { type: 'model/gltf-binary' }), state.name + '.glb');
$('saveJson').onclick = () => download(new Blob([JSON.stringify(state, null, 2)], { type: 'application/json' }), state.name + '.json');
$('loadJson').onchange = async (e) => { const f = e.target.files[0]; if (f) api.setState(JSON.parse(await f.text())); e.target.value = ''; };
$('shot').onclick = async () => download(await (await fetch(api.screenshot())).blob(), state.name + '.png');
nameEl.onchange = () => { pushUndo(); state.name = nameEl.value || 'piece'; commit(true); };
document.querySelectorAll('[data-view]').forEach((b) => (b.onclick = () => setView(b.dataset.view)));
document.addEventListener('keydown', (e) => { if ((e.ctrlKey || e.metaKey) && e.key === 'z' && document.activeElement.tagName !== 'TEXTAREA') { e.preventDefault(); api.undo(); } });

function download(blob, filename) {
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = filename; a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

async function loadPresetList() {
  try {
    const list = await (await fetch('pieces/index.json')).json();
    const sel = $('presets'); sel.innerHTML = '<option value="">Open preset…</option>';
    list.forEach((f) => sel.insertAdjacentHTML('beforeend', `<option>${f}</option>`));
    sel.onchange = () => sel.value && api.loadURL('pieces/' + sel.value);
  } catch { /* running from file:// */ }
}

// ------------------------------------------------------------------ API ----
// Same operations the UI uses; Claude (via Playwright) or any script can call these.
const api = {
  getState: () => clone(state),
  setState(s) {
    pushUndo(); state = normalize(s);
    if (!state.parts.find((p) => p.id === selectedId)) selectedId = state.parts[0]?.id ?? null;
    nameEl.value = state.name; commit(true);
  },
  async loadURL(url) { api.setState(await (await fetch(url)).json()); return api.getState(); },
  addPart(part) { pushUndo(); state.parts.push(part); state = normalize(state); selectedId = part.id; commit(true); },
  updatePart(id, patch) { const p = state.parts.find((x) => x.id === id); if (!p) throw new Error('no part ' + id); pushUndo(); Object.assign(p, clone(patch)); state = normalize(state); commit(true); },
  removePart(id) { pushUndo(); state.parts = state.parts.filter((p) => p.id !== id); if (selectedId === id) selectedId = state.parts[0]?.id ?? null; commit(true); },
  selectPart(id) { selectedId = id; commit(true); },
  setProfile(id, pts) { api.updatePart(id, { profile: pts }); },
  movePoint(id, i, a, b) { const p = state.parts.find((x) => x.id === id); pushUndo(); geomOf(p)[i][0] = a; geomOf(p)[i][1] = b; commit(true); },
  undo() { const s = undoStack.pop(); if (!s) return; state = JSON.parse(s); if (!state.parts.find((p) => p.id === selectedId)) selectedId = state.parts[0]?.id ?? null; nameEl.value = state.name; commit(true); },
  setView,
  screenshot: () => { renderer.render(scene, camera); return renderer.domElement.toDataURL('image/png'); },
  async exportGLB() {
    return new Promise((res, rej) => new GLTFExporter().parse(pieceGroup, res, rej, { binary: true }));
  },
  // Show several pieces side by side (for comparing a variant against the original). setState() returns to editing.
  lineup(states, gap = 1.5) {
    disposeGroup(pieceGroup);
    const n = states.length;
    states.forEach((s, k) => {
      const g = new THREE.Group(); normalizeInto(s).parts.forEach((p) => g.add(buildPart(p)));
      g.position.x = (k - (n - 1) / 2) * gap; pieceGroup.add(g);
    });
    setView('iso', [0, 0.75, 0], 5.8 + n * 0.9);
  },
};
function normalizeInto(s) { const keep = state; const n = normalize(s); state = keep; return n; }
window.pieceLathe = api;

// ---------------------------------------------------------------- boot ----
resize(); setView('iso');
loadPresetList();
const q = new URLSearchParams(location.search);
(async () => {
  try { await api.loadURL('pieces/' + (q.get('piece') || 'pawn.json')); } catch { api.setState({ name: 'piece', parts: [newPart('lathe')] }); }
  undoStack.length = 0;
  window.pieceLatheReady = true;
})();
