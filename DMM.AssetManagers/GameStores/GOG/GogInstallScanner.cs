using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Gog;

[SupportedOSPlatform("windows")]
public sealed class GogInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.Gog;

    public StoreScanResult Scan(StoreScanContext context)
        => ScanAsync(context).GetAwaiter().GetResult();

    public async Task<StoreScanResult> ScanAsync(StoreScanContext context, CancellationToken ct = default)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        IReadOnlyList<object> roots = context.Roots;

        List<GogGameCatalog.GameEntry> catalog;
        try
        {
            ct.ThrowIfCancellationRequested();
            catalog = GogGameCatalog.BuildCatalog(roots, issues);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "GOG_CATALOG_BUILD_FAILED",
                Message = "Failed to build GOG catalog from roots.",
                StoreKey = StoreKey,
                Exception = ToExceptionInfo(ex)
            });

            return new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues };
        }

        foreach (var e in catalog)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                apps.Add(MapGameEntryBase(e));

                foreach (var t in e.Tools)
                    apps.Add(MapToolEntryBase(e, t));
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "GOG_ENTRY_MAP_FAILED",
                    Message = $"Failed to map GOG catalog entry '{e.ProductId}'.",
                    StoreKey = StoreKey,
                    AppKey = e.ProductId,
                    Path = e.SourcePath,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        await GogDataEnrichment.DoDataEnrichmentAsync(context, apps, issues, ct).ConfigureAwait(false);

        if ((context.Roots?.Count ?? 0) > 0 && apps.Count == 0)
        {
            issues.Add(new ScanIssue
            {
                Code = "GOG_NO_APPS_FOUND",
                Message = $"GOG roots supplied ({context.Roots.Count}) but no installs were discovered.",
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

    private AppInstallSnapshot MapGameEntryBase(GogGameCatalog.GameEntry e)
    {
        var folders = new InstallFoldersSnapshot
        {
            InstallFolder = !string.IsNullOrWhiteSpace(e.InstallFolder)
                ? new FolderRef { Path = e.InstallFolder! }
                : null,
            ContentFolder = null,
            DataFolder = null,
            AdditionalFolders = Array.Empty<NamedFolderRef>()
        };

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductId"] = e.ProductId,
            ["SourceKind"] = e.SourceKind,
            ["SourcePath"] = e.SourcePath,
            ["WebsiteSearchUrl"] = BuildGogGameUrlOrFallback(e.DisplayName)
        };

        if (!string.IsNullOrWhiteSpace(e.PrimaryExeFullPath))
            meta["PrimaryExeFullPath"] = e.PrimaryExeFullPath!;

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gog",
            "game"
        };

        if (!string.IsNullOrWhiteSpace(e.InstallFolder) && Directory.Exists(e.InstallFolder!))
            tags.Add("install-folder-exists");

        if (!string.IsNullOrWhiteSpace(e.PrimaryExeName))
            tags.Add("has-exe");

        return new AppInstallSnapshot
        {
            Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = e.ProductId, InstallInstanceId = null },
            DisplayName = e.DisplayName,
            InstallFolders = folders,
            ExecutableName = e.PrimaryExeName,
            VisualAssets = null,
            Version = null,
            InstallState = BestEffortState(e),
            LastUpdatedUtc = null,
            Depots = Array.Empty<DepotSnapshot>(),
            StoreMetadata = meta,
            Issues = PerAppIssues(e),
            Tags = tags
        };
    }

    private AppInstallSnapshot MapToolEntryBase(GogGameCatalog.GameEntry parent, GogGameCatalog.ToolEntry tool)
    {
        var toolFolder = !string.IsNullOrWhiteSpace(tool.ExeFullPath)
            ? SafeGetDirectoryName(tool.ExeFullPath!)
            : parent.InstallFolder;

        var folders = new InstallFoldersSnapshot
        {
            InstallFolder = !string.IsNullOrWhiteSpace(toolFolder) ? new FolderRef { Path = toolFolder! } : null,
            ContentFolder = null,
            DataFolder = null,
            AdditionalFolders = Array.Empty<NamedFolderRef>()
        };

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductId"] = parent.ProductId,
            ["ParentDisplayName"] = parent.DisplayName,
            ["ToolKey"] = tool.ToolKey,
            ["SourcePath"] = tool.SourcePath,
            ["SourceKind"] = parent.SourceKind
        };

        if (!string.IsNullOrWhiteSpace(tool.ExeFullPath))
            meta["ExeFullPath"] = tool.ExeFullPath!;

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gog", "tool" };

        return new AppInstallSnapshot
        {
            Id = new StoreInstallId { StoreKey = StoreKey, StoreAppId = tool.ToolKey, InstallInstanceId = null },
            DisplayName = tool.DisplayName,
            InstallFolders = folders,
            ExecutableName = tool.ExeName,
            VisualAssets = null,
            Version = null,
            InstallState = BestEffortToolState(parent, tool),
            LastUpdatedUtc = null,
            Depots = Array.Empty<DepotSnapshot>(),
            StoreMetadata = meta,
            Issues = Array.Empty<ScanIssue>(),
            Tags = tags
        };
    }

    private static InstallState BestEffortState(GogGameCatalog.GameEntry e)
    {
        if (!string.IsNullOrWhiteSpace(e.InstallFolder) && Directory.Exists(e.InstallFolder!))
            return InstallState.Installed;

        if (!string.IsNullOrWhiteSpace(e.InstallFolder))
            return InstallState.Unknown;

        return InstallState.Installed;
    }

    private static InstallState BestEffortToolState(GogGameCatalog.GameEntry parent, GogGameCatalog.ToolEntry tool)
    {
        if (!string.IsNullOrWhiteSpace(tool.ExeFullPath) && File.Exists(tool.ExeFullPath!))
            return InstallState.Installed;

        return InstallState.Unknown;
    }

    private static IReadOnlyList<ScanIssue> PerAppIssues(GogGameCatalog.GameEntry e)
    {
        if (!string.IsNullOrWhiteSpace(e.InstallFolder) && !Directory.Exists(e.InstallFolder!))
        {
            return new[]
            {
                new ScanIssue
                {
                    Code = "GOG_INSTALL_FOLDER_MISSING",
                    Message = $"Install folder does not exist: '{e.InstallFolder}'.",
                    StoreKey = StoreKeys.Gog,
                    AppKey = e.ProductId,
                    Path = e.InstallFolder
                }
            };
        }

        return Array.Empty<ScanIssue>();
    }

    private static string? SafeGetDirectoryName(string path)
    {
        try { return Path.GetDirectoryName(path); }
        catch { return null; }
    }

    private static string BuildGogGameUrlOrFallback(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "https://www.gog.com/en/games";

        var slug = BuildSlug(displayName);
        if (string.IsNullOrWhiteSpace(slug))
            return $"https://www.gog.com/en/games?search={Uri.EscapeDataString(displayName)}";

        return $"https://www.gog.com/en/game/{Uri.EscapeDataString(slug)}";
    }

    private static string BuildSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sb.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '-' || ch == ':')
            {
                sb.Append('_');
            }
        }

        var collapsed = Regex.Replace(sb.ToString(), "_+", "_").Trim('_');

        return collapsed;
    }

    private static ExceptionInfo ToExceptionInfo(Exception ex) => new ExceptionInfo
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };
}
