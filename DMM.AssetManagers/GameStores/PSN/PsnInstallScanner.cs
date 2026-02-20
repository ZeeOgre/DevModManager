using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.PSN;

public sealed class PsnInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.Psn;

    public StoreScanResult Scan(StoreScanContext context)
        => ScanAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    public Task<StoreScanResult> ScanAsync(StoreScanContext context, CancellationToken ct = default)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        try
        {
            var installPath = WindowsLauncherDiscovery.TryFindInstallLocationByDisplayNameContains("PlayStation")
                ?? WindowsLauncherDiscovery.TryFindInstallLocationByDisplayNameContains("PS Remote Play");

            if (!string.IsNullOrWhiteSpace(installPath))
            {
                apps.Add(new AppInstallSnapshot
                {
                    Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = "playstation-launcher", InstallInstanceId = null },
                    DisplayName = "PlayStation PC",
                    InstallFolders = WindowsLauncherDiscovery.CreateInstallFolders(installPath!),
                    ExecutableName = null,
                    InstallState = InstallState.Installed,
                    Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "playstation", "launcher" },
                    StoreMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Discovery"] = "Windows uninstall registry"
                    },
                    Depots = Array.Empty<DepotSnapshot>(),
                    Issues = Array.Empty<ScanIssue>()
                });
            }
            else
            {
                issues.Add(new ScanIssue
                {
                    Code = "PSN_LAUNCHER_NOT_FOUND",
                    Message = "No PlayStation PC launcher installation was discovered.",
                    StoreKey = StoreKey
                });
            }

            issues.Add(new ScanIssue
            {
                Code = "PSN_GAME_ENUMERATION_PENDING",
                Message = "PSN game-level enumeration is not yet implemented. FriendsOfGalaxy-derived mappings can be added when script details are available.",
                StoreKey = StoreKey
            });
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "PSN_SCAN_FAILED",
                Message = "PSN scan failed.",
                StoreKey = StoreKey,
                Exception = WindowsLauncherDiscovery.ToExceptionInfo(ex)
            });
        }

        return Task.FromResult(new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues });
    }
}
