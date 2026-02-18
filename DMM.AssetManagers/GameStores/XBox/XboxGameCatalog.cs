using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Comparers;

public static class XboxGameCatalog
{
    public sealed record GameEntry(
        string IdentityName,
        string DisplayName,
        string? StoreId,
        string? TitleId,
        string ConfigPath,
        string InstallRoot,
        string? ContentPath,
        string? ExecutableName,
        string? IconPath,          // Path to the icon image (Square44x44Logo)
        string? LogoPath,          // Path to the logo image (Square150x150Logo)
        string? SplashScreenPath   // Path to the splash screen image
    );

    public static List<string> FindGamingRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var driveRoot = drive.RootDirectory.FullName; // e.g., "G:\\")
            var gamingRootFile = Path.Combine(driveRoot, ".GamingRoot");

            if (!File.Exists(gamingRootFile))
                continue;

            var root = TryParseGamingRootFile(gamingRootFile, driveRoot);
            if (root != null && Directory.Exists(root))
                roots.Add(NormalizeDir(root));
        }

        return roots.ToList();
    }

    public static List<GameEntry> BuildCatalog(IEnumerable<string> gamingRoots)
    {
        var results = new List<GameEntry>();

        foreach (var root in gamingRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var gameDir in EnumerateGameDirectories(root))
            {
                foreach (var cfgPath in EnumerateLikelyMicrosoftGameConfigPaths(gameDir))
                {
                    var entry = TryParseMicrosoftGameConfig(cfgPath, gameDir);
                    if (entry != null)
                        results.Add(entry);
                }
            }
        }

        // Deduplicate by IdentityName + InstallRoot using TupleStringComparer
        return results
            .GroupBy(e => (e.IdentityName, e.InstallRoot), new TupleStringComparer())
            .Select(g => g.First())
            .ToList();
    }

    public static GameEntry? FindByIdentity(List<GameEntry> catalog, string identityName)
        => CatalogUtils.FindByIdentity(catalog, identityName, e => e.IdentityName);

    private static IEnumerable<string> EnumerateGameDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateLikelyMicrosoftGameConfigPaths(string gameDir)
    {
        var direct = Path.Combine(gameDir, "MicrosoftGame.config");
        if (File.Exists(direct)) yield return direct;

        var content = Path.Combine(gameDir, "Content", "MicrosoftGame.config");
        if (File.Exists(content)) yield return content;
    }

    private static GameEntry? TryParseMicrosoftGameConfig(string configPath, string gameDir)
    {
        try
        {
            var doc = XDocument.Load(configPath);

            string identityName =
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Identity")?.Attribute("Name")?.Value?.Trim()
                ?? "";

            if (string.IsNullOrWhiteSpace(identityName))
                return null;

            string displayName =
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals")?.Attribute("DefaultDisplayName")?.Value?.Trim()
                ?? Path.GetFileName(gameDir);

            string? storeId = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "StoreId")?.Value?.Trim();
            string? titleId = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "TitleId")?.Value?.Trim();

            string? exeName =
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Executable")?.Attribute("Name")?.Value?.Trim();

            // Extract visual assets
            string? iconPath =
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals")?.Attribute("Square44x44Logo")?.Value?.Trim();
            string? logoPath =
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals")?.Attribute("Square150x150Logo")?.Value?.Trim();
            string? splashScreenPath =
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals")?.Attribute("SplashScreenImage")?.Value?.Trim();

            // Resolve paths relative to the game directory
            iconPath = !string.IsNullOrWhiteSpace(iconPath) ? Path.Combine(gameDir, iconPath) : null;
            logoPath = !string.IsNullOrWhiteSpace(logoPath) ? Path.Combine(gameDir, logoPath) : null;
            splashScreenPath = !string.IsNullOrWhiteSpace(splashScreenPath) ? Path.Combine(gameDir, splashScreenPath) : null;

            // Determine install root + content path
            string installRoot = gameDir;
            string? contentPath = null;

            // If config is in Content, prefer parent as root
            var cfgDir = Path.GetDirectoryName(configPath)!;
            if (string.Equals(Path.GetFileName(cfgDir), "Content", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.GetParent(cfgDir);
                if (parent != null) installRoot = parent.FullName;
                contentPath = cfgDir;
            }
            else
            {
                // Otherwise prefer <InstallRoot>\Content if exists
                var candidate = Path.Combine(gameDir, "Content");
                if (Directory.Exists(candidate)) contentPath = candidate;
            }

            return new GameEntry(
                IdentityName: identityName,
                DisplayName: displayName,
                StoreId: string.IsNullOrWhiteSpace(storeId) ? null : storeId,
                TitleId: string.IsNullOrWhiteSpace(titleId) ? null : titleId,
                ConfigPath: configPath,
                InstallRoot: NormalizeDir(installRoot),
                ContentPath: contentPath != null ? NormalizeDir(contentPath) : null,
                ExecutableName: exeName,
                IconPath: iconPath,
                LogoPath: logoPath,
                SplashScreenPath: splashScreenPath
            );
        }
        catch
        {
            return null;
        }
    }

    private static string? TryParseGamingRootFile(string path, string driveRoot)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);

            // Decode as UTF-16LE; this will include lots of NULs / junk before the path,
            // but the actual path string becomes readable.
            var text = Encoding.Unicode.GetString(bytes);

            // Find something that looks like a path.
            var candidates = text
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Contains("\\") || s.Contains("/"))
                .ToList();

            if (candidates.Count == 0)
                return null;

            var relOrAbs = candidates.Last();

            // If it’s relative (starts with "\Games"), combine with drive root.
            if (Path.IsPathRooted(relOrAbs))
                return relOrAbs;

            if (relOrAbs.StartsWith("\\") || relOrAbs.StartsWith("/"))
                return Path.Combine(driveRoot, relOrAbs.TrimStart('\\', '/'));

            return Path.Combine(driveRoot, relOrAbs);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeDir(string dir)
        => PathUtils.NormalizeDir(dir);
}

 
