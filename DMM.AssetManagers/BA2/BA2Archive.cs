using System.Collections.Concurrent;
using System.IO.Compression;

namespace DMM.AssetManagers;

public sealed class Ba2Entry
{
    public string ArchivePath { get; init; } = "";
    public string RelativePath { get; init; } = "";
    public string ArchiveInnerPath { get; init; } = "";
    public long FileSize { get; init; } = -1;
    public long DataOffset { get; init; } = -1;
    public uint PackedSize { get; init; }
    public uint UnpackedSize { get; init; }
    public string BethesdaArchiveType { get; init; } = "GNRL";

    public override string ToString()
        => $"{RelativePath} (size={FileSize}, from={Path.GetFileName(ArchivePath)})";
}

public static partial class BA2Archive
{
    public sealed class Ba2IndexBuildStats
    {
        public int MasterCount { get; set; }
        public int ArchivePathCount { get; set; }
        public int ZipPathCount { get; set; }
        public int IndexedFileCount { get; set; }
        public long IndexedBytes { get; set; }
        public long EstimatedRecordBytes { get; set; }
        public int NonBa2CandidateCount { get; set; }
        public List<string> NonBa2CandidateSamples { get; } = new();
        public int ReadFailureCount { get; set; }
        public List<string> ReadFailureSamples { get; } = new();
        public int AttemptedArchiveCount { get; set; }
        public List<string> AttemptedArchiveSamples { get; } = new();
        public string? LastArchiveCandidate { get; set; }
        public string? LastArchiveOutcome { get; set; }
    }

    public static IReadOnlyList<Ba2Entry> ReadIndex(string ba2Path)
    {
        if (ba2Path is null) throw new ArgumentNullException(nameof(ba2Path));
        if (!File.Exists(ba2Path)) throw new FileNotFoundException("BA2 archive not found", ba2Path);

        if (TryReadBethesdaBa2Index(ba2Path, out var bethesdaEntries, out var bethesdaFailure))
        {
            return bethesdaEntries;
        }

        if (IsBethesdaBa2Magic(ba2Path))
        {
            throw new NotSupportedException($"BA2 archive '{ba2Path}' could not be indexed by current Bethesda reader: {bethesdaFailure}");
        }

        return ReadBuildIndex(ba2Path)
            .Select(x => new Ba2Entry
            {
                ArchivePath = x.ArchivePath,
                ArchiveInnerPath = x.ArchiveInnerPath,
                RelativePath = NormalizeRel(x.ArchiveInnerPath),
                FileSize = x.UncompressedSize,
                DataOffset = -1,
                PackedSize = (uint)Math.Max(0, x.StoredSize),
                UnpackedSize = (uint)Math.Max(0, x.UncompressedSize)
            })
            .ToArray();
    }


    private sealed record CachedArchiveIndex(long Length, DateTime LastWriteTimeUtc, IReadOnlyList<Ba2Entry> Entries);

    private static readonly ConcurrentDictionary<string, CachedArchiveIndex> ArchiveIndexCache = new(StringComparer.OrdinalIgnoreCase);

    public static void ClearArchiveIndexCache() => ArchiveIndexCache.Clear();

    private static IReadOnlyList<Ba2Entry> ReadIndexCached(string ba2Path)
    {
        var fullPath = Path.GetFullPath(ba2Path);
        var info = new FileInfo(fullPath);
        var currentLength = info.Length;
        var currentWriteTime = info.LastWriteTimeUtc;

        if (ArchiveIndexCache.TryGetValue(fullPath, out var cached) &&
            cached.Length == currentLength &&
            cached.LastWriteTimeUtc == currentWriteTime)
        {
            return cached.Entries;
        }

        var entries = ReadIndex(fullPath);
        ArchiveIndexCache[fullPath] = new CachedArchiveIndex(currentLength, currentWriteTime, entries);
        return entries;
    }

