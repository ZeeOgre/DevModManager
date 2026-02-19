namespace DMM.AssetManagers.GameStores.Common;

using DMM.AssetManagers.GameStores.Common.Models;

public interface IStoreInstallScanner
{
    string StoreKey { get; }

    StoreScanResult Scan(StoreScanContext context);
}

public sealed record StoreScanContext
{
    public IReadOnlyList<string> Roots { get; init; } = Array.Empty<string>();
    public bool IncludeVisualAssets { get; init; } = true;
}

public sealed record StoreScanResult
{
    public required string StoreKey { get; init; }
    public required IReadOnlyList<AppInstallSnapshot> Apps { get; init; }
    public IReadOnlyList<ScanIssue> Issues { get; init; } = Array.Empty<ScanIssue>();
}
