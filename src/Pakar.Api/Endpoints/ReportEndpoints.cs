using System.Security.Claims;
using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;

namespace Pakar.Api.Endpoints;

/// <summary>Inaccuracy reports. Users submit and track their own (FR 5, FR 6); admins review all (FR 16).</summary>
public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var reports = app.MapGroup("/api/reports").RequireAuthorization().WithTags("Reports");

        reports.MapPost("/", async (SubmitReportRequest request, ClaimsPrincipal user, IReportService service, CancellationToken ct) =>
            (await service.SubmitAsync(user.GetUserId(), request, ct))
                .ToHttpResult(created => Results.Created($"/api/reports/{created.Id}", created)));

        reports.MapGet("/mine", async (ClaimsPrincipal user, IReportService service, CancellationToken ct) =>
            Results.Ok(await service.ListForUserAsync(user.GetUserId(), ct)));

        reports.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IReportService service, CancellationToken ct) =>
            (await service.GetAsync(id, user.GetUserId(), user.IsAdmin(), ct)).ToHttpResult());

        // Admin review queue.
        reports.MapGet("/", async (string? status, int? page, int? pageSize, IReportService service, CancellationToken ct) =>
                (await service.ListAsync(status, page, pageSize, ct)).ToHttpResult())
            .RequireAuthorization(AuthPolicies.AdminOnly);

        reports.MapPost("/{id:guid}/review", async (Guid id, ReviewReportRequest request, ClaimsPrincipal user, IReportService service, CancellationToken ct) =>
                (await service.ReviewAsync(id, user.GetUserId(), request, ct)).ToHttpResult())
            .RequireAuthorization(AuthPolicies.AdminOnly);
    }
}
