(function (global) {
  'use strict';

  var ART_ROOT = '/gacha/art/';
  var LIBRARY_URL = ART_ROOT + 'library.json';
  var STATE_URL = '/gacha/state';
  var DRAW_URL = '/gacha/draw';
  var FORM_HEADER = 'X-Shop-Form';
  var FORM_META = 'shop-form';
  var REQUEST_TIMEOUT_MS = 15000;
  var FRAME_MS = 50;
  var MAX_CATCH_UP = 4;

  var ROOT = 0;
  var MOVIE = 422;
  var DRILL_SWITCH = 355;
  var NORMAL_DIG = 300;
  var DEEP_DIG = 354;
  var GLOW = 362;
  var HOLE = 369;
  var HOLE_TRIGGER = 367;
  var PRIZE_LOADING_FIELD = 389;
  var SCENES = { 372: '62', 377: '63', 382: '64', 386: '61' };
  var AREAS = ['61', '62', '63', '64'];
  var BACK_BUTTONS = ['btn1', 'btn2', 'btn6', 'btn7', 'btn8', 'btn9'];
  var DIG_START_FRAME = 1;
  var DIG_DONE_FRAME = 65;
  var DEEP_COST = 2;

  var FRAME_WELCOME = 42;
  var FRAME_AREA_LEAD_IN = 53;
  var FRAME_DRILL_CHOICE = 111;
  var FRAME_WAIT_LOOP = 184;
  var PICTURE_BOX = { x: 188, y: 213, width: 130, height: 150 };

  var LABEL_AREA = 'map';
  var LABEL_DRILL = 'gem';
  var LABEL_HOLE_CLICKED = 'movie3';
  var LABEL_REQUEST = 'scene8';
  var LABEL_DIG_DONE = 'scene5';
  var LABEL_NORMAL = 'normal';
  var LABEL_DEEP = 'sp';
  var LABEL_NO_ACCOUNT = 'wrongmv1';
  var LABEL_NO_COUPONS = 'wrongmv2';
  var LABEL_WRONG_AREA = 'wrongmv4';
  var LABEL_ACCOUNT_LOST = 'wrongmv5';
  var LABEL_SERVER_NO_COUPONS = 'wrongmv10';
  var LABEL_SERVER_ERROR = 'wrongmv14';

  var FONT_BOLD = { 3: true, 18: true, 117: true };
  var FONT_HEAVY = { 119: true };
  var FONT_SERIF = { 45: true };
  var WRAPPED_FIELDS = { 46: true };

  var TEXT = {
    12: { lines: [{ t: 'Server' }] },
    14: { lines: [{ t: 'Account' }] },
    15: { lines: [{ t: 'Coupons' }], max: 54 },
    39: { lines: [{ t: 'Welcome to Drill Egg!' }], center: true },
    44: {
      lines: [
        { t: '\u2605 How to Play', g: 0 },
        { t: '1. You need a coupon to play.', g: 1 },
        { t: '2. There are many dig sites, and each one hides its own limited rare treasures.', g: 2, indent: 13 },
        { t: '3. There are two drills: the Normal Drill and the Deep Drill. The Deep Drill uses one extra coupon, but your chance of finding a rare treasure goes up!', g: 3, indent: 13 },
        { t: '4. Choose a dig site, then drill wherever you think the treasure is buried. Happy hunting!', g: 5, indent: 13 }
      ]
    },
    118: { lines: [{ t: 'Dig Site', g: 0 }, { t: 'Click!', g: 1 }], center: true },
    121: { lines: [{ t: 'Dig Site', g: 0 }, { t: 'Click!', g: 1 }], center: true },
    249: {
      lines: [
        { t: 'Choosing a Drill', g: 0 },
        { t: 'Pick the Normal Drill or the Deep Drill below.', g: 1 },
        { t: 'The Deep Drill digs deeper than most players go, so rare treasures turn up more often. It costs one extra coupon.', g: 2 }
      ]
    },
    252: { lines: [{ t: 'Treasure List' }], center: true },
    254: { lines: [{ t: 'Treasure List' }], center: true },
    258: { lines: [{ t: 'Normal Drill' }], center: true },
    262: { lines: [{ t: 'Normal Drill' }], center: true },
    266: { lines: [{ t: 'Deep Drill' }], center: true },
    268: { lines: [{ t: 'Deep Drill' }], center: true },
    391: { lines: [{ t: 'Play Again' }], center: true },
    393: { lines: [{ t: 'Play Again' }], center: true },
    398: { lines: [{ t: 'My Treasures' }], center: true },
    400: { lines: [{ t: 'My Treasures' }], center: true },
    402: { lines: [{ t: 'Buy Coupons' }], center: true },
    403: { lines: [{ t: 'Buy Coupons' }], center: true },
    406: { lines: [{ t: 'Error Message' }], single: true },
    407: { lines: [{ t: 'Invalid access!!', g: 0 }, { t: 'Game account not found!!', g: 1 }], single: true },
    408: { lines: [{ t: 'You do not have enough coupons!!' }], single: true },
    409: { lines: [{ t: 'Invalid server selection!!' }], single: true },
    410: { lines: [{ t: 'The dig site selection failed!!' }], single: true },
    411: { lines: [{ t: 'Game account not found!!' }], single: true },
    412: { lines: [{ t: 'Server not found.' }], single: true },
    413: { lines: [{ t: 'Dig site selection error.' }], single: true },
    414: { lines: [{ t: 'Drill selection error!!' }], single: true },
    415: { lines: [{ t: 'Server name not found!!' }], single: true },
    416: { lines: [{ t: 'Not enough coupons!!' }], single: true },
    417: { lines: [{ t: 'This account does not exist!!' }], single: true },
    418: { lines: [{ t: 'Coupon record not found!!' }], single: true },
    419: { lines: [{ t: 'The item could not be delivered!!' }], single: true },
    420: { lines: [{ t: 'An error occurred!!' }], single: true },
    421: { lines: [{ t: 'An unexpected error occurred!!' }], single: true }
  };

  var FIELD_TEXT = {
    22: '',
    395: '* Character items you win go to the Paid Item Box.\r* House items go to the My House Inventory window.',
    396: 'All your coupons are used up. Please close this window, buy more coupons, and play again.'
  };

  var library = null;
  var player = null;
  var formToken = '';
  var account = { pending: false, loaded: false, waiting: null };
  var picture = { shown: null, image: null };

  function formTokenFromPage() {
    var metas = document.getElementsByTagName('meta');
    for (var i = 0; i < metas.length; i++) {
      if (metas[i].getAttribute('name') === FORM_META) {
        return metas[i].getAttribute('content') || '';
      }
    }
    return '';
  }

  function request(method, url, body, done) {
    var xhr = new XMLHttpRequest();
    var finished = false;
    function finish(status, text) {
      if (finished) {
        return;
      }
      finished = true;
      var parsed = null;
      if (status === 200 && text) {
        try {
          parsed = JSON.parse(text);
        } catch (parseError) {
          status = 0;
        }
      }
      done(status, parsed);
    }
    xhr.open(method, url, true);
    xhr.timeout = REQUEST_TIMEOUT_MS;
    if (method === 'POST') {
      xhr.setRequestHeader(FORM_HEADER, formToken);
      xhr.setRequestHeader('Content-Type', 'application/json');
    }
    xhr.onreadystatechange = function () {
      if (xhr.readyState === 4) {
        finish(xhr.status, xhr.responseText);
      }
    };
    xhr.ontimeout = function () {
      finish(0, null);
    };
    xhr.onerror = function () {
      finish(0, null);
    };
    xhr.send(body === null ? null : JSON.stringify(body));
  }

  function movie() {
    return player.findChild(player.root, 'movie1');
  }

  function header() {
    return player.findChild(player.root, 'cash_mv1');
  }

  function field(clip, name) {
    var inst = clip ? player.findChild(clip, name) : null;
    return inst && inst.kind === 'edittext' ? inst : null;
  }

  function readField(clip, name) {
    var inst = field(clip, name);
    return inst ? inst.text : '';
  }

  function writeField(clip, name, value) {
    var inst = field(clip, name);
    if (inst) {
      inst.text = value;
    }
  }

  function accountName() {
    return readField(header(), 'gamename');
  }

  function coupons() {
    return readField(header(), 'gacha');
  }

  function noCoupons() {
    var value = coupons();
    return value === '' || value === '0';
  }

  function couponNumber() {
    var value = parseInt(coupons(), 10);
    return isNaN(value) ? 0 : value;
  }

  function worldName(world) {
    return world > 0 ? 'World ' + world : '';
  }

  function go(clip, target, play) {
    player.gotoFrame(clip, target, play);
  }

  function stop(clip) {
    clip.playing = false;
  }

  function gate(clip) {
    if (account.pending) {
      stop(clip);
      account.waiting = { clip: clip, frame: clip.frame };
      return false;
    }
    if (accountName() === '') {
      go(clip, LABEL_NO_ACCOUNT, true);
      return false;
    }
    if (noCoupons()) {
      go(clip, LABEL_NO_COUPONS, true);
      return false;
    }
    return true;
  }

  function welcomeGate(clip) {
    if (gate(clip)) {
      stop(clip);
    }
  }

  function sceneGate(area) {
    return function (clip) {
      if (!gate(clip)) {
        return;
      }
      if (readField(player.root, 'selectmap') !== area) {
        go(clip, LABEL_WRONG_AREA, true);
        return;
      }
      stop(clip);
    };
  }

  function hideDeepDrill(clip) {
    if (couponNumber() < DEEP_COST) {
      var deep = player.findChild(clip, 'drill2');
      if (deep) {
        deep.visible = false;
      }
    }
  }

  function waitForPrize(clip) {
    var shown = player.findChild(clip, 'showmv');
    if (readField(shown, 'returnname') === '') {
      go(clip, FRAME_WAIT_LOOP, true);
    }
  }

  function showResult(clip) {
    var hidden = player.findAllChildren(clip, 'btnshow').concat(player.findAllChildren(clip, 'btnbuy'));
    for (var i = 0; i < hidden.length; i++) {
      hidden[i].visible = false;
    }
    for (var d = 0; d < clip.depths.length; d++) {
      var child = clip.children[clip.depths[d]];
      if (child && child.id === PRIZE_LOADING_FIELD) {
        child.visible = false;
      }
    }
    var again = player.findChild(clip, 'btnplay');
    var memo = player.findChild(clip, 'closememo');
    if (coupons() === '0') {
      setVisible(again, false);
      setVisible(memo, true);
    } else if (readField(player.root, 'selectdrill') === '1') {
      if (couponNumber() < DEEP_COST) {
        setVisible(again, false);
      } else {
        setVisible(again, true);
        setVisible(memo, false);
      }
    } else {
      setVisible(again, true);
      setVisible(memo, false);
    }
    stop(clip);
  }

  function setVisible(inst, visible) {
    if (inst) {
      inst.visible = visible;
    }
  }

  function digFinished(clip) {
    var scene = clip.parent && clip.parent.parent;
    if (scene && scene.kind === 'clip') {
      go(scene, LABEL_DIG_DONE, true);
    }
    stop(clip);
  }

  function drillSwitchNormal(clip) {
    var drill = readField(player.root, 'selectdrill');
    if (drill === '0') {
      stop(clip);
    } else if (drill === '1') {
      go(clip, LABEL_DEEP, true);
    }
  }

  function drillSwitchDeep(clip) {
    var drill = readField(player.root, 'selectdrill');
    if (drill === '0') {
      go(clip, LABEL_NORMAL, true);
    } else if (drill === '1') {
      stop(clip);
    }
  }

  function loopTo(frame) {
    return function (clip) {
      go(clip, frame, true);
    };
  }

  function boot(clip) {
    go(clip, accountName() === '' ? 2 : 5, true);
  }

  function stopHere(clip) {
    stop(clip);
  }

  function scriptsFor(id) {
    var scripts = {};
    if (id === ROOT) {
      scripts[0] = boot;
      scripts[5] = stopHere;
    } else if (id === MOVIE) {
      scripts[FRAME_WELCOME] = welcomeGate;
      scripts[94] = welcomeGate;
      scripts[108] = hideDeepDrill;
      scripts[135] = welcomeGate;
      scripts[137] = stopHere;
      scripts[138] = sceneGate('62');
      scripts[139] = sceneGate('63');
      scripts[140] = sceneGate('64');
      scripts[141] = sceneGate('61');
      scripts[179] = function (clip) { go(clip, LABEL_REQUEST, true); };
      scripts[195] = waitForPrize;
      scripts[196] = showResult;
      for (var f = 197; f <= 211; f++) {
        scripts[f] = stopHere;
      }
    } else if (id === 32 || id === 287 || id === 299) {
      scripts[18] = stopHere;
    } else if (id === 250) {
      scripts[0] = stopHere;
    } else if (id === 279) {
      scripts[7] = loopTo(0);
    } else if (id === 298) {
      scripts[7] = stopHere;
    } else if (id === 314) {
      scripts[12] = loopTo(0);
    } else if (id === 346) {
      scripts[0] = stopHere;
      scripts[44] = stopHere;
    } else if (id === NORMAL_DIG || id === DEEP_DIG) {
      scripts[0] = stopHere;
      scripts[DIG_DONE_FRAME] = digFinished;
    } else if (id === DRILL_SWITCH) {
      scripts[0] = drillSwitchNormal;
      scripts[1] = drillSwitchDeep;
    } else if (SCENES.hasOwnProperty(id)) {
      scripts[0] = stopHere;
      scripts[76] = stopHere;
    }
    return scripts;
  }

  var scriptTable = {};

  function chooseArea(area) {
    return function (button) {
      var clip = button.parent;
      if (accountName() === '') {
        go(clip, LABEL_NO_ACCOUNT, true);
      } else if (noCoupons()) {
        go(clip, LABEL_NO_COUPONS, true);
      } else {
        writeField(player.root, 'selectmap', area);
        go(clip, LABEL_DRILL, true);
      }
    };
  }

  function chooseDrill(drill) {
    return function (button) {
      var clip = button.parent;
      if (noCoupons()) {
        go(clip, LABEL_NO_COUPONS, true);
        return;
      }
      writeField(player.root, 'selectdrill', drill);
      var area = readField(player.root, 'selectmap');
      if (AREAS.indexOf(area) >= 0) {
        go(clip, 'map' + area, true);
      }
    };
  }

  function startDig(button) {
    go(button.parent, DIG_START_FRAME, true);
    var clip = movie();
    for (var i = 0; i < BACK_BUTTONS.length; i++) {
      setVisible(player.findChild(clip, BACK_BUTTONS[i]), false);
    }
  }

  function goTo(frame) {
    return function (button) {
      go(button.parent, frame, true);
    };
  }

  function playAgain() {
    var area = readField(player.root, 'selectmap');
    if (AREAS.indexOf(area) >= 0) {
      go(movie(), 'map' + area, true);
    }
  }

  var BUTTONS = {
    42: { overDownToOverUp: goTo(LABEL_AREA) },
    125: { overUpToOverDown: chooseArea('61') },
    198: { overUpToOverDown: chooseArea('62') },
    241: { overUpToOverDown: chooseArea('63') },
    242: { overUpToOverDown: chooseArea('64') },
    248: { overUpToOverDown: goTo(FRAME_WELCOME) },
    263: { overUpToOverDown: chooseDrill('0') },
    269: { overUpToOverDown: chooseDrill('1') },
    270: { overUpToOverDown: goTo(FRAME_AREA_LEAD_IN) },
    281: { overDownToOverUp: startDig },
    347: { overDownToOverUp: startDig },
    371: { overUpToOverDown: goTo(LABEL_HOLE_CLICKED) },
    376: { overUpToOverDown: goTo(LABEL_HOLE_CLICKED) },
    381: { overUpToOverDown: goTo(LABEL_HOLE_CLICKED) },
    385: { overUpToOverDown: goTo(LABEL_HOLE_CLICKED) },
    373: { overUpToOverDown: goTo(FRAME_DRILL_CHOICE) },
    394: { overUpToOverDown: playAgain },
    405: { overUpToOverDown: goTo(FRAME_AREA_LEAD_IN) }
  };

  function hasEvent(inst, name) {
    return inst.events && inst.events.indexOf(name) >= 0;
  }

  function followPointer(drill) {
    var scene = drill.parent;
    var target = player.localMouse(scene);
    var m = drill.matrix;
    var x = m[4] + (truncate(target[0]) - m[4]) * 0.9;
    var y = m[5] + (truncate(target[1]) - m[5]) * 0.9;
    drill.matrix = [m[0], m[1], m[2], m[3], Math.round(x * 20) / 20, Math.round(y * 20) / 20];
  }

  function truncate(value) {
    return value < 0 ? Math.ceil(value) : Math.floor(value);
  }

  function resume(clip) {
    clip.playing = true;
  }

  function clipEvents(parent, inst) {
    var parentId = parent.def.id;
    if (parentId === ROOT && inst.name === 'cash_mv1' && hasEvent(inst, 'load')) {
      return { load: loadAccount };
    }
    if (parentId === MOVIE && inst.name === 'showmv' && hasEvent(inst, 'load')) {
      return { load: drawPrize };
    }
    if (SCENES.hasOwnProperty(parentId)) {
      if (inst.id === DRILL_SWITCH && hasEvent(inst, 'enterFrame')) {
        return { enterFrame: followPointer };
      }
      if ((inst.id === GLOW || inst.id === HOLE) && hasEvent(inst, 'press')) {
        return { press: resume };
      }
    }
    if (parentId === HOLE && inst.id === HOLE_TRIGGER && hasEvent(inst, 'mouseDown')) {
      return {
        mouseDown: function (trigger) {
          go(movie(), LABEL_HOLE_CLICKED, true);
          stop(trigger);
        }
      };
    }
    return null;
  }

  function loadAccount() {
    account.pending = true;
    request('POST', STATE_URL, null, function (status, body) {
      account.pending = false;
      account.loaded = true;
      var cash = header();
      if (status === 200 && body && body.account) {
        writeField(cash, 'gamename', String(body.account));
        writeField(cash, 'gacha', String(body.coupons));
        writeField(player.root, 'serverno', String(body.world));
        writeField(player.root, 'servername', worldName(body.world));
        go(player.root, 5, true);
      } else {
        writeField(cash, 'gamename', '');
        writeField(cash, 'gacha', '');
        writeField(player.root, 'serverno', '');
        writeField(player.root, 'servername', '');
      }
      var waiting = account.waiting;
      account.waiting = null;
      if (waiting && !waiting.clip.dead && waiting.clip.frame === waiting.frame) {
        var script = scriptTable[waiting.clip.def.id][waiting.frame];
        if (script) {
          script.call(player, waiting.clip);
        }
      }
      player.runQueue();
      player.render();
    });
  }

  function prizeName(body) {
    var name = body.name ? String(body.name) : 'Item ' + body.item;
    if (body.count && body.count > 1) {
      name += ' x' + body.count;
    }
    return name;
  }

  function drawPrize(shown) {
    var area = parseInt(readField(player.root, 'selectmap'), 10);
    var deep = readField(player.root, 'selectdrill') === '1';
    request('POST', DRAW_URL, { area: area, deep: deep }, function (status, body) {
      if (shown.dead) {
        return;
      }
      var clip = shown.parent;
      if (status === 200 && body && typeof body.result === 'string') {
        if (typeof body.coupons === 'number') {
          writeField(header(), 'gacha', String(body.coupons));
        }
        if (body.result === 'won') {
          writeField(shown, 'returnname', prizeName(body));
          showPicture(shown, body.picture);
        } else if (body.result === 'coupons') {
          go(clip, LABEL_SERVER_NO_COUPONS, true);
        } else {
          go(clip, LABEL_SERVER_ERROR, true);
        }
      } else {
        go(clip, LABEL_ACCOUNT_LOST, true);
      }
      player.runQueue();
      player.render();
    });
  }

  function showPicture(shown, url) {
    picture.shown = null;
    picture.image = null;
    if (typeof url !== 'string' || url === '') {
      return;
    }
    var image = new Image();
    image.onload = function () {
      if (!shown.dead) {
        picture.shown = shown;
        picture.image = image;
        player.render();
      }
    };
    image.src = url;
  }

  function drawOverlay(ctx) {
    if (picture.image && picture.shown && !picture.shown.dead) {
      ctx.drawImage(picture.image, PICTURE_BOX.x, PICTURE_BOX.y, PICTURE_BOX.width, PICTURE_BOX.height);
    }
  }

  function fontCss(fontId, size) {
    var weight = FONT_HEAVY[fontId] ? '900 ' : FONT_BOLD[fontId] ? 'bold ' : '';
    var family = FONT_SERIF[fontId] ? 'serif' : 'sans-serif';
    return weight + size + 'px ' + family;
  }

  function lineGroups(def) {
    if (def.groups) {
      return def.groups;
    }
    var groups = [];
    var font = 0;
    var height = 12;
    var color = [0, 0, 0, 255];
    var x = 0;
    var y = 0;
    var current = null;
    for (var i = 0; i < def.runs.length; i++) {
      var run = def.runs[i];
      if (run.font !== undefined) {
        font = run.font;
      }
      if (run.height !== undefined) {
        height = run.height;
      }
      if (run.color) {
        color = run.color;
      }
      if (run.x !== undefined) {
        x = run.x;
      }
      var newLine = run.y !== undefined && (!current || run.y !== current.y);
      if (run.y !== undefined) {
        y = run.y;
      }
      var width = 0;
      for (var a = 0; a < run.advances.length; a++) {
        width += run.advances[a];
      }
      if (newLine || !current) {
        current = { font: font, height: height, color: color, x: x, y: y, width: 0 };
        groups.push(current);
      }
      current.width += width;
      x += width;
    }
    def.groups = groups;
    return groups;
  }

  function wrap(ctx, text, width) {
    var words = text.split(' ');
    var lines = [];
    var line = '';
    for (var i = 0; i < words.length; i++) {
      var candidate = line === '' ? words[i] : line + ' ' + words[i];
      if (line !== '' && ctx.measureText(candidate).width > width) {
        lines.push(line);
        line = words[i];
      } else {
        line = candidate;
      }
    }
    if (line !== '') {
      lines.push(line);
    }
    return lines;
  }

  function applyGlow(ctx, inst, cx) {
    if (!inst.filters) {
      return 1;
    }
    for (var i = 0; i < inst.filters.length; i++) {
      var filter = inst.filters[i];
      if (filter.type === 'glow' && !filter.inner) {
        ctx.shadowColor = DrillPlayer.css(DrillPlayer.transformColor(filter.color, cx));
        ctx.shadowBlur = Math.max(filter.blurX, filter.blurY);
        ctx.shadowOffsetX = 0;
        ctx.shadowOffsetY = 0;
        return Math.max(1, Math.min(4, Math.round(filter.strength)));
      }
    }
    return 1;
  }

  function drawText(ctx, inst, m, cx) {
    var def = inst.def;
    var full = DrillPlayer.concat(m, def.matrix);
    ctx.save();
    ctx.setTransform(full[0], full[1], full[2], full[3], full[4], full[5]);
    ctx.textBaseline = 'alphabetic';
    var passes = applyGlow(ctx, inst, cx);
    for (var pass = 0; pass < passes; pass++) {
      if (TEXT.hasOwnProperty(def.id)) {
        drawReplacement(ctx, def, TEXT[def.id], cx);
      } else {
        drawGlyphRuns(ctx, def, cx);
      }
    }
    ctx.restore();
  }

  function drawGlyphRuns(ctx, def, cx) {
    var font = 0;
    var height = 12;
    var color = [0, 0, 0, 255];
    var x = 0;
    var y = 0;
    for (var i = 0; i < def.runs.length; i++) {
      var run = def.runs[i];
      if (run.font !== undefined) {
        font = run.font;
      }
      if (run.height !== undefined) {
        height = run.height;
      }
      if (run.color) {
        color = run.color;
      }
      if (run.x !== undefined) {
        x = run.x;
      }
      if (run.y !== undefined) {
        y = run.y;
      }
      ctx.font = fontCss(font, height);
      ctx.fillStyle = DrillPlayer.css(DrillPlayer.transformColor(color, cx));
      var text = run.text || '';
      for (var g = 0; g < run.advances.length; g++) {
        var ch = text.charAt(g);
        if (ch) {
          ctx.fillText(ch, x, y);
        }
        x += run.advances[g];
      }
    }
  }

  function drawReplacement(ctx, def, entry, cx) {
    var groups = lineGroups(def);
    if (groups.length === 0) {
      return;
    }
    var spacing = groups.length > 1 ? (groups[groups.length - 1].y - groups[0].y) / (groups.length - 1) : groups[0].height * 1.3;
    var limit = def.bounds[2];
    var placed = [];
    for (var i = 0; i < entry.lines.length; i++) {
      var line = entry.lines[i];
      var group = groups[Math.min(line.g || 0, groups.length - 1)];
      ctx.font = fontCss(group.font, group.height);
      if (entry.center) {
        placed.push({ text: line.t, group: group, x: group.x + group.width / 2, center: true });
        continue;
      }
      var indent = line.indent || 0;
      var rows = entry.single ? [line.t] : wrap(ctx, line.t, limit - group.x);
      for (var r = 0; r < rows.length; r++) {
        if (r > 0) {
          var hanging = wrap(ctx, rows.slice(r).join(' '), limit - group.x - indent);
          for (var h = 0; h < hanging.length; h++) {
            placed.push({ text: hanging[h], group: group, x: group.x + indent });
          }
          break;
        }
        placed.push({ text: rows[r], group: group, x: group.x });
      }
    }
    var top = groups[0].y;
    var bottom = Math.max(def.bounds[3], groups[groups.length - 1].y);
    var step = spacing;
    if (placed.length > 1 && top + step * (placed.length - 1) > bottom) {
      step = Math.max(groups[0].height * 1.15, (bottom - top) / (placed.length - 1));
    }
    for (var p = 0; p < placed.length; p++) {
      var item = placed[p];
      var size = item.group.height;
      ctx.font = fontCss(item.group.font, size);
      if (entry.max && ctx.measureText(item.text).width > entry.max) {
        size = Math.floor(size * entry.max / ctx.measureText(item.text).width);
        ctx.font = fontCss(item.group.font, size);
      }
      var y = item.center ? item.group.y : placed.length === groups.length ? groups[p].y : top + step * p;
      ctx.fillStyle = DrillPlayer.css(DrillPlayer.transformColor(item.group.color, cx));
      if (item.center) {
        ctx.textAlign = 'center';
        ctx.fillText(item.text, item.x, y);
        ctx.textAlign = 'start';
      } else {
        ctx.fillText(item.text, item.x, y);
      }
    }
  }

  function drawEditText(ctx, inst, m, cx) {
    var def = inst.def;
    var text = inst.text;
    if (!text) {
      return;
    }
    var size = def.height || 12;
    var leading = def.leading === undefined ? 2 : def.leading;
    var color = def.color || [0, 0, 0, 255];
    var b = def.bounds;
    var left = b[0] + 2 + (def.leftMargin || 0);
    var right = b[2] - 2 - (def.rightMargin || 0);
    var width = right - left;
    ctx.save();
    ctx.setTransform(m[0], m[1], m[2], m[3], m[4], m[5]);
    ctx.font = fontCss(def.font, size);
    ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = DrillPlayer.css(DrillPlayer.transformColor(color, cx));
    var paragraphs = text.split(/\r\n|\r|\n/);
    var lines = [];
    for (var i = 0; i < paragraphs.length; i++) {
      if (def.wordWrap || WRAPPED_FIELDS[def.id]) {
        lines = lines.concat(wrap(ctx, paragraphs[i], width));
      } else {
        lines.push(paragraphs[i]);
      }
    }
    var align = def.align || 0;
    var y = b[1] + 2 + size * 0.9;
    for (var l = 0; l < lines.length; l++) {
      if (align === 1) {
        ctx.textAlign = 'right';
        ctx.fillText(lines[l], right, y);
      } else if (align === 2) {
        ctx.textAlign = 'center';
        ctx.fillText(lines[l], (left + right) / 2, y);
      } else {
        ctx.textAlign = 'left';
        ctx.fillText(lines[l], left, y);
      }
      y += size + leading;
    }
    ctx.restore();
  }

  function initialText(def) {
    if (FIELD_TEXT.hasOwnProperty(def.id)) {
      return FIELD_TEXT[def.id];
    }
    return def.text || '';
  }

  var behaviour = {
    frameScript: function (clip, index) {
      var id = clip.def.id;
      if (!scriptTable.hasOwnProperty(id)) {
        scriptTable[id] = scriptsFor(id);
      }
      return scriptTable[id][index] || null;
    },
    buttonAction: function (button, condition) {
      var actions = BUTTONS[button.def.id];
      return actions ? actions[condition] || null : null;
    },
    clipEvents: clipEvents,
    initialText: initialText,
    drawText: drawText,
    drawEditText: drawEditText,
    drawOverlay: drawOverlay
  };

  function SoundBank(entries) {
    this.entries = {};
    for (var id in entries) {
      if (entries.hasOwnProperty(id)) {
        this.entries[id] = { url: ART_ROOT + entries[id].file, pool: [] };
      }
    }
  }

  SoundBank.prototype.element = function (entry) {
    for (var i = 0; i < entry.pool.length; i++) {
      var audio = entry.pool[i];
      if (audio.paused || audio.ended) {
        return audio;
      }
    }
    var created = new Audio(entry.url);
    created.preload = 'auto';
    entry.pool.push(created);
    return created;
  };

  SoundBank.prototype.preload = function () {
    for (var id in this.entries) {
      if (this.entries.hasOwnProperty(id)) {
        this.element(this.entries[id]);
      }
    }
  };

  SoundBank.prototype.playing = function (entry) {
    for (var i = 0; i < entry.pool.length; i++) {
      if (!entry.pool[i].paused && !entry.pool[i].ended) {
        return true;
      }
    }
    return false;
  };

  SoundBank.prototype.play = function (info) {
    var entry = this.entries[info.id];
    if (!entry) {
      return;
    }
    if (info.stop) {
      for (var i = 0; i < entry.pool.length; i++) {
        entry.pool[i].pause();
      }
      return;
    }
    if (info.noMultiple && this.playing(entry)) {
      return;
    }
    var audio = this.element(entry);
    audio.loop = (info.loops || 0) > 1;
    try {
      audio.currentTime = 0;
    } catch (seekError) {
      audio.load();
    }
    audio.play();
  };

  function showFailure(canvas, message) {
    var ctx = canvas.getContext('2d');
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#fff';
    ctx.font = '16px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(message, canvas.width / 2, canvas.height / 2);
    canvas.style.visibility = 'visible';
  }

  function loadImages(entries, done) {
    var images = {};
    var waiting = 0;
    var finished = false;
    function settle() {
      waiting--;
      if (waiting === 0 && !finished) {
        finished = true;
        done(images);
      }
    }
    for (var id in entries) {
      if (entries.hasOwnProperty(id)) {
        waiting++;
      }
    }
    if (waiting === 0) {
      done(images);
      return;
    }
    for (var key in entries) {
      if (entries.hasOwnProperty(key)) {
        (function (bitmapId, file) {
          var image = new Image();
          image.onload = function () {
            images[bitmapId] = image;
            settle();
          };
          image.onerror = function () {
            settle();
          };
          image.src = ART_ROOT + file;
        })(key, entries[key].file);
      }
    }
  }

  function run(canvas, images, sounds) {
    player = new DrillPlayer.Player(canvas, library, { images: images, sounds: sounds }, behaviour);
    canvas.onmousemove = function (event) { player.onMouseMove(event || window.event); };
    canvas.onmousedown = function (event) {
      event = event || window.event;
      if (event.button === 0) {
        player.onMouseDown(event);
      }
      return false;
    };
    document.onmouseup = function (event) { player.onMouseUp(event || window.event); };
    canvas.onmouseout = function () { player.onMouseLeave(); };
    player.start();
    canvas.style.visibility = 'visible';
    var last = new Date().getTime();
    global.setInterval(function () {
      var now = new Date().getTime();
      var steps = Math.floor((now - last) / FRAME_MS);
      if (steps <= 0) {
        return;
      }
      if (steps > MAX_CATCH_UP) {
        steps = MAX_CATCH_UP;
        last = now;
      } else {
        last += steps * FRAME_MS;
      }
      for (var i = 0; i < steps; i++) {
        player.tick();
      }
    }, 10);
  }

  function main() {
    var canvas = document.getElementById('stage');
    canvas.oncontextmenu = function () { return false; };
    canvas.onselectstart = function () { return false; };
    formToken = formTokenFromPage();
    request('GET', LIBRARY_URL, null, function (status, body) {
      if (status !== 200 || !body || !body.characters || !body.root) {
        showFailure(canvas, 'The drill game could not be loaded. Please close this window and try again.');
        return;
      }
      library = body;
      var sounds = new SoundBank(library.sounds);
      sounds.preload();
      loadImages(library.bitmaps, function (images) {
        run(canvas, images, sounds);
      });
    });
  }

  if (document.readyState === 'complete' || document.readyState === 'interactive') {
    main();
  } else {
    global.onload = main;
  }
})(window);
