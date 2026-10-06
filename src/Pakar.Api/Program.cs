using Microsoft.EntityFrameworkCore;
using Pakar.Api.Hubs;
using Pakar.Infrastructure.Persistence;
using Pakar.Application.DTOs;
using Pakar.Domain.Entities;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Pakar.Api.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
    if (cs.Contains(".db", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(cs);
    else
        options.UseSqlServer(cs);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR Service
builder.Services.AddSignalR();

builder.Services.AddSingleton<ParkingStateStore>();
builder.Services.AddHostedService<CameraWatchdog>();

builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                 ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(app);
}

// Middleware Configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Pakar API V1");
        c.RoutePrefix = string.Empty; 
    });
}

// CORS 
app.UseCors("frontend");

// --- HEALTH CHECK ---
app.MapGet("/test-db", async (AppDbContext db) =>
{
    return new HealthCheckDto 
    { 
        Message = "DB Connected!", 
        TotalZones = await db.Zones.CountAsync() 
    };
}).WithTags("System");

// --- FR 1: GET ALL ZONES (Public/User) ---
app.MapGet("/api/parking/zones", async (AppDbContext db) =>
{
    var zones = await db.Zones
        .Include(z => z.Spots)
        .Select(z => new 
        {
            z.Id,
            z.Name,
            z.TotalCapacity,
            AvailableSpots = z.Spots.Count(s => !s.IsOccupied), 
            Spots = z.Spots.Select(s => new 
            {
                s.Id,
                s.Name,
                s.Latitude,
                s.Longitude,
                s.IsOccupied
            }).ToList()
        })
        .ToListAsync();

    return Results.Ok(zones);
}).WithTags("Parking Info");

// --- FR 13: PARKING ZONES MANAGEMENT (Admin Only) ---

// POST: Add new zone
app.MapPost("/api/zones", async (AppDbContext db, ZoneDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name) || dto.TotalCapacity <= 0)
        return Results.BadRequest("Name and valid Capacity are required.");

    var zone = new ParkingZone { Name = dto.Name, TotalCapacity = dto.TotalCapacity };
    db.Zones.Add(zone);
    await db.SaveChangesAsync();
    
    return Results.Created($"/api/zones/{zone.Id}", new { zone.Id, zone.Name });
}).WithTags("Admin - Zones");

// DELETE: Remove a zone
app.MapDelete("/api/zones/{id}", async (Guid id, AppDbContext db) =>
{
    var zone = await db.Zones.FindAsync(id);
    if (zone is null) return Results.NotFound();

    db.Zones.Remove(zone);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Admin - Zones");

// --- FR 14: CAMERA MANAGEMENT (Admin Only) ---

// POST: Add camera to a zone
app.MapPost("/api/cameras", async (AppDbContext db, CameraDto dto) =>
{
    // Validasi Format IP Address
    var ipRegex = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
    if (!Regex.IsMatch(dto.IpAddress, ipRegex))
        return Results.BadRequest("Invalid IP Address format.");

    // Validasi Zone Existence
    if (!await db.Zones.AnyAsync(z => z.Id == dto.ZoneId))
        return Results.BadRequest("Target Zone does not exist.");

    var camera = new Camera 
    { 
        Name = dto.Name, 
        ConnectionType = "IP", 
        ApiKeyHash = "temp-hash", 
        ZoneId = dto.ZoneId 
    };
    
    db.Cameras.Add(camera);
    await db.SaveChangesAsync();
    return Results.Created($"/api/cameras/{camera.Id}", camera.Id);
}).WithTags("Admin - Cameras");

// DELETE: Remove camera
app.MapDelete("/api/cameras/{id}", async (Guid id, AppDbContext db) =>
{
    var camera = await db.Cameras.FindAsync(id);
    if (camera is null) return Results.NotFound();

    db.Cameras.Remove(camera);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Admin - Cameras");

// --- REAL-TIME EVENT BROADCASTING ---

// Endpoint ini mensimulasikan AI yang mendeteksi perubahan status slot
// (ParkingHub di sini = Hub LAWAS dari namespace Pakar.Api.Hubs; Hub BARU ada di Pakar.Api.Realtime)
app.MapPost("/api/inference/trigger-update", async (
    IHubContext<Pakar.Api.Hubs.ParkingHub> hubContext,
    AppDbContext db,
    Guid spotId,
    bool isOccupied) =>
{
    var spot = await db.Spots.FindAsync(spotId);
    if (spot is null) return Results.NotFound("Spot not found");

    // 1. Update Database
    spot.IsOccupied = isOccupied;
    await db.SaveChangesAsync();

    // 2. Broadcast ke semua client yang terhubung via WebSocket/SignalR
    await hubContext.Clients.All.SendAsync("ReceiveSpotUpdate", new
    {
        SpotId = spot.Id,
        IsOccupied = spot.IsOccupied,
        UpdatedAt = DateTime.UtcNow
    });

    return Results.Ok(new { message = "Status updated and broadcasted" });
}).WithTags("AI Inference");

// Realtime Parking Endpoints (SignalR Hub + Snapshot + Ingest Detection)
// PENTING: GANTI endpoint ParkingHub LAMA dengan MapParkingRealtime agar tidak bentrok route
app.MapParkingRealtime();

// Hub lain (tetap pertahankan jika dibutuhkan)
app.MapHub<OccupancyHub>("/occupancyHub");

// Test SignalR Tester (serve dari origin yang sama = localhost:5000, jadi tidak butuh CORS)
app.MapGet("/test-signalr", async (HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.WriteAsync("""
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
<h1>🅿️ PAKAR Realtime SignalR Tester (Step 10 — Same-Origin No CORS)</h1>
<p>State: <span id="state" class="tag disconnected">disconnected</span></p>

<div class="summary">
  <div class="card"><h4>Total Spots</h4><div class="big" id="cntSpots">0</div></div>
  <div class="card"><h4>Occupied</h4><div class="big" id="cntOcc" style="color:#f66">0</div></div>
  <div class="card"><h4>Available</h4><div class="big" id="cntAvail" style="color:#0f0">0</div></div>
  <div class="card"><h4>Cameras Active</h4><div class="big" id="cntCam">0</div></div>
</div>

<h3>🛑 Spot Status Changed Events (realtime):</h3><div id="spots"></div>
<h3>🟢 Zone Updated Events:</h3><pre id="zones"></pre>
<h3>📷 Camera Status:</h3><pre id="cams"></pre>
<h3>📸 Snapshot Awal (GetSnapshot):</h3><pre id="snap"></pre>
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
  div.innerHTML="<b>["+new Date().toLocaleTimeString()+"]</b> "+s.spotNumber+" (id="+s.spotId+") → <b>"+s.status+"</b>  (conf="+(s.confidence*100).toFixed(0)+"%)  areaId="+s.areaId;
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
""");
});

app.Run();
