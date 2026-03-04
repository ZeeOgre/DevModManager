using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DMM.AssetManagers;
using DMM.AssetManagers.MAT;
using DMM.AssetManagers.NIF;
using DMM.AssetManagers.TES;

namespace DMM.AssetManagers;

public sealed record ModDependencyEntry(
    string RelativeDataPath,
    string SourcePath,
    string? XboxRelativePath,
    string? XboxSourcePath,
    string? TifRelativePath,
    string? TifSourcePath,
    bool ParentArchiveMatch);

public sealed class ModDependencyDiscoveryResult
{
    public List<ModDependencyEntry> Entries { get; } = new();
    public List<string> MissingReferences { get; } = new();
    public List<string> ParentArchiveReferences { get; } = new();
    public List<string> HighProbabilityKeep { get; } = new();
    public List<string> HighProbabilityDiscard { get; } = new();
    public List<string> UndefinedDiscard { get; } = new();
    public List<string> DefiniteKeep { get; } = new();
    public Dictionary<string, string> ArchiveHitKinds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int CollisionCount { get; set; }
    public int ParentMasterCount { get; set; }
    public int ParentArchiveCount { get; set; }
    public int ParentZipCount { get; set; }
    public int ParentIndexedFileCount { get; set; }
    public long ParentIndexedBytes { get; set; }
    public long ParentEstimatedRecordBytes { get; set; }
    public int ParentNonBa2CandidateCount { get; set; }
    public List<string> ParentNonBa2CandidateSamples { get; } = new();
    public int ParentReadFailureCount { get; set; }
    public List<string> ParentReadFailureSamples { get; } = new();
    public int ParentAttemptedArchiveCount { get; set; }
    public List<string> ParentAttemptedArchiveSamples { get; } = new();
    public string? ParentLastArchiveCandidate { get; set; }
    public string? ParentLastArchiveOutcome { get; set; }
    public long ScanMs { get; set; }
}

