# Slot model export and runtime

The application runs only .NET. Python is an offline training/export/validation tool.
Use trusted weights: loading a PT checkpoint executes the PyTorch deserializer.

## Obtain the model

The model and video are local user-supplied assets, not public downloads. There
is no verified public distribution URL. Ask the project owner for `best.pt` and
`parking-demo.mp4`, verify the hashes in `dotnet-pipeline-progress.md`, then export:

```powershell
python -m pip install ultralytics==8.4.173 onnx==1.23.2 onnxruntime==1.29.0
python dl/scripts/export_onnx.py --weights C:/assets/best.pt --output artifacts/models/best.onnx
```

`docs/ai/onnx-manifest.json` records the validated export, labels, architecture,
shapes, versions and hashes. ONNX protobuf metadata may change its file hash on
re-export; retain the resulting manifest with each distributed binary.
This is YOLOv8n architecture, two-class slot detection; do not substitute a COCO
vehicle detector or an end-to-end YOLO26 model.

Set `Model:Path` (`Model__Path` in the environment) to the exported ONNX. Mount
the containing folder into the container read-only. Do not commit binaries.
ONNX Runtime CPU and OpenCvSharp implement fixed 640 square letterbox with 114
padding, bilinear resize, BGR to RGB, NCHW FP32 /255, class-aware NMS .70 and
confidence .10. Box coordinates returned by the existing HTTP DTO are 0..1
relative to the original frame (letterbox padding removed).

## Reproduce box parity

```powershell
python dl/scripts/prepare_parity.py --weights C:/assets/best.pt --video C:/assets/parking-demo.mp4 --output artifacts/parity
dotnet run --project tools/Pakar.AiValidation -c Release -- artifacts/models/best.onnx artifacts/parity
```

The freshly generated PyTorch reference uses identical lossless frames and
`rect=False`, so both runtimes receive 640 square inputs. Historical Python
results used auto rectangular preprocessing; they are a separate measurement.
Matching requires same class and IoU >= .98, maximum 1 pixel coordinate error
and .001 confidence error, with no unmatched boxes. Runtime includes image
decode, preprocessing, inference, decoding and NMS; first-run warmup is included.
