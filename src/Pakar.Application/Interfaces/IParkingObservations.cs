using Pakar.Application.DTOs;

namespace Pakar.Application.Interfaces;

/// <summary>The current process's latest readings. A restart clears current evidence.</summary>
public interface IParkingObservations
{
    IReadOnlyDictionary<int, SpotView> Read();
}