public sealed class ModDependencyDiscoveryService
{
    private readonly TESFile _tesFile = new();
    private readonly NifReader _nifReader = new();
    private readonly global::DMM.AssetManagers.MAT.MAT _matReader = new();

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
        var unresolvedCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in pluginFiles)
        {
            DiscoverPluginWalkCandidates(plugin, scanRoot, gameRoot, discoveredCandidates, unresolvedCandidates);
            DiscoverConventionCandidates(plugin, scanRoot, gameRoot, modName, discoveredCandidates);
        }

        var parentArchiveIndex = BuildParentArchiveIndex(scanRoot, gameRoot, pluginFiles, out var parentStats, out var catalog);
        ExportParentCatalogCsv(modName, catalog, parentArchiveIndex);
        var tifRoot = ResolveTifRoot(gameRoot);
        var result = new ModDependencyDiscoveryResult();

        foreach (var source in pluginFiles.Concat(ba2Files).Concat(discoveredCandidates).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var rel = ToDataRelativePath(scanRoot, gameRoot, source);
            if (rel is null)
            {
                continue;
            }

            if (ShouldForceDiscardDataRelativeToken(rel))
            {
                result.UndefinedDiscard.Add(rel);
                continue;
            }

            if (!source.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase) && parentArchiveIndex.TryGetValue(rel, out var archiveMatchEntry))
            {
                result.CollisionCount++;
                result.ParentArchiveReferences.Add(rel);
                result.HighProbabilityDiscard.Add(rel);
                result.ArchiveHitKinds[rel] = ClassifyArchiveHitKind(archiveMatchEntry.ArchivePath);
                result.Entries.Add(new ModDependencyEntry(rel, source, null, null, null, null, true));
                continue;
            }
            string? xboxRel = null;
            string? xboxSource = null;
            if (IsXboxMirroredCandidate(rel))
            {
                xboxRel = rel;
                xboxSource = ResolveXboxSourceFromDataRelative(scanRoot, gameRoot, xboxRel);
            }

            string? tifRel = null;
            string? tifSource = null;
            if (rel.StartsWith("Data\\Textures\\", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(tifRoot))
            {
                var tifCandidateRel = Path.Combine("TGATextures", Path.ChangeExtension(rel["Data\\Textures\\".Length..], ".tga"));
                tifSource = ResolveTifSource(tifRoot, tifCandidateRel);
                if (!string.IsNullOrWhiteSpace(tifSource))
                {
                    var relUnderTgaRoot = Path.GetRelativePath(tifRoot, tifSource!).Replace('/', '\\');
                    tifRel = NormalizeRel(Path.Combine("TGATextures", relUnderTgaRoot));
                }
            }

            result.Entries.Add(new ModDependencyEntry(rel, source, xboxRel, xboxSource, tifRel, tifSource, false));
            result.HighProbabilityKeep.Add(rel);
        }

        foreach (var missing in unresolvedCandidates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (ShouldForceDiscardDataRelativeToken(missing))
            {
                result.UndefinedDiscard.Add(missing);
                continue;
            }

            if (parentArchiveIndex.TryGetValue(missing, out var unresolvedArchiveMatch) || result.ParentArchiveReferences.Contains(missing, StringComparer.OrdinalIgnoreCase))
            {
                if (unresolvedArchiveMatch is not null)
                {
                    result.ArchiveHitKinds[missing] = ClassifyArchiveHitKind(unresolvedArchiveMatch.ArchivePath);
                }
                result.UndefinedDiscard.Add(missing);
                continue;
            }

            if (!result.Entries.Any(x => string.Equals(x.RelativeDataPath, missing, StringComparison.OrdinalIgnoreCase)))
            {
                result.MissingReferences.Add(missing);
            }
        }


        result.ParentMasterCount = parentStats.MasterCount;
        result.ParentArchiveCount = parentStats.ArchivePathCount;
        result.ParentZipCount = parentStats.ZipPathCount;
        result.ParentIndexedFileCount = parentStats.IndexedFileCount;
        result.ParentIndexedBytes = parentStats.IndexedBytes;
        result.ParentEstimatedRecordBytes = parentStats.EstimatedRecordBytes;
        result.ParentNonBa2CandidateCount = parentStats.NonBa2CandidateCount;
        result.ParentReadFailureCount = parentStats.ReadFailureCount;
        result.ParentNonBa2CandidateSamples.AddRange(parentStats.NonBa2CandidateSamples);
        result.ParentReadFailureSamples.AddRange(parentStats.ReadFailureSamples);
        result.ParentAttemptedArchiveCount = parentStats.AttemptedArchiveCount;
        result.ParentAttemptedArchiveSamples.AddRange(parentStats.AttemptedArchiveSamples);
        result.ParentLastArchiveCandidate = parentStats.LastArchiveCandidate;
        result.ParentLastArchiveOutcome = parentStats.LastArchiveOutcome;

        foreach (var rel in result.HighProbabilityKeep.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            result.DefiniteKeep.Add(rel);

        result.HighProbabilityKeep.Sort(StringComparer.OrdinalIgnoreCase);
        result.HighProbabilityDiscard.Sort(StringComparer.OrdinalIgnoreCase);
        result.UndefinedDiscard.Sort(StringComparer.OrdinalIgnoreCase);

        timer.Stop();
        result.ScanMs = timer.ElapsedMilliseconds;
        return result;
    }

    private void DiscoverPluginWalkCandidates(string pluginFile, string scanRoot, string gameRoot, HashSet<string> candidates, HashSet<string> unresolvedCandidates)
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
            if (!IsLikelyScriptToken(script))
            {
                continue;
            }

            var scriptCandidates = ExpandScriptCandidates(script).ToList();
            var resolved = false;
            foreach (var candidate in scriptCandidates)
            {
                if (TryResolveDataRelative(candidate, scanRoot, gameRoot, out var fullPath))
                {
                    candidates.Add(fullPath);
                    resolved = true;
                    break;
                }
            }

            if (!resolved && scriptCandidates.Count > 0)
            {
                unresolvedCandidates.Add(NormalizeToDataRelative(scriptCandidates[0]));
            }
        }

        foreach (var audio in tesResult.ReferencedAudio)
        {
            TryAddByDataRelative(audio, scanRoot, gameRoot, candidates, unresolvedCandidates);
        }

        foreach (var texture in tesResult.ReferencedTextures)
        {
            TryAddByDataRelative(texture, scanRoot, gameRoot, candidates, unresolvedCandidates);
        }

        foreach (var mat in tesResult.ReferencedMats)
        {
            if (TryResolveDataRelative(mat, scanRoot, gameRoot, out var matPath))
            {
                candidates.Add(matPath);
                DiscoverMatTextures(matPath, scanRoot, gameRoot, candidates, unresolvedCandidates);
            }
            else
            {
                unresolvedCandidates.Add(NormalizeToDataRelative(mat));
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
                        unresolvedCandidates.Add(NormalizeToDataRelative(mat));
                        continue;
                    }

                    candidates.Add(matPath);
                    DiscoverMatTextures(matPath, scanRoot, gameRoot, candidates, unresolvedCandidates);
                }

                foreach (var mesh in nifRead.Meshes)
                {
                    TryAddByDataRelative(mesh, scanRoot, gameRoot, candidates, unresolvedCandidates);
                }

                foreach (var rig in nifRead.Rigs)
                {
                    TryAddByDataRelative(rig, scanRoot, gameRoot, candidates, unresolvedCandidates);
                }
            }
            catch
            {
            }
        }
    }


    private static bool IsLikelyScriptToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var normalized = token.Replace('/', '\\').TrimStart('\\').Trim();
        if (normalized.Length < 3 || normalized.Length > 180)
            return false;

        var hasLetter = normalized.Any(char.IsLetter);
        if (!hasLetter)
            return false;

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
                continue;

            if (ch is '_' or '-' or '\\' or ':' or '.')
                continue;

            return false;
        }

        var firstSegment = normalized.Split(new[] { '\\', ':' }, 2)[0];
        if (string.IsNullOrWhiteSpace(firstSegment) || firstSegment.Length < 2)
            return false;

        return true;
    }

    private static IEnumerable<string> ExpandScriptCandidates(string scriptToken)
    {
        if (string.IsNullOrWhiteSpace(scriptToken))
            yield break;

        var normalized = scriptToken.Replace('/', '\\').TrimStart('\\').Trim();

        // ScriptName token style in plugins: Namespace:Folder:ScriptName
        if (normalized.Contains(':'))
        {
            normalized = normalized.Replace(':', '\\');
            yield return Path.Combine("Data", "Scripts", normalized + ".pex");
            yield return Path.Combine("Data", "Scripts", "Source", normalized + ".psc");
            yield break;
        }

        if (normalized.EndsWith(".pex", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(".psc", StringComparison.OrdinalIgnoreCase))
        {
            string rel = normalized.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : normalized.StartsWith("Scripts\\", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine("Data", normalized)
                    : Path.Combine("Data", "Scripts", normalized);

            yield return rel;

            if (rel.EndsWith(".pex", StringComparison.OrdinalIgnoreCase))
            {
                var stem = rel[..^4];
                yield return stem + ".psc";
                yield return stem.Replace("Data\\Scripts\\", "Data\\Scripts\\Source\\", StringComparison.OrdinalIgnoreCase) + ".psc";
            }
            else
            {
                var stem = rel[..^4];
                yield return stem + ".pex";
                yield return stem.Replace("Data\\Scripts\\Source\\", "Data\\Scripts\\", StringComparison.OrdinalIgnoreCase) + ".pex";
            }
        }
    }

    private void DiscoverMatTextures(string matPath, string scanRoot, string gameRoot, HashSet<string> candidates, HashSet<string> unresolvedCandidates)
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

                TryAddByDataRelative(rel, scanRoot, gameRoot, candidates, unresolvedCandidates);
            }
        }
        catch
        {
        }
    }


    private static void DiscoverConventionCandidates(string pluginFile, string scanRoot, string gameRoot, string modName, HashSet<string> candidates)
    {
        var pluginName = Path.GetFileName(pluginFile);
        var pluginStem = Path.GetFileNameWithoutExtension(pluginFile);

        foreach (var scriptsRoot in new[] { Path.Combine(scanRoot, "Scripts"), Path.Combine(scanRoot, "Scripts", "Source") })
        {
            if (!Directory.Exists(scriptsRoot))
            {
                continue;
            }

            foreach (var script in Directory.EnumerateFiles(scriptsRoot, "*.*", SearchOption.AllDirectories)
                         .Where(x => x.EndsWith(".psc", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".pex", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileName(script);
                if (name.Contains(pluginStem, StringComparison.OrdinalIgnoreCase) || name.Contains(modName, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(script);
                }
            }
        }

        DiscoverInterfaceIconCandidates(scanRoot, gameRoot, pluginStem, pluginName, modName, candidates);
    }

    private static void DiscoverInterfaceIconCandidates(
        string scanRoot,
        string? gameRoot,
        string pluginStem,
        string pluginName,
        string modName,
        HashSet<string> candidates)
    {
        var roots = new List<string> { scanRoot };
        if (!string.IsNullOrWhiteSpace(gameRoot))
        {
            roots.Add(Path.Combine(gameRoot, "Data"));
        }

        var bucketNames = new[] { "InventoryIcons", "ShipBuilderIcons", "WorkshopIcons" };
        var idCandidates = new[]
        {
            pluginName,
            pluginStem,
            modName,
            pluginStem + ".esm",
            pluginStem + ".esp",
            modName + ".esm",
            modName + ".esp"
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        foreach (var dataRoot in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var bucket in bucketNames)
            {
                foreach (var id in idCandidates)
                {
                    var root = Path.Combine(dataRoot, "Textures", "Interface", bucket, id);
                    if (!Directory.Exists(root)) continue;

                    foreach (var dds in Directory.EnumerateFiles(root, "*.dds", SearchOption.AllDirectories))
                    {
                        candidates.Add(dds);
                    }
                }
            }
        }
    }

    private static List<string> CollectCanonicalPluginFiles(string scanRoot, string modName, string primaryPlugin)
    {
        var expectedPluginName = $"{modName}{Path.GetExtension(primaryPlugin)}";
        var pluginCandidates = new[]
        {
            Path.Combine(scanRoot, primaryPlugin),
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

    private static Dictionary<string, Ba2Entry> BuildParentArchiveIndex(string scanRoot, string gameRoot, IReadOnlyList<string> pluginFiles, out BA2Archive.Ba2IndexBuildStats stats, out ParentCatalogSources catalogSources)
    {
        var masterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in pluginFiles)
        {
            foreach (var mast in ExtractMasterNames(plugin))
            {
                masterNames.Add(mast);
            }
        }

        var merged = BA2Archive.BuildMasterArchiveIndex(masterNames, scanRoot, out stats);

        var listedArchives = LoadStarfieldArchiveList(scanRoot)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Fallback: when INI archive list is unavailable, index all BA2 files under Data
        // (except archives that belong to the mod being scanned) so parent/base matches still work.
        if (listedArchives.Count == 0)
        {
            var currentModPrefixes = pluginFiles
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            listedArchives = Directory.EnumerateFiles(scanRoot, "*.ba2", SearchOption.TopDirectoryOnly)
                .Where(path => !currentModPrefixes.Any(prefix =>
                    Path.GetFileName(path).StartsWith(prefix + " - ", StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        foreach (var listedArchive in listedArchives)
        {
            if (!BA2Archive.TryValidateBa2Path(listedArchive, out var reason))
            {
                stats.NonBa2CandidateCount++;
                if (stats.NonBa2CandidateSamples.Count < 5)
                {
                    stats.NonBa2CandidateSamples.Add($"{listedArchive} :: {reason}");
                }
                continue;
            }

            try
            {
                foreach (var kvp in BA2Archive.BuildMergedIndex(new[] { listedArchive }))
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                stats.ReadFailureCount++;
                if (stats.ReadFailureSamples.Count < 5)
                {
                    stats.ReadFailureSamples.Add($"{listedArchive} :: {ex.Message}");
                }
            }
        }

        var zipCandidates = DiscoverCreationKitZipCandidates(gameRoot);

        if (zipCandidates.Count > 0)
        {
            foreach (var kvp in ZipArchiveIndex.BuildIndex(zipCandidates))
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        stats.ArchivePathCount += listedArchives.Count;
        stats.ZipPathCount = zipCandidates.Count;
        stats.IndexedFileCount = merged.Count;
        stats.IndexedBytes = merged.Values.Where(x => x.FileSize > 0).Sum(x => x.FileSize);
        stats.EstimatedRecordBytes = merged.Sum(x => 64L + (x.Key?.Length ?? 0) * sizeof(char));

        catalogSources = new ParentCatalogSources(masterNames, listedArchives, zipCandidates);
        return merged;
    }



    private sealed record ParentCatalogSources(
        IReadOnlyCollection<string> MasterNames,
        IReadOnlyCollection<string> ArchivePaths,
        IReadOnlyCollection<string> ZipPaths);

    private static void ExportParentCatalogCsv(string modName, ParentCatalogSources catalog, IReadOnlyDictionary<string, Ba2Entry> index)
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                return;
            }

            var exportRoot = Path.Combine(localAppData, "zeeogre", "devmodmanager");
            Directory.CreateDirectory(exportRoot);

            var safeModName = string.IsNullOrWhiteSpace(modName)
                ? "unknown-mod"
                : string.Join("_", modName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrWhiteSpace(safeModName))
            {
                safeModName = "unknown-mod";
            }

            var exportPath = Path.Combine(exportRoot, $"{safeModName}_parentcatalog.csv");
            using var writer = new StreamWriter(exportPath, false);

            WriteSection(writer, "Section 1 - Parent Mods",
                catalog.MasterNames
                    .Where(x => !IsBaseGameMaster(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new[] { x }));

            WriteSection(writer, "Section 2 - Basegame Mods",
                catalog.MasterNames
                    .Where(IsBaseGameMaster)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new[] { x }));

            WriteSection(writer, "Section 3 - Parent Archives",
                catalog.ArchivePaths
                    .Where(x => string.Equals(ClassifyArchiveHitKind(x), "parent-archive", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new[] { x }));

            WriteSection(writer, "Section 4 - Basegame Archives",
                catalog.ArchivePaths
                    .Where(x => string.Equals(ClassifyArchiveHitKind(x), "basegame-archive", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new[] { x }));

            WriteSection(writer, "Section 5 - Creations Zip",
                catalog.ZipPaths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new[] { x }));

            WriteSection(writer, "Section 6 - File Catalog",
                index
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new[]
                    {
                        x.Key,
                        x.Value.ArchivePath,
                        ClassifyArchiveHitKind(x.Value.ArchivePath)
                    }),
                "filename", "source", "source_type");
        }
        catch
        {
            // Debug export should never block dependency discovery.
        }
    }

    private static bool IsBaseGameMaster(string name)
    {
        var fileName = Path.GetFileName(name);
        return fileName.StartsWith("Starfield", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteSection(StreamWriter writer, string title, IEnumerable<string[]> rows, params string[]? header)
    {
        writer.WriteLine(EscapeCsv(title));

        if (header is { Length: > 0 })
        {
            writer.WriteLine(string.Join(',', header.Select(EscapeCsv)));
        }

        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(',', row.Select(EscapeCsv)));
        }

        writer.WriteLine();
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains('"') && !text.Contains(',') && !text.Contains('\n') && !text.Contains('\r'))
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static List<string> DiscoverCreationKitZipCandidates(string gameRoot)
    {
        var candidateRoots = new[]
        {
            gameRoot,
            Directory.GetParent(gameRoot)?.FullName ?? gameRoot,
            Path.Combine(gameRoot, "Tools"),
            Path.Combine(gameRoot, "tools")
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in candidateRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var canonical = Path.Combine(root, "ContentResources.zip");
            if (File.Exists(canonical))
            {
                candidates.Add(canonical);
            }

            try
            {
                foreach (var zip in Directory.EnumerateFiles(root, "*.zip", SearchOption.TopDirectoryOnly))
                {
                    candidates.Add(zip);
                }
            }
            catch
            {
                // Continue scanning remaining roots.
            }
        }

        return candidates
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ClassifyArchiveHitKind(string archivePath)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return "base-ck-zip";

        var file = Path.GetFileName(archivePath);
        if (file.StartsWith("Starfield", StringComparison.OrdinalIgnoreCase))
            return "basegame-archive";

        return "parent-archive";
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

    private static bool ShouldForceDiscardDataRelativeToken(string relDataPath)
    {
        if (string.IsNullOrWhiteSpace(relDataPath))
            return true;

        var normalized = NormalizeToDataRelative(relDataPath);

        // NIF descriptor/tweak fields (for example "BSMaterial::...") are not asset paths.
        if (normalized.Contains("::", StringComparison.Ordinal))
            return true;

        // Resource-handle style pseudo-paths (for example "res:112BA342:...") are identifiers,
        // not loose-file paths we can resolve/copy.
        if (normalized.StartsWith("Data\\Textures\\res:", StringComparison.OrdinalIgnoreCase))
            return true;

        // Windows file names cannot contain ':'; treat these as malformed parse artifacts.
        return normalized.Contains(':');
    }

    private static void TryAddByDataRelative(string relDataPath, string scanRoot, string gameRoot, HashSet<string> candidates, HashSet<string> unresolvedCandidates)
    {
        if (ShouldForceDiscardDataRelativeToken(relDataPath))
        {
            unresolvedCandidates.Add(NormalizeToDataRelative(relDataPath));
            return;
        }

        if (TryResolveDataRelative(relDataPath, scanRoot, gameRoot, out var fullPath))
        {
            candidates.Add(fullPath);
            return;
        }

        unresolvedCandidates.Add(NormalizeToDataRelative(relDataPath));
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

    private static bool IsXboxMirroredCandidate(string relDataPath)
    {
        if (string.IsNullOrWhiteSpace(relDataPath)) return false;
        if (relDataPath.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase)) return false;

        return relDataPath.StartsWith("Data\\Textures\\", StringComparison.OrdinalIgnoreCase)
            || relDataPath.StartsWith("Data\\Sound\\", StringComparison.OrdinalIgnoreCase);
    }


    private static string? ResolveXboxSourceFromDataRelative(string scanRoot, string gameRoot, string relDataPath)
    {
        var rel = relDataPath.Replace('/', '\\').TrimStart('\\');
        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
        {
            rel = Path.Combine("Data", rel);
        }

        var relUnderData = rel["Data\\".Length..];
        var candidateRoots = new[]
        {
            Path.Combine(Path.GetDirectoryName(scanRoot) ?? scanRoot, "XBOX", "Data"),
            Path.Combine(gameRoot, "XBOX", "Data"),
            Path.GetFullPath(Path.Combine(gameRoot, "..", "XBOX", "Data"))
        }
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in candidateRoots)
        {
            try
            {
                var full = Path.GetFullPath(Path.Combine(root, relUnderData));
                var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!full.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(full))
                {
                    var marker = $"{Path.DirectorySeparatorChar}XBOX{Path.DirectorySeparatorChar}Data{Path.DirectorySeparatorChar}";
                    var fullNormalized = full.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                    if (fullNormalized.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        return full;
                    }
                }
            }
            catch
            {
            }
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

    private static string NormalizeToDataRelative(string relDataPath)
    {
        var rel = relDataPath.Replace('/', '\\').TrimStart('\\');
        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
        {
            rel = Path.Combine("Data", rel);
        }

        return NormalizeRel(rel);
    }

    private static string NormalizeRel(string rel)
    {
        var x = rel.Replace('/', '\\').Trim();
        while (x.StartsWith(".\\", StringComparison.Ordinal)) x = x[2..];
        while (x.StartsWith("\\", StringComparison.Ordinal)) x = x[1..];
        return x;
    }
}
