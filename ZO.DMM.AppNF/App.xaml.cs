using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using AutoUpdaterDotNET;



namespace ZO.DMM.AppNF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool IsSettingsMode { get; private set; }

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
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Debug.WriteLine("Application_Startup called");
            var updateUrl = ZO.DMM.AppNF.Properties.Settings.Default.UpdateUrl;

            SetProbingPaths();

            Config.VerifyLocalAppDataFiles();

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

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string probingPaths = ZO.DMM.AppNF.Properties.Settings.Default.ProbingPaths;
            string[] paths = probingPaths.Split(';');

            foreach (string path in paths)
            {
                string assemblyPath = Path.Combine(AppContext.BaseDirectory, path, new AssemblyName(args.Name).Name + ".dll");

                if (File.Exists(assemblyPath))
                {
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
                MessageBox.Show($"Error during auto-check: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private static T GetAssemblyAttribute<T>(Assembly assembly) where T : Attribute
        {
            return (T)Attribute.GetCustomAttribute(assembly, typeof(T));
        }

        private void SetProbingPaths()
        {
            string probingPaths = ZO.DMM.AppNF.Properties.Settings.Default.ProbingPaths;
            AppDomain.CurrentDomain.SetData("PROBING_DIRECTORIES", probingPaths);
        }
    }
}           