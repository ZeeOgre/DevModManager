using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Data.Sqlite;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Gog;

[SupportedOSPlatform("windows")]
public static class GogGameCatalog
{
    public sealed record GameEntry
    {
        public required string ProductId { get; init; }         // numeric as string (stable)
        public required string DisplayName { get; init; }       // originalTitle when possible
        public string? InstallPath { get; init; }               // from ExecutableSupportFiles.installPath (often exe full path)
        public string? GalaxyDbPath { get; init; }              // which db was used
        public string? WebsiteSlugGuess { get; init; }          // derived from title (best-effort)
        public string? WebsiteUrlGuess { get; init; }           // derived from slug
    }

    public static List<string> DiscoverGalaxyStorageRoots(List<ScanIssue> issues)
    {
        var roots = new List<string>();

        try
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(programData))
            {
                // Typical:
                // C:\ProgramData\GOG.com\Galaxy\storage\galaxy-2.0.db
                var storage = Path.Combine(programData, "GOG.com", "Galaxy", "storage");
                roots.Add(storage);
            }
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

        // De-dupe + keep existing only
        return roots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> NormalizeRoots(IReadOnlyList<string> roots)
    {
        // Accept either:
        // - storage folder
        // - direct path to galaxy-2.0.db
        // - direct path to galaxy.db
        var normalized = new List<string>();

        foreach (var r in roots ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(r))
                continue;

            var p = r.Trim().Trim('"');

            if (File.Exists(p))
            {
                normalized.Add(p);
                continue;
            }

            if (Directory.Exists(p))
            {
                normalized.Add(p);
                continue;
            }
        }

        return normalized
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> ResolveDbCandidates(List<string> roots)
    {
        // Prefer galaxy-2.0.db; fallback to galaxy.db
        var dbs = new List<string>();

        foreach (var r in roots)
        {
            if (File.Exists(r))
            {
                dbs.Add(r);
                continue;
            }

            if (Directory.Exists(r))
            {
                var g2 = Path.Combine(r, "galaxy-2.0.db");
                var g1 = Path.Combine(r, "galaxy.db");

                if (File.Exists(g2)) dbs.Add(g2);
                if (File.Exists(g1)) dbs.Add(g1);
            }
        }

        // Prefer galaxy-2.0.db if both exist
        return dbs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(p => p.EndsWith("galaxy-2.0.db", StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<GameEntry> BuildCatalog(IReadOnlyList<string> roots, List<ScanIssue> issues)
    {
        var normalized = NormalizeRoots(roots);
        var dbs = ResolveDbCandidates(normalized);

        if (dbs.Count == 0)
            return new List<GameEntry>();

        // Try DBs in order until we successfully read entries.
        foreach (var db in dbs)
        {
            try
            {
                var entries = ReadInstalledGogEntriesFromDb(db, issues);
                if (entries.Count > 0)
                    return entries;

                // If db readable but empty, keep trying other dbs
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

        return new List<GameEntry>();
    }

    private static List<GameEntry> ReadInstalledGogEntriesFromDb(string dbPath, List<ScanIssue> issues)
    {
        // Notes:
        // - Titles are available via GamePieces/GamePieceTypes originalTitle (as seen in known query) :contentReference[oaicite:4]{index=4}
        // - installPath is visible in ExecutableSupportFiles schema :contentReference[oaicite:5]{index=5}
        //
        // We do a best-effort join:
        // InstalledProducts.productId -> title + installPath
        //
        // Some products may have multiple ExecutableSupportFiles rows; we pick MIN(installPath).

        var results = new List<GameEntry>();

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        };

        using var conn = new SqliteConnection(csb.ToString());
        conn.Open();

        // Verify tables exist (avoid hard-crash on schema changes)
        var tableSet = GetTableNames(conn);
        bool hasInstalled = tableSet.Contains("InstalledProducts");
        bool hasPieces = tableSet.Contains("GamePieces") && tableSet.Contains("GamePieceTypes");
        bool hasExeSupport = tableSet.Contains("ExecutableSupportFiles");

        if (!hasInstalled)
        {
            issues.Add(new ScanIssue
            {
                Code = "GOG_DB_SCHEMA_UNEXPECTED",
                Message = "GOG Galaxy DB does not contain InstalledProducts table; schema may have changed.",
                StoreKey = StoreKeys.Gog,
                Path = dbPath
            });
            return results;
        }

        // If we can’t get title/installPath, still return product ids.
        var sql = BuildBestQuery(hasPieces, hasExeSupport);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var productId = reader.GetString(0);

            string display = productId;
            if (reader.FieldCount > 1 && !reader.IsDBNull(1))
                display = reader.GetString(1);

            string? installPath = null;
            if (reader.FieldCount > 2 && !reader.IsDBNull(2))
                installPath = reader.GetString(2);

            var slug = !string.IsNullOrWhiteSpace(display) ? GuessGogSlugFromTitle(display) : null;
            var url = !string.IsNullOrWhiteSpace(slug) ? $"https://www.gog.com/en/game/{slug}" : null;

            results.Add(new GameEntry
            {
                ProductId = productId,
                DisplayName = display,
                InstallPath = installPath,
                GalaxyDbPath = dbPath,
                WebsiteSlugGuess = slug,
                WebsiteUrlGuess = url
            });
        }

        return results;
    }

    private static string BuildBestQuery(bool hasPieces, bool hasExeSupport)
    {
        // Base: product ids
        // Title extraction follows the known pattern of trimming JSON-ish value {"title":"..."} :contentReference[oaicite:6]{index=6}
        //
        // installPath from ExecutableSupportFiles.installPath :contentReference[oaicite:7]{index=7}

        var sb = new StringBuilder();

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

    private static string GuessGogSlugFromTitle(string title)
    {
        // Heuristic based on examples like:
        // fear_platinum
        // dino_crisis_bundle
        //
        // Lowercase; spaces->underscore; strip punctuation; collapse underscores.
        var sb = new StringBuilder(title.Length);

        bool prevUnderscore = false;

        foreach (var ch in title.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                prevUnderscore = false;
                continue;
            }

            // treat whitespace & punctuation as underscore separators
            if (!prevUnderscore)
            {
                sb.Append('_');
                prevUnderscore = true;
            }
        }

        var s = sb.ToString().Trim('_');

        while (s.Contains("__", StringComparison.Ordinal))
            s = s.Replace("__", "_", StringComparison.Ordinal);

        return s;
    }

    private static ExceptionInfo ToExceptionInfo(Exception ex) => new ExceptionInfo
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        HResult = ex.HResult.ToString("X")
    };
}
