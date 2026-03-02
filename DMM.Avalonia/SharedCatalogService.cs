using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DMM.Avalonia;

internal sealed class SharedCatalogService
{
    public SharedCatalogDocument Load(string repoRoot)
    {
        var path = GetPath(repoRoot);
        if (!File.Exists(path))
        {
            return new SharedCatalogDocument();
        }

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<SharedCatalogDocument>(json);
            return document ?? new SharedCatalogDocument();
        }
        catch
        {
            return new SharedCatalogDocument();
        }
    }

    public void UpsertEntry(string repoRoot, SharedCatalogEntry entry)
    {
        var document = Load(repoRoot);
        var existingIndex = document.Mods.FindIndex(x =>
            string.Equals(x.GameId, entry.GameId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ModName, entry.ModName, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            document.Mods[existingIndex] = entry;
        }
        else
        {
            document.Mods.Add(entry);
        }

        document.GeneratedUtc = DateTimeOffset.UtcNow.ToString("O");
        document.Mods = document.Mods
            .OrderBy(x => x.GameId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ModName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var path = GetPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? repoRoot);
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static string ToStageDisplayName(string stageBranch)
    {
        if (string.IsNullOrWhiteSpace(stageBranch))
        {
            return "DEV";
        }

        var stage = stageBranch.StartsWith("stage/", StringComparison.OrdinalIgnoreCase)
            ? stageBranch["stage/".Length..]
            : stageBranch;
        return stage.ToUpperInvariant();
    }

    private static string GetPath(string repoRoot) => Path.Combine(repoRoot, ".dmm", "catalog.json");
}

internal sealed class SharedCatalogDocument
{
    public int Version { get; set; } = 1;
    public string GeneratedUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public List<SharedCatalogEntry> Mods { get; set; } = new();
}

internal sealed record SharedCatalogEntry(
    string GameId,
    string ModName,
    string PrimaryPlugin,
    string RepoId,
    string SubmodulePath,
    string RepoUrl,
    string StageBranch);
