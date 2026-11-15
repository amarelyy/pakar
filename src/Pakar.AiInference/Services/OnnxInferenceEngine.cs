using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Pakar.Shared.Dtos;

namespace Pakar.AiInference.Services;

public sealed class ModelOptions
{
    public string Path { get; set; } = "models/best.onnx";
    public float Confidence { get; set; } = .10f;
    public float NmsIou { get; set; } = .70f;
}

/// <summary>Fixed FP32 YOLOv8 slot model, [1,3,640,640] -> [1,6,8400]. CPU provider.</summary>
public sealed class OnnxInferenceEngine : IInferenceEngine, IDisposable
{
    private readonly InferenceSession session;
    private readonly ModelOptions options;
    private readonly string inputName;
    private static readonly string[] Labels = ["empty", "occupied"];

    public OnnxInferenceEngine(IOptions<ModelOptions> configured)
    {
        options = configured.Value;
        if (!File.Exists(options.Path)) throw new ModelNotLoadedException();
        if (!float.IsFinite(options.Confidence) || options.Confidence is < 0 or > 1 ||
            !float.IsFinite(options.NmsIou) || options.NmsIou is < 0 or > 1)
            throw new ArgumentException("Model thresholds must be in 0..1.");
        using var settings = new Microsoft.ML.OnnxRuntime.SessionOptions { IntraOpNumThreads = 4 };
        try { session = new InferenceSession(options.Path, settings); }
        catch (OnnxRuntimeException) { throw new ModelNotLoadedException("The ONNX slot model could not be loaded."); }
        try
        {
            var input = session.InputMetadata.Single();
            var output = session.OutputMetadata.Single();
            if (input.Value.ElementType != typeof(float) || output.Value.ElementType != typeof(float) ||
                !input.Value.Dimensions.SequenceEqual(new[] { 1, 3, 640, 640 }) ||
                !output.Value.Dimensions.SequenceEqual(new[] { 1, 6, 8400 }))
                throw new ModelNotLoadedException("Unsupported model shape/type. Export the documented slot model.");
            var metadata = session.ModelMetadata.CustomMetadataMap["names"].Replace('\'', '"');
            metadata = Regex.Replace(metadata, @"(?<=[{,])\s*(\d+)\s*:", "\"$1\":");
            var names = JsonSerializer.Deserialize<Dictionary<string, string>>(metadata)!;
            if (names.Count != 2 || names["0"] != Labels[0] || names["1"] != Labels[1])
                throw new ModelNotLoadedException("Model labels must be 0=empty, 1=occupied.");
            inputName = input.Key;
        }
        catch (Exception ex)
        {
            session.Dispose();
            if (ex is ModelNotLoadedException) throw;
            throw new ModelNotLoadedException("The model metadata is incompatible with the slot detector.");
        }
    }

    public Task<IReadOnlyList<BoundingBoxDto>> DetectAsync(byte[] jpeg, CancellationToken ct)
    {
        Mat image;
        try { image = Cv2.ImDecode(jpeg, ImreadModes.Color); }
        catch (OpenCVException) { throw new ArgumentException("Invalid image frame."); }
        using var frame = image;
        if (image.Empty()) throw new ArgumentException("Invalid image frame.");
        return Task.FromResult(Detect(image, ct));
    }

    public IReadOnlyList<BoundingBoxDto> Detect(Mat image, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (image.Empty() || image.Type() != MatType.CV_8UC3) throw new ArgumentException("Expected BGR8 frame.");
        double scale = Math.Min(640d / image.Width, 640d / image.Height);
        int w = (int)Math.Round(image.Width * scale), h = (int)Math.Round(image.Height * scale);
        int left = (int)Math.Round((640 - w) / 2d - .1), top = (int)Math.Round((640 - h) / 2d - .1);
        using var resized = new Mat();
        using var padded = new Mat();
        Cv2.Resize(image, resized, new Size(w, h), interpolation: InterpolationFlags.Linear);
        Cv2.CopyMakeBorder(resized, padded, top, 640 - h - top, left, 640 - w - left,
            BorderTypes.Constant, new Scalar(114, 114, 114));
        var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
        for (int y = 0; y < 640; y++)
            for (int x = 0; x < 640; x++)
            {
                var bgr = padded.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = bgr.Item2 / 255f;
                tensor[0, 1, y, x] = bgr.Item1 / 255f;
                tensor[0, 2, y, x] = bgr.Item0 / 255f;
            }
        using var runOptions = new RunOptions();
        using var registration = ct.Register(() => runOptions.Terminate = true);
        using var result = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) },
            session.OutputNames, runOptions);
        ct.ThrowIfCancellationRequested();
        var output = result.First().AsTensor<float>();
        var boxes = new List<BoundingBoxDto>();
        for (int i = 0; i < 8400; i++)
        {
            int label = output[0, 4, i] >= output[0, 5, i] ? 0 : 1;
            float conf = output[0, 4 + label, i];
            if (!float.IsFinite(conf) || conf < options.Confidence) continue;
            double cx = output[0, 0, i], cy = output[0, 1, i];
            double bw = output[0, 2, i], bh = output[0, 3, i];
            if (!double.IsFinite(cx + cy + bw + bh) || bw <= 0 || bh <= 0) continue;
            float x1 = (float)((cx - bw / 2 - left) / scale / image.Width);
            float y1 = (float)((cy - bh / 2 - top) / scale / image.Height);
            float x2 = (float)((cx + bw / 2 - left) / scale / image.Width);
            float y2 = (float)((cy + bh / 2 - top) / scale / image.Height);
            if (x2 > x1 && y2 > y1) boxes.Add(new(x1, y1, x2 - x1, y2 - y1, conf, Labels[label]));
        }
        // NMS precedes clipping, as in Ultralytics; clipping first changes edge-box IoU.
        return Suppress(boxes, options.NmsIou).Select(b =>
        {
            float x1 = Math.Clamp(b.X, 0, 1), y1 = Math.Clamp(b.Y, 0, 1);
            float x2 = Math.Clamp(b.X + b.Width, 0, 1), y2 = Math.Clamp(b.Y + b.Height, 0, 1);
            return b with { X = x1, Y = y1, Width = x2 - x1, Height = y2 - y1 };
        }).Where(b => b.Width > 0 && b.Height > 0).ToList();
    }

    public static IReadOnlyList<BoundingBoxDto> Suppress(IEnumerable<BoundingBoxDto> boxes, float threshold)
    {
        var kept = new List<BoundingBoxDto>();
        foreach (var box in boxes.OrderByDescending(b => b.Confidence))
        {
            if (kept.Any(b => b.Label == box.Label && IoU(b, box) > threshold)) continue;
            kept.Add(box);
            if (kept.Count == 300) break;
        }
        return kept;
    }

    public static float IoU(BoundingBoxDto a, BoundingBoxDto b)
    {
        float intersection = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X)) *
                             Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        float union = a.Width * a.Height + b.Width * b.Height - intersection;
        return union > 0 ? intersection / union : 0;
    }

    public void Dispose() => session.Dispose();
}
