using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Gog;

[SupportedOSPlatform("windows")]
public sealed class GogInstallScanner : IStoreInstallScanner
{
    public string StoreKey => StoreKeys.Gog;

    // Keep synchronous API for callers that rely on IStoreInstallScanner.
    // New async implementation below can be awaited by newer callers.
    public StoreScanResult Scan(StoreScanContext context)
        => ScanAsync(context).GetAwaiter().GetResult();

    // Async scan: performs catalog build synchronously (same as before), then
    // concurrently enriches each app from the GOG API and (optionally) downloads visuals.
    public async Task<StoreScanResult> ScanAsync(StoreScanContext context)
    {
        var issues = new List<ScanIssue>();
        var apps = new List<AppInstallSnapshot>();

        IReadOnlyList<object> roots = context.Roots;

        // Build catalog (unchanged, synchronous work)
        List<GogGameCatalog.GameEntry> catalog;
        try
        {
            catalog = GogGameCatalog.BuildCatalog(roots, issues);
        }
        catch (Exception ex)
        {
            catalog = new List<GogGameCatalog.GameEntry>();
            issues.Add(new ScanIssue
            {
                Code = "GOG_CATALOG_BUILD_FAILED",
                Message = "Failed to build GOG catalog from roots.",
                StoreKey = StoreKey,
                Exception = ToExceptionInfo(ex)
            });

            return new StoreScanResult { StoreKey = StoreKey, Apps = apps, Issues = issues };
        }

        // Map base snapshot for each entry and schedule enrichment tasks.
        var enrichTasks = new List<Task>();
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DMM/1.0 (+https://example)");

        foreach (var e in catalog)
        {
            try
            {
                var snapshot = MapGameEntryBase(e);
                apps.Add(snapshot);

                // Only attempt API enrichment when we have a numeric product id.
                if (!string.IsNullOrWhiteSpace(e.ProductId) && e.ProductId.All(char.IsDigit))
                {
                    // Start enrichment concurrently. includeVisuals via context.IncludeVisualAssets
                    enrichTasks.Add(EnrichSnapshotFromGogApiAsync(http, e.ProductId, snapshot, context.IncludeVisualAssets));
                }

                // Tools enrichment is not necessary (no API mapping per tool).
                foreach (var t in e.Tools)
                {
                    var toolSnap = MapToolEntryBase(e, t);
                    apps.Add(toolSnap);
                }
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

        // Await all enrichment tasks but do not let a single failure throw — collect issues.
        try
        {
            await Task.WhenAll(enrichTasks).ConfigureAwait(false);
        }
        catch
        {
            // Individual enrichment tasks swallow their exceptions; this is defensive.
        }

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

    // Build minimal snapshot (no blocking network calls).
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
            VisualAssets = null, // will be filled by enrichment
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
        var toolInstallFolder = !string.IsNullOrWhiteSpace(tool.ExeFullPath)
            ? SafeGetDirectoryName(tool.ExeFullPath!)
            : parent.InstallFolder;

        var folders = new InstallFoldersSnapshot
        {
            InstallFolder = !string.IsNullOrWhiteSpace(toolInstallFolder)
                ? new FolderRef { Path = toolInstallFolder! }
                : null,
            ContentFolder = null,
            DataFolder = null,
            AdditionalFolders = Array.Empty<NamedFolderRef>()
        };

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductId"] = parent.ProductId,
            ["ParentProductId"] = parent.ProductId,
            ["SourceKind"] = parent.SourceKind,
            ["SourcePath"] = tool.SourcePath
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

    // Enrich snapshot from GOG API (fill StoreMetadata and VisualAssets). Best-effort, non-throwing.
    private static async Task EnrichSnapshotFromGogApiAsync(HttpClient http, string productId, AppInstallSnapshot snapshot, bool downloadVisuals)
    {
        var apiUrl = $"https://api.gog.com/v2/games/{productId}";
        try
        {
            var json = await http.GetStringAsync(apiUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            snapshot.StoreMetadata["GogApiUrl"] = apiUrl;

            // images and links live under data.attributes or data._embedded.product etc.
            JsonElement attrs;
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("attributes", out var a))
                attrs = a;
            else if (root.TryGetProperty("_embedded", out var emb) && emb.TryGetProperty("product", out var prod) && prod.TryGetProperty("_links", out var prodLinks))
                attrs = prodLinks;
            else
                attrs = root;

            // Try to extract image links from the common places.
            var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // _links (top-level)
            if (root.TryGetProperty("_links", out var topLinks) && topLinks.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in topLinks.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("href", out var href) && href.ValueKind == JsonValueKind.String)
                        images[p.Name] = href.GetString()!;
                }
            }

            // data._embedded.product._links.image templated URL (supports {formatter})
            if (root.TryGetProperty("data", out var data2) && data2.TryGetProperty("_embedded", out var embedded) && embedded.ValueKind == JsonValueKind.Object)
            {
                if (embedded.TryGetProperty("product", out var product) && product.ValueKind == JsonValueKind.Object)
                {
                    if (product.TryGetProperty("_links", out var plinks) && plinks.ValueKind == JsonValueKind.Object)
                    {
                        if (plinks.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object && image.TryGetProperty("href", out var href))
                        {
                            // templated URL, store the templated form under a key.
                            images["product_image_templated"] = href.GetString()!;
                        }
                    }

                    // also check product._embedded or product.images
                    if (product.TryGetProperty("images", out var imgObj) && imgObj.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in imgObj.EnumerateObject())
                        {
                            if (p.Value.ValueKind == JsonValueKind.String)
                                images[p.Name] = p.Value.GetString()!;
                        }
                    }
                }
            }

            // attributes.images common shape
            if (attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty("images", out var attrImages) && attrImages.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in attrImages.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String)
                        images[p.Name] = p.Value.GetString()!;
                    else
                        images[p.Name] = p.Value.ToString()!;
                }
            }

            // gather playTasks for executable hints
            if (attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty("playTasks", out var playTasks) && playTasks.ValueKind == JsonValueKind.Array)
            {
                foreach (var pt in playTasks.EnumerateArray())
                {
                    if (pt.TryGetProperty("isPrimary", out var isP) && isP.ValueKind == JsonValueKind.True && pt.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
                    {
                        snapshot.StoreMetadata["GogApiPlayTask:PrimaryPath"] = pathEl.GetString()!;
                        break;
                    }
                }
            }

            if (attrs.ValueKind == JsonValueKind.Object)
            {
                if (attrs.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                    snapshot.StoreMetadata["GogApiName"] = nameEl.GetString()!;
                if (attrs.TryGetProperty("version", out var verEl) && (verEl.ValueKind == JsonValueKind.String || verEl.ValueKind == JsonValueKind.Number))
                    snapshot.StoreMetadata["GogApiVersion"] = verEl.ToString()!;
            }

            // Add gogdb url
            snapshot.StoreMetadata["WebsiteGogDbUrl"] = $"https://www.gogdb.org/product/{productId}#details";

            // Construct visual assets from discovered image links.
            var visual = new AppVisualAssetsSnapshot
            {
                Icon = null,
                Logo = null,
                Splash = null,
                Additional = Array.Empty<NamedVisualAssetRef>()
            };

            var additional = new List<NamedVisualAssetRef>();

            // map common keys
            if (images.TryGetValue("icon", out var iconUrl)) visual = visual with { Icon = MakeVisualRef(iconUrl, productId, downloadVisuals) };
            if (images.TryGetValue("logo", out var logoUrl)) visual = visual with { Logo = MakeVisualRef(logoUrl, productId, downloadVisuals) };
            if (images.TryGetValue("backgroundImage", out var bgUrl)) visual = visual with { Splash = MakeVisualRef(bgUrl, productId, downloadVisuals) };
            if (images.TryGetValue("boxArtImage", out var boxUrl)) additional.Add(new NamedVisualAssetRef { Kind = "boxart", Asset = MakeVisualRef(boxUrl, productId, downloadVisuals) });
            if (images.TryGetValue("product_image_templated", out var templated))
            {
                // attempt to produce a reasonable product image URL (use first formatter)
                var candidate = templated.Replace("{formatter}", "product_630");
                additional.Add(new NamedVisualAssetRef { Kind = "product_image", Asset = MakeVisualRef(candidate, productId, downloadVisuals) });
            }

            // Also include any images found under other names.
            foreach (var kv in images)
            {
                if (kv.Key == "icon" || kv.Key == "logo" || kv.Key == "backgroundImage" || kv.Key == "boxArtImage" || kv.Key == "product_image_templated")
                    continue;
                additional.Add(new NamedVisualAssetRef { Kind = kv.Key, Asset = MakeVisualRef(kv.Value, productId, downloadVisuals) });
            }

            if (additional.Count > 0)
                visual = visual with { Additional = additional };

            // Only assign visuals if we have at least one asset
            if (visual.Icon is not null || visual.Logo is not null || visual.Splash is not null || visual.Additional.Count > 0)
                snapshot.VisualAssets = visual;
        }
        catch
        {
            // Best-effort; do not throw on enrichment failure.
        }
    }

    // Create VisualAssetRef with Uri set; optionally download and set FilePath.
    private static VisualAssetRef MakeVisualRef(string url, string productId, bool download)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null!; // caller will ignore nulls

        var v = new VisualAssetRef { Uri = url };

        if (!download)
            return v;

        try
        {
            // synchronous wrapper avoided here; enrichment call will be async if downloading needed.
            var local = DownloadImageForProductAsync(url, productId).GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(local))
            {
                v = v with { FilePath = local };
            }
        }
        catch
        {
            // swallow download errors
        }

        return v;
    }

    // Download image to local cache and return file path. Best-effort.
    private static async Task<string?> DownloadImageForProductAsync(string url, string productId)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DMM/1.0 (+https://example)");
            using var stream = await http.GetStreamAsync(url).ConfigureAwait(false);

            var cacheDir = Path.Combine(Path.GetTempPath(), "DMM", "gog", productId);
            Directory.CreateDirectory(cacheDir);

            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "image";

            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(ext))
                fileName += ".img";

            var dest = Path.Combine(cacheDir, fileName);

            // write stream
            using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fs).ConfigureAwait(false);

            return dest;
        }
        catch
        {
            return null;
        }
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
