using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using AutoUpdaterDotNET;



namespace DevModManager.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool IsSettingsMode { get; private set; }
        public static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        public static string Company { get; }
        public static string PackageID { get; }
        public static string Version { get; }

        static App()
        {
            var assembly = Assembly.GetExecutingAssembly();
            Company = GetAssemblyAttribute<AssemblyCompanyAttribute>(assembly)?.Company ?? "Unknown Company";
            PackageID = GetAssemblyAttribute<AssemblyProductAttribute>(assembly)?.Product ?? "Unknown Product";
            Version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        }

        public App()
        {
            AssemblyLoadContext.Default.Resolving += OnAssemblyResolve;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Debug.WriteLine("Application_Startup called");
            var updateUrl = DevModManager.App.Properties.Settings.Default.UpdateUrl;

            // Initialize AutoUpdater.NET
            AutoUpdater.ReportErrors = true;
            AutoUpdater.Start(updateUrl);

            SetProbingPaths();

            Config.VerifyLocalAppDataFiles();

            if (e.Args.Contains("--settings"))
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
                SetProbingPaths();

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

        private static Assembly? OnAssemblyResolve(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            string probingPaths = DevModManager.App.Properties.Settings.Default.ProbingPaths;
            string[] paths = probingPaths.Split(';');

            foreach (string path in paths)
            {
                string assemblyPath = Path.Combine(AppContext.BaseDirectory, path, $"{assemblyName.Name}.dll");

                if (File.Exists(assemblyPath))
                {
                    return context.LoadFromAssemblyPath(assemblyPath);
                }
            }

            return null;
        }

        private static T? GetAssemblyAttribute<T>(Assembly assembly) where T : Attribute
        {
            return (T?)Attribute.GetCustomAttribute(assembly, typeof(T));
        }

        private void SetProbingPaths()
        {
            string probingPaths = DevModManager.App.Properties.Settings.Default.ProbingPaths;
            AppDomain.CurrentDomain.SetData("PROBING_DIRECTORIES", probingPaths);
        }
    }
}           