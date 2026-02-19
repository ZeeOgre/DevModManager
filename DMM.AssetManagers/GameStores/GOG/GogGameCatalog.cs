using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Gog;


public static class GogGameCatalog
{
    public sealed record GameEntry(
        string ProductId,
        string DisplayName,
        string? InstallFolder,
        string? PrimaryExeName,
        string? PrimaryExeFullPath,
        IReadOnlyList<ToolEntry> Tools,
        string SourceKind,   // "galaxy-db" | "goggame-info"
        string SourcePath    // db path or .info path
    );

    public sealed record ToolEntry(
        string ToolKey,          // stable-ish: $"{ProductId}:{Kind}:{ExeName}"
        string DisplayName,      // e.g. "Skyrim - Creation Kit" (best-effort)
        string? ExeName,
        string? ExeFullPath,
        string SourcePath
    );

    private static readonly string DefaultGalaxyStorageDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GOG.com", "Galaxy", "storage");

    // -------------------------
    // Root handling
    // -------------------------
    private sealed record RootSplit(
        List<string> GalaxyDbOrStorageRoots,
        List<string> LibraryRoots
    );

    public static List<string> DiscoverDefaultRoots(List<ScanIssue> issues)
    {
        // Default “store-root” only. Library roots are user-provided.
        var roots = new List<string>();
        try
        {
            if (Directory.Exists(DefaultGalaxyStorageDir))
                roots.Add(DefaultGalaxyStorageDir);
        }
        catch (Exception ex)
        {
            issues.Add(new ScanIssue
            {
                Code = "GOG_ROOT_DISCOVERY_FAILED",
                Message = "Failed to discover default GOG Galaxy storage root under ProgramData.",
                StoreKey = StoreKeys.Gog,
                Exception = ToExceptionInfo(ex)
            });
        }

        return roots;
    }

    private static RootSplit SplitRoots(IReadOnlyList<object> roots)
    {
        var galaxy = new List<string>();
        var libs = new List<string>();

        foreach (var o in roots ?? Array.Empty<object>())
        {
            if (o is not string s) continue;
            s = s.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(s)) continue;

            if (File.Exists(s) &&
                (s.EndsWith("galaxy-2.0.db", StringComparison.OrdinalIgnoreCase) ||
                 s.EndsWith("galaxy.db", StringComparison.OrdinalIgnoreCase)))
            {
                galaxy.Add(s);
                continue;
            }

            if (!Directory.Exists(s))
                continue;

            var g2 = Path.Combine(s, "galaxy-2.0.db");
            var g1 = Path.Combine(s, "galaxy.db");

            if (File.Exists(g2) || File.Exists(g1) ||
                s.IndexOf(@"\GOG.com\Galaxy\storage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                galaxy.Add(s);
            }
            else
            {
                libs.Add(s);
            }
        }

        return new RootSplit(
            GalaxyDbOrStorageRoots: galaxy.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            LibraryRoots: libs.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        );
    }

