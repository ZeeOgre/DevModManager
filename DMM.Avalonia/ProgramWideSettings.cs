using System;
using System.IO;
using System.Text.Json;
using DMM.Data;
using Microsoft.Data.Sqlite;

namespace DMM.Avalonia;

public sealed class ProgramWideSettings
{
    public string RepoRootPath { get; set; } = GetDefaultRepoRoot();
    public RepoOrganizationStrategy RepoOrganization { get; set; } = RepoOrganizationStrategy.GameRepoWithPerModFolders;
    public string LastSelectedGameFolder { get; set; } = string.Empty;

    // GitHub settings for repo/bootstrap workflows.
    public string GitHubAccount { get; set; } = string.Empty;
    public string GitHubToken { get; set; } = string.Empty;
    public string GitHubModRootRepo { get; set; } = string.Empty;
    public ExitSyncPreference ExitSyncPreference { get; set; } = ExitSyncPreference.Prompt;
    public ModFocusSyncPreference ModFocusSyncPreference { get; set; } = ModFocusSyncPreference.Prompt;
    public bool TimedAutoSyncEnabled { get; set; } = false;
    public int TimedAutoSyncIntervalMinutes { get; set; } = 10;

    public static string GetDefaultRepoRoot()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "ModRepo");
    }
}

public enum RepoOrganizationStrategy
{
    GameRepoWithPerModFolders = 0,
    RepoPerMod = 1
}

public enum ExitSyncPreference
{
    Prompt = 0,
    Always = 1,
    Never = 2
}

public enum ModFocusSyncPreference
{
    Prompt = 0,
    Always = 1,
    Never = 2
}

internal sealed class ProgramBootstrapSettings
{
    public string DatabasePath { get; set; } = string.Empty;
}

public sealed class ProgramWideSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string BootstrapPath => Path.Combine(DatabasePaths.GetDatabaseDirectory(), "program-settings.json");

    private readonly DatabaseManager _database;

    public ProgramWideSettingsStore()
    {
        var bootstrap = LoadBootstrapSettings();
        _database = string.IsNullOrWhiteSpace(bootstrap.DatabasePath)
            ? new DatabaseManager()
            : new DatabaseManager(bootstrap.DatabasePath);
    }

    public ProgramWideSettings Load()
    {
        _database.EnsureCreated();

        using var connection = _database.OpenConnection();
        EnsureProgramSettingsTable(connection);

        return new ProgramWideSettings
        {
            RepoRootPath = ReadValue(connection, nameof(ProgramWideSettings.RepoRootPath)) ?? ProgramWideSettings.GetDefaultRepoRoot(),
            RepoOrganization = ParseEnum(ReadValue(connection, nameof(ProgramWideSettings.RepoOrganization)), RepoOrganizationStrategy.GameRepoWithPerModFolders),
            LastSelectedGameFolder = ReadValue(connection, nameof(ProgramWideSettings.LastSelectedGameFolder)) ?? string.Empty,
            GitHubAccount = ReadValue(connection, nameof(ProgramWideSettings.GitHubAccount)) ?? string.Empty,
            GitHubToken = ReadValue(connection, nameof(ProgramWideSettings.GitHubToken)) ?? string.Empty,
            GitHubModRootRepo = ReadValue(connection, nameof(ProgramWideSettings.GitHubModRootRepo)) ?? string.Empty,
            ExitSyncPreference = ParseEnum(ReadValue(connection, nameof(ProgramWideSettings.ExitSyncPreference)), ExitSyncPreference.Prompt),
            ModFocusSyncPreference = ParseEnum(ReadValue(connection, nameof(ProgramWideSettings.ModFocusSyncPreference)), ModFocusSyncPreference.Prompt),
            TimedAutoSyncEnabled = ParseBool(ReadValue(connection, nameof(ProgramWideSettings.TimedAutoSyncEnabled)), false),
            TimedAutoSyncIntervalMinutes = ParseInt(ReadValue(connection, nameof(ProgramWideSettings.TimedAutoSyncIntervalMinutes)), 10, 1)
        };
    }

    public void Save(ProgramWideSettings settings)
    {
        _database.EnsureCreated();

        using var connection = _database.OpenConnection();
        EnsureProgramSettingsTable(connection);

        UpsertValue(connection, nameof(ProgramWideSettings.RepoRootPath), settings.RepoRootPath);
        UpsertValue(connection, nameof(ProgramWideSettings.RepoOrganization), settings.RepoOrganization.ToString());
        UpsertValue(connection, nameof(ProgramWideSettings.LastSelectedGameFolder), settings.LastSelectedGameFolder);
        UpsertValue(connection, nameof(ProgramWideSettings.GitHubAccount), settings.GitHubAccount);
        UpsertValue(connection, nameof(ProgramWideSettings.GitHubToken), settings.GitHubToken);
        UpsertValue(connection, nameof(ProgramWideSettings.GitHubModRootRepo), settings.GitHubModRootRepo);
        UpsertValue(connection, nameof(ProgramWideSettings.ExitSyncPreference), settings.ExitSyncPreference.ToString());
        UpsertValue(connection, nameof(ProgramWideSettings.ModFocusSyncPreference), settings.ModFocusSyncPreference.ToString());
        UpsertValue(connection, nameof(ProgramWideSettings.TimedAutoSyncEnabled), settings.TimedAutoSyncEnabled ? "1" : "0");
        UpsertValue(connection, nameof(ProgramWideSettings.TimedAutoSyncIntervalMinutes), Math.Max(1, settings.TimedAutoSyncIntervalMinutes).ToString());

        SaveBootstrapSettings(new ProgramBootstrapSettings
        {
            DatabasePath = _database.DatabasePath
        });
    }

    private static void EnsureProgramSettingsTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ProgramSettings (
                SettingKey   TEXT PRIMARY KEY,
                SettingValue TEXT
            );
            """;
        command.ExecuteNonQuery();
    }

    private static string? ReadValue(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SettingValue FROM ProgramSettings WHERE SettingKey = $key LIMIT 1";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void UpsertValue(SqliteConnection connection, string key, string? value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ProgramSettings (SettingKey, SettingValue)
            VALUES ($key, $value)
            ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = excluded.SettingValue;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value ?? string.Empty);
        command.ExecuteNonQuery();
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static bool ParseBool(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (bool.TryParse(value, out var boolResult))
        {
            return boolResult;
        }

        if (int.TryParse(value, out var intResult))
        {
            return intResult != 0;
        }

        return fallback;
    }

    private static int ParseInt(string? value, int fallback, int minValue)
    {
        if (!int.TryParse(value, out var result))
        {
            return fallback;
        }

        return Math.Max(minValue, result);
    }

    private static ProgramBootstrapSettings LoadBootstrapSettings()
    {
        try
        {
            if (!File.Exists(BootstrapPath))
            {
                return new ProgramBootstrapSettings();
            }

            var json = File.ReadAllText(BootstrapPath);
            return JsonSerializer.Deserialize<ProgramBootstrapSettings>(json, JsonOptions) ?? new ProgramBootstrapSettings();
        }
        catch
        {
            return new ProgramBootstrapSettings();
        }
    }

    private static void SaveBootstrapSettings(ProgramBootstrapSettings settings)
    {
        Directory.CreateDirectory(DatabasePaths.GetDatabaseDirectory());
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(BootstrapPath, json);
    }
}
