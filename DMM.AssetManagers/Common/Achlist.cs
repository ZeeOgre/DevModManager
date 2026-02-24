using DMM.AssetManagers.BSA;

namespace DMM.AssetManagers.Common;

public sealed class AchlistEntry
{
    public required string ArchivePath { get; init; }
    public BsaCompressionMode Compression { get; init; } = BsaCompressionMode.InheritArchive;
}

public static class Achlist
{
    public static IReadOnlyList<AchlistEntry> Parse(string achlistContents)
    {
        if (achlistContents is null)
            throw new ArgumentNullException(nameof(achlistContents));

        var entries = new List<AchlistEntry>();
        using var reader = new StringReader(achlistContents);
        string? line;
        int lineNo = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNo++;
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                continue;

            var compression = BsaCompressionMode.InheritArchive;
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                compression = ParseCompression(parts[1], lineNo);

            entries.Add(new AchlistEntry
            {
                ArchivePath = parts[0].Replace('\\', '/').TrimStart('/'),
                Compression = compression
            });
        }

        return entries;
    }

    private static BsaCompressionMode ParseCompression(string value, int lineNo)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "inherit" or "default" => BsaCompressionMode.InheritArchive,
            "compressed" or "zip" or "zlib" => BsaCompressionMode.Compressed,
            "uncompressed" or "store" or "none" => BsaCompressionMode.Uncompressed,
            "smart" or "auto" => BsaCompressionMode.Smart,
            _ => throw new FormatException($"Unsupported achlist compression token '{value}' at line {lineNo}.")
        };
    }
}
