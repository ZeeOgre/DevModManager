using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.BattleNet;

public sealed class BattleNetInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.BattleNet;

    public StoreScanResult Scan(StoreScanContext context)
        => ScanAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    public Task<StoreScanResult> ScanAsync(StoreScanContext context, CancellationToken ct = default)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        try
        {
            var installPath = WindowsLauncherDiscovery.TryFindInstallLocationByDisplayNameContains("Battle.net");

            if (!string.IsNullOrWhiteSpace(installPath))
            {
                apps.Add(new AppInstallSnapshot
                {
                    Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = "battle-net-launcher", InstallInstanceId = null },
                    DisplayName = "Battle.net",
                    InstallFolders = WindowsLauncherDiscovery.CreateInstallFolders(installPath!),
                    ExecutableName = "Battle.net.exe",
                    InstallState = InstallState.Installed,
                    Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "blizzard", "launcher" },
                    StoreMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Discovery"] = "Windows uninstall registry"
                    },
                    Depots = Array.Empty<DepotSnapshot>(),
                    Issues = Array.Empty<ScanIssue>()
                });
            }

            issues.Add(new ScanIssue
            {
                Code = "BATTLENET_GAME_ENUMERATION_PENDING",
                Message = "Battle.net game-level enumeration is pending parser implementation modeled from FriendsOfGalaxy metadata files.",
                StoreKey = StoreKey
            });
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "BATTLENET_SCAN_FAILED",
                Message = "Battle.net scan failed.",
                StoreKey = StoreKey,
                Exception = WindowsLauncherDiscovery.ToExceptionInfo(ex)
            });
        }

        return Task.FromResult(new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues });
    }
}
