using System.ComponentModel.DataAnnotations;

namespace Pakar.Api.Security;

/// <summary>
/// Shared secret that edge devices (camera agents, inference workers) present on ingest routes.
/// Bound from the "Ingest" configuration section.
/// </summary>
public sealed class IngestOptions
{
    public const string SectionName = "Ingest";

    [Required]
    [MinLength(32)]
    public string ApiKey { get; init; } = string.Empty;
}
