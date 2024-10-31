using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LibGit2Sharp;

namespace ZO.DMM.AppNF
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private Config _config;
        private string _archiveFormat;
        public Window ParentWindow { get; set; }


        private string _gitHubUsername;
        private string _tokenExpiration;
        private bool _authenticated;
        public string GitHubUsername
        {
            get => _gitHubUsername;
            set
            {
                _gitHubUsername = value;
                OnPropertyChanged();
            }
        }

        public SettingsViewModel()
        {
            _config = Config.Instance;

            AvailableArchiveFormats = new ObservableCollection<string>();
            LoadAvailableArchiveFormats();

            LoadCommand = new RelayCommand(_ => LoadSettings());

            CheckForUpdatesCommand = new RelayCommand(_ => App.CheckForUpdates(ParentWindow));
            CloneRepoCommand = new RelayCommand(_ => CloneRepo());


            LoadSettings();
        }
        public string TokenExpiration
        {
            get => _tokenExpiration;
            set
            {
                _tokenExpiration = value;
                OnPropertyChanged(nameof(TokenExpiration));
            }
        }

        public bool GitHubAuthenticated
        {
            get => _authenticated;
            set
            {
                _authenticated = value;
                OnPropertyChanged(nameof(GitHubAuthenticated));
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand LaunchGameFolderCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand CloneRepoCommand { get; }
        //public ICommand LoadSettingsCommand { get; } = new RelayCommand(_ => LoadSettings());
        //public ICommand SaveSettingsCommand { get; } = new RelayCommand(_ => SaveSettings());

        public bool AutoCheckForUpdates
        {
            get => _config.AutoCheckForUpdates;
            set
            {
                _config.AutoCheckForUpdates = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AvailableArchiveFormats { get; private set; }
        
        private void LoadAvailableArchiveFormats()
        {
            string query = "SELECT FormatName FROM ArchiveFormats;";

            using (var connection = DbManager.Instance.GetConnection())
            {
                connection.Open();
                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            AvailableArchiveFormats.Add(reader["FormatName"]?.ToString() ?? string.Empty);
                        }
                    }
                }
            }

            // Add default formats if the database is empty
            if (AvailableArchiveFormats.Count == 0)
            {
                AvailableArchiveFormats.Add("zip");
                AvailableArchiveFormats.Add("7z");
            }
        }

        public void UpdateFromConfig()
        {
            var config = Config.Instance;
            RepoFolder = config.RepoFolder;
            UseGit = config.UseGit;
            GitHubRepo = config.GitHubRepo;
            UseModManager = config.UseModManager;
            ModStagingFolder = config.ModStagingFolder;
            GameFolder = config.GameFolder;
            ModManagerExecutable = config.ModManagerExecutable;
            ModManagerParameters = config.ModManagerParameters;
            IdeExecutable = config.IdeExecutable;
            PromoteIncludeFiletypes = string.Join(",", config.PromoteIncludeFiletypes?.Select(s => s.Trim()) ?? Array.Empty<string>());
            PackageExcludeFiletypes = string.Join(",", config.PackageExcludeFiletypes?.Select(s => s.Trim()) ?? Array.Empty<string>());
            ModStages = string.Join(",", config.ModStages?.Select(stage => stage.Trim()) ?? Array.Empty<string>());
            LimitFiletypes = config.LimitFiletypes;
            TimestampFormat = config.TimestampFormat;
            ArchiveFormat = string.IsNullOrEmpty(config.ArchiveFormat) ? "zip" : config.ArchiveFormat;
            MyNameSpace = config.MyNameSpace;
            MyResourcePrefix = config.MyResourcePrefix;
            ShowSaveMessage = config.ShowSaveMessage;
            ShowOverwriteMessage = config.ShowOverwriteMessage;
            NexusAPIKey = config.NexusAPIKey;
            AutoCheckForUpdates = config.AutoCheckForUpdates;
            DarkMode = config.DarkMode;
            GitHubUsername = config.GitHubUsername;
            GitHubToken = config.GitHubToken;
            GitHubTokenExpiration = config.GitHubTokenExpiration;
            GitHubAuthenticated = config.GitHubAuthenticated;   


            OnPropertyChanged(null);
        }


        public bool DarkMode {
            get => _config.DarkMode;
            set
            {
                _config.DarkMode = value;
                OnPropertyChanged();
            }
        }
        public string GitHubToken {
            get => _config.GitHubToken;
            set
            {
                _config.GitHubToken = value;
                OnPropertyChanged();
            }
        }
        public DateTime? GitHubTokenExpiration {
            get => _config.GitHubTokenExpiration;
            set
            {
                _config.GitHubTokenExpiration = value;
                OnPropertyChanged();
            }
        }
        
        public string RepoFolder
        {
            get => _config.RepoFolder;
            set
            {
                _config.RepoFolder = value;
                OnPropertyChanged();
            }
        }

        public bool UseGit
        {
            get => _config.UseGit;
            set
            {
                _config.UseGit = value;
                OnPropertyChanged();
            }
        }

        public string GitHubRepo
        {
            get => _config.GitHubRepo;
            set
            {
                _config.GitHubRepo = value;
                OnPropertyChanged();
            }
        }

        public bool UseModManager
        {
            get => _config.UseModManager;
            set
            {
                _config.UseModManager = value;
                OnPropertyChanged();
            }
        }

        public string ModStagingFolder
        {
            get => _config.ModStagingFolder;
            set
            {
                _config.ModStagingFolder = value;
                OnPropertyChanged();
            }
        }

        public string GameFolder
        {
            get => _config.GameFolder;
            set
            {
                _config.GameFolder = value;
                OnPropertyChanged();
            }
        }

        public string ModManagerExecutable
        {
            get => _config.ModManagerExecutable;
            set
            {
                _config.ModManagerExecutable = value;
                OnPropertyChanged();
            }
        }

        public string ModManagerParameters
        {
            get => _config.ModManagerParameters;
            set
            {
                _config.ModManagerParameters = value;
                OnPropertyChanged();
            }
        }

        public string IdeExecutable
        {
            get => _config.IdeExecutable;
            set
            {
                _config.IdeExecutable = value;
                OnPropertyChanged();
            }
        }

        public string PromoteIncludeFiletypes
        {
            get => _config.PromoteIncludeFiletypes != null ? string.Join(",", _config.PromoteIncludeFiletypes) : string.Empty;
            set
            {
                _config.PromoteIncludeFiletypes = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                OnPropertyChanged();
            }
        }

        public string PackageExcludeFiletypes
        {
            get => _config.PackageExcludeFiletypes != null ? string.Join(",", _config.PackageExcludeFiletypes) : string.Empty;
            set
            {
                _config.PackageExcludeFiletypes = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                OnPropertyChanged();
            }
        }

        public string ModStages
        {
            get => _config.ModStages != null ? string.Join(",", _config.ModStages) : string.Empty;
            set
            {
                _config.ModStages = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                OnPropertyChanged();
            }
        }

        public bool LimitFiletypes
        {
            get => _config.LimitFiletypes;
            set
            {
                _config.LimitFiletypes = value;
                OnPropertyChanged();
            }
        }

        public string TimestampFormat
        {
            get => _config.TimestampFormat;
            set
            {
                _config.TimestampFormat = value;
                OnPropertyChanged();
            }
        }

        public string ArchiveFormat
        {
            get => _archiveFormat;
            set
            {
                if (_archiveFormat != value)
                {
                    _archiveFormat = value;
                    OnPropertyChanged(nameof(ArchiveFormat));
                    _config.ArchiveFormat = value;
                }
            }
        }

        public string MyNameSpace
        {
            get => _config.MyNameSpace;
            set
            {
                _config.MyNameSpace = value;
                OnPropertyChanged();
            }
        }

        public string MyResourcePrefix
        {
            get => _config.MyResourcePrefix;
            set
            {
                _config.MyResourcePrefix = value;
                OnPropertyChanged();
            }
        }

        public bool ShowSaveMessage
        {
            get => _config.ShowSaveMessage;
            set
            {
                _config.ShowSaveMessage = value;
                OnPropertyChanged();
            }
        }

        public bool ShowOverwriteMessage
        {
            get => _config.ShowOverwriteMessage;
            set
            {
                _config.ShowOverwriteMessage = value;
                OnPropertyChanged();
            }
        }

        public string NexusAPIKey
        {
            get => _config.NexusAPIKey;
            set
            {
                _config.NexusAPIKey = value;
                OnPropertyChanged();
            }
        }

        private void SaveSettings()
        {
            // Update the Config singleton with the current values
            Config.Instance.RepoFolder = RepoFolder;
            Config.Instance.UseGit = UseGit;
            Config.Instance.GitHubRepo = GitHubRepo;
            Config.Instance.UseModManager = UseModManager;
            Config.Instance.ModStagingFolder = ModStagingFolder;
            Config.Instance.GameFolder = GameFolder;
            Config.Instance.ModManagerExecutable = ModManagerExecutable;
            Config.Instance.ModManagerParameters = ModManagerParameters;
            Config.Instance.IdeExecutable = IdeExecutable;
            Config.Instance.LimitFiletypes = LimitFiletypes;
            Config.Instance.PromoteIncludeFiletypes = PromoteIncludeFiletypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            Config.Instance.PackageExcludeFiletypes = PackageExcludeFiletypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            Config.Instance.TimestampFormat = TimestampFormat;
            Config.Instance.MyNameSpace = MyNameSpace;
            Config.Instance.MyResourcePrefix = MyResourcePrefix;
            Config.Instance.ShowSaveMessage = ShowSaveMessage;
            Config.Instance.ShowOverwriteMessage = ShowOverwriteMessage;
            Config.Instance.NexusAPIKey = NexusAPIKey;
            Config.Instance.ModStages = ModStages.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            Config.Instance.ArchiveFormat = ArchiveFormat;
            Config.Instance.AutoCheckForUpdates = AutoCheckForUpdates;
            Config.Instance.DarkMode = DarkMode;
            Config.Instance.GitHubUsername = GitHubUsername;
            Config.Instance.GitHubToken = GitHubToken;
            Config.Instance.GitHubTokenExpiration = GitHubTokenExpiration;
            Config.Instance.GitHubAuthenticated = GitHubAuthenticated;    

            // Save to YAML and Database
            Config.SaveToYaml();
            Config.SaveToDatabase();
        }

        private void LoadSettings()
        {
            // Load settings from the Config singleton
            RepoFolder = Config.Instance.RepoFolder;
            UseGit = Config.Instance.UseGit;
            GitHubRepo = Config.Instance.GitHubRepo;
            UseModManager = Config.Instance.UseModManager;
            ModStagingFolder = Config.Instance.ModStagingFolder;
            GameFolder = Config.Instance.GameFolder;
            ModManagerExecutable = Config.Instance.ModManagerExecutable;
            ModManagerParameters = Config.Instance.ModManagerParameters;
            IdeExecutable = Config.Instance.IdeExecutable;
            LimitFiletypes = Config.Instance.LimitFiletypes;
            PromoteIncludeFiletypes = string.Join(", ", Config.Instance.PromoteIncludeFiletypes?.Select(s => s.Trim()) ?? new string[0]);
            PackageExcludeFiletypes = string.Join(", ", Config.Instance.PackageExcludeFiletypes?.Select(s => s.Trim()) ?? new string[0]);
            ModStages = string.Join(", ", Config.Instance.ModStages?.Select(s => s.Trim()) ?? new string[0]);
            TimestampFormat = Config.Instance.TimestampFormat;
            MyNameSpace = Config.Instance.MyNameSpace;
            MyResourcePrefix = Config.Instance.MyResourcePrefix;
            ShowSaveMessage = Config.Instance.ShowSaveMessage;
            ShowOverwriteMessage = Config.Instance.ShowOverwriteMessage;
            NexusAPIKey = Config.Instance.NexusAPIKey;
            ArchiveFormat = Config.Instance.ArchiveFormat;
            AutoCheckForUpdates = Config.Instance.AutoCheckForUpdates;
            DarkMode = Config.Instance.DarkMode;
            GitHubUsername = Config.Instance.GitHubUsername;
            GitHubToken = Config.Instance.GitHubToken;
            GitHubTokenExpiration = Config.Instance.GitHubTokenExpiration;
            GitHubAuthenticated = Config.Instance.GitHubAuthenticated;


        }



        private void LaunchGameFolder()
        {
            if (!string.IsNullOrEmpty(Config.Instance.GameFolder) && Directory.Exists(Config.Instance.GameFolder))
            {
                _ = MessageBox.Show("Launching game folder: " + Config.Instance.GameFolder, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = Config.Instance.GameFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                _ = MessageBox.Show("Game folder is not set or does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CloneRepo()
        {
            if (string.IsNullOrEmpty(GitHubRepo) || string.IsNullOrEmpty(RepoFolder))
            {
                MessageBox.Show("GitHub repository URL or local repository folder is not set.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Ensure the RepoFolder exists
                if (!Directory.Exists(RepoFolder))
                {
                    Directory.CreateDirectory(RepoFolder);
                }

                // Clone the repository
                LibGit2Sharp.Repository.Clone(GitHubRepo, RepoFolder);
                MessageBox.Show("Repository cloned successfully.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clone the repository: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }




    }

}