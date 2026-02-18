namespace DMM.AssetManagers.GameStores.Common.Models;

public sealed record AppInstallSnapshot
{
    /// <summary>Store key: "steam", "xbox", etc.</summary>
    public required StoreInstallId Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Primary install folders and any known related content roots.</summary>
    public required InstallFoldersSnapshot InstallFolders { get; init; }

    /// <summary>Main executable name if known (not full path).</summary>
    public string? ExecutableName { get; init; }

    /// <summary>Common marketing/media assets, if discoverable.</summary>
    public AppVisualAssetsSnapshot? VisualAssets { get; init; }

    /// <summary>Normalized version string if store reports it.</summary>
    public string? Version { get; init; }

    /// <summary>Best-effort install state from the store catalog perspective.</summary>
    public InstallState InstallState { get; init; } = InstallState.Installed;

    /// <summary>Optional: last updated timestamp if store provides it.</summary>
    public DateTimeOffset? LastUpdatedUtc { get; init; }

    /// <summary>Steam-only (or other store that has depots/manifests). Empty otherwise.</summary>
    public IReadOnlyList<DepotSnapshot> Depots { get; init; } = Array.Empty<DepotSnapshot>();

    /// <summary>Free-form store-specific metadata (ids, package family name, etc.).</summary>
    public IReadOnlyDictionary<string, string> StoreMetadata { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Scanner notes/issues tied to this app specifically.</summary>
    public IReadOnlyList<ScanIssue> Issues { get; init; } = Array.Empty<ScanIssue>();
}

public sealed record StoreInstallId
{
    public required string StoreKey { get; init; }
    public required string StoreAppId { get; init; }
    public string? InstallInstanceId { get; init; } // optional
}


public enum InstallState
{
    Installed,
    NotInstalled,     // typically shouldn’t appear in install snapshot, but useful if adapters emit catalog entries
    PartiallyInstalled,
    UpdatePending,
    Unknown
}
