using System.Text.Json;
using Microsoft.Extensions.Options;
using Pakar.AiInference.Detection;
using Pakar.AiInference.Services;
using Pakar.AiInference.Video;
using Pakar.Shared.Dtos;

internal static class RoiEvaluation
{
    private sealed record Interval(double Start, double End, string Status);
    private sealed record Slot(int Id,string Name,double[][] Polygon,List<Interval> Labels);
    private sealed record Case(string Id,string Source,string Scope,double SampleFps,string Transform,List<Slot> Slots);
    private sealed record Specification(List<Case> Cases);
    private sealed class Metrics
    {
        public int Labelled,Matched,Wrong,Unknown,FalseAvailable,FalseOccupied,ExpectedOccupied,ExpectedAvailable;
        public void Add(string expected,string? actual)
        {
            Labelled++;if(expected=="occupied")ExpectedOccupied++;else ExpectedAvailable++;
            if(actual is null)Unknown++;else if(actual==expected)Matched++;else Wrong++;
            if(expected=="occupied" && actual=="available")FalseAvailable++;
            if(expected=="available" && actual=="occupied")FalseOccupied++;
        }
        public object Summary()=>new {Labelled,Matched,Wrong,Unknown,FalseAvailable,FalseOccupied,ExpectedOccupied,ExpectedAvailable,
            coverage=Labelled==0?0:(Labelled-Unknown)/(double)Labelled,
            correctFraction=Labelled==0?0:Matched/(double)Labelled,
            accuracyAmongDecided=Labelled==Unknown?(double?)null:Matched/(double)(Labelled-Unknown)};
    }
    public static int Run(string model,string specification,string output)
    {
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        var spec=JsonSerializer.Deserialize<Specification>(File.ReadAllText(specification),json)!;
        Directory.CreateDirectory(output);
        using var engine=new OnnxInferenceEngine(Options.Create(new ModelOptions{Path=model}));
        var summaries=new List<object>();
        foreach(var c in spec.Cases)
        {
            if(c.SampleFps<=0 || c.SampleFps>30 || c.Transform is not ("original" or "dark"))throw new ArgumentException("Invalid sampling or transform.");
            foreach(var slot in c.Slots)
            {
                var intervals=slot.Labels.OrderBy(i=>i.Start).ToArray();
                for(int i=0;i<intervals.Length;i++)
                    if(intervals[i].Start<0 || intervals[i].End<=intervals[i].Start || intervals[i].Status is not ("occupied" or "available" or "ignore") || (i>0 && intervals[i].Start<intervals[i-1].End))throw new ArgumentException("Invalid ground truth intervals.");
            }
            var slots=c.Slots.Select(s=>new WorkerSlotDto(s.Id,s.Name,s.Polygon)).ToList();
            using var mapper=new RoiMapper(slots);var tracker=new OccupancyTracker(slots);
            using var capture=new OpenCvSharp.VideoCapture(c.Source);
            if(!capture.IsOpened())throw new IOException("Cannot open evaluation source: "+c.Id);
            var duration=capture.FrameCount/capture.Fps;
            var rawMetric=new Metrics();var publishedMetric=new Metrics();int frames=0,rejected=0,fullyLabelled=0,countBoundsMiss=0;
            using var rows=new StreamWriter(Path.Combine(output,c.Id+".jsonl"));
            for(double t=0;t<duration;t+=1/c.SampleFps)
            {
                var target=(long)Math.Floor(t*capture.Fps);
                while(capture.PosFrames<target)if(!capture.Grab())throw new IOException("Cannot advance source: "+c.Id);
                using var frame=new OpenCvSharp.Mat();if(!capture.Read(frame)||frame.Empty())throw new IOException("Decode failure in "+c.Id);
                if(c.Transform=="dark")frame.ConvertTo(frame,-1,.08);
                var quality=FrameQuality.RejectionReason(frame);
                var boxes=quality is null ? engine.Detect(frame) : [];
                var observations=quality is null ? mapper.Map(boxes,frame.Width,frame.Height) : slots.ToDictionary(s=>s.SpotId,_=>new Observation(null,0,quality));
                var stable=tracker.Update(observations,t,slots.Count);
                if(quality is not null)rejected++;
                var records=c.Slots.Select(s=>
                {
                    var expected=s.Labels.FirstOrDefault(i=>t>=i.Start && t<i.End)?.Status;
                    var raw=observations[s.Id];var decided=stable.Visible.FirstOrDefault(x=>x.SpotId==s.Id);
                    var published=raw.Status is not null && decided?.Status==raw.Status?raw.Status:null;
                    if(expected is "occupied" or "available"){rawMetric.Add(expected,raw.Status);publishedMetric.Add(expected,published);}
                    return new {id=s.Id,name=s.Name,expected,raw=raw.Status,published,raw.Confidence,reason=quality??(raw.Status!=decided?.Status?"pending_debounce":raw.Reason)};
                }).ToList();
                if(records.All(r=>r.expected is "occupied" or "available"))
                {
                    fullyLabelled++;int actual=records.Count(r=>r.expected=="occupied"),min=records.Count(r=>r.published=="occupied"),max=min+records.Count(r=>r.published is null);
                    if(actual<min || actual>max)countBoundsMiss++;
                }
                // One compact record per frame; do not serialize images or model weights.
                rows.WriteLine(JsonSerializer.Serialize(new {seconds=t,quality,slots=records},new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                frames++;if(frames%100==0)Console.WriteLine($"{c.Id}: {frames} frames");
            }
            var summary=new {c.Id,c.Scope,c.Transform,frames,duration,rejected,fullyLabelled,countBoundsMiss,
                raw=rawMetric.Summary(),published=publishedMetric.Summary(),
                demoQualityCriterion=publishedMetric.FalseAvailable==0 && publishedMetric.Labelled>0 && publishedMetric.Unknown<=.05*publishedMetric.Labelled};
            summaries.Add(summary);Console.WriteLine(JsonSerializer.Serialize(summary,json));
            File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(summaries,json));
        }
        return 0; // Successful measurement does not imply model acceptance; inspect each quality criterion.
    }
}
