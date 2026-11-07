import * as THREE from './vendor/three.module.js';
import { OrbitControls } from './vendor/OrbitControls.js';
import { GLTFExporter } from './vendor/GLTFExporter.js';

// ---------------------------------------------------------------- model ----
// A piece is plain JSON: { name, parts: [ lathe | extrude | ribbon ], idle: {...} }
//   lathe   : { id, type:'lathe', profile:[[radius,height,corner?],...], smooth, segments, color, offset, scale, rotationY }
//   extrude : { id, type:'extrude', outline:[[x,y,corner?],...], depth, bevel, bevelSegments, smooth, taper, color, offset, scale, rotationY }
//             smooth = curve samples per outline segment (0 = straight edges), bevel = rounded edge size, taper narrows the
//             piece toward +x (0..1) so a head can slim down toward the nose
//   ribbon  : { id, type:'ribbon', offset(anchor), dir, length, width, tipWidth, segments, stiffness, damping, gravity, color }
//             a chain of segments that trails behind the piece with spring physics (bandana tails, scarves, capes...)
//   idle    : { speed, bounce, sway, lean, breathe, squash }  the looping "ready to fight" animation
// Everything the UI does goes through setState(), and so does the window.pieceLathe API.

const clone = (o) => JSON.parse(JSON.stringify(o));
const r2 = (n) => Math.round(n * 100) / 100;

let state = { name: 'piece', parts: [] };
let selectedId = null;
const undoStack = [];

const IDLE_DEFAULTS = { style: 'fighter', speed: 1, bounce: 0.04, sway: 0.035, lean: 3.5, breathe: 0.012, squash: 0.06 };
const RIBBON_DEFAULTS = { dir: [0, -0.25, -1], length: 0.6, width: 0.07, tipWidth: 0.04, segments: 6, stiffness: 0.12, damping: 0.015, gravity: 4 };

