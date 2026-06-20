using System.Data;
using System.IO.Compression;
using DMM.AssetManagers;
using Microsoft.Data.Sqlite;

namespace DmmDep;

/// <summary>
/// Manages persistent caching of parent/system archive file indexes using SQLite.
/// Cache is stored in %LOCALAPPDATA%\ZeeOgre\dmmdeps\starfield_basefiles.db
/// 
/// Schema:
///   archives: archive_path, file_name, size, last_modified, file_count, scan_timestamp
///   files: file_path, archive_name (indexed for fast lookups)
/// </summary>
internal static class ParentArchiveCache
{
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZeeOgre",
        "dmmdeps"
    );

    private static readonly string CacheDbPath = Path.Combine(CacheRoot, "starfield_basefiles.db");

    /// <summary>
    /// Get the cached parent archive index, or build/update it if stale.
    /// Returns only files from allowed archives (base game, ContentResources, and specified masters).
    /// </summary>
    /// <param name="gameRoot">Game installation root directory</param>
    /// <param name="masterNames">Optional list of master plugin names (e.g., "DebugMenuFramework.esm") to include their BA2 archives</param>
    /// <param name="logger">Optional logging callback</param>
    public static Dictionary<string, string> GetOrBuildIndex(
        string gameRoot,
        IEnumerable<string>? masterNames = null,
        Action<string>? logger = null)
    {
        logger ??= _ => { };

        Directory.CreateDirectory(CacheRoot);
        logger($"[Cache] DB path: {CacheDbPath}");
        EnsureSchema();

        var parentArchives = DiscoverParentArchives(gameRoot, masterNames, logger);
        logger($"[Cache] Found {parentArchives.Count} parent archives in game root");

        // Build list of allowed archive names (base game + ContentResources + masters only)
        var allowedArchiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var archive in parentArchives)
        {
            allowedArchiveNames.Add(archive.Name);
        }

        logger($"[Cache] Allowed archives for lookup: {string.Join(", ", allowedArchiveNames)}");

        // DEBUG: Log ContentResources.zip specifically
        var contentResources = parentArchives.FirstOrDefault(a => a.Name.Equals("ContentResources.zip", StringComparison.OrdinalIgnoreCase));
        if (contentResources != null)
        {
            logger($"[Cache]   -> ContentResources.zip found: {contentResources.FullName}");
        }
        else
        {
            logger($"[Cache]   -> WARNING: ContentResources.zip NOT found!");
        }

        using var conn = OpenConnection();

        // Check which archives need scanning
        var staleArchives = FindStaleArchives(conn, parentArchives, logger);

        if (staleArchives.Count > 0)
        {
            logger($"[Cache] {staleArchives.Count} archive(s) need scanning");
            foreach (var archive in staleArchives)
            {
                ScanAndCacheArchive(conn, archive, logger);
            }
        }
        else
        {
            logger("[Cache] All archives are up to date");
        }

        // Load the index, but only for allowed archives
        var index = LoadFileIndex(conn, allowedArchiveNames);
        logger($"[Cache] Loaded {index.Count} files from allowed parent archives");

        // DEBUG: Show which archives contributed files
        if (index.Count > 0)
        {
            var archiveFileCounts = index.GroupBy(kvp => kvp.Value)
                .Select(g => $"{g.Key}={g.Count()}")
                .OrderByDescending(s => int.Parse(s.Split('=')[1]))
                .Take(10);
            logger($"[Cache] Top contributing archives: {string.Join(", ", archiveFileCounts)}");
        }

        return index;
    }

    /// <summary>
    /// Create the SQLite connection string and open connection.
    /// </summary>
    private static SqliteConnection OpenConnection()
    {
        var connString = new SqliteConnectionStringBuilder
        {
            DataSource = CacheDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var conn = new SqliteConnection(connString);
        conn.Open();

        // Enable WAL mode for better concurrency
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        return conn;
    }

    /// <summary>
    /// Ensure the database schema exists.
    /// </summary>
    private static void EnsureSchema()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS archives (
                archive_path TEXT PRIMARY KEY COLLATE NOCASE,
                file_name TEXT NOT NULL COLLATE NOCASE,
                size INTEGER NOT NULL,
                last_modified TEXT NOT NULL,
                file_count INTEGER NOT NULL,
                scan_timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS files (
                file_path TEXT PRIMARY KEY COLLATE NOCASE,
                archive_name TEXT NOT NULL COLLATE NOCASE
            );

            CREATE INDEX IF NOT EXISTS idx_files_archive 
            ON files(archive_name COLLATE NOCASE);
        ";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Discover all BA2 archives in the game root Data directory plus ContentResources.zip,
    /// plus any BA2 archives for master plugins.
    /// </summary>
    private static List<FileInfo> DiscoverParentArchives(
        string gameRoot,
        IEnumerable<string>? masterNames,
        Action<string> logger)
    {
        var archives = new List<FileInfo>();

        // 1. BA2 archives in Data directory (base game only)
        var dataDir = Path.Combine(gameRoot, "Data");
        if (Directory.Exists(dataDir))
        {
            archives.AddRange(
                Directory.GetFiles(dataDir, "*.ba2", SearchOption.TopDirectoryOnly)
                    .Where(p => IsBaseGameArchive(p))
                    .Select(p => new FileInfo(p))
            );
        }

        // 2. Master plugin archives (e.g., "DebugMenuFramework*.ba2")
        if (masterNames != null && Directory.Exists(dataDir))
        {
            foreach (var masterFile in masterNames)
            {
                if (string.IsNullOrWhiteSpace(masterFile))
                    continue;

                var baseName = Path.GetFileNameWithoutExtension(masterFile);
                if (string.IsNullOrWhiteSpace(baseName))
                    continue;

                var pattern = $"{baseName}*.ba2";
                var masterArchives = Directory.GetFiles(dataDir, pattern, SearchOption.TopDirectoryOnly);

                if (masterArchives.Length > 0)
                {
                    logger($"[Cache]   Found {masterArchives.Length} archive(s) for master '{masterFile}': {pattern}");
                    archives.AddRange(masterArchives.Select(p => new FileInfo(p)));
                }
            }
        }

        // 3. ContentResources.zip (canonical source of all CK-distributed files)
        var contentResourcesPath = Path.Combine(gameRoot, "Tools", "ContentResources.zip");
        if (File.Exists(contentResourcesPath))
        {
            archives.Add(new FileInfo(contentResourcesPath));
        }

        return archives.OrderBy(f => f.Name).ToList();
    }

    /// <summary>
    /// Heuristic to identify base game archives.
    /// For Starfield, base game archives typically start with "Starfield".
    /// </summary>
    private static bool IsBaseGameArchive(string ba2Path)
    {
        var name = Path.GetFileNameWithoutExtension(ba2Path);
        return name.StartsWith("Starfield", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Find archives that need to be rescanned (new, changed, or missing from cache).
    /// </summary>
    private static List<FileInfo> FindStaleArchives(SqliteConnection conn, List<FileInfo> currentArchives, Action<string> logger)
    {
        var stale = new List<FileInfo>();

        foreach (var archive in currentArchives)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT size, last_modified 
                FROM archives 
                WHERE archive_path = @path COLLATE NOCASE
            ";
            cmd.Parameters.AddWithValue("@path", archive.FullName);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                // Not in cache
                logger($"[Cache]   {archive.Name} - not in cache");
                stale.Add(archive);
                continue;
            }

            var cachedSize = reader.GetInt64(0);
            var cachedModifiedStr = reader.GetString(1);
            var cachedModified = DateTime.Parse(cachedModifiedStr, null, System.Globalization.DateTimeStyles.RoundtripKind);

            if (cachedSize != archive.Length || cachedModified != archive.LastWriteTimeUtc)
            {
                logger($"[Cache]   {archive.Name} - changed (size or timestamp)");
                logger($"[Cache]     Cached: size={cachedSize}, modified={cachedModified:o}");
                logger($"[Cache]     Disk:   size={archive.Length}, modified={archive.LastWriteTimeUtc:o}");
                stale.Add(archive);
            }
        }

        return stale;
    }

    /// <summary>
    /// Scan an archive (BA2 or ZIP) and cache its file list.
    /// </summary>
    private static void ScanAndCacheArchive(SqliteConnection conn, FileInfo archive, Action<string> logger)
    {
        logger($"[Cache] Scanning: {archive.Name}");

        try
        {
            List<(string RelativePath, long Size)> entries;

            if (archive.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                entries = ScanZipArchive(archive.FullName);
            }
            else
            {
                entries = BA2Archive.ReadIndex(archive.FullName)
                    .Select(e => (e.RelativePath, (long)e.UnpackedSize))
                    .ToList();
            }

            logger($"[Cache]   -> {entries.Count} files found");

            using var transaction = conn.BeginTransaction();

            // Delete old entries for this archive
            using (var deleteArchiveCmd = conn.CreateCommand())
            {
                deleteArchiveCmd.CommandText = "DELETE FROM archives WHERE archive_path = @path COLLATE NOCASE";
                deleteArchiveCmd.Parameters.AddWithValue("@path", archive.FullName);
                deleteArchiveCmd.ExecuteNonQuery();
            }

            using (var deleteFilesCmd = conn.CreateCommand())
            {
                deleteFilesCmd.CommandText = "DELETE FROM files WHERE archive_name = @name COLLATE NOCASE";
                deleteFilesCmd.Parameters.AddWithValue("@name", archive.Name);
                deleteFilesCmd.ExecuteNonQuery();
            }

            // Insert archive metadata
            using (var insertArchiveCmd = conn.CreateCommand())
            {
                insertArchiveCmd.CommandText = @"
                    INSERT INTO archives (archive_path, file_name, size, last_modified, file_count, scan_timestamp)
                    VALUES (@path, @name, @size, @modified, @count, @timestamp)
                ";
                insertArchiveCmd.Parameters.AddWithValue("@path", archive.FullName);
                insertArchiveCmd.Parameters.AddWithValue("@name", archive.Name);
                insertArchiveCmd.Parameters.AddWithValue("@size", archive.Length);
                insertArchiveCmd.Parameters.AddWithValue("@modified", archive.LastWriteTimeUtc.ToString("o"));
                insertArchiveCmd.Parameters.AddWithValue("@count", entries.Count);
                insertArchiveCmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("o"));
                insertArchiveCmd.ExecuteNonQuery();
            }

            // Insert file entries in batches
            const int batchSize = 500;
            for (int i = 0; i < entries.Count; i += batchSize)
            {
                var batch = entries.Skip(i).Take(batchSize);
                using var insertFilesCmd = conn.CreateCommand();
                var sql = new System.Text.StringBuilder("INSERT OR REPLACE INTO files (file_path, archive_name) VALUES ");
                var first = true;
                var paramIndex = 0;

                foreach (var entry in batch)
                {
                    if (!first) sql.Append(", ");
                    sql.Append($"(@path{paramIndex}, @archive{paramIndex})");
                    insertFilesCmd.Parameters.AddWithValue($"@path{paramIndex}", NormalizePath(entry.RelativePath));
                    insertFilesCmd.Parameters.AddWithValue($"@archive{paramIndex}", archive.Name);
                    paramIndex++;
                    first = false;
                }

                insertFilesCmd.CommandText = sql.ToString();
                insertFilesCmd.ExecuteNonQuery();
            }

            transaction.Commit();
            logger($"[Cache]   -> Cached {entries.Count} files");
        }
        catch (Exception ex)
        {
            logger($"[Cache]   -> Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Load the file index from the cache, filtered to only include files from allowed archives.
    /// </summary>
    /// <param name="conn">Open SQLite connection</param>
    /// <param name="allowedArchiveNames">Optional set of archive names to include. If null, includes all archives.</param>
    private static Dictionary<string, string> LoadFileIndex(SqliteConnection conn, HashSet<string>? allowedArchiveNames = null)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var cmd = conn.CreateCommand();

        if (allowedArchiveNames != null && allowedArchiveNames.Count > 0)
        {
            // Build a parameterized IN clause
            var parameters = new List<string>();
            for (int i = 0; i < allowedArchiveNames.Count; i++)
            {
                parameters.Add($"@archive{i}");
            }

            // Note: file_path is already lowercase in the cache, and we compare lowercase in Program.cs,
            // so COLLATE is not needed here. archive_name comparison is case-insensitive by design.
            cmd.CommandText = $"SELECT file_path, archive_name FROM files WHERE archive_name IN ({string.Join(", ", parameters)})";

            int paramIndex = 0;
            foreach (var archiveName in allowedArchiveNames)
            {
                cmd.Parameters.AddWithValue($"@archive{paramIndex}", archiveName);
                paramIndex++;
            }
        }
        else
        {
            // No filter - return everything (legacy behavior)
            cmd.CommandText = "SELECT file_path, archive_name FROM files";
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var path = reader.GetString(0);
            var archive = reader.GetString(1);
            index[path] = archive;
        }

        return index;
    }

    /// <summary>
    /// Normalize a path to Data-relative format for consistent lookups.
    /// </summary>
    private static string NormalizePath(string path)
    {
        path = path.Replace('/', '\\').Trim('\\');

        // Ensure it starts with "Data\"
        if (!path.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
            path = "Data\\" + path;

        // Normalize to lowercase for case-insensitive comparison
        return path.ToLowerInvariant();
    }

    /// <summary>
    /// Clear the entire cache database by dropping and recreating tables.
    /// </summary>
    public static void ClearCache()
    {
        if (!File.Exists(CacheDbPath))
            return;

        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                DROP TABLE IF EXISTS files;
                DROP TABLE IF EXISTS archives;
            ";
            cmd.ExecuteNonQuery();

            // Recreate schema
            EnsureSchema();
        }
        catch (Exception ex)
        {
            // If we can't clear, try to delete the file
            try
            {
                File.Delete(CacheDbPath);
            }
            catch
            {
                throw new InvalidOperationException($"Cannot clear cache: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Scan a ZIP archive and extract its file list.
    /// </summary>
    private static List<(string RelativePath, long Size)> ScanZipArchive(string zipPath)
    {
        var entries = new List<(string, long)>();

        using var archive = ZipFile.OpenRead(zipPath);
        int totalEntries = 0;
        int skippedDirs = 0;

        foreach (var entry in archive.Entries)
        {
            totalEntries++;

            // Skip directories (entries with names ending in /)
            if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
            {
                skippedDirs++;
                continue;
            }

            entries.Add((entry.FullName, entry.Length));
        }

        Console.WriteLine($"[ZIP] Total entries: {totalEntries}, Dirs skipped: {skippedDirs}, Files: {entries.Count}");
        return entries;
    }

    /// <summary>
    /// Parse the TES4 header of a plugin file and extract master plugin names from MAST subrecords.
    /// </summary>
    public static List<string> ParsePluginMasters(string pluginPath)
    {
        var masters = new List<string>();

        if (!File.Exists(pluginPath))
            return masters;

        try
        {
            using var stream = File.OpenRead(pluginPath);
            using var reader = new BinaryReader(stream);

            // Read TES4 record type (4 bytes)
            var recordType = new string(reader.ReadChars(4));
            if (recordType != "TES4")
                return masters; // Not a valid plugin

            // Skip record size (4 bytes) and flags (4 bytes)
            reader.ReadUInt32();
            reader.ReadUInt32();

            // Read form ID (4 bytes) and version control (4 bytes)
            reader.ReadUInt32();
            reader.ReadUInt32();

            // Read TES4 data size (4 bytes) to know when to stop
            var tes4DataSize = reader.ReadUInt32();
            var tes4EndPos = stream.Position + tes4DataSize;

            // Scan subrecords until we reach the end of TES4
            while (stream.Position < tes4EndPos)
            {
                // Read subrecord type (4 bytes)
                var subType = new string(reader.ReadChars(4));
                var subSize = reader.ReadUInt16();

                if (subType == "MAST")
                {
                    // MAST contains a null-terminated string with the master filename
                    var masterBytes = reader.ReadBytes(subSize);
                    var masterName = System.Text.Encoding.UTF8.GetString(masterBytes).TrimEnd('\0');
                    if (!string.IsNullOrWhiteSpace(masterName))
                    {
                        masters.Add(masterName);
                    }
                }
                else
                {
                    // Skip this subrecord
                    stream.Seek(subSize, SeekOrigin.Current);
                }
            }
        }
        catch
        {
            // If parsing fails, just return empty list
        }

        return masters;
    }
}
