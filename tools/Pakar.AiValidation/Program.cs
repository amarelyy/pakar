using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pakar.AiInference.Services;
using Pakar.Shared.Dtos;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
if (args.Length == 4 && args[0] == "--evaluate-roi") return RoiEvaluation.Run(args[1],args[2],args[3]);
if (args.Length == 2 && args[0] == "--slots") return SlotParity.Run(args[1]);
if (args.Length != 2) throw new ArgumentException("Usage: <onnx> <parity-frame-folder>");
using var engine = new OnnxInferenceEngine(Options.Create(new ModelOptions { Path = args[0] }));
var records = JsonSerializer.Deserialize<List<Frame>>(File.ReadAllText(Path.Combine(args[1], "python.json")), json)!;
var output = new List<object>();
var elapsed = new List<double>();
int unmatched = 0;
double maxPixelError = 0, maxConfidenceError = 0;
foreach (var frame in records)
{
    var bytes = File.ReadAllBytes(Path.Combine(args[1], frame.File));
    var sw = Stopwatch.StartNew();
    var detected = await engine.DetectAsync(bytes, default);
    sw.Stop();
    elapsed.Add(sw.Elapsed.TotalMilliseconds);
    var remaining = detected.ToList();
    foreach (var expected in frame.Boxes)
    {
        var match = remaining.Where(b => b.Label == expected.Label)
            .OrderByDescending(b => OnnxInferenceEngine.IoU(b, expected)).FirstOrDefault();
        if (match is null || OnnxInferenceEngine.IoU(match, expected) < .98f) { unmatched++; continue; }
        remaining.Remove(match);
        double error = new[] { Math.Abs(match.X - expected.X) * frame.Width,
            Math.Abs(match.Y - expected.Y) * frame.Height,
            Math.Abs(match.X + match.Width - expected.X - expected.Width) * frame.Width,
            Math.Abs(match.Y + match.Height - expected.Y - expected.Height) * frame.Height }.Max();
        maxPixelError = Math.Max(maxPixelError, error);
        maxConfidenceError = Math.Max(maxConfidenceError, Math.Abs(match.Confidence - expected.Confidence));
    }
    unmatched += remaining.Count;
    output.Add(new { frame.File, frame.Seconds, boxes = detected });
}
var summary = new { frames = records.Count, unmatched, maxPixelError, maxConfidenceError,
    meanMilliseconds = elapsed.Average(), p95Milliseconds = elapsed.Order().ElementAt((int)(elapsed.Count * .95)),
    tolerance = new { pixels = 1, confidence = .001 },
    passed = unmatched == 0 && maxPixelError <= 1 && maxConfidenceError <= .001 };
File.WriteAllText(Path.Combine(args[1], "dotnet.json"), JsonSerializer.Serialize(output, json));
File.WriteAllText(Path.Combine(args[1], "parity-summary.json"), JsonSerializer.Serialize(summary, json));
Console.WriteLine(JsonSerializer.Serialize(summary, json));
return summary.passed ? 0 : 1;

record Frame(string File, double Seconds, int Width, int Height, List<BoundingBoxDto> Boxes);