function normalize(s) {
  s = clone(s);
  s.name = s.name || 'piece';
  s.idle = { ...IDLE_DEFAULTS, ...(s.idle || {}) };
  s.parts = Array.isArray(s.parts) ? s.parts : [];
  const seen = new Set();
  s.parts.forEach((p, i) => {
    p.type = p.type === 'extrude' || p.type === 'ribbon' ? p.type : 'lathe';
    let id = p.id || `${p.type}${i + 1}`;
    while (seen.has(id)) id += '_';
    p.id = id; seen.add(id);
    p.color = p.color || '#e9e2d0';
    if (p.type === 'lathe') p.profile = p.profile || [[0, 0, 1], [0.4, 0], [0, 1, 1]];
    else if (p.type === 'ribbon') {
      for (const k in RIBBON_DEFAULTS) if (p[k] === undefined) p[k] = clone(RIBBON_DEFAULTS[k]);
      p.offset = p.offset || [0, 0, 0];
    } else { p.outline = p.outline || [[-0.3, 0], [0.3, 0], [0, 1]]; p.depth = p.depth ?? 0.4; p.bevel = p.bevel ?? 0.04; p.bevelSegments = p.bevelSegments ?? 3; p.smooth = p.smooth ?? 0; p.taper = p.taper ?? 0; }
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
const root = new THREE.Group(); scene.add(root);            // moved around the board (drag / slide test)
const pieceGroup = new THREE.Group(); root.add(pieceGroup); // the piece's own parts; idle animation moves this
const ribbonGroup = new THREE.Group(); scene.add(ribbonGroup); // ribbon meshes live in world space (physics)

const DEFAULT_DIST = () => (matchMedia('(max-width:820px)').matches ? 4.4 : 5.8); // closer on phones: the stage is short
function setView(name, target = [0, 0.75, 0], dist = DEFAULT_DIST()) {
  const t = new THREE.Vector3(...target);
  const dirs = { front: [0, 0.1, 1], back: [0, 0.1, -1], side: [1, 0.1, 0], top: [0, 1, 0.001], iso: [0.8, 0.55, 1] };
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

// Smooth a closed outline with Catmull-Rom; points flagged as corners (3rd value = 1) stay sharp.
function smoothOutline(pts, smooth) {
  const P = pts.map((p) => ({ v: new THREE.Vector2(p[0], p[1]), c: !!p[2] }));
  if (!smooth || smooth < 1 || P.length < 3) return P.map((p) => p.v);
  const sample = (vs, closed) => {
    const curve = new THREE.CatmullRomCurve3(vs.map((v) => new THREE.Vector3(v.x, v.y, 0)), closed, 'centripetal');
    const out = curve.getPoints(closed ? vs.length * smooth : (vs.length - 1) * smooth).map((v) => new THREE.Vector2(v.x, v.y));
    return out.slice(0, -1); // last point repeats the next run's first
  };
  const first = P.findIndex((p) => p.c);
  if (first < 0) return sample(P.map((p) => p.v), true);
  const arr = P.slice(first).concat(P.slice(0, first)), out = [];
  let run = [arr[0].v];
  for (let i = 1; i <= arr.length; i++) {
    const q = arr[i % arr.length];
    run.push(q.v);
    if (q.c || i === arr.length) {
      out.push(...(run.length >= 3 ? sample(run, false) : run.slice(0, -1)));
      run = [q.v];
    }
  }
  return out;
}

// Merge shared vertices and recompute normals so a smooth, bevelled extrusion shades smoothly instead of faceted.
function weldSmooth(geo) {
  const pos = geo.attributes.position, map = new Map(), verts = [], idx = [];
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), y = pos.getY(i), z = pos.getZ(i);
    const key = `${Math.round(x * 1e4)},${Math.round(y * 1e4)},${Math.round(z * 1e4)}`;
    let j = map.get(key);
    if (j === undefined) { j = verts.length / 3; map.set(key, j); verts.push(x, y, z); }
    idx.push(j);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(verts, 3));
  g.setIndex(idx); g.computeVertexNormals(); geo.dispose();
  return g;
}

function buildPart(part) {
  let geo;
  if (part.type === 'lathe') {
    geo = new THREE.LatheGeometry(latheProfile(part), part.segments ?? state.segments ?? 48);
  } else {
    const shape = new THREE.Shape(smoothOutline(part.outline, part.smooth));
    const b = part.bevel || 0;
    geo = new THREE.ExtrudeGeometry(shape, { depth: part.depth, bevelEnabled: b > 0, bevelThickness: b, bevelSize: b, bevelSegments: part.bevelSegments ?? 3, curveSegments: 12 });
    geo.translate(0, 0, -part.depth / 2);
    if (part.taper) { // thickness shrinks linearly from the back (min x) to the front (max x)
      geo.computeBoundingBox();
      const { min, max } = geo.boundingBox, p = geo.attributes.position;
      for (let i = 0; i < p.count; i++) p.setZ(i, p.getZ(i) * (1 - part.taper * ((p.getX(i) - min.x) / (max.x - min.x || 1))));
      p.needsUpdate = true; geo.computeVertexNormals();
    }
    if (b > 0 && part.smooth > 0) geo = weldSmooth(geo);
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

function rebuildOne(id) {
  const part = state.parts.find((p) => p.id === id);
  if (!part || part.type === 'ribbon') return rebuild3D();
  const old = pieceGroup.getObjectByName(id);
  if (old) { old.geometry.dispose(); old.material.dispose(); pieceGroup.remove(old); }
  pieceGroup.add(buildPart(part));
}

function rebuild3D() {
  disposeGroup(pieceGroup);
  clearRibbons();
  pieceGroup.name = state.name;
  state.parts.forEach((p) => (p.type === 'ribbon' ? makeRibbon(p) : pieceGroup.add(buildPart(p))));
  root.updateMatrixWorld(true);
  ribbons.forEach((r) => { snapRibbon(r); updateRibbonMesh(r); });
}

// ------------------------------------------------------------- ribbons ----
// A ribbon is a chain of points (Verlet) pinned at an anchor on the piece. Each step every point keeps its
// momentum, is pulled gently toward its "rest" spot on a line behind the anchor, then segment lengths are
// restored. Because the chain lives in world space, moving the piece makes it lag and swing.
const _v1 = new THREE.Vector3(), _v2 = new THREE.Vector3(), _v3 = new THREE.Vector3();
const Y_AXIS = new THREE.Vector3(0, 1, 0);
let ribbons = [];

const ribbonDir = (part) => new THREE.Vector3(...part.dir).normalize().applyAxisAngle(Y_AXIS, THREE.MathUtils.degToRad(part.rotationY || 0));
const ribbonSide = (d) => { const w = Y_AXIS.clone().addScaledVector(d, -Y_AXIS.dot(d)); return (w.lengthSq() < 1e-4 ? w.set(1, 0, 0) : w).normalize(); };

function clearRibbons() { disposeGroup(ribbonGroup); ribbons = []; }

function makeRibbon(part) {
  const n = Math.max(2, Math.round(part.segments));
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(new Float32Array((n + 1) * 6), 3));
  const idx = [];
  for (let i = 0; i < n; i++) { const a = i * 2; idx.push(a, a + 1, a + 2, a + 1, a + 3, a + 2); }
  geo.setIndex(idx);
  const mesh = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ color: part.color, roughness: 0.6, side: THREE.DoubleSide }));
  mesh.name = part.id; mesh.frustumCulled = false;
  ribbonGroup.add(mesh);
  const r = { part, n, mesh, dirLocal: ribbonDir(part), anchor: new THREE.Vector3(), rest: new THREE.Vector3(), p: [], prev: [] };
  for (let i = 0; i <= n; i++) { r.p.push(new THREE.Vector3()); r.prev.push(new THREE.Vector3()); }
  ribbons.push(r);
  return r;
}

function ribbonFrame(r) {
  r.anchor.set(...r.part.offset).applyMatrix4(pieceGroup.matrixWorld);
  r.rest.copy(r.dirLocal).transformDirection(pieceGroup.matrixWorld);
}

function snapRibbon(r) {
  ribbonFrame(r);
  const seg = r.part.length / r.n;
  for (let i = 0; i <= r.n; i++) { r.p[i].copy(r.rest).multiplyScalar(seg * i).add(r.anchor); r.prev[i].copy(r.p[i]); }
}

