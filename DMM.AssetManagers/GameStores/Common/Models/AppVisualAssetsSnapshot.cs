namespace DMM.AssetManagers.GameStores.Common.Models;

public sealed record AppVisualAssetsSnapshot
{
    public VisualAssetRef? Icon { get; init; }
    public VisualAssetRef? Logo { get; init; }
    public VisualAssetRef? Splash { get; init; }

    /// <summary>Any other store-provided images (hero art, background, etc.).</summary>
    public IReadOnlyList<NamedVisualAssetRef> Additional { get; init; } = Array.Empty<NamedVisualAssetRef>();
}

public sealed record VisualAssetRef
{
    /// <summary>Absolute file path if the asset exists locally.</summary>
    public string? FilePath { get; init; }

    /// <summary>URI if store provides a remote asset (optional; scanner may not fetch).</summary>
    public string? Uri { get; init; }

    /// <summary>Optional dimensions if known without expensive image decode.</summary>
    public int? Width { get; init; }
    public int? Height { get; init; }

    /// <summary>Optional content hash if you choose to compute it later.</summary>
    public string? Sha256 { get; init; }
}

public sealed record NamedVisualAssetRef
{
    public required string Kind { get; init; } // e.g. "hero", "background", "tile"
    public required VisualAssetRef Asset { get; init; }
}

