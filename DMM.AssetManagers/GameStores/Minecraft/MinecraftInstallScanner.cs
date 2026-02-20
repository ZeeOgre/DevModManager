using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Minecraft;

[SupportedOSPlatform("windows")]
public sealed class MinecraftInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.Minecraft;

    public StoreScanResult Scan(StoreScanContext context)
        => ScanAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    public Task<StoreScanResult> ScanAsync(StoreScanContext context, CancellationToken ct = default)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var mcRoot = Path.Combine(appData, ".minecraft");

            if (Directory.Exists(mcRoot))
            {
                var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "minecraft", "java"
                };

                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LauncherRoot"] = mcRoot,
                    ["ProfilesJson"] = Path.Combine(mcRoot, "launcher_profiles.json"),
                    ["InstallationsJson"] = Path.Combine(mcRoot, "launcher_settings.json")
                };

                apps.Add(new AppInstallSnapshot
                {
                    Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = "minecraft-java", InstallInstanceId = null },
                    DisplayName = "Minecraft: Java Edition",
                    InstallFolders = WindowsLauncherDiscovery.CreateInstallFolders(mcRoot),
                    ExecutableName = "MinecraftLauncher.exe",
                    InstallState = InstallState.Installed,
                    Version = null,
                    Tags = tags,
                    StoreMetadata = metadata,
                    Depots = Array.Empty<DepotSnapshot>(),
                    Issues = Array.Empty<ScanIssue>()
                });
            }
            else
            {
                issues.Add(new ScanIssue
                {
                    Code = "MINECRAFT_ROOT_NOT_FOUND",
                    Message = "Minecraft launcher root '.minecraft' was not found under %APPDATA%.",
                    StoreKey = StoreKey,
                    Path = mcRoot
                });
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "MINECRAFT_SCAN_FAILED",
                Message = "Minecraft scan failed.",
                StoreKey = StoreKey,
                Exception = WindowsLauncherDiscovery.ToExceptionInfo(ex)
            });
        }

        return Task.FromResult(new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues });
    }
}
