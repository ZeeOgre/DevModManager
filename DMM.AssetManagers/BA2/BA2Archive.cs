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
                entries = ReadIndex(full);
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
                foreach (var e in ReadIndex(full))
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

    public static Dictionary<string, Ba2Entry> BuildZipIndex(IEnumerable<string> zipPaths)
    {
        if (zipPaths == null) throw new ArgumentNullException(nameof(zipPaths));

        var index = new Dictionary<string, Ba2Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in zipPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full)) continue;

            try
            {
                using var archive = ZipFile.OpenRead(full);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.FullName) || entry.FullName.EndsWith('/')) continue;

                    var rel = NormalizeRel(entry.FullName);
                    index[rel] = new Ba2Entry
                    {
                        ArchivePath = full,
                        ArchiveInnerPath = NormalizeInnerPath(entry.FullName),
                        RelativePath = rel,
                        FileSize = entry.Length
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to read ZIP '{full}': {ex.Message}");
            }
        }

        return index;
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

    private static bool IsBethesdaBa2Magic(string archivePath)
    {
        using var fs = File.OpenRead(archivePath);
        if (fs.Length < 4)
        {
            return false;
        }

        Span<byte> magic = stackalloc byte[4];
        _ = fs.Read(magic);
        return magic[0] == (byte)'B' && magic[1] == (byte)'T' && magic[2] == (byte)'D' && magic[3] == (byte)'X';
    }

    private static bool TryReadBethesdaBa2Index(string archivePath, out IReadOnlyList<Ba2Entry> entries, out string failureReason)
    {
        entries = Array.Empty<Ba2Entry>();
        failureReason = string.Empty;

        try
        {
            using var fs = File.OpenRead(archivePath);
            using var br = new BinaryReader(fs);

            var magic = br.ReadUInt32();
            if (magic != 0x58445442)
            {
                failureReason = "not BTDX";
                return false;
            }

            _ = br.ReadUInt32();
            var type = new string(br.ReadChars(4));
            var fileCount = br.ReadUInt32();
            var nameTableOffset = br.ReadUInt64();

            if (!string.Equals(type, "GNRL", StringComparison.Ordinal))
            {
                failureReason = $"unsupported BA2 type '{type}'";
                return false;
            }

            var records = new (ulong Offset, uint Packed, uint Unpacked)[fileCount];
            for (var i = 0; i < fileCount; i++)
            {
                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                var dataOffset = br.ReadUInt64();
                var packedSize = br.ReadUInt32();
                var unpackedSize = br.ReadUInt32();
                _ = br.ReadUInt32();
                records[i] = (dataOffset, packedSize, unpackedSize);
            }

            fs.Position = (long)nameTableOffset;
            var list = new List<Ba2Entry>((int)fileCount);
            for (var i = 0; i < fileCount; i++)
            {
                var pathLength = br.ReadUInt16();
                var pathBytes = br.ReadBytes(pathLength);
                var innerPath = NormalizeInnerPath(System.Text.Encoding.UTF8.GetString(pathBytes));
                var rec = records[i];
                var fileSize = rec.Unpacked > 0 ? rec.Unpacked : rec.Packed;

                list.Add(new Ba2Entry
                {
                    ArchivePath = Path.GetFullPath(archivePath),
                    ArchiveInnerPath = innerPath,
                    RelativePath = NormalizeRel(innerPath),
                    FileSize = fileSize,
                    DataOffset = (long)rec.Offset,
                    PackedSize = rec.Packed,
                    UnpackedSize = rec.Unpacked
                });
            }

            entries = list;
            return true;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            entries = Array.Empty<Ba2Entry>();
            return false;
        }
    }

    private static byte[] ExtractBethesdaBa2File(Ba2Entry entry)
    {
        using var fs = File.OpenRead(entry.ArchivePath);
        fs.Position = entry.DataOffset;

        var storedSize = entry.PackedSize > 0 ? entry.PackedSize : entry.UnpackedSize;
        if (storedSize == 0)
        {
            return Array.Empty<byte>();
        }

        var raw = new byte[storedSize];
        var read = fs.Read(raw, 0, raw.Length);
        if (read != raw.Length)
        {
            throw new EndOfStreamException($"Expected {raw.Length} bytes but read {read} from '{entry.ArchivePath}'.");
        }

        if (entry.PackedSize == 0 || entry.PackedSize == entry.UnpackedSize)
        {
            return raw;
        }

        using var input = new MemoryStream(raw, writable: false);
        using var z = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        z.CopyTo(output);
        return output.ToArray();
    }

    private static string NormalizeRel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Data\\";

        var rel = raw.Trim().Replace('/', '\\');
        while (rel.StartsWith("\\", StringComparison.Ordinal)) rel = rel[1..];
        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
            rel = Path.Combine("Data", rel);

        return rel;
    }

    private static string NormalizeInnerPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var p = raw.Trim().Replace('/', '\\');
        while (p.StartsWith("\\", StringComparison.Ordinal)) p = p[1..];
        return p;
    }
}
