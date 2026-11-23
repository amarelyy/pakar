// Run with: node tests/js/ai-feeder.test.mjs  (pure functions only, no browser or model needed)
import assert from "node:assert/strict";
import { ANCHORS, decode, letterbox, suppress, toTensorData } from "../../src/Pakar.Web/wwwroot/js/ai-feeder.js";

// Letterbox of a 1920x1080 frame into 640x640: scaled to 640x360, centred vertically.
const lb = letterbox(1920, 1080);
assert.equal(lb.w, 640); assert.equal(lb.h, 360); assert.equal(lb.left, 0);
assert.equal(lb.top, Math.round((640 - 360) / 2 - 0.1));

// A synthetic output with one occupied box centred in the frame and one weak box below the threshold.
const out = new Float32Array(6 * ANCHORS);
const set = (i, cx, cy, w, h, empty, occupied) => {
    out[i] = cx; out[ANCHORS + i] = cy; out[2 * ANCHORS + i] = w; out[3 * ANCHORS + i] = h;
    out[4 * ANCHORS + i] = empty; out[5 * ANCHORS + i] = occupied;
};
set(0, 320, 320, 64, 36, 0.05, 0.90);       // occupied, 64x36 px in letterbox space
set(1, 322, 320, 64, 36, 0.05, 0.80);       // same label, overlaps heavily: removed by NMS
set(2, 100, 320, 40, 40, 0.60, 0.10);       // empty
set(3, 500, 320, 40, 40, 0.05, 0.05);       // below 0.10: ignored
const boxes = decode(out, 1920, 1080);
assert.equal(boxes.length, 2);
const occupied = boxes.find(b => b.label === "occupied");
assert.ok(Math.abs(occupied.confidence - 0.90) < 1e-6);
assert.ok(Math.abs(occupied.x + occupied.width / 2 - 0.5) < 1e-6, "box centre x");
assert.ok(Math.abs(occupied.y + occupied.height / 2 - 0.5) < 1e-6, "box centre y");
assert.ok(Math.abs(occupied.width - 64 / 640) < 1e-6, "box width in frame units");
assert.ok(Math.abs(occupied.height - 36 / 360) < 1e-6, "box height in frame units");
assert.equal(boxes.find(b => b.label === "empty").confidence.toFixed(2), "0.60");

// Different labels never suppress each other.
const same = suppress([
    { x: 0, y: 0, width: .2, height: .2, confidence: .9, label: "empty" },
    { x: 0, y: 0, width: .2, height: .2, confidence: .8, label: "occupied" }], .7);
assert.equal(same.length, 2);

// Tensor layout is planar RGB in 0..1.
const rgba = new Uint8ClampedArray(640 * 640 * 4);
rgba[0] = 255; rgba[1] = 128; rgba[2] = 0; rgba[3] = 255;
const t = toTensorData(rgba);
assert.equal(t[0], 1); assert.ok(Math.abs(t[640 * 640] - 128 / 255) < 1e-6); assert.equal(t[2 * 640 * 640], 0);

console.log("ai-feeder.js: all checks passed");
