using Microsoft.EntityFrameworkCore;
using Pakar.Api.Hubs;
using Pakar.Api.Services;
using Pakar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Tambahkan Service DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOpenApi();

// --- added: SignalR + fake occupancy broadcaster ---
builder.Services.AddSignalR();
builder.Services.AddHostedService<FakeOccupancyBroadcaster>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection(); 

// 2. Endpoint Test Database
app.MapGet("/test-db", async (AppDbContext db) =>
{
    var zones = await db.Zones.ToListAsync();
    return new { 
        message = "DB Connected!", 
        totalZones = zones.Count 
    };
});

// --- added: occupancy hub endpoint ---
app.MapHub<OccupancyHub>("/occupancyHub");

app.Run();
