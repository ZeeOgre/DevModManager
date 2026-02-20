using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Epic;

[SupportedOSPlatform("windows")]
public sealed class EpicInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.Epic;

    public Task<StoreScanResult> ScanAsync(
        StoreScanContext context,
        CancellationToken ct = default)
        => Task.FromResult(Scan(context));

    public StoreScanResult Scan(StoreScanContext context)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        List<EpicGameCatalog.GameEntry> catalog;
        try
        {
            catalog = EpicGameCatalog.BuildCatalog(issues);
        }
        catch (Exception ex)
        {
            catalog = new List<EpicGameCatalog.GameEntry>();
            issues.Add(new ScanIssue
            {
                Code = "EPIC_CATALOG_BUILD_FAILED",
                Message = "Failed to build Epic game catalog.",
                StoreKey = StoreKey,
                Exception = ToExceptionInfo(ex)
            });
        }

        foreach (var e in catalog)
        {
            try
            {
                apps.Add(MapEntry(e));
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "EPIC_ENTRY_MAP_FAILED",
                    Message = $"Failed to map Epic catalog entry '{e.AppName}'.",
                    StoreKey = StoreKey,
                    AppKey = e.AppName,
                    Path = e.SourcePath,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        // Helpful: if manifests dir exists but we got nothing, make it visible.
        // (EpicGameCatalog already emits EPIC_MANIFEST_DIR_NOT_FOUND when appropriate.)
        if (apps.Count == 0)
        {
            issues.Add(new ScanIssue
            {
                Code = "EPIC_NO_APPS_FOUND",
                Message = "Epic scan completed but no installed apps were discovered (no manifests parsed and/or LauncherInstalled.dat empty).",
                StoreKey = StoreKey
            });
        }

        return new StoreScanResult
        {
            StoreKey = StoreKey,
            Apps = apps,
            Issues = issues
        };
    }

    private AppInstallSnapshot MapEntry(EpicGameCatalog.GameEntry e)
    {
        var folders = new InstallFoldersSnapshot
        {
            InstallFolder = new FolderRef { Path = e.InstallLocation },
            ContentFolder = null,
            DataFolder = null,
            AdditionalFolders = Array.Empty<NamedFolderRef>()
        };

        // Epic "LaunchExecutable" is typically a relative path inside InstallLocation.
        // Your model wants just the executable name (not full path).
        string? exeName = null;
        if (!string.IsNullOrWhiteSpace(e.LaunchExecutable))
        {
            exeName = Path.GetFileName(e.LaunchExecutable);
        }

        // Epic visuals are not reliably present on disk in a standard way (unlike Steam).
        AppVisualAssetsSnapshot? visuals = null;

        // Store metadata: already prepared by catalog; copy and enrich with computed fields.
        var meta = new Dictionary<string, string>(e.StoreMetadata, StringComparer.OrdinalIgnoreCase)
        {
            ["SourceKind"] = e.SourceKind,
            ["SourcePath"] = e.SourcePath
        };

        if (!string.IsNullOrWhiteSpace(e.LaunchExecutable))
        {
            meta["LaunchExecutable"] = e.LaunchExecutable!;
            // Helpful: computed full path (don’t use as ExecutableName, but handy metadata)
            try
            {
                var full = Path.Combine(e.InstallLocation, e.LaunchExecutable!);
                meta["LaunchExecutableFullPath"] = full;
            }
            catch
            {
                // ignore
            }
        }

        // Tags: keep the derived ones from catalog.
        var tags = e.DerivedTags;

        // Depots: Epic doesn’t expose Steam-like depots in these files; leave empty.
        var depots = Array.Empty<DepotSnapshot>();

        // InstallState: if InstallLocation folder is missing, consider it "Unknown" or "NotInstalled".
        // I’m using Unknown (less aggressive) and letting higher layers decide.
        var state = Directory.Exists(e.InstallLocation) ? InstallState.Installed : InstallState.Unknown;
        if (state != InstallState.Installed)
        {
            // Per-app issue: missing folder on disk
            return new AppInstallSnapshot
            {
                Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = e.AppName, InstallInstanceId = null },
                DisplayName = e.DisplayName,
                InstallFolders = folders,
                ExecutableName = exeName,
                VisualAssets = visuals,
                Version = e.AppVersion,
                InstallState = state,
                LastUpdatedUtc = null,
                Depots = depots,
                StoreMetadata = meta,
                Issues = new[]
                {
                    new ScanIssue
                    {
                        Code = "EPIC_INSTALL_FOLDER_MISSING",
                        Message = $"InstallLocation does not exist on disk: '{e.InstallLocation}'.",
                        StoreKey = StoreKey,
                        AppKey = e.AppName,
                        Path = e.InstallLocation
                    }
                },
                Tags = tags
            };
        }

        return new AppInstallSnapshot
        {
            Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = e.AppName, InstallInstanceId = null },
            DisplayName = e.DisplayName,
            InstallFolders = folders,
            ExecutableName = exeName,
            VisualAssets = visuals,
            Version = e.AppVersion,
            InstallState = state,
            LastUpdatedUtc = null,
            Depots = depots,
            StoreMetadata = meta,
            Issues = Array.Empty<ScanIssue>(),
            Tags = tags
        };
    }

    private static ExceptionInfo ToExceptionInfo(Exception ex) => new ExceptionInfo
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };
}