function stepRibbon(r, h) {
  ribbonFrame(r);
  if (!physicsOn) return snapRibbon(r);
  const { p, prev, n, part } = r, seg = part.length / n;
  const k = part.stiffness * 0.25, damp = part.damping, g = part.gravity;
  p[0].copy(r.anchor); prev[0].copy(r.anchor);
  for (let i = 1; i <= n; i++) {
    _v1.copy(p[i]).sub(prev[i]).multiplyScalar(1 - damp);
    prev[i].copy(p[i]);
    p[i].add(_v1); p[i].y -= g * h * h;
    _v2.copy(r.rest).multiplyScalar(seg * i).add(r.anchor);
    p[i].lerp(_v2, k * (1 - 0.6 * (i / n)));
  }
  for (let it = 0; it < 3; it++) {
    for (let i = 1; i <= n; i++) {
      _v1.copy(p[i]).sub(p[i - 1]);
      p[i].copy(p[i - 1]).addScaledVector(_v1, seg / (_v1.length() || 1e-6));
    }
  }
}

function updateRibbonMesh(r) {
  const pos = r.mesh.geometry.attributes.position.array, { p, n, part } = r;
  _v3.copy(Y_AXIS).transformDirection(pieceGroup.matrixWorld);
  for (let i = 0; i <= n; i++) {
    _v1.copy(p[Math.min(i + 1, n)]).sub(p[Math.max(i - 1, 0)]).normalize();         // tangent
    _v2.copy(_v3).addScaledVector(_v1, -_v3.dot(_v1));                               // sheet's width axis ~ piece "up"
    if (_v2.lengthSq() < 1e-4) _v2.set(1, 0, 0).addScaledVector(_v1, -_v1.x);
    _v2.normalize().multiplyScalar(0.5 * THREE.MathUtils.lerp(part.width, part.tipWidth, i / n));
    const o = i * 6;
    pos[o] = p[i].x - _v2.x; pos[o + 1] = p[i].y - _v2.y; pos[o + 2] = p[i].z - _v2.z;
    pos[o + 3] = p[i].x + _v2.x; pos[o + 4] = p[i].y + _v2.y; pos[o + 5] = p[i].z + _v2.z;
  }
  r.mesh.geometry.attributes.position.needsUpdate = true;
  r.mesh.geometry.computeVertexNormals();
}

// Rest-pose skinned ribbon (one bone per point) for export / lineups. Unity & Godot can drive these bones
// with their own spring-bone physics.
function buildRibbonSkinned(part) {
  const n = Math.max(2, Math.round(part.segments)), seg = part.length / n, d = ribbonDir(part), w = ribbonSide(d);
  const off = new THREE.Vector3(...(part.offset || [0, 0, 0]));
  const holder = new THREE.Group(); holder.name = part.id;
  const bones = [];
  for (let i = 0; i <= n; i++) {
    const b = new THREE.Bone(); b.name = `${part.id}_b${i}`;
    b.position.copy(i === 0 ? off : d.clone().multiplyScalar(seg));
    if (i) bones[i - 1].add(b);
    bones.push(b);
  }
  const pos = [], si = [], sw = [], idx = [];
  for (let i = 0; i <= n; i++) {
    const c = off.clone().addScaledVector(d, seg * i), hw = 0.5 * THREE.MathUtils.lerp(part.width, part.tipWidth, i / n);
    for (const sgn of [-1, 1]) { const v = c.clone().addScaledVector(w, sgn * hw); pos.push(v.x, v.y, v.z); si.push(i, 0, 0, 0); sw.push(1, 0, 0, 0); }
  }
  for (let i = 0; i < n; i++) { const a = i * 2; idx.push(a, a + 1, a + 2, a + 1, a + 3, a + 2); }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  geo.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(si, 4));
  geo.setAttribute('skinWeight', new THREE.Float32BufferAttribute(sw, 4));
  geo.setIndex(idx); geo.computeVertexNormals();
  const mesh = new THREE.SkinnedMesh(geo, new THREE.MeshStandardMaterial({ color: part.color, roughness: 0.6, side: THREE.DoubleSide }));
  mesh.name = part.id + '_mesh';
  holder.add(bones[0], mesh); holder.updateMatrixWorld(true);
  mesh.bind(new THREE.Skeleton(bones));
  return holder;
}

// ----------------------------------------------- idle ("ready to fight") ----
// One loop = 2 hops, 1 sway, 1 breath. The piece dips (lands + squashes) at the far left and far right of the sway
// and rises as it passes through the middle. The bottom of the piece stays on the board; squash is about the base.
let idlePlaying = false, idleT = 0, physicsOn = true, simTime = 0, slide = null, moveMode = false;
const TAU = Math.PI * 2;
const idleLoopSeconds = (c) => 2 / Math.max(0.05, c.speed);

function idlePose(c, phase) {
  const s = Math.sin(TAU * phase), hop = Math.abs(Math.cos(TAU * phase)), land = Math.pow(1 - hop, 6);
  const sy = 1 - c.squash * land + c.breathe * s, sxz = 1 / Math.sqrt(sy), lean = THREE.MathUtils.degToRad(c.lean);
  return {
    x: c.sway * s, y: c.bounce * hop,
    rz: -lean * s,                                                                   // lean into the dip on each side
    rx: THREE.MathUtils.degToRad(1.5) + lean * 0.4 * Math.sin(2 * TAU * phase + 1),   // chin-forward stance + rock
    sx: sxz, sy, sz: sxz,
  };
}

