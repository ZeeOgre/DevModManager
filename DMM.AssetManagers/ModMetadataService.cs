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
        IReadOnlyList<ModDependencyEntry> entries)
    {
        var metadataFolder = Path.Combine(modRepoRoot, "metadata");
        Directory.CreateDirectory(metadataFolder);

        var achlistPath = Path.Combine(metadataFolder, $"{modName}.achlist");
        var achlist = entries
            .Select(x => x.RelativeDataPath)
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
    }
}
