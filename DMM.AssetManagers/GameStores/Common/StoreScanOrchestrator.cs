namespace DMM.AssetManagers.GameStores.Common;

using DMM.AssetManagers.GameStores.Common.Models;

public sealed class StoreScanOrchestrator
{
    private readonly IReadOnlyDictionary<string, IStoreInstallScanner> _scanners;

    public StoreScanOrchestrator(IEnumerable<IStoreInstallScanner> scanners)
    {
        _scanners = scanners.ToDictionary(s => s.StoreKey, StringComparer.OrdinalIgnoreCase);
    }

    public InstallSnapshot ScanStore(string storeKey, StoreScanContext? context = null)
    {
        context ??= new StoreScanContext();

        if (!_scanners.TryGetValue(storeKey, out var scanner))
            throw new ArgumentException($"Unknown storeKey '{storeKey}'", nameof(storeKey));

        var result = scanner.Scan(context);

        return BuildSnapshot(ScanScope.StoreSingle, new[] { result });
    }

    public InstallSnapshot ScanStores(StoreScanContext? context = null)
    {
        context ??= new StoreScanContext();

        var results = _scanners.Values
            .Select(s => s.Scan(context))
            .ToList();

        return BuildSnapshot(ScanScope.StoresAll, results);
    }

    private static InstallSnapshot BuildSnapshot(ScanScope scope, IReadOnlyList<StoreScanResult> results)
    {
        var apps = new List<AppInstallSnapshot>();
        var issues = new List<ScanIssue>();

        foreach (var r in results)
        {
            apps.AddRange(r.Apps);
            issues.AddRange(r.Issues);
        }

        return new InstallSnapshot
        {
            Identity = new SnapshotIdentity
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Scope = scope
            },
            Apps = apps,
            Issues = issues
        };
    }
}
