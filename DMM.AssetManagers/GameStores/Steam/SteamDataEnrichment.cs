using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;
using EnrichmentBase = DMM.AssetManagers.GameStores.Common.StoreDataEnrichmentBase;

namespace DMM.AssetManagers.GameStores.Steam;

public static class SteamDataEnrichment
{
    // Deterministic store URIs (no slugify)
    public static string BuildSteamStoreUri(string appId) => $"https://store.steampowered.com/app/{appId}/";
    public static string BuildSteamCommunityUri(string appId) => $"https://steamcommunity.com/app/{appId}/";

    /// <summary>
    /// Enrich Steam app snapshots using store.steampowered.com "appdetails".
    /// - Idempotent: fills missing metadata and visuals, never overwrites existing.
    /// - Best-effort: records issues, does not throw out of the loop.
    /// - Optional downloads: governed by context.IncludeVisualAssets.
    /// </summary>
    public static async Task DoDataEnrichmentAsync(
        StoreScanContext context,
        IReadOnlyList<AppInstallSnapshot> apps,
        List<ScanIssue> issues,
        CancellationToken ct = default)
    {
        if (apps is null || apps.Count == 0) return;

        var targets = apps
            .Where(a => a?.Id?.StoreKey != null
                        && string.Equals(a.Id.StoreKey, StoreKeys.Steam, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(a.Id.StoreAppId)
                        && a.Id.StoreAppId.All(char.IsDigit))
            .ToList();

        if (targets.Count == 0) return;

        var cacheRoot = EnrichmentBase.ResolveCacheRoot(StoreKeys.Steam);
        var assetsRoot = Path.Combine(cacheRoot, "assets");
        var responsesRoot = Path.Combine(cacheRoot, "responses");

        Directory.CreateDirectory(assetsRoot);
        Directory.CreateDirectory(responsesRoot);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DevModManager/1.0 (+https://github.com/ZeeOgre/DevModManager)");

        // keep it polite
        using var gate = new SemaphoreSlim(6, 6);
        var tasks = new List<Task>(targets.Count);

        foreach (var snap in targets)
        {
            tasks.Add(Task.Run(async () =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await EnrichOneAsync(http, context, snap, issues, assetsRoot, responsesRoot, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    issues.Add(new ScanIssue
                    {
                        Code = "STEAM_ENRICH_FAILED",
                        Message = $"Steam enrichment failed for appid {snap.Id.StoreAppId}.",
                        StoreKey = StoreKeys.Steam,
                        AppKey = snap.Id.StoreAppId,
                        Exception = EnrichmentBase.ToExceptionInfo(ex)
                    });
                }
                finally
                {
                    gate.Release();
                }
            }, ct));
        }

        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch { /* per-task error already recorded */ }
    }

    private static async Task EnrichOneAsync(
        HttpClient http,
        StoreScanContext context,
        AppInstallSnapshot snap,
        List<ScanIssue> issues,
        string assetsRoot,
        string responsesRoot,
        CancellationToken ct)
    {
        var appId = snap.Id.StoreAppId!.Trim();

        // StoreMetadata has a default dictionary initializer; assume it's present.

        // deterministic links (only set if missing)
        EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamStoreUri", BuildSteamStoreUri(appId));
        EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamCommunityUri", BuildSteamCommunityUri(appId));
        


        // Keep defaults consistent for caching
        const string cc = "US";
        const string lang = "english";

        var apiUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc={cc}&l={lang}";
        EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamAppDetailsUrl", apiUrl);

        var json = await http.GetStringAsync(apiUrl, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return;

        // Optional: store raw response on disk (not DB)
        var responsePath = Path.Combine(responsesRoot, $"{appId}_appdetails_cc-{cc}_l-{lang}.json");
        EnrichmentBase.TryWriteAllText(responsePath, json);

        Dictionary<string, Envelope>? dict;
        try
        {
            dict = JsonSerializer.Deserialize<Dictionary<string, Envelope>>(json, JsonOpts);
        }
        catch (JsonException)
        {
            issues.Add(new ScanIssue
            {
                Code = "STEAM_APPDETAILS_JSON_INVALID",
                Message = $"Steam appdetails returned invalid JSON for appid {appId}.",
                StoreKey = StoreKeys.Steam,
                AppKey = appId
            });
            return;
        }

        if (dict is null || !dict.TryGetValue(appId, out var env) || env is null) return;
        if (!env.Success || env.Data is null) return;

        var d = env.Data;

        // ---- Small metadata set (idempotent)
        EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamType", d.Type);
        EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamWebsite", d.Website);
        EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamSupportedLanguages", d.SupportedLanguages);

        if (!string.IsNullOrWhiteSpace(d.ReleaseDate?.Date))
            EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamReleaseDate", d.ReleaseDate!.Date);

        if (d.Developers is { Length: > 0 })
            EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamDevelopers", string.Join(" | ", d.Developers));

        if (d.Publishers is { Length: > 0 })
            EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamPublishers", string.Join(" | ", d.Publishers));

        if (d.Dlc is { Length: > 0 })
            EnrichmentBase.SetIfMissing(snap.StoreMetadata, "SteamDlcAppIds", string.Join(",", d.Dlc));

        // ---- Visual enrichment
        if (!context.IncludeVisualAssets) return;

        snap.VisualAssets ??= new AppVisualAssetsSnapshot { Additional = new List<NamedVisualAssetRef>() };
        snap.VisualAssets.Additional ??= new List<NamedVisualAssetRef>();

        // If Steam librarycache already provided Icon/Logo/Splash, leave them alone.
        // Add missing high-value visuals as "Additional" kinds.
        await AddVisualIfMissingAsync(http, snap, appId, "Header", d.HeaderImage, assetsRoot, issues, ct).ConfigureAwait(false);
        await AddVisualIfMissingAsync(http, snap, appId, "Capsule", d.CapsuleImageV5 ?? d.CapsuleImage, assetsRoot, issues, ct).ConfigureAwait(false);
        await AddVisualIfMissingAsync(http, snap, appId, "Background", d.BackgroundRaw ?? d.Background, assetsRoot, issues, ct).ConfigureAwait(false);
        snap.StoreMetadata["SteamEnrichedUtc"] = DateTime.UtcNow.ToString("O");

    }

    private static async Task AddVisualIfMissingAsync(
        HttpClient http,
        AppInstallSnapshot snap,
        string appId,
        string fileKind,
        string? url,
        string assetsRoot,
        List<ScanIssue> issues,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // Idempotent: if an Additional entry already exists with this kind, skip
        if (snap.VisualAssets?.Additional != null)
        {
            foreach (var a in snap.VisualAssets.Additional)
            {
                if (string.Equals(a.Kind, fileKind, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        // Also store the remote URL as audit/debug pointer (idempotent)
        EnrichmentBase.SetIfMissing(snap.StoreMetadata, $"SteamVisualUrl:{fileKind}", url);

        // Determine original filename (strip querystring), then prefix with {appid}_{FileKind}_
        var originalName = EnrichmentBase.ExtractFilenameFromUrl(url);
        if (string.IsNullOrWhiteSpace(originalName))
        {
            // Very rare for Steam, but keep safe.
            originalName = $"{fileKind}.jpg";
        }

        var localName = $"{appId}_{fileKind}_{originalName}";
        localName = EnrichmentBase.SanitizeFilename(localName);

        var localPath = Path.Combine(assetsRoot, localName);

        try
        {
            if (!File.Exists(localPath))
            {
                using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                await using var fs = File.Create(localPath);
                await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
            }

            snap.VisualAssets ??= new AppVisualAssetsSnapshot { Additional = new List<NamedVisualAssetRef>() };
            snap.VisualAssets.Additional ??= new List<NamedVisualAssetRef>();

            snap.VisualAssets.Additional.Add(new NamedVisualAssetRef
            {
                Kind = fileKind,
                Asset = new VisualAssetRef { FilePath = localPath }
            });
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "STEAM_VISUAL_DOWNLOAD_FAILED",
                Message = $"Failed downloading Steam visual {fileKind} for appid {appId}.",
                StoreKey = StoreKeys.Steam,
                AppKey = appId,
                Exception = EnrichmentBase.ToExceptionInfo(ex)
            });
        }
        StoreDataEnrichmentBase.StampEnrichmentUtc(snap.StoreMetadata);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // appdetails response is: { "22380": { "success": true, "data": { ... } } }
    private sealed class Envelope
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public Data? Data { get; set; }
    }

    private sealed class Data
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("steam_appid")]
        public uint SteamAppId { get; set; }

        [JsonPropertyName("supported_languages")]
        public string? SupportedLanguages { get; set; }

        [JsonPropertyName("header_image")]
        public string? HeaderImage { get; set; }

        [JsonPropertyName("capsule_image")]
        public string? CapsuleImage { get; set; }

        [JsonPropertyName("capsule_imagev5")]
        public string? CapsuleImageV5 { get; set; }

        [JsonPropertyName("background")]
        public string? Background { get; set; }

        [JsonPropertyName("background_raw")]
        public string? BackgroundRaw { get; set; }

        [JsonPropertyName("website")]
        public string? Website { get; set; }

        [JsonPropertyName("developers")]
        public string[]? Developers { get; set; }

        [JsonPropertyName("publishers")]
        public string[]? Publishers { get; set; }

        [JsonPropertyName("dlc")]
        public uint[]? Dlc { get; set; }

        [JsonPropertyName("release_date")]
        public ReleaseDate? ReleaseDate { get; set; }
    }

    private sealed class ReleaseDate
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }
    }
}
