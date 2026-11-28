using Microsoft.EntityFrameworkCore;
using Pakar.Api.Security;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Development migrates automatically. Production applies schema changes as a deployment step
        // (migration bundle) unless Database:ApplyMigrationsOnStartup is set to true.
        var applyMigrations = app.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup")
                              ?? app.Environment.IsDevelopment();
        if (applyMigrations)
            await context.Database.MigrateAsync();

        await ParkingSeed.ApplyAsync(context, app.Configuration, app.Logger);

        await SeedAdminAsync(context, app.Configuration, app.Logger);
    }

    /// <summary>
    /// Creates the initial administrator from the "Admin" configuration section (Admin:Email,
    /// Admin:Password, optional Admin:Name). Skipped when unset or when the account already exists,
    /// so credentials can be removed from configuration after first start.
    /// </summary>
    private static async Task SeedAdminAsync(AppDbContext context, IConfiguration config, ILogger logger)
    {
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        email = EmailAddresses.Normalize(email);
        if (await context.Users.AnyAsync(u => u.Email == email))
            return;

        if (password.Length < 12)
            throw new InvalidOperationException("Admin:Password must be at least 12 characters.");

        context.Users.Add(new AppUser
        {
            Name = config["Admin:Name"] ?? "Administrator",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Roles.Admin
        });
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded administrator account {Email}.", email);
    }
}