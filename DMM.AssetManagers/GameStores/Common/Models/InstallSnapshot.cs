namespace DMM.AssetManagers.GameStores.Common.Models;

public sealed record InstallSnapshot
{
    public required SnapshotIdentity Identity { get; init; }

    /// <summary>All discovered installs across the scan scope.</summary>
    public required IReadOnlyList<AppInstallSnapshot> Apps { get; init; }

    /// <summary>Non-fatal problems encountered during scanning/parsing.</summary>
    public IReadOnlyList<ScanIssue> Issues { get; init; } = Array.Empty<ScanIssue>();
}

public sealed record SnapshotIdentity
{
    /// <summary>UTC timestamp when the snapshot was created.</summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>What was requested: one store, or "all stores".</summary>
    public required ScanScope Scope { get; init; }

    /// <summary>Optional machine identifier for debugging multi-PC logs (do not treat as stable ID).</summary>
    public string? MachineName { get; init; }

    /// <summary>Scanner build/version for diagnostics.</summary>
    public string? ScannerVersion { get; init; }
}

public enum ScanScope
{
    StoreSingle,
    StoresAll
}

public sealed record ScanIssue
{
    public required string Code { get; init; }          // e.g. "REGISTRY_READ_FAILED"
    public required string Message { get; init; }       // human friendly
    public string? StoreKey { get; init; }              // "steam", "xbox"
    public string? AppKey { get; init; }                // store app id if known
    public string? Path { get; init; }                  // offending path if relevant
    public ExceptionInfo? Exception { get; init; }      // no raw Exception in model
}

public sealed record ExceptionInfo
{
    public required string Type { get; init; }
    public required string Message { get; init; }
    public string? HResult { get; init; }
}
