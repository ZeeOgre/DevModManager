using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Steam;

[SupportedOSPlatform("windows")]
public sealed class SteamInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.Steam;

    public StoreScanResult Scan(StoreScanContext context)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        List<string> steamAppsRoots;
        try
        {
            steamAppsRoots = (context.Roots is { Count: > 0 })
                ? SteamGameCatalog.NormalizeSteamAppsRoots(context.Roots)
                : SteamGameCatalog.DiscoverSteamAppsRoots(issues);
        }
        catch (Exception ex)
        {
            steamAppsRoots = new List<string>();
            issues.Add(new ScanIssue
            {
                Code = "STEAM_ROOT_DISCOVERY_FAILED",
                Message = "Failed to discover Steam library roots.",
                StoreKey = StoreKey,
                Exception = ToExceptionInfo(ex)
            });
        }

        List<SteamGameCatalog.GameEntry> catalog;
        try
        {
            catalog = SteamGameCatalog.BuildCatalog(steamAppsRoots, issues);
        }
        catch (Exception ex)
        {
            catalog = new List<SteamGameCatalog.GameEntry>();
            issues.Add(new ScanIssue
            {
                Code = "STEAM_CATALOG_BUILD_FAILED",
                Message = "Failed to build Steam game catalog from library roots.",
                StoreKey = StoreKey,
                Exception = ToExceptionInfo(ex)
            });
        }

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
                    Code = "STEAM_ENTRY_MAP_FAILED",
                    Message = $"Failed to map Steam catalog entry '{e.AppId}'.",
                    StoreKey = StoreKey,
                    AppKey = e.AppId,
                    Path = e.ManifestPath,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        // Helpful: if roots exist but no apps were found, emit a non-fatal issue
        if (steamAppsRoots.Count > 0 && apps.Count == 0)
        {
            issues.Add(new ScanIssue
            {
                Code = "STEAM_NO_APPS_FOUND",
                Message = $"Steam library roots found ({steamAppsRoots.Count}) but no appmanifest_*.acf files were parsed.",
                StoreKey = StoreKey
            });
        }

        return new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues };
    }

    private AppInstallSnapshot MapEntry(SteamGameCatalog.GameEntry e, bool includeVisualAssets)
    {
        var folders = new InstallFoldersSnapshot
        {
            InstallFolder = new FolderRef { Path = e.InstallRoot },
            ContentFolder = null,
            DataFolder = null,
            AdditionalFolders = Array.Empty<NamedFolderRef>()
        };

        AppVisualAssetsSnapshot? visuals = null;
        if (includeVisualAssets)
        {
            visuals = new AppVisualAssetsSnapshot
            {
                Icon = MakeVisual(e.IconPath),
                Logo = MakeVisual(e.LogoPath),
                Splash = MakeVisual(e.SplashPath),
                Additional = e.AdditionalVisuals
                    .Select(v => new NamedVisualAssetRef { Kind = v.Kind, Asset = new VisualAssetRef { FilePath = v.Path } })
                    .ToList()
            };

            if (visuals.Icon is null && visuals.Logo is null && visuals.Splash is null && visuals.Additional.Count == 0)
                visuals = null;
        }

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ManifestPath"] = e.ManifestPath,
            ["SteamAppsRoot"] = e.SteamAppsRoot
        };

        if (!string.IsNullOrWhiteSpace(e.BuildId)) meta["BuildId"] = e.BuildId!;
        if (!string.IsNullOrWhiteSpace(e.Installdir)) meta["Installdir"] = e.Installdir!;
        if (!string.IsNullOrWhiteSpace(e.StateFlags)) meta["StateFlags"] = e.StateFlags!;
        if (e.LastUpdatedUtc is not null) meta["LastUpdatedUtc"] = e.LastUpdatedUtc.Value.ToString("O");

        return new AppInstallSnapshot
        {
            Id = new StoreInstallId
            {
                StoreKey = StoreKey,
                StoreAppId = e.AppId,
                InstallInstanceId = null
            },
            DisplayName = e.DisplayName,
            InstallFolders = folders,
            ExecutableName = null, // Steam manifests generally don't declare an exe; we can add later via appinfo.vdf if desired
            VisualAssets = visuals,
            Version = e.BuildId, // best available store-provided "version-ish" value
            InstallState = InstallState.Installed,
            LastUpdatedUtc = e.LastUpdatedUtc,
            Depots = e.Depots,
            StoreMetadata = meta,
            Issues = Array.Empty<ScanIssue>()
        };
    }

    private static VisualAssetRef? MakeVisual(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!File.Exists(path)) return null;
        return new VisualAssetRef { FilePath = path };
    }

    private static ExceptionInfo ToExceptionInfo(Exception ex) => new ExceptionInfo
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };
}
