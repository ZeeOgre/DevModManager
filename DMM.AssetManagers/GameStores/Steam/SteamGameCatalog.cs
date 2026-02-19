using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;
using Microsoft.Win32;

namespace DMM.AssetManagers.GameStores.Steam;

[SupportedOSPlatform("windows")]
public static class SteamGameCatalog

{
    public sealed record VisualCandidate(string Kind, string Path);

    public sealed record GameEntry(
        string AppId,
        string DisplayName,
        string ManifestPath,
        string SteamAppsRoot,
        string InstallRoot,
        string? Installdir,
        string? BuildId,
        DateTimeOffset? LastUpdatedUtc,
        string? StateFlags,
        IReadOnlyList<DepotSnapshot> Depots,
        string? IconPath,
        string? LogoPath,
        string? SplashPath,
        IReadOnlyList<VisualCandidate> AdditionalVisuals,
        IReadOnlySet<string> DerivedTags

    );

    /// <summary>
    /// If caller passes roots, accept either:
    ///  - SteamApps folders (..\steamapps)
    ///  - Library roots containing steamapps
    /// Normalize to full steamapps paths.
    /// </summary>
    public static List<string> NormalizeSteamAppsRoots(IEnumerable<string> roots)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r0 in roots ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(r0)) continue;

            var r = Path.GetFullPath(r0.Trim());

            // If the user gave ...\steamapps
            if (string.Equals(Path.GetFileName(r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    "steamapps", StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(r)) result.Add(NormalizeDir(r));
                continue;
            }

            // If the user gave a library root containing steamapps
            var steamApps = Path.Combine(r, "steamapps");
            if (Directory.Exists(steamApps))
                result.Add(NormalizeDir(steamApps));
        }

        return result.ToList();
    }

    public static List<string> DiscoverSteamAppsRoots(List<ScanIssue> issues)
    {
        var steamPath = TryGetSteamInstallPath(issues);
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
            return new List<string>();

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The primary steamapps
        var primarySteamApps = Path.Combine(steamPath, "steamapps");
        if (Directory.Exists(primarySteamApps))
            roots.Add(NormalizeDir(primarySteamApps));

        // Library folders
        var libraryVdf = Path.Combine(primarySteamApps, "libraryfolders.vdf");
        foreach (var lib in TryParseLibraryFolders(libraryVdf, issues))
        {
            var steamApps = Path.Combine(lib, "steamapps");
            if (Directory.Exists(steamApps))
                roots.Add(NormalizeDir(steamApps));
        }

        return roots.ToList();
    }

    public static List<GameEntry> BuildCatalog(IEnumerable<string> steamAppsRoots, List<ScanIssue> issues)
    {
        var results = new List<GameEntry>();

        foreach (var steamApps in steamAppsRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(steamApps))
                continue;

            foreach (var manifest in EnumerateAppManifests(steamApps))
            {
                try
                {
                    var entry = TryParseAppManifest(manifest, steamApps, issues);
                    if (entry != null)
                        results.Add(entry);
                }
                catch (Exception ex)
                {
                    issues.Add(new ScanIssue
                    {
                        Code = "STEAM_MANIFEST_PARSE_FAILED",
                        Message = $"Failed parsing Steam manifest '{manifest}'.",
                        StoreKey = StoreKeys.Steam,
                        Path = manifest,
                        Exception = new ExceptionInfo
                        {
                            Type = ex.GetType().FullName ?? ex.GetType().Name,
                            Message = ex.Message,
                            HResult = ex.HResult.ToString("X")
                        }
                    });
                }
            }
        }

        // Deduplicate by AppId + InstallRoot (possible in odd multi-library states)
        return results
            .GroupBy(e => (e.AppId, e.InstallRoot), new SteamTupleComparer())
            .Select(g => g.First())
            .ToList();
    }

    private static IEnumerable<string> EnumerateAppManifests(string steamAppsRoot)
    {
        try
        {
            return Directory.EnumerateFiles(steamAppsRoot, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private static GameEntry? TryParseAppManifest(string manifestPath, string steamAppsRoot, List<ScanIssue> issues)
    {
        // appmanifest structure is KeyValues with a root "AppState" section
        var root = SteamVdf.ParseFile(manifestPath);
        var appState = root.GetSection("AppState");
        if (appState is null) return null;

        var appId = appState.GetString("appid")?.Trim();
        if (string.IsNullOrWhiteSpace(appId)) return null;

        var name = appState.GetString("name")?.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = $"steam:{appId}";

        var installdir = appState.GetString("installdir")?.Trim();
        if (string.IsNullOrWhiteSpace(installdir))
        {
            issues.Add(new ScanIssue
            {
                Code = "STEAM_MANIFEST_MISSING_INSTALLDIR",
                Message = $"Steam manifest missing installdir for appid {appId}.",
                StoreKey = StoreKeys.Steam,
                AppKey = appId,
                Path = manifestPath
            });
            return null;
        }

        var common = Path.Combine(steamAppsRoot, "common");
        var installRoot = NormalizeDir(Path.Combine(common, installdir));

        // If folder doesn't exist, treat as not installed (for now: skip)
        if (!Directory.Exists(installRoot))
            return null;

        var buildId = appState.GetString("buildid")?.Trim();
        var stateFlags = appState.GetString("StateFlags")?.Trim();
        
        var depots = ParseInstalledDepots(appState, buildId);

        var derivedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
            "installed",
            "steam"
};

        if (!string.IsNullOrWhiteSpace(buildId))
            derivedTags.Add("has-buildid");

        if (depots.Count > 0)
            derivedTags.Add("has-depots");

        if (depots.Count >= 2)
            derivedTags.Add("multi-depot");

        // Heuristics that are useful *now*, before appinfo.vdf enrichment:
        var sizeOnDisk = appState.GetString("SizeOnDisk")?.Trim();
        if (long.TryParse(sizeOnDisk, out var bytes))
        {
            if (bytes < 200L * 1024 * 1024) derivedTags.Add("small-install");
            if (bytes > 20L * 1024 * 1024 * 1024) derivedTags.Add("large-install");
        }

        var appType = appState.GetString("Type")?.Trim(); // sometimes present
        if (!string.IsNullOrWhiteSpace(appType))
            derivedTags.Add($"type:{appType.ToLowerInvariant()}");

        DateTimeOffset? lastUpdatedUtc = null;
        var lastUpdatedRaw = appState.GetString("LastUpdated")?.Trim();
        if (long.TryParse(lastUpdatedRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
        {
            try { lastUpdatedUtc = DateTimeOffset.FromUnixTimeSeconds(epoch); } catch { /* ignore */ }
        }

        // Installed depots

        // Visual assets: use Steam appcache librarycache when possible
        var steamPath = TryGetSteamInstallPath(issues);
        var visuals = (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
            ? (icon: (string?)null, logo: (string?)null, splash: (string?)null, extra: (IReadOnlyList<VisualCandidate>)Array.Empty<VisualCandidate>())
            : FindLibraryCacheVisuals(steamPath!, appId);

        return new GameEntry(
            AppId: appId,
            DisplayName: name!,
            ManifestPath: manifestPath,
            SteamAppsRoot: steamAppsRoot,
            InstallRoot: installRoot,
            Installdir: installdir,
            BuildId: string.IsNullOrWhiteSpace(buildId) ? null : buildId,
            LastUpdatedUtc: lastUpdatedUtc,
            StateFlags: string.IsNullOrWhiteSpace(stateFlags) ? null : stateFlags,
            Depots: depots,
            IconPath: visuals.icon,
            LogoPath: visuals.logo,
            SplashPath: visuals.splash,
            AdditionalVisuals: visuals.extra,
            DerivedTags: derivedTags


        );
    }

    private static IReadOnlyList<DepotSnapshot> ParseInstalledDepots(SteamVdf.Node appState, string? buildId)
    {
        var depots = new List<DepotSnapshot>();

        // Newer manifests: InstalledDepots { "123" { "manifest" "987" } }
        var installed = appState.GetSection("InstalledDepots");
        if (installed != null)
        {
            foreach (var (depotId, depotNode) in installed.Children)
            {
                if (depotNode is not SteamVdf.Node depotSection) continue;

                var manifestId = depotSection.GetString("manifest")?.Trim();
                depots.Add(new DepotSnapshot
                {
                    DepotId = depotId,
                    ManifestId = string.IsNullOrWhiteSpace(manifestId) ? null : manifestId,
                    Branch = null,
                    BuildId = buildId,
                    InstallDir = null,
                    Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    StoreMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                });
            }
        }

        // Older manifests can have "MountedDepots" etc; you can add later if needed.

        return depots;
    }

    private static (string? icon, string? logo, string? splash, IReadOnlyList<VisualCandidate> extra) FindLibraryCacheVisuals(string steamPath, string appId)
    {
        var cacheDir = Path.Combine(steamPath, "appcache", "librarycache");
        if (!Directory.Exists(cacheDir))
            return (null, null, null, Array.Empty<VisualCandidate>());

        string? Pick(params string[] candidates)
        {
            foreach (var c in candidates)
            {
                var p = Path.Combine(cacheDir, c);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        // Common Steam cache naming patterns (not guaranteed)
        var icon = Pick($"{appId}_icon.jpg", $"{appId}_icon.png");
        var logo = Pick($"{appId}_logo.png", $"{appId}_logo.jpg");
        var splash = Pick($"{appId}_library_hero.jpg", $"{appId}_library_hero.png");

        var extra = new List<VisualCandidate>();

        void AddIfExists(string kind, params string[] names)
        {
            var p = Pick(names);
            if (p != null) extra.Add(new VisualCandidate(kind, p));
        }

        AddIfExists("header", $"{appId}_header.jpg", $"{appId}_header.png");
        AddIfExists("library_600x900", $"{appId}_library_600x900.jpg", $"{appId}_library_600x900.png");
        AddIfExists("library_hero_blur", $"{appId}_library_hero_blur.jpg", $"{appId}_library_hero_blur.png");

        return (icon, logo, splash, extra);
    }

    private static IEnumerable<string> TryParseLibraryFolders(string libraryFoldersVdfPath, List<ScanIssue> issues)
    {
        if (!File.Exists(libraryFoldersVdfPath))
            return Enumerable.Empty<string>();

        try
        {
            var root = SteamVdf.ParseFile(libraryFoldersVdfPath);
            var lib = root.GetSection("libraryfolders") ?? root; // older vs newer variants

            // Newer format: libraryfolders { "0" { "path" "C:\..." } "1" { "path" "D:\..." } }
            var paths = new List<string>();

            foreach (var (k, v) in lib.Children)
            {
                if (v is not SteamVdf.Node node) continue;

                var p = node.GetString("path");
                if (!string.IsNullOrWhiteSpace(p))
                    paths.Add(Path.GetFullPath(p.Trim()));
            }

            // Older format: libraryfolders { "0" "C:\..." "1" "D:\..." }
            if (paths.Count == 0)
            {
                foreach (var (k, v) in lib.Values)
                {
                    if (v is string s && !string.IsNullOrWhiteSpace(s))
                        paths.Add(Path.GetFullPath(s.Trim()));
                }
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "STEAM_LIBRARYFOLDERS_PARSE_FAILED",
                Message = "Failed to parse Steam libraryfolders.vdf.",
                StoreKey = StoreKeys.Steam,
                Path = libraryFoldersVdfPath,
                Exception = new ExceptionInfo
                {
                    Type = ex.GetType().FullName ?? ex.GetType().Name,
                    Message = ex.Message,
                    HResult = ex.HResult.ToString("X")
                }
            });
            return Enumerable.Empty<string>();
        }
    }

    private static string? TryGetSteamInstallPath(List<ScanIssue> issues)
    {
        // Try HKCU first
        string? Read(RegistryKey baseKey, string subKey, string valueName)
        {
            try
            {
                using var k = baseKey.OpenSubKey(subKey);
                return k?.GetValue(valueName) as string;
            }
            catch
            {
                return null;
            }
        }

        var hkcu = Read(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        if (!string.IsNullOrWhiteSpace(hkcu))
            return Path.GetFullPath(hkcu);

        var hklmWow = Read(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        if (!string.IsNullOrWhiteSpace(hklmWow))
            return Path.GetFullPath(hklmWow);

        var hklm = Read(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        if (!string.IsNullOrWhiteSpace(hklm))
            return Path.GetFullPath(hklm);

        issues.Add(new ScanIssue
        {
            Code = "STEAM_REGISTRY_PATH_NOT_FOUND",
            Message = "Steam install path not found in registry (HKCU/HKLM).",
            StoreKey = StoreKeys.Steam
        });

        return null;
    }

    private static string NormalizeDir(string path)
        => Path.GetFullPath(path.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

    private sealed class SteamTupleComparer : IEqualityComparer<(string AppId, string InstallRoot)>
    {
        public bool Equals((string AppId, string InstallRoot) x, (string AppId, string InstallRoot) y)
            => string.Equals(x.AppId, y.AppId, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.InstallRoot, y.InstallRoot, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string AppId, string InstallRoot) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.AppId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.InstallRoot));
    }
}
