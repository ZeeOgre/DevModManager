using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Rockstar;

public sealed class RockstarInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.Rockstar;

    public StoreScanResult Scan(StoreScanContext context)
        => ScanAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    public Task<StoreScanResult> ScanAsync(StoreScanContext context, CancellationToken ct = default)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        try
        {
            var installPath = WindowsLauncherDiscovery.TryFindInstallLocationByDisplayNameContains("Rockstar Games Launcher");

            if (!string.IsNullOrWhiteSpace(installPath))
            {
                apps.Add(new AppInstallSnapshot
                {
                    Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = "rockstar-launcher", InstallInstanceId = null },
                    DisplayName = "Rockstar Games Launcher",
                    InstallFolders = WindowsLauncherDiscovery.CreateInstallFolders(installPath!),
                    ExecutableName = "Launcher.exe",
                    InstallState = InstallState.Installed,
                    Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "rockstar", "launcher" },
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
                Code = "ROCKSTAR_GAME_ENUMERATION_PENDING",
                Message = "Rockstar game-level enumeration is pending parser implementation modeled from FriendsOfGalaxy metadata files.",
                StoreKey = StoreKey
            });
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "ROCKSTAR_SCAN_FAILED",
                Message = "Rockstar scan failed.",
                StoreKey = StoreKey,
                Exception = WindowsLauncherDiscovery.ToExceptionInfo(ex)
            });
        }

        return Task.FromResult(new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues });
    }
}