    public static Dictionary<string, Ba2Entry> BuildMergedIndex(IEnumerable<string> ba2Paths)
    {
        if (ba2Paths == null) throw new ArgumentNullException(nameof(ba2Paths));

        var index = new Dictionary<string, Ba2Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ba2Paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full)) continue;

            if (!LooksLikeBa2Archive(full, out var signatureReason))
            {
                Console.WriteLine($"[WARN] Skipping non-BA2 file '{full}': {signatureReason}");
                continue;
            }

            IReadOnlyList<Ba2Entry> entries;
            try
            {
                entries = ReadIndexCached(full);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to read BA2 '{full}': {ex.Message}");
                continue;
            }

            foreach (var e in entries)
            {
                index[e.RelativePath] = e;
            }
        }

        return index;
    }

    public static Dictionary<string, Ba2Entry> BuildMasterArchiveIndex(IEnumerable<string> masterNames, string dataRoot)
        => BuildMasterArchiveIndex(masterNames, dataRoot, out _);

    public static Dictionary<string, Ba2Entry> BuildMasterArchiveIndex(
        IEnumerable<string> masterNames,
        string dataRoot,
        out Ba2IndexBuildStats stats)
    {
        if (masterNames == null) throw new ArgumentNullException(nameof(masterNames));
        if (dataRoot == null) throw new ArgumentNullException(nameof(dataRoot));

        var normalizedMasters = masterNames.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var ba2Paths = new List<string>();

        foreach (var master in normalizedMasters)
        {
            var baseName = Path.GetFileNameWithoutExtension(master);
            if (string.IsNullOrWhiteSpace(baseName)) continue;

            try
            {
                ba2Paths.AddRange(Directory.GetFiles(dataRoot, baseName + "*.ba2", SearchOption.TopDirectoryOnly));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to enumerate BA2s for master '{master}': {ex.Message}");
            }
        }

        stats = new Ba2IndexBuildStats
        {
            MasterCount = normalizedMasters.Count,
            ArchivePathCount = ba2Paths.Distinct(StringComparer.OrdinalIgnoreCase).Count()
        };

        var merged = new Dictionary<string, Ba2Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var full in ba2Paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(Path.GetFullPath))
        {
            stats.AttemptedArchiveCount++;
            stats.LastArchiveCandidate = full;

            if (!TryValidateBa2Path(full, out var reason))
            {
                stats.NonBa2CandidateCount++;
                stats.LastArchiveOutcome = $"SKIPPED: {reason}";
                if (stats.NonBa2CandidateSamples.Count < 5)
                {
                    stats.NonBa2CandidateSamples.Add($"{full} :: {reason}");
                }
                if (stats.AttemptedArchiveSamples.Count < 10)
                {
                    stats.AttemptedArchiveSamples.Add($"SKIPPED :: {full} :: {reason}");
                }
                continue;
            }

            try
            {
                foreach (var e in ReadIndexCached(full))
                {
                    merged[e.RelativePath] = e;
                }

                stats.LastArchiveOutcome = "INDEXED";
                if (stats.AttemptedArchiveSamples.Count < 10)
                {
                    stats.AttemptedArchiveSamples.Add($"INDEXED :: {full}");
                }
            }
            catch (Exception ex)
            {
                stats.ReadFailureCount++;
                stats.LastArchiveOutcome = $"FAILED: {ex.Message}";
                if (stats.ReadFailureSamples.Count < 5)
                {
                    stats.ReadFailureSamples.Add($"{full} :: {ex.Message}");
                }
                if (stats.AttemptedArchiveSamples.Count < 10)
                {
                    stats.AttemptedArchiveSamples.Add($"FAILED :: {full} :: {ex.Message}");
                }
                Console.WriteLine($"[WARN] Failed to read BA2 '{full}': {ex.Message}");
            }
        }

        stats.IndexedFileCount = merged.Count;
        stats.IndexedBytes = merged.Values.Where(x => x.FileSize > 0).Sum(x => x.FileSize);
        return merged;
    }

    public static bool TryValidateBa2Path(string path, out string reason)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "path is empty";
            return false;
        }

        if (!File.Exists(path))
        {
            reason = "file does not exist";
            return false;
        }

        return LooksLikeBa2Archive(path, out reason);
    }

    public static bool IsLooseFileAlreadyPacked(
        string gameRoot,
        string relPath,
        IReadOnlyDictionary<string, Ba2Entry> mergedIndex,
        out Ba2Entry? matchedEntry)
    {
        if (gameRoot is null) throw new ArgumentNullException(nameof(gameRoot));
        if (relPath is null) throw new ArgumentNullException(nameof(relPath));
        if (mergedIndex is null) throw new ArgumentNullException(nameof(mergedIndex));

        matchedEntry = null;
        var normRel = NormalizeRel(relPath);

        if (!mergedIndex.TryGetValue(normRel, out var entry)) return false;
        matchedEntry = entry;

        var fullLoose = Path.Combine(gameRoot, normRel);
        if (!File.Exists(fullLoose)) return false;

        var looseSize = new FileInfo(fullLoose).Length;
        if (entry.FileSize >= 0 && entry.FileSize != looseSize) return false;

        // For chunked Bethesda texture archives (DX10/GNMF), we currently compare by canonical
        // path + logical unpacked size match and treat that as already packed.
        if (!string.Equals(entry.BethesdaArchiveType, "GNRL", StringComparison.Ordinal))
        {
            return true;
        }

        using var looseStream = File.OpenRead(fullLoose);
        using var archiveStream = OpenArchiveEntryStream(entry);

        if (!archiveStream.CanRead || !archiveStream.CanSeek)
            return FullCompareSIMD(looseStream, archiveStream);

        return FastApproxEqual(looseStream, archiveStream);
    }

    private static Stream OpenArchiveEntryStream(Ba2Entry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));

        if (entry.ArchivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(entry.ArchivePath);
            var zipEntry = archive.GetEntry(entry.ArchiveInnerPath.Replace('\\', '/'));
            if (zipEntry == null)
                throw new FileNotFoundException("Asset not found in ZIP: " + entry.ArchiveInnerPath, entry.ArchivePath);

            var ms = new MemoryStream();
            using var zs = zipEntry.Open();
            zs.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        if (!LooksLikeBa2Archive(entry.ArchivePath, out var signatureReason))
            throw new InvalidDataException($"Expected BA2 archive but got '{entry.ArchivePath}' ({signatureReason}).");

        if (entry.DataOffset >= 0)
        {
            var bytes = ExtractBethesdaBa2File(entry);
            return new MemoryStream(bytes, writable: false);
        }

        var builtBytes = ExtractBuiltFile(entry.ArchivePath, entry.ArchiveInnerPath);
        return new MemoryStream(builtBytes, writable: false);
    }

    private static bool FastApproxEqual(Stream a, Stream b)
    {
        if (!a.CanRead || !b.CanRead || !a.CanSeek || !b.CanSeek)
            return FullCompareSIMD(a, b);

        if (a.Length != b.Length) return false;

        const int blockSize = 4096;
        var bufA = new byte[blockSize];
        var bufB = new byte[blockSize];

        if (!ReadAndCompareBlock(a, b, 0, bufA, bufB)) return false;

        if (a.Length > blockSize)
        {
            var tailPos = a.Length - blockSize;
            if (!ReadAndCompareBlock(a, b, tailPos, bufA, bufB)) return false;
        }

        if (a.Length > 2 * blockSize)
        {
            var midPos = a.Length / 2;
            if (!ReadAndCompareBlock(a, b, midPos, bufA, bufB)) return false;
        }

        return true;
    }

    private static bool ReadAndCompareBlock(Stream a, Stream b, long position, byte[] bufA, byte[] bufB)
    {
        const int blockSize = 4096;
        a.Position = position;
        b.Position = position;

        var readA = a.Read(bufA, 0, blockSize);
        var readB = b.Read(bufB, 0, blockSize);
        if (readA != readB) return false;

        return bufA.AsSpan(0, readA).SequenceEqual(bufB.AsSpan(0, readB));
    }

    private static bool FullCompareSIMD(Stream a, Stream b)
    {
        if (a.CanSeek && b.CanSeek && a.Length != b.Length) return false;

        const int bufSize = 65536;
        var bufA = new byte[bufSize];
        var bufB = new byte[bufSize];

        if (a.CanSeek) a.Position = 0;
        if (b.CanSeek) b.Position = 0;

        while (true)
        {
            var readA = a.Read(bufA, 0, bufSize);
            var readB = b.Read(bufB, 0, bufSize);
            if (readA != readB) return false;
            if (readA == 0) break;
            if (!bufA.AsSpan(0, readA).SequenceEqual(bufB.AsSpan(0, readB))) return false;
        }

        return true;
    }


    private static bool LooksLikeBa2Archive(string path, out string reason)
    {
        reason = string.Empty;
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < 4)
            {
                reason = "file too small";
                return false;
            }

            Span<byte> magic = stackalloc byte[4];
            _ = fs.Read(magic);
            var isBethesdaBa2 = magic[0] == (byte)'B' && magic[1] == (byte)'T' && magic[2] == (byte)'D' && magic[3] == (byte)'X';
            var isLegacyBuildArchive = magic[0] == (byte)'B' && magic[1] == (byte)'S' && magic[2] == (byte)'A' && magic[3] == 0;
            if (!isBethesdaBa2 && !isLegacyBuildArchive)
            {
                reason = $"invalid magic 0x{magic[0]:X2}{magic[1]:X2}{magic[2]:X2}{magic[3]:X2}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }


}
