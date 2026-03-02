using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DMM.Data;
using Microsoft.Data.Sqlite;

namespace DMM.Avalonia;

internal sealed class GameSetupRepository
{
    private readonly DatabaseManager _database = new();

    public GameSetupRepository()
    {
        EnsureManagedModCatalogTable();
    }

    public IReadOnlyList<ManagedGame> LoadManagedGames()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.Name,
                   COALESCE(g.Executable, ''),
                   COALESCE((
                       SELECT gsa.StoreAppId
                       FROM GameStoreApp gsa
                       JOIN GameSource gs ON gs.id = gsa.GameSourceId
                       WHERE gsa.GameId = g.id
                         AND gs.Name = 'Steam'
                       ORDER BY gsa.id
                       LIMIT 1
                   ), '')
            FROM Game g
            WHERE g.IsDlc = 0
            ORDER BY g.Name
            """;

        var games = new List<ManagedGame>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            games.Add(new ManagedGame
            {
                Name = name,
                Executable = reader.GetString(1),
                StoreId = reader.GetString(2)
            });
        }

        return games;
    }

    public IReadOnlyList<KnownGameCatalogRecord> LoadKnownGameCatalog()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.Name,
                   g.IsDlc,
                   parent.Name,
                   COALESCE(gsa.StoreAppId, '')
            FROM Game g
            LEFT JOIN Game parent ON parent.id = g.ParentGameId
            LEFT JOIN GameStoreApp gsa ON gsa.GameId = g.id
            """;

