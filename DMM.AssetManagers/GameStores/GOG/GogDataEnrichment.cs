using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;
using EnrichmentBase = global::DMM.AssetManagers.GameStores.Common.StoreDataEnrichmentBase;

namespace DMM.AssetManagers.GameStores.Gog;

public static class GogDataEnrichment
{
    public static async Task DoDataEnrichmentAsync(
        StoreScanContext context,
        IReadOnlyList<AppInstallSnapshot> apps,
        List<ScanIssue> issues,
        CancellationToken ct = default)
    {
        if (apps is null || apps.Count == 0) return;

        var targets = apps
            .Where(a => a?.Id?.StoreKey != null
                        && string.Equals(a.Id.StoreKey, StoreKeys.Gog, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(a.Id.StoreAppId)
                        && a.Id.StoreAppId.All(char.IsDigit))
            .ToList();

        if (targets.Count == 0) return;

        var cacheRoot = EnrichmentBase.ResolveCacheRoot(StoreKeys.Gog);
        var assetsRoot = Path.Combine(cacheRoot, "assets");
        var responsesRoot = Path.Combine(cacheRoot, "responses");

        Directory.CreateDirectory(assetsRoot);
        Directory.CreateDirectory(responsesRoot);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DevModManager/1.0 (+https://github.com/ZeeOgre/DevModManager)");

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
                    lock (issues)
                    {
                        issues.Add(new ScanIssue
                        {
                            Code = "GOG_ENRICH_FAILED",
                            Message = $"GOG enrichment failed for product {snap.Id.StoreAppId}.",
                            StoreKey = StoreKeys.Gog,
                            AppKey = snap.Id.StoreAppId,
                            Exception = EnrichmentBase.ToExceptionInfo(ex)
                        });
                    }
                }
                finally
                {
                    gate.Release();
                }
            }, ct));
        }

        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch { /* per-task errors already captured */ }
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
        var productId = snap.Id.StoreAppId!.Trim();
        var apiUrl = $"https://api.gog.com/v2/games/{productId}";

        EnrichmentBase.SetIfMissing(snap.StoreMetadata, "GogApiUrl", apiUrl);
        EnrichmentBase.SetIfMissing(snap.StoreMetadata, "WebsiteGogDbUrl", $"https://www.gogdb.org/product/{productId}#details");

        var json = await http.GetStringAsync(apiUrl, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return;

        var responsePath = Path.Combine(responsesRoot, $"{productId}_game.json");
        EnrichmentBase.TryWriteAllText(responsePath, json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var attrs = ResolveAttributesNode(root);
        ExtractMetadata(attrs, snap.StoreMetadata);

        var images = ExtractImages(root, attrs);

        if (!context.IncludeVisualAssets)
        {
            snap.StoreMetadata["GogEnrichedUtc"] = DateTime.UtcNow.ToString("O");
            return;
        }

        snap.VisualAssets ??= new AppVisualAssetsSnapshot { Additional = new List<NamedVisualAssetRef>() };
        snap.VisualAssets.Additional ??= new List<NamedVisualAssetRef>();

        await AddVisualIfMissingAsync(http, snap, productId, "Icon", PickImage(images, "icon"), assetsRoot, issues, ct).ConfigureAwait(false);
        await AddVisualIfMissingAsync(http, snap, productId, "Logo", PickImage(images, "logo"), assetsRoot, issues, ct).ConfigureAwait(false);
        await AddVisualIfMissingAsync(http, snap, productId, "Background", PickImage(images, "backgroundImage"), assetsRoot, issues, ct).ConfigureAwait(false);
        await AddVisualIfMissingAsync(http, snap, productId, "BoxArt", PickImage(images, "boxArtImage"), assetsRoot, issues, ct).ConfigureAwait(false);

        if (images.TryGetValue("product_image_templated", out var templated) && !string.IsNullOrWhiteSpace(templated))
        {
            var candidate = templated.Replace("{formatter}", "product_630", StringComparison.Ordinal);
            await AddVisualIfMissingAsync(http, snap, productId, "ProductImage", candidate, assetsRoot, issues, ct).ConfigureAwait(false);
        }

        snap.StoreMetadata["GogEnrichedUtc"] = DateTime.UtcNow.ToString("O");
    }

    private static JsonElement ResolveAttributesNode(JsonElement root)
    {
        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("attributes", out var attrs))
            return attrs;

        if (root.TryGetProperty("_embedded", out var emb)
            && emb.TryGetProperty("product", out var prod)
            && prod.TryGetProperty("_links", out var links))
            return links;

        return root;
    }

    private static void ExtractMetadata(JsonElement attrs, IDictionary<string, string> metadata)
    {
        if (attrs.ValueKind != JsonValueKind.Object) return;

        if (attrs.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            EnrichmentBase.SetIfMissing(metadata, "GogApiName", nameEl.GetString());

        if (attrs.TryGetProperty("version", out var versionEl)
            && (versionEl.ValueKind == JsonValueKind.String || versionEl.ValueKind == JsonValueKind.Number))
            EnrichmentBase.SetIfMissing(metadata, "GogApiVersion", versionEl.ToString());

        if (attrs.TryGetProperty("playTasks", out var playTasks) && playTasks.ValueKind == JsonValueKind.Array)
        {
            foreach (var pt in playTasks.EnumerateArray())
            {
                if (pt.TryGetProperty("isPrimary", out var isPrimary)
                    && isPrimary.ValueKind == JsonValueKind.True
                    && pt.TryGetProperty("path", out var pathEl)
                    && pathEl.ValueKind == JsonValueKind.String)
                {
                    EnrichmentBase.SetIfMissing(metadata, "GogApiPlayTask:PrimaryPath", pathEl.GetString());
                    break;
                }
            }
        }
    }

    private static Dictionary<string, string> ExtractImages(JsonElement root, JsonElement attrs)
    {
        var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("_links", out var topLinks) && topLinks.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in topLinks.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Object
                    && p.Value.TryGetProperty("href", out var href)
                    && href.ValueKind == JsonValueKind.String)
                {
                    images[p.Name] = href.GetString()!;
                }
            }
        }

        if (root.TryGetProperty("data", out var data)
            && data.TryGetProperty("_embedded", out var embedded)
            && embedded.ValueKind == JsonValueKind.Object
            && embedded.TryGetProperty("product", out var product)
            && product.ValueKind == JsonValueKind.Object)
        {
            if (product.TryGetProperty("_links", out var plinks)
                && plinks.ValueKind == JsonValueKind.Object
                && plinks.TryGetProperty("image", out var image)
                && image.ValueKind == JsonValueKind.Object
                && image.TryGetProperty("href", out var href)
                && href.ValueKind == JsonValueKind.String)
            {
                images["product_image_templated"] = href.GetString()!;
            }

            if (product.TryGetProperty("images", out var productImages) && productImages.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in productImages.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String)
                        images[p.Name] = p.Value.GetString()!;
                }
            }
        }

        if (attrs.ValueKind == JsonValueKind.Object
            && attrs.TryGetProperty("images", out var attrImages)
            && attrImages.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in attrImages.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String)
                    images[p.Name] = p.Value.GetString()!;
                else
                    images[p.Name] = p.Value.ToString()!;
            }
        }

        return images;
    }

    private static string? PickImage(Dictionary<string, string> images, string key)
        => images.TryGetValue(key, out var value) ? value : null;

    private static async Task AddVisualIfMissingAsync(
        HttpClient http,
        AppInstallSnapshot snap,
        string productId,
        string fileKind,
        string? url,
        string assetsRoot,
        List<ScanIssue> issues,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        if (string.Equals(fileKind, "Icon", StringComparison.OrdinalIgnoreCase) && snap.VisualAssets?.Icon is not null) return;
        if (string.Equals(fileKind, "Logo", StringComparison.OrdinalIgnoreCase) && snap.VisualAssets?.Logo is not null) return;
        if (string.Equals(fileKind, "Background", StringComparison.OrdinalIgnoreCase) && snap.VisualAssets?.Splash is not null) return;

        if (snap.VisualAssets?.Additional != null && snap.VisualAssets.Additional.Any(a => string.Equals(a.Kind, fileKind, StringComparison.OrdinalIgnoreCase)))
            return;

        EnrichmentBase.SetIfMissing(snap.StoreMetadata, $"GogVisualUrl:{fileKind}", url);

        var originalName = EnrichmentBase.ExtractFilenameFromUrl(url);
        if (string.IsNullOrWhiteSpace(originalName))
            originalName = $"{fileKind}.jpg";

        var localName = EnrichmentBase.SanitizeFilename($"{productId}_{fileKind}_{originalName}");
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

            var asset = new VisualAssetRef { Uri = url, FilePath = localPath };
            if (string.Equals(fileKind, "Icon", StringComparison.OrdinalIgnoreCase))
                snap.VisualAssets = snap.VisualAssets! with { Icon = asset };
            else if (string.Equals(fileKind, "Logo", StringComparison.OrdinalIgnoreCase))
                snap.VisualAssets = snap.VisualAssets! with { Logo = asset };
            else if (string.Equals(fileKind, "Background", StringComparison.OrdinalIgnoreCase))
                snap.VisualAssets = snap.VisualAssets! with { Splash = asset };
            else
                snap.VisualAssets!.Additional.Add(new NamedVisualAssetRef { Kind = fileKind, Asset = asset });
        }
        catch (Exception ex)
        {
            lock (issues)
            {
                issues.Add(new ScanIssue
                {
                    Code = "GOG_VISUAL_DOWNLOAD_FAILED",
                    Message = $"Failed downloading GOG visual {fileKind} for product {productId}.",
                    StoreKey = StoreKeys.Gog,
                    AppKey = productId,
                    Exception = EnrichmentBase.ToExceptionInfo(ex)
                });
            }
        }
    }
}
