using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.BattleNet;

[SupportedOSPlatform("windows")]
public sealed class BattleNetInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.BattleNet;

    public StoreScanResult Scan(StoreScanContext context)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        // 1) Try to detect the launcher itself (optional)
        // If you already get this via a generic "uninstall registry" scanner, you can remove this.
        // For now, we keep Battle.net games as the primary output.

        // 2) Parse product.db for installed products.
        var productDbPath = TryFindProductDbPath();
        if (productDbPath is null)
        {
            issues.Add(new ScanIssue
            {
                Code = "BATTLETNET_PRODUCTDB_NOT_FOUND",
                Message = "Battle.net Agent product.db not found under ProgramData.",
                StoreKey = StoreKey
            });

            return new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues };
        }

        IReadOnlyList<BattleNetProductDb.ProductInstallInfo> installs;
        try
        {
            installs = BattleNetProductDb.TryReadProductDb(productDbPath);
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "BATTLETNET_PRODUCTDB_PARSE_FAILED",
                Message = $"Failed to parse Battle.net product.db at '{productDbPath}'.",
                StoreKey = StoreKey,
                Path = productDbPath,
                Exception = ToExceptionInfo(ex)
            });

            return new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues };
        }

        foreach (var i in installs)
        {
            try
            {
                // Heuristics:
                // - prefer entries that have an install_path
                // - if we also have installed==false, skip (unless you want "known but not installed")
                if (string.IsNullOrWhiteSpace(i.InstallPath))
                    continue;

                if (i.Installed is false)
                    continue;

                var installPath = NormalizePath(i.InstallPath!);
                if (string.IsNullOrWhiteSpace(installPath))
                    continue;

                var storeAppId = !string.IsNullOrWhiteSpace(i.Uid) ? i.Uid! : (i.ProductCode ?? "unknown");
                var display = BattleNetKnownProducts.TryGetDisplayName(i.ProductCode, i.Uid) ?? storeAppId;

                var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ProductDbPath"] = productDbPath,
                };

                if (!string.IsNullOrWhiteSpace(i.Uid)) meta["Uid"] = i.Uid!;
                if (!string.IsNullOrWhiteSpace(i.ProductCode)) meta["ProductCode"] = i.ProductCode!;
                if (!string.IsNullOrWhiteSpace(i.PlayRegion)) meta["PlayRegion"] = i.PlayRegion!;
                if (!string.IsNullOrWhiteSpace(i.VersionBranch)) meta["VersionBranch"] = i.VersionBranch!;
                if (!string.IsNullOrWhiteSpace(i.CurrentVersion)) meta["CurrentVersion"] = i.CurrentVersion!;
                if (!string.IsNullOrWhiteSpace(i.CurrentVersionStr)) meta["CurrentVersionStr"] = i.CurrentVersionStr!;
                if (i.Playable is not null) meta["Playable"] = i.Playable.Value ? "true" : "false";

                // Optional: a deterministic "launch hint" for the Battle.net client.
                // Historically: Battle.net.exe --exec="launch Pro" etc. :contentReference[oaicite:3]{index=3}
                if (!string.IsNullOrWhiteSpace(i.ProductCode))
                    meta["LaunchHint"] = $"Battle.net.exe --exec=\"launch {i.ProductCode}\"";

                apps.Add(new AppInstallSnapshot
                {
                    Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = storeAppId, InstallInstanceId = null },
                    DisplayName = display,
                    InstallFolders = new InstallFoldersSnapshot
                    {
                        InstallFolder = new FolderRef { Path = installPath },
                        ContentFolder = null,
                        DataFolder = null,
                        AdditionalFolders = Array.Empty<NamedFolderRef>(),
                    },
                    ExecutableName = null, // can be enriched later by probing known exe names
                    VisualAssets = null,
                    Version = i.CurrentVersionStr ?? i.CurrentVersion,
                    InstallState = InstallState.Installed,
                    LastUpdatedUtc = null,

                    Tags = MakeTags(i),
                    Depots = Array.Empty<DepotSnapshot>(),

                    StoreMetadata = meta,
                    Issues = Array.Empty<ScanIssue>()
                });
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "BATTLETNET_ENTRY_MAP_FAILED",
                    Message = "Failed to map Battle.net product install entry.",
                    StoreKey = StoreKey,
                    AppKey = i.Uid ?? i.ProductCode,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        if (apps.Count == 0)
        {
            issues.Add(new ScanIssue
            {
                Code = "BATTLETNET_NO_APPS_FOUND",
                Message = "Battle.net product.db was parsed but no installed apps were emitted (no install_path / installed=false).",
                StoreKey = StoreKey,
                Path = productDbPath
            });
        }

        return new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues };
    }

    public Task<StoreScanResult> ScanAsync(StoreScanContext context, CancellationToken ct = default)
        => Task.FromResult(Scan(context));

    private static string? TryFindProductDbPath()
    {
        // CommonApplicationData -> %ProgramData%
        var pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        // Most common: %ProgramData%\Battle.net\Agent\product.db :contentReference[oaicite:4]{index=4}
        var p1 = Path.Combine(pd, "Battle.net", "Agent", "product.db");
        if (File.Exists(p1)) return p1;

        // Some installs place it under Agent\data\
        var p2 = Path.Combine(pd, "Battle.net", "Agent", "data", "product.db");
        if (File.Exists(p2)) return p2;

        return null;
    }

    private static string NormalizePath(string p)
    {
        try
        {
            p = p.Trim();
            // product.db sometimes stores forward slashes; normalize
            p = p.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(p);
        }
        catch
        {
            return p;
        }
    }

    private static IReadOnlySet<string> MakeTags(BattleNetProductDb.ProductInstallInfo i)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "blizzard",
            "battlenet"
        };

        if (!string.IsNullOrWhiteSpace(i.ProductCode))
            tags.Add(i.ProductCode!);

        if (i.Playable is true) tags.Add("playable");
        if (i.Playable is false) tags.Add("not-playable");

        return tags;
    }

    private static ExceptionInfo ToExceptionInfo(Exception ex) => new ExceptionInfo
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };
}