        var records = new List<KnownGameCatalogRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new KnownGameCatalogRecord(
                reader.GetString(0),
                reader.GetInt64(1) == 1,
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3)));
        }

        return records;
    }

    public IReadOnlyList<KnownPluginRecord> LoadKnownPluginsForGame(string gameName)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kp.DisplayName, kp.PluginName, kp.IsBaseGame, kp.IsDlc
            FROM GameKnownPlugin kp
            JOIN Game g ON g.id = kp.GameId
            WHERE g.Name = $gameName
            ORDER BY kp.PluginName
            """;
        command.Parameters.AddWithValue("$gameName", gameName);

        var plugins = new List<KnownPluginRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            plugins.Add(new KnownPluginRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2) == 1,
                reader.GetInt64(3) == 1));
        }

        return plugins;
    }

    public IReadOnlyList<KnownPluginRecord> LoadKnownPluginsForGameIncludingDlc(string gameName)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kp.DisplayName, kp.PluginName, kp.IsBaseGame, kp.IsDlc
            FROM GameKnownPlugin kp
            JOIN Game g ON g.id = kp.GameId
            LEFT JOIN Game parent ON parent.id = g.ParentGameId
            WHERE g.Name = $gameName OR parent.Name = $gameName
            ORDER BY kp.PluginName
            """;
        command.Parameters.AddWithValue("$gameName", gameName);

        var plugins = new List<KnownPluginRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            plugins.Add(new KnownPluginRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2) == 1,
                reader.GetInt64(3) == 1));
        }

        return plugins;
    }

    public IReadOnlyList<GameInstallRecord> LoadManagedInstalls(IEnumerable<ManagedGame> managedGames)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(gs.Name, ''), COALESCE(g.Name, ''), COALESCE(gsi.StoreAppId, ''), COALESCE(f.Path, ''),
                   CASE WHEN EXISTS (
                       SELECT 1 FROM GameStoreProductLink l
                       WHERE l.ChildInstallId = gsi.id AND l.LinkType = 'DLC'
                   ) THEN 1 ELSE 0 END AS IsDlc
            FROM GameStoreInstall gsi
            LEFT JOIN GameStoreRoot gsr ON gsr.id = gsi.GameStoreRootId
            LEFT JOIN GameSource gs ON gs.id = gsr.GameSourceId
            LEFT JOIN Game g ON g.id = gsi.GameId
            LEFT JOIN Folders f ON f.id = gsi.InstallFolderId
            ORDER BY gsi.LastSeenDT DESC
            """;

        var managedByName = managedGames.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var installs = new List<GameInstallRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var gameName = reader.GetString(1);
            managedByName.TryGetValue(gameName, out var game);
            installs.Add(new GameInstallRecord
            {
                Manage = true,
                GameStore = reader.GetString(0),
                ManagedGame = game,
                StoreAppId = reader.GetString(2),
                InstallPath = reader.GetString(3),
                IsDlc = reader.GetInt64(4) == 1
            });
        }

        return installs;
    }

    public IReadOnlyList<ManagedModRecord> LoadManagedModsForInstall(string installPath, string gameName)
    {
        if (string.IsNullOrWhiteSpace(installPath) || string.IsNullOrWhiteSpace(gameName))
        {
            return Array.Empty<ManagedModRecord>();
        }

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT GameName, InstallPath, ModName, PrimaryPlugin, StageName, ModRepoPath
            FROM ManagedModCatalog
            WHERE InstallPath = $installPath
              AND GameName = $gameName
            ORDER BY ModName
            """;
        command.Parameters.AddWithValue("$installPath", installPath);
        command.Parameters.AddWithValue("$gameName", gameName);

        var records = new List<ManagedModRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new ManagedModRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return records;
    }

    public void UpsertManagedModForInstall(
        string gameName,
        string installPath,
        string modName,
        string primaryPlugin,
        string stage,
        string modRepoPath)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ManagedModCatalog (GameName, InstallPath, ModName, PrimaryPlugin, StageName, ModRepoPath, LastSeenUtc)
            VALUES ($gameName, $installPath, $modName, $primaryPlugin, $stageName, $modRepoPath, $lastSeenUtc)
            ON CONFLICT(InstallPath, ModName) DO UPDATE SET
                GameName = excluded.GameName,
                PrimaryPlugin = excluded.PrimaryPlugin,
                StageName = excluded.StageName,
                ModRepoPath = excluded.ModRepoPath,
                LastSeenUtc = excluded.LastSeenUtc
            """;
        command.Parameters.AddWithValue("$gameName", gameName);
        command.Parameters.AddWithValue("$installPath", installPath);
        command.Parameters.AddWithValue("$modName", modName);
        command.Parameters.AddWithValue("$primaryPlugin", primaryPlugin);
        command.Parameters.AddWithValue("$stageName", stage);
        command.Parameters.AddWithValue("$modRepoPath", modRepoPath);
        command.Parameters.AddWithValue("$lastSeenUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void EnsureManagedModCatalogTable()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ManagedModCatalog (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                GameName      TEXT NOT NULL,
                InstallPath   TEXT NOT NULL,
                ModName       TEXT NOT NULL,
                PrimaryPlugin TEXT NOT NULL,
                StageName     TEXT NOT NULL,
                ModRepoPath   TEXT NOT NULL,
                LastSeenUtc   TEXT NOT NULL,
                UNIQUE (InstallPath, ModName)
            );
            """;
        command.ExecuteNonQuery();
    }

    public void UpsertManagedGame(ManagedGame game)
    {
        using var connection = _database.OpenConnection();

        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT id FROM Game WHERE Name = $name LIMIT 1";
        exists.Parameters.AddWithValue("$name", game.Name);
        var existingId = exists.ExecuteScalar();

        using var command = connection.CreateCommand();
        if (existingId is null)
        {
            command.CommandText = "INSERT INTO Game (Name, Executable) VALUES ($name, $exe)";
        }
        else
        {
            command.CommandText = "UPDATE Game SET Executable = $exe WHERE id = $id";
            command.Parameters.AddWithValue("$id", (long)existingId);
        }

        command.Parameters.AddWithValue("$name", game.Name);
        command.Parameters.AddWithValue("$exe", game.Executable);
        command.ExecuteNonQuery();
    }

    public void ReplaceManagedInstalls(IReadOnlyList<GameInstallRecord> installs, IReadOnlyCollection<ManagedGame> managedGames)
    {
        using var connection = _database.OpenConnection();
        using var tx = connection.BeginTransaction();

        using (var clear = connection.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM GameStoreInstall";
            clear.ExecuteNonQuery();
        }

        var gameIdLookup = LoadGameIdLookup(connection, tx);
        foreach (var game in managedGames)
        {
            EnsureGameId(connection, tx, gameIdLookup, game);
        }

        var folderTypeId = EnsureFolderType(connection, tx, "GameInstall");
        var folderRoleId = EnsureFolderRole(connection, tx, "GameInstall");
        var fileStorageKindId = EnsureFileStorageKind(connection, tx, "Primary", "Game/discovered file on disk");

        foreach (var install in installs)
        {
            var installFolderId = EnsureFolder(connection, tx, install.InstallPath, folderTypeId, folderRoleId);
            var rootPath = Path.GetPathRoot(install.InstallPath) ?? install.InstallPath;
            var rootFolderId = EnsureFolder(connection, tx, rootPath, folderTypeId, folderRoleId);
            var sourceId = EnsureGameSource(connection, tx, install.GameStore);
            var rootId = EnsureStoreRoot(connection, tx, sourceId, rootFolderId);

            var storeAppId = !string.IsNullOrWhiteSpace(install.StoreAppId)
                ? install.StoreAppId
                : !string.IsNullOrWhiteSpace(install.ManagedGame?.StoreId)
                    ? install.ManagedGame.StoreId
                    : $"custom:{install.InstallPath}";

            long? gameId = EnsureGameId(connection, tx, gameIdLookup, install.ManagedGame);

            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO GameStoreInstall (
                    GameStoreRootId, InstallFolderId, GameId, StoreAppId, DisplayName, ExecutableName, LastSeenDT)
                VALUES ($rootId, $installFolderId, $gameId, $storeAppId, $displayName, $exe, $now)
                """;
            cmd.Parameters.AddWithValue("$rootId", rootId);
            cmd.Parameters.AddWithValue("$installFolderId", installFolderId);
            cmd.Parameters.AddWithValue("$gameId", gameId.HasValue ? gameId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$storeAppId", storeAppId);
            cmd.Parameters.AddWithValue("$displayName", install.ManagedGame?.Name ?? "Unknown");
            cmd.Parameters.AddWithValue("$exe", install.ManagedGame?.Executable ?? string.Empty);
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();

            var childInstallId = ReadLastInsertRowId(connection, tx);
            PersistInstallManifestFiles(connection, tx, childInstallId, gameId, fileStorageKindId, folderTypeId, folderRoleId, install);

            if (install.IsDlc && !string.IsNullOrWhiteSpace(install.ManagedGame?.StoreId))
            {
                using var link = connection.CreateCommand();
                link.Transaction = tx;
                link.CommandText = """
                    INSERT OR IGNORE INTO GameStoreProductLink (
                        ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType)
                    VALUES ($childInstallId, $parentGameSourceId, $parentStoreAppId, 'DLC')
                    """;
                link.Parameters.AddWithValue("$childInstallId", childInstallId);
                link.Parameters.AddWithValue("$parentGameSourceId", sourceId);
                link.Parameters.AddWithValue("$parentStoreAppId", install.ManagedGame.StoreId);
                link.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    private static long EnsureFileStorageKind(SqliteConnection connection, SqliteTransaction tx, string name, string description)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM FileStorageKind WHERE Name = $name LIMIT 1";
        select.Parameters.AddWithValue("$name", name);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO FileStorageKind (Name, Description) VALUES ($name, $description)";
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$description", description);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static void PersistInstallManifestFiles(
        SqliteConnection connection,
        SqliteTransaction tx,
        long installId,
        long? gameId,
        long fileStorageKindId,
        long folderTypeId,
        long folderRoleId,
        GameInstallRecord install)
    {
        if (string.IsNullOrWhiteSpace(install.BaseGameManifestPath) || !File.Exists(install.BaseGameManifestPath))
        {
            return;
        }

        using var stream = File.OpenRead(install.BaseGameManifestPath);
        using var doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.TryGetProperty("Files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var fileEntry in files.EnumerateArray())
        {
            if (!fileEntry.TryGetProperty("RelativePath", out var relativePathElement))
            {
                continue;
            }

            var relativePath = relativePathElement.GetString();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var extension = Path.GetExtension(relativePath);
            if (!IsKnownGameDataExtension(extension))
            {
                continue;
            }

            var fileName = Path.GetFileName(relativePath);
            var size = fileEntry.TryGetProperty("SizeBytes", out var sizeElement) ? sizeElement.GetInt64() : 0L;
            var dtStamp = fileEntry.TryGetProperty("LastWriteUtc", out var lastWriteElement) &&
                          lastWriteElement.ValueKind == JsonValueKind.String &&
                          DateTimeOffset.TryParse(lastWriteElement.GetString(), out var parsed)
                ? parsed
                : DateTimeOffset.UtcNow;

            var relativeFolderPath = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
            var relativeFolderId = EnsureRelativeFolderId(connection, tx, relativeFolderPath, folderTypeId, folderRoleId);
            var fileTypeId = TryFindFileTypeId(connection, tx, extension);
            var fileInfoId = EnsureManifestFileInfo(connection, tx, fileName, size, dtStamp, gameId, fileTypeId, relativeFolderId, fileStorageKindId);

            using var insertLink = connection.CreateCommand();
            insertLink.Transaction = tx;
            insertLink.CommandText = """
                INSERT OR IGNORE INTO GameStoreInstallFile (
                    InstallId, FileInfoId, RelativePath, FileRole, IsPresentOnDisk, LastValidatedDT)
                VALUES ($installId, $fileInfoId, $relativePath, 'Reference', 1, $lastValidated)
                """;
            insertLink.Parameters.AddWithValue("$installId", installId);
            insertLink.Parameters.AddWithValue("$fileInfoId", fileInfoId);
            insertLink.Parameters.AddWithValue("$relativePath", relativePath.Replace('\\', '/'));
            insertLink.Parameters.AddWithValue("$lastValidated", dtStamp.ToString("O"));
            insertLink.ExecuteNonQuery();
        }
    }

    private static long? EnsureRelativeFolderId(
        SqliteConnection connection,
        SqliteTransaction tx,
        string? relativeFolderPath,
        long folderTypeId,
        long folderRoleId)
    {
        if (string.IsNullOrWhiteSpace(relativeFolderPath) || relativeFolderPath == ".")
        {
            return null;
        }

        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM Folders WHERE Path = $path LIMIT 1";
        select.Parameters.AddWithValue("$path", relativeFolderPath);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO Folders (Path, FolderTypeId, FolderRoleId) VALUES ($path, $folderTypeId, $folderRoleId)";
        insert.Parameters.AddWithValue("$path", relativeFolderPath);
        insert.Parameters.AddWithValue("$folderTypeId", folderTypeId);
        insert.Parameters.AddWithValue("$folderRoleId", folderRoleId);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static bool IsKnownGameDataExtension(string? extension)
        => extension is not null && (
            extension.Equals(".esm", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".esl", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".esp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ba2", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bsa", StringComparison.OrdinalIgnoreCase));

    private static long? TryFindFileTypeId(SqliteConnection connection, SqliteTransaction tx, string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM FileType WHERE LOWER(FileExtension) = $ext LIMIT 1";
        cmd.Parameters.AddWithValue("$ext", extension.ToLowerInvariant());
        var result = cmd.ExecuteScalar();
        return result is long id ? id : null;
    }

    private static long EnsureManifestFileInfo(
        SqliteConnection connection,
        SqliteTransaction tx,
        string name,
        long size,
        DateTimeOffset dtStamp,
        long? gameId,
        long? fileTypeId,
        long? relativeFolderId,
        long fileStorageKindId)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = """
            SELECT id FROM FileInfo
            WHERE Name = $name
              AND Size = $size
              AND IFNULL(GameId, 0) = IFNULL($gameId, 0)
              AND IFNULL(FileTypeId, 0) = IFNULL($fileTypeId, 0)
              AND IFNULL(RelativeFolderId, 0) = IFNULL($relativeFolderId, 0)
            LIMIT 1
            """;
        select.Parameters.AddWithValue("$name", name);
        select.Parameters.AddWithValue("$size", size);
        select.Parameters.AddWithValue("$gameId", gameId.HasValue ? gameId.Value : DBNull.Value);
        select.Parameters.AddWithValue("$fileTypeId", fileTypeId.HasValue ? fileTypeId.Value : DBNull.Value);
        select.Parameters.AddWithValue("$relativeFolderId", relativeFolderId.HasValue ? relativeFolderId.Value : DBNull.Value);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO FileInfo (Name, DTStamp, Size, GameId, FileTypeId, RelativeFolderId, FileStorageKindId)
            VALUES ($name, $dtStamp, $size, $gameId, $fileTypeId, $relativeFolderId, $fileStorageKindId)
            """;
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$dtStamp", dtStamp.ToString("O"));
        insert.Parameters.AddWithValue("$size", size);
        insert.Parameters.AddWithValue("$gameId", gameId.HasValue ? gameId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("$fileTypeId", fileTypeId.HasValue ? fileTypeId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("$relativeFolderId", relativeFolderId.HasValue ? relativeFolderId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("$fileStorageKindId", fileStorageKindId);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long? EnsureGameId(
        SqliteConnection connection,
        SqliteTransaction tx,
        IDictionary<string, long> gameIdLookup,
        ManagedGame? game)
    {
        if (game is null || string.IsNullOrWhiteSpace(game.Name))
        {
            return null;
        }

        if (gameIdLookup.TryGetValue(game.Name, out var existingId))
        {
            return existingId;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO Game (Name, Executable) VALUES ($name, $exe)";
        insert.Parameters.AddWithValue("$name", game.Name);
        insert.Parameters.AddWithValue("$exe", game.Executable ?? string.Empty);
        insert.ExecuteNonQuery();

        var createdId = ReadLastInsertRowId(connection, tx);
        gameIdLookup[game.Name] = createdId;
        return createdId;
    }

    private static Dictionary<string, long> LoadGameIdLookup(SqliteConnection connection, SqliteTransaction tx)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id, Name FROM Game";
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(1)] = reader.GetInt64(0);
        }
        return result;
    }

    private static long EnsureFolderType(SqliteConnection connection, SqliteTransaction tx, string name)
        => EnsureByName(connection, tx, "FolderType", name);

    private static long EnsureFolderRole(SqliteConnection connection, SqliteTransaction tx, string name)
        => EnsureByName(connection, tx, "FolderRole", name);

    private static long EnsureByName(SqliteConnection connection, SqliteTransaction tx, string tableName, string name)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = $"SELECT id FROM {tableName} WHERE Name = $name LIMIT 1";
        select.Parameters.AddWithValue("$name", name);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = $"INSERT INTO {tableName} (Name) VALUES ($name)";
        insert.Parameters.AddWithValue("$name", name);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long EnsureFolder(SqliteConnection connection, SqliteTransaction tx, string path, long folderTypeId, long folderRoleId)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM Folders WHERE Path = $path LIMIT 1";
        select.Parameters.AddWithValue("$path", path);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO Folders (Path, FolderTypeId, FolderRoleId) VALUES ($path, $typeId, $roleId)";
        insert.Parameters.AddWithValue("$path", path);
        insert.Parameters.AddWithValue("$typeId", folderTypeId);
        insert.Parameters.AddWithValue("$roleId", folderRoleId);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long EnsureGameSource(SqliteConnection connection, SqliteTransaction tx, string store)
    {
        var normalizedStore = string.IsNullOrWhiteSpace(store) ? "Custom" : store.Trim();

        var sourceName = normalizedStore switch
        {
            "Game Pass" => "GamePass",
            "GOG" => "GoG",
            _ => normalizedStore
        };

        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM GameSource WHERE Name = $name LIMIT 1";
        select.Parameters.AddWithValue("$name", sourceName);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO GameSource (Name) VALUES ($name)";
        insert.Parameters.AddWithValue("$name", sourceName);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long EnsureStoreRoot(SqliteConnection connection, SqliteTransaction tx, long gameSourceId, long rootFolderId)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM GameStoreRoot WHERE GameSourceId = $sourceId AND RootFolderId = $folderId AND RootType = 'Library' LIMIT 1";
        select.Parameters.AddWithValue("$sourceId", gameSourceId);
        select.Parameters.AddWithValue("$folderId", rootFolderId);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO GameStoreRoot (GameSourceId, RootFolderId, RootType, LastSeenDT) VALUES ($sourceId, $folderId, 'Library', $now)";
        insert.Parameters.AddWithValue("$sourceId", gameSourceId);
        insert.Parameters.AddWithValue("$folderId", rootFolderId);
        insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long ReadLastInsertRowId(SqliteConnection connection, SqliteTransaction tx)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}

internal sealed record ManagedModRecord(string GameName, string InstallPath, string ModName, string PrimaryPlugin, string Stage, string ModRepoPath);
internal sealed record KnownPluginRecord(string DisplayName, string PluginName, bool IsBaseGame, bool IsDlc);
internal sealed record KnownGameCatalogRecord(string GameName, bool IsDlc, string? ParentGameName, string StoreAppId);

