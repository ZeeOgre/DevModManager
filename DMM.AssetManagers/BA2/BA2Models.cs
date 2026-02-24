namespace DMM.AssetManagers;

public enum BA2CompressionMode
{
    InheritArchive = 0,
    Compressed = 1,
    Uncompressed = 2,
    Smart = 3
}

public enum BA2TargetPlatform
{
    Pc = 0,
    Xbox = 1
}

public sealed class BA2CreateOptions
{
    public bool ArchiveCompressedByDefault { get; init; } = true;
    public BA2TargetPlatform TargetPlatform { get; init; } = BA2TargetPlatform.Pc;
    public int Version { get; init; } = 104;
}

public sealed class BA2BuildFile
{
    public required string ArchivePath { get; init; }
    public required byte[] Data { get; init; }
    public BA2CompressionMode Compression { get; init; } = BA2CompressionMode.InheritArchive;
}

public sealed class BA2ArchiveEntry
{
    public required string ArchivePath { get; init; }
    public required string FolderPath { get; init; }
    public required string FileName { get; init; }
    public required string ArchiveInnerPath { get; init; }
    public long StoredSize { get; init; }
    public long UncompressedSize { get; init; }
    public bool IsCompressed { get; init; }
}
