using Microsoft.EntityFrameworkCore;
using Pakar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Tambahkan Service DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOpenApi();

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

app.Run();