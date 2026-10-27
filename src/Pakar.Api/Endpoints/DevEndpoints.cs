using Microsoft.AspNetCore.SignalR;
using Pakar.Api.Hubs;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Endpoints;

/// <summary>
/// Developer tooling: a manual spot-status trigger and a browser SignalR tester.
/// Mapped only in the Development environment; never exposed in production.
/// </summary>
public static class DevEndpoints
{
    public static void MapDevEndpoints(this WebApplication app)
    {
        // Simulates an AI detection for one spot and pushes it to /occupancyHub clients.
        app.MapPost("/api/inference/trigger-update", async (
            IHubContext<OccupancyHub> hubContext,
            AppDbContext db,
            Guid spotId,
            bool isOccupied) =>
        {
            var spot = await db.Spots.FindAsync(spotId);
            if (spot is null) return Results.NotFound("Spot not found");

            spot.IsOccupied = isOccupied;
            await db.SaveChangesAsync();

            await hubContext.Clients.All.SendAsync("ReceiveSpotUpdate", new
            {
                SpotId = spot.Id,
                IsOccupied = spot.IsOccupied,
                UpdatedAt = DateTime.UtcNow
            });

            return Results.Ok(new { message = "Status updated and broadcasted" });
        }).WithTags("Development");

        // Same-origin page for watching /hubs/parking events without a CORS setup.
        app.MapGet("/test-signalr", () => Results.Content(SignalRTesterHtml, "text/html; charset=utf-8"))
            .ExcludeFromDescription();
    }

    private const string SignalRTesterHtml = """
<!DOCTYPE html><html><head><meta charset="UTF-8"><title>PAKAR SignalR Tester</title>
<style>body{font-family:Consolas;background:#0a0a0a;color:#0f0;padding:20px}h1{color:#0ff}
pre{background:#111;padding:10px;border:1px solid #333;height:260px;overflow:auto;color:#fff;white-space:pre-wrap}
.tag{display:inline-block;padding:2px 8px;border-radius:4px;margin:2px;font-weight:bold}
.connected{background:#080;color:#fff}.reconnecting{background:#f80}.disconnected{background:#800}
.spot{background:#030;padding:8px;margin:2px;border-left:4px solid #0f0}
.spot.occ{background:#500;border-left-color:#f00}
.summary{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;margin:10px 0}
.card{background:#111;padding:15px;border:1px solid #333;border-radius:8px}
.card h4{margin:0 0 8px;color:#0ff;font-size:14px}
.card .big{font-size:28px;font-weight:bold}
</style></head><body>
<h1>PAKAR Realtime SignalR Tester</h1>
<p>State: <span id="state" class="tag disconnected">disconnected</span></p>

<div class="summary">
  <div class="card"><h4>Total Spots</h4><div class="big" id="cntSpots">0</div></div>
  <div class="card"><h4>Occupied</h4><div class="big" id="cntOcc" style="color:#f66">0</div></div>
  <div class="card"><h4>Available</h4><div class="big" id="cntAvail" style="color:#0f0">0</div></div>
  <div class="card"><h4>Cameras Active</h4><div class="big" id="cntCam">0</div></div>
</div>

<h3>Spot Status Changed Events</h3><div id="spots"></div>
<h3>Zone Updated Events</h3><pre id="zones"></pre>
<h3>Camera Status</h3><pre id="cams"></pre>
<h3>Initial Snapshot (GetSnapshot)</h3><pre id="snap"></pre>
<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
<script>
const $=(id)=>document.getElementById(id);
const conn=new signalR.HubConnectionBuilder().withUrl("/hubs/parking")
  .withAutomaticReconnect([0,1000,3000,5000,10000]).configureLogging(signalR.LogLevel.Information).build();
function setState(s){$("state").className="tag "+s;$("state").textContent=s;}
function refreshSummary(snap){
  const occ = snap.spots.filter(s=>s.status==="occupied").length;
  $("cntSpots").textContent = snap.spots.length;
  $("cntOcc").textContent = occ;
  $("cntAvail").textContent = snap.spots.length - occ;
  $("cntCam").textContent = snap.cameras.filter(c=>c.active).length + "/" + snap.cameras.length;
}
conn.on("SpotStatusChanged",s=>{
  const div=document.createElement("div");div.className="spot "+(s.status==="occupied"?"occ":"");
  div.textContent="["+new Date().toLocaleTimeString()+"] "+s.spotNumber+" (id="+s.spotId+") -> "+s.status+"  (conf="+(s.confidence*100).toFixed(0)+"%)  areaId="+s.areaId;
  $("spots").prepend(div);
  conn.invoke("GetSnapshot").then(refreshSummary);
});
conn.on("ZoneUpdated",z=>{$("zones").textContent=JSON.stringify(z,null,2)+"\n------------------------\n"+$("zones").textContent});
conn.on("CameraStatusChanged",c=>{$("cams").textContent=JSON.stringify(c,null,2)+"\n------------------------\n"+$("cams").textContent});
conn.onreconnecting(()=>setState("reconnecting"));
conn.onreconnected(async()=>{setState("connected");await sync()});
conn.onclose(()=>setState("disconnected"));
async function sync(){
  const snap=await conn.invoke("GetSnapshot");
  $("snap").textContent=JSON.stringify(snap,null,2);
  refreshSummary(snap);
  await Promise.all(snap.zones.map(z=>conn.invoke("JoinArea",z.areaId)));
}
(async()=>{setState("reconnecting");await conn.start();setState("connected");await sync();})();
</script></body></html>
""";
}
