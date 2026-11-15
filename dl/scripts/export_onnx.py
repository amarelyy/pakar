"""Export the trusted custom slot detector; Python is an offline build tool only.

Usage: python dl/scripts/export_onnx.py --weights /path/best.pt --output /path/best.onnx
"""
import argparse
import hashlib
import importlib.metadata
import json
import shutil
from pathlib import Path


def sha256(path):
    with Path(path).open('rb') as file:
        return hashlib.file_digest(file, 'sha256').hexdigest()


def main():
    from ultralytics import YOLO
    import onnx
    import onnxruntime as ort

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--weights', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    model = YOLO(str(args.weights))
    if model.task != 'detect' or list(model.names.values()) != ['empty', 'occupied']:
        raise ValueError('Expected detect model with labels 0=empty, 1=occupied')
    params = dict(format='onnx', imgsz=640, batch=1, dynamic=False,
                  half=False, simplify=False, opset=17, nms=False, device='cpu')
    exported = Path(model.export(**params))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    if exported.resolve() != args.output.resolve():
        shutil.copyfile(exported, args.output)
    graph = onnx.load(str(args.output))
    onnx.checker.check_model(graph)
    session = ort.InferenceSession(str(args.output), providers=['CPUExecutionProvider'])
    manifest = {
        'model_sha256': sha256(args.output), 'source_sha256': sha256(args.weights),
        'labels': model.names, 'export': params,
        'versions': {p: importlib.metadata.version(p) for p in
                     ('ultralytics', 'torch', 'onnx', 'onnxruntime')},
        'inputs': [{'name': v.name, 'shape': v.shape, 'type': v.type} for v in session.get_inputs()],
        'outputs': [{'name': v.name, 'shape': v.shape, 'type': v.type} for v in session.get_outputs()],
        'architecture': str(model.model.yaml),
    }
    args.output.with_suffix('.manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
