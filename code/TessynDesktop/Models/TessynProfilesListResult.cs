using System.Collections.Generic;

namespace TessynDesktop.Models;

/// <summary>
/// Response from the profiles.list RPC.
/// </summary>
/// <remarks>Created by Claude</remarks>
public sealed class TessynProfilesListResult
{
    public List<TessynProfile> Profiles { get; init; } = [];
    public string? DefaultProfile { get; init; }
}
