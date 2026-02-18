namespace DMM.AssetManagers.GameStores.Common.Models;

public sealed record DepotSnapshot
{
    /// <summary>Store-specific depot identifier (Steam depotid).</summary>
    public required string DepotId { get; init; }

    /// <summary>Manifest identifier that this depot is pinned to for this install.</summary>
    public string? ManifestId { get; init; }

    /// <summary>Optional: content branch (public, beta, etc.).</summary>
    public string? Branch { get; init; }

    /// <summary>Optional: build/version correlation if the store reports it.</summary>
    public string? BuildId { get; init; }

    /// <summary>Optional: where this depot content resides relative to install root, if known.</summary>
    public string? InstallDir { get; init; }

    /// <summary>Optional: depot flags (DLC, language pack, optional content).</summary>
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Store-specific metadata as a pressure valve.</summary>
    public IReadOnlyDictionary<string, string> StoreMetadata { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record DepotManifestPair
{
    public required string DepotId { get; init; }
    public required string ManifestId { get; init; }
}
