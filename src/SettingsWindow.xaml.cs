using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using MahApps.Metro.Controls;
using MaterialDesignThemes.Wpf;
using Octokit;
using Octokit.Internal;
using Application = System.Windows.Application;
using System.Net;


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

    public partial class SettingsWindow : MetroWindow
    {
        private SettingsViewModel _viewModel;
        private bool _isSaveButtonClicked;
        private readonly SettingsLaunchSource _launchSource;
        private GitHubClient _gitHubClient;

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
                _ = Config.LoadFromYaml();

                // Populate the UI elements from the Config object
                _viewModel.UpdateFromConfig();
                DataContext = _viewModel;
            }
        }


        //  GH Client ID Ov23liI5VnvjIcTPKLIk
        //  GH API Secret c0bbaa6651d29ca1f0368ac3924d0e00dce3acb9


        private async Task AuthenticateUserWithGitHub()
        {
            try
            {
                var clientId = "Ov23liI5VnvjIcTPKLIk";
                var clientSecret = "c0bbaa6651d29ca1f0368ac3924d0e00dce3acb9";
                var oauthClient = new GitHubClient(new ProductHeaderValue("ZO.DevModManager"));

                var request = new OauthLoginRequest(clientId)
                {
                    Scopes = { "repo", "user" },
                    RedirectUri = new Uri("http://localhost:53306/")
                };

                var authorizeUrl = oauthClient.Oauth.GetGitHubLoginUrl(request);

                Process.Start(new ProcessStartInfo
                {
                    FileName = authorizeUrl.ToString(),
                    UseShellExecute = true
                });

                var httpListener = new HttpListener();
                httpListener.Prefixes.Add("http://localhost:53306/");
                httpListener.Start();

                // Wait for the callback from GitHub
                var context = await httpListener.GetContextAsync();
                var code = context.Request.QueryString["code"];

                if (string.IsNullOrEmpty(code))
                {
                    MessageBox.Show("Failed to receive authorization code from GitHub.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Send a response back to the browser
                var response = context.Response;
                string responseString = "<html><body>Authentication successful! You can close this window.</body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                var output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();

                // Exchange the code for an access token
                var tokenRequest = new OauthTokenRequest(clientId, clientSecret, code);
                var token = await oauthClient.Oauth.CreateAccessToken(tokenRequest);

                if (!string.IsNullOrEmpty(token.AccessToken))
                {
                    _viewModel.GitHubTokenExpiration = DateTime.UtcNow.AddDays(90);
                    _gitHubClient = new GitHubClient(new ProductHeaderValue("ZO.DevModManager"))
                    {
                        Credentials = new Octokit.Credentials(token.AccessToken)
                    };

                    var user = await _gitHubClient.User.Current();
                    _viewModel.GitHubUsername = user.Login;
                    _viewModel.GitHubAuthenticated = true;

                    Config.Instance.GitHubToken = token.AccessToken;
                    Config.Instance.GitHubUsername = user.Login;
                    Config.Instance.GitHubTokenExpiration = _viewModel.GitHubTokenExpiration;
                    Config.Instance.GitHubAuthenticated = true;

                    Config.SaveToDatabase();

                    MessageBox.Show($"Authenticated as {user.Login}", "Authentication Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Authentication failed. Please try again.", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                httpListener.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during authentication: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private void AuthenticateButton_Click(object sender, RoutedEventArgs e)
        {
            _ = AuthenticateUserWithGitHub();
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
                    _ = MessageBox.Show("The configuration contains invalid or sample data. Please correct it before saving.", "Invalid Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Config.SaveToYaml();
                Config.SaveToDatabase();

                DbManager.Instance.SetInitializationStatus(true);

                if (Config.Instance.ShowSaveMessage)
                {
                    var configText = ConvertConfigToString();
                    _ = MessageBox.Show(configText, "Configuration Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Prompt user to restart the application
                if (_launchSource != SettingsLaunchSource.MainWindow)
                {
                    var restartResult = MessageBox.Show("Configuration saved successfully. Would you like to restart the application?", "Restart Application", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (restartResult == MessageBoxResult.Yes)
                    {
                        Application.Current.Shutdown();
                        _ = Process.Start(Application.ResourceAssembly.Location);
                    }
                }
                _isSaveButtonClicked = true;

                HandleExitLogic();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception during save: {ex.Message}");
                _ = MessageBox.Show($"An error occurred during save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (_launchSource != SettingsLaunchSource.CommandLine)
            {
                DialogResult = true; // Only set DialogResult if not in command line mode
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
                        _ = Process.Start(exePath);
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
                    _ = Config.LoadFromYaml(openFileDialog.FileName);
                    _viewModel.UpdateFromConfig();
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show($"An error occurred while loading the YAML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
