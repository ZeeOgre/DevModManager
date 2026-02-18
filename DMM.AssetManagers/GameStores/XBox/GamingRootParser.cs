using System.Text;

public static class GamingRootParser
{
    // 52 47 42 58 01 00 00 00  ==  'R' 'G' 'B' 'X' 1 0 0 0
    private static readonly byte[] Header = { 0x52, 0x47, 0x42, 0x58, 0x01, 0x00, 0x00, 0x00 };

    /// <summary>
    /// Parses X:\.GamingRoot and returns the Xbox games folder path.
    /// Returns null if the file is missing or invalid.
    /// </summary>
    public static string? TryGetXboxGamesRootForDrive(string driveRoot)
    {
        if (string.IsNullOrWhiteSpace(driveRoot))
            throw new ArgumentException("driveRoot is required", nameof(driveRoot));

        // Normalize: ensure "G:\" style
        driveRoot = Path.GetPathRoot(driveRoot) ?? driveRoot;
        if (!driveRoot.EndsWith(Path.DirectorySeparatorChar))
            driveRoot += Path.DirectorySeparatorChar;

        var path = Path.Combine(driveRoot, ".GamingRoot");
        if (!File.Exists(path))
            return null;

        return TryParseGamingRootFile(path, driveRoot);
    }

    /// <summary>
    /// Parses the .GamingRoot file content as:
    /// Header (8 bytes) + null-terminated UTF-16LE string.
    /// </summary>
    public static string? TryParseGamingRootFile(string gamingRootPath, string driveRoot)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(gamingRootPath); }
        catch { return null; }

        if (bytes.Length < Header.Length + 2)
            return null;

        // Validate header
        for (int i = 0; i < Header.Length; i++)
            if (bytes[i] != Header[i])
                return null;

        // Read UTF-16LE null-terminated string starting at offset 8
        const int start = 8;

        // Find terminator: two consecutive zero bytes aligned on UTF-16 boundary
        int end = -1;
        for (int i = start; i + 1 < bytes.Length; i += 2)
        {
            if (bytes[i] == 0x00 && bytes[i + 1] == 0x00)
            {
                end = i;
                break;
            }
        }

        if (end < 0 || end == start)
            return null;

        string raw;
        try
        {
            raw = Encoding.Unicode.GetString(bytes, start, end - start);
        }
        catch
        {
            return null;
        }

        raw = raw.Trim();

        if (raw.Length == 0)
            return null;

        // Normalize to absolute path on that drive
        // Common case: "\Games" or "\XboxGames" etc.
        if (raw.StartsWith("\\") || raw.StartsWith("/"))
        {
            var rel = raw.TrimStart('\\', '/');
            return NormalizeDir(Path.Combine(driveRoot, rel));
        }

        // Already absolute (e.g., "G:\Games")
        if (Path.IsPathRooted(raw))
            return NormalizeDir(raw);

        // Relative without leading slash (unlikely, but handle)
        return NormalizeDir(Path.Combine(driveRoot, raw));
    }

    private static string NormalizeDir(string path)
    {
        var full = Path.GetFullPath(path.Trim());
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
}
