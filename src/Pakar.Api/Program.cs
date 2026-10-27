using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Pakar.Api.Data;
using Pakar.Api.Endpoints;
using Pakar.Api.Hubs;
using Pakar.Api.Middlewares;
using Pakar.Api.Realtime;
using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Persistence. A connection string pointing at a ".db" file selects SQLite (local dev);
// anything else selects SQL Server.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set it via user-secrets or environment variables.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString);
});

// Secrets are validated at startup so a misconfigured deployment fails fast instead of serving requests.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= JwtOptions.MinKeyBytes,
        $"Jwt:Key must be at least {JwtOptions.MinKeyBytes} bytes.")
    .ValidateOnStart();

builder.Services.AddOptions<IngestOptions>()
    .Bind(builder.Configuration.GetSection(IngestOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Authentication: bearer JWTs issued by AuthController.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
        bearer.TokenValidationParameters = jwt.Value.CreateValidationParameters());

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the token returned by POST /api/auth/login."
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<ParkingStateStore>();
builder.Services.AddHostedService<CameraWatchdog>();

builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                 ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

await DbSeeder.SeedAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Pakar API V1");
        c.RoutePrefix = string.Empty;
    });
}
else
{
    // Return RFC 7807 problem details instead of leaking exception details.
    app.UseExceptionHandler();
}

app.UseCors("frontend");

// Edge devices authenticate with an API key rather than a user JWT.
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api/ingest"),
    branch => branch.UseMiddleware<CameraAuthMiddleware>());

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/test-db", async (AppDbContext db) => new HealthCheckDto
{
    Message = "DB Connected!",
    TotalZones = await db.Zones.CountAsync()
}).WithTags("System");

app.MapAdminEndpoints();

// Realtime: /hubs/parking, snapshot, and the API-key protected ingest endpoint.
app.MapParkingRealtime();

// Legacy occupancy hub, still consumed by Pakar.Web (Park.razor).
app.MapHub<OccupancyHub>("/occupancyHub");

if (app.Environment.IsDevelopment())
    app.MapDevEndpoints();

app.Run();
