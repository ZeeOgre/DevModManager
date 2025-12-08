using AutoUpdaterDotNET;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using ZO.DMM.AppNF.Properties;
using Microsoft.Win32;
using ControlzEx.Theming;


namespace ZO.DMM.AppNF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool IsSettingsMode { get; private set; }

        public static string CompanyName { get; }
        public static string ProductName { get; }
        public static string PackageID { get; }
        public static string Version { get; }

        static App()
        {
            var assembly = Assembly.GetExecutingAssembly();
            CompanyName = GetAssemblyAttribute<AssemblyCompanyAttribute>(assembly)?.Company ?? "Unknown Company";
            ProductName = GetAssemblyAttribute<AssemblyProductAttribute>(assembly)?.Product ?? "Unknown Product";
            PackageID = GetAssemblyAttribute<AssemblyProductAttribute>(assembly)?.Product ?? "Unknown Product";
            Version = Settings.Default.version ?? "0.0.0.0";
        }

        public App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SetProbingPaths();
            Debug.WriteLine("Application_Startup called");
            var updateUrl = ZO.DMM.AppNF.Properties.Settings.Default.UpdateUrl;

            Config.VerifyLocalAppDataFiles();

            SetThemeBasedOnSystem();

            if (Array.Exists(e.Args, arg => arg.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
            {
                IsSettingsMode = true;
                HandleSettingsMode();
            }
            else
            {
                HandleNormalMode();
            }
        }

        private void HandleSettingsMode()
        {
            Config.InitializeNewInstance();
            Debug.WriteLine("Launching SettingsWindow in settings mode.");
            var settingsWindow = new SettingsWindow(SettingsLaunchSource.CommandLine);
            _ = settingsWindow.ShowDialog();
        }

        private void HandleNormalMode()
        {
            try
            {
                Debug.WriteLine("Initializing database...");
                DbManager.Instance.Initialize();
                Debug.WriteLine("Database initialized.");

                Debug.WriteLine("Initializing configuration...");
                Config.Initialize();
                Debug.WriteLine("Configuration initialized.");

                if (DbManager.Instance.IsDatabaseInitialized())
                {
                    Debug.WriteLine("Database initialized. Opening MainWindow.");
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                }
                else
                {
                    Debug.WriteLine("Database not initialized. Opening SettingsWindow.");
                    var settingsWindow = new SettingsWindow(SettingsLaunchSource.MissingConfig);
                    settingsWindow.Show();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception during startup: {ex.Message}");
                _ = MessageBox.Show($"An error occurred during startup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void RestartApplication()
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null)
            {
                _ = Process.Start(exePath);
                Shutdown();
            }
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            string probingPaths = Settings.Default.ProbingPaths;
            string[] paths = probingPaths.Split(';');

            foreach (string path in paths)
            {
                string assemblyPath = Path.Combine(AppContext.BaseDirectory, path, new AssemblyName(args.Name).Name + ".dll");

                if (File.Exists(assemblyPath))
                {
                    Debug.WriteLine($"Resolved assembly path: {assemblyPath}");
                    return Assembly.LoadFrom(assemblyPath);
                }
            }

            return null;
        }

        public static void CheckForUpdates(Window owner)
        {
            try
            {
                AutoUpdater.SetOwner(owner);
                Debug.WriteLine($"Starting Autoupdate, checking : {ZO.DMM.AppNF.Properties.Settings.Default.UpdateUrl}");
                AutoUpdater.InstallationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeeOgre", "DevModManager", "AutoUpdater");
                Debug.WriteLine($"Autoupdate saving to : {AutoUpdater.InstallationPath}");
                AutoUpdater.ReportErrors = true;
                AutoUpdater.Synchronous = true;
                AutoUpdater.Start(ZO.DMM.AppNF.Properties.Settings.Default.UpdateUrl);
                Debug.WriteLine($"Autoupdate complete");
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Error during auto-check: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static T? GetAssemblyAttribute<T>(Assembly assembly) where T : Attribute
        {
            return (T?)Attribute.GetCustomAttribute(assembly, typeof(T));
        }

        private void SetProbingPaths()
        {
            string probingPaths = ZO.DMM.AppNF.Properties.Settings.Default.ProbingPaths;
            AppDomain.CurrentDomain.SetData("PROBING_DIRECTORIES", probingPaths);
        }

        private void SetThemeBasedOnSystem()
        {
            var isDarkTheme = IsSystemInDarkMode();
            ApplyCustomTheme(isDarkTheme);
        }

        private bool IsSystemInDarkMode()
        {
            const string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string registryValue = "AppsUseLightTheme";

            object? registryValueObject = Registry.GetValue(registryKey, registryValue, null);
            if (registryValueObject is int registryValueInt)
            {
                return registryValueInt == 0; // 0 means dark mode, 1 means light mode
            }

            return false; // Default to light mode if the registry key is not found
        }

        public void ApplyCustomTheme(bool isDarkMode)
        {
            var theme = isDarkMode ? ThemeManager.BaseColorDarkConst : ThemeManager.BaseColorLightConst;
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Remove existing Resource Dictionaries related to themes.
                var existingDictionaries = Application.Current.Resources.MergedDictionaries.ToList();
                foreach (var dictionary in existingDictionaries)
                {
                    if (dictionary.Source != null &&
                        (dictionary.Source.OriginalString.Contains("MaterialDesignTheme.Light.xaml") ||
                         dictionary.Source.OriginalString.Contains("MaterialDesignTheme.Dark.xaml") ||
                         dictionary.Source.OriginalString.Contains("MahApps.Metro;component/Styles/Themes/Light.Blue.xaml") ||
                         dictionary.Source.OriginalString.Contains("MahApps.Metro;component/Styles/Themes/Dark.Blue.xaml") ||
                         dictionary.Source.OriginalString.Contains("Themes/ColorsLight.xaml") ||
                         dictionary.Source.OriginalString.Contains("Themes/ColorsDark.xaml")))
                    {
                        _ = Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                    }
                }

                // Load new theme dictionaries based on the selected mode
                var materialDesignResourcePath = isDarkMode
                    ? "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml"
                    : "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml";

                var mahAppsResourcePath = isDarkMode
                    ? "pack://application:,,,/MahApps.Metro;component/Styles/Themes/Dark.Blue.xaml"
                    : "pack://application:,,,/MahApps.Metro;component/Styles/Themes/Light.Blue.xaml";

                var customColorResourcePath = isDarkMode
                    ? "pack://application:,,,/Themes/ColorsDark.xaml"
                    : "pack://application:,,,/Themes/ColorsLight.xaml";

                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(materialDesignResourcePath) });
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(mahAppsResourcePath) });
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(customColorResourcePath) });

                // Apply MahApps theme
                _ = ThemeManager.Current.ChangeThemeBaseColor(Application.Current, theme);
                _ = ThemeManager.Current.ChangeThemeColorScheme(Application.Current, "Blue");

                // Restart the application to apply the new theme
                //RestartApplication();
            });
        }
    }
}