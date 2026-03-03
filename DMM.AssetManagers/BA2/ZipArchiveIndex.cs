using System.IO.Compression;

namespace DMM.AssetManagers;

public static class ZipArchiveIndex
{
    public static Dictionary<string, Ba2Entry> BuildIndex(IEnumerable<string> zipPaths)
    {
        if (zipPaths == null) throw new ArgumentNullException(nameof(zipPaths));

        var index = new Dictionary<string, Ba2Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in zipPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full)) continue;

            try
            {
                using var archive = ZipFile.OpenRead(full);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.FullName) || entry.FullName.EndsWith('/'))
                    {
                        continue;
                    }

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
