using System;
using System.Collections.Generic;
using System.IO;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Common;

public static class StoreDataEnrichmentBase
{
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
}