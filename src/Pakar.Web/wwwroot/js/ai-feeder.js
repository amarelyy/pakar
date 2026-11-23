// Browser-side slot detection with ONNX Runtime Web (the .NET ONNX package cannot run in a browser).
// This module only runs the model and decodes its output. Slot mapping, debouncing and the API calls
// stay in C# (Pakar.Vision and the Feeder page). The canvas below is used to read video pixels, not to draw.
//
// Model contract (same as Pakar.AiInference.OnnxInferenceEngine):
//   input  [1,3,640,640] float32, RGB 0..1, letterboxed with grey 114
//   output [1,6,8400]    float32, rows: cx, cy, w, h, score(empty), score(occupied)

export const INPUT_SIZE = 640;
export const ANCHORS = 8400;
export const LABELS = ["empty", "occupied"];

/** Letterbox geometry used by Ultralytics: scale to fit, centre, pad with grey. */
export function letterbox(width, height, size = INPUT_SIZE) {
    const scale = Math.min(size / width, size / height);
    const w = Math.round(width * scale);
    const h = Math.round(height * scale);
    // "|| 0" turns -0 into 0, matching the integer maths of the .NET engine.
    return { scale, w, h, left: Math.round((size - w) / 2 - 0.1) || 0, top: Math.round((size - h) / 2 - 0.1) || 0 };
}

/** RGBA pixels (size x size) to a planar RGB float tensor in 0..1. */
export function toTensorData(rgba, size = INPUT_SIZE) {
    const plane = size * size;
    const data = new Float32Array(3 * plane);
    for (let i = 0; i < plane; i++) {
        data[i] = rgba[i * 4] / 255;
        data[plane + i] = rgba[i * 4 + 1] / 255;
        data[2 * plane + i] = rgba[i * 4 + 2] / 255;
    }
    return data;
}

function iou(a, b) {
    const w = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
    const h = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
    const inter = w * h;
    const union = a.width * a.height + b.width * b.height - inter;
    return union > 0 ? inter / union : 0;
}

/** Same-label non-maximum suppression, highest confidence first, at most 300 boxes. */
export function suppress(boxes, threshold) {
    const kept = [];
    for (const box of [...boxes].sort((a, b) => b.confidence - a.confidence)) {
        if (kept.some(k => k.label === box.label && iou(k, box) > threshold)) continue;
        kept.push(box);
        if (kept.length === 300) break;
    }
    return kept;
}

/** Turns the raw model output into boxes normalized to the original frame (0..1). */
export function decode(output, frameWidth, frameHeight, confidence = 0.10, nmsIou = 0.70) {
    const lb = letterbox(frameWidth, frameHeight);
    const boxes = [];
    for (let i = 0; i < ANCHORS; i++) {
        const empty = output[4 * ANCHORS + i];
        const occupied = output[5 * ANCHORS + i];
        const label = empty >= occupied ? 0 : 1;
        const score = label === 0 ? empty : occupied;
        if (!Number.isFinite(score) || score < confidence) continue;
        const cx = output[i], cy = output[ANCHORS + i];
        const bw = output[2 * ANCHORS + i], bh = output[3 * ANCHORS + i];
        if (![cx, cy, bw, bh].every(Number.isFinite) || bw <= 0 || bh <= 0) continue;
        const x1 = (cx - bw / 2 - lb.left) / lb.scale / frameWidth;
        const y1 = (cy - bh / 2 - lb.top) / lb.scale / frameHeight;
        const x2 = (cx + bw / 2 - lb.left) / lb.scale / frameWidth;
        const y2 = (cy + bh / 2 - lb.top) / lb.scale / frameHeight;
        if (x2 > x1 && y2 > y1) boxes.push({ x: x1, y: y1, width: x2 - x1, height: y2 - y1, confidence: score, label: LABELS[label] });
    }
    // NMS runs before clipping, as in Ultralytics and the .NET engine.
    return suppress(boxes, nmsIou).map(b => {
        const x1 = Math.min(Math.max(b.x, 0), 1), y1 = Math.min(Math.max(b.y, 0), 1);
        const x2 = Math.min(Math.max(b.x + b.width, 0), 1), y2 = Math.min(Math.max(b.y + b.height, 0), 1);
        return { ...b, x: x1, y: y1, width: x2 - x1, height: y2 - y1 };
    }).filter(b => b.width > 0 && b.height > 0);
}

