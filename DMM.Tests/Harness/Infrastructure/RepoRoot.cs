namespace DMM.Tests.Harness.Infrastructure;

internal static class RepoRoot
{
    public static string Find()
    {
        // Walk up from test bin folder until we find a marker file that should exist at repo root.
        // Directory.Packages.props is a good marker for this repo.
        string? dir = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(dir))
        {
            string marker = Path.Combine(dir, "Directory.Packages.props");
            if (File.Exists(marker))
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate repo root (Directory.Packages.props not found in parent chain).");
    }
}