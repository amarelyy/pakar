using Microsoft.EntityFrameworkCore;
using Pakar.Api.Hubs;
using Pakar.Api.Services;
using Pakar.Infrastructure.Persistence;
using Pakar.Application.DTOs;
using Pakar.Domain.Entities;
using System.Text.RegularExpressions;
using Pakar.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// 1. Services Configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Tambahkan SignalR Service
builder.Services.AddSignalR();

// --- added: SignalR + fake occupancy broadcaster ---
builder.Services.AddSignalR();
builder.Services.AddHostedService<FakeOccupancyBroadcaster>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await Pakar.Api.Data.DbSeeder.SeedAsync(app);
}

// 2. Middleware Configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Pakar API V1");
        c.RoutePrefix = string.Empty; 
    });
}

// --- HEALTH CHECK ---
app.MapGet("/test-db", async (AppDbContext db) =>
{
    return new HealthCheckDto 
    { 
        Message = "DB Connected!", 
        TotalZones = await db.Zones.CountAsync() 
    };
}).WithTags("System");

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

// --- REAL-TIME EVENT BROADCASTING (AI Simulation) ---

// Endpoint ini mensimulasikan AI yang mendeteksi perubahan status slot
app.MapPost("/api/inference/trigger-update", async (
    IHubContext<ParkingHub> hubContext, 
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

// Registrasi SignalR Hub Endpoint
app.MapHub<ParkingHub>("/hubs/parking");

// --- added: occupancy hub endpoint ---
app.MapHub<OccupancyHub>("/occupancyHub");

app.Run();