// ---- runtime (needs a browser) ----

let ort = null;
let session = null;
let inputName = null;
let provider = null;
let canvas = null;
let context = null;

/** Loads ONNX Runtime Web from the given base URL and opens the model. Returns the execution provider used. */
export async function load(ortBaseUrl, modelUrl) {
    if (session) return provider;
    const base = ortBaseUrl.endsWith("/") ? ortBaseUrl : ortBaseUrl + "/";
    ort = await import(base + "ort.all.min.mjs");
    ort.env.wasm.wasmPaths = base;

    const attempts = [];
    if (typeof navigator !== "undefined" && navigator.gpu) attempts.push("webgpu");
    attempts.push("wasm");

    let lastError = null;
    for (const candidate of attempts) {
        try {
            session = await ort.InferenceSession.create(modelUrl, { executionProviders: [candidate], graphOptimizationLevel: "all" });
            provider = candidate;
            break;
        } catch (error) {
            lastError = error;
        }
    }
    if (!session) throw new Error("Could not open the model: " + (lastError?.message ?? "unknown error"));

    inputName = session.inputNames[0];
    canvas = typeof OffscreenCanvas !== "undefined" ? new OffscreenCanvas(INPUT_SIZE, INPUT_SIZE) : Object.assign(document.createElement("canvas"), { width: INPUT_SIZE, height: INPUT_SIZE });
    context = canvas.getContext("2d", { willReadFrequently: true });
    return provider;
}

/** Runs the model on the current frame of a <video>. Returns null while the video has no frame yet. */
export async function detect(video, confidence = 0.10, nmsIou = 0.70) {
    if (!session) throw new Error("The model is not loaded.");
    if (video.readyState < 2 || !video.videoWidth || !video.videoHeight) return null;

    const started = performance.now();
    const width = video.videoWidth, height = video.videoHeight;
    const lb = letterbox(width, height);
    context.fillStyle = "rgb(114,114,114)";
    context.fillRect(0, 0, INPUT_SIZE, INPUT_SIZE);
    context.drawImage(video, lb.left, lb.top, lb.w, lb.h);
    const pixels = context.getImageData(0, 0, INPUT_SIZE, INPUT_SIZE).data;

    const tensor = new ort.Tensor("float32", toTensorData(pixels), [1, 3, INPUT_SIZE, INPUT_SIZE]);
    const results = await session.run({ [inputName]: tensor });
    const output = results[session.outputNames[0]];
    if (output.dims.length !== 3 || output.dims[1] !== 6 || output.dims[2] !== ANCHORS)
        throw new Error("Unsupported model output shape [" + output.dims.join(",") + "]; expected [1,6," + ANCHORS + "].");

    return {
        boxes: decode(output.data, width, height, confidence, nmsIou),
        width,
        height,
        videoTime: video.currentTime,
        milliseconds: performance.now() - started
    };
}

/** Starts playback from a user gesture; returns false when the browser blocks it. */
export async function play(video) {
    try {
        await video.play();
        return true;
    } catch {
        return false;
    }
}

export function pause(video) {
    video.pause();
}

let localVideoUrl = null;

/** Uses a video file picked on this computer. It is read by the browser only; nothing is uploaded. */
export function useLocalVideo(input) {
    const file = input.files && input.files[0];
    releaseLocalVideo();
    if (!file) return null;
    localVideoUrl = URL.createObjectURL(file);
    return { url: localVideoUrl, name: file.name, size: file.size };
}

export function releaseLocalVideo() {
    if (localVideoUrl) {
        URL.revokeObjectURL(localVideoUrl);
        localVideoUrl = null;
    }
}
