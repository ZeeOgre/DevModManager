using System;
using System.IO;
using System.Linq;

namespace DMM.Core;

public static class ModRepositoryPathService
{
    public static string BuildModRepoRoot(string repoRoot, string gameName, string modName, bool usePerModRepoLayout)
    {
        var safeGameName = SanitizePathSegment(gameName);
        var safeModName = SanitizePathSegment(modName);
        var gameRoot = Path.Combine(repoRoot, safeGameName);

        return usePerModRepoLayout
            ? Path.Combine(gameRoot, "mods", safeModName)
            : Path.Combine(gameRoot, safeModName);
    }

    public static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Unnamed" : cleaned;
    }
}
