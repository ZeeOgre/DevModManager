using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;
using Microsoft.Win32;

namespace DMM.AssetManagers.GameStores.Epic;

[SupportedOSPlatform("windows")]
public static class EpicGameCatalog
{
    // Common/default locations
    private static readonly string DefaultProgramDataEpicRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic");

    private static readonly string DefaultEpicLauncherDataRoot =
        Path.Combine(DefaultProgramDataEpicRoot, "EpicGamesLauncher", "Data");

    private static readonly string DefaultManifestsDir =
        Path.Combine(DefaultEpicLauncherDataRoot, "Manifests");

    private static readonly string DefaultLauncherInstalledDat =
        Path.Combine(DefaultProgramDataEpicRoot, "UnrealEngineLauncher", "LauncherInstalled.dat");

    /// <summary>
    /// One normalized Epic install entry from either *.item or LauncherInstalled.dat.
    /// </summary>
    public sealed record GameEntry(
        string AppName,
        string DisplayName,
        string InstallLocation,
        string? LaunchExecutable,
        string? AppVersion,
        string SourcePath,
        string SourceKind, // "manifest-item" or "launcherinstalled"
        string? CatalogNamespace,
        string? CatalogItemId,
        IReadOnlySet<string> DerivedTags,
        IReadOnlyDictionary<string, string> StoreMetadata
    );

