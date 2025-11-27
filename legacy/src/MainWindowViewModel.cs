using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace ZO.DMM.AppNF
{
    public class MainWindowViewModel : ViewModelBase
    {
        public bool HasUnsavedChanges { get; set; }

        private ObservableCollection<ModItem> _modItems;
        private readonly Config _config;
        private string _selectedModStage;
        private ObservableCollection<string> _currentStages;
        private bool _isLoadingStages;
        private bool _isLoadingModItems;
        private bool _isMainWindowLoaded = false;
        private ModItem _workingMod;

        public ObservableCollection<ModItem> ModItems
        {
            get => _modItems;
            set => SetProperty(ref _modItems, value);
        }

        public Config Config => _config;

        public ICommand OpenSettingsCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand LaunchModManagerCommand { get; }
        public ICommand LaunchIDECommand { get; }
        public ICommand OpenGitHubCommand { get; }
        public ICommand LoadOrderCommand { get; }
        public ICommand OpenGameFolderCommand { get; }
        public ICommand PromoteCommand { get; }

        public ObservableCollection<string> CurrentStages
        {
            get => _currentStages;
            set => SetProperty(ref _currentStages, value);
        }

        public ModItem WorkingMod
        {
            get => _workingMod;
            set
            {
                _workingMod = value;
                OnPropertyChanged(nameof(WorkingMod));
            }
        }

        public string SelectedModStage
        {
            get => _selectedModStage;
            set
            {
                if (_selectedModStage != value)
                {
                    _selectedModStage = value;
                    OnPropertyChanged();
                    //HandleSelectedModStageChange();
                }
            }
        }

        public MainWindowViewModel()
        {
            _config = Config.Instance ?? throw new InvalidOperationException("Config instance is not initialized.");
            _modItems = new ObservableCollection<ModItem>();
            _workingMod = new ModItem();
            //LoadStages();
            //LoadModItems();
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            BackupCommand = new RelayCommand(_ => ExecuteBackupCommand());
            LaunchModManagerCommand = new RelayCommand(_ => LaunchModManager());
            LaunchIDECommand = new RelayCommand(_ => LaunchIDE());
            OpenGitHubCommand = new RelayCommand(_ => OpenGitHub());
            LoadOrderCommand = new RelayCommand(_ => LoadOrder());
            OpenGameFolderCommand = new RelayCommand(_ => OpenGameFolder());
        }

        public void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Initialization logic
            Debug.WriteLine("MWViewModel: MainWindow loaded.");
            _isMainWindowLoaded = true;
        }

        public void FlushDatabase()
        {
            // Logic to flush the database
            Debug.WriteLine("MWViewModel Flushing database before closing.");
        }

        public void LoadStages()
        {
            _isLoadingStages = true;
            try
            {
                // Fetch stages for _currentStages and _sourceStages
                var stagesQuery = "SELECT StageName FROM Stages WHERE isReserved = 0";
                var stages = ExecuteStageQuery(stagesQuery);
                stages.Insert(0, string.Empty); // Prepend string.Empty

                _currentStages = new ObservableCollection<string>(stages);
            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately
                _ = MessageBox.Show($"Failed to load stages: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoadingStages = false;
            }
        }

        private List<string> ExecuteStageQuery(string query)
        {
            var stages = new List<string>();
            using (var connection = DbManager.Instance.GetConnection())
            {
                connection.Open();
                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stages.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return stages;
        }

        public void LoadModItems()
        {
            _isLoadingModItems = true;
            try
            {
                ModItems = ModItem.LoadModItems();

                if (!ModItems.Any())
                {
                    ModItems = ModItem.BuildModItems();
                }
            }
            finally
            {
                _isLoadingModItems = false;
            }
        }

        private void OpenGameFolder()
        {
            string gameFolder = Config.Instance.GameFolder;
            if (!string.IsNullOrEmpty(gameFolder) && Directory.Exists(gameFolder))
            {
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = gameFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                _ = MessageBox.Show("Game folder is not set or does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void PromptForUrlIfNeeded(ModItem modItem, string urlType)
        {
            var urlInputDialog = new UrlInputDialog(modItem, urlType);
            if (urlInputDialog.ShowDialog() == true)
            {
                string newUrl = urlInputDialog.Url;
                if (!string.IsNullOrWhiteSpace(newUrl))
                {
                    if (urlType == "Bethesda" && string.IsNullOrWhiteSpace(modItem.BethesdaUrl))
                    {
                        modItem.BethesdaUrl = newUrl;
                    }
                    else if (urlType == "Nexus" && string.IsNullOrWhiteSpace(modItem.NexusUrl))
                    {
                        modItem.NexusUrl = newUrl;
                    }

                    SaveModItems();
                    OnPropertyChanged(nameof(ModItems));
                }
                else
                {
                    // Close the window and make no change
                    _ = MessageBox.Show("URL cannot be empty. No changes were made.");
                }
            }
        }

        public void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(SettingsLaunchSource.MainWindow);
            _ = settingsWindow.ShowDialog();
        }

        private void Close()
        {
            // Implement the logic to close the window if needed
        }

        public void ExecuteBackupCommand()
        {
            var backedUpMods = new List<string>();

            foreach (var modItem in ModItems)
            {
                string backupFolder = PathBuilder.GetModSourceBackupFolder(modItem.ModName);
                string backupZipPath = ModItem.Files.CreateBackup(modItem.ModFolderPath, backupFolder);

                if (!string.IsNullOrEmpty(backupZipPath))
                {
                    backedUpMods.Add(modItem.ModName);
                }
            }

            // Save the updated ModItems to ModStatus.json
            SaveModItems();
            OnPropertyChanged(nameof(ModItems));

            DisplayBackupResults(backedUpMods);
        }

        public void SaveModItems()
        {
            _ = Path.Combine(_config.RepoFolder ?? string.Empty, "ModStatus.json");
            ModItem.SaveModItems(ModItems);
        }

        private void DisplayBackupResults(List<string> backedUpMods)
        {
            if (!backedUpMods.Any())
            {
                _ = MessageBox.Show("All backed up files are current, nothing to do", "Backup Results", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var message = new StringBuilder();
            foreach (var modName in backedUpMods)
            {
                _ = message.AppendLine(modName);
            }
            _ = message.AppendLine();
            _ = message.AppendLine($"{backedUpMods.Count} mods backed up.");

            _ = MessageBox.Show(message.ToString(), "Backup Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LaunchModManager()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Config.Instance.ModManagerExecutable,
                Arguments = Config.Instance.ModManagerParameters
            };
            _ = Process.Start(processInfo);
        }

        private void LaunchIDE()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Config.Instance.IdeExecutable,
                Arguments = string.Empty
            };
            _ = Process.Start(processInfo);
        }

        private void OpenGitHub()
        {
            _ = Process.Start("explorer.exe", Config.Instance.GitHubRepo ?? string.Empty);
        }

        public void LoadOrder()
        {
            string loadOrderManagerPath = GetLoadOrderManagerPathFromRegistry();

            if (string.IsNullOrEmpty(loadOrderManagerPath) || !File.Exists(loadOrderManagerPath))
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Executable Files (*.exe)|*.exe",
                    Title = "Locate LoadOrderManager.exe",
                    FileName = "LoadOrderManager.exe"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    loadOrderManagerPath = openFileDialog.FileName;
                }
            }

            if (!string.IsNullOrEmpty(loadOrderManagerPath) && File.Exists(loadOrderManagerPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = loadOrderManagerPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("LoadOrderManager.exe not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool IsModFolderAccessible(ModItem modItem)
        {
            return !string.IsNullOrEmpty(modItem.DeployedStage) &&
                   !string.IsNullOrEmpty(modItem.ModDeploymentFolder) &&
                   Directory.Exists(PathBuilder.GetModStagingFolder(modItem.ModName));
        }
        private string GetLoadOrderManagerPathFromRegistry()
        {
            const string registryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\ZeeOgre\ZO.LoadOrderManager";
            const string registryValue = "Path";

            try
            {
                var installPath = (string)Microsoft.Win32.Registry.GetValue(registryKey, registryValue, null);
                if (!string.IsNullOrEmpty(installPath))
                {
                    return Path.Combine(installPath, "LoadOrderManager.exe");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading registry: {ex.Message}");
            }

            return string.Empty;
        }
    }
}
