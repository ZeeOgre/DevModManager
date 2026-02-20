using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Common;

public sealed class StoreScanOrchestrator
{
    private readonly IReadOnlyDictionary<string, IStoreInstallScanner> _scanners;

    public StoreScanOrchestrator(IEnumerable<IStoreInstallScanner> scanners)
    {
        _scanners = scanners.ToDictionary(s => s.StoreKey, StringComparer.OrdinalIgnoreCase);
    }

    // Existing sync API (keep)
    public InstallSnapshot ScanStore(string storeKey, StoreScanContext? context = null)
    {
        context ??= new StoreScanContext();

        if (!_scanners.TryGetValue(storeKey, out var scanner))
            throw new ArgumentException($"Unknown storeKey '{storeKey}'", nameof(storeKey));

        var result = scanner.Scan(context);

        return BuildSnapshot(ScanScope.StoreSingle, new[] { result });
    }

    // Existing sync API (keep)
    public InstallSnapshot ScanStores(StoreScanContext? context = null)
    {
        context ??= new StoreScanContext();

        var results = _scanners.Values
            .Select(s => s.Scan(context))
            .ToList();

        return BuildSnapshot(ScanScope.StoresAll, results);
    }

    // New async API
    public async Task<InstallSnapshot> ScanStoreAsync(
        string storeKey,
        StoreScanContext? context = null,
        CancellationToken ct = default)
    {
        context ??= new StoreScanContext();

        if (!_scanners.TryGetValue(storeKey, out var scanner))
            throw new ArgumentException($"Unknown storeKey '{storeKey}'", nameof(storeKey));

        var result = await scanner.ScanAsync(context, ct).ConfigureAwait(false);
        return BuildSnapshot(ScanScope.StoreSingle, new[] { result });
    }

    // New async API
    public async Task<InstallSnapshot> ScanStoresAsync(
        StoreScanContext? context = null,
        CancellationToken ct = default)
    {
        context ??= new StoreScanContext();

        // run stores in parallel
        var tasks = _scanners.Values.Select(s => s.ScanAsync(context, ct));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

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
