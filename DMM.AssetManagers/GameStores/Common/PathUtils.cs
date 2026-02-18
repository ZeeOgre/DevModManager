namespace DMM.AssetManagers.GameStores.Common
{
    public static class PathUtils
    {
        public static string NormalizeDir(string dir)
            => Path.GetFullPath(dir.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
    }
}