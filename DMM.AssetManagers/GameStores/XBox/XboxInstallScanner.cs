using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.XBox;

public sealed class XboxInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.Xbox; // or "xbox" if you haven't added StoreKeys yet

    public Task<StoreScanResult> ScanAsync(
        StoreScanContext context,
        CancellationToken ct = default)
        => Task.FromResult(Scan(context));

    public StoreScanResult Scan(StoreScanContext context)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        // Roots: either caller-supplied, or discover via .GamingRoot
        List<string> roots;
        try
        {
            roots = (context.Roots is { Count: > 0 })
                ? context.Roots.Where(r => !string.IsNullOrWhiteSpace(r)).ToList()
                : XboxGameCatalog.FindGamingRoots();
        }
        catch (Exception ex)
        {
            roots = new List<string>();
            issues.Add(new ScanIssue
            {
                Code = "XBOX_ROOT_DISCOVERY_FAILED",
                Message = "Failed to discover Xbox gaming roots.",
                StoreKey = StoreKey,
                Exception = ToExceptionInfo(ex)
            });
        }

        // Build catalog from discovered roots
        List<XboxGameCatalog.GameEntry> catalog;
        try
        {
            catalog = XboxGameCatalog.BuildCatalog(roots);
        }
        catch (Exception ex)
        {
            catalog = new List<XboxGameCatalog.GameEntry>();
            issues.Add(new ScanIssue
            {
                Code = "XBOX_CATALOG_BUILD_FAILED",
                Message = "Failed to build Xbox game catalog from gaming roots.",
                StoreKey = StoreKey,
                Exception = ToExceptionInfo(ex)
            });
        }

        // Map catalog entries -> normalized AppInstallSnapshot
        foreach (var e in catalog)
        {
            try
            {
                apps.Add(MapEntry(e, context.IncludeVisualAssets));
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "XBOX_ENTRY_MAP_FAILED",
                    Message = $"Failed to map Xbox catalog entry '{e.IdentityName}'.",
                    StoreKey = StoreKey,
                    AppKey = e.IdentityName,
                    Path = e.ConfigPath,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        return new StoreScanResult
        {
            StoreKey = StoreKey,
            Apps = apps,
            Issues = issues
        };
    }

    private AppInstallSnapshot MapEntry(XboxGameCatalog.GameEntry e, bool includeVisualAssets)
    {
        var folders = new InstallFoldersSnapshot
        {
            InstallFolder = new FolderRef { Path = e.InstallRoot },
            ContentFolder = string.IsNullOrWhiteSpace(e.ContentPath) ? null : new FolderRef { Path = e.ContentPath! },
            DataFolder = null // Xbox installs don't have a "Data" folder concept like Bethesda PC; leave null
        };

        AppVisualAssetsSnapshot? visuals = null;
        if (includeVisualAssets)
        {
            visuals = new AppVisualAssetsSnapshot
            {
                Icon = MakeVisual(e.IconPath),
                Logo = MakeVisual(e.LogoPath),
                Splash = MakeVisual(e.SplashScreenPath)
            };

            // Optional: if all 3 are null, just keep visuals null
            if (visuals.Icon is null && visuals.Logo is null && visuals.Splash is null)
                visuals = null;
        }

        var storeMeta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConfigPath"] = e.ConfigPath
        };

        if (!string.IsNullOrWhiteSpace(e.StoreId)) storeMeta["StoreId"] = e.StoreId!;
        if (!string.IsNullOrWhiteSpace(e.TitleId)) storeMeta["TitleId"] = e.TitleId!;
        if (!string.IsNullOrWhiteSpace(e.ContentPath)) storeMeta["ContentPath"] = e.ContentPath!;

        return new AppInstallSnapshot
        {
            Id = new StoreInstallId
            {
                StoreKey = StoreKey,
                StoreAppId = e.IdentityName,
                InstallInstanceId = null // set later if you ever want to disambiguate multiple installs across roots
            },

            DisplayName = e.DisplayName,
            InstallFolders = folders,
            ExecutableName = e.ExecutableName,
            VisualAssets = visuals,
            Version = null, // MicrosoftGame.config doesn't provide a simple version field in your current parse
            InstallState = InstallState.Installed,
            LastUpdatedUtc = null,
            Depots = Array.Empty<DepotSnapshot>(),

            StoreMetadata = storeMeta,
            Issues = Array.Empty<ScanIssue>()
        };
    }

    private static VisualAssetRef? MakeVisual(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        // Your catalog resolves these as Path.Combine(gameDir, relative) already.
        // Only keep if the file is actually present.
        if (!File.Exists(path))
            return null;

        return new VisualAssetRef
        {
            FilePath = path
        };
    }

    private static ExceptionInfo ToExceptionInfo(Exception ex) => new ExceptionInfo
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };
}
