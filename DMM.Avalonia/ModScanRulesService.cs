using System;
using System.Collections.Generic;
using System.IO;

namespace DMM.Avalonia;

internal sealed class ModScanRulesService
{
    private static readonly HashSet<string> StarfieldOfficialPluginBaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "starfield",
        "constellation",
        "blueprintshipsstarfield"
    };

    public bool IsOfficialPluginName(string gameName, string pluginName)
    {
        if (!string.Equals(gameName, "Starfield", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var baseName = Path.GetFileNameWithoutExtension(pluginName);
        return !string.IsNullOrWhiteSpace(baseName) && StarfieldOfficialPluginBaseNames.Contains(baseName);
    }

    public int GetPluginExtensionPriority(string? extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".esm" => 0,
            ".esp" => 1,
            ".esl" => 2,
            _ => 99
        };
    }
}
