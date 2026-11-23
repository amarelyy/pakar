using System.Text.Json;
using Pakar.AiInference.Detection;
using Pakar.Shared.Dtos;

public static class SlotParity
{
    public static int Run(string folder)
    {
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        var source=JsonSerializer.Deserialize<WorkerSourceDto>(File.ReadAllText(Path.Combine(folder,"source.json")),json)!;
        var pythonFrames=JsonSerializer.Deserialize<List<Frame>>(File.ReadAllText(Path.Combine(folder,"python.json")),json)!;
        using var expected=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"python-slots.json")));
        using var actual=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"dotnet.json")));
        using var mapper=new RoiMapper(source.Slots);
        var tracker=new OccupancyTracker(source.Slots);
        int differences=0, checkedSlots=0; var results=new List<object>();
        for(int i=0;i<pythonFrames.Count;i++)
        {
            var frame=pythonFrames[i];
            var boxes=actual.RootElement[i].GetProperty("boxes").Deserialize<List<BoundingBoxDto>>(json)!;
            var mapped=mapper.Map(boxes,frame.Width,frame.Height);
            var result=tracker.Update(mapped,frame.Seconds,source.ZoneCapacity);
            var reference=expected.RootElement[i];
            foreach(var slot in source.Slots)
            {
                checkedSlots++;
                string? want=reference.GetProperty("states").TryGetProperty(slot.SpotNumber,out var value)?value.GetString():null;
                string? got=result.Visible.FirstOrDefault(s=>s.SpotId==slot.SpotId)?.Status;
                string? reason=reference.GetProperty("unknown").TryGetProperty(slot.SpotNumber,out var why)?why.GetString():null;
                if(want!=got || reason!=result.Unknown.GetValueOrDefault(slot.SpotId)) differences++;
                string? raw=reference.GetProperty("raw_states").TryGetProperty(slot.SpotNumber,out var rawValue)?rawValue.GetString():null;
                if(raw!=mapped[slot.SpotId].Status) differences++;
            }
            var expectedStates=reference.GetProperty("states").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString());
            int pending=expectedStates.Count(p=>reference.GetProperty("raw_states").GetProperty(p.Key).GetString()!=p.Value);
            if(result.Occupied!=expectedStates.Count(p=>p.Value=="occupied") ||
                result.Available!=expectedStates.Count(p=>p.Value=="available") ||
                result.Unknown.Count!=reference.GetProperty("unknown").EnumerateObject().Count() || result.Pending!=pending) differences++;
            var expectedChanged=reference.GetProperty("changes").EnumerateArray().Select(c=>c.GetProperty("spot").GetString()).Order().ToArray();
            if(!expectedChanged.SequenceEqual(result.Changes.Select(c=>(string?)c.SpotNumber).Order())) differences++;
            results.Add(new {frame.Seconds,result});
        }
        var summary=new {frames=pythonFrames.Count,checkedSlots,checkedRawStates=checkedSlots,checkedZoneCounts=pythonFrames.Count,differences,passed=differences==0};
        File.WriteAllText(Path.Combine(folder,"slot-parity-summary.json"),JsonSerializer.Serialize(summary,json));
        File.WriteAllText(Path.Combine(folder,"dotnet-slots.json"),JsonSerializer.Serialize(results,json));
        Console.WriteLine(JsonSerializer.Serialize(summary,json));
        return differences==0?0:1;
    }
}
