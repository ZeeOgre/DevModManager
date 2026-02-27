using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Common;

public static class StoreDataEnrichmentBase
{
    private const string BaseGameManifestPathKey = "BaseGameManifestPath";
    private const string BaseGameManifestFileCountKey = "BaseGameManifestFileCount";
    private const string BaseGameManifestTotalBytesKey = "BaseGameManifestTotalBytes";
    private const string BaseGameManifestGeneratedUtcKey = "BaseGameManifestGeneratedUtc";

    // Intention-revealing metadata helpers
    public static void SetIfMissing(IDictionary<string, string> meta, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!meta.ContainsKey(key))
            meta[key] = value.Trim();
    }

    public static void SetAlways(IDictionary<string, string> meta, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        meta[key] = value.Trim();
    }

    // Standard enrichment timestamps (cross-store invariant)
    public const string EnrichedFirstUtcKey = "EnrichedFirstUtc";
    public const string EnrichedLastUtcKey = "EnrichedLastUtc";

    public static void StampEnrichmentUtc(IDictionary<string, string> meta)
    {
        var now = DateTime.UtcNow.ToString("O");

        if (!meta.ContainsKey(EnrichedFirstUtcKey))
            meta[EnrichedFirstUtcKey] = now;

        meta[EnrichedLastUtcKey] = now;
    }

    public static void TryWriteAllText(string path, string text)
    {
        try { File.WriteAllText(path, text); }
        catch { /* best-effort */ }
    }

    public static string ResolveCacheRoot(string storeKey)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "ZeeOgre", "DevModManager", "Cache", "GameStore", storeKey.ToLowerInvariant());
    }

    public static string ExtractFilenameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return Path.GetFileName(uri.AbsolutePath);
        }
        catch
        {
            var q = url.IndexOf('?', StringComparison.Ordinal);
            var clean = q >= 0 ? url[..q] : url;
            var slash = clean.LastIndexOf('/');
            return slash >= 0 ? clean[(slash + 1)..] : clean;
        }
    }

    public static string SanitizeFilename(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    public static ExceptionInfo ToExceptionInfo(Exception ex) => new()
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };

    public static void EnrichWithBaseGameFileManifest(
        string storeKey,
        AppInstallSnapshot app,
        List<ScanIssue> issues)
    {
        // Base-game manifests should only be generated for installed game entries.
        if (app.InstallState != InstallState.Installed)
            return;

        if (app.Tags.Contains("tool", StringComparer.OrdinalIgnoreCase))
            return;

        if (app.InstallFolders?.InstallFolder?.Path is not { Length: > 0 } installRoot)
            return;

        if (!Directory.Exists(installRoot))
            return;

        try
        {
            var files = Directory
                .EnumerateFiles(
                    installRoot,
                    "*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true
                    })
                .Select(path =>
                {
                    var fileInfo = new FileInfo(path);
                    return new BaseGameFileManifestEntry
                    {
                        RelativePath = Path.GetRelativePath(installRoot, path),
                        SizeBytes = fileInfo.Length,
                        LastWriteUtc = fileInfo.LastWriteTimeUtc
                    };
                })
                .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var totalBytes = files.Sum(f => f.SizeBytes);
            var generatedUtc = DateTime.UtcNow;

            var cacheRoot = ResolveCacheRoot(storeKey);
            var manifestsRoot = Path.Combine(cacheRoot, "manifests");
            Directory.CreateDirectory(manifestsRoot);

            var appIdPart = SanitizeFilename(app.Id.StoreAppId);
            var manifestPath = Path.Combine(manifestsRoot, $"{appIdPart}_base-game-files.json");

            var payload = new BaseGameFileManifest
            {
                StoreKey = storeKey,
                StoreAppId = app.Id.StoreAppId,
                DisplayName = app.DisplayName,
                InstallRoot = installRoot,
                GeneratedUtc = generatedUtc,
                FileCount = files.Count,
                TotalBytes = totalBytes,
                Files = files
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestPath, json);

            SetAlways(app.StoreMetadata, BaseGameManifestPathKey, manifestPath);
            SetAlways(app.StoreMetadata, BaseGameManifestFileCountKey, files.Count.ToString());
            SetAlways(app.StoreMetadata, BaseGameManifestTotalBytesKey, totalBytes.ToString());
            SetAlways(app.StoreMetadata, BaseGameManifestGeneratedUtcKey, generatedUtc.ToString("O"));
            StampEnrichmentUtc(app.StoreMetadata);
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "BASE_GAME_FILE_MANIFEST_FAILED",
                Message = $"Failed to build base game file manifest for '{app.DisplayName}'.",
                StoreKey = storeKey,
                AppKey = app.Id.StoreAppId,
                Path = installRoot,
                Exception = ToExceptionInfo(ex)
            });
        }
    }

    public static void EnrichWithBaseGameFileManifests(
        string storeKey,
        IReadOnlyList<AppInstallSnapshot> apps,
        List<ScanIssue> issues)
    {
        foreach (var app in apps)
            EnrichWithBaseGameFileManifest(storeKey, app, issues);
    }

    private sealed record BaseGameFileManifest
    {
        public required string StoreKey { get; init; }
        public required string StoreAppId { get; init; }
        public required string DisplayName { get; init; }
        public required string InstallRoot { get; init; }
        public required DateTime GeneratedUtc { get; init; }
        public required int FileCount { get; init; }
        public required long TotalBytes { get; init; }
        public required IReadOnlyList<BaseGameFileManifestEntry> Files { get; init; }
    }

    private sealed record BaseGameFileManifestEntry
    {
        public required string RelativePath { get; init; }
        public required long SizeBytes { get; init; }
        public required DateTime LastWriteUtc { get; init; }
    }
}
