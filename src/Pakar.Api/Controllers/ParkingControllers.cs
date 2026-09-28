using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pakar.Infrastructure.Persistence;
using Pakar.Application.DTOs;

namespace Pakar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParkingController : ControllerBase
{
    private readonly AppDbContext _context;

    public ParkingController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/parking/zones
    [HttpGet("zones")]
    public async Task<ActionResult<IEnumerable<ZoneDto>>> GetZones()
    {
        var zones = await _context.Zones
            .Include(z => z.Spots)
            .Select(z => new ZoneDto
            {
                Id = z.Id,
                Name = z.Name,
                TotalCapacity = z.TotalCapacity,
                AvailableSpots = z.Spots.Count(s => !s.IsOccupied),
                Spots = z.Spots.Select(s => new SpotDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    IsOccupied = s.IsOccupied
                }).ToList()
            })
            .ToListAsync();

        return Ok(zones);
    }
}