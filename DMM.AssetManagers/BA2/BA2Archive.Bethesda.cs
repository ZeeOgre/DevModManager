using System.IO.Compression;

namespace DMM.AssetManagers;

public static partial class BA2Archive
{
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

            var version = br.ReadUInt32();
            if (version is not (1 or 2 or 3 or 7 or 8))
            {
                failureReason = $"unsupported BA2 version '{version}'";
                return false;
            }

            var type = new string(br.ReadChars(4));
            var fileCount = br.ReadUInt32();
            var nameTableOffset = br.ReadUInt64();

            if (!TryReadBethesdaRecords(fs, br, type, version, fileCount, nameTableOffset, out var records, out var readFailure))
            {
                failureReason = readFailure;
                return false;
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
                if (string.Equals(rec.Type, "DX10", StringComparison.Ordinal))
                {
                    // DDS header is reconstructed from metadata in DX10 archives.
                    fileSize += 148;
                }

                list.Add(new Ba2Entry
                {
                    ArchivePath = Path.GetFullPath(archivePath),
                    ArchiveInnerPath = innerPath,
                    RelativePath = NormalizeRel(innerPath),
                    FileSize = fileSize,
                    DataOffset = (long)rec.Offset,
                    PackedSize = rec.Packed,
                    UnpackedSize = rec.Unpacked,
                    BethesdaArchiveType = rec.Type
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

    private static bool TryReadBethesdaRecords(
        FileStream fs,
        BinaryReader br,
        string type,
        uint version,
        uint fileCount,
        ulong nameTableOffset,
        out (ulong Offset, uint Packed, uint Unpacked, string Type)[] records,
        out string failureReason)
    {
        records = Array.Empty<(ulong Offset, uint Packed, uint Unpacked, string Type)>();
        failureReason = string.Empty;

        var candidatePositions = new List<long>();

        void addCandidate(long pos)
        {
            if (pos < 24 || pos >= fs.Length) return;
            if (!candidatePositions.Contains(pos)) candidatePositions.Add(pos);
        }

        addCandidate(24);
        if (version == 2) addCandidate(32);
        if (version == 3)
        {
            addCandidate(32);
            addCandidate(36);
        }

        // Fallback probe (4-byte aligned) for header variants, without reparsing full records per byte.
        var probeEnd = (long)Math.Min(Math.Min(fs.Length, (long)nameTableOffset), 128);
        for (var pos = 24L; pos < probeEnd; pos += 4)
        {
            addCandidate(pos);
        }

        foreach (var position in candidatePositions)
        {
            try
            {
                if (!LooksLikeRecordStart(fs, br, type, position, nameTableOffset))
                {
                    continue;
                }

                fs.Position = position;
                var parsed = type switch
                {
                    "GNRL" => ReadGeneralRecords(br, fileCount),
                    "DX10" => ReadChunkedTextureRecords(br, fileCount, "DX10", 24),
                    "GNMF" => ReadChunkedTextureRecords(br, fileCount, "GNMF", 48),
                    _ => throw new NotSupportedException($"unsupported BA2 type '{type}'")
                };

                if (nameTableOffset > (ulong)fs.Length)
                {
                    throw new InvalidDataException($"name table offset {nameTableOffset} is outside archive length {fs.Length}.");
                }

                if (parsed.Length != fileCount)
                {
                    throw new InvalidDataException($"expected {fileCount} records but parsed {parsed.Length}.");
                }

                records = parsed;
                return true;
            }
            catch (Exception ex)
            {
                failureReason = $"offset {position}: {ex.Message}";
            }
        }

        failureReason = $"{type} record parse failed for all candidate offsets: {failureReason}";
        return false;
    }


    private static bool LooksLikeRecordStart(FileStream fs, BinaryReader br, string type, long position, ulong nameTableOffset)
    {
        var restore = fs.Position;
        try
        {
            if (position < 24 || (ulong)position >= nameTableOffset) return false;

            fs.Position = position;

            if (string.Equals(type, "GNRL", StringComparison.Ordinal))
            {
                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                var dataOffset = br.ReadUInt64();
                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                var sentinel = br.ReadUInt32();
                return sentinel == 0xBAADF00D && dataOffset < nameTableOffset;
            }

            if (string.Equals(type, "DX10", StringComparison.Ordinal) || string.Equals(type, "GNMF", StringComparison.Ordinal))
            {
                var expectedFileHeaderSize = string.Equals(type, "DX10", StringComparison.Ordinal) ? (ushort)24 : (ushort)48;

                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                _ = br.ReadByte();
                var chunkCount = br.ReadByte();
                var fileHeaderSize = br.ReadUInt16();

                if (fileHeaderSize != expectedFileHeaderSize || chunkCount == 0) return false;

                var firstChunkSentinelPos = position + fileHeaderSize + 20;
                if ((ulong)firstChunkSentinelPos + 4 > nameTableOffset) return false;

                fs.Position = firstChunkSentinelPos;
                var sentinel = br.ReadUInt32();
                return sentinel == 0xBAADF00D;
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            fs.Position = restore;
        }
    }

    private static (ulong Offset, uint Packed, uint Unpacked, string Type)[] ReadGeneralRecords(BinaryReader br, uint fileCount)
    {
        var records = new (ulong Offset, uint Packed, uint Unpacked, string Type)[fileCount];
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
            records[i] = (dataOffset, packedSize, unpackedSize, "GNRL");
        }

        return records;
    }

    private static (ulong Offset, uint Packed, uint Unpacked, string Type)[] ReadChunkedTextureRecords(
        BinaryReader br,
        uint fileCount,
        string type,
        ushort expectedFileHeaderSize)
    {
        var records = new (ulong Offset, uint Packed, uint Unpacked, string Type)[fileCount];

        for (var i = 0; i < fileCount; i++)
        {
            _ = br.ReadUInt32();
            _ = br.ReadUInt32();
            _ = br.ReadUInt32();

            _ = br.ReadByte(); // unknown
            var chunkCount = br.ReadByte();
            var fileHeaderSize = br.ReadUInt16();

            if (fileHeaderSize != expectedFileHeaderSize)
            {
                throw new InvalidDataException($"{type} entry has unexpected file header size {fileHeaderSize} (expected {expectedFileHeaderSize}).");
            }

            var remainingHeaderBytes = fileHeaderSize - 16;
            _ = br.ReadBytes(remainingHeaderBytes);

            ulong firstOffset = 0;
            uint totalPacked = 0;
            uint totalUnpacked = 0;

            for (var chunk = 0; chunk < chunkCount; chunk++)
            {
                var dataOffset = br.ReadUInt64();
                var packedSize = br.ReadUInt32();
                var unpackedSize = br.ReadUInt32();
                _ = br.ReadUInt16(); // start mip
                _ = br.ReadUInt16(); // end mip
                var sentinel = br.ReadUInt32();
                if (sentinel != 0xBAADF00D)
                {
                    throw new InvalidDataException($"{type} chunk has invalid sentinel 0x{sentinel:X8}.");
                }

                if (chunk == 0)
                {
                    firstOffset = dataOffset;
                }

                totalPacked = unchecked(totalPacked + packedSize);
                totalUnpacked = unchecked(totalUnpacked + unpackedSize);
            }

            records[i] = (firstOffset, totalPacked, totalUnpacked, type);
        }

        return records;
    }

    private static byte[] ExtractBethesdaBa2File(Ba2Entry entry)
    {
        if (!string.Equals(entry.BethesdaArchiveType, "GNRL", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"{entry.BethesdaArchiveType} BA2 extraction is not implemented for byte-for-byte loose file comparison yet.");
        }

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
