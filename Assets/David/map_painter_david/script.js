'use strict';

// ============================================================
//  FLOATING ISLAND MAP PAINTER
// ============================================================

const DEFAULT_CANVAS_PX = 1536;
const DEFAULT_CELL_SIZE = 32;
const GRID_MIN_SIZE = 8;
const GRID_MAX_SIZE = 256;
const ZOOM_MIN = 0.4;
const ZOOM_MAX = 8.0;
const ZOOM_STEP = 0.1;
const SKY_COLOR = '#B3D9EA';

const CATEGORIES = ['groundTypes','decalTypes','resourceTypes','npcTypes','largeSpriteTypes'];
const CAT_LABELS = {groundTypes:'Ground',decalTypes:'Decal',resourceTypes:'Resource',npcTypes:'NPC',largeSpriteTypes:'Large Sprite'};
const CAT_PLURALS = {groundTypes:'Ground Types',decalTypes:'Decal Types',resourceTypes:'Resource Types',npcTypes:'NPC Types',largeSpriteTypes:'Large Sprite Types'};
const TYPE_ID_BASES = {
  groundTypes: 0,
  decalTypes: 1000,
  resourceTypes: 2000,
  npcTypes: 3000,
  largeSpriteTypes: 4000,
};
const TYPE_NID_KEYS = {
  groundTypes: 'gT',
  decalTypes: 'dT',
  resourceTypes: 'rT',
  npcTypes: 'nT',
  largeSpriteTypes: 'lT',
};
const TOOL_TO_CATEGORY = {
  ground: 'groundTypes',
  decal: 'decalTypes',
  resource: 'resourceTypes',
  npc: 'npcTypes',
  largeSprite: 'largeSpriteTypes',
};
const CATEGORY_TO_TOOL = {
  groundTypes: 'ground',
  decalTypes: 'decal',
  resourceTypes: 'resource',
  npcTypes: 'npc',
  largeSpriteTypes: 'largeSprite',
};
const TOOL_ICON_MAP = {
  ground: '🟫',
  decal: '💠',
  resource: '⛏️',
  npc: '🧍',
  largeSprite: '🖼️',
  eraser: '🧽',
};
const RECENT_MAX_PER_CATEGORY = 8;
const DATA_CURRENT_MAP_FILE = 'current-map.json';
const DATA_PALETTE_FILE = 'palette.json';
const DATA_MAPS_DIR = 'maps';
const BUNDLED_PALETTE_PATH = 'data/palette.json';
const PLAYER_START_STRING_KEY = 'npc_player_start';
const PLAYER_START_FALLBACK_ID = TYPE_ID_BASES.npcTypes;
const DATA_HANDLE_DB = 'mapPainterDataFolderDb';
const DATA_HANDLE_STORE = 'handles';
const DATA_HANDLE_KEY = 'defaultDataFolder';

// ---- Global State ----
let ST = null; // set in init
let canvas, ctx;
let dirty = false;
let autosaveTimer = null;

const HISTORY_LIMIT = 100;
const HISTORY_COALESCE_MS = 350;
let undoStack = [];
let redoStack = [];
let historyLocked = false;
let historyGroupKey = null;
let historyGroupRecorded = false;
let lastHistoryKey = '';
let lastHistoryAt = 0;
let panState = {
  active: false,
  startX: 0,
  startY: 0,
  startScrollLeft: 0,
  startScrollTop: 0,
};
let resizeState = {
  active: false,
  mode: null,
  startX: 0,
  startY: 0,
  startW: 0,
  startH: 0,
};

function createRecentToolsState() {
  return {
    groundTypes: [],
    decalTypes: [],
    resourceTypes: [],
    npcTypes: [],
    largeSpriteTypes: [],
    eraser: [],
  };
}

function makeDefaultMapData() {
  const defaultGrid = Math.floor(DEFAULT_CANVAS_PX / DEFAULT_CELL_SIZE);
  return {
    version: 1,
    mapName: 'Floating Island Map',
    canvas: { size: DEFAULT_CANVAS_PX, cellSize: DEFAULT_CELL_SIZE, gridWidth: defaultGrid, gridHeight: defaultGrid },
    toolset: {
      groundTypes: [ {id:TYPE_ID_BASES.groundTypes,name:'Ground 0',stringKey:'',color:'#72B86A',enabled:true} ],
      decalTypes: [ {id:TYPE_ID_BASES.decalTypes,name:'Decal 1000',stringKey:'',color:'#2E7D32',enabled:true} ],
      resourceTypes: [ {id:TYPE_ID_BASES.resourceTypes,name:'Resource 2000',stringKey:'',color:'#4DD4AC',enabled:true} ],
      npcTypes: [ {id:TYPE_ID_BASES.npcTypes,name:'NPC 3000',stringKey:'',color:'#FFD166',enabled:true} ],
      largeSpriteTypes: [ {id:TYPE_ID_BASES.largeSpriteTypes,name:'Large Sprite 4000',stringKey:'',color:'#B983FF',width:2,height:2,enabled:true} ],
    },
    islands: [],
    layers: { decals:[], resources:[], npcs:[], largeSprites:[] },
    _nid: {
      gT: TYPE_ID_BASES.groundTypes + 1,
      dT: TYPE_ID_BASES.decalTypes + 1,
      rT: TYPE_ID_BASES.resourceTypes + 1,
      nT: TYPE_ID_BASES.npcTypes + 1,
      lT: TYPE_ID_BASES.largeSpriteTypes + 1,
      isl:1,
      dI:1,
      rI:1,
      nI:1,
      lI:1,
    }
  };
}

function makeDefaultState() {
  return {
    mode: 'paint',
    defCat: 'groundTypes',
    tool: 'ground',
    eraserTarget: 'ground',
    activeTypeId: 0,
    activeIslandId: null,
    vis: { ground:true, decals:true, resources:true, npcs:true, largeSprites:true, outlines:true, grid:true },
    hx: -1, hy: -1,
    painting: false,
    paintBtn: 0,
    zoom: 1,
    recentTools: createRecentToolsState(),
    recentPaletteCollapsed: false,
    dataFolderHandle: null,
    dataFolderName: '',
    folderAutosaveWarned: false,
    md: makeDefaultMapData(),
    gl: new Map(), // groundLookup "x,y" -> {islandId, typeId}
    il: new Map(), // islandLookup "x,y" -> islandId
    ol: new Map(), // occupantLookup "x,y" -> {cat, instId, typeId, islandId}
    status: '', statusType: 'info', statusTimer: null,
    genSettings: {
      islandCount: 3,
      radiusMin: 4,
      radiusMax: 12,
      seed: 42,
    },
  };
}