    public static List<GameEntry> BuildCatalog(List<ScanIssue> issues)
    {
        var entries = new List<GameEntry>();

        // 1) Prefer *.item manifests if we can find them
        var manifestsDir = DiscoverManifestsDir(issues);
        if (!string.IsNullOrWhiteSpace(manifestsDir) && Directory.Exists(manifestsDir))
        {
            try
            {
                entries.AddRange(ParseItemManifests(manifestsDir!, issues));
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "EPIC_MANIFESTS_PARSE_FAILED",
                    Message = $"Failed parsing Epic .item manifests in '{manifestsDir}'.",
                    StoreKey = StoreKeys.Epic,
                    Path = manifestsDir,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        // 2) Fallback: LauncherInstalled.dat (can help if manifests are missing)
        if (entries.Count == 0)
        {
            var launcherInstalledPath = DiscoverLauncherInstalledDatPath(manifestsDir, issues);
            if (!string.IsNullOrWhiteSpace(launcherInstalledPath) && File.Exists(launcherInstalledPath))
            {
                try
                {
                    entries.AddRange(ParseLauncherInstalledDat(launcherInstalledPath!, issues));
                }
                catch (Exception ex)
                {
                    issues.Add(new ScanIssue
                    {
                        Code = "EPIC_LAUNCHERINSTALLED_PARSE_FAILED",
                        Message = $"Failed parsing LauncherInstalled.dat at '{launcherInstalledPath}'.",
                        StoreKey = StoreKeys.Epic,
                        Path = launcherInstalledPath,
                        Exception = ToExceptionInfo(ex)
                    });
                }
            }
        }

        // If we have duplicates across sources, prefer manifests (they typically have more fields).
        entries = entries
            .GroupBy(e => e.AppName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var manifest = g.FirstOrDefault(x => x.SourceKind.Equals("manifest-item", StringComparison.OrdinalIgnoreCase));
                return manifest ?? g.First();
            })
            .ToList();

        return entries;
    }

    /// <summary>
    /// Discovers Epic manifests directory.
    /// Order:
    ///   - Registry AppDataPath (WOW6432 / normal, HKLM then HKCU)
    ///   - Default %ProgramData%\Epic\EpicGamesLauncher\Data\Manifests
    ///   - PCB-mode heuristic: if launcher InstallLocation exists, look for "\Epic\EpicGamesLauncher\Data\Manifests"
    /// </summary>
    public static string? DiscoverManifestsDir(List<ScanIssue> issues)
    {
        // Registry AppDataPath is the most robust if present.
        var appDataPath = TryReadEpicAppDataPathFromRegistry();
        if (!string.IsNullOrWhiteSpace(appDataPath))
        {
            var dir = Path.Combine(appDataPath!, "Manifests");
            if (Directory.Exists(dir))
                return dir;
        }

        if (Directory.Exists(DefaultManifestsDir))
            return DefaultManifestsDir;

        // PCB Mode heuristic: Epic folder moved under launcher install directory
        // (Epic docs describe moving "Epic" folder into launcher install dir).
        var launcherInstallLocation = TryReadEpicLauncherInstallLocationFromRegistry();
        if (!string.IsNullOrWhiteSpace(launcherInstallLocation))
        {
            // Common PCB mode layout: <LauncherInstall>\Epic\EpicGamesLauncher\Data\Manifests
            var candidate = Path.Combine(launcherInstallLocation!, "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (Directory.Exists(candidate))
                return candidate;

            // Some installs: <LauncherInstall>\EpicGamesLauncher\Data\Manifests (less common, but cheap to check)
            candidate = Path.Combine(launcherInstallLocation!, "EpicGamesLauncher", "Data", "Manifests");
            if (Directory.Exists(candidate))
                return candidate;
        }

        // Not found: do not hard-fail. Scanner can still return a friendly issue.
        issues.Add(new ScanIssue
        {
            Code = "EPIC_MANIFEST_DIR_NOT_FOUND",
            Message = "Epic manifests folder was not found. Epic installs may not be discoverable on this machine.",
            StoreKey = StoreKeys.Epic
        });

        return null;
    }

    public static string? DiscoverLauncherInstalledDatPath(string? manifestsDir, List<ScanIssue> issues)
    {
        // If we found manifestsDir, LauncherInstalled.dat may be nearby:
        // ProgramData\Epic\UnrealEngineLauncher\LauncherInstalled.dat is common.
        if (File.Exists(DefaultLauncherInstalledDat))
            return DefaultLauncherInstalledDat;

        // If manifestsDir is known, try walking up: ...\EpicGamesLauncher\Data\Manifests
        // -> ...\EpicGamesLauncher\Data
        // -> ...\EpicGamesLauncher
        // -> ...\Epic
        // then append UnrealEngineLauncher\LauncherInstalled.dat
        try
        {
            if (!string.IsNullOrWhiteSpace(manifestsDir))
            {
                var d = new DirectoryInfo(manifestsDir!);
                // Manifests -> Data -> EpicGamesLauncher -> (Epic root)
                var epicRoot = d.Parent?.Parent?.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(epicRoot))
                {
                    var candidate = Path.Combine(epicRoot!, "UnrealEngineLauncher", "LauncherInstalled.dat");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "EPIC_LAUNCHERINSTALLED_DISCOVERY_FAILED",
                Message = "Failed while attempting to discover LauncherInstalled.dat path.",
                StoreKey = StoreKeys.Epic,
                Exception = ToExceptionInfo(ex)
            });
        }

        // Not fatal.
        return null;
    }

    private static IEnumerable<GameEntry> ParseItemManifests(string manifestsDir, List<ScanIssue> issues)
    {
        var files = Directory.EnumerateFiles(manifestsDir, "*.item", SearchOption.TopDirectoryOnly).ToList();
        var list = new List<GameEntry>();

        foreach (var f in files)
        {
            try
            {
                var json = File.ReadAllText(f);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var appName = GetString(root, "AppName");
                var installLocation = GetString(root, "InstallLocation");
                var displayName = GetString(root, "DisplayName") ?? GetString(root, "AppName") ?? "(unknown)";

                if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(installLocation))
                {
                    issues.Add(new ScanIssue
                    {
                        Code = "EPIC_ITEM_MISSING_REQUIRED_FIELDS",
                        Message = $"Epic .item manifest missing required fields (AppName/InstallLocation): '{Path.GetFileName(f)}'.",
                        StoreKey = StoreKeys.Epic,
                        Path = f
                    });
                    continue;
                }

                var launchExe = GetString(root, "LaunchExecutable");
                var appVersion = GetString(root, "AppVersionString");
                var catalogNs = GetString(root, "CatalogNamespace");
                var catalogItemId = GetString(root, "CatalogItemId");

                // Tags
                var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                tags.Add("epic");
                var isApplication = GetBool(root, "bIsApplication");
                if (isApplication == true) tags.Add("application");
                var incomplete = GetBool(root, "bIsIncompleteInstall");
                if (incomplete == true) tags.Add("incomplete");

                // AppCategories: ["public","games","applications",...]
                foreach (var c in GetStringArray(root, "AppCategories"))
                    tags.Add(c);

                // Store metadata (keep it flat, like Steam/Xbox)
                var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ManifestPath"] = f,
                    ["FormatVersion"] = GetInt(root, "FormatVersion")?.ToString() ?? "0",
                    ["ManifestLocation"] = GetString(root, "ManifestLocation") ?? "",
                    ["StagingLocation"] = GetString(root, "StagingLocation") ?? "",
                    ["InstallLocation"] = installLocation!
                };

                if (!string.IsNullOrWhiteSpace(launchExe)) meta["LaunchExecutable"] = launchExe!;
                if (!string.IsNullOrWhiteSpace(appVersion)) meta["AppVersionString"] = appVersion!;
                if (!string.IsNullOrWhiteSpace(catalogNs)) meta["CatalogNamespace"] = catalogNs!;
                if (!string.IsNullOrWhiteSpace(catalogItemId)) meta["CatalogItemId"] = catalogItemId!;

                list.Add(new GameEntry(
                    AppName: appName!,
                    DisplayName: displayName!,
                    InstallLocation: installLocation!,
                    LaunchExecutable: launchExe,
                    AppVersion: appVersion,
                    SourcePath: f,
                    SourceKind: "manifest-item",
                    CatalogNamespace: catalogNs,
                    CatalogItemId: catalogItemId,
                    DerivedTags: tags,
                    StoreMetadata: meta
                ));
            }
            catch (JsonException jex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "EPIC_ITEM_JSON_INVALID",
                    Message = $"Epic .item manifest JSON is invalid: '{Path.GetFileName(f)}'.",
                    StoreKey = StoreKeys.Epic,
                    Path = f,
                    Exception = ToExceptionInfo(jex)
                });
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "EPIC_ITEM_PARSE_FAILED",
                    Message = $"Failed parsing Epic .item manifest: '{Path.GetFileName(f)}'.",
                    StoreKey = StoreKeys.Epic,
                    Path = f,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        return list;
    }

    private static IEnumerable<GameEntry> ParseLauncherInstalledDat(string launcherInstalledPath, List<ScanIssue> issues)
    {
        var list = new List<GameEntry>();

        var json = File.ReadAllText(launcherInstalledPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("InstallationList", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ScanIssue
            {
                Code = "EPIC_LAUNCHERINSTALLED_NO_LIST",
                Message = "LauncherInstalled.dat did not contain an InstallationList array.",
                StoreKey = StoreKeys.Epic,
                Path = launcherInstalledPath
            });
            return list;
        }

        foreach (var e in arr.EnumerateArray())
        {
            var appName = GetString(e, "AppName");
            var installLocation = GetString(e, "InstallLocation");

            if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(installLocation))
                continue;

            var displayName = GetString(e, "ArtifactId") ?? appName!;
            var appVersion = GetString(e, "AppVersion");

            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "epic", "launcherinstalled" };

            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LauncherInstalledDat"] = launcherInstalledPath,
                ["InstallLocation"] = installLocation!
            };

            // Common keys seen for UE + games
            CopyIfPresent(e, meta, "NamespaceId");
            CopyIfPresent(e, meta, "ItemId");
            CopyIfPresent(e, meta, "ArtifactId");
            CopyIfPresent(e, meta, "AppVersion");

            list.Add(new GameEntry(
                AppName: appName!,
                DisplayName: displayName!,
                InstallLocation: installLocation!,
                LaunchExecutable: null,
                AppVersion: appVersion,
                SourcePath: launcherInstalledPath,
                SourceKind: "launcherinstalled",
                CatalogNamespace: GetString(e, "NamespaceId"),
                CatalogItemId: GetString(e, "ItemId"),
                DerivedTags: tags,
                StoreMetadata: meta
            ));
        }

        return list;
    }

    private static void CopyIfPresent(JsonElement obj, Dictionary<string, string> meta, string key)
    {
        var v = GetString(obj, key);
        if (!string.IsNullOrWhiteSpace(v))
            meta[key] = v!;
    }

    private static string? TryReadEpicAppDataPathFromRegistry()
    {
        // Vortex wiki notes: AppDataPath under HKLM\SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher
        // We check HKLM/HKCU + WOW6432Node + non-WOW for robustness.
        var candidates = new (RegistryHive hive, string subKey, string valueName)[]
        {
            (RegistryHive.LocalMachine,  @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher", "AppDataPath"),
            (RegistryHive.LocalMachine,  @"SOFTWARE\Epic Games\EpicGamesLauncher",           "AppDataPath"),
            (RegistryHive.CurrentUser,   @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher", "AppDataPath"),
            (RegistryHive.CurrentUser,   @"SOFTWARE\Epic Games\EpicGamesLauncher",           "AppDataPath"),
        };

        foreach (var (hive, sub, val) in candidates)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(sub);
                var s = key?.GetValue(val) as string;
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static string? TryReadEpicLauncherInstallLocationFromRegistry()
    {
        // Not guaranteed, but cheap to check. Some installs store InstallLocation.
        var candidates = new (RegistryHive hive, string subKey, string valueName)[]
        {
            (RegistryHive.LocalMachine,  @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher", "InstallLocation"),
            (RegistryHive.LocalMachine,  @"SOFTWARE\Epic Games\EpicGamesLauncher",           "InstallLocation"),
            (RegistryHive.CurrentUser,   @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher", "InstallLocation"),
            (RegistryHive.CurrentUser,   @"SOFTWARE\Epic Games\EpicGamesLauncher",           "InstallLocation"),
        };

        foreach (var (hive, sub, val) in candidates)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(sub);
                var s = key?.GetValue(val) as string;
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static string? GetString(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var p))
            return null;

        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            _ => p.ToString()
        };
    }

    private static int? GetInt(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var p))
            return null;

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
            return i;

        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var j))
            return j;

        return null;
    }

    private static bool? GetBool(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var p))
            return null;

        if (p.ValueKind == JsonValueKind.True) return true;
        if (p.ValueKind == JsonValueKind.False) return false;

        if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var b))
            return b;

        return null;
    }

    private static IEnumerable<string> GetStringArray(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var p) || p.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var e in p.EnumerateArray())
        {
            if (e.ValueKind == JsonValueKind.String)
            {
                var s = e.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    yield return s!;
            }
        }
    }

    private static ExceptionInfo ToExceptionInfo(Exception ex) => new ExceptionInfo
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };
}
