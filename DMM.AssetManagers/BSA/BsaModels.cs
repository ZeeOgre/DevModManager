namespace DMM.AssetManagers.BSA;

public enum BsaCompressionMode
{
    InheritArchive = 0,
    Compressed = 1,
    Uncompressed = 2,
    Smart = 3
}

public enum BsaTargetPlatform
{
    Pc = 0,
    Xbox = 1
}

public sealed class BsaCreateOptions
{
    public bool ArchiveCompressedByDefault { get; init; } = true;
    public BsaTargetPlatform TargetPlatform { get; init; } = BsaTargetPlatform.Pc;
    public int Version { get; init; } = 104;
}

public sealed class BsaBuildFile
{
    public required string ArchivePath { get; init; }
    public required byte[] Data { get; init; }
    public BsaCompressionMode Compression { get; init; } = BsaCompressionMode.InheritArchive;
}

public sealed class BsaEntry
{
    public required string ArchivePath { get; init; }
    public required string FolderPath { get; init; }
    public required string FileName { get; init; }
    public required string ArchiveInnerPath { get; init; }
    public long StoredSize { get; init; }
    public long UncompressedSize { get; init; }
    public bool IsCompressed { get; init; }
}