    private static List<string> ResolveDbCandidates(IEnumerable<string> galaxyRoots)
    {
        var dbs = new List<string>();

        foreach (var r in galaxyRoots)
        {
            if (File.Exists(r))
            {
                dbs.Add(r);
                continue;
            }

            if (!Directory.Exists(r))
                continue;

            var g2 = Path.Combine(r, "galaxy-2.0.db");
            var g1 = Path.Combine(r, "galaxy.db");

            if (File.Exists(g2)) dbs.Add(g2);
            if (File.Exists(g1)) dbs.Add(g1);
        }

        return dbs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(p => p.EndsWith("galaxy-2.0.db", StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // -------------------------
    // Public entry point
    // -------------------------
    public static List<GameEntry> BuildCatalog(IReadOnlyList<object> roots, List<ScanIssue> issues)
    {
        var split = SplitRoots(roots);

        // If user gave nothing, use default Galaxy store-root
        if (split.GalaxyDbOrStorageRoots.Count == 0 && split.LibraryRoots.Count == 0)
        {
            var defaults = DiscoverDefaultRoots(issues);
            split = split with { GalaxyDbOrStorageRoots = defaults };
        }

        // 1) Galaxy DB entries (store-root)
        var dbEntries = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        var dbs = ResolveDbCandidates(split.GalaxyDbOrStorageRoots);

        foreach (var db in dbs)
        {
            try
            {
                foreach (var e in ReadInstalledProductsFromGalaxyDb(db, issues))
                {
                    // Prefer first DB that provides the entry
                    if (!dbEntries.ContainsKey(e.ProductId))
                        dbEntries[e.ProductId] = e;
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "GOG_DB_READ_FAILED",
                    Message = $"Failed to read GOG Galaxy DB: {db}",
                    StoreKey = StoreKeys.Gog,
                    Path = db,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        // 2) Library-root entries via goggame-*.info
        var infoEntries = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var lib in split.LibraryRoots)
        {
            try
            {
                foreach (var e in ScanLibraryForGogGameInfo(lib, issues))
                {
                    if (!infoEntries.ContainsKey(e.ProductId))
                        infoEntries[e.ProductId] = e;
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "GOG_LIBRARY_SCAN_FAILED",
                    Message = $"Failed scanning GOG library root for goggame-*.info: {lib}",
                    StoreKey = StoreKeys.Gog,
                    Path = lib,
                    Exception = ToExceptionInfo(ex)
                });
            }
        }

        // 3) Merge (DB wins; fill gaps from .info)
        var merged = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in infoEntries) merged[kv.Key] = kv.Value;
        foreach (var kv in dbEntries) merged[kv.Key] = MergePreferDb(kv.Value, merged.GetValueOrDefault(kv.Key));

        return merged.Values
            .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static GameEntry MergePreferDb(GameEntry db, GameEntry? info)
    {
        if (info is null) return db;

        // Fill install folder / exe if DB lacks them.
        var installFolder = db.InstallFolder ?? info.InstallFolder;
        var exeName = db.PrimaryExeName ?? info.PrimaryExeName;
        var exeFull = db.PrimaryExeFullPath ?? info.PrimaryExeFullPath;

        // Tools: union by ToolKey
        var tools = new Dictionary<string, ToolEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in info.Tools) tools[t.ToolKey] = t;
        foreach (var t in db.Tools) tools[t.ToolKey] = t;

        return db with
        {
            InstallFolder = installFolder,
            PrimaryExeName = exeName,
            PrimaryExeFullPath = exeFull,
            Tools = tools.Values.ToList()
        };
    }

    // -------------------------
    // Galaxy DB read (store-root)
    // -------------------------
    private static IEnumerable<GameEntry> ReadInstalledProductsFromGalaxyDb(string dbPath, List<ScanIssue> issues)
    {
        // Schema can vary; we best-effort:
        // - InstalledProducts.productId
        // - Title from GamePieces/GamePieceTypes originalTitle (when present)
        // - Primary installPath from ExecutableSupportFiles (when present)
        // This approach is consistent with known Galaxy DB querying patterns. :contentReference[oaicite:3]{index=3}

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        };

        using var conn = new SqliteConnection(csb.ToString());
        conn.Open();

        var tables = GetTableNames(conn);
        bool hasInstalledBase = tables.Contains("InstalledBaseProducts");


        if (!hasInstalledBase && !tables.Contains("InstalledProducts"))
        {
            issues.Add(new ScanIssue
            {
                Code = "GOG_DB_SCHEMA_UNEXPECTED",
                Message = "Galaxy DB missing InstalledBaseProducts/InstalledProducts; cannot read installed items.",
                StoreKey = StoreKeys.Gog,
                Path = dbPath
            });
            yield break;
        }


        bool hasPieces = tables.Contains("GamePieces") && tables.Contains("GamePieceTypes");
        bool hasExeSupport = tables.Contains("ExecutableSupportFiles");

        var sql = BuildBestDbQuery(hasPieces, hasExeSupport, hasInstalledBase);


        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var productId = reader.GetString(0);
            var title = reader.IsDBNull(1) ? productId : reader.GetString(1);

            string? installPath = reader.IsDBNull(2) ? null : reader.GetString(2);

            // installPath may be exe or folder; normalize:
            string? installFolder = null;
            string? exeName = null;
            string? exeFull = null;

            if (!string.IsNullOrWhiteSpace(installPath))
            {
                var p = installPath.Trim();

                if (p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    exeFull = p;
                    exeName = Path.GetFileName(p);
                    installFolder = SafeGetDirectoryName(p);
                }
                else
                {
                    installFolder = p;
                }
            }

            yield return new GameEntry(
                ProductId: productId,
                DisplayName: title,
                InstallFolder: installFolder,
                PrimaryExeName: exeName,
                PrimaryExeFullPath: exeFull,
                Tools: Array.Empty<ToolEntry>(),
                SourceKind: "galaxy-db",
                SourcePath: dbPath
            );
        }
    }

    private static string BuildBestDbQuery(bool hasPieces, bool hasExeSupport, bool hasInstalledBase)
    {
        var sb = new StringBuilder();

        // Prefer InstalledBaseProducts because it has deterministic installationPath
        // (your screenshot shows this clearly).
        if (hasInstalledBase)
        {
            sb.AppendLine("SELECT ibp.productId AS ProductId,");

            if (hasPieces)
            {
                sb.AppendLine("       COALESCE(");
                sb.AppendLine("         (");
                sb.AppendLine("           SELECT trim(trim(gp.value,'{\"\"title\"\":\"\"'), '\"\"}')");
                sb.AppendLine("           FROM GamePieces gp");
                sb.AppendLine("           JOIN GamePieceTypes gpt ON gp.gamePieceTypeId = gpt.id");
                sb.AppendLine("           WHERE gp.releaseKey = 'gog_' || ibp.productId");
                sb.AppendLine("             AND gpt.type = 'originalTitle'");
                sb.AppendLine("           LIMIT 1");
                sb.AppendLine("         ),");
                sb.AppendLine("         CAST(ibp.productId AS TEXT)");
                sb.AppendLine("       ) AS Title,");
            }
            else
            {
                sb.AppendLine("       CAST(ibp.productId AS TEXT) AS Title,");
            }

            sb.AppendLine("       ibp.installationPath AS InstallPath");
            sb.AppendLine("FROM InstalledBaseProducts ibp");
            sb.AppendLine("ORDER BY ibp.productId;");
            return sb.ToString();
        }

        // Fallback to the old InstalledProducts + ExecutableSupportFiles method
        sb.AppendLine("SELECT ip.productId AS ProductId,");

        if (hasPieces)
        {
            sb.AppendLine("       COALESCE(");
            sb.AppendLine("         (");
            sb.AppendLine("           SELECT trim(trim(gp.value,'{\"\"title\"\":\"\"'), '\"\"}')");
            sb.AppendLine("           FROM GamePieces gp");
            sb.AppendLine("           JOIN GamePieceTypes gpt ON gp.gamePieceTypeId = gpt.id");
            sb.AppendLine("           WHERE gp.releaseKey = 'gog_' || ip.productId");
            sb.AppendLine("             AND gpt.type = 'originalTitle'");
            sb.AppendLine("           LIMIT 1");
            sb.AppendLine("         ),");
            sb.AppendLine("         CAST(ip.productId AS TEXT)");
            sb.AppendLine("       ) AS Title,");
        }
        else
        {
            sb.AppendLine("       CAST(ip.productId AS TEXT) AS Title,");
        }

        if (hasExeSupport)
        {
            sb.AppendLine("       (SELECT MIN(esf.installPath) FROM ExecutableSupportFiles esf WHERE esf.productId = ip.productId) AS InstallPath");
        }
        else
        {
            sb.AppendLine("       NULL AS InstallPath");
        }

        sb.AppendLine("FROM InstalledProducts ip");
        sb.AppendLine("ORDER BY ip.productId;");

        return sb.ToString();
    }


    private static HashSet<string> GetTableNames(SqliteConnection conn)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasInstalledBase = tables.Contains("InstalledBaseProducts");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                tables.Add(reader.GetString(0));
        }

