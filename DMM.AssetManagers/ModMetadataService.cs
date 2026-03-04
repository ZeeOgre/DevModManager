using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DMM.AssetManagers;

public static class ModMetadataService
{
    public static void WriteMetadataFiles(
        string modRepoRoot,
        string modName,
        string pluginName,
        IReadOnlyList<ModDependencyEntry> entries,
        ModDependencyDiscoveryResult? discovery = null)
    {
        var metadataFolder = Path.Combine(modRepoRoot, "metadata");
        Directory.CreateDirectory(metadataFolder);

        var achlistPath = Path.Combine(metadataFolder, $"{modName}.achlist");
        var achlist = entries
            .Select(x => x.RelativeDataPath)
            .Where(x => !x.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)
                     && !x.EndsWith(".esp", StringComparison.OrdinalIgnoreCase)
                     && !x.EndsWith(".esl", StringComparison.OrdinalIgnoreCase)
                     && !x.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var achlistJson = JsonSerializer.Serialize(achlist, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(achlistPath, achlistJson);

        var catalogPath = Path.Combine(metadataFolder, "catalog.json");
        var catalogPayload = new
        {
            mod = modName,
            plugin = pluginName,
            generatedUtc = DateTimeOffset.UtcNow,
            files = entries
                .OrderBy(x => x.RelativeDataPath, StringComparer.OrdinalIgnoreCase)
                .Select(x => new
                {
                    pcPath = x.RelativeDataPath,
                    xboxPath = x.XboxRelativePath,
                    tifPath = x.TifRelativePath
                })
                .ToList()
        };
        var catalogJson = JsonSerializer.Serialize(catalogPayload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(catalogPath, catalogJson);

        var reviewPath = Path.Combine(metadataFolder, "dependency-review.json");
        var reviewPayload = new
        {
            mod = modName,
            plugin = pluginName,
            generatedUtc = DateTimeOffset.UtcNow,
            highProbabilityKeep = (discovery?.HighProbabilityKeep ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            highProbabilityDiscard = (discovery?.HighProbabilityDiscard ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            undefinedDiscard = (discovery?.UndefinedDiscard ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            definiteKeep = (discovery?.DefiniteKeep ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
        var reviewJson = JsonSerializer.Serialize(reviewPayload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(reviewPath, reviewJson);
    }
}
