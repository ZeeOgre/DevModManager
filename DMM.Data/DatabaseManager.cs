using Microsoft.Data.Sqlite;

namespace DMM.Data;

public sealed class DatabaseManager
{
    private const string BaselineSeedVersion = "2026.02-baseline-core";

    private readonly string _databasePath;

    public DatabaseManager(string? databasePath = null)
    {
        _databasePath = databasePath ?? DatabasePaths.GetDatabasePath();
    }

    public string DatabasePath => _databasePath;

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = _databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = true,
        Cache = SqliteCacheMode.Default
    }.ToString();

    public void EnsureCreated()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var isNewDatabase = !File.Exists(_databasePath);

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        if (isNewDatabase)
        {
            ExecuteSqlScript(connection, LoadSqlScript("database_schema.sql"));
        }

        EnsureSeedTable(connection);
        EnsureBaselineSeedApplied(connection);
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    private static void EnsureSeedTable(SqliteConnection connection)
    {
        ExecuteSqlScript(connection, """
CREATE TABLE IF NOT EXISTS SeedHistory (
    Version   TEXT PRIMARY KEY,
    AppliedAt TEXT NOT NULL
);
""");
    }

    private static void EnsureBaselineSeedApplied(SqliteConnection connection)
    {
        using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT 1 FROM SeedHistory WHERE Version = $version LIMIT 1";
        existsCommand.Parameters.AddWithValue("$version", BaselineSeedVersion);

        var alreadyApplied = existsCommand.ExecuteScalar() is not null;
        if (alreadyApplied)
        {
            return;
        }

        var seedSql = LoadSqlScript("database_seed.sql");
        ExecuteSqlScript(connection, seedSql);

        using var markAppliedCommand = connection.CreateCommand();
        markAppliedCommand.CommandText = "INSERT INTO SeedHistory (Version, AppliedAt) VALUES ($version, $appliedAt)";
        markAppliedCommand.Parameters.AddWithValue("$version", BaselineSeedVersion);
        markAppliedCommand.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
        markAppliedCommand.ExecuteNonQuery();
    }

    private static void ExecuteSqlScript(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string LoadSqlScript(string scriptName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Schema", scriptName);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Database SQL file was not found at '{scriptPath}'.", scriptPath);
        }

        return File.ReadAllText(scriptPath);
    }
}