        return tables;
    }

    // -------------------------
    // goggame-*.info scan (library roots)
    // -------------------------
    private static IEnumerable<GameEntry> ScanLibraryForGogGameInfo(string libraryRoot, List<ScanIssue> issues)
    {
        if (!Directory.Exists(libraryRoot))
            yield break;

        // We do a bounded-depth scan to avoid crawling the entire drive.
        // Typical: <library>\<game>\goggame-<id>.info
        foreach (var infoPath in EnumerateFilesDepthLimited(libraryRoot, "goggame-*.info", maxDepth: 4))
        {
            GameEntry? entry = null;
            try
            {
                entry = ParseGogGameInfo(infoPath, issues);
            }
            catch (JsonException jex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "GOG_INFO_JSON_INVALID",
                    Message = $"Invalid JSON in '{Path.GetFileName(infoPath)}'.",
                    StoreKey = StoreKeys.Gog,
                    Path = infoPath,
                    Exception = ToExceptionInfo(jex)
                });
            }
            catch (Exception ex)
            {
                issues.Add(new ScanIssue
                {
                    Code = "GOG_INFO_PARSE_FAILED",
                    Message = $"Failed parsing '{Path.GetFileName(infoPath)}'.",
                    StoreKey = StoreKeys.Gog,
                    Path = infoPath,
                    Exception = ToExceptionInfo(ex)
                });
            }

            if (entry is not null)
                yield return entry;
        }
    }

    private static GameEntry? ParseGogGameInfo(string infoPath, List<ScanIssue> issues)
    {
        var file = Path.GetFileNameWithoutExtension(infoPath); // goggame-1207662443
        var productId = ExtractProductIdFromInfoFilename(file);
        if (string.IsNullOrWhiteSpace(productId))
            return null;

        var installFolder = Path.GetDirectoryName(infoPath);
        if (string.IsNullOrWhiteSpace(installFolder))
            return null;

        var json = File.ReadAllText(infoPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Best-effort title fields (varies)
        var title =
            GetString(root, "name") ??
            GetString(root, "title") ??
            GetString(root, "gameName") ??
            productId;

        // Best-effort executable discovery:
        // Collect any strings that look like relative/absolute exe paths.
        var exePaths = new List<string>();
        CollectExeLikeStrings(root, exePaths);

        // Normalize to full paths where possible
        var normalizedExeFull = exePaths
            .Select(p => NormalizeExePath(p, installFolder))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Choose “primary” exe (first existing if any, else first)
        string? primaryExeFull = normalizedExeFull.FirstOrDefault(File.Exists) ?? normalizedExeFull.FirstOrDefault();
        string? primaryExeName = !string.IsNullOrWhiteSpace(primaryExeFull) ? Path.GetFileName(primaryExeFull) : null;

        // Tools: if multiple exe candidates exist, emit tool entries for the extras
        var tools = new List<ToolEntry>();
        foreach (var exeFull in normalizedExeFull.Skip(1))
        {
            var exeName = Path.GetFileName(exeFull);
            var toolKey = $"{productId}:tool:{exeName}".ToLowerInvariant();

            tools.Add(new ToolEntry(
                ToolKey: toolKey,
                DisplayName: $"{title} (Tool: {exeName})",
                ExeName: exeName,
                ExeFullPath: exeFull,
                SourcePath: infoPath
            ));
        }

        return new GameEntry(
            ProductId: productId,
            DisplayName: title,
            InstallFolder: installFolder,
            PrimaryExeName: primaryExeName,
            PrimaryExeFullPath: primaryExeFull,
            Tools: tools,
            SourceKind: "goggame-info",
            SourcePath: infoPath
        );
    }

    private static string? ExtractProductIdFromInfoFilename(string fileNameNoExt)
    {
        // "goggame-1207662443" -> "1207662443"
        const string prefix = "goggame-";
        if (!fileNameNoExt.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var tail = fileNameNoExt.Substring(prefix.Length);
        // Allow only digits
        return tail.All(char.IsDigit) ? tail : null;
    }

    private static void CollectExeLikeStrings(JsonElement node, List<string> hits)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in node.EnumerateObject())
                {
                    // Heuristic: properties often named "path", "executable", etc.
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.Value.GetString();
                        if (LooksLikeExePath(s))
                            hits.Add(s!);
                    }
                    else
                    {
                        CollectExeLikeStrings(prop.Value, hits);
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var e in node.EnumerateArray())
                    CollectExeLikeStrings(e, hits);
                break;

            case JsonValueKind.String:
                var str = node.GetString();
                if (LooksLikeExePath(str))
                    hits.Add(str!);
                break;
        }
    }

    private static bool LooksLikeExePath(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (!s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static string NormalizeExePath(string raw, string installFolder)
    {
        raw = raw.Trim().Trim('"');

        // If absolute, keep.
        if (Path.IsPathRooted(raw))
            return raw;

        // Relative to install folder
        return Path.Combine(installFolder, raw);
    }

    private static IEnumerable<string> EnumerateFilesDepthLimited(string root, string pattern, int maxDepth)
    {
        var stack = new Stack<(string dir, int depth)>();
        stack.Push((root, 0));

        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var f in files)
                yield return f;

            if (depth >= maxDepth)
                continue;

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var sd in subDirs)
                stack.Push((sd, depth + 1));
        }
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

    private static string? SafeGetDirectoryName(string path)
    {
        try { return Path.GetDirectoryName(path); }
        catch { return null; }
    }

    private static ExceptionInfo ToExceptionInfo(Exception ex) => new ExceptionInfo
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };
}