function applyIdle() {
  if (!idlePlaying) { pieceGroup.position.set(0, 0, 0); pieceGroup.rotation.set(0, 0, 0); pieceGroup.scale.set(1, 1, 1); return; }
  const c = state.idle, k = idlePose(c, (idleT / idleLoopSeconds(c)) % 1);
  pieceGroup.position.set(k.x, k.y, 0); pieceGroup.rotation.set(k.rx, 0, k.rz); pieceGroup.scale.set(k.sx, k.sy, k.sz);
}

function idleClip(nodeName) {
  const c = state.idle, L = idleLoopSeconds(c), frames = Math.max(2, Math.round(L * 30));
  const times = [], pos = [], quat = [], scl = [], e = new THREE.Euler(), q = new THREE.Quaternion();
  for (let f = 0; f <= frames; f++) {
    const k = idlePose(c, f / frames);
    times.push((f / frames) * L); pos.push(k.x, k.y, 0);
    q.setFromEuler(e.set(k.rx, 0, k.rz)); quat.push(q.x, q.y, q.z, q.w); scl.push(k.sx, k.sy, k.sz);
  }
  return new THREE.AnimationClip('idle', L, [
    new THREE.VectorKeyframeTrack(`${nodeName}.position`, times, pos),
    new THREE.QuaternionKeyframeTrack(`${nodeName}.quaternion`, times, quat),
    new THREE.VectorKeyframeTrack(`${nodeName}.scale`, times, scl),
  ]);
}

// ---------------------------------------------------- slide test + loop ----
// 0 -> +1.2 -> 0 -> -1.2 -> 0 along x, to see ribbons trail. legs: [start, duration, from, to]
const SLIDE_LEGS = [[0.2, 0.6, 0, 1.2], [1.3, 0.6, 1.2, 0], [2.4, 0.6, 0, -1.2], [3.5, 0.6, -1.2, 0]];
const SLIDE_END = 4.2;
function slideOffset(t) {
  let x = 0;
  for (const [t0, d, a, b] of SLIDE_LEGS) if (t >= t0) { const u = Math.min(1, (t - t0) / d); x = a + (b - a) * u * u * (3 - 2 * u); }
  return x;
}

const H = 1 / 120; let acc = 0; // fixed physics step
function advance(dt) {
  acc += dt;
  while (acc >= H) {
    acc -= H; simTime += H;
    if (idlePlaying) idleT += H;
    if (slide) { const t = simTime - slide.t0; if (t > SLIDE_END) { root.position.x = slide.base; slide = null; } else root.position.x = slide.base + slideOffset(t); }
    applyIdle(); root.updateMatrixWorld(true);
    for (const r of ribbons) stepRibbon(r, H);
  }
  for (const r of ribbons) updateRibbonMesh(r);
}

// Shift + drag (mouse) or the Move piece toggle (touch) moves the piece around the floor so you can watch ribbons trail.
const ray = new THREE.Raycaster(), ndc = new THREE.Vector2(), floorPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0), hitP = new THREE.Vector3();
let grab = null;
function floorPoint(e) {
  const r = renderer.domElement.getBoundingClientRect();
  ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
  ray.setFromCamera(ndc, camera);
  return ray.ray.intersectPlane(floorPlane, hitP);
}
renderer.domElement.addEventListener('pointerdown', (e) => {
  if (!e.shiftKey && !moveMode) return;
  if (moveMode && !e.isPrimary) return;
  const pt = floorPoint(e); if (!pt) return;
  controls.enabled = false; e.stopImmediatePropagation();
  slide = null; grab = { dx: root.position.x - pt.x, dz: root.position.z - pt.z };
  renderer.domElement.setPointerCapture(e.pointerId);
}, true);
renderer.domElement.addEventListener('pointermove', (e) => {
  if (!grab) return;
  const pt = floorPoint(e); if (!pt) return;
  root.position.set(THREE.MathUtils.clamp(pt.x + grab.dx, -5, 5), 0, THREE.MathUtils.clamp(pt.z + grab.dz, -5, 5));
});
const endGrab = () => { grab = null; controls.enabled = true; };
renderer.domElement.addEventListener('pointerup', endGrab);
renderer.domElement.addEventListener('pointercancel', endGrab);

let lastT = performance.now(), frozen = false;
(function loop(now) {
  if (!frozen) advance(Math.min(0.05, (now - lastT) / 1000));
  lastT = now;
  controls.update(); renderer.render(scene, camera); requestAnimationFrame(loop);
})(performance.now());

