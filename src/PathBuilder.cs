using System.IO;

namespace ZO.DMM.AppNF
{
    public static class PathBuilder
    {
        public static readonly string RepoFolder = Config.Instance.RepoFolder;
        public static readonly string BackupFolder = Path.Combine(RepoFolder, "BACKUP");
        public static readonly string ModStagingFolder = Config.Instance.ModStagingFolder;
        public static readonly List<string> ValidStages = Config.Instance.ModStages.ToList();

        public static string BuildPath(string modName, string stage = null, bool isBackup = false, bool isDeploy = false)
        {
            if (isBackup && isDeploy)
            {
                throw new ArgumentException("isBackup and isDeploy cannot both be true.");
            }

            if (stage != null && !ValidStages.Contains(stage))
            {
                throw new ArgumentException($"Invalid stage: {stage}");
            }

            if (stage != null && stage.StartsWith("#") && !isBackup)
            {
                throw new ArgumentException($"Stage {stage} is reserved for backup only.");
            }

            if (modName == null)
            {
                throw new ArgumentNullException(nameof(modName));
            }

            if (stage == null && !isDeploy)
            {
                // Return source path
                string sourceStage = ValidStages.FirstOrDefault(s => s.StartsWith("*")) ?? throw new InvalidOperationException("No source stage found.");
                return Path.Combine(RepoFolder, sourceStage.TrimStart('*'), modName);
            }

            if (isBackup)
            {
                return Path.Combine(BackupFolder, modName, stage.TrimStart('#'));
            }

            if (isDeploy)
            {
                return Path.Combine(ModStagingFolder, modName);
            }

            return Path.Combine(RepoFolder, stage, modName);
        }

        public static string GetBackupFolder(string modName)
        {
            return Path.Combine(BackupFolder, modName);
        }

        public static string GetModStagingFolder(string modName)
        {
            return Path.Combine(ModStagingFolder, modName);
        }

        public static string GetModSourceBackupFolder(string modName)
        {
            string sourceStage = ValidStages.FirstOrDefault(s => s.StartsWith("*")) ?? throw new InvalidOperationException("No source stage found.");
            return Path.Combine(BackupFolder, modName, sourceStage.TrimStart('*'));
        }

        public static string GetDeployBackupFolder(string modName)
        {
            return Path.Combine(BackupFolder, modName, "DEPLOYED");
        }

        public static string GetPackageDestination(string modName)
        {
            return Path.Combine(RepoFolder, "NEXUS", modName);
        }

        public static string GetPackageBackup(string modName)
        {
            return Path.Combine(BackupFolder, modName, "NEXUS");
        }

        public static string GetModStageFolder(string modName, string stage)
        {
            return Path.Combine(RepoFolder, stage, modName);
        }

        public static string GetModStageBackupFolder(string modName, string stage)
        {
            return Path.Combine(BackupFolder, modName, stage.TrimStart('#', '*'));
        }

        public static string GetRelativePath(string relativeTo, string path)
        {
            // Ensure the relativeTo path ends with a directory separator
            if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                relativeTo += Path.DirectorySeparatorChar;
            }

            var uri = new Uri(relativeTo);
            var pathUri = new Uri(path);

            if (uri.Scheme != pathUri.Scheme)
            {
                return path; // Path can't be made relative.
            }

            Uri relativeUri = uri.MakeRelativeUri(pathUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (pathUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }
    }
}

