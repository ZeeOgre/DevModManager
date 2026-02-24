using System.IO.Compression;
using System.Text;

namespace DMM.AssetManagers.BSA;

public static class BsaArchive
{
    private const uint Magic = 0x00415342;
    private const uint HeaderSize = 36;
    private const uint ArchiveFlagCompressed = 0x4;
    private const uint CompressionToggleBit = 0x40000000;

    public static IReadOnlyList<BsaEntry> ReadIndex(string archivePath)
    {
        var map = BuildFileMap(archivePath, out bool archiveCompressedByDefault);
        return map.Select(kvp => new BsaEntry
        {
            ArchivePath = Path.GetFullPath(archivePath),
            FolderPath = Path.GetDirectoryName(kvp.Key)?.Replace('\\', '/') ?? string.Empty,
            FileName = Path.GetFileName(kvp.Key),
            ArchiveInnerPath = kvp.Key,
            StoredSize = kvp.Value.SizeAndFlags & 0x3FFFFFFF,
            UncompressedSize = ReadUncompressedSize(archivePath, kvp.Value, IsCompressed(kvp.Value.SizeAndFlags, archiveCompressedByDefault)),
            IsCompressed = IsCompressed(kvp.Value.SizeAndFlags, archiveCompressedByDefault)
        }).OrderBy(e => e.ArchiveInnerPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static byte[] ExtractFile(string archivePath, string innerPath)
    {
        var map = BuildFileMap(archivePath, out bool archiveCompressedByDefault);
        var normalized = NormalizePath(innerPath);
        if (!map.TryGetValue(normalized, out var record))
            throw new FileNotFoundException($"File '{innerPath}' not found in archive.", archivePath);

        return ReadEntryPayload(archivePath, record, archiveCompressedByDefault);
    }

    public static void ExtractFiles(string archivePath, IEnumerable<string> innerPaths, string outputRoot)
    {
        var map = BuildFileMap(archivePath, out bool archiveCompressedByDefault);
        Directory.CreateDirectory(outputRoot);

        foreach (var rawPath in innerPaths)
        {
            var normalized = NormalizePath(rawPath);
            if (!map.TryGetValue(normalized, out var record))
                continue;

            var outPath = Path.Combine(outputRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllBytes(outPath, ReadEntryPayload(archivePath, record, archiveCompressedByDefault));
        }
    }

    public static void ExtractAll(string archivePath, string outputRoot)
    {
        var map = BuildFileMap(archivePath, out bool archiveCompressedByDefault);
        Directory.CreateDirectory(outputRoot);

        foreach (var kvp in map)
        {
            var outPath = Path.Combine(outputRoot, kvp.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllBytes(outPath, ReadEntryPayload(archivePath, kvp.Value, archiveCompressedByDefault));
        }
    }

    public static void Create(string outputArchivePath, IEnumerable<BsaBuildFile> files, BsaCreateOptions? options = null)
    {
        options ??= new BsaCreateOptions();
        var normalized = files.Select(f => new BsaBuildFile
        {
            ArchivePath = NormalizePath(f.ArchivePath),
            Data = f.Data,
            Compression = f.Compression
        }).OrderBy(f => f.ArchivePath, StringComparer.OrdinalIgnoreCase).ToArray();

        using var fs = File.Create(outputArchivePath);
        using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: true);
        WriteArchive(bw, normalized, options);
    }

    public static void AddOrReplaceFiles(string archivePath, IEnumerable<BsaBuildFile> replacements, BsaCreateOptions? options = null)
    {
        options ??= new BsaCreateOptions();
        var existing = ReadAllFiles(archivePath).ToDictionary(x => x.ArchivePath, StringComparer.OrdinalIgnoreCase);
        foreach (var replacement in replacements)
        {
            var norm = NormalizePath(replacement.ArchivePath);
            existing[norm] = new BsaBuildFile { ArchivePath = norm, Data = replacement.Data, Compression = replacement.Compression };
        }

        var tempPath = archivePath + ".tmp";
        Create(tempPath, existing.Values, options);
        File.Move(tempPath, archivePath, overwrite: true);
    }

    private static IReadOnlyList<BsaBuildFile> ReadAllFiles(string archivePath)
    {
        var map = BuildFileMap(archivePath, out bool archiveCompressedByDefault);
        return map.Select(kvp => new BsaBuildFile
        {
            ArchivePath = kvp.Key,
            Data = ReadEntryPayload(archivePath, kvp.Value, archiveCompressedByDefault),
            Compression = IsCompressed(kvp.Value.SizeAndFlags, archiveCompressedByDefault)
                ? BsaCompressionMode.Compressed
                : BsaCompressionMode.Uncompressed
        }).ToArray();
    }

    private static void WriteArchive(BinaryWriter bw, IReadOnlyList<BsaBuildFile> files, BsaCreateOptions options)
    {
        var folders = files.GroupBy(f => Path.GetDirectoryName(f.ArchivePath)?.Replace('\\', '/') ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FolderData(g.Key, g.ToArray())).ToArray();

        uint archiveFlags = options.ArchiveCompressedByDefault ? ArchiveFlagCompressed : 0u;
        uint totalFolderNameLength = (uint)folders.Sum(f => Encoding.UTF8.GetByteCount(f.FolderPath) + 1);
        uint totalFileNameLength = (uint)files.Sum(f => Encoding.UTF8.GetByteCount(Path.GetFileName(f.ArchivePath)) + 1);

        bw.Write(Magic);
        bw.Write((uint)options.Version);
        bw.Write(HeaderSize);
        bw.Write(archiveFlags);
        bw.Write((uint)folders.Length);
        bw.Write((uint)files.Count);
        bw.Write(totalFolderNameLength);
        bw.Write(totalFileNameLength);
        bw.Write(0u);

        long folderRecordsPos = bw.BaseStream.Position;
        foreach (var folder in folders)
        {
            bw.Write(0UL);
            bw.Write((uint)folder.Files.Length);
            bw.Write(0u);
        }

        var pending = new List<PendingRecord>(files.Count);
        int fileIndex = 0;
        for (int folderIndex = 0; folderIndex < folders.Length; folderIndex++)
        {
            var folder = folders[folderIndex];
            long folderBlockOffset = bw.BaseStream.Position;
            WriteBsaFolderName(bw, folder.FolderPath);
            foreach (var file in folder.Files)
            {
                long recordPos = bw.BaseStream.Position;
                bw.Write(0UL);
                bw.Write(0u);
                bw.Write(0u);
                pending.Add(new PendingRecord(file, recordPos, fileIndex));
                fileIndex++;
            }

            long cur = bw.BaseStream.Position;
            bw.BaseStream.Position = folderRecordsPos + (folderIndex * 16) + 12;
            bw.Write((uint)folderBlockOffset);
            bw.BaseStream.Position = cur;
        }

        foreach (var file in files)
            WriteNullTerminatedString(bw, Path.GetFileName(file.ArchivePath));

        foreach (var item in pending)
        {
            bool compress = ResolveCompression(item.File, options);
            byte[] payload = compress ? BuildCompressedPayload(item.File.Data) : item.File.Data;
            uint sizeAndFlags = (uint)payload.Length;
            if (compress != options.ArchiveCompressedByDefault)
                sizeAndFlags |= CompressionToggleBit;

            uint dataOffset = (uint)bw.BaseStream.Position;
            bw.Write(payload);

            long cur = bw.BaseStream.Position;
            bw.BaseStream.Position = item.RecordPosition;
            bw.Write(0UL);
            bw.Write(sizeAndFlags);
            bw.Write(dataOffset);
            bw.BaseStream.Position = cur;
        }
    }

    private static bool ResolveCompression(BsaBuildFile file, BsaCreateOptions options)
        => file.Compression switch
        {
            BsaCompressionMode.InheritArchive => options.ArchiveCompressedByDefault,
            BsaCompressionMode.Compressed => true,
            BsaCompressionMode.Uncompressed => false,
            BsaCompressionMode.Smart => EvaluateSmartCompression(file.ArchivePath, options.TargetPlatform),
            _ => options.ArchiveCompressedByDefault
        };

    private static bool EvaluateSmartCompression(string archivePath, BsaTargetPlatform targetPlatform)
    {
        string ext = Path.GetExtension(archivePath).ToLowerInvariant();
        return ext switch
        {
            ".wem" => false,
            ".dds" when targetPlatform == BsaTargetPlatform.Xbox => false,
            ".dds" => true,
            _ => true
        };
    }

    private static Dictionary<string, RawFileRecord> BuildFileMap(string archivePath, out bool archiveCompressedByDefault)
    {
        using var fs = File.OpenRead(archivePath);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
        var header = ReadHeader(br);
        archiveCompressedByDefault = header.ArchiveCompressedByDefault;

        var folderRecords = new uint[header.FolderCount];
        for (int i = 0; i < folderRecords.Length; i++)
        {
            _ = br.ReadUInt64();
            folderRecords[i] = br.ReadUInt32();
            _ = br.ReadUInt32();
        }

        var records = new List<RawFileRecord>((int)header.FileCount);
        for (int i = 0; i < folderRecords.Length; i++)
        {
            string folderName = ReadBsaFolderName(br);
            uint fileCount = folderRecords[i];
            for (int j = 0; j < fileCount; j++)
            {
                _ = br.ReadUInt64();
                uint sizeAndFlags = br.ReadUInt32();
                uint dataOffset = br.ReadUInt32();
                records.Add(new RawFileRecord(folderName, sizeAndFlags, dataOffset));
            }
        }

        foreach (var record in records)
            record.FileName = ReadNullTerminatedString(br);

        return records.ToDictionary(r => NormalizePath(Path.Combine(r.FolderPath, r.FileName!)), StringComparer.OrdinalIgnoreCase);
    }

    private static byte[] ReadEntryPayload(string archivePath, RawFileRecord record, bool archiveCompressedByDefault)
    {
        using var fs = File.OpenRead(archivePath);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
        fs.Position = record.DataOffset;
        uint storedSize = record.SizeAndFlags & 0x3FFFFFFF;
        bool isCompressed = IsCompressed(record.SizeAndFlags, archiveCompressedByDefault);

        if (!isCompressed)
            return br.ReadBytes((int)storedSize);

        int uncompressedSize = br.ReadInt32();
        byte[] compressed = br.ReadBytes((int)storedSize - 4);
        return Decompress(compressed, uncompressedSize);
    }

    private static long ReadUncompressedSize(string archivePath, RawFileRecord record, bool compressed)
    {
        uint storedSize = record.SizeAndFlags & 0x3FFFFFFF;
        if (!compressed)
            return storedSize;

        using var fs = File.OpenRead(archivePath);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
        fs.Position = record.DataOffset;
        return br.ReadInt32();
    }

    private static Header ReadHeader(BinaryReader br)
    {
        if (br.ReadUInt32() != Magic)
            throw new InvalidDataException("Not a BSA archive.");

        _ = br.ReadUInt32();
        if (br.ReadUInt32() != HeaderSize)
            throw new InvalidDataException("Unsupported BSA header size.");

        uint flags = br.ReadUInt32();
        uint folderCount = br.ReadUInt32();
        uint fileCount = br.ReadUInt32();
        _ = br.ReadUInt32();
        _ = br.ReadUInt32();
        _ = br.ReadUInt32();

        return new Header(folderCount, fileCount, (flags & ArchiveFlagCompressed) != 0);
    }

    private static bool IsCompressed(uint sizeAndFlags, bool archiveCompressedByDefault)
        => archiveCompressedByDefault ^ ((sizeAndFlags & CompressionToggleBit) != 0);

    private static string ReadBsaFolderName(BinaryReader br)
    {
        int len = br.ReadByte();
        byte[] data = br.ReadBytes(len);
        return NormalizePath(Encoding.UTF8.GetString(data));
    }

    private static void WriteBsaFolderName(BinaryWriter bw, string folder)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(NormalizePath(folder));
        if (bytes.Length > byte.MaxValue)
            throw new InvalidOperationException("Folder name too long for BSA format.");

        bw.Write((byte)bytes.Length);
        bw.Write(bytes);
    }

    private static string ReadNullTerminatedString(BinaryReader br)
    {
        var bytes = new List<byte>();
        int value;
        while ((value = br.BaseStream.ReadByte()) > 0)
            bytes.Add((byte)value);

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static void WriteNullTerminatedString(BinaryWriter bw, string value)
    {
        bw.Write(Encoding.UTF8.GetBytes(value));
        bw.Write((byte)0);
    }

    private static byte[] BuildCompressedPayload(byte[] data)
    {
        byte[] compressed = Compress(data);
        byte[] payload = new byte[compressed.Length + 4];
        BitConverter.GetBytes(data.Length).CopyTo(payload, 0);
        Buffer.BlockCopy(compressed, 0, payload, 4, compressed.Length);
        return payload;
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(data, 0, data.Length);

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] compressed, int expectedSize)
    {
        using var input = new MemoryStream(compressed);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(expectedSize);
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private sealed record Header(uint FolderCount, uint FileCount, bool ArchiveCompressedByDefault);
    private sealed record FolderData(string FolderPath, BsaBuildFile[] Files);
    private sealed record PendingRecord(BsaBuildFile File, long RecordPosition, int FileIndex);
    private sealed class RawFileRecord(string folderPath, uint sizeAndFlags, uint dataOffset)
    {
        public string FolderPath { get; } = folderPath;
        public uint SizeAndFlags { get; } = sizeAndFlags;
        public uint DataOffset { get; } = dataOffset;
        public string? FileName { get; set; }
    }
}