// ------------------------------------------------------- 2D profile editor ----
const cv = document.getElementById('editor');
const ctx = cv.getContext('2d');
let drag = null;
const SCALE_UNITS = 1.9; // units visible vertically
const COARSE = matchMedia('(pointer:coarse)').matches;
const HANDLE = COARSE ? 8 : 5, HIT = COARSE ? 26 : 12;
let tool = 'edit'; // edit (add / move) | delete | corner
const edColors = () => {
  const cs = getComputedStyle(document.documentElement), c = (n) => cs.getPropertyValue('--ed-' + n).trim();
  return { bg: c('bg'), grid: c('grid'), text: c('text'), axis: c('axis'), ghost: c('ghost'), curve: c('curve'), point: c('point'), corner: c('corner'), drag: c('drag') };
};

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
  const f = frame(); const p = selPart(), C = edColors();
  ctx.setTransform(f.dpr, 0, 0, f.dpr, 0, 0);
  ctx.clearRect(0, 0, f.w, f.h);
  ctx.fillStyle = C.bg; ctx.fillRect(0, 0, f.w, f.h);
  ctx.strokeStyle = C.grid; ctx.lineWidth = 1; ctx.font = '10px sans-serif'; ctx.fillStyle = C.text;
  for (let v = -2; v <= 2; v += 0.25) {
    const [x] = toPx(f, v, 0); ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, f.h); ctx.stroke();
  }
  for (let v = 0; v <= SCALE_UNITS; v += 0.25) {
    const [, y] = toPx(f, 0, v); ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(f.w, y); ctx.stroke();
    if (v % 0.5 === 0) ctx.fillText(v.toFixed(1), 3, y - 2);
  }
  ctx.strokeStyle = C.axis; ctx.beginPath(); ctx.moveTo(f.ox, 0); ctx.lineTo(f.ox, f.h); ctx.stroke();
  if (!p) { ctx.fillStyle = C.text; ctx.fillText('Select or add a part', 12, 20); return; }
  if (p.type === 'ribbon') { ctx.fillStyle = C.text; ctx.fillText('Ribbon: shape it with the settings below, then use Slide test.', 12, 20); return; }
  const g = geomOf(p);
  const path = (p.type === 'lathe' ? latheProfile(p) : smoothOutline(p.outline, p.smooth)).map((v) => [v.x, v.y]);
  const trace = (mirror) => {
    ctx.beginPath();
    path.forEach((pt, i) => { const [x, y] = toPx(f, mirror ? -pt[0] : pt[0], pt[1]); i ? ctx.lineTo(x, y) : ctx.moveTo(x, y); });
    if (p.type === 'extrude') ctx.closePath();
  };
  if (p.type === 'lathe') { ctx.strokeStyle = C.ghost; trace(true); ctx.stroke(); }
  ctx.strokeStyle = C.curve; ctx.lineWidth = 2; trace(false); ctx.stroke(); ctx.lineWidth = 1;
  g.forEach((pt, i) => {
    const [x, y] = toPx(f, pt[0], pt[1]);
    ctx.beginPath();
    if (pt[2]) ctx.rect(x - HANDLE, y - HANDLE, HANDLE * 2, HANDLE * 2); else ctx.arc(x, y, HANDLE, 0, 7);
    ctx.fillStyle = drag && drag.i === i ? C.drag : pt[2] ? C.corner : C.point; ctx.fill();
  });
}

