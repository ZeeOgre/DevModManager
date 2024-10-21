using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ZO.DMM.AppNF
{
    public partial class ModItem
    {
        public string ModName { get; set; } = string.Empty;
        public string ModFolderPath { get; set; } = string.Empty;
        public string DeployedStage { get; set; }
        public Dictionary<string, (string Stage, string Path)> CurrentArchiveFiles { get; set; } = new Dictionary<string, (string, string)>();
        public string NexusUrl { get; set; }
        public string BethesdaUrl { get; set; }
        public string ModDeploymentFolder { get; set; }
        public Dictionary<string, (string Stage, string RelativePath, DateTime Timestamp, string Hash)> ModFiles { get; set; } = new Dictionary<string, (string Stage, string RelativePath, DateTime Timestamp, string Hash)>();
        public List<string> AvailableStages { get; set; } = new List<string>();



        public void SaveMod()
        {
            DB.WriteMod(this);
        }


        public static void SaveModItems(ObservableCollection<ModItem> modItems)
        {
            ModItem.DB.SaveToDatabase(modItems);
            //SaveToYaml(modItems);
        }

        private static void SaveToYaml(ObservableCollection<ModItem> modItems)
        {
            var config = Config.Instance;
            if (config.RepoFolder == null)
            {
                throw new InvalidOperationException("RepoFolder is not configured.");
            }

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            string yaml = serializer.Serialize(modItems);
            File.WriteAllText(Path.Combine(config.RepoFolder, "ModStatus.yaml"), yaml);
        }

        public static ObservableCollection<ModItem> LoadModItems()
        {
            _ = new ObservableCollection<ModItem>();

            ObservableCollection<ModItem> modItems = DB.LoadFromDatabase();
            if (modItems.Count > 0) return modItems;

            // Fallback to LoadFromYaml
            // modItems = LoadFromYaml();
            // if (modItems.Count > 0) return modItems;

            // Fallback to BuildModItems
            modItems = BuildModItems();

            // If modItems is still empty, show an error window
            if (modItems.Count == 0)
            {
                _ = System.Windows.MessageBox.Show(
                    "The collection of mod items is empty. Please ensure the directory structure is laid out as specified in the README.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                System.Windows.Application.Current.Shutdown();
            }

            return modItems;
        }

        //private static ObservableCollection<ModItem> LoadFromYaml()
        //{
        //    var config = Config.Instance;
        //    if (config.RepoFolder == null)
        //    {
        //        throw new InvalidOperationException("RepoFolder is not configured.");
        //    }

        //    var yamlFilePath = Path.Combine(config.RepoFolder, "ModStatus.yaml");

        //    if (!File.Exists(yamlFilePath))
        //    {
        //        return new ObservableCollection<ModItem>();
        //    }

        //    var deserializer = new DeserializerBuilder()
        //        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        //        .Build();

        //    var yamlContent = File.ReadAllText(yamlFilePath);
        //    var modItems = deserializer.Deserialize<ObservableCollection<ModItem>>(yamlContent);

        //    return modItems ?? new ObservableCollection<ModItem>();
        //}

        public static ObservableCollection<ModItem> BuildModItems()
        {
            var config = Config.Instance;
            if (config.RepoFolder == null)
            {
                throw new InvalidOperationException("RepoFolder is not configured.");
            }

            var modItems = new ObservableCollection<ModItem>();

            // Fetch stages from the database
            var stages = ModItem.DB.GetStages();
            string sourceStage = ModItem.DB.GetSourceStage();
            string sourceFolderPath = Path.Combine(config.RepoFolder, sourceStage);

            if (Directory.Exists(sourceFolderPath))
            {
                var modFolders = Directory.GetDirectories(sourceFolderPath);
                foreach (var modFolder in modFolders)
                {
                    var modName = Path.GetFileName(modFolder);
                    var modItem = new ModItem
                    {
                        ModName = modName,
                        ModFolderPath = modFolder,
                        DeployedStage = string.Empty, // Start with an empty stage
                        NexusUrl = string.Empty, // Initialize as empty
                        BethesdaUrl = string.Empty, // Initialize as empty
                        ModDeploymentFolder = string.Empty // Initialize as empty
                    };

                    // Set the initial AvailableStages
                    modItem.AvailableStages.Add(sourceStage);
                    modItem.AvailableStages = modItem.AvailableStages.Distinct().ToList();

                    // Load files with relative paths, timestamps, and hashes
                    GatherModFiles(modItem);

                    // Check deployment status
                    CheckDeploymentStatus(modItem);

                    // Get the latest archives for each stage
                    foreach (var stage in stages)
                    {
                        var latestZip = GetLatestZip(modName, stage);
                        if (!string.IsNullOrEmpty(latestZip))
                        {
                            modItem.CurrentArchiveFiles[stage] = (stage, latestZip);
                            modItem.AvailableStages.Add(sourceStage);
                            modItem.AvailableStages = modItem.AvailableStages.Distinct().ToList();
                        }
                    }

                    // Ensure no empty entries are added
                    var latestModArchives = ModItem.DB.GetLatestModArchives(modItem);
                    foreach (var archive in latestModArchives)
                    {
                        if (!string.IsNullOrEmpty(archive.Value.Path))
                        {
                            modItem.CurrentArchiveFiles[archive.Key] = archive.Value;
                        }
                    }

                    modItems.Add(modItem);
                }
            }
            SaveModItems(modItems);
            return modItems;
        }

        public static void GatherModFiles(ModItem modItem)
        {
            var config = Config.Instance;
            if (config.RepoFolder == null)
            {
                throw new InvalidOperationException("RepoFolder is not configured.");
            }

            var stages = ModItem.DB.GetStages();
            var excludedFileTypes = config.PackageExcludeFiletypes?.ToArray() ?? Array.Empty<string>();

            foreach (var stage in stages)
            {
                var stageFolderPath = Path.Combine(config.RepoFolder, stage, modItem.ModName);
                if (!Directory.Exists(stageFolderPath))
                {
                    continue;
                }

                var files = Directory.GetFiles(stageFolderPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var relativePath = PathBuilder.GetRelativePath(stageFolderPath, file);
                    var fileName = fileInfo.Name;
                    var dtStamp = fileInfo.LastWriteTime;
                    var hash = ModItem.Files.ComputeHash(file);

                    if (!IsExcludedFileType(fileName, excludedFileTypes))
                    {
                        modItem.ModFiles[fileName] = (stage, relativePath, dtStamp, hash);
                        modItem.AvailableStages.Add(stage);
                        modItem.AvailableStages = modItem.AvailableStages.Distinct().ToList();
                    }
                }
                var stageBackupPath = PathBuilder.GetModStageBackupFolder(modItem.ModName, stage);
                if (Directory.Exists(stageBackupPath))
                {
                    var archives = Directory.GetFiles(stageBackupPath, $"*.{Config.Instance.ArchiveFormat}");
                    modItem.CurrentArchiveFiles[stage] = (stage, archives.FirstOrDefault());
                }

            }
        }

        private static bool IsExcludedFileType(string fileName, string[] excludedFileTypes)
        {
            foreach (var excludedFileType in excludedFileTypes)
            {
                if (fileName.EndsWith(excludedFileType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetLatestZip(string modName, string stage)
        {
            var config = Config.Instance;
            if (config.RepoFolder == null)
            {
                throw new InvalidOperationException("RepoFolder is not configured.");
            }

            var stageFolder = Path.Combine(config.RepoFolder, "BACKUP", modName, stage);
            if (!Directory.Exists(stageFolder))
            {
                return string.Empty;
            }

            var fileTypes = new[] { "*.zip", "*.7z", "*.rar", "*.ba2" };
            var newestFile = ModItem.Files.GetNewestFile(stageFolder, fileTypes);
            return newestFile ?? string.Empty;
        }

        public static void CheckDeploymentStatus(ModItem modItem)
        {
            var config = Config.Instance;
            if (config.RepoFolder == null)
            {
                throw new InvalidOperationException("RepoFolder is not configured.");
            }

            var modStagingFolder = PathBuilder.GetModStagingFolder(modItem.ModName);
            if (Directory.Exists(modStagingFolder))
            {
                var targetDir = ModItem.Files.GetJunctionTarget(modStagingFolder);
                if (targetDir != null && targetDir.StartsWith(Path.Combine(config.RepoFolder)))
                {
                    // Extract the stage from the targetDir
                    var relativePath = targetDir.Substring(config.RepoFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                    var parts = relativePath.Split(Path.DirectorySeparatorChar);
                    if (parts.Length >= 2)
                    {
                        var stage = parts[0];
                        var modName = parts[1];

                        if (modName.Equals(modItem.ModName, StringComparison.OrdinalIgnoreCase))
                        {
                            modItem.DeployedStage = stage.ToUpper();
                            modItem.ModDeploymentFolder = modStagingFolder;
                        }
                    }
                }
                else
                {
                    var result = MessageBox.Show(
                        $"The mod {modItem.ModName} is not correctly deployed. Do you want to delete the target folder?",
                        "Deployment Issue",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        ModItem.Files.RemoveJunctionPoint(modStagingFolder);
                        var redeployResult = MessageBox.Show(
                            $"Do you want to redeploy the mod {modItem.ModName}?",
                            "Redeploy Mod",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        );

                        if (redeployResult == MessageBoxResult.Yes)
                        {
                            var sourceStage = modItem.DeployedStage ?? string.Empty;
                            ModItem.Files.CreateJunctionPoint(modStagingFolder, Path.Combine(config.RepoFolder, sourceStage, modItem.ModName));
                        }
                    }
                }
            }
        }
    }
}
