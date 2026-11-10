using Microsoft.AspNetCore.Mvc;
using Pakar.Api.Endpoints;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;

namespace Pakar.Api.Controllers;

/// <summary>Public zone availability; guests can use every route without logging in (FR 3).</summary>
[ApiController]
[Route("api/[controller]")]
public class ParkingController(IZoneQueryService zones) : ControllerBase
{
    // FR 1: GET api/parking/zones, each zone with its status (available, almost_full, full).
    [HttpGet("zones")]
    public async Task<ActionResult<IReadOnlyList<ZoneDto>>> GetZones(CancellationToken ct) =>
        Ok(await zones.ListAsync(ct));

    // FR 2: GET api/parking/zones/search?q=&status=&minAvailable=&lat=&lng=&limit=
    [HttpGet("zones/search")]
    public async Task<IResult> Search([FromQuery] ZoneSearchQuery query, CancellationToken ct) =>
        (await zones.SearchAsync(query, ct)).ToHttpResult();

    // FR 4: GET api/parking/zones/{id}/alternate?lat=&lng=
    [HttpGet("zones/{id:guid}/alternate")]
    public async Task<IResult> Alternate(Guid id, [FromQuery] double? lat, [FromQuery] double? lng, CancellationToken ct) =>
        (await zones.FindAlternateAsync(id, lat, lng, ct)).ToHttpResult();
}
