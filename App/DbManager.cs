using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace DevModManager.App
{
    public class DbManager
    {
        private static readonly Lazy<DbManager> _instance = new Lazy<DbManager>(() => new DbManager());
        private static readonly string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeeOgre","DevModManager");
        private static readonly string dbFilePath = Path.Combine(localAppDataPath, "DevModManager.db");
        private static string ConnectionString;

        static DbManager()
        {
            ConnectionString = $"Data Source={dbFilePath};Version=3;";
        }

        private DbManager() { }

        public static DbManager Instance => _instance.Value;

        public void Initialize()
        {
            // Verify local app data files before any database operations
            Config.VerifyLocalAppDataFiles();

            bool dbExists = File.Exists(dbFilePath);
            Debug.WriteLine($"Database file path: {dbFilePath}");
            Debug.WriteLine($"Database exists: {dbExists}");

            using var connection = GetConnection();
            connection.Open();

            if (IsConfigTableEmpty() || !IsDatabaseInitialized())
            {
                var config = Config.LoadFromYaml();
                if (IsSampleOrInvalidData(config))
                {
                    Debug.WriteLine($"Loaded sample data from YAML: {config}");
                    bool settingsSaved = LaunchSettingsWindow(SettingsLaunchSource.DatabaseInitialization);

                    config = Config.LoadFromYaml();
                    if (IsSampleOrInvalidData(config))
                    {
                        Debug.WriteLine("Configuration data is still invalid after settings window.");
                        var resultRetry = MessageBox.Show("Configuration data is invalid. Would you like to retry?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error);
                        if (resultRetry == MessageBoxResult.Yes)
                        {
                            settingsSaved = LaunchSettingsWindow(SettingsLaunchSource.DatabaseInitialization);
                        }
                        else
                        {
                            Debug.WriteLine("User chose not to retry. Shutting down application.");
                            Application.Current.Shutdown();
                            return;
                        }
                    }

                    if (!settingsSaved)
                    {
                        Debug.WriteLine("Settings were not saved. Shutting down application.");
                        Application.Current.Shutdown();
                        return;
                    }
                }
                SetInitializationStatus(true);
            }
            else
            {
                Debug.WriteLine("Loading configuration from database.");
                _ = Config.LoadFromDatabase();
            }
        }

        public static bool IsSampleOrInvalidData(Config config)
        {
            return config.RepoFolder == "<<REPOFOLDER PATH>>" ||
                   config.GitHubRepo == "<<GITHUB REPO PATH>>" ||
                   config.ModStagingFolder == "<<MOD STAGING PATH>>" ||
                   config.GameFolder == "<<GAME ROOT FOLDER>>" ||
                   !Directory.Exists(config.RepoFolder) ||
                   !Directory.Exists(config.ModStagingFolder) ||
                   !Directory.Exists(config.GameFolder);
        }

        public bool LaunchSettingsWindow(SettingsLaunchSource source)
        {
            var settingsWindow = new SettingsWindow(source);
            bool? result = settingsWindow.ShowDialog();
            Debug.WriteLine($"SettingsWindow result: {result}");
            return result == true;
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(ConnectionString);
        }

        private bool IsConfigTableEmpty()
        {
            using var connection = GetConnection();
            connection.Open();
            using var command = new SQLiteCommand("SELECT COUNT(*) FROM Config", connection);
            return Convert.ToInt32(command.ExecuteScalar()) == 0;
        }

        public bool IsDatabaseInitialized()
        {
            using var connection = GetConnection();
            connection.Open();

            // Check if InitializationStatus table exists
            using (var tableCheckCommand = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='InitializationStatus';", connection))
            {
                var tableName = tableCheckCommand.ExecuteScalar();
                if (tableName == null)
                {
                    Debug.WriteLine("InitializationStatus table does not exist.");
                    return false;
                }
            }

            // Check if Config table exists
            using (var tableCheckCommand = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='Config';", connection))
            {
                var tableName = tableCheckCommand.ExecuteScalar();
                if (tableName == null)
                {
                    Debug.WriteLine("Config table does not exist.");
                    return false;
                }
            }

            // Check if the database is marked as initialized
            using (var initCheckCommand = new SQLiteCommand("SELECT IsInitialized FROM InitializationStatus", connection))
            {
                var initialized = initCheckCommand.ExecuteScalar();
                if (initialized == null || !Convert.ToBoolean(initialized))
                {
                    Debug.WriteLine("Database is not marked as initialized.");
                    return false;
                }
            }

            // Check if there is at least one entry in the Config table
            using (var configCheckCommand = new SQLiteCommand("SELECT COUNT(*) FROM Config", connection))
            {
                var configCount = Convert.ToInt32(configCheckCommand.ExecuteScalar());
                if (configCount < 1)
                {
                    Debug.WriteLine("Config table is empty.");
                    return false;
                }
            }

            return true;
        }

        public void SetInitializationStatus(bool status)
        {
            using var connection = GetConnection();
            connection.Open();
            using (var command = new SQLiteCommand("INSERT OR REPLACE INTO InitializationStatus (Id, IsInitialized, InitializationTime) VALUES (1, @IsInitialized, @InitializationTime)", connection))
            {
                _ = command.Parameters.AddWithValue("@IsInitialized", status);
                command.Parameters.AddWithValue("@InitializationTime", DateTime.UtcNow);
                _ = command.ExecuteNonQuery();
            }
            Debug.WriteLine($"Database marked as initialized: {status}");
        }

        public static void FlushDB()
        {
            using var connection = Instance.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                transaction.Commit();

                using (var vacuumCommand = new SQLiteCommand("VACUUM;", connection))
                {
                    _ = vacuumCommand.ExecuteNonQuery();
                }

                using var reindexCommand = new SQLiteCommand("REINDEX;", connection);
                _ = reindexCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during FlushDB: {ex.Message}");
                transaction.Rollback();
            }
            finally
            {
                connection.Close();
            }

            SQLiteConnection.ClearAllPools();
        }
    }
}
