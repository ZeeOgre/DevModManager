using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DMM.AssetManagers;
using DMM.AssetManagers.MAT;
using DMM.AssetManagers.NIF;
using DMM.AssetManagers.TES;

namespace DMM.Avalonia;

internal sealed record ModDependencyEntry(
    string RelativeDataPath,
    string SourcePath,
    string? XboxRelativePath,
    string? XboxSourcePath,
    string? TifRelativePath,
    string? TifSourcePath);

internal sealed class ModDependencyDiscoveryResult
{
    public List<ModDependencyEntry> Entries { get; } = new();
    public int CollisionCount { get; set; }
    public long ScanMs { get; set; }
}

internal sealed class ModDependencyDiscoveryService
{
    private readonly TESFile _tesFile = new();
    private readonly NifReader _nifReader = new();
    private readonly MAT _matReader = new();

    private static readonly string[] CanonicalBa2Suffixes =
    {
        " - Main.ba2",
        " - Main_xbox.ba2",
        " - Textures.ba2",
        " - Textures_xbox.ba2"
    };

    private static readonly HashSet<string> PluginExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".esm", ".esp", ".esl"
    };

    public ModDependencyDiscoveryResult CollectInitialFiles(
        string scanRoot,
        string gameRoot,
        string modName,
        string primaryPlugin)
    {
        var timer = Stopwatch.StartNew();

        var pluginFiles = CollectCanonicalPluginFiles(scanRoot, modName, primaryPlugin);
        var ba2Files = CanonicalBa2Suffixes
            .Select(suffix => Path.Combine(scanRoot, modName + suffix))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var discoveredCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in pluginFiles)
        {
            DiscoverPluginWalkCandidates(plugin, scanRoot, gameRoot, discoveredCandidates);
        }

        var parentArchiveIndex = BuildParentArchiveIndex(scanRoot, pluginFiles);
        var tifRoot = ResolveTifRoot(gameRoot);
        var result = new ModDependencyDiscoveryResult();

        foreach (var source in pluginFiles.Concat(ba2Files).Concat(discoveredCandidates).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var rel = ToDataRelativePath(scanRoot, gameRoot, source);
            if (rel is null)
            {
                continue;
            }

            if (!source.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase) && parentArchiveIndex.ContainsKey(rel))
            {
                result.CollisionCount++;
                continue;
            }

            var xboxRel = rel.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase) && !rel.Contains("_xbox", StringComparison.OrdinalIgnoreCase)
                ? rel.Replace(".ba2", "_xbox.ba2", StringComparison.OrdinalIgnoreCase)
                : (rel.Contains("_xbox", StringComparison.OrdinalIgnoreCase) ? rel : null);
            var xboxSource = xboxRel is null ? null : ResolveSourceFromDataRelative(scanRoot, gameRoot, xboxRel);

            var tifRel = rel.StartsWith("Data\\Textures\\", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine("TGATextures", Path.ChangeExtension(rel["Data\\Textures\\".Length..], ".tga"))
                : null;
            var tifSource = tifRel is null || string.IsNullOrWhiteSpace(tifRoot)
                ? null
                : ResolveTifSource(tifRoot, tifRel);

            result.Entries.Add(new ModDependencyEntry(rel, source, xboxRel, xboxSource, tifRel, tifSource));
        }

        timer.Stop();
        result.ScanMs = timer.ElapsedMilliseconds;
        return result;
    }

    private void DiscoverPluginWalkCandidates(string pluginFile, string scanRoot, string gameRoot, HashSet<string> candidates)
    {
        TesFileResult tesResult;
        try
        {
            tesResult = _tesFile.Read(pluginFile);
        }
        catch
        {
            return;
        }

        foreach (var script in tesResult.ReferencedScripts)
        {
            TryAddByDataRelative(script, scanRoot, gameRoot, candidates);

            if (script.EndsWith(".pex", StringComparison.OrdinalIgnoreCase))
            {
                TryAddByDataRelative(script[..^4] + ".psc", scanRoot, gameRoot, candidates);
            }
            else if (script.EndsWith(".psc", StringComparison.OrdinalIgnoreCase))
            {
                TryAddByDataRelative(script[..^4] + ".pex", scanRoot, gameRoot, candidates);
            }
        }

        foreach (var audio in tesResult.ReferencedAudio)
        {
            TryAddByDataRelative(audio, scanRoot, gameRoot, candidates);
        }

        foreach (var texture in tesResult.ReferencedTextures)
        {
            TryAddByDataRelative(texture, scanRoot, gameRoot, candidates);
        }

        foreach (var mat in tesResult.ReferencedMats)
        {
            if (TryResolveDataRelative(mat, scanRoot, gameRoot, out var matPath))
            {
                candidates.Add(matPath);
                DiscoverMatTextures(matPath, scanRoot, gameRoot, candidates);
            }
        }

        foreach (var nif in tesResult.ReferencedNifs)
        {
            if (!TryResolveDataRelative(nif, scanRoot, gameRoot, out var nifPath))
            {
                continue;
            }

            candidates.Add(nifPath);

            try
            {
                var nifRead = _nifReader.Read(nifPath);
                foreach (var mat in nifRead.Mats)
                {
                    if (!TryResolveDataRelative(mat, scanRoot, gameRoot, out var matPath))
                    {
                        continue;
                    }

                    candidates.Add(matPath);
                    DiscoverMatTextures(matPath, scanRoot, gameRoot, candidates);
                }
            }
            catch
            {
            }
        }
    }

    private void DiscoverMatTextures(string matPath, string scanRoot, string gameRoot, HashSet<string> candidates)
    {
        try
        {
            var textures = _matReader.ExtractTextures(matPath);
            foreach (var token in textures)
            {
                var normalized = token.Replace('/', '\\').TrimStart('\\');
                var rel = normalized.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                    ? normalized
                    : normalized.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase)
                        ? Path.Combine("Data", normalized)
                        : Path.Combine("Data", "Textures", normalized);

                TryAddByDataRelative(rel, scanRoot, gameRoot, candidates);
            }
        }
        catch
        {
        }
    }

    private static List<string> CollectCanonicalPluginFiles(string scanRoot, string modName, string primaryPlugin)
    {
        var expectedPluginName = $"{modName}{Path.GetExtension(primaryPlugin)}";
        var pluginCandidates = new[]
        {
            Path.Combine(scanRoot, expectedPluginName),
            Path.Combine(scanRoot, $"{modName}.esm"),
            Path.Combine(scanRoot, $"{modName}.esp"),
            Path.Combine(scanRoot, $"{modName}.esl")
        };

        return pluginCandidates
            .Where(File.Exists)
            .Where(path => PluginExtensions.Contains(Path.GetExtension(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, Ba2Entry> BuildParentArchiveIndex(string scanRoot, IReadOnlyList<string> pluginFiles)
    {
        var masterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in pluginFiles)
        {
            foreach (var mast in ExtractMasterNames(plugin))
            {
                masterNames.Add(mast);
            }
        }

        var merged = BA2Archive.BuildMasterArchiveIndex(masterNames, scanRoot);

        foreach (var listedArchive in LoadStarfieldArchiveList(scanRoot))
        {
            if (!File.Exists(listedArchive))
            {
                continue;
            }

            try
            {
                foreach (var kvp in BA2Archive.BuildMergedIndex(new[] { listedArchive }))
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }
            catch
            {
            }
        }

        return merged;
    }

    private static IEnumerable<string> LoadStarfieldArchiveList(string dataRoot)
    {
        var iniPath = Path.Combine(Directory.GetParent(dataRoot)?.FullName ?? dataRoot, "Starfield.ini");
        if (!File.Exists(iniPath))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(iniPath))
        {
            if (!line.Contains('=') || !line.Contains("Archive", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 0 || eq + 1 >= line.Length)
            {
                continue;
            }

            var rhs = line[(eq + 1)..];
            foreach (var item in rhs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!item.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return Path.Combine(dataRoot, item);
            }
        }
    }

    private static IEnumerable<string> ExtractMasterNames(string pluginPath)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(pluginPath);
        }
        catch
        {
            yield break;
        }

        const int recordHeaderSize = 24;
        var pos = 0;
        while (pos + recordHeaderSize <= bytes.Length)
        {
            var sig = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
            var size = BitConverter.ToInt32(bytes, pos + 4);
            if (size < 0)
            {
                yield break;
            }

            if (string.Equals(sig, "TES4", StringComparison.Ordinal))
            {
                var payloadStart = pos + recordHeaderSize;
                var payloadEnd = payloadStart + size;
                if (payloadEnd > bytes.Length)
                {
                    yield break;
                }

                var i = payloadStart;
                while (i + 6 <= payloadEnd)
                {
                    var sub = System.Text.Encoding.ASCII.GetString(bytes, i, 4);
                    var subSize = BitConverter.ToUInt16(bytes, i + 4);
                    i += 6;
                    if (i + subSize > payloadEnd)
                    {
                        break;
                    }

                    if (string.Equals(sub, "MAST", StringComparison.Ordinal))
                    {
                        var master = System.Text.Encoding.UTF8.GetString(bytes, i, subSize).TrimEnd('\0').Trim();
                        if (!string.IsNullOrWhiteSpace(master))
                        {
                            yield return master;
                        }
                    }

                    i += subSize;
                }

                yield break;
            }

            pos += recordHeaderSize + size;
        }
    }

    private static void TryAddByDataRelative(string relDataPath, string scanRoot, string gameRoot, HashSet<string> candidates)
    {
        if (TryResolveDataRelative(relDataPath, scanRoot, gameRoot, out var fullPath))
        {
            candidates.Add(fullPath);
        }
    }

    private static bool TryResolveDataRelative(string relDataPath, string scanRoot, string gameRoot, out string fullPath)
    {
        fullPath = string.Empty;
        var rel = relDataPath.Replace('/', '\\').TrimStart('\\');
        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
        {
            rel = Path.Combine("Data", rel);
        }

        var relUnderData = rel["Data\\".Length..];
        var fromScanRoot = Path.Combine(scanRoot, relUnderData);
        if (File.Exists(fromScanRoot))
        {
            fullPath = fromScanRoot;
            return true;
        }

        var fromGameData = Path.Combine(gameRoot, "Data", relUnderData);
        if (File.Exists(fromGameData))
        {
            fullPath = fromGameData;
            return true;
        }

        return false;
    }

    private static string? ToDataRelativePath(string scanRoot, string gameRoot, string fullPath)
    {
        var full = Path.GetFullPath(fullPath);
        var dataRoot = Path.GetFullPath(scanRoot) + Path.DirectorySeparatorChar;
        if (full.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
        {
            var rel = full[dataRoot.Length..].Replace('/', '\\');
            return NormalizeRel(Path.Combine("Data", rel));
        }

        var gameData = Path.GetFullPath(Path.Combine(gameRoot, "Data")) + Path.DirectorySeparatorChar;
        if (full.StartsWith(gameData, StringComparison.OrdinalIgnoreCase))
        {
            var rel = full[gameData.Length..].Replace('/', '\\');
            return NormalizeRel(Path.Combine("Data", rel));
        }

        return null;
    }

    private static string? ResolveSourceFromDataRelative(string scanRoot, string gameRoot, string relDataPath)
    {
        if (TryResolveDataRelative(relDataPath, scanRoot, gameRoot, out var fullPath))
        {
            return fullPath;
        }

        return null;
    }

    private static string ResolveTifRoot(string gameRoot)
    {
        try
        {
            return Path.GetFullPath(Path.Combine(gameRoot, "..", "..", "Source", "TGATextures"));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? ResolveTifSource(string tifRoot, string tifRelativePath)
    {
        var rel = tifRelativePath.Replace('/', '\\').TrimStart('\\');
        if (!rel.StartsWith("TGATextures\\", StringComparison.OrdinalIgnoreCase))
        {
            rel = Path.Combine("TGATextures", rel);
        }

        var relUnderTga = rel["TGATextures\\".Length..];
        var tgaPath = Path.Combine(tifRoot, relUnderTga);
        if (File.Exists(tgaPath))
        {
            return tgaPath;
        }

        var tifPath = Path.ChangeExtension(tgaPath, ".tif");
        if (File.Exists(tifPath))
        {
            return tifPath;
        }

        return null;
    }

    private static string NormalizeRel(string rel)
    {
        var x = rel.Replace('/', '\\').Trim();
        while (x.StartsWith(".\\", StringComparison.Ordinal)) x = x[2..];
        while (x.StartsWith("\\", StringComparison.Ordinal)) x = x[1..];
        return x;
    }
}