function evPos(e) { const r = cv.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; }
function hit(f, g, px, py, r = HIT) {
  let best = -1, bd = r;
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
  const p = selPart(); if (!p || p.type === 'ribbon' || e.button !== 0) return;
  const f = frame(), [px, py] = evPos(e), g = geomOf(p);
  let i = hit(f, g, px, py, e.pointerType === 'mouse' ? 12 : 26);
  if (tool === 'delete') { if (i >= 0 && g.length > 3) { pushUndo(); g.splice(i, 1); commit(true); } return; }
  if (tool === 'corner') { if (i >= 0) { pushUndo(); const pt = g[i]; pt[2] ? pt.splice(2, 1) : (pt[2] = 1); commit(true); } return; }
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
cv.addEventListener('pointercancel', () => { if (drag) { drag = null; commit(true); } });
cv.addEventListener('dblclick', (e) => { // toggle sharp corner
  const p = selPart(); if (!p || p.type === 'ribbon') return;
  const [px, py] = evPos(e), i = hit(frame(), geomOf(p), px, py); if (i < 0) return;
  pushUndo(); const pt = geomOf(p)[i]; pt[2] ? pt.splice(2, 1) : (pt[2] = 1); commit(true);
});
cv.addEventListener('contextmenu', (e) => { // delete point
  e.preventDefault();
  const p = selPart(); if (!p || p.type === 'ribbon') return;
  const g = geomOf(p), [px, py] = evPos(e), i = hit(frame(), g, px, py);
  if (i < 0 || g.length <= 3) return;
  pushUndo(); g.splice(i, 1); commit(true);
});
new ResizeObserver(drawEditor).observe(cv);
matchMedia('(prefers-color-scheme: light)').addEventListener?.('change', drawEditor);
new MutationObserver(drawEditor).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

// ------------------------------------------------------------ panel UI ----
const $ = (id) => document.getElementById(id);
const partsEl = $('parts'), jsonEl = $('json'), propsEl = $('props'), nameEl = $('name');

function pushUndo() { undoStack.push(JSON.stringify(state)); if (undoStack.length > 100) undoStack.shift(); }

// commit(true) = full UI refresh; commit(false) = lightweight (during drag)
let dragRaf = 0;
function commit(full = true) {
  if (!full) { // dragging a point: rebuild just that part, at most once per frame
    if (!dragRaf) dragRaf = requestAnimationFrame(() => { dragRaf = 0; rebuildOne(selectedId); drawEditor(); });
    return;
  }
  if (dragRaf) { cancelAnimationFrame(dragRaf); dragRaf = 0; }
  rebuild3D(); drawEditor();
  renderParts(); renderProps(); renderIdleProps();
  if (document.activeElement !== jsonEl) jsonEl.value = JSON.stringify(state, null, 2);
  saveDraft();
}

// Remember the piece in this browser so a phone reload or tab discard does not lose work.
const DRAFT_KEY = 'pieceLathe.draft.v1';
let draftTimer = 0;
function saveDraft() { clearTimeout(draftTimer); draftTimer = setTimeout(() => { try { localStorage.setItem(DRAFT_KEY, JSON.stringify(state)); } catch { /* storage unavailable */ } }, 400); }
function loadDraft() { try { const d = JSON.parse(localStorage.getItem(DRAFT_KEY) || 'null'); return d && Array.isArray(d.parts) && d.parts.length ? d : null; } catch { return null; } }

let toastTimer = 0;
function toast(msg, ms = 2600) { const t = $('toast'); t.textContent = msg; t.hidden = false; clearTimeout(toastTimer); toastTimer = setTimeout(() => (t.hidden = true), ms); }

function renderParts() {
  partsEl.innerHTML = '';
  state.parts.forEach((p) => {
    const b = document.createElement('button');
    b.className = 'part'; b.setAttribute('aria-pressed', String(p.id === selectedId));
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
  const p = selPart();
  $('selInfo').textContent = p ? `Editing "${p.id}" (${p.type}). Pick another part on the Parts tab.` : 'No part selected. Pick one on the Parts tab.';
  if (!p) return;
  p.offset = p.offset || [0, 0, 0];
  const idL = document.createElement('label'); idL.textContent = 'id';
  const idI = document.createElement('input'); idI.value = p.id;
  idI.onchange = () => { pushUndo(); p.id = idI.value || p.id; selectedId = p.id; commit(true); };
  idL.appendChild(idI); propsEl.appendChild(idL);
  const colL = document.createElement('label'); colL.textContent = 'color';
  const colI = document.createElement('input'); colI.type = 'color'; colI.value = p.color;
  colI.oninput = () => { p.color = colI.value; rebuild3D(); renderParts(); }; colI.onchange = () => { pushUndo(); commit(true); };
  colL.appendChild(colI); propsEl.appendChild(colL);
  const isRib = p.type === 'ribbon';
  ['x', 'y', 'z'].forEach((a, k) => propsEl.appendChild(numField((isRib ? 'anchor ' : 'offset ') + a, () => p.offset[k], (v) => (p.offset[k] = v))));
  if (!isRib) propsEl.appendChild(numField('scale', () => (Array.isArray(p.scale) ? p.scale[0] : p.scale ?? 1), (v) => (p.scale = v || 1)));
  propsEl.appendChild(numField('rotate Y°', () => p.rotationY || 0, (v) => (p.rotationY = v), 1));
  if (isRib) {
    ['x', 'y', 'z'].forEach((a, k) => propsEl.appendChild(numField('rest dir ' + a, () => p.dir[k], (v) => (p.dir[k] = v), 0.05)));
    propsEl.appendChild(numField('length', () => p.length, (v) => (p.length = Math.max(0.05, v))));
    propsEl.appendChild(numField('width', () => p.width, (v) => (p.width = Math.max(0.005, v))));
    propsEl.appendChild(numField('tip width', () => p.tipWidth, (v) => (p.tipWidth = Math.max(0, v))));
    propsEl.appendChild(numField('segments', () => p.segments, (v) => (p.segments = Math.max(2, Math.round(v))), 1));
    propsEl.appendChild(numField('stiffness', () => p.stiffness, (v) => (p.stiffness = Math.max(0, v)), 0.02));
    propsEl.appendChild(numField('damping', () => p.damping, (v) => (p.damping = Math.max(0, v)), 0.005));
    propsEl.appendChild(numField('gravity', () => p.gravity, (v) => (p.gravity = v), 0.5));
  } else if (p.type === 'lathe') {
    propsEl.appendChild(numField('smooth', () => p.smooth ?? 6, (v) => (p.smooth = Math.max(0, Math.round(v))), 1));
    propsEl.appendChild(numField('segments', () => p.segments ?? 48, (v) => (p.segments = Math.max(3, Math.round(v))), 1));
  } else {
    propsEl.appendChild(numField('depth', () => p.depth, (v) => (p.depth = Math.max(0.01, v))));
    propsEl.appendChild(numField('bevel', () => p.bevel, (v) => (p.bevel = Math.max(0, v))));
    propsEl.appendChild(numField('bevel segs', () => p.bevelSegments, (v) => (p.bevelSegments = Math.max(1, Math.round(v))), 1));
    propsEl.appendChild(numField('smooth', () => p.smooth, (v) => (p.smooth = Math.max(0, Math.round(v))), 1));
    propsEl.appendChild(numField('taper', () => p.taper, (v) => (p.taper = Math.min(0.95, Math.max(-0.95, v))), 0.05));
  }
}

function renderIdleProps() {
  const el = $('idleProps'); el.innerHTML = '';
  [['speed', 0.05], ['bounce', 0.005], ['sway', 0.005], ['lean °', 0.5, 'lean'], ['breathe', 0.002], ['squash', 0.01]].forEach(([label, step, key]) => {
    const k = key || label;
    el.appendChild(numField(label, () => state.idle[k], (v) => (state.idle[k] = v), step));
  });
}

function newPart(type) {
  const id = type + (state.parts.length + 1);
  if (type === 'ribbon') return { id, type, color: '#d1202b', offset: [0, 1, -0.2], ...clone(RIBBON_DEFAULTS) };
  return type === 'lathe'
    ? { id, type, color: '#cfd6e6', smooth: 6, profile: [[0, 0, 1], [0.4, 0], [0.3, 0.5], [0.4, 1], [0, 1, 1]] }
    : { id, type, color: '#cfd6e6', depth: 0.4, bevel: 0.04, outline: [[-0.4, 0], [0.4, 0], [0.4, 0.8], [0, 1.2], [-0.4, 0.8]] };
}

$('addLathe').onclick = () => api.addPart(newPart('lathe'));
$('addExtrude').onclick = () => api.addPart(newPart('extrude'));
$('addRibbon').onclick = () => api.addPart(newPart('ribbon'));
$('slide').onclick = () => api.slideTest();
$('physics').onchange = (e) => api.setPhysics(e.target.checked);
$('idleOn').onchange = (e) => api.setIdle(e.target.checked);
$('dupPart').onclick = () => { const p = selPart(); if (p) api.addPart({ ...clone(p), id: p.id + '_copy' }); };
$('delPart').onclick = () => selectedId && api.removePart(selectedId);
$('undo').onclick = () => api.undo();
$('applyJson').onclick = () => { try { api.setState(JSON.parse(jsonEl.value)); } catch (e) { alert('Bad JSON: ' + e.message); } };
$('exportGlb').onclick = async () => {
  try {
    const buf = await api.exportGLB({ idle: $('exportIdle').checked });
    // The viewer only saves a few file types, not .glb, so inside it the model goes out as a .zip holding the .glb.
    if (inViewer()) await saveFile(zipStore(fileBase() + '.glb', new Uint8Array(buf)), fileBase() + '.zip');
    else await saveFile(new Blob([buf], { type: 'model/gltf-binary' }), fileBase() + '.glb');
  } catch (e) { toast('Could not build the 3D model'); }
};
$('saveJson').onclick = () => saveFile(JSON.stringify(state, null, 2), fileBase() + '.json');
$('loadJson').onchange = async (e) => { const f = e.target.files[0]; if (f) api.setState(JSON.parse(await f.text())); e.target.value = ''; };
$('shot').onclick = () => saveFile(Uint8Array.from(atob(api.screenshot().split(',')[1]), (c) => c.charCodeAt(0)), fileBase() + '.png');
nameEl.onchange = () => { pushUndo(); state.name = nameEl.value || 'piece'; commit(true); };
document.querySelectorAll('[data-view]').forEach((b) => (b.onclick = () => setView(b.dataset.view)));
document.addEventListener('keydown', (e) => { if ((e.ctrlKey || e.metaKey) && e.key === 'z' && document.activeElement.tagName !== 'TEXTAREA') { e.preventDefault(); api.undo(); } });

const fileBase = () => (state.name || 'piece').replace(/[^\w.-]+/g, '_');
const inViewer = () => typeof window.claude?.use === 'function';

// ---- saving: in the Claude viewer through the `downloads` capability, elsewhere as a normal browser download.
const crcTable = (() => { const t = new Uint32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } return t; })();
function crc32(u8) { let c = 0xffffffff; for (let i = 0; i < u8.length; i++) c = crcTable[(c ^ u8[i]) & 0xff] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; }
function zipStore(filename, data) { // one-file zip, stored uncompressed
  const name = new TextEncoder().encode(filename), crc = crc32(data), n = data.length;
  const lh = new DataView(new ArrayBuffer(30)), ch = new DataView(new ArrayBuffer(46)), eo = new DataView(new ArrayBuffer(22));
  lh.setUint32(0, 0x04034b50, true); lh.setUint16(4, 20, true); lh.setUint16(6, 0x0800, true); lh.setUint16(12, 0x21, true);
  lh.setUint32(14, crc, true); lh.setUint32(18, n, true); lh.setUint32(22, n, true); lh.setUint16(26, name.length, true);
  ch.setUint32(0, 0x02014b50, true); ch.setUint16(4, 20, true); ch.setUint16(6, 20, true); ch.setUint16(8, 0x0800, true); ch.setUint16(14, 0x21, true);
  ch.setUint32(16, crc, true); ch.setUint32(20, n, true); ch.setUint32(24, n, true); ch.setUint16(28, name.length, true);
  eo.setUint32(0, 0x06054b50, true); eo.setUint16(8, 1, true); eo.setUint16(10, 1, true);
  eo.setUint32(12, 46 + name.length, true); eo.setUint32(16, 30 + name.length + n, true);
  const out = new Uint8Array(30 + name.length + n + 46 + name.length + 22); let o = 0;
  for (const part of [new Uint8Array(lh.buffer), name, data, new Uint8Array(ch.buffer), name, new Uint8Array(eo.buffer)]) { out.set(part, o); o += part.length; }
  return out;
}

async function saveFile(data, filename) {
  if (inViewer()) {
    let dl = null;
    try { dl = await window.claude.use('downloads'); } catch { /* no capability */ }
    if (!dl) { toast('Saving is not available in this view'); return false; }
    try { await dl.save({ filename, data }); toast('Saved ' + filename); return true; } catch (e) {
      const ext = filename.split('.').pop();
      toast(e?.code === 'declined' ? 'Save cancelled' : e?.code === 'rejected_extension' || e?.code === 'extension_not_enabled' ? `This view cannot save .${ext} files` : 'Could not save ' + filename);
      return false;
    }
  }
  const blob = data instanceof Blob ? data : new Blob([data]);
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = filename; a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  return true;
}

// ---- profile tools, move-piece toggle, tabs
document.querySelectorAll('#tools [data-tool]').forEach((b) => (b.onclick = () => {
  tool = b.dataset.tool;
  document.querySelectorAll('#tools [data-tool]').forEach((x) => x.setAttribute('aria-pressed', String(x === b)));
  cv.style.cursor = tool === 'edit' ? 'crosshair' : 'pointer';
}));
$('moveMode').onclick = () => {
  moveMode = !moveMode; $('moveMode').setAttribute('aria-pressed', String(moveMode));
  toast(moveMode ? 'Drag on the floor to move the piece' : 'Drag to orbit the camera');
};
function showTab(t) {
  $('panel').dataset.tab = t; $('panel').scrollTop = 0;
  document.querySelectorAll('.tabs [data-tab]').forEach((b) => b.setAttribute('aria-selected', String(b.dataset.tab === t)));
}
document.querySelectorAll('.tabs [data-tab]').forEach((b) => (b.onclick = () => showTab(b.dataset.tab)));

// The single-file build embeds the presets (window.__PRESETS__) because fetch() is blocked on file:// pages.
async function getJSON(url) {
  const embedded = window.__PRESETS__ && window.__PRESETS__[url.replace(/^pieces\//, '')];
  return embedded ? clone(embedded) : (await fetch(url)).json();
}

async function loadPresetList() {
  try {
    const list = await getJSON('pieces/index.json');
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
  async loadURL(url) { api.setState(await getJSON(url)); return api.getState(); },
  addPart(part) { pushUndo(); state.parts.push(part); state = normalize(state); selectedId = part.id; commit(true); },
  updatePart(id, patch) { const p = state.parts.find((x) => x.id === id); if (!p) throw new Error('no part ' + id); pushUndo(); Object.assign(p, clone(patch)); state = normalize(state); commit(true); },
  removePart(id) { pushUndo(); state.parts = state.parts.filter((p) => p.id !== id); if (selectedId === id) selectedId = state.parts[0]?.id ?? null; commit(true); },
  selectPart(id) { selectedId = id; commit(true); },
  setProfile(id, pts) { api.updatePart(id, { profile: pts }); },
  movePoint(id, i, a, b) { const p = state.parts.find((x) => x.id === id); pushUndo(); geomOf(p)[i][0] = a; geomOf(p)[i][1] = b; commit(true); },
  undo() { const s = undoStack.pop(); if (!s) return; state = JSON.parse(s); if (!state.parts.find((p) => p.id === selectedId)) selectedId = state.parts[0]?.id ?? null; nameEl.value = state.name; commit(true); },
  setView,
  screenshot: () => { renderer.render(scene, camera); return renderer.domElement.toDataURL('image/png'); },
  // Clean rest-pose export: static parts + skinned ribbons (bones <id>_b0..bN) + optional looping "idle" clip.
  async exportGLB({ idle = true } = {}) {
    const g = new THREE.Group(); g.name = state.name.replace(/[^\w-]/g, '_') || 'piece';
    state.parts.forEach((p) => g.add(p.type === 'ribbon' ? buildRibbonSkinned(p) : buildPart(p)));
    const animations = idle ? [idleClip(g.name)] : [];
    return new Promise((res, rej) => new GLTFExporter().parse(g, res, rej, { binary: true, animations }));
  },
  freeze(on) { frozen = !!on; },                        // stop the live clock (render.js steps time by hand)
  advance(seconds) { advance(seconds); },                 // step physics/idle deterministically (used by render.js)
  slideTest() { slide = { t0: simTime, base: root.position.x }; },
  setPhysics(on) { physicsOn = !!on; $('physics').checked = physicsOn; },
  setIdle(on, cfg) {
    if (cfg) Object.assign(state.idle, cfg);
    if (on && !idlePlaying) idleT = 0;
    idlePlaying = !!on; $('idleOn').checked = idlePlaying; applyIdle(); root.updateMatrixWorld(true);
    renderIdleProps();
  },
  resetPiecePosition() { root.position.set(0, 0, 0); slide = null; },
  // Show several pieces side by side (for comparing a variant against the original). setState() returns to editing.
  lineup(states, gap = 1.5) {
    disposeGroup(pieceGroup); clearRibbons(); api.setIdle(false);
    const n = states.length;
    states.forEach((s, k) => {
      const g = new THREE.Group(); normalizeInto(s).parts.forEach((p) => g.add(p.type === 'ribbon' ? buildRibbonSkinned(p) : buildPart(p)));
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
  const draft = q.get('piece') ? null : loadDraft();
  if (draft) { api.setState(draft); toast('Restored your last piece. Open a preset to start over.', 3600); }
  else { try { await api.loadURL('pieces/' + (q.get('piece') || 'pawn.json')); } catch { api.setState({ name: 'piece', parts: [newPart('lathe')] }); } }
  undoStack.length = 0;
  window.pieceLatheReady = true;
})();
