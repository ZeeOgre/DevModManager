using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ZO.DMM.AppNF
{
    public enum SettingsLaunchSource
    {
        DatabaseInitialization,
        ConfigurationInitialization,
        MissingConfig,
        MissingConfigDialog,
        CommandLine,
        MainWindow
    }

    public partial class SettingsWindow : Window
    {
        private SettingsViewModel _viewModel;
        private bool _isSaveButtonClicked;
        private readonly SettingsLaunchSource _launchSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
        /// </summary>
        public SettingsWindow(SettingsLaunchSource launchSource)
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            DataContext = _viewModel;
            _viewModel.ParentWindow = this;
            _launchSource = launchSource;
            this.Closed += OnSettingsWindowClosed;
            InitializeSettings();
        }

        private void InitializeSettings()
        {
            if (_launchSource == SettingsLaunchSource.CommandLine || _launchSource == SettingsLaunchSource.MissingConfig)
            {
                // Initialize a new blank Config object using a method
                Config.InitializeNewInstance();

                // Load configuration from YAML
                Config.LoadFromYaml();

                // Populate the UI elements from the Config object
                _viewModel.UpdateFromConfig();
                DataContext = _viewModel;
            }
        }

        private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CheckForUpdatesCommand.Execute(null);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateConfigFromViewModel();

                // Validate the configuration
                if (DbManager.IsSampleOrInvalidData(Config.Instance))
                {
                    MessageBox.Show("The configuration contains invalid or sample data. Please correct it before saving.", "Invalid Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Config.SaveToYaml();
                Config.SaveToDatabase();

                DbManager.Instance.SetInitializationStatus(true);

                if (Config.Instance.ShowSaveMessage)
                {
                    var configText = ConvertConfigToString();
                    MessageBox.Show(configText, "Configuration Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Prompt user to restart the application
                if (_launchSource != SettingsLaunchSource.MainWindow)
                {
                    var restartResult = MessageBox.Show("Configuration saved successfully. Would you like to restart the application?", "Restart Application", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (restartResult == MessageBoxResult.Yes)
                    {
                        Application.Current.Shutdown();
                        Process.Start(Application.ResourceAssembly.Location);
                    }
                }
                _isSaveButtonClicked = true;
               
                HandleExitLogic();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception during save: {ex.Message}");
                MessageBox.Show($"An error occurred during save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleExitLogic()
        {
            if (!_isSaveButtonClicked)
            {
                var result = MessageBox.Show("Configuration was not saved. Do you want to exit without saving?", "Unsaved Configuration", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            // Validate the configuration before closing
            if (DbManager.IsSampleOrInvalidData(Config.Instance))
            {
                var result = MessageBox.Show("The configuration contains invalid or sample data. Do you want to exit without saving?", "Invalid Configuration", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            switch (_launchSource)
            {
                case SettingsLaunchSource.DatabaseInitialization:
                case SettingsLaunchSource.ConfigurationInitialization:
                case SettingsLaunchSource.CommandLine:
                    // Restart the application
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (exePath != null)
                    {
                        Process.Start(exePath);
                        Application.Current.Shutdown();
                    }
                    break;
                case SettingsLaunchSource.MainWindow:
                    // Close the settings window and return to the main window
                    this.Close();
                    break;
            }
        }

        private void LoadYamlButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "YAML files (*.yaml)|*.yaml|All files (*.*)|*.*",
                Title = "Browse for saved settings file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Config.LoadFromYaml(openFileDialog.FileName);
                    _viewModel.UpdateFromConfig();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while loading the YAML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateConfigFromViewModel()
        {
            var config = Config.Instance;
            config.RepoFolder = _viewModel.RepoFolder;
            config.UseGit = _viewModel.UseGit;
            config.GitHubRepo = _viewModel.GitHubRepo;
            config.UseModManager = _viewModel.UseModManager;
            config.ModStagingFolder = _viewModel.ModStagingFolder;
            config.GameFolder = _viewModel.GameFolder;
            config.ModManagerExecutable = _viewModel.ModManagerExecutable;
            config.ModManagerParameters = _viewModel.ModManagerParameters;
            config.IdeExecutable = _viewModel.IdeExecutable;
            config.LimitFiletypes = _viewModel.LimitFiletypes;
            config.PromoteIncludeFiletypes = SplitAndTrim(_viewModel.PromoteIncludeFiletypes);
            config.PackageExcludeFiletypes = SplitAndTrim(_viewModel.PackageExcludeFiletypes);
            config.TimestampFormat = _viewModel.TimestampFormat;
            config.MyNameSpace = _viewModel.MyNameSpace;
            config.MyResourcePrefix = _viewModel.MyResourcePrefix;
            config.ShowSaveMessage = _viewModel.ShowSaveMessage;
            config.ShowOverwriteMessage = _viewModel.ShowOverwriteMessage;
            config.NexusAPIKey = _viewModel.NexusAPIKey;
            config.ModStages = SortModStages(SplitAndTrim(_viewModel.ModStages));
            config.ArchiveFormat = _viewModel.ArchiveFormat;
            config.AutoCheckForUpdates = _viewModel.AutoCheckForUpdates;
        }

        private string[] SplitAndTrim(string input)
        {
            return input?.Split(new[] { ',' }, StringSplitOptions.None).Select(s => s.Trim()).ToArray() ?? new string[0];
        }

        private string[] SortModStages(string[] modStages)
        {
            if (modStages == null) return new string[0];

            var starred = modStages.Where(s => s.StartsWith("*")).ToArray();
            var normal = modStages.Where(s => !s.StartsWith("*") && !s.StartsWith("#")).ToArray();
            var hashed = modStages.Where(s => s.StartsWith("#")).ToArray();

            return starred.Concat(normal).Concat(hashed).ToArray();
        }

        private string ConvertConfigToString()
        {
            return $@"
                            RepoFolder: {Config.Instance.RepoFolder}
                            UseGit: {Config.Instance.UseGit}
                            GitHubRepo: {Config.Instance.GitHubRepo}
                            UseModManager: {Config.Instance.UseModManager}
                            ModStagingFolder: {Config.Instance.ModStagingFolder}
                            GameFolder: {Config.Instance.GameFolder}
                            ModManagerExecutable: {Config.Instance.ModManagerExecutable}
                            ModManagerParameters: {Config.Instance.ModManagerParameters}
                            IdeExecutable: {Config.Instance.IdeExecutable}
                            LimitFiletypes: {Config.Instance.LimitFiletypes}
                            PromoteIncludeFiletypes: {string.Join(", ", Config.Instance.PromoteIncludeFiletypes ?? new string[0])}
                            PackageExcludeFiletypes: {string.Join(", ", Config.Instance.PackageExcludeFiletypes ?? new string[0])}
                            TimestampFormat: {Config.Instance.TimestampFormat}
                            MyNameSpace: {Config.Instance.MyNameSpace}
                            MyResourcePrefix: {Config.Instance.MyResourcePrefix}
                            ShowSaveMessage: {Config.Instance.ShowSaveMessage}
                            ShowOverwriteMessage: {Config.Instance.ShowOverwriteMessage}
                            NexusAPIKey: {Config.Instance.NexusAPIKey}
                            ModStages: {string.Join(", ", Config.Instance.ModStages ?? new string[0])}
                            ArchiveFormat: {Config.Instance.ArchiveFormat}
                            AutoCheckForUpdates: {Config.Instance.AutoCheckForUpdates}  
                        ";
        }

        private void SelectFolder(TextBox textBox)
        {
            var dialog = new OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            if (!string.IsNullOrEmpty(textBox.Text) && Directory.Exists(textBox.Text))
            {
                dialog.InitialDirectory = textBox.Text;
            }

            if (dialog.ShowDialog() == true)
            {
                textBox.Text = Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void RepoFolderButton_Click(object sender, RoutedEventArgs e)
        {
            SelectFolder(RepoFolderTextBox);
        }

        private void ModStagingFolderButton_Click(object sender, RoutedEventArgs e)
        {
            SelectFolder(ModStagingFolderTextBox);
        }

        private void GameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            SelectFolder(GameFolderTextBox);
        }

        private void ModManagerExecutableButton_Click(object sender, RoutedEventArgs e)
        {
            SelectFile(ModManagerExecutableTextBox);
        }

        private void IDEExecutableButton_Click(object sender, RoutedEventArgs e)
        {
            SelectFile(IDEExecutableTextBox);
        }

        private void SelectFile(TextBox textBox)
        {
            var dialog = new OpenFileDialog();

            if (!string.IsNullOrEmpty(textBox.Text) && Directory.Exists(Path.GetDirectoryName(textBox.Text)))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(textBox.Text);
            }

            if (dialog.ShowDialog() == true)
            {
                textBox.Text = dialog.FileName;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Just close the window without setting the dialog result
            this.Close();
        }

        /// <summary>
        /// Called when the window is closed.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Only handle exit logic if the Save button was clicked and the launch source is the main window
            if (_isSaveButtonClicked && _launchSource == SettingsLaunchSource.MainWindow)
            {
                HandleExitLogic();
            }
        }

        private void OnSettingsWindowClosed(object sender, EventArgs e)
        {
            if (_launchSource != SettingsLaunchSource.MainWindow)
            {
                DbManager.FlushDB();
            }
        }
    }
}
