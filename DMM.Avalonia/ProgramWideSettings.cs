using System;
using System.IO;
using System.Text.Json;
using DMM.Data;

namespace DMM.Avalonia;

public sealed class ProgramWideSettings
{
    public string RepoRootPath { get; set; } = GetDefaultRepoRoot();
    public RepoOrganizationStrategy RepoOrganization { get; set; } = RepoOrganizationStrategy.GameRepoWithPerModFolders;

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

public sealed class ProgramWideSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsPath => Path.Combine(DatabasePaths.GetDatabaseDirectory(), "program-settings.json");

    public ProgramWideSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new ProgramWideSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<ProgramWideSettings>(json, JsonOptions);
            return loaded ?? new ProgramWideSettings();
        }
        catch
        {
            return new ProgramWideSettings();
        }
    }

    public void Save(ProgramWideSettings settings)
    {
        Directory.CreateDirectory(DatabasePaths.GetDatabaseDirectory());
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
