namespace DMM.AssetManagers.BSA;

[System.Obsolete("Use DMM.AssetManagers.BA2Archive APIs.")]
public static class BsaArchive
{
    public static IReadOnlyList<BsaEntry> ReadIndex(string archivePath)
        => BA2Archive.ReadBuildIndex(archivePath)
            .Select(x => new BsaEntry
            {
                ArchivePath = x.ArchivePath,
                FolderPath = x.FolderPath,
                FileName = x.FileName,
                ArchiveInnerPath = x.ArchiveInnerPath,
                StoredSize = x.StoredSize,
                UncompressedSize = x.UncompressedSize,
                IsCompressed = x.IsCompressed
            }).ToArray();

    public static byte[] ExtractFile(string archivePath, string innerPath)
        => BA2Archive.ExtractBuiltFile(archivePath, innerPath);


    public static void ExtractFiles(string archivePath, IEnumerable<string> innerPaths, string outputRoot)
    {
        Directory.CreateDirectory(outputRoot);
        foreach (var innerPath in innerPaths)
        {
            var bytes = ExtractFile(archivePath, innerPath);
            var outPath = Path.Combine(outputRoot, innerPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllBytes(outPath, bytes);
        }
    }

    public static void ExtractAll(string archivePath, string outputRoot)
    {
        Directory.CreateDirectory(outputRoot);
        foreach (var entry in ReadIndex(archivePath))
        {
            var bytes = ExtractFile(archivePath, entry.ArchiveInnerPath);
            var outPath = Path.Combine(outputRoot, entry.ArchiveInnerPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllBytes(outPath, bytes);
        }
    }

    public static void Create(string outputArchivePath, IEnumerable<BsaBuildFile> files, BsaCreateOptions? options = null)
        => BA2Archive.Create(
            outputArchivePath,
            files.Select(x => new BA2BuildFile
            {
                ArchivePath = x.ArchivePath,
                Data = x.Data,
                Compression = (BA2CompressionMode)x.Compression
            }),
            options is null
                ? null
                : new BA2CreateOptions
                {
                    ArchiveCompressedByDefault = options.ArchiveCompressedByDefault,
                    TargetPlatform = (BA2TargetPlatform)options.TargetPlatform,
                    Version = options.Version
                });

    public static void AddOrReplaceFiles(string archivePath, IEnumerable<BsaBuildFile> replacements, BsaCreateOptions? options = null)
        => BA2Archive.AddOrReplaceFiles(
            archivePath,
            replacements.Select(x => new BA2BuildFile
            {
                ArchivePath = x.ArchivePath,
                Data = x.Data,
                Compression = (BA2CompressionMode)x.Compression
            }),
            options is null
                ? null
                : new BA2CreateOptions
                {
                    ArchiveCompressedByDefault = options.ArchiveCompressedByDefault,
                    TargetPlatform = (BA2TargetPlatform)options.TargetPlatform,
                    Version = options.Version
                });
}