// ---- Utilities ----
function clamp(v,lo,hi){ return Math.max(lo,Math.min(hi,v)); }
function pk(x,y){ return x+','+y; }
function escHtml(s){ return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

function ensureRecentToolsState() {
  if(!ST.recentTools || typeof ST.recentTools !== 'object') {
    ST.recentTools = createRecentToolsState();
    return;
  }
  for(const key of Object.keys(createRecentToolsState())) {
    if(!Array.isArray(ST.recentTools[key])) ST.recentTools[key] = [];
  }
}

function pushRecentTool(tool, typeId) {
  ensureRecentToolsState();

  let category = null;
  let value = null;

  if(tool === 'eraser') {
    category = 'eraser';
    value = 'eraser';
  } else {
    category = TOOL_TO_CATEGORY[tool] || null;
    if(!category) return;
    if(!Number.isFinite(typeId)) return;
    value = typeId;
  }

  const list = ST.recentTools[category];
  const idx = list.indexOf(value);
  if(idx !== -1) list.splice(idx, 1);
  list.unshift(value);
  if(list.length > RECENT_MAX_PER_CATEGORY) list.length = RECENT_MAX_PER_CATEGORY;

  renderRecentPalette();
}

function toggleRecentPalette() {
  ST.recentPaletteCollapsed = !ST.recentPaletteCollapsed;
  renderRecentPalette();
}

function renderRecentPalette() {
  const panel = document.getElementById('recentToolsPalette');
  const content = document.getElementById('recentToolsContent');
  const toggleBtn = document.getElementById('recentToolsToggle');
  if(!panel || !content || !ST) return;

  if(ST.mode !== 'paint') {
    panel.style.display = 'none';
    return;
  }

  ensureRecentToolsState();
  panel.style.display = 'block';
  panel.style.width = ST.recentPaletteCollapsed ? '170px' : '256px';
  if(toggleBtn){
    toggleBtn.textContent = ST.recentPaletteCollapsed ? '▸' : '▾';
    toggleBtn.title = ST.recentPaletteCollapsed ? 'Expand recent tools' : 'Collapse recent tools';
  }

  if(ST.recentPaletteCollapsed){
    content.innerHTML = '<div class="text-[11px] text-gray-400">Collapsed</div>';
    return;
  }

  const sections = [
    { category: 'groundTypes', label: 'Ground', tool: 'ground' },
    { category: 'decalTypes', label: 'Decal', tool: 'decal' },
    { category: 'resourceTypes', label: 'Resource', tool: 'resource' },
    { category: 'npcTypes', label: 'NPC', tool: 'npc' },
    { category: 'largeSpriteTypes', label: 'Sprite', tool: 'largeSprite' },
    { category: 'eraser', label: 'Eraser', tool: 'eraser' },
  ];

  let h = '';
  for(const section of sections) {
    const icon = TOOL_ICON_MAP[section.tool] || '•';
    h += '<div class="mb-2">';
    h += '<div class="text-[10px] uppercase tracking-wide text-gray-400 mb-1">' + icon + ' ' + escHtml(section.label) + '</div>';
    h += '<div class="grid grid-cols-2 gap-1">';

    if(section.category === 'eraser') {
      const active = ST.tool === 'eraser';
      h += '<button onclick="S.selectRecentTool(\'eraser\', \'eraser\')" class="px-2 py-1 rounded text-[11px] border ' +
        (active ? 'bg-indigo-600 border-indigo-400 text-white' : 'bg-gray-900 border-gray-700 hover:bg-gray-700 text-gray-200') +
        '">🧽 Erase</button>';
    } else {
      const validIds = [];
      for(const id of ST.recentTools[section.category]) {
        const t = findType(section.category, id);
        if(t) validIds.push(id);
      }
      ST.recentTools[section.category] = validIds;

      if(validIds.length === 0) {
        h += '<div class="col-span-2 text-[10px] text-gray-500 italic">No recent</div>';
      } else {
        for(const id of validIds) {
          const t = findType(section.category, id);
          if(!t) continue;
          const active = ST.tool === section.tool && ST.activeTypeId === id;
          h += '<button onclick="S.selectRecentTool(\'' + section.category + '\',' + id + ')" class="flex items-center gap-1 px-1.5 py-1 rounded text-[11px] border ' +
            (active ? 'bg-indigo-600 border-indigo-400 text-white' : 'bg-gray-900 border-gray-700 hover:bg-gray-700 text-gray-200') +
            '">';
          h += '<span class="inline-block w-2.5 h-2.5 rounded" style="background:' + t.color + '"></span>';
          h += '<span class="truncate">' + escHtml(t.name) + '</span>';
          h += '</button>';
        }
      }
    }

    h += '</div></div>';
  }

  content.innerHTML = h;
}

function renderPlayerScaleIndicator() {
  const panel = document.getElementById('playerScaleIndicator');
  const canvasEl = document.getElementById('playerScaleCanvas');
  const textEl = document.getElementById('playerScaleText');
  if(!panel || !canvasEl || !textEl || !ST) return;

  if(ST.mode !== 'paint') {
    panel.style.display = 'none';
    return;
  }
  panel.style.display = 'block';

  const ctx2 = canvasEl.getContext('2d');
  if(!ctx2) return;

  const logicalTile = Math.max(1, ST.md.canvas.cellSize || DEFAULT_CELL_SIZE);
  const zoom = Math.max(0.01, ST.zoom || 1);
  const drawTile = Math.max(4, logicalTile * zoom);
  const drawPlayer = drawTile * 0.5;
  const padding = 8;
  const side = Math.max(48, Math.ceil(drawTile + padding * 2));

  if(canvasEl.width !== side) canvasEl.width = side;
  if(canvasEl.height !== side) canvasEl.height = side;

  const ox = Math.floor((side - drawTile) / 2);
  const oy = Math.floor((side - drawTile) / 2);
  const px = Math.floor((side - drawPlayer) / 2);
  const py = Math.floor((side - drawPlayer) / 2);

  ctx2.clearRect(0, 0, canvasEl.width, canvasEl.height);
  ctx2.fillStyle = '#0f172a';
  ctx2.fillRect(0, 0, canvasEl.width, canvasEl.height);

  // Tile footprint at current on-screen zoom scale.
  ctx2.fillStyle = '#334155';
  ctx2.fillRect(ox, oy, drawTile, drawTile);
  ctx2.strokeStyle = '#94a3b8';
  ctx2.lineWidth = 1;
  ctx2.strokeRect(ox + 0.5, oy + 0.5, drawTile, drawTile);

  // Player footprint = 50% of tile.
  ctx2.fillStyle = '#22c55e';
  ctx2.fillRect(px, py, drawPlayer, drawPlayer);
  ctx2.strokeStyle = 'rgba(255,255,255,0.75)';
  ctx2.strokeRect(px + 0.5, py + 0.5, drawPlayer, drawPlayer);

  textEl.textContent =
    'Tile: ' + drawTile.toFixed(1) + 'px | Player: ' + drawPlayer.toFixed(1) + 'px';
}

function selectRecentTool(category, value) {
  ST.mode = 'paint';

  if(category === 'eraser') {
    ST.tool = 'eraser';
    pushRecentTool('eraser');
    renderSidebar();
    renderCanvas();
    return;
  }

  const tool = CATEGORY_TO_TOOL[category];
  const id = parseInt(value, 10);
  if(!tool || !Number.isFinite(id)) return;

  const type = findType(category, id);
  if(!type) {
    warn('Recent tool no longer exists');
    renderRecentPalette();
    return;
  }

  ST.tool = tool;
  ST.activeTypeId = id;
  pushRecentTool(tool, id);
  renderSidebar();
  renderCanvas();
}

function setTypeStatus(msg, type) {
  ST.status = msg; ST.statusType = type||'info';
  const el = document.getElementById('statusArea');
  if(el){
    el.textContent = msg;
    el.className = 'text-xs max-w-xs truncate ' + ({
      info:'text-gray-400', warning:'text-yellow-400', error:'text-red-400', success:'text-green-400'
    }[type]||'text-gray-400');
  }
  clearTimeout(ST.statusTimer);
  ST.statusTimer = setTimeout(()=>{ ST.status=''; const e2=document.getElementById('statusArea'); if(e2) e2.textContent=''; }, 4000);
}

function warn(msg){ setTypeStatus(msg,'warning'); }
function err(msg){ setTypeStatus(msg,'error'); }
function info(msg){ setTypeStatus(msg,'info'); }
function ok(msg){ setTypeStatus(msg,'success'); }

function getCanvasWidthPx(){
  if(!ST || !ST.md || !ST.md.canvas) return DEFAULT_CANVAS_PX;
  return Math.max(64, (ST.md.canvas.gridWidth || 1) * (ST.md.canvas.cellSize || DEFAULT_CELL_SIZE));
}

function getCanvasHeightPx(){
  if(!ST || !ST.md || !ST.md.canvas) return DEFAULT_CANVAS_PX;
  return Math.max(64, (ST.md.canvas.gridHeight || 1) * (ST.md.canvas.cellSize || DEFAULT_CELL_SIZE));
}

function applyCanvasZoom(){
  if(!canvas || !ST) return;
  const pxW = getCanvasWidthPx();
  const pxH = getCanvasHeightPx();
  const zoom = clamp(ST.zoom || 1, ZOOM_MIN, ZOOM_MAX);
  canvas.style.width = Math.round(pxW * zoom) + 'px';
  canvas.style.height = Math.round(pxH * zoom) + 'px';

  const zoomLabel = document.getElementById('zoomLabel');
  if(zoomLabel){
    zoomLabel.textContent = Math.round(zoom * 100) + '%';
  }
}

function setZoom(nextZoom, anchorClientX, anchorClientY){
  if(!ST || !canvas) return;
  const viewport = document.getElementById('canvasViewport');
  const oldRect = canvas.getBoundingClientRect();

  const targetZoom = clamp(nextZoom, ZOOM_MIN, ZOOM_MAX);
  if(Math.abs(targetZoom - ST.zoom) < 0.0001) return;

  const hasAnchor = typeof anchorClientX === 'number' && typeof anchorClientY === 'number';
  const ax = hasAnchor ? anchorClientX : (oldRect.left + oldRect.width / 2);
  const ay = hasAnchor ? anchorClientY : (oldRect.top + oldRect.height / 2);
  const relX = oldRect.width > 0 ? (ax - oldRect.left) / oldRect.width : 0.5;
  const relY = oldRect.height > 0 ? (ay - oldRect.top) / oldRect.height : 0.5;

  ST.zoom = targetZoom;
  applyCanvasZoom();

  if(viewport){
    const newRect = canvas.getBoundingClientRect();
    const nextAx = newRect.left + newRect.width * relX;
    const nextAy = newRect.top + newRect.height * relY;
    viewport.scrollLeft += (nextAx - ax);
    viewport.scrollTop += (nextAy - ay);
  }

  // Re-render so zoom-dependent overlays (like grid density/thickness) stay correct.
  renderCanvas();
}

function zoomIn(){ setZoom((ST && ST.zoom ? ST.zoom : 1) + ZOOM_STEP); }
function zoomOut(){ setZoom((ST && ST.zoom ? ST.zoom : 1) - ZOOM_STEP); }
function zoomReset(){ setZoom(1); }

function deepClone(obj){
  return JSON.parse(JSON.stringify(obj));
}

function captureSnapshot(){
  return {
    md: deepClone(ST.md),
    recentTools: deepClone(ST.recentTools),
    mode: ST.mode,
    defCat: ST.defCat,
    tool: ST.tool,
    eraserTarget: ST.eraserTarget,
    activeTypeId: ST.activeTypeId,
    activeIslandId: ST.activeIslandId,
    vis: deepClone(ST.vis),
  };
}

function updateHistoryButtons(){
  const undoBtn = document.getElementById('undoBtn');
  const redoBtn = document.getElementById('redoBtn');
  if(undoBtn){
    const disabled = undoStack.length === 0;
    undoBtn.disabled = disabled;
    undoBtn.classList.toggle('opacity-50', disabled);
    undoBtn.classList.toggle('cursor-not-allowed', disabled);
  }
  if(redoBtn){
    const disabled = redoStack.length === 0;
    redoBtn.disabled = disabled;
    redoBtn.classList.toggle('opacity-50', disabled);
    redoBtn.classList.toggle('cursor-not-allowed', disabled);
  }
}

function pushUndoSnapshot(snapshot){
  undoStack.push(snapshot);
  if(undoStack.length > HISTORY_LIMIT){
    undoStack.shift();
  }
  redoStack = [];
  updateHistoryButtons();
}


function beginHistoryGroup(key){
  historyGroupKey = key;
  historyGroupRecorded = false;
}

function endHistoryGroup(){
  historyGroupKey = null;
  historyGroupRecorded = false;
}

function recordMutation(key){
  if(historyLocked) return;

  if(historyGroupKey){
    if(historyGroupRecorded) return;
    pushUndoSnapshot(captureSnapshot());
    historyGroupRecorded = true;
    lastHistoryKey = historyGroupKey;
    lastHistoryAt = Date.now();
    return;
  }

  const now = Date.now();
  if(key === lastHistoryKey && (now - lastHistoryAt) < HISTORY_COALESCE_MS){
    lastHistoryAt = now;
    return;
  }

  pushUndoSnapshot(captureSnapshot());
  lastHistoryKey = key;
  lastHistoryAt = now;
}

function restoreSnapshot(snapshot){
  historyLocked = true;
  ST.md = deepClone(snapshot.md);
  ST.recentTools = deepClone(snapshot.recentTools || createRecentToolsState());
  ST.mode = snapshot.mode;
  ST.defCat = snapshot.defCat;
  ST.tool = snapshot.tool;
  ST.eraserTarget = snapshot.eraserTarget;
  ST.activeTypeId = snapshot.activeTypeId;
  ST.activeIslandId = snapshot.activeIslandId;
  ST.vis = deepClone(snapshot.vis);
  rebuildLookups();
  const inp = document.getElementById('mapNameInput');
  if(inp) inp.value = ST.md.mapName;
  renderSidebar();
  renderCanvas();
  renderRecentPalette();
  renderPlayerScaleIndicator();
  if(ST.hx >= 0 && ST.hy >= 0){
    updateCellInfo(ST.hx, ST.hy);
  }
  historyLocked = false;
}

function undo(){
  if(undoStack.length === 0){
    warn('Nothing to undo');
    return;
  }
  const current = captureSnapshot();
  const previous = undoStack.pop();
  redoStack.push(current);
  restoreSnapshot(previous);
  dirty = true;
  updateHistoryButtons();
  info('Undo');
}

function redo(){
  if(redoStack.length === 0){
    warn('Nothing to redo');
    return;
  }
  const current = captureSnapshot();
  const next = redoStack.pop();
  undoStack.push(current);
  restoreSnapshot(next);
  dirty = true;
  updateHistoryButtons();
  info('Redo');
}

function handleUndoRedoHotkeys(e){
  const mod = e.ctrlKey || e.metaKey;
  if(!mod) return;

  const target = e.target;
  const tag = target && target.tagName ? target.tagName.toLowerCase() : '';
  const isEditingField = tag === 'input' || tag === 'textarea' || (target && target.isContentEditable);
  if(isEditingField) return;

  const k = (e.key || '').toLowerCase();
  if(k === 'z'){
    e.preventDefault();
    if(e.shiftKey) redo();
    else undo();
  } else if(k === 'y'){
    e.preventDefault();
    redo();
  }
}

// ---- Lookup rebuild ----
function rebuildLookups() {
  ST.gl.clear(); ST.il.clear(); ST.ol.clear();
  for (const isl of ST.md.islands) {
    for (const t of isl.tiles) {
      const k = pk(t.x, t.y);
      ST.gl.set(k, {islandId:isl.id, typeId:t.ground});
      ST.il.set(k, isl.id);
    }
  }
  const layerCats = {decals:'decalTypes',resources:'resourceTypes',npcs:'npcTypes',largeSprites:'largeSpriteTypes'};
  for (const [layerKey, cat] of Object.entries(layerCats)) {
    for (const inst of ST.md.layers[layerKey]) {
      if (cat === 'largeSpriteTypes') {
        const ls = inst;
        for (let dy=0; dy<ls.height; dy++) {
          for (let dx=0; dx<ls.width; dx++) {
            ST.ol.set(pk(ls.x+dx, ls.y+dy), {cat:layerKey, instId:ls.id, typeId:ls.typeId, islandId:ls.islandId});
          }
        }
      } else {
        ST.ol.set(pk(inst.x, inst.y), {cat:layerKey, instId:inst.id, typeId:inst.typeId, islandId:inst.islandId});
      }
    }
  }
}

// ---- Type Management ----
function getTypes(cat){ return ST.md.toolset[cat]; }
function findType(cat, id){ return ST.md.toolset[cat].find(t=>t.id===id); }
function nextTypeId(cat){
  const key = TYPE_NID_KEYS[cat];
  const base = TYPE_ID_BASES[cat] || 0;
  if(ST.md._nid[key] < base){
    ST.md._nid[key] = base;
  }
  const n = ST.md._nid[key]++;
  // ensure no collision
  while(findType(cat,n)) { ST.md._nid[key]++; return ST.md._nid[key]-1; }
  return n;
}
function addType(cat) {
  recordMutation('add-type');
  const id = nextTypeId(cat);
  const idx = ST.md.toolset[cat].length;
  const names = {groundTypes:'Ground',decalTypes:'Decal',resourceTypes:'Resource',npcTypes:'NPC',largeSpriteTypes:'Large Sprite'};
  const colors = {groundTypes:'#72B86A',decalTypes:'#2E7D32',resourceTypes:'#4DD4AC',npcTypes:'#FFD166',largeSpriteTypes:'#B983FF'};
  const t = {id, name:names[cat]+' '+id, stringKey:'', color:colors[cat], enabled:true};
  if(cat==='largeSpriteTypes'){ t.width=2; t.height=2; }
  ST.md.toolset[cat].push(t);
  dirty=true;
  renderSidebar();
}
function updateTypeName(cat, id, val) {
  const t = findType(cat, id);
  if(t){ recordMutation('type-name'); t.name=val; dirty=true; }
}
function updateTypeColor(cat, id, val) {
  const t = findType(cat, id);
  if(t){ recordMutation('type-color'); t.color=val; dirty=true; renderCanvas(); }
}
function updateTypeStringKey(cat, id, val) {
  const t = findType(cat, id);
  if(t){ recordMutation('type-key'); t.stringKey=val; dirty=true; }
}
function updateTypeWidth(cat, id, val) {
  const t = findType(cat, id);
  if(t){ recordMutation('type-width'); t.width=Math.max(1,parseInt(val)||1); dirty=true; renderCanvas(); }
}
function updateTypeHeight(cat, id, val) {
  const t = findType(cat, id);
  if(t){ recordMutation('type-height'); t.height=Math.max(1,parseInt(val)||1); dirty=true; renderCanvas(); }
}
function toggleTypeEnabled(cat, id) {
  const t = findType(cat, id);
  if(t){ recordMutation('type-enabled'); t.enabled=!t.enabled; dirty=true; renderSidebar(); renderCanvas(); }
}
function deleteType(cat, id) {
  const t = findType(cat, id);
  if(!t) return;
  if(!confirm('Delete "'+t.name+'" (ID:'+id+')? This type will be removed from the toolset. Existing references may become invalid.')) return;
  recordMutation('delete-type');
  ST.md.toolset[cat] = ST.md.toolset[cat].filter(x=>x.id!==id);
  dirty=true;
  renderSidebar(); renderCanvas();
}

// ---- Island Management ----
function nextIslandId() {
  const n = ST.md._nid.isl++;
  const id = 'island_'+String(n).padStart(3,'0');
  // ensure unique
  while(ST.md.islands.find(i=>i.id===id)){ ST.md._nid.isl++; return 'island_'+String(ST.md._nid.isl-1).padStart(3,'0'); }
  return id;
}
function createIsland() {
  recordMutation('create-island');
  const id = nextIslandId();
  const colors = ['#FFFFFF','#FF6B6B','#51CF66','#339AF0','#FCC419','#CC5DE8','#22B8CF','#FF922B'];
  const ci = ST.md.islands.length % colors.length;
  const isl = {id, name:'Island '+ST.md.islands.length, outlineColor:colors[ci], tiles:[]};
  ST.md.islands.push(isl);
  ST.activeIslandId = id;
  dirty=true;
  renderSidebar(); renderCanvas();
  ok('Island created: '+isl.name);
}
function getActiveIsland(){ return ST.md.islands.find(i=>i.id===ST.activeIslandId)||null; }
function selectIsland(id){ ST.activeIslandId=id; renderSidebar(); renderCanvas(); }
function renameIsland(id, name){
  const isl = ST.md.islands.find(i=>i.id===id);
  if(isl){ recordMutation('rename-island'); isl.name=name; dirty=true; }
}
function changeIslandOutlineColor(id, color){
  const isl = ST.md.islands.find(i=>i.id===id);
  if(isl){ recordMutation('island-outline'); isl.outlineColor=color; dirty=true; renderCanvas(); }
}
function deleteIsland(id){
  const isl = ST.md.islands.find(i=>i.id===id);
  if(!isl) return;
  if(!confirm('Delete island "'+isl.name+'"? All ground tiles and associated occupants will be removed.')) return;
  recordMutation('delete-island');
  // remove tiles
  const tileKeys = new Set(isl.tiles.map(t=>pk(t.x,t.y)));
  // remove occupants referencing this island
  for(const lk of Object.keys(ST.md.layers)){
    ST.md.layers[lk] = ST.md.layers[lk].filter(inst => inst.islandId !== id);
  }
  // remove large sprites with cells in this island
  ST.md.layers.largeSprites = ST.md.layers.largeSprites.filter(ls=>{
    for(let dy=0;dy<ls.height;dy++) for(let dx=0;dx<ls.width;dx++) if(tileKeys.has(pk(ls.x+dx,ls.y+dy))) return false;
    return true;
  });
  ST.md.islands = ST.md.islands.filter(i=>i.id!==id);
  if(ST.activeIslandId===id) ST.activeIslandId = ST.md.islands.length?ST.md.islands[0].id:null;
  dirty=true;
  rebuildLookups(); renderSidebar(); renderCanvas();
}

// ---- Procedural Island Generation ----
function genSeededRng(seed) {
  let s = seed | 0;
  return {
    next() { s = Math.imul(1664525, s) + 1013904223 | 0; return (s >>> 0) / 4294967296; },
    int(a, b) { return Math.floor(this.next() * (b - a)) + a; },
  };
}

function genShuffle(arr, rng) {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = rng.int(0, i + 1);
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

function genPointInPoly(px, py, poly) {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
    if (((yi > py) !== (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi)) inside = !inside;
  }
  return inside;
}

function genPoly(cx, cy, r, samples, jitter, rng) {
  let drift = 0;
  const pts = [];
  for (let i = 0; i < samples; i++) {
    const theta = 2 * Math.PI * i / samples;
    drift += rng.next() * jitter * 2 - jitter;
    drift *= 0.9;
    let lr = r * (1 + drift);
    lr = clamp(lr, r * 0.65, r * 1.4);
    pts.push([cx + Math.cos(theta) * lr, cy + Math.sin(theta) * lr]);
  }
  return pts;
}

function collectPolyTiles(poly, W, H) {
  const xs = poly.map(p => p[0]);
  const ys = poly.map(p => p[1]);
  const x0 = Math.max(0, Math.floor(Math.min(...xs)));
  const x1 = Math.min(W - 1, Math.ceil(Math.max(...xs)));
  const y0 = Math.max(0, Math.floor(Math.min(...ys)));
  const y1 = Math.min(H - 1, Math.ceil(Math.max(...ys)));
  const tiles = [];
  for (let y = y0; y <= y1; y++) {
    for (let x = x0; x <= x1; x++) {
      if (genPointInPoly(x + 0.5, y + 0.5, poly)) tiles.push({ x, y });
    }
  }
  return tiles;
}

function placeDispersedIslandCenters(W, H, n, radii, maxRadius, rng) {
  const cols = Math.max(1, Math.ceil(Math.sqrt(n * W / H)));
  const rows = Math.ceil(n / cols);
  const cells = [];
  for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) cells.push([r, c]);
  genShuffle(cells, rng);

  const centers = [];
  for (let i = 0; i < n; i++) {
    const r = radii[i];
    let placed = false;
    for (let attempt = 0; attempt < 80; attempt++) {
      let cx, cy;
      if (attempt < 20) {
        const [gr, gc] = cells[i % cells.length];
        const cw = Math.floor(W / cols);
        const ch = Math.floor(H / rows);
        cx = clamp(gc * cw + rng.int(0, Math.max(1, cw)), r + 1, W - r - 2);
        cy = clamp(gr * ch + rng.int(0, Math.max(1, ch)), r + 1, H - r - 2);
      } else {
        cx = rng.int(r + 1, Math.max(r + 2, W - r - 2));
        cy = rng.int(r + 1, Math.max(r + 2, H - r - 2));
      }
      let ok = true;
      for (let j = 0; j < centers.length; j++) {
        const [px, py] = centers[j];
        const minDist = radii[j] + r + 3;
        if ((cx - px) ** 2 + (cy - py) ** 2 < minDist * minDist) { ok = false; break; }
      }
      if (ok) { centers.push([cx, cy]); placed = true; break; }
    }
    if (!placed) {
      const [gr, gc] = cells[i % cells.length];
      const cw = Math.floor(W / cols);
      const ch = Math.floor(H / rows);
      const cx = clamp(gc * cw + rng.int(r + 1, Math.max(r + 2, cw - r)), r + 1, W - r - 2);
      const cy = clamp(gr * ch + rng.int(r + 1, Math.max(r + 2, ch - r)), r + 1, H - r - 2);
      centers.push([cx, cy]);
    }
  }
  return centers;
}

const GENERATED_GROUND_COLOR = '#72B86A';

function getGeneratedGroundTypeId() {
  const target = GENERATED_GROUND_COLOR.toLowerCase();
  const types = getTypes('groundTypes').filter(t => t.enabled);
  const byColor = types.find(t => (t.color || '').toLowerCase() === target);
  if (byColor) return byColor.id;
  const softGrass = types.find(t => t.stringKey === 'ground_grass_soft');
  if (softGrass) return softGrass.id;
  const fallback = types.find(t => t.id !== TYPE_ID_BASES.groundTypes) || types[0];
  return fallback ? fallback.id : TYPE_ID_BASES.groundTypes;
}

function updateGenSetting(key, value) {
  if (!ST.genSettings) ST.genSettings = { islandCount: 3, radiusMin: 4, radiusMax: 12, seed: 42 };
  ST.genSettings[key] = value;
}

function randomGenSeed() {
  updateGenSetting('seed', Math.floor(Math.random() * 99999));
  renderSidebar();
}

function generateRandomIslands() {
  const gs = ST.genSettings || {};
  const W = gridW();
  const H = gridH();
  let n = clamp(parseInt(gs.islandCount, 10) || 3, 1, 32);
  let rMin = clamp(parseInt(gs.radiusMin, 10) || 4, 2, Math.min(W, H) - 2);
  let rMax = clamp(parseInt(gs.radiusMax, 10) || 12, rMin, Math.min(W, H) - 2);
  const seed = parseInt(gs.seed, 10) || 42;

  if (ST.md.islands.length > 0) {
    if (!confirm('Replace existing islands with procedurally generated ones?\n将用程序生成的岛屿替换现有岛屿，是否继续？')) return;
  }

  recordMutation('generate-islands');
  const rng = genSeededRng(seed);
  const groundTypeId = getGeneratedGroundTypeId();
  const islandRadii = [];
  for (let i = 0; i < n; i++) islandRadii.push(rng.int(rMin, rMax + 1));

  const centers = placeDispersedIslandCenters(W, H, n, islandRadii, rMax, rng);
  const colors = ['#FFFFFF', '#FF6B6B', '#51CF66', '#339AF0', '#FCC419', '#CC5DE8', '#22B8CF', '#FF922B'];
  const occupied = new Set();
  const newIslands = [];

  for (let i = 0; i < n; i++) {
    const [cx, cy] = centers[i];
    const r = islandRadii[i];
    const poly = genPoly(cx, cy, r, 72, 0.09, rng);
    const rawTiles = collectPolyTiles(poly, W, H);
    const tiles = [];
    for (const t of rawTiles) {
      const k = pk(t.x, t.y);
      if (occupied.has(k)) continue;
      occupied.add(k);
      tiles.push({ x: t.x, y: t.y, ground: groundTypeId });
    }
    if (!tiles.length) continue;

    const id = nextIslandId();
    const ci = newIslands.length % colors.length;
    newIslands.push({
      id,
      name: 'Island ' + newIslands.length,
      outlineColor: colors[ci],
      tiles,
    });
  }

  if (!newIslands.length) {
    warn('Could not place any islands. Try smaller radius or fewer islands.\n未能生成任何岛屿，请减小半径或减少岛屿数量。');
    return;
  }

  ST.md.islands = newIslands;
  ST.md.layers = { decals: [], resources: [], npcs: [], largeSprites: [] };
  ST.activeIslandId = newIslands[0].id;
  dirty = true;
  rebuildLookups();
  renderSidebar();
  renderCanvas();
  ok('Generated ' + newIslands.length + ' island(s) · 已生成 ' + newIslands.length + ' 个岛屿');
}

// ---- Map Editing ----
function gridW(){ return ST.md.canvas.gridWidth; }
function gridH(){ return ST.md.canvas.gridHeight; }

function cropToBounds(maxW, maxH) {
  let removed = 0;

  for(const isl of ST.md.islands){
    const before = isl.tiles.length;
    isl.tiles = isl.tiles.filter(t => t.x >= 0 && t.y >= 0 && t.x < maxW && t.y < maxH);
    removed += (before - isl.tiles.length);
  }

  for(const layerKey of ['decals', 'resources', 'npcs']){
    const before = ST.md.layers[layerKey].length;
    ST.md.layers[layerKey] = ST.md.layers[layerKey].filter(inst =>
      inst.x >= 0 && inst.y >= 0 && inst.x < maxW && inst.y < maxH,
    );
    removed += (before - ST.md.layers[layerKey].length);
  }

  const beforeSprites = ST.md.layers.largeSprites.length;
  ST.md.layers.largeSprites = ST.md.layers.largeSprites.filter(ls =>
    ls.x >= 0 && ls.y >= 0 && (ls.x + ls.width) <= maxW && (ls.y + ls.height) <= maxH,
  );
  removed += (beforeSprites - ST.md.layers.largeSprites.length);

  return removed;
}

function resizeCanvasBounds(newW, newH, options = {}) {
  const opts = {
    recordHistory: true,
    rerenderSidebar: true,
    silent: false,
    ...options,
  };

  if(!Number.isFinite(newW)) newW = gridW();
  if(!Number.isFinite(newH)) newH = gridH();

  const nextW = clamp(Math.round(newW), GRID_MIN_SIZE, GRID_MAX_SIZE);
  const nextH = clamp(Math.round(newH), GRID_MIN_SIZE, GRID_MAX_SIZE);
  if(nextW === gridW() && nextH === gridH()) return;

  if(opts.recordHistory) recordMutation('resize-bounds');

  ST.md.canvas.gridWidth = nextW;
  ST.md.canvas.gridHeight = nextH;
  ST.md.canvas.size = Math.max(nextW, nextH) * ST.md.canvas.cellSize;

  const removed = cropToBounds(nextW, nextH);
  rebuildLookups();
  dirty = true;

  if(opts.rerenderSidebar) renderSidebar();
  renderCanvas();
  if(!opts.silent){
    info('Bounds: ' + nextW + ' x ' + nextH + (removed ? (' (trimmed ' + removed + ' item(s))') : ''));
  }
}

function paintGround(gx, gy, typeId) {
  if(gx<0||gy<0||gx>=gridW()||gy>=gridH()) return;
  if(!ST.activeIslandId){ warn('No active island selected'); return; }
  const isl = getActiveIsland(); if(!isl) return;
  const k = pk(gx,gy);
  // check if belongs to different island
  const existingIslId = ST.il.get(k);
  if(existingIslId && existingIslId!==ST.activeIslandId){ warn('Tile belongs to another island'); return; }
  // check if already part of this island with same type
  const existing = ST.gl.get(k);
  if(existing && existing.islandId===ST.activeIslandId && existing.typeId===typeId) return;
  // remove old tile if exists in this island
  recordMutation('paint-ground');
  if(existingIslId===ST.activeIslandId){
    isl.tiles = isl.tiles.filter(t=>!(t.x===gx&&t.y===gy));
  }
  isl.tiles.push({x:gx, y:gy, ground:typeId});
  ST.gl.set(k, {islandId:ST.activeIslandId, typeId:typeId});
  ST.il.set(k, ST.activeIslandId);
  pushRecentTool('ground', typeId);
  dirty=true;
}

function eraseGround(gx, gy) {
  if(gx<0||gy<0||gx>=gridW()||gy>=gridH()) return;
  const k = pk(gx,gy);
  const gl = ST.gl.get(k);
  if(!gl) return; // no ground
  recordMutation('erase-ground');
  const islId = gl.islandId;
  const isl = ST.md.islands.find(i=>i.id===islId);
  if(!isl) return;
  // remove ground tile
  isl.tiles = isl.tiles.filter(t=>!(t.x===gx&&t.y===gy));
  // remove occupant on this tile
  const occ = ST.ol.get(k);
  if(occ) removeOccupantByLookup(gx, gy, occ);
  // also check all large sprites that cover this cell
  ST.md.layers.largeSprites = ST.md.layers.largeSprites.filter(ls=>{
    if(ls.x<=gx && gx<ls.x+ls.width && ls.y<=gy && gy<ls.y+ls.height){
      // part of footprint - remove the whole sprite
      return false;
    }
    return true;
  });
  ST.gl.delete(k);
  ST.il.delete(k);
  rebuildLookups();
  dirty=true;
}

function removeOccupantByLookup(gx, gy, occ) {
  if(occ.cat==='largeSprites'){
    // find and remove the full large sprite
    ST.md.layers.largeSprites = ST.md.layers.largeSprites.filter(ls=>ls.id!==occ.instId);
  } else {
    const layer = ST.md.layers[occ.cat];
    const idx = layer.findIndex(inst=>inst.id===occ.instId);
    if(idx!==-1) layer.splice(idx,1);
  }
}

function clearOccupantsInArea(x, y, w, h) {
  const seen = new Set();
  const toRemove = [];

  for(let dy=0; dy<h; dy++){
    for(let dx=0; dx<w; dx++){
      const gx = x + dx;
      const gy = y + dy;
      const occ = ST.ol.get(pk(gx, gy));
      if(!occ) continue;
      const key = occ.cat + ':' + occ.instId;
      if(seen.has(key)) continue;
      seen.add(key);
      toRemove.push({gx, gy, occ});
    }
  }

  if(toRemove.length===0) return 0;

  for(const item of toRemove){
    removeOccupantByLookup(item.gx, item.gy, item.occ);
  }
  rebuildLookups();
  return toRemove.length;
}

function isPlayerStartNpcTypeId(typeId) {
  const t = findType('npcTypes', typeId);
  if(!t) return false;
  const key = String(t.stringKey || '').trim().toLowerCase();
  return key === PLAYER_START_STRING_KEY || t.id === PLAYER_START_FALLBACK_ID;
}

function enforceSinglePlayerStartNpc() {
  const playerIndices = [];
  for(let i=0; i<ST.md.layers.npcs.length; i++) {
    const npc = ST.md.layers.npcs[i];
    if(isPlayerStartNpcTypeId(npc.typeId)) {
      playerIndices.push(i);
    }
  }

  if(playerIndices.length <= 1) return 0;

  // Keep the first one, remove the rest.
  let removed = 0;
  for(let i = playerIndices.length - 1; i >= 1; i--) {
    ST.md.layers.npcs.splice(playerIndices[i], 1);
    removed++;
  }

  return removed;
}

function placeOccupant(gx, gy, cat, typeId) {
  if(gx<0||gy<0||gx>=gridW()||gy>=gridH()) return;
  const k = pk(gx,gy);
  // must have ground
  if(!ST.gl.has(k)){ warn('No ground on this tile'); return; }

  const existing = ST.ol.get(k);
  if(existing && existing.cat===cat && existing.typeId===typeId){
    return; // already has the same occupant type
  }

  const isPlayerStartPlacement = cat === 'npcs' && isPlayerStartNpcTypeId(typeId);
  const islandId = ST.il.get(k);
  recordMutation('place-occupant');

  let relocatedPlayerStart = false;
  if(isPlayerStartPlacement){
    const idx = ST.md.layers.npcs.findIndex(n => isPlayerStartNpcTypeId(n.typeId));
    if(idx !== -1){
      const existingPlayer = ST.md.layers.npcs[idx];
      if(existingPlayer.x === gx && existingPlayer.y === gy){
        return;
      }
      ST.md.layers.npcs.splice(idx, 1);
      relocatedPlayerStart = true;
    }
  }

  // Replace whatever is currently on this tile (including large sprite footprints)
  clearOccupantsInArea(gx, gy, 1, 1);

  const idMap = {decals:'dI',resources:'rI',npcs:'nI'};
  const idKey = idMap[cat]; if(!idKey) return;
  const instId = cat.slice(0,-1)+'_'+String(ST.md._nid[idKey]++).padStart(3,'0');
  const inst = {id:instId, typeId, x:gx, y:gy, islandId};
  ST.md.layers[cat].push(inst);
  ST.ol.set(k, {cat, instId, typeId, islandId});
  const toolMap = {decals:'decal', resources:'resource', npcs:'npc'};
  pushRecentTool(toolMap[cat], typeId);
  dirty=true;

  if(relocatedPlayerStart){
    rebuildLookups();
    info('Player Start moved (only one allowed)');
  }
}

function eraseOccupant(gx, gy) {
  if(gx<0||gy<0||gx>=gridW()||gy>=gridH()) return;
  const k = pk(gx,gy);
  const occ = ST.ol.get(k);
  if(!occ) return;
  recordMutation('erase-occupant');
  removeOccupantByLookup(gx, gy, occ);
  rebuildLookups();
  dirty=true;
}

function placeLargeSprite(gx, gy, typeId) {
  const type = findType('largeSpriteTypes', typeId);
  if(!type) return;
  const w=type.width, h=type.height;
  // check bounds
  if(gx<0||gy<0||gx+w>gridW()||gy+h>gridH()){ warn('Large sprite extends outside grid'); return; }

  // check all cells: must have ground
  let islandId = null;
  for(let dy=0;dy<h;dy++){
    for(let dx=0;dx<w;dx++){
      const k=pk(gx+dx,gy+dy);
      if(!ST.gl.has(k)){ warn('Large sprite footprint must be on ground tiles'); return; }
      if(dx===0&&dy===0) islandId=ST.il.get(k);
    }
  }
  if(!islandId) return;

  recordMutation('place-large-sprite');

  // Replace overlapping occupants inside the footprint
  clearOccupantsInArea(gx, gy, w, h);

  const instId = 'large_'+String(ST.md._nid.lI++).padStart(3,'0');
  const inst = {id:instId, typeId, x:gx, y:gy, width:w, height:h, anchor:'topLeft', islandId};
  ST.md.layers.largeSprites.push(inst);
  for(let dy=0;dy<h;dy++) for(let dx=0;dx<w;dx++) ST.ol.set(pk(gx+dx,gy+dy),{cat:'largeSprites',instId,typeId,islandId});
  pushRecentTool('largeSprite', typeId);
  dirty=true;
  ok('Large sprite placed');
}

// ---- Tool Logic: Click/Drag ----
function applyTool(gx, gy, btn) {
  if(ST.mode!=='paint') return;
  const isRight = (btn===2);
  const tool = ST.tool;

  if(tool==='ground'){
    if(isRight){
      eraseGround(gx,gy);
    } else {
      const type = findType('groundTypes', ST.activeTypeId);
      if(!type||!type.enabled){ warn('Select a valid ground type'); return; }
      paintGround(gx,gy,ST.activeTypeId);
    }
  } else if(tool==='eraser'){
    pushRecentTool('eraser');
    if(ST.eraserTarget==='ground'){
      eraseGround(gx,gy);
    } else {
      eraseOccupant(gx,gy);
    }
  } else if(['decal','resource','npc'].includes(tool)){
    if(isRight){
      eraseOccupant(gx,gy);
    } else {
      const catMap = {decal:'decals',resource:'resources',npc:'npcs'};
      const typeMap = {decal:'decalTypes',resource:'resourceTypes',npc:'npcTypes'};
      const type = findType(typeMap[tool], ST.activeTypeId);
      if(!type||!type.enabled){ warn('Select a valid '+CAT_LABELS[typeMap[tool]]+' type'); return; }
      placeOccupant(gx,gy,catMap[tool],ST.activeTypeId);
    }
  } else if(tool==='largeSprite'){
    if(isRight){
      // erase large sprite at this cell
      const k=pk(gx,gy);
      const occ=ST.ol.get(k);
      if(occ&&occ.cat==='largeSprites'){
        removeOccupantByLookup(gx,gy,occ);
        rebuildLookups();
        dirty=true;
      } else {
        eraseOccupant(gx,gy);
      }
    } else {
      const type = findType('largeSpriteTypes', ST.activeTypeId);
      if(!type||!type.enabled){ warn('Select a valid Large Sprite type'); return; }
      placeLargeSprite(gx,gy,ST.activeTypeId);
    }
  }
  renderCanvas();
  updateCellInfo(gx,gy);
}

// ---- Canvas Rendering ----
function renderCanvas() {
  const md = ST.md;
  const cs = md.canvas.cellSize;
  const gw = md.canvas.gridWidth;
  const gh = md.canvas.gridHeight;
  const canvasW = getCanvasWidthPx();
  const canvasH = getCanvasHeightPx();

  if(canvas.width !== canvasW) canvas.width = canvasW;
  if(canvas.height !== canvasH) canvas.height = canvasH;
  applyCanvasZoom();

  const handleRight = document.getElementById('resizeHandleRight');
  const handleBottom = document.getElementById('resizeHandleBottom');
  const handleCorner = document.getElementById('resizeHandleCorner');
  const showHandles = ST.mode === 'paint';
  if(handleRight) handleRight.style.display = showHandles ? 'block' : 'none';
  if(handleBottom) handleBottom.style.display = showHandles ? 'block' : 'none';
  if(handleCorner) handleCorner.style.display = showHandles ? 'block' : 'none';

  // Background
  ctx.fillStyle = SKY_COLOR;
  ctx.fillRect(0,0,canvasW,canvasH);

  // Ground layer
  if(ST.vis.ground){
    for(const isl of md.islands){
      for(const t of isl.tiles){
        const type = findType('groundTypes',t.ground);
        const color = type ? type.color : '#888';
        ctx.fillStyle = color;
        ctx.fillRect(t.x*cs, t.y*cs, cs, cs);
        // subtle shadow/depth
        ctx.fillStyle = 'rgba(0,0,0,0.08)';
        ctx.fillRect(t.x*cs, (t.y+1)*cs-3, cs, 3);
        ctx.fillRect((t.x+1)*cs-2, t.y*cs+2, 2, cs);
      }
    }
  }

  // Large Sprites
  if(ST.vis.largeSprites){
    for(const ls of md.layers.largeSprites){
      const type = findType('largeSpriteTypes',ls.typeId);
      const c = type?type.color:'#B983FF';
      const x=ls.x*cs, y=ls.y*cs, w=ls.width*cs, h=ls.height*cs;
      ctx.fillStyle = c+'55';
      ctx.fillRect(x,y,w,h);
      ctx.strokeStyle = c;
      ctx.lineWidth = 2;
      ctx.strokeRect(x+1,y+1,w-2,h-2);
      // label
      ctx.fillStyle = '#fff';
      ctx.font = 'bold 11px sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      const label = type?type.name:'LS'+ls.typeId;
      ctx.fillText(label, x+w/2, y+h/2, w-8);
      ctx.textAlign = 'left';
      ctx.textBaseline = 'alphabetic';
    }
  }

  // Decals
  if(ST.vis.decals){
    for(const d of md.layers.decals){
      const type = findType('decalTypes',d.typeId);
      const c = type?type.color:'#2E7D32';
      const cx=d.x*cs+cs/2, cy=d.y*cs+cs/2, r=cs*0.28;
      ctx.save();
      ctx.translate(cx,cy);
      ctx.rotate(Math.PI/4);
      ctx.fillStyle = c;
      ctx.fillRect(-r,-r,r*2,r*2);
      ctx.strokeStyle = 'rgba(0,0,0,0.4)';
      ctx.lineWidth = 1;
      ctx.strokeRect(-r,-r,r*2,r*2);
      ctx.restore();
      // label
      ctx.fillStyle='#fff';
      ctx.font='9px sans-serif';
      ctx.textAlign='center';
      ctx.textBaseline='middle';
      ctx.fillText('D', d.x*cs+cs/2, d.y*cs+cs/2, cs-4);
      ctx.textAlign='left';
      ctx.textBaseline='alphabetic';
    }
  }

  // Resources
  if(ST.vis.resources){
    for(const r of md.layers.resources){
      const type = findType('resourceTypes',r.typeId);
      const c = type?type.color:'#4DD4AC';
      const cx=r.x*cs+cs/2, cy=r.y*cs+cs/2, rad=cs*0.3;
      ctx.beginPath();
      ctx.arc(cx,cy,rad,0,Math.PI*2);
      ctx.fillStyle=c;
      ctx.fill();
      ctx.strokeStyle='rgba(0,0,0,0.4)';
      ctx.lineWidth=1;
      ctx.stroke();
      ctx.fillStyle='#fff';
      ctx.font='9px sans-serif';
      ctx.textAlign='center';
      ctx.textBaseline='middle';
      ctx.fillText('R',cx,cy,cs-6);
      ctx.textAlign='left';
      ctx.textBaseline='alphabetic';
    }
  }

  // NPCs
  if(ST.vis.npcs){
    for(const n of md.layers.npcs){
      const type = findType('npcTypes',n.typeId);
      const c = type?type.color:'#FFD166';
      const cx=n.x*cs+cs/2, cy=n.y*cs+cs/2, sz=cs*0.3;
      ctx.beginPath();
      ctx.moveTo(cx,cy-sz);
      ctx.lineTo(cx+sz,cy+sz);
      ctx.lineTo(cx-sz,cy+sz);
      ctx.closePath();
      ctx.fillStyle=c;
      ctx.fill();
      ctx.strokeStyle='rgba(0,0,0,0.4)';
      ctx.lineWidth=1;
      ctx.stroke();
      ctx.fillStyle='#fff';
      ctx.font='9px sans-serif';
      ctx.textAlign='center';
      ctx.textBaseline='middle';
      ctx.fillText('N',cx,cy+2,cs-6);
      ctx.textAlign='left';
      ctx.textBaseline='alphabetic';
    }
  }

  // Island Outlines
  if(ST.vis.outlines){
    for(const isl of md.islands){
      ctx.strokeStyle = isl.outlineColor;
      ctx.lineWidth = 2;
      for(const t of isl.tiles){
        const x=t.x*cs, y=t.y*cs;
        // check neighbors
        if(!ST.il.has(pk(t.x,t.y-1)) || ST.il.get(pk(t.x,t.y-1))!==isl.id){
          ctx.beginPath(); ctx.moveTo(x,y); ctx.lineTo(x+cs,y); ctx.stroke();
        }
        if(!ST.il.has(pk(t.x,t.y+1)) || ST.il.get(pk(t.x,t.y+1))!==isl.id){
          ctx.beginPath(); ctx.moveTo(x,y+cs); ctx.lineTo(x+cs,y+cs); ctx.stroke();
        }
        if(!ST.il.has(pk(t.x-1,t.y)) || ST.il.get(pk(t.x-1,t.y))!==isl.id){
          ctx.beginPath(); ctx.moveTo(x,y); ctx.lineTo(x,y+cs); ctx.stroke();
        }
        if(!ST.il.has(pk(t.x+1,t.y)) || ST.il.get(pk(t.x+1,t.y))!==isl.id){
          ctx.beginPath(); ctx.moveTo(x+cs,y); ctx.lineTo(x+cs,y+cs); ctx.stroke();
        }
      }
    }
  }

  // Grid
  if(ST.vis.grid){
    ctx.strokeStyle = 'rgba(0,0,0,0.12)';
    ctx.lineWidth = 0.5;
    for(let x=0;x<=gw;x++){
      ctx.beginPath(); ctx.moveTo(x*cs,0); ctx.lineTo(x*cs,canvasH); ctx.stroke();
    }
    for(let y=0;y<=gh;y++){
      ctx.beginPath(); ctx.moveTo(0,y*cs); ctx.lineTo(canvasW,y*cs); ctx.stroke();
    }
  }

  // Hover preview
  if(ST.mode==='paint' && ST.hx>=0 && ST.hy>=0){
    const gx=ST.hx, gy=ST.hy;
    if(gx>=0&&gx<gw&&gy>=0&&gy<gh){
      if(ST.tool==='largeSprite'){
        const type = findType('largeSpriteTypes',ST.activeTypeId);
        if(type){
          const w=type.width, h=type.height;
          ctx.fillStyle = type.color+'44';
          ctx.fillRect(gx*cs,gy*cs,w*cs,h*cs);
          ctx.strokeStyle = type.color;
          ctx.lineWidth = 1;
          ctx.setLineDash([4,4]);
          ctx.strokeRect(gx*cs,gy*cs,w*cs,h*cs);
          ctx.setLineDash([]);
        }
      } else {
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.strokeRect(gx*cs+1,gy*cs+1,cs-2,cs-2);
      }
    }
  }
}

// ---- Cell Info ----
function updateCellInfo(gx, gy) {
  const ci = document.getElementById('cellInfo');
  if(!ci) return;
  if(gx<0||gy<0||gx>=gridW()||gy>=gridH()){ ci.textContent=''; return; }
  const k=pk(gx,gy);
  const parts = [];
  const gl = ST.gl.get(k);
  if(gl){
    const gt = findType('groundTypes',gl.typeId);
    parts.push('Ground: '+(gt?gt.name:'?'+gl.typeId));
  }
  const occ = ST.ol.get(k);
  if(occ){
    const typeMap = {decals:'decalTypes',resources:'resourceTypes',npcs:'npcTypes',largeSprites:'largeSpriteTypes'};
    const t = findType(typeMap[occ.cat],occ.typeId);
    const catName = {decals:'Decal',resources:'Resource',npcs:'NPC',largeSprites:'Sprite'}[occ.cat]||occ.cat;
    parts.push(catName+': '+(t?t.name:'?'+occ.typeId));
  }
  ci.textContent = parts.join(' | ') || 'Empty';
}

// ---- Sidebar Rendering ----
function renderSidebar() {
  const sb = document.getElementById('sidebar');
  const ms = document.getElementById('modeSwitch');
  if(!sb) return;

  // Mode Switch - always visible above scroll area
  if(ms){
    ms.innerHTML = '<div class="flex rounded overflow-hidden border border-gray-600">' +
      '<button onclick="S.setMode(\'define\')" class="flex-1 px-2 py-1.5 text-xs font-semibold tab-btn'+(ST.mode==='define'?' active':'')+'" style="background:'+(ST.mode==='define'?'':'#374151')+'">Define Toolset</button>' +
      '<button onclick="S.setMode(\'paint\')" class="flex-1 px-2 py-1.5 text-xs font-semibold tab-btn'+(ST.mode==='paint'?' active':'')+'" style="background:'+(ST.mode==='paint'?'':'#374151')+'">Paint Map</button>' +
      '</div>';
  }

  let h = '';
  if(ST.mode==='define'){
    h += renderDefineMode();
  } else {
    h += renderPaintMode();
  }

  sb.innerHTML = h;
  renderRecentPalette();
  renderPlayerScaleIndicator();
}

function renderDefineMode() {
  let h = '';
  // Category tabs
  h += '<div class="text-xs font-semibold text-gray-400 mb-1">Category</div>';
  h += '<div class="flex flex-wrap gap-1 mb-3">';
  for(const cat of CATEGORIES){
    const active = ST.defCat===cat;
    h += '<button onclick="S.setDefCat(\''+cat+'\')" class="px-2 py-1 rounded text-xs '+((active?'tab-btn active':'bg-gray-700 hover:bg-gray-600'))+'">'+CAT_LABELS[cat]+'</button>';
  }
  h += '</div>';

  // Type list
  h += '<div class="flex items-center justify-between mb-2">';
  h += '<span class="text-xs font-semibold text-gray-400">'+escHtml(CAT_PLURALS[ST.defCat])+'</span>';
  h += '<button onclick="S.addType(\''+ST.defCat+'\')" class="bg-indigo-700 hover:bg-indigo-600 px-2 py-0.5 rounded text-xs">+ Add</button>';
  h += '</div>';

  const types = getTypes(ST.defCat);
  if(types.length===0){
    h += '<div class="text-gray-500 text-xs italic">No types defined</div>';
  }
  for(const t of types){
    h += renderTypeCard(ST.defCat, t);
  }

  return h;
}

function renderTypeCard(cat, t) {
  let h = '<div class="type-card'+(t.enabled?'':' opacity-50')+'">';
  h += '<div class="flex items-center justify-between mb-1">';
  h += '<span class="text-[10px] text-gray-500 font-mono">ID:'+t.id+'</span>';
  h += '<div class="flex gap-1">';
  if(t.enabled){
    h += '<button onclick="S.toggleTypeEnabled(\''+cat+'\','+t.id+')" class="text-[10px] text-yellow-400 hover:text-yellow-300">Disable</button>';
  } else {
    h += '<button onclick="S.toggleTypeEnabled(\''+cat+'\','+t.id+')" class="text-[10px] text-green-400 hover:text-green-300">Enable</button>';
  }
  h += '<button onclick="S.deleteType(\''+cat+'\','+t.id+')" class="text-[10px] text-red-400 hover:text-red-300">Delete</button>';
  h += '</div></div>';

  // Name
  h += '<div class="mb-1"><label class="text-[10px] text-gray-400">Name</label>';
  h += '<input type="text" value="'+escHtml(t.name)+'" oninput="S.updateTypeName(\''+cat+'\','+t.id+',this.value)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-0.5 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';

  // Color
  h += '<div class="flex items-center gap-2 mb-1">';
  h += '<label class="text-[10px] text-gray-400 w-10">Color</label>';
  h += '<input type="color" value="'+t.color+'" oninput="S.updateTypeColor(\''+cat+'\','+t.id+',this.value)" class="w-8 h-6">';
  h += '<input type="text" value="'+t.color+'" oninput="S.updateTypeColor(\''+cat+'\','+t.id+',this.value)" class="bg-gray-900 border border-gray-600 rounded px-1.5 py-0.5 text-xs w-20 focus:outline-none focus:border-indigo-500 font-mono" maxlength="7">';
  h += '</div>';

  // String Key
  h += '<div class="mb-1"><label class="text-[10px] text-gray-400">String Key</label>';
  h += '<input type="text" value="'+escHtml(t.stringKey)+'" oninput="S.updateTypeStringKey(\''+cat+'\','+t.id+',this.value)" placeholder="e.g. grass_prefab" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-0.5 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';

  // Large sprite: width/height
  if(cat==='largeSpriteTypes'){
    h += '<div class="flex gap-2 mb-1">';
    h += '<div class="flex-1"><label class="text-[10px] text-gray-400">Width</label>';
    h += '<input type="number" value="'+t.width+'" min="1" oninput="S.updateTypeWidth(\''+cat+'\','+t.id+',this.value)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-0.5 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';
    h += '<div class="flex-1"><label class="text-[10px] text-gray-400">Height</label>';
    h += '<input type="number" value="'+t.height+'" min="1" oninput="S.updateTypeHeight(\''+cat+'\','+t.id+',this.value)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-0.5 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';
    h += '</div>';
  }

  // Enabled status
  h += '<div class="text-[10px] '+(t.enabled?'text-green-400':'text-gray-500')+'">'+(t.enabled?'✓ Enabled':'✗ Disabled')+'</div>';

  h += '</div>';
  return h;
}

function renderPaintMode() {
  let h = '';

  // Island Section
  h += '<div class="mb-3">';
  h += '<div class="text-xs font-semibold text-gray-400 mb-1 flex items-center justify-between">Active Island <button onclick="S.createIsland()" class="bg-indigo-700 hover:bg-indigo-600 px-2 py-0.5 rounded text-[10px]">+ New</button></div>';
  if(ST.md.islands.length===0){
    h += '<div class="text-gray-500 text-xs italic">No islands. Create one to start painting.</div>';
  } else {
    h += '<select onchange="S.selectIsland(this.value)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-1 text-xs focus:outline-none focus:border-indigo-500 mb-1">';
    for(const isl of ST.md.islands){
      h += '<option value="'+isl.id+'"'+(ST.activeIslandId===isl.id?' selected':'')+'>'+escHtml(isl.name)+' ('+isl.id+')</option>';
    }
    h += '</select>';

    const ai = getActiveIsland();
    if(ai){
      h += '<div class="space-y-1 mt-1">';
      h += '<div><label class="text-[10px] text-gray-500">Name</label><input type="text" value="'+escHtml(ai.name)+'" oninput="S.renameIsland(\''+ai.id+'\',this.value)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-0.5 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';
      h += '<div class="flex items-center gap-2">';
      h += '<label class="text-[10px] text-gray-500">Outline</label>';
      h += '<input type="color" value="'+ai.outlineColor+'" oninput="S.changeIslandOutlineColor(\''+ai.id+'\',this.value)" class="w-6 h-5">';
      h += '<span class="text-[10px] text-gray-500 font-mono">'+ai.outlineColor+'</span>';
      h += '</div>';
      h += '<button onclick="S.deleteIsland(\''+ai.id+'\')" class="text-[10px] text-red-400 hover:text-red-300">Delete Island</button>';
      h += '</div>';
    }
  }
  h += '</div>';

  // Tools Section
  h += '<div class="mb-3">';
  h += '<div class="text-xs font-semibold text-gray-400 mb-1">Tool</div>';
  h += '<div class="space-y-1">';
  const tools = [
    {key:'ground',icon:'🟫',label:'Ground'},
    {key:'decal',icon:'💠',label:'Decal'},
    {key:'resource',icon:'⛏️',label:'Resource'},
    {key:'npc',icon:'🧍',label:'NPC'},
    {key:'largeSprite',icon:'🖼️',label:'Sprite'},
    {key:'eraser',icon:'🧽',label:'Eraser'},
  ];
  for(const t of tools){
    const active = ST.tool===t.key;
    h += '<button onclick="S.setTool(\''+t.key+'\')" class="tool-btn w-full flex items-center gap-2 px-2 py-1.5 rounded text-xs '+(active?' active':' bg-gray-700 hover:bg-gray-600')+'">';
    h += '<span class="text-lg leading-none">'+t.icon+'</span>';
    h += '<span class="font-medium">'+t.label+'</span>';
    h += '</button>';
  }
  h += '</div>';

  // Eraser sub-mode
  if(ST.tool==='eraser'){
    h += '<div class="flex gap-2 mt-1">';
    h += '<label class="flex items-center gap-1 text-[10px] cursor-pointer"><input type="radio" name="eraserTarget" value="ground" '+(ST.eraserTarget==='ground'?'checked':'')+' onchange="S.setEraserTarget(\'ground\')"> Ground+Occ</label>';
    h += '<label class="flex items-center gap-1 text-[10px] cursor-pointer"><input type="radio" name="eraserTarget" value="occupant" '+(ST.eraserTarget==='occupant'?'checked':'')+' onchange="S.setEraserTarget(\'occupant\')"> Occupant Only</label>';
    h += '</div>';
  }
  h += '</div>';

  // Type Selector
  const typeCatMap = {ground:'groundTypes',decal:'decalTypes',resource:'resourceTypes',npc:'npcTypes',largeSprite:'largeSpriteTypes'};
  const typeCat = typeCatMap[ST.tool];
  if(typeCat){
    const typeIconMap = {ground:'🟫',decal:'💠',resource:'⛏️',npc:'🧍',largeSprite:'🖼️'};
    const typeIcon = typeIconMap[ST.tool] || '•';
    h += '<div class="mb-3">';
    h += '<div class="text-xs font-semibold text-gray-400 mb-1">'+typeIcon+' Type</div>';
    const types = getTypes(typeCat).filter(t=>t.enabled);
    if(types.length===0){
      h += '<div class="text-gray-500 text-xs italic">No enabled types. Switch to Define mode to add.</div>';
    } else {
      if(!types.find(t=>t.id===ST.activeTypeId) && types.length>0){
        ST.activeTypeId = types[0].id;
      }

      // Auto-populated quick pick list for the active tool's types
      h += '<div class="space-y-1 max-h-40 overflow-y-auto pr-1">';
      for(const t of types){
        const active = ST.activeTypeId===t.id;
        h += '<button onclick="S.setActiveTypeId('+t.id+')" class="w-full flex items-center gap-2 px-2 py-1 rounded border text-left text-xs '+
          (active
            ? 'bg-indigo-600 border-indigo-400 text-white'
            : 'bg-gray-900 border-gray-700 hover:bg-gray-700 text-gray-200')+
          '">';
        h += '<span class="text-[11px]">'+typeIcon+'</span>';
        h += '<span class="inline-block w-3 h-3 rounded" style="background:'+t.color+'"></span>';
        h += '<span class="flex-1 truncate">'+escHtml(t.name)+'</span>';
        h += '<span class="text-[10px] opacity-80">ID:'+t.id+'</span>';
        h += '</button>';
      }
      h += '</div>';

      // Selected type details
      const selType = findType(typeCat, ST.activeTypeId);
      if(selType){
        h += '<div class="flex items-center gap-2 mt-1">';
        h += '<div class="w-5 h-5 rounded" style="background:'+selType.color+'"></div>';
        h += '<span class="text-[10px] text-gray-400">'+selType.color+(selType.stringKey?' → '+escHtml(selType.stringKey):'')+'</span>';
        h += '</div>';
        if(typeCat==='largeSpriteTypes'){
          h += '<span class="text-[10px] text-gray-500">Footprint: '+selType.width+'×'+selType.height+'</span>';
        }
      }
    }
    h += '</div>';
  }

  // Visibility Section
  h += '<div class="mb-3">';
  h += '<div class="text-xs font-semibold text-gray-400 mb-1">Visibility</div>';
  const visItems = [
    {key:'ground',label:'Ground'},
    {key:'decals',label:'Decals'},
    {key:'resources',label:'Resources'},
    {key:'npcs',label:'NPCs'},
    {key:'largeSprites',label:'Large Sprites'},
    {key:'outlines',label:'Island Outlines'},
    {key:'grid',label:'Grid'},
  ];
  for(const v of visItems){
    const checked = ST.vis[v.key]?'checked':'';
    h += '<label class="vis-row flex items-center gap-2 px-1 py-0.5 rounded cursor-pointer">';
    h += '<input type="checkbox" '+checked+' onchange="S.toggleVis(\''+v.key+'\',this.checked)" class="accent-indigo-500 w-3.5 h-3.5">';
    h += '<span class="text-xs">'+v.label+'</span>';
    h += '</label>';
  }
  h += '</div>';

  // Bounds Section
  h += '<div class="mb-3">';
  h += '<div class="text-xs font-semibold text-gray-400 mb-1">Bounds (tiles)</div>';
  h += '<div class="grid grid-cols-2 gap-2">';
  h += '<div><label class="text-[10px] text-gray-500">Width</label>';
  h += '<input type="number" min="'+GRID_MIN_SIZE+'" max="'+GRID_MAX_SIZE+'" value="'+gridW()+'" onchange="S.setBounds(parseInt(this.value), null)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-1 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';
  h += '<div><label class="text-[10px] text-gray-500">Height</label>';
  h += '<input type="number" min="'+GRID_MIN_SIZE+'" max="'+GRID_MAX_SIZE+'" value="'+gridH()+'" onchange="S.setBounds(null, parseInt(this.value))" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-1 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';
  h += '</div>';
  h += '<div class="text-[10px] text-gray-500 mt-1">Tip: drag the right/bottom/corner handles on the canvas to resize interactively.</div>';
  h += '</div>';

  // Procedural Generation Section
  const gs = ST.genSettings || { islandCount: 3, radiusMin: 4, radiusMax: 12, seed: 42 };
  h += '<div class="mb-3 border-t border-gray-700 pt-2">';
  h += '<div class="text-xs font-semibold text-gray-400 mb-1">Generate Islands · 生成岛屿</div>';
  h += '<div class="space-y-2">';
  h += '<div><label class="text-[10px] text-gray-500">岛屿数 Islands n</label>';
  h += '<input type="number" min="1" max="32" value="'+gs.islandCount+'" onchange="S.updateGenSetting(\'islandCount\', parseInt(this.value)||3)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-1 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';
  h += '<div><label class="text-[10px] text-gray-500">地形半径 Terrain radius <span class="text-gray-600">(min – max)</span></label>';
  h += '<div class="grid grid-cols-[1fr_auto_1fr] gap-1 items-center mt-0.5">';
  h += '<input type="number" min="2" max="'+Math.min(gridW(), gridH())+'" value="'+gs.radiusMin+'" onchange="S.updateGenSetting(\'radiusMin\', parseInt(this.value)||4)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-1 text-xs focus:outline-none focus:border-indigo-500">';
  h += '<span class="text-[10px] text-gray-500">–</span>';
  h += '<input type="number" min="2" max="'+Math.min(gridW(), gridH())+'" value="'+gs.radiusMax+'" onchange="S.updateGenSetting(\'radiusMax\', parseInt(this.value)||12)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-1 text-xs focus:outline-none focus:border-indigo-500">';
  h += '</div></div>';
  h += '<div><label class="text-[10px] text-gray-500">种子 Seed</label>';
  h += '<input type="number" value="'+gs.seed+'" onchange="S.updateGenSetting(\'seed\', parseInt(this.value)||42)" class="w-full bg-gray-900 border border-gray-600 rounded px-1.5 py-1 text-xs focus:outline-none focus:border-indigo-500 mt-0.5"></div>';
  h += '<button onclick="S.generateRandomIslands()" class="w-full bg-indigo-700 hover:bg-indigo-600 px-2 py-1.5 rounded text-xs font-medium">生成岛屿 Generate ↗</button>';
  h += '<button onclick="S.randomGenSeed()" class="w-full bg-gray-700 hover:bg-gray-600 px-2 py-1 rounded text-[10px]">随机种子 Random seed</button>';
  h += '<div class="text-[10px] text-gray-500 leading-relaxed">Randomly picks n dispersed points and grows green Ground tiles within map bounds.<br>在地图内随机选取 n 个分散点，并在边界内生成绿色 Ground 方格。</div>';
  h += '</div></div>';

  // Help
  h += '<div class="text-[10px] text-gray-500 space-y-0.5 border-t border-gray-700 pt-2">';
  h += '<div>Left click: Paint / Place</div>';
  h += '<div>Right click: Erase active layer</div>';
  h += '<div>Middle mouse drag: Pan viewport</div>';
  h += '<div>Drag: Continuous ground paint</div>';
  h += '</div>';

  return h;
}

// ---- Canvas Events ----
function setHover(gx,gy){
  ST.hx=gx; ST.hy=gy;
  const cd = document.getElementById('coordDisplay');
  if(cd) cd.textContent = 'Cell: ('+gx+', '+gy+')';
  renderCanvas();
  updateCellInfo(gx,gy);
}

function setupCanvasEvents() {
  canvas = document.getElementById('mapCanvas');
  ctx = canvas.getContext('2d');
  const viewport = document.getElementById('canvasViewport');

  applyCanvasZoom();

  canvas.addEventListener('contextmenu', e=>e.preventDefault());

  if(viewport){
    viewport.addEventListener('mousedown', e=>{
      if(e.button !== 1) return;
      panState.active = true;
      panState.startX = e.clientX;
      panState.startY = e.clientY;
      panState.startScrollLeft = viewport.scrollLeft;
      panState.startScrollTop = viewport.scrollTop;
      viewport.style.cursor = 'grabbing';
      e.preventDefault();
    });
  }

  window.addEventListener('mousemove', e=>{
    if(panState.active && viewport){
      const dx = e.clientX - panState.startX;
      const dy = e.clientY - panState.startY;
      viewport.scrollLeft = panState.startScrollLeft - dx;
      viewport.scrollTop = panState.startScrollTop - dy;
    }

    if(resizeState.active){
      const cs = ST.md.canvas.cellSize;
      const zoom = Math.max(0.01, ST.zoom || 1);
      const dxCanvas = (e.clientX - resizeState.startX) / zoom;
      const dyCanvas = (e.clientY - resizeState.startY) / zoom;
      const dCellsX = Math.round(dxCanvas / cs);
      const dCellsY = Math.round(dyCanvas / cs);

      let nextW = resizeState.startW;
      let nextH = resizeState.startH;
      if(resizeState.mode === 'right' || resizeState.mode === 'corner'){
        nextW += dCellsX;
      }
      if(resizeState.mode === 'bottom' || resizeState.mode === 'corner'){
        nextH += dCellsY;
      }

      resizeCanvasBounds(nextW, nextH, { recordHistory: true, rerenderSidebar: false, silent: true });
      e.preventDefault();
    }
  });

  window.addEventListener('mouseup', () => {
    if(panState.active && viewport){
      panState.active = false;
      viewport.style.cursor = '';
    }
    if(resizeState.active){
      resizeState.active = false;
      resizeState.mode = null;
      endHistoryGroup();
      renderSidebar();
    }
  });

  canvas.addEventListener('wheel', e=>{
    e.preventDefault();
    const dir = e.deltaY < 0 ? 1 : -1;
    setZoom((ST.zoom || 1) + (dir * ZOOM_STEP), e.clientX, e.clientY);
  }, { passive:false });

  canvas.addEventListener('mousedown', e=>{
    if(e.button === 1) return;
    if(resizeState.active || panState.active) return;
    if(ST.mode!=='paint') return;
    e.preventDefault();
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width/rect.width;
    const scaleY = canvas.height/rect.height;
    const px = (e.clientX-rect.left)*scaleX;
    const py = (e.clientY-rect.top)*scaleY;
    const cs = ST.md.canvas.cellSize;
    const gx = Math.floor(px/cs);
    const gy = Math.floor(py/cs);

    ST.painting = true;
    ST.paintBtn = e.button;
    beginHistoryGroup('paint-stroke');
    applyTool(gx,gy,e.button);
  });

  canvas.addEventListener('mousemove', e=>{
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width/rect.width;
    const scaleY = canvas.height/rect.height;
    const px = (e.clientX-rect.left)*scaleX;
    const py = (e.clientY-rect.top)*scaleY;
    const cs = ST.md.canvas.cellSize;
    const gx = Math.floor(px/cs);
    const gy = Math.floor(py/cs);

    setHover(gx,gy);

    if(ST.painting && ST.mode==='paint'){
      // Only drag-paint for ground and eraser
      if((ST.tool==='ground' || ST.tool==='eraser') && ST.paintBtn===e.button){
        applyTool(gx,gy,ST.paintBtn);
      }
    }
  });

  canvas.addEventListener('mouseup', e=>{
    ST.painting = false;
    endHistoryGroup();
  });

  canvas.addEventListener('mouseleave', e=>{
    ST.painting = false;
    endHistoryGroup();
    ST.hx=-1; ST.hy=-1;
    renderCanvas();
    const cd = document.getElementById('coordDisplay');
    if(cd) cd.textContent = 'Cell: —';
    const ci = document.getElementById('cellInfo');
    if(ci) ci.textContent = '';
  });

  const rightHandle = document.getElementById('resizeHandleRight');
  const bottomHandle = document.getElementById('resizeHandleBottom');
  const cornerHandle = document.getElementById('resizeHandleCorner');

  const startResize = (mode, e) => {
    if(e.button !== 0) return;
    if(ST.mode !== 'paint') return;
    e.preventDefault();
    e.stopPropagation();
    resizeState.active = true;
    resizeState.mode = mode;
    resizeState.startX = e.clientX;
    resizeState.startY = e.clientY;
    resizeState.startW = gridW();
    resizeState.startH = gridH();
    beginHistoryGroup('resize-bounds');
  };

  if(rightHandle) rightHandle.addEventListener('mousedown', e => startResize('right', e));
  if(bottomHandle) bottomHandle.addEventListener('mousedown', e => startResize('bottom', e));
  if(cornerHandle) cornerHandle.addEventListener('mousedown', e => startResize('corner', e));
}

// ---- JSON Export ----
function buildExportData() {
  const md = ST.md;
  const clean = {
    version: 1,
    mapName: md.mapName,
    canvas: {...md.canvas},
    toolset: {},
    islands: [],
    layers: {}
  };
  // Toolset
  for(const cat of CATEGORIES){
    clean.toolset[cat] = md.toolset[cat].map(t=>{
      const obj = {id:t.id, name:t.name, stringKey:t.stringKey||'', color:t.color, enabled:t.enabled};
      if(cat==='largeSpriteTypes'){ obj.width=t.width; obj.height=t.height; }
      return obj;
    });
  }
  // Islands
  clean.islands = md.islands.map(isl=>({
    id:isl.id,
    name:isl.name,
    outlineColor:isl.outlineColor,
    tiles: isl.tiles.map(t=>({x:t.x,y:t.y,ground:t.ground}))
  }));
  // Layers
  const layerKeys = ['decals','resources','npcs','largeSprites'];
  for(const lk of layerKeys){
    clean.layers[lk] = md.layers[lk].map(inst=>{
      const obj = {id:inst.id, typeId:inst.typeId, x:inst.x, y:inst.y, islandId:inst.islandId};
      if(lk==='largeSprites'){ obj.width=inst.width; obj.height=inst.height; obj.anchor=inst.anchor; }
      return obj;
    });
  }
  return clean;
}

function exportJSON() {
  const data = buildExportData();
  const json = JSON.stringify(data, null, 2);
  const blob = new Blob([json], {type:'application/json'});
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = (ST.md.mapName||'map')+'.json';
  a.click();
  URL.revokeObjectURL(url);
  ok('JSON exported');
}

function copyJSON() {
  const data = buildExportData();
  const json = JSON.stringify(data, null, 2);
  // Show in modal
  const modal = document.getElementById('jsonModal');
  const title = document.getElementById('jsonModalTitle');
  const content = document.getElementById('jsonModalContent');
  title.textContent = 'Map JSON (also copied to clipboard)';
  content.value = json;
  modal.classList.remove('hidden');
  modal.classList.add('flex');
  navigator.clipboard.writeText(json).then(()=>ok('JSON copied to clipboard')).catch(()=>err('Failed to copy'));
}

// ---- JSON Import ----
function importJSON() {
  const input = document.getElementById('importFileInput');
  input.onchange = e=>{
    const file = e.target.files[0];
    if(!file) return;
    const reader = new FileReader();
    reader.onload = ev=>{
      try {
        const data = JSON.parse(ev.target.result);
        const result = validateImport(data);
        if(!result.valid){
          err('Import failed: '+result.errors.join('; '));
          return;
        }
        applyImport(result.data);
        ok('Map imported successfully');
      } catch(ex){
        err('Invalid JSON: '+ex.message);
      }
    };
    reader.readAsText(file);
    input.value = '';
  };
  input.click();
}

function validateImport(data) {
  const errors = [];
  if(!data || typeof data !== 'object'){ errors.push('Not an object'); return {valid:false,errors}; }
  if(data.version !== 1) errors.push('Unsupported version: '+(data.version||'missing'));
  if(!data.canvas || typeof data.canvas !== 'object') errors.push('Missing canvas config');
  if(!data.toolset || typeof data.toolset !== 'object') errors.push('Missing toolset');
  if(!Array.isArray(data.islands)) errors.push('Missing islands array');
  if(!data.layers || typeof data.layers !== 'object') errors.push('Missing layers');
  // Check toolset categories
  if(data.toolset){
    for(const cat of CATEGORIES){
      if(!Array.isArray(data.toolset[cat])) errors.push('Missing toolset.'+cat);
      else {
        for(const t of data.toolset[cat]){
          if(typeof t.id!=='number') errors.push(cat+' has type without numeric id');
          if(typeof t.name!=='string') errors.push(cat+' has type without name');
        }
      }
    }
  }
  // Check islands
  if(Array.isArray(data.islands)){
    const islIds = new Set();
    for(const isl of data.islands){
      if(!isl.id || typeof isl.id!=='string') errors.push('Island without valid id');
      else islIds.add(isl.id);
      if(!Array.isArray(isl.tiles)) errors.push('Island '+isl.id+' missing tiles array');
    }
    // Check layer references
    if(data.layers){
      const layerKeys = ['decals','resources','npcs','largeSprites'];
      for(const lk of layerKeys){
        if(!Array.isArray(data.layers[lk])){ errors.push('Missing layers.'+lk); continue; }
        for(const inst of data.layers[lk]){
          if(inst.islandId && !islIds.has(inst.islandId)){
            errors.push(lk+' instance '+inst.id+' references missing island '+inst.islandId);
          }
        }
      }
    }
  }
  return {valid:errors.length===0, errors, data};
}

function applyImport(data, recordHistoryStep = true) {
  if(recordHistoryStep){
    recordMutation('apply-import');
  }
  const md = ST.md;
  md.version = data.version || 1;
  md.mapName = data.mapName || 'Imported Map';
  const importedSize = (data.canvas&&data.canvas.size)||DEFAULT_CANVAS_PX;
  const importedCellSize = Math.max(1, (data.canvas&&data.canvas.cellSize)||DEFAULT_CELL_SIZE);
  const importedGridWidth = (data.canvas&&data.canvas.gridWidth)||Math.max(1, Math.floor(importedSize / importedCellSize));
  const importedGridHeight = (data.canvas&&data.canvas.gridHeight)||Math.max(1, Math.floor(importedSize / importedCellSize));
  md.canvas = {
    size: importedSize,
    cellSize: importedCellSize,
    gridWidth: importedGridWidth,
    gridHeight: importedGridHeight,
  };
  // Toolset
  for(const cat of CATEGORIES){
    md.toolset[cat] = (data.toolset&&data.toolset[cat]) ? data.toolset[cat].map(t=>validateTypeObj(cat,t)) : [];
  }
  // Islands
  md.islands = (data.islands||[]).map(isl=>({
    id: isl.id||'island_000',
    name: isl.name||'Unnamed',
    outlineColor: isl.outlineColor||'#FFFFFF',
    tiles: (isl.tiles||[]).map(t=>({x:t.x||0,y:t.y||0,ground:t.ground||0})),
  }));
  // Layers
  md.layers.decals = (data.layers&&data.layers.decals||[]).map(d=>({...d,islandId:d.islandId||''}));
  md.layers.resources = (data.layers&&data.layers.resources||[]).map(r=>({...r,islandId:r.islandId||''}));
  md.layers.npcs = (data.layers&&data.layers.npcs||[]).map(n=>({...n,islandId:n.islandId||''}));
  md.layers.largeSprites = (data.layers&&data.layers.largeSprites||[]).map(l=>({...l,islandId:l.islandId||'',anchor:l.anchor||'topLeft'}));

  // Normalize IDs into non-overlapping category ranges and remap all references.
  normalizeTypeIdsAndReferences();

  // Keep a single Player Start marker in imported data.
  const removedPlayerStartDuplicates = enforceSinglePlayerStartNpc();
  if(removedPlayerStartDuplicates > 0){
    warn('Removed extra Player Start markers; only one is allowed');
  }

  // Rebuild next IDs
  rebuildNextIds();
  rebuildLookups();

  ST.activeIslandId = md.islands.length ? md.islands[0].id : null;
  ST.activeTypeId = 0;

  // Update map name input
  const inp = document.getElementById('mapNameInput');
  if(inp) inp.value = md.mapName;

  renderSidebar();
  renderCanvas();
  dirty = false;
}

function validateTypeObj(cat, t) {
  const obj = {
    id: typeof t.id==='number' ? t.id : (TYPE_ID_BASES[cat] || 0),
    name: typeof t.name==='string' ? t.name : 'Unnamed',
    stringKey: typeof t.stringKey==='string' ? t.stringKey : '',
    color: typeof t.color==='string' ? t.color : '#888888',
    enabled: typeof t.enabled==='boolean' ? t.enabled : true,
  };
  if(cat==='largeSpriteTypes'){
    obj.width = typeof t.width==='number' && t.width>=1 ? t.width : 1;
    obj.height = typeof t.height==='number' && t.height>=1 ? t.height : 1;
  }
  return obj;
}

function normalizeTypeIdsAndReferences() {
  const md = ST.md;
  const idMaps = {
    groundTypes: new Map(),
    decalTypes: new Map(),
    resourceTypes: new Map(),
    npcTypes: new Map(),
    largeSpriteTypes: new Map(),
  };

  for(const cat of CATEGORIES){
    const base = TYPE_ID_BASES[cat] || 0;
    const types = md.toolset[cat] || [];

    for(let i=0; i<types.length; i++){
      const t = types[i];
      const oldId = t.id;
      const newId = base + i;
      idMaps[cat].set(oldId, newId);
      t.id = newId;
    }
  }

  for(const isl of md.islands || []){
    for(const tile of isl.tiles || []){
      if(idMaps.groundTypes.has(tile.ground)){
        tile.ground = idMaps.groundTypes.get(tile.ground);
      }
    }
  }

  for(const inst of md.layers.decals || []){
    if(idMaps.decalTypes.has(inst.typeId)) inst.typeId = idMaps.decalTypes.get(inst.typeId);
  }
  for(const inst of md.layers.resources || []){
    if(idMaps.resourceTypes.has(inst.typeId)) inst.typeId = idMaps.resourceTypes.get(inst.typeId);
  }
  for(const inst of md.layers.npcs || []){
    if(idMaps.npcTypes.has(inst.typeId)) inst.typeId = idMaps.npcTypes.get(inst.typeId);
  }
  for(const inst of md.layers.largeSprites || []){
    if(idMaps.largeSpriteTypes.has(inst.typeId)) inst.typeId = idMaps.largeSpriteTypes.get(inst.typeId);
  }
}

function rebuildNextIds() {
  const md = ST.md;
  const maxId = (arr) => arr.length ? Math.max(...arr.map(x=>typeof x==='object'?x.id:0)) : -1;
  md._nid.gT = Math.max(TYPE_ID_BASES.groundTypes, maxId(md.toolset.groundTypes)+1);
  md._nid.dT = Math.max(TYPE_ID_BASES.decalTypes, maxId(md.toolset.decalTypes)+1);
  md._nid.rT = Math.max(TYPE_ID_BASES.resourceTypes, maxId(md.toolset.resourceTypes)+1);
  md._nid.nT = Math.max(TYPE_ID_BASES.npcTypes, maxId(md.toolset.npcTypes)+1);
  md._nid.lT = Math.max(TYPE_ID_BASES.largeSpriteTypes, maxId(md.toolset.largeSpriteTypes)+1);

  const islNums = md.islands.map(i=>parseInt(i.id.replace('island_',''))||0);
  md._nid.isl = islNums.length ? Math.max(...islNums)+1 : 1;

  md._nid.dI = maxId(md.layers.decals)+1;
  md._nid.rI = maxId(md.layers.resources)+1;
  md._nid.nI = maxId(md.layers.npcs)+1;
  md._nid.lI = maxId(md.layers.largeSprites)+1;
}

// ---- Data Folder Persistence ----
function supportsDataFolderPersistence(){
  return typeof window !== 'undefined'
    && typeof window.showDirectoryPicker === 'function'
    && typeof window.indexedDB !== 'undefined';
}

function openDataFolderDb(){
  return new Promise((resolve, reject)=>{
    const req = indexedDB.open(DATA_HANDLE_DB, 1);
    req.onupgradeneeded = () => {
      const db = req.result;
      if(!db.objectStoreNames.contains(DATA_HANDLE_STORE)){
        db.createObjectStore(DATA_HANDLE_STORE);
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error || new Error('Failed to open IndexedDB'));
  });
}

async function putStoredDataFolderHandle(handle){
  if(!supportsDataFolderPersistence()) return;
  const db = await openDataFolderDb();
  try {
    await new Promise((resolve, reject)=>{
      const tx = db.transaction(DATA_HANDLE_STORE, 'readwrite');
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error || new Error('Failed to write folder handle'));
      tx.objectStore(DATA_HANDLE_STORE).put(handle, DATA_HANDLE_KEY);
    });
  } finally {
    db.close();
  }
}

async function getStoredDataFolderHandle(){
  if(!supportsDataFolderPersistence()) return null;
  const db = await openDataFolderDb();
  try {
    return await new Promise((resolve, reject)=>{
      const tx = db.transaction(DATA_HANDLE_STORE, 'readonly');
      const req = tx.objectStore(DATA_HANDLE_STORE).get(DATA_HANDLE_KEY);
      req.onsuccess = () => resolve(req.result || null);
      req.onerror = () => reject(req.error || new Error('Failed to read folder handle'));
    });
  } finally {
    db.close();
  }
}

async function ensureFolderPermission(handle, mode, requestIfNeeded){
  if(!handle) return false;
  if(typeof handle.queryPermission !== 'function') return true;

  const opts = { mode: mode || 'readwrite' };
  let status = await handle.queryPermission(opts);
  if(status === 'granted') return true;

  if(!requestIfNeeded || typeof handle.requestPermission !== 'function') return false;
  status = await handle.requestPermission(opts);
  return status === 'granted';
}

function sanitizeMapFileName(name){
  const base = String(name || 'map').trim().toLowerCase();
  const normalized = base
    .replace(/[^a-z0-9_-]+/g, '-')
    .replace(/^-+/g, '')
    .replace(/-+$/g, '')
    .slice(0, 80);
  return normalized || 'map';
}

function buildMapSavePayload(){
  const data = buildExportData();
  data._nid = { ...ST.md._nid };
  return data;
}

function buildPaletteSavePayload(){
  return {
    version: 1,
    updatedAt: new Date().toISOString(),
    toolset: deepClone(ST.md.toolset),
  };
}

function hasValidPaletteShape(data){
  if(!data || typeof data !== 'object' || !data.toolset || typeof data.toolset !== 'object') return false;
  for(const cat of CATEGORIES){
    if(!Array.isArray(data.toolset[cat])) return false;
  }
  return true;
}

function normalizeToolsetFromPalette(data){
  const out = {};
  for(const cat of CATEGORIES){
    const arr = Array.isArray(data.toolset[cat]) ? data.toolset[cat] : [];
    out[cat] = arr.map(t => validateTypeObj(cat, t)).sort((a,b)=>a.id-b.id);
  }
  return out;
}

function applyPaletteToolsetData(data, options = {}){
  const opts = {
    recordHistory: false,
    markDirty: false,
    rerender: true,
    ...options,
  };

  if(!hasValidPaletteShape(data)) return false;

  if(opts.recordHistory){
    recordMutation('apply-palette');
  }

  ST.md.toolset = normalizeToolsetFromPalette(data);
  rebuildNextIds();

  const activeCat = TOOL_TO_CATEGORY[ST.tool];
  if(activeCat){
    const currentType = findType(activeCat, ST.activeTypeId);
    if(!currentType || !currentType.enabled){
      const enabled = getTypes(activeCat).filter(t=>t.enabled);
      ST.activeTypeId = enabled.length ? enabled[0].id : -1;
    }
  }

  if(opts.markDirty){
    dirty = true;
  }

  if(opts.rerender){
    renderSidebar();
    renderCanvas();
    renderRecentPalette();
  }

  return true;
}

async function loadBundledPalette(options = {}){
  const opts = {
    silent: false,
    ...options,
  };

  try {
    const response = await fetch(BUNDLED_PALETTE_PATH, { cache: 'no-store' });
    if(!response.ok){
      if(!opts.silent) warn('Bundled palette not found');
      return false;
    }

    const data = await response.json();
    const applied = applyPaletteToolsetData(data, {
      recordHistory: false,
      markDirty: false,
      rerender: true,
    });

    if(!applied){
      if(!opts.silent) warn('Bundled palette format is invalid');
      return false;
    }

    if(!opts.silent) ok('Bundled palette loaded');
    return true;
  } catch(_) {
    if(!opts.silent) warn('Unable to load bundled palette');
    return false;
  }
}

async function writeJsonFile(dirHandle, fileName, data){
  const fileHandle = await dirHandle.getFileHandle(fileName, { create: true });
  const writable = await fileHandle.createWritable();
  await writable.write(JSON.stringify(data, null, 2));
  await writable.close();
}

async function readJsonFile(dirHandle, fileName){
  const fileHandle = await dirHandle.getFileHandle(fileName);
  const file = await fileHandle.getFile();
  const text = await file.text();
  return JSON.parse(text);
}

async function saveToDataFolder(options = {}) {
  const opts = {
    silent: false,
    requestPermission: true,
    autosave: false,
    ...options,
  };

  if(!ST.dataFolderHandle){
    if(opts.requestPermission){
      await linkDataFolder();
    }
  }

  if(!ST.dataFolderHandle){
    if(!opts.silent) warn('No data folder linked');
    return false;
  }

  const granted = await ensureFolderPermission(ST.dataFolderHandle, 'readwrite', opts.requestPermission);
  if(!granted){
    if(!opts.silent) warn('Data folder write permission is required');
    return false;
  }

  try {
    const mapPayload = buildMapSavePayload();
    const palettePayload = buildPaletteSavePayload();
    const mapsHandle = await ST.dataFolderHandle.getDirectoryHandle(DATA_MAPS_DIR, { create: true });
    const mapFileName = sanitizeMapFileName(ST.md.mapName) + '.json';

    await writeJsonFile(ST.dataFolderHandle, DATA_CURRENT_MAP_FILE, mapPayload);
    await writeJsonFile(ST.dataFolderHandle, DATA_PALETTE_FILE, palettePayload);
    await writeJsonFile(mapsHandle, mapFileName, mapPayload);

    ST.folderAutosaveWarned = false;
    if(!opts.autosave) dirty = false;
    if(!opts.silent) ok('Saved map and palette to data folder');
    return true;
  } catch(e) {
    if(opts.autosave){
      if(!ST.folderAutosaveWarned){
        warn('Folder autosave failed: ' + e.message);
        ST.folderAutosaveWarned = true;
      }
    } else {
      err('Data folder save failed: ' + e.message);
    }
    return false;
  }
}

async function loadPaletteFromDataFolder(options = {}) {
  const opts = {
    silent: false,
    requestPermission: true,
    recordHistory: false,
    markDirty: false,
    ...options,
  };

  if(!ST.dataFolderHandle){
    if(!opts.silent) warn('No data folder linked');
    return false;
  }

  const granted = await ensureFolderPermission(ST.dataFolderHandle, 'read', opts.requestPermission);
  if(!granted){
    if(!opts.silent) warn('Data folder read permission is required');
    return false;
  }

  try {
    const data = await readJsonFile(ST.dataFolderHandle, DATA_PALETTE_FILE);
    const applied = applyPaletteToolsetData(data, {
      recordHistory: opts.recordHistory,
      markDirty: opts.markDirty,
      rerender: true,
    });

    if(!applied){
      if(!opts.silent) err('Palette load failed: invalid format');
      return false;
    }

    if(!opts.silent) ok('Loaded palette from data folder');
    return true;
  } catch(e) {
    if(!opts.silent) err('Palette load failed: ' + e.message);
    return false;
  }
}

async function loadFromDataFolder(options = {}) {
  const opts = {
    silent: false,
    requestPermission: true,
    ...options,
  };

  if(!ST.dataFolderHandle){
    if(opts.requestPermission){
      await linkDataFolder();
    }
  }

  if(!ST.dataFolderHandle){
    if(!opts.silent) warn('No data folder linked');
    return false;
  }

  const granted = await ensureFolderPermission(ST.dataFolderHandle, 'read', opts.requestPermission);
  if(!granted){
    if(!opts.silent) warn('Data folder read permission is required');
    return false;
  }

  try {
    const data = await readJsonFile(ST.dataFolderHandle, DATA_CURRENT_MAP_FILE);
    const result = validateImport(data);
    if(!result.valid){
      throw new Error(result.errors.join('; '));
    }
    if(data._nid) ST.md._nid = { ...data._nid };
    applyImport(result.data, true);
    const paletteLoaded = await loadPaletteFromDataFolder({
      silent: true,
      requestPermission: false,
      recordHistory: false,
      markDirty: false,
    });
    dirty = false;
    if(!opts.silent) ok(paletteLoaded ? 'Loaded map and palette from data folder' : 'Loaded current map from data folder');
    return true;
  } catch(e) {
    if(!opts.silent) err('Data folder load failed: ' + e.message);
    return false;
  }
}

async function linkDataFolder(){
  if(!supportsDataFolderPersistence()){
    warn('File System Access API is not available in this browser');
    return;
  }

  try {
    const handle = await window.showDirectoryPicker({ mode: 'readwrite' });
    const granted = await ensureFolderPermission(handle, 'readwrite', true);
    if(!granted){
      warn('Data folder permission was not granted');
      return;
    }

    ST.dataFolderHandle = handle;
    ST.dataFolderName = handle.name || 'data';
    ST.folderAutosaveWarned = false;
    await putStoredDataFolderHandle(handle);
    const loadedPalette = await loadPaletteFromDataFolder({
      silent: true,
      requestPermission: false,
      recordHistory: false,
      markDirty: false,
    });

    if(loadedPalette){
      ok('Data folder linked: ' + ST.dataFolderName + ' (palette loaded)');
    } else {
      await saveToDataFolder({ silent: true, requestPermission: false, autosave: false });
      ok('Data folder linked: ' + ST.dataFolderName + ' (initialized data files)');
    }
  } catch(e) {
    if(e && e.name === 'AbortError') return;
    err('Link data folder failed: ' + e.message);
  }
}

async function restoreLinkedDataFolder(){
  if(!supportsDataFolderPersistence()) return;

  try {
    const handle = await getStoredDataFolderHandle();
    if(!handle) return;

    ST.dataFolderHandle = handle;
    ST.dataFolderName = handle.name || 'data';
    ST.folderAutosaveWarned = false;

    const canRead = await ensureFolderPermission(handle, 'read', false);
    if(canRead){
      info('Data folder ready: ' + ST.dataFolderName);
    } else {
      info('Data folder linked. Click Link Data to re-grant permission.');
    }
  } catch(_) {
    // Keep browser-storage save flow working if IndexedDB handle restore fails.
  }
}

// ---- Local Storage ----
const LS_KEY = 'floatingIslandMapPainter';

function saveToLocal() {
  try {
    const data = buildExportData();
    data._nid = {...ST.md._nid};
    localStorage.setItem(LS_KEY, JSON.stringify(data));
    dirty = false;
    ok('Saved to browser storage');
  } catch(e){
    err('Save failed: '+e.message);
  }
}

function loadFromLocal() {
  try {
    const raw = localStorage.getItem(LS_KEY);
    if(!raw){ warn('No saved data found'); return; }
    const data = JSON.parse(raw);
    if(data._nid) ST.md._nid = {...data._nid};
    applyImport(data, true);
    ok('Loaded from browser storage');
  } catch(e){
    err('Load failed: '+e.message);
  }
}

function clearMap() {
  if(!confirm('Clear the entire map? This cannot be undone.')) return;
  recordMutation('clear-map');

  const prev = ST.md;
  const preservedToolset = deepClone(prev.toolset);
  const preservedCanvas = deepClone(prev.canvas);
  const preservedName = prev.mapName;
  const preservedNid = prev._nid ? { ...prev._nid } : {};

  ST.md = makeDefaultMapData();
  ST.md.mapName = preservedName || ST.md.mapName;
  ST.md.canvas = preservedCanvas;
  ST.md.toolset = preservedToolset;
  ST.md.islands = [];
  ST.md.layers = { decals: [], resources: [], npcs: [], largeSprites: [] };

  ST.md._nid.gT = preservedNid.gT ?? ST.md._nid.gT;
  ST.md._nid.dT = preservedNid.dT ?? ST.md._nid.dT;
  ST.md._nid.rT = preservedNid.rT ?? ST.md._nid.rT;
  ST.md._nid.nT = preservedNid.nT ?? ST.md._nid.nT;
  ST.md._nid.lT = preservedNid.lT ?? ST.md._nid.lT;
  ST.md._nid.isl = 1;
  ST.md._nid.dI = 1;
  ST.md._nid.rI = 1;
  ST.md._nid.nI = 1;
  ST.md._nid.lI = 1;

  rebuildLookups();
  ST.activeIslandId = null;

  const activeCat = TOOL_TO_CATEGORY[ST.tool];
  if(activeCat){
    const currentType = findType(activeCat, ST.activeTypeId);
    if(!currentType || !currentType.enabled){
      const enabled = getTypes(activeCat).filter(t=>t.enabled);
      ST.activeTypeId = enabled.length ? enabled[0].id : -1;
    }
  } else {
    ST.activeTypeId = -1;
  }

  const inp = document.getElementById('mapNameInput');
  if(inp) inp.value = ST.md.mapName;
  renderSidebar();
  renderCanvas();
  dirty = false;
  ok('Map cleared');
}

// ---- Autosave ----
async function runAutosaveTick() {
  let localSaved = false;
  let folderSaved = false;
  const folderLinked = !!ST.dataFolderHandle;

  try {
    const data = buildMapSavePayload();
    localStorage.setItem(LS_KEY+'_auto', JSON.stringify(data));
    localSaved = true;
  } catch(_) {}

  if(folderLinked){
    folderSaved = await saveToDataFolder({
      silent: true,
      requestPermission: false,
      autosave: true,
    });
  }

  if(folderLinked){
    dirty = !folderSaved;
  } else if(localSaved){
    dirty = false;
  }
}

function startAutosave() {
  if(autosaveTimer) clearInterval(autosaveTimer);
  autosaveTimer = setInterval(()=>{
    if(dirty){
      void runAutosaveTick();
    }
  }, 30000);
}

// ---- Mode/Tool setters ----
const S = {
  setMode(m){ ST.mode=m; if(m==='paint'){ /* ensure activeTypeId is valid */ } renderSidebar(); renderCanvas(); },
  setDefCat(c){ ST.defCat=c; renderSidebar(); },
  setTool(t){ ST.tool=t; const catMap={ground:'groundTypes',decal:'decalTypes',resource:'resourceTypes',npc:'npcTypes',largeSprite:'largeSpriteTypes'}; const cat=catMap[t]; if(cat){ const enabled=getTypes(cat).filter(x=>x.enabled); ST.activeTypeId=enabled.length?enabled[0].id:-1; } else { ST.activeTypeId=-1; } renderSidebar(); },
  setEraserTarget(t){ ST.eraserTarget=t; },
  setActiveTypeId(id){ ST.activeTypeId=id; renderSidebar(); },
  toggleRecentPalette,
  selectRecentTool,
  setBounds(w, h){ resizeCanvasBounds(w, h, { recordHistory: true, rerenderSidebar: true }); },
  toggleVis(key,val){ ST.vis[key]=val; renderCanvas(); },
  setMapName(name){ recordMutation('map-name'); ST.md.mapName=name; dirty=true; },
  // Re-export functions for inline handlers
  addType, updateTypeName, updateTypeColor, updateTypeStringKey, updateTypeWidth, updateTypeHeight,
  toggleTypeEnabled, deleteType,
  createIsland, selectIsland, renameIsland, changeIslandOutlineColor, deleteIsland,
  updateGenSetting, randomGenSeed, generateRandomIslands,
  saveToLocal, loadFromLocal, exportJSON, importJSON, copyJSON, clearMap,
  linkDataFolder, saveToDataFolder, loadFromDataFolder,
  undo, redo,
  zoomIn, zoomOut, zoomReset,
};

// ---- Init ----
async function init() {
  ST = makeDefaultState();
  ensureRecentToolsState();
  await loadBundledPalette({ silent: true });
  rebuildLookups();

  undoStack = [];
  redoStack = [];
  endHistoryGroup();
  updateHistoryButtons();

  setupCanvasEvents();
  renderSidebar();
  renderCanvas();
  document.addEventListener('keydown', handleUndoRedoHotkeys);

  // Try auto-load
  try {
    const raw = localStorage.getItem(LS_KEY+'_auto');
    if(raw){
      const data = JSON.parse(raw);
      if(data && data.canvas && data.canvas.cellSize === 1){
        info('Skipped legacy pixel-cell auto-save; using classic tile mode defaults');
      } else {
        if(data._nid) ST.md._nid = {...data._nid};
        applyImport(data, false);
        info('Auto-saved data restored');
      }
    } else {
      const raw2 = localStorage.getItem(LS_KEY);
      if(raw2){
        const data = JSON.parse(raw2);
        if(data && data.canvas && data.canvas.cellSize === 1){
          info('Skipped legacy pixel-cell save; using classic tile mode defaults');
        } else {
          if(data._nid) ST.md._nid = {...data._nid};
          applyImport(data, false);
          info('Saved data restored');
        }
      }
    }
  } catch(e){}

  // Update map name input
  const inp = document.getElementById('mapNameInput');
  if(inp) inp.value = ST.md.mapName;

  await restoreLinkedDataFolder();

  if(ST.dataFolderHandle){
    const loadedFromFolder = await loadPaletteFromDataFolder({
      silent: true,
      requestPermission: false,
      recordHistory: false,
      markDirty: false,
    });
    if(!loadedFromFolder){
      await loadBundledPalette({ silent: true });
    }
  } else {
    await loadBundledPalette({ silent: true });
  }

  startAutosave();
}

// Make globally accessible
window.S = S;

document.addEventListener('DOMContentLoaded', init);