using System;
using System.IO;
using System.Linq;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace ZO.DMM.AppNF
{
    public static class ModStageManager
    {
        public static string PromoteModStage(ModItem modItem, string sourceStage, string targetStage)
        {
            var config = Config.Instance;
            string sourcePath = Path.Combine(config.RepoFolder, sourceStage, modItem.ModName);
            string targetPath = Path.Combine(config.RepoFolder, targetStage, modItem.ModName);
            string backupFolder = Path.Combine(config.RepoFolder, "BACKUP", modItem.ModName, targetStage);

            // Delete files from targetPath
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }

            // Copy included file types from sourcePath to targetPath
            Directory.CreateDirectory(targetPath);
            var filesToCopy = Directory.GetFiles(sourcePath, "*.*", SearchOption.TopDirectoryOnly)
                                       .Where(f => config.PromoteIncludeFiletypes.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

            if (!filesToCopy.Any())
            {
                MessageBox.Show("There were no source files of the correct type. Have you created the .esm and .ba2 files yet?", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            foreach (var file in filesToCopy)
            {
                File.Copy(file, Path.Combine(targetPath, Path.GetFileName(file)));
            }

            // Create a backup from targetPath
            string backupZipPath = ModItem.Files.CreateBackup(targetPath, backupFolder);

            // Update the appropriate zip property based on the target stage
            // (Assuming this is handled elsewhere in the code)

            return backupZipPath;
        }

        public static void PackageMod(ModItem modItem, string sourceStage)
        {
            string releasePath = PathBuilder.GetModStageFolder(modItem.ModName, sourceStage);
            string backupNexusPath = PathBuilder.GetPackageBackup(modItem.ModName);
            string nexusPath = PathBuilder.GetPackageDestination(modItem.ModName);

            Directory.CreateDirectory(backupNexusPath);
            Directory.CreateDirectory(nexusPath);

            // Create the dated zip file from the releasePath
            string datedZipFile = ModItem.Files.CreateBackup(releasePath, backupNexusPath);

            if (string.IsNullOrEmpty(datedZipFile))
            {
                throw new InvalidOperationException("Failed to create the dated zip file.");
            }

            // Define the undated zip file path
            string undatedZipFile = Path.Combine(nexusPath, $"{modItem.ModName}.zip");

            // Copy the dated zip file to the undated zip file
            File.Copy(datedZipFile, undatedZipFile, true);
        }

        public static ModItem DeployStage(ModItem modItem, string targetStage)
        {
            if (modItem == null)
            {
                return modItem;
            }
            if (string.IsNullOrEmpty(targetStage))
            {
                ModItem.Files.RemoveJunctionPoint(modItem.ModDeploymentFolder);
                modItem.ModDeploymentFolder = string.Empty;
                modItem.DeployedStage = string.Empty;
                return modItem;
            }

            var sourcePath = PathBuilder.GetModStageFolder(modItem.ModName, targetStage);
            var targetPath = PathBuilder.GetModStagingFolder(modItem.ModName);
            var backupPath = PathBuilder.GetDeployBackupFolder(modItem.ModName);

            ModItem.Files.CreateBackup(sourcePath, backupPath);
            ModItem.Files.CreateJunctionPoint(targetPath, sourcePath);
            modItem.ModDeploymentFolder = targetPath;
            modItem.DeployedStage = targetStage;

            return modItem;
        }

        // Uncomment and refactor the following methods if needed
        /*
        public static void ExecuteModStageChangedCommand(string modName, ObservableCollection<ModItem> modItems)
        {
            var config = Config.Instance;
            var modItem = modItems.FirstOrDefault(m => m.ModName == modName);
            if (modItem != null)
            {
                string modStagingFolder = Path.Combine(config.ModStagingFolder, modName);
                string repoFolder = config.RepoFolder;
                string backupFolder = Path.Combine(repoFolder, "BACKUP");

                bool createBackup = modItem.DeployedStage == "DEV";

                switch (modItem.DeployedStage)
                {
                    case "DEV":
                        modItem.DeployedStage = "TEST";
                        HandleStageChange(modItem);
                        break;
                    case "TEST":
                        modItem.DeployedStage = "RELEASE";
                        HandleStageChange(modItem);
                        break;
                    default:
                        ModItem.Files.RemoveJunctionPoint(modStagingFolder);
                        break;
                }

                if (createBackup && modItem.DeployedStage != "DEV")
                {
                    ModItem.Files.CreateBackup(modName, backupFolder);
                }
                WriteModStatus(modItems);
            }
        }

        private static void HandleStageChange(ModItem modItem)
        {
            var config = Config.Instance;
            string modStagingFolder = config.ModStagingFolder;
            string repoFolder = config.RepoFolder;
            string stage = modItem.DeployedStage;
            string modName = modItem.ModName;
            string stageFolder = Path.Combine(repoFolder, stage, modName);
            string dataFolder = Path.Combine(modStagingFolder, modName);
            string backupFolder = Path.Combine(repoFolder, "BACKUP", modName, "DEPLOYED");
            string zipPath = Path.Combine(backupFolder, $"{modName}_{config.TimestampFormat}.zip");

            ModItem.Files.CreateBackup(stageFolder, backupFolder);
            ModItem.Files.CreateJunctionPoint(dataFolder, stageFolder);
            modItem.ModDeploymentFolder = dataFolder;
        }

        public static void WriteModStatus(ObservableCollection<ModItem> modItems)
        {
            var config = Config.Instance;
            var modStatus = modItems.Select(modItem => new
            {
                modItem.ModName,
                modItem.ModFolderPath,
                modItem.ModDeploymentFolder,
                modItem.DeployedStage,
                modItem.CurrentArchiveFiles,
                modItem.BethesdaUrl,
                modItem.NexusUrl,
                modItem.ModFiles
            });

            // File.WriteAllText(modStatusPath, JsonConvert.SerializeObject(modStatus, Formatting.Indented));
        }

        public static ModItem GetModStatus(string modName, ObservableCollection<ModItem> modItems)
        {
            return modItems.FirstOrDefault(m => m.ModName == modName);
        }
        */
    }
}