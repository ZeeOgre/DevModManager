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

            _ = br.ReadUInt32(); // version
            var type = new string(br.ReadChars(4));
            var fileCount = br.ReadUInt32();
            var nameTableOffset = br.ReadUInt64();

            var records = new (ulong Offset, uint Packed, uint Unpacked, bool IsDx10)[fileCount];
            switch (type)
            {
                case "GNRL":
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
                        records[i] = (dataOffset, packedSize, unpackedSize, false);
                    }
                    break;
                case "DX10":
                    for (var i = 0; i < fileCount; i++)
                    {
                        _ = br.ReadUInt32();
                        _ = br.ReadUInt32();
                        _ = br.ReadUInt32();

                        _ = br.ReadByte(); // unknown
                        var chunkCount = br.ReadByte();
                        var chunkHeaderSize = br.ReadUInt16();
                        _ = br.ReadUInt16(); // height
                        _ = br.ReadUInt16(); // width
                        _ = br.ReadByte();   // mip count
                        _ = br.ReadByte();   // dxgi format
                        _ = br.ReadUInt16(); // flags

                        if (chunkHeaderSize != 24)
                        {
                            throw new InvalidDataException($"DX10 entry has unexpected chunk header size {chunkHeaderSize}.");
                        }

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
                                throw new InvalidDataException($"DX10 chunk has invalid sentinel 0x{sentinel:X8}.");
                            }

                            if (chunk == 0)
                            {
                                firstOffset = dataOffset;
                            }

                            totalPacked = unchecked(totalPacked + packedSize);
                            totalUnpacked = unchecked(totalUnpacked + unpackedSize);
                        }

                        records[i] = (firstOffset, totalPacked, totalUnpacked, true);
                    }
                    break;
                default:
                    failureReason = $"unsupported BA2 type '{type}'";
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
                if (rec.IsDx10)
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
                    IsDx10TextureArchive = rec.IsDx10
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
        if (entry.IsDx10TextureArchive)
        {
            throw new NotSupportedException("DX10 BA2 texture extraction is not implemented for byte-for-byte loose file comparison yet.");
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
