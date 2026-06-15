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
        ModDependencyDiscoveryResult? discovery = null,
        IReadOnlyCollection<string>? warnEntries = null,
        IReadOnlyCollection<string>? discardEntries = null)
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

        // Write .achlist_warn if warn entries are provided
        if (warnEntries != null && warnEntries.Count > 0)
        {
            var achlistWarnPath = Path.Combine(metadataFolder, $"{modName}.achlist_warn");
            var warnList = warnEntries
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var achlistWarnJson = JsonSerializer.Serialize(warnList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(achlistWarnPath, achlistWarnJson);
        }

        // Write .achlist_discard if discard entries are provided
        if (discardEntries != null && discardEntries.Count > 0)
        {
            var achlistDiscardPath = Path.Combine(metadataFolder, $"{modName}.achlist_discard");
            var discardList = discardEntries
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var achlistDiscardJson = JsonSerializer.Serialize(discardList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(achlistDiscardPath, achlistDiscardJson);
        }

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
                    ps5Path = x.Ps5RelativePath,
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
