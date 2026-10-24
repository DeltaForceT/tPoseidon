(function (global) {
  'use strict';

  var GRADIENT_EXTENT = 16384;
  var RATIO_MAX = 65535;
  var PATH_MOVE = 0;
  var PATH_LINE = 1;
  var PATH_CURVE = 2;
  var PATH_CLOSE = 3;
  var CAPS = ['round', 'butt', 'square'];
  var JOINS = ['round', 'bevel', 'miter'];
  var IDENTITY = [1, 0, 0, 1, 0, 0];

  function concat(p, c) {
    return [
      p[0] * c[0] + p[2] * c[1],
      p[1] * c[0] + p[3] * c[1],
      p[0] * c[2] + p[2] * c[3],
      p[1] * c[2] + p[3] * c[3],
      p[0] * c[4] + p[2] * c[5] + p[4],
      p[1] * c[4] + p[3] * c[5] + p[5]
    ];
  }

  function invert(m) {
    var det = m[0] * m[3] - m[1] * m[2];
    if (Math.abs(det) < 1e-12) {
      return null;
    }
    return [
      m[3] / det,
      -m[1] / det,
      -m[2] / det,
      m[0] / det,
      (m[2] * m[5] - m[3] * m[4]) / det,
      (m[1] * m[4] - m[0] * m[5]) / det
    ];
  }

  function applyPoint(m, x, y) {
    return [m[0] * x + m[2] * y + m[4], m[1] * x + m[3] * y + m[5]];
  }

  function scaleOf(m) {
    return Math.sqrt(Math.abs(m[0] * m[3] - m[1] * m[2]));
  }

  function concatColor(p, c) {
    if (!c) {
      return p;
    }
    if (!p) {
      return c;
    }
    return [
      p[0] * c[0], p[1] * c[1], p[2] * c[2], p[3] * c[3],
      p[0] * c[4] + p[4], p[1] * c[5] + p[5], p[2] * c[6] + p[6], p[3] * c[7] + p[7]
    ];
  }

  function clampByte(v) {
    v = Math.round(v);
    return v < 0 ? 0 : v > 255 ? 255 : v;
  }

  function transformColor(color, cx) {
    if (!cx) {
      return color;
    }
    return [
      clampByte(color[0] * cx[0] + cx[4]),
      clampByte(color[1] * cx[1] + cx[5]),
      clampByte(color[2] * cx[2] + cx[6]),
      clampByte(color[3] * cx[3] + cx[7])
    ];
  }

  function css(color) {
    return 'rgba(' + color[0] + ',' + color[1] + ',' + color[2] + ',' + (color[3] / 255) + ')';
  }

  function lerp(a, b, t) {
    return a + (b - a) * t;
  }

  function lerpList(a, b, t) {
    var out = [];
    for (var i = 0; i < a.length; i++) {
      out.push(lerp(a[i], b[i], t));
    }
    return out;
  }

  function parsePath(d) {
    var ops = [];
    var pts = [];
    var i = 0;
    var n = d.length;
    function number() {
      while (i < n && d.charAt(i) === ' ') {
        i++;
      }
      var start = i;
      while (i < n) {
        var ch = d.charAt(i);
        if ((ch >= '0' && ch <= '9') || ch === '.' || ch === '-' || ch === 'e' || ch === 'E' || (ch === '+' && i > start)) {
          i++;
        } else {
          break;
        }
      }
      return parseFloat(d.substring(start, i));
    }
    while (i < n) {
      var c = d.charAt(i);
      i++;
      if (c === 'M') {
        ops.push(PATH_MOVE);
        pts.push(number(), number());
      } else if (c === 'L') {
        ops.push(PATH_LINE);
        pts.push(number(), number());
      } else if (c === 'Q') {
        ops.push(PATH_CURVE);
        pts.push(number(), number(), number(), number());
      } else if (c === 'Z') {
        ops.push(PATH_CLOSE);
      }
    }
    return { ops: ops, pts: pts };
  }

  function prepareShape(def) {
    var morph = def.type === 'morph';
    for (var l = 0; l < def.layers.length; l++) {
      var layer = def.layers[l];
      var lists = [layer.fillPaths, layer.linePaths];
      for (var k = 0; k < lists.length; k++) {
        for (var p = 0; p < lists[k].length; p++) {
          var path = lists[k][p];
          var start = parsePath(path.d);
          path.ops = start.ops;
          path.pts = start.pts;
          path.endPts = morph ? parsePath(path.e).pts : null;
        }
      }
    }
  }

  function tracePath(ctx, path, m, t) {
    var ops = path.ops;
    var pts = path.endPts && t > 0 ? lerpList(path.pts, path.endPts, t) : path.pts;
    var j = 0;
    for (var i = 0; i < ops.length; i++) {
      var op = ops[i];
      if (op === PATH_MOVE) {
        ctx.moveTo(m[0] * pts[j] + m[2] * pts[j + 1] + m[4], m[1] * pts[j] + m[3] * pts[j + 1] + m[5]);
        j += 2;
      } else if (op === PATH_LINE) {
        ctx.lineTo(m[0] * pts[j] + m[2] * pts[j + 1] + m[4], m[1] * pts[j] + m[3] * pts[j + 1] + m[5]);
        j += 2;
      } else if (op === PATH_CURVE) {
        ctx.quadraticCurveTo(
          m[0] * pts[j] + m[2] * pts[j + 1] + m[4], m[1] * pts[j] + m[3] * pts[j + 1] + m[5],
          m[0] * pts[j + 2] + m[2] * pts[j + 3] + m[4], m[1] * pts[j + 2] + m[3] * pts[j + 3] + m[5]);
        j += 4;
      } else {
        ctx.closePath();
      }
    }
  }

  function morphFill(style, t) {
    if (!style.start) {
      return style;
    }
    var a = style.start;
    var b = style.end;
    if (a.type === 'solid') {
      return { type: 'solid', color: lerpList(a.color, b.color, t) };
    }
    if (a.type === 'bitmap') {
      return { type: 'bitmap', bitmap: a.bitmap, matrix: lerpList(a.matrix, b.matrix, t), repeat: a.repeat, smooth: a.smooth };
    }
    var stops = [];
    for (var i = 0; i < a.stops.length; i++) {
      stops.push(lerpList(a.stops[i], b.stops[i], t));
    }
    return { type: a.type, matrix: lerpList(a.matrix, b.matrix, t), stops: stops, focal: lerp(a.focal, b.focal, t), spread: a.spread };
  }

  function morphLine(style, t) {
    if (!style.start) {
      return style;
    }
    return {
      width: lerp(style.start.width, style.end.width, t),
      color: lerpList(style.start.color, style.end.color, t),
      startCap: style.start.startCap,
      endCap: style.start.endCap,
      join: style.start.join,
      miter: style.start.miter
    };
  }

  function Player(canvas, library, assets, behaviour) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.hitCanvas = document.createElement('canvas');
    this.hitCanvas.width = 1;
    this.hitCanvas.height = 1;
    this.hitCtx = this.hitCanvas.getContext('2d');
    this.library = library;
    this.characters = library.characters;
    this.images = assets.images;
    this.sounds = assets.sounds;
    this.behaviour = behaviour;
    this.stage = library.stage;
    this.tintCache = {};
    this.patternCache = {};
    this.queue = [];
    this.tickId = 0;
    this.mouseX = -1;
    this.mouseY = -1;
    this.mouseInside = false;
    this.mouseDown = false;
    this.hover = null;
    this.pressed = null;
    this.mouseDownClips = [];
    this.enterFrameClips = [];
    for (var id in this.characters) {
      if (this.characters.hasOwnProperty(id)) {
        var def = this.characters[id];
        def.id = parseInt(id, 10);
        if (def.type === 'shape' || def.type === 'morph') {
          prepareShape(def);
        }
      }
    }
    var scale = Math.min(canvas.width / this.stage.width, canvas.height / this.stage.height);
    this.stageMatrix = [scale, 0, 0, scale, (canvas.width - this.stage.width * scale) / 2, (canvas.height - this.stage.height * scale) / 2];
    this.root = this.createClip({ id: 0, type: 'sprite', frameCount: library.root.frameCount, frames: library.root.frames }, null, true);
  }

  Player.prototype.createClip = function (def, parent, isRoot) {
    var clip = {
      kind: 'clip',
      def: def,
      parent: parent,
      isRoot: !!isRoot,
      frame: -1,
      playing: true,
      children: {},
      depths: [],
      bornTick: this.tickId,
      dead: false,
      visible: true,
      handlers: null,
      vars: {}
    };
    return clip;
  };

  Player.prototype.instantiate = function (op, frame, parent) {
    var def = this.characters[op.id];
    if (!def) {
      return null;
    }
    var inst;
    if (def.type === 'sprite') {
      inst = this.createClip(def, parent, false);
    } else if (def.type === 'button') {
      inst = { kind: 'button', def: def, parent: parent, state: 'up', records: [], dead: false, visible: true, bornTick: this.tickId };
      for (var i = 0; i < def.records.length; i++) {
        var rec = def.records[i];
        var child = this.instantiate({ id: rec.id }, 0, inst);
        if (child) {
          child.matrix = rec.matrix;
          child.colorTransform = rec.colorTransform || null;
          child.depth = rec.depth;
          child.states = rec.states;
          inst.records.push(child);
          if (child.kind === 'clip') {
            this.enterFirstFrame(child);
          }
        }
      }
      inst.records.sort(function (a, b) { return a.depth - b.depth; });
    } else if (def.type === 'edittext') {
      inst = { kind: 'edittext', def: def, parent: parent, text: this.behaviour.initialText(def), dead: false, visible: true };
    } else if (def.type === 'text') {
      inst = { kind: 'text', def: def, parent: parent, dead: false, visible: true };
    } else {
      inst = { kind: 'shape', def: def, parent: parent, dead: false, visible: true };
    }
    inst.id = op.id;
    inst.placed = frame;
    inst.matrix = IDENTITY;
    inst.colorTransform = null;
    inst.ratio = 0;
    inst.name = null;
    inst.clipDepth = 0;
    inst.filters = null;
    inst.events = [];
    return inst;
  };

  Player.prototype.applyPlacement = function (inst, op) {
    if (op.matrix) {
      inst.matrix = op.matrix;
    }
    if (op.colorTransform) {
      inst.colorTransform = op.colorTransform;
    }
    if (op.ratio !== undefined) {
      inst.ratio = op.ratio;
    }
    if (op.name !== undefined) {
      inst.name = op.name;
    }
    if (op.clipDepth !== undefined) {
      inst.clipDepth = op.clipDepth;
    }
    if (op.filters) {
      inst.filters = op.filters;
    }
    if (op.events) {
      inst.events = op.events;
    }
  };

  Player.prototype.attach = function (clip, depth, inst) {
    inst.depth = depth;
    if (!clip.children.hasOwnProperty(depth)) {
      clip.depths.push(depth);
      clip.depths.sort(function (a, b) { return a - b; });
    }
    clip.children[depth] = inst;
    this.bind(clip, inst);
    if (inst.kind === 'clip') {
      this.enterFirstFrame(inst);
    }
  };

  Player.prototype.detach = function (clip, depth) {
    var inst = clip.children[depth];
    if (!inst) {
      return;
    }
    delete clip.children[depth];
    for (var i = 0; i < clip.depths.length; i++) {
      if (clip.depths[i] === depth) {
        clip.depths.splice(i, 1);
        break;
      }
    }
    this.kill(inst);
  };

  Player.prototype.kill = function (inst) {
    if (inst.dead) {
      return;
    }
    inst.dead = true;
    if (inst.handlers && inst.handlers.unload) {
      inst.handlers.unload.call(this, inst);
    }
    if (inst.kind === 'clip') {
      for (var i = 0; i < inst.depths.length; i++) {
        this.kill(inst.children[inst.depths[i]]);
      }
    } else if (inst.kind === 'button') {
      for (var j = 0; j < inst.records.length; j++) {
        this.kill(inst.records[j]);
      }
    }
    if (this.hover === inst) {
      this.hover = null;
    }
    if (this.pressed === inst) {
      this.pressed = null;
    }
  };

  Player.prototype.bind = function (clip, inst) {
    var handlers = this.behaviour.clipEvents(clip, inst);
    inst.handlers = handlers;
    if (handlers && handlers.load) {
      this.queue.push({ target: inst, run: handlers.load });
    }
  };

  Player.prototype.enterFirstFrame = function (clip) {
    clip.frame = 0;
    this.queueScript(clip, 0);
    this.applyOps(clip, 0);
    this.startSounds(clip, 0);
  };

  Player.prototype.frameAt = function (clip, index) {
    var frames = clip.def.frames;
    return index < frames.length ? frames[index] : null;
  };

  Player.prototype.applyOps = function (clip, index) {
    var frame = this.frameAt(clip, index);
    if (!frame || !frame.ops) {
      return;
    }
    for (var i = 0; i < frame.ops.length; i++) {
      var op = frame.ops[i];
      if (op.op === 'remove') {
        this.detach(clip, op.depth);
        continue;
      }
      var existing = clip.children[op.depth];
      if (op.move) {
        if (!existing) {
          continue;
        }
        if (op.id !== undefined && op.id !== existing.id) {
          var replacement = this.instantiate(op, index, clip);
          if (!replacement) {
            continue;
          }
          replacement.matrix = existing.matrix;
          replacement.colorTransform = existing.colorTransform;
          replacement.ratio = existing.ratio;
          replacement.name = existing.name;
          replacement.clipDepth = existing.clipDepth;
          replacement.visible = existing.visible;
          this.applyPlacement(replacement, op);
          this.detach(clip, op.depth);
          this.attach(clip, op.depth, replacement);
        } else {
          this.applyPlacement(existing, op);
        }
        continue;
      }
      var inst = this.instantiate(op, index, clip);
      if (!inst) {
        continue;
      }
      this.applyPlacement(inst, op);
      if (existing) {
        this.detach(clip, op.depth);
      }
      this.attach(clip, op.depth, inst);
    }
  };

  Player.prototype.queueScript = function (clip, index) {
    var script = this.behaviour.frameScript(clip, index);
    if (script) {
      this.queue.push({ target: clip, run: script, frame: index });
    }
  };

  Player.prototype.startSounds = function (clip, index) {
    var frame = this.frameAt(clip, index);
    if (!frame || !frame.sounds) {
      return;
    }
    for (var i = 0; i < frame.sounds.length; i++) {
      this.playSound(frame.sounds[i]);
    }
  };

  Player.prototype.playSound = function (info) {
    if (this.sounds) {
      this.sounds.play(info);
    }
  };

  Player.prototype.labelFrame = function (clip, label) {
    var frames = clip.def.frames;
    for (var i = 0; i < frames.length; i++) {
      var labels = frames[i].labels;
      if (labels) {
        for (var j = 0; j < labels.length; j++) {
          if (labels[j] === label) {
            return i;
          }
        }
      }
    }
    return -1;
  };

  Player.prototype.gotoFrame = function (clip, target, play) {
    if (clip.dead) {
      return;
    }
    if (typeof target === 'string') {
      target = this.labelFrame(clip, target);
      if (target < 0) {
        return;
      }
    }
    var count = clip.def.frameCount;
    if (target < 0) {
      target = 0;
    }
    if (target >= count) {
      target = count - 1;
    }
    clip.playing = !!play;
    if (target === clip.frame) {
      return;
    }
    this.seek(clip, target);
  };

  Player.prototype.seek = function (clip, target) {
    if (target > clip.frame) {
      this.queueScript(clip, target);
      for (var i = clip.frame + 1; i <= target; i++) {
        this.applyOps(clip, i);
      }
    } else {
      this.queueScript(clip, target);
      this.rebuild(clip, target);
    }
    clip.frame = target;
    this.startSounds(clip, target);
  };

  Player.prototype.rebuild = function (clip, target) {
    var state = {};
    for (var i = 0; i <= target; i++) {
      var frame = this.frameAt(clip, i);
      if (!frame || !frame.ops) {
        continue;
      }
      for (var k = 0; k < frame.ops.length; k++) {
        var op = frame.ops[k];
        if (op.op === 'remove') {
          delete state[op.depth];
          continue;
        }
        var rec = state[op.depth];
        if (op.move) {
          if (!rec) {
            continue;
          }
          if (op.id !== undefined && op.id !== rec.id) {
            rec = { id: op.id, placed: i, ops: rec.ops.slice(0) };
            state[op.depth] = rec;
          }
          rec.ops.push(op);
        } else {
          state[op.depth] = { id: op.id, placed: i, ops: [op] };
        }
      }
    }
    var depths = clip.depths.slice(0);
    for (var d = 0; d < depths.length; d++) {
      var depth = depths[d];
      var current = clip.children[depth];
      var wanted = state[depth];
      if (!wanted || wanted.id !== current.id || wanted.placed !== current.placed) {
        this.detach(clip, depth);
      }
    }
    var keys = [];
    for (var key in state) {
      if (state.hasOwnProperty(key)) {
        keys.push(parseInt(key, 10));
      }
    }
    keys.sort(function (a, b) { return a - b; });
    for (var n = 0; n < keys.length; n++) {
      var at = keys[n];
      var entry = state[at];
      var inst = clip.children[at];
      var fresh = !inst;
      if (fresh) {
        inst = this.instantiate({ id: entry.id }, entry.placed, clip);
        if (!inst) {
          continue;
        }
      }
      inst.matrix = IDENTITY;
      inst.colorTransform = null;
      for (var p = 0; p < entry.ops.length; p++) {
        this.applyPlacement(inst, entry.ops[p]);
      }
      if (fresh) {
        this.attach(clip, at, inst);
      }
    }
  };

  Player.prototype.advance = function (clip) {
    if (clip.dead) {
      return;
    }
    if (clip.bornTick !== this.tickId && clip.playing && clip.def.frameCount > 1) {
      var next = clip.frame + 1;
      if (next >= clip.def.frameCount) {
        next = 0;
      }
      this.seek(clip, next);
    }
    var depths = clip.depths.slice(0);
    for (var i = 0; i < depths.length; i++) {
      var child = clip.children[depths[i]];
      if (!child || child.dead) {
        continue;
      }
      if (child.kind === 'clip') {
        this.advance(child);
      } else if (child.kind === 'button') {
        for (var r = 0; r < child.records.length; r++) {
          var record = child.records[r];
          if (record.kind === 'clip' && this.recordShown(child, record)) {
            this.advance(record);
          }
        }
      }
    }
  };

  Player.prototype.recordShown = function (button, record) {
    var states = record.states;
    for (var i = 0; i < states.length; i++) {
      if (states[i] === button.state) {
        return true;
      }
    }
    return false;
  };

  Player.prototype.runQueue = function () {
    var guard = 0;
    while (this.queue.length > 0 && guard < 10000) {
      guard++;
      var item = this.queue.shift();
      if (item.target.dead) {
        continue;
      }
      if (item.frame !== undefined && item.target.frame !== item.frame) {
        continue;
      }
      item.run.call(this, item.target);
    }
  };

  Player.prototype.collectEnterFrame = function (clip, list) {
    var depths = clip.depths;
    for (var i = 0; i < depths.length; i++) {
      var child = clip.children[depths[i]];
      if (!child || child.dead) {
        continue;
      }
      if (child.handlers && child.handlers.enterFrame) {
        list.push(child);
      }
      if (child.kind === 'clip') {
        this.collectEnterFrame(child, list);
      }
    }
  };

  Player.prototype.start = function () {
    this.enterFirstFrame(this.root);
    this.runQueue();
    this.render();
  };

  Player.prototype.tick = function () {
    this.tickId++;
    this.advance(this.root);
    this.runQueue();
    var list = [];
    this.collectEnterFrame(this.root, list);
    for (var i = 0; i < list.length; i++) {
      if (!list[i].dead) {
        list[i].handlers.enterFrame.call(this, list[i]);
      }
    }
    this.runQueue();
    this.updateHover();
    this.render();
  };

  Player.prototype.findChild = function (clip, name) {
    if (!clip || clip.kind !== 'clip') {
      return null;
    }
    for (var i = 0; i < clip.depths.length; i++) {
      var child = clip.children[clip.depths[i]];
      if (child && child.name === name) {
        return child;
      }
    }
    return null;
  };

  Player.prototype.findAllChildren = function (clip, name) {
    var found = [];
    for (var i = 0; i < clip.depths.length; i++) {
      var child = clip.children[clip.depths[i]];
      if (child && child.name === name) {
        found.push(child);
      }
    }
    return found;
  };

  Player.prototype.worldMatrix = function (inst) {
    var chain = [];
    var node = inst;
    while (node && !node.isRoot) {
      chain.push(node.matrix);
      node = node.parent;
    }
    var m = this.stageMatrix;
    for (var i = chain.length - 1; i >= 0; i--) {
      m = concat(m, chain[i]);
    }
    return m;
  };

  Player.prototype.localMouse = function (clip) {
    var inverse = invert(this.worldMatrix(clip));
    if (!inverse) {
      return [0, 0];
    }
    return applyPoint(inverse, this.mouseX, this.mouseY);
  };

  Player.prototype.render = function () {
    var ctx = this.ctx;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.globalAlpha = 1;
    ctx.fillStyle = css(this.stage.background);
    ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
    this.renderChildren(ctx, this.root, this.stageMatrix, null);
    if (this.behaviour.drawOverlay) {
      ctx.setTransform(this.stageMatrix[0], this.stageMatrix[1], this.stageMatrix[2], this.stageMatrix[3], this.stageMatrix[4], this.stageMatrix[5]);
      ctx.globalAlpha = 1;
      this.behaviour.drawOverlay(ctx);
    }
  };

  Player.prototype.renderChildren = function (ctx, clip, m, cx) {
    var depths = clip.depths;
    var i = 0;
    while (i < depths.length) {
      var child = clip.children[depths[i]];
      i++;
      if (!child || child.dead) {
        continue;
      }
      if (child.clipDepth) {
        var masked = [];
        while (i < depths.length && depths[i] <= child.clipDepth) {
          masked.push(clip.children[depths[i]]);
          i++;
        }
        ctx.save();
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.beginPath();
        this.addOutline(ctx, child, concat(m, child.matrix), 0);
        ctx.clip();
        for (var k = 0; k < masked.length; k++) {
          if (masked[k] && !masked[k].dead) {
            this.renderObject(ctx, masked[k], m, cx);
          }
        }
        ctx.restore();
        continue;
      }
      this.renderObject(ctx, child, m, cx);
    }
  };

  Player.prototype.renderObject = function (ctx, inst, m, cx) {
    if (!inst.visible) {
      return;
    }
    var world = concat(m, inst.matrix);
    var color = concatColor(cx, inst.colorTransform);
    if (color && color[3] <= 0 && color[7] <= 0) {
      return;
    }
    if (inst.kind === 'shape') {
      this.drawShape(ctx, inst.def, world, color, inst.ratio / RATIO_MAX);
    } else if (inst.kind === 'clip') {
      this.renderChildren(ctx, inst, world, color);
    } else if (inst.kind === 'button') {
      for (var i = 0; i < inst.records.length; i++) {
        var record = inst.records[i];
        if (this.recordShown(inst, record)) {
          this.renderObject(ctx, record, world, color);
        }
      }
    } else if (inst.kind === 'text') {
      this.behaviour.drawText(ctx, inst, world, color);
    } else if (inst.kind === 'edittext') {
      this.behaviour.drawEditText(ctx, inst, world, color);
    }
  };

  Player.prototype.addOutline = function (ctx, inst, m, depth) {
    if (depth > 16 || inst.dead) {
      return;
    }
    if (inst.kind === 'shape') {
      var def = inst.def;
      var t = def.type === 'morph' ? inst.ratio / RATIO_MAX : 0;
      for (var l = 0; l < def.layers.length; l++) {
        var paths = def.layers[l].fillPaths;
        for (var p = 0; p < paths.length; p++) {
          tracePath(ctx, paths[p], m, t);
        }
      }
    } else if (inst.kind === 'clip') {
      for (var i = 0; i < inst.depths.length; i++) {
        var child = inst.children[inst.depths[i]];
        if (child && !child.clipDepth) {
          this.addOutline(ctx, child, concat(m, child.matrix), depth + 1);
        }
      }
    } else if (inst.kind === 'button') {
      for (var r = 0; r < inst.records.length; r++) {
        var record = inst.records[r];
        if (this.recordShown(inst, record)) {
          this.addOutline(ctx, record, concat(m, record.matrix), depth + 1);
        }
      }
    } else if (inst.kind === 'text' || inst.kind === 'edittext') {
      var b = inst.def.bounds;
      var corners = [applyPoint(m, b[0], b[1]), applyPoint(m, b[2], b[1]), applyPoint(m, b[2], b[3]), applyPoint(m, b[0], b[3])];
      ctx.moveTo(corners[0][0], corners[0][1]);
      for (var c = 1; c < 4; c++) {
        ctx.lineTo(corners[c][0], corners[c][1]);
      }
      ctx.closePath();
    }
  };

  Player.prototype.drawShape = function (ctx, def, m, cx, t) {
    var morph = def.type === 'morph';
    for (var l = 0; l < def.layers.length; l++) {
      var layer = def.layers[l];
      for (var f = 0; f < layer.fillPaths.length; f++) {
        var fillPath = layer.fillPaths[f];
        var fill = layer.fills[fillPath.style - 1];
        if (!fill) {
          continue;
        }
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.beginPath();
        tracePath(ctx, fillPath, m, morph ? t : 0);
        this.paintFill(ctx, morph ? morphFill(fill, t) : fill, m, cx);
      }
      for (var s = 0; s < layer.linePaths.length; s++) {
        var linePath = layer.linePaths[s];
        var line = layer.lines[linePath.style - 1];
        if (!line) {
          continue;
        }
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.beginPath();
        tracePath(ctx, linePath, m, morph ? t : 0);
        this.strokeLine(ctx, morph ? morphLine(line, t) : line, m, cx);
      }
    }
    ctx.setTransform(1, 0, 0, 1, 0, 0);
  };

  Player.prototype.gradient = function (ctx, fill, cx) {
    var g;
    if (fill.type === 'linear') {
      g = ctx.createLinearGradient(-GRADIENT_EXTENT, 0, GRADIENT_EXTENT, 0);
    } else {
      var focal = fill.type === 'focal' ? fill.focal * GRADIENT_EXTENT : 0;
      g = ctx.createRadialGradient(focal, 0, 0, 0, 0, GRADIENT_EXTENT);
    }
    for (var i = 0; i < fill.stops.length; i++) {
      var stop = fill.stops[i];
      g.addColorStop(Math.min(1, Math.max(0, stop[0] / 255)), css(transformColor([stop[1], stop[2], stop[3], stop[4]], cx)));
    }
    return g;
  };

  Player.prototype.paintFill = function (ctx, fill, m, cx) {
    if (fill.type === 'solid') {
      ctx.fillStyle = css(transformColor(fill.color, cx));
      ctx.fill();
      return;
    }
    var fm = concat(m, fill.matrix);
    if (Math.abs(fm[0] * fm[3] - fm[1] * fm[2]) < 1e-9) {
      if (fill.stops && fill.stops.length > 0) {
        var last = fill.stops[fill.stops.length - 1];
        ctx.fillStyle = css(transformColor([last[1], last[2], last[3], last[4]], cx));
        ctx.fill();
      }
      return;
    }
    ctx.save();
    if (fill.type === 'bitmap') {
      var image = this.bitmapFor(fill.bitmap, cx);
      if (!image) {
        ctx.restore();
        return;
      }
      if (cx && image.alphaOnly) {
        ctx.globalAlpha = Math.max(0, Math.min(1, cx[3] + cx[7] / 255));
      }
      ctx.setTransform(fm[0], fm[1], fm[2], fm[3], fm[4], fm[5]);
      if ('imageSmoothingEnabled' in ctx) {
        ctx.imageSmoothingEnabled = fill.smooth;
      } else if ('msImageSmoothingEnabled' in ctx) {
        ctx.msImageSmoothingEnabled = fill.smooth;
      }
      ctx.fillStyle = this.patternFor(ctx, image, fill.repeat);
    } else {
      ctx.setTransform(fm[0], fm[1], fm[2], fm[3], fm[4], fm[5]);
      ctx.fillStyle = this.gradient(ctx, fill, cx);
    }
    ctx.fill();
    ctx.restore();
  };

  Player.prototype.patternFor = function (ctx, image, repeat) {
    var key = image.cacheKey + (repeat ? '|r' : '|n');
    var pattern = this.patternCache[key];
    if (!pattern) {
      pattern = ctx.createPattern(image.source, repeat ? 'repeat' : 'no-repeat');
      this.patternCache[key] = pattern;
    }
    return pattern;
  };

  Player.prototype.bitmapFor = function (id, cx) {
    var source = this.images[id];
    if (!source) {
      return null;
    }
    var tinted = cx && (cx[0] !== 1 || cx[1] !== 1 || cx[2] !== 1 || cx[4] !== 0 || cx[5] !== 0 || cx[6] !== 0);
    if (!tinted) {
      return { source: source, cacheKey: String(id), alphaOnly: true };
    }
    var key = id + '|' + cx.join(',');
    var cached = this.tintCache[key];
    if (!cached) {
      var canvas = document.createElement('canvas');
      canvas.width = source.width;
      canvas.height = source.height;
      var tctx = canvas.getContext('2d');
      tctx.drawImage(source, 0, 0);
      var data = tctx.getImageData(0, 0, canvas.width, canvas.height);
      var px = data.data;
      for (var i = 0; i < px.length; i += 4) {
        px[i] = clampByte(px[i] * cx[0] + cx[4]);
        px[i + 1] = clampByte(px[i + 1] * cx[1] + cx[5]);
        px[i + 2] = clampByte(px[i + 2] * cx[2] + cx[6]);
        px[i + 3] = clampByte(px[i + 3] * cx[3] + cx[7]);
      }
      tctx.putImageData(data, 0, 0);
      cached = { source: canvas, cacheKey: key, alphaOnly: false };
      this.tintCache[key] = cached;
    }
    return cached;
  };

  Player.prototype.strokeLine = function (ctx, line, m, cx) {
    var width = line.width * scaleOf(m);
    ctx.lineWidth = width < 1 ? 1 : width;
    ctx.lineCap = CAPS[line.startCap || 0] || 'round';
    ctx.lineJoin = JOINS[line.join || 0] || 'round';
    ctx.miterLimit = line.miter || 3;
    if (line.fill) {
      var fill = line.fill;
      if (fill.type === 'solid') {
        ctx.strokeStyle = css(transformColor(fill.color, cx));
      } else if (fill.stops && fill.stops.length > 0) {
        var fm = concat(m, fill.matrix);
        var a = applyPoint(fm, -GRADIENT_EXTENT, 0);
        var b = applyPoint(fm, GRADIENT_EXTENT, 0);
        var g = fill.type === 'linear'
          ? ctx.createLinearGradient(a[0], a[1], b[0], b[1])
          : ctx.createRadialGradient(fm[4], fm[5], 0, fm[4], fm[5], GRADIENT_EXTENT * scaleOf(fm));
        for (var i = 0; i < fill.stops.length; i++) {
          var stop = fill.stops[i];
          g.addColorStop(stop[0] / 255, css(transformColor([stop[1], stop[2], stop[3], stop[4]], cx)));
        }
        ctx.strokeStyle = g;
      } else {
        return;
      }
    } else {
      ctx.strokeStyle = css(transformColor(line.color, cx));
    }
    ctx.stroke();
  };

  Player.prototype.hitObject = function (inst, m, x, y, depth) {
    if (depth > 16 || inst.dead || !inst.visible) {
      return false;
    }
    var ctx = this.hitCtx;
    if (inst.kind === 'shape') {
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      ctx.beginPath();
      this.addOutline(ctx, inst, m, depth);
      return ctx.isPointInPath(x, y);
    }
    if (inst.kind === 'text' || inst.kind === 'edittext') {
      var inverse = invert(m);
      if (!inverse) {
        return false;
      }
      var p = applyPoint(inverse, x, y);
      var b = inst.def.bounds;
      return p[0] >= b[0] && p[0] <= b[2] && p[1] >= b[1] && p[1] <= b[3];
    }
    if (inst.kind === 'clip') {
      for (var i = 0; i < inst.depths.length; i++) {
        var child = inst.children[inst.depths[i]];
        if (child && !child.clipDepth && this.hitObject(child, concat(m, child.matrix), x, y, depth + 1)) {
          return true;
        }
      }
      return false;
    }
    if (inst.kind === 'button') {
      for (var r = 0; r < inst.records.length; r++) {
        var record = inst.records[r];
        if (record.states.indexOf('hit') >= 0 && this.hitObject(record, concat(m, record.matrix), x, y, depth + 1)) {
          return true;
        }
      }
    }
    return false;
  };

  Player.prototype.findTarget = function (clip, m, x, y) {
    for (var i = clip.depths.length - 1; i >= 0; i--) {
      var child = clip.children[clip.depths[i]];
      if (!child || child.dead || !child.visible || child.clipDepth) {
        continue;
      }
      var world = concat(m, child.matrix);
      if (child.kind === 'button') {
        if (this.hitObject(child, world, x, y, 0)) {
          return child;
        }
      } else if (child.kind === 'clip') {
        if (child.handlers && child.handlers.press && this.hitObject(child, world, x, y, 0)) {
          return child;
        }
        var nested = this.findTarget(child, world, x, y);
        if (nested) {
          return nested;
        }
      }
    }
    return null;
  };

  Player.prototype.buttonEvent = function (target, condition) {
    if (!target || target.dead) {
      return;
    }
    if (target.kind === 'button') {
      var sound = target.def.sounds && target.def.sounds[condition];
      if (sound) {
        this.playSound(sound);
      }
      var action = this.behaviour.buttonAction(target, condition);
      if (action) {
        action.call(this, target);
      }
    } else if (target.kind === 'clip' && target.handlers) {
      if (condition === 'overUpToOverDown' && target.handlers.press) {
        target.handlers.press.call(this, target);
      }
    }
  };

  Player.prototype.setState = function (target, state) {
    if (target && target.kind === 'button') {
      target.state = state;
    }
  };

  Player.prototype.updateHover = function () {
    var target = this.mouseInside ? this.findTarget(this.root, this.stageMatrix, this.mouseX, this.mouseY) : null;
    if (this.pressed) {
      var over = target === this.pressed;
      var wasOver = this.pressed.state === 'down';
      if (over && !wasOver) {
        this.setState(this.pressed, 'down');
        this.buttonEvent(this.pressed, 'outDownToOverDown');
      } else if (!over && wasOver) {
        this.setState(this.pressed, 'over');
        this.buttonEvent(this.pressed, 'overDownToOutDown');
      }
    } else if (target !== this.hover) {
      if (this.hover && !this.hover.dead) {
        this.setState(this.hover, 'up');
        this.buttonEvent(this.hover, 'overUpToIdle');
      }
      this.hover = target;
      if (target) {
        this.setState(target, 'over');
        this.buttonEvent(target, 'idleToOverUp');
      }
    }
    this.canvas.style.cursor = (this.pressed || this.hover) ? 'pointer' : 'default';
    this.runQueue();
  };

  Player.prototype.collectMouseDown = function (clip, list) {
    for (var i = 0; i < clip.depths.length; i++) {
      var child = clip.children[clip.depths[i]];
      if (!child || child.dead) {
        continue;
      }
      if (child.handlers && child.handlers.mouseDown) {
        list.push(child);
      }
      if (child.kind === 'clip') {
        this.collectMouseDown(child, list);
      }
    }
  };

  Player.prototype.pointer = function (event) {
    var rect = this.canvas.getBoundingClientRect();
    this.mouseX = (event.clientX - rect.left) * (this.canvas.width / rect.width);
    this.mouseY = (event.clientY - rect.top) * (this.canvas.height / rect.height);
    this.mouseInside = this.mouseX >= 0 && this.mouseY >= 0 && this.mouseX < this.canvas.width && this.mouseY < this.canvas.height;
  };

  Player.prototype.onMouseMove = function (event) {
    this.pointer(event);
    this.updateHover();
    this.render();
  };

  Player.prototype.onMouseDown = function (event) {
    this.pointer(event);
    this.mouseDown = true;
    var list = [];
    this.collectMouseDown(this.root, list);
    for (var i = 0; i < list.length; i++) {
      if (!list[i].dead) {
        list[i].handlers.mouseDown.call(this, list[i]);
      }
    }
    this.runQueue();
    this.updateHover();
    if (this.hover && !this.hover.dead) {
      this.pressed = this.hover;
      this.setState(this.pressed, 'down');
      this.buttonEvent(this.pressed, 'overUpToOverDown');
    }
    this.runQueue();
    this.updateHover();
    this.render();
  };

  Player.prototype.onMouseUp = function (event) {
    this.pointer(event);
    this.mouseDown = false;
    var pressed = this.pressed;
    this.pressed = null;
    if (pressed && !pressed.dead) {
      if (pressed.state === 'down') {
        this.setState(pressed, 'over');
        this.buttonEvent(pressed, 'overDownToOverUp');
      } else {
        this.setState(pressed, 'up');
        this.buttonEvent(pressed, 'outDownToIdle');
        this.hover = null;
      }
    }
    this.runQueue();
    this.updateHover();
    this.render();
  };

  Player.prototype.onMouseLeave = function () {
    this.mouseInside = false;
    this.updateHover();
    this.render();
  };

  global.DrillPlayer = {
    Player: Player,
    concat: concat,
    applyPoint: applyPoint,
    transformColor: transformColor,
    css: css
  };
})(window);
