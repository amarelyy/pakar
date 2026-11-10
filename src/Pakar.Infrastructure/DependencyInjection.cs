using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pakar.Application.Interfaces;
using Pakar.Infrastructure.Services;

namespace Pakar.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the EF Core implementations of the Application interfaces.</summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
