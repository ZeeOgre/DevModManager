using Microsoft.Win32;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DevModManager.App
{

    /// <summary>
    /// Configuration class for DevModManager.App.
    /// </summary>
    public class Config
    {
        private static Config? _instance;
        private static readonly object _lock = new object();
        private static readonly string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeeOgre", "DevModManager");
        public static readonly string configFilePath = Path.Combine(localAppDataPath, "config.yaml");
        public static readonly string dbFilePath = Path.Combine(localAppDataPath, "DevModManager.db");
        private static bool _isVerificationInProgress = false; // Flag to track verification

        /// <summary>
        /// Gets the singleton instance of the Config class.
        /// </summary>
        public static Config Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Config();
                        }
                    }
                }
                return _instance;
            }
        }

        public string? RepoFolder { get; set; }
        public bool UseGit { get; set; }
        public string? GitHubRepo { get; set; }
        public bool UseModManager { get; set; }
        public string? ModStagingFolder { get; set; }
        public string? GameFolder { get; set; }
        public string? ModManagerExecutable { get; set; }
        public string? ModManagerParameters { get; set; }
        public string? IdeExecutable { get; set; }
        public string[]? ModStages { get; set; }
        public bool LimitFiletypes { get; set; }
        public string[]? PromoteIncludeFiletypes { get; set; }
        public string[]? PackageExcludeFiletypes { get; set; }
        public string? TimestampFormat { get; set; }
        public string? ArchiveFormat { get; set; }
        public string? MyNameSpace { get; set; }
        public string? MyResourcePrefix { get; set; }
        public bool ShowSaveMessage { get; set; }
        public bool ShowOverwriteMessage { get; set; }
        public string? NexusAPIKey { get; set; }
        public bool AutoCheckForUpdates { get; set; }   



        /// <summary>
        /// Initializes the configuration.
        /// </summary>
        public static void Initialize()
        {
            //MessageBox.Show($"Regular Init: Config file path: {configFilePath}\nDB file path: {dbFilePath}", "Initialization Paths");

            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        VerifyLocalAppDataFiles();
                        if (File.Exists(dbFilePath))
                        {
                            _ = LoadFromDatabase();
                        }
                        else
                        {
                            _ = LoadFromYaml();
                        }
                    }
                }
            }
        }
        
        public static void InitializeNewInstance()
        {
            _instance = new Config();
            //MessageBox.Show($"Special Blank Init:Config file path: {configFilePath}\nDB file path: {dbFilePath}", "Initialization Paths");

        }

        public static void VerifyLocalAppDataFiles()
        {
            if (_isVerificationInProgress)
            {
                return; // Exit if verification is already in progress
            }

            _isVerificationInProgress = true;

            try
            {
                if (!Directory.Exists(localAppDataPath))
                {
                    Directory.CreateDirectory(localAppDataPath);
                    return;
                }

                if (!File.Exists(dbFilePath))
                {
                    string sampleDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "DevModManager.db");
                    if (File.Exists(sampleDbPath))
                    {
                        var result = MessageBox.Show("The database file is missing. Would you like to copy the sample data over?", "Database Missing", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            File.Copy(sampleDbPath, dbFilePath);
                            MessageBox.Show("Sample data copied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Database file is missing. Please reinstall the application and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("The database file is missing and no sample data is available. Please reinstall the application and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
                        return;
                    }
                }

                if (!File.Exists(configFilePath))
                {
                    string sampleConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "config.yaml");
                    if (File.Exists(sampleConfigPath))
                    {
                        File.Copy(sampleConfigPath, configFilePath);
                        MessageBox.Show("Sample config copied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("The config file is missing and no sample data is available. Please reinstall the application and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
                        return;
                    }
                }
            }
            finally
            {
                _isVerificationInProgress = false;
            }
        }

        /// <summary>
        /// Loads the configuration from a YAML file.
        /// </summary>
        /// <returns>The loaded configuration.</returns>
        public static Config LoadFromYaml()
        {
            return LoadFromYaml(configFilePath);
        }

        /// <summary>
        /// Saves the configuration to a YAML file.
        /// </summary>
        public static void SaveToYaml()
        {
            SaveToYaml(configFilePath);
        }

        /// <summary>
        /// Loads the configuration from a specified YAML file.
        /// </summary>
        /// <param name="filePath">The path to the YAML file.</param>
        /// <returns>The loaded configuration.</returns>
        public static Config LoadFromYaml(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Configuration file not found", filePath);
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            using var reader = new StreamReader(filePath);
            var config = deserializer.Deserialize<Config>(reader);

            lock (_lock)
            {
                _instance = config;
            }

            return _instance;
        }

        /// <summary>
        /// Saves the configuration to a specified YAML file.
        /// </summary>
        /// <param name="filePath">The path to the YAML file.</param>
        public static void SaveToYaml(string filePath)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(Instance);

            File.WriteAllText(filePath, yaml);
        }

        /// <summary>
        /// Loads the configuration from the database.
        /// </summary>
        /// <returns>The loaded configuration.</returns>
        public static Config? LoadFromDatabase()
        {
            using (var connection = DbManager.Instance.GetConnection())
            {
                connection.Open();
                using var command = new SQLiteCommand("SELECT * FROM vwConfig", connection);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    _instance = new Config
                    {
                        RepoFolder = reader["RepoFolder"]?.ToString(),
                        UseGit = Convert.ToBoolean(reader["UseGit"]),
                        GitHubRepo = reader["GitHubRepo"]?.ToString(),
                        UseModManager = Convert.ToBoolean(reader["UseModManager"]),
                        GameFolder = reader["GameFolder"]?.ToString(),
                        ModStagingFolder = reader["ModStagingFolder"]?.ToString(),
                        ModManagerExecutable = reader["ModManagerExecutable"]?.ToString(),
                        ModManagerParameters = reader["ModManagerParameters"]?.ToString(),
                        IdeExecutable = reader["IDEExecutable"]?.ToString(),
                        LimitFiletypes = Convert.ToBoolean(reader["LimitFileTypes"]),
                        PromoteIncludeFiletypes = reader["PromoteIncludeFiletypes"]?.ToString()?.Split(',') ?? Array.Empty<string>(),
                        PackageExcludeFiletypes = reader["PackageExcludeFiletypes"]?.ToString()?.Split(',') ?? Array.Empty<string>(),
                        TimestampFormat = reader["TimestampFormat"]?.ToString(),
                        MyNameSpace = reader["MyNameSpace"]?.ToString(),
                        MyResourcePrefix = reader["MyResourcePrefix"]?.ToString(),
                        ShowSaveMessage = Convert.ToBoolean(reader["ShowSaveMessage"]),
                        ShowOverwriteMessage = Convert.ToBoolean(reader["ShowOverwriteMessage"]),
                        NexusAPIKey = reader["NexusAPIKey"]?.ToString(),
                        ModStages = reader["ModStages"]?.ToString()?.Split(',') ?? Array.Empty<string>(),
                        ArchiveFormat = reader["ArchiveFormat"]?.ToString(),
                        AutoCheckForUpdates = Convert.ToBoolean(reader["AutoCheckForUpdates"])

                    };
                }
            }
            return _instance;
        }

        /// <summary>
        /// Asynchronously initializes the configuration.
        /// </summary>
        //public static async Task InitializeAsync()
        //{
        //    if (_instance == null)
        //    {
        //        if (File.Exists(dbFilePath))
        //        {
        //            _instance = await LoadFromDatabaseAsync();
        //        }
        //        else
        //        {
        //            _instance = await LoadFromYamlAsync(configFilePath);
        //        }
        //    }
        //}

        /// <summary>
        /// Asynchronously loads the configuration from a specified YAML file.
        /// </summary>
        /// <param name="filePath">The path to the YAML file.</param>
        /// <returns>The loaded configuration.</returns>
        //public static async Task<Config> LoadFromYamlAsync(string filePath)
        //{
        //    if (!File.Exists(filePath))
        //    {
        //        throw new FileNotFoundException("Configuration file not found", filePath);
        //    }

        //    var deserializer = new DeserializerBuilder()
        //        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        //        .Build();

        //    using var reader = new StreamReader(filePath);
        //    var config = deserializer.Deserialize<Config>(await reader.ReadToEndAsync());

        //    lock (_lock)
        //    {
        //        _instance = config;
        //    }

        //    return _instance;
        //}

        /// <summary>
        /// Asynchronously loads the configuration from the database.
        /// </summary>
        /// <returns>The loaded configuration.</returns>
        //public static async Task<Config?> LoadFromDatabaseAsync()
        //{
        //    using (var connection = DbManager.Instance.GetConnection())
        //    {
        //        await connection.OpenAsync();
        //        using var command = new SQLiteCommand("SELECT * FROM vwConfig", connection);
        //        using var reader = await command.ExecuteReaderAsync();
        //        if (await reader.ReadAsync())
        //        {
        //            _instance = new Config
        //            {
        //                // Populate properties from reader
        //            };
        //        }
        //    }
        //    return _instance;
        //}

        /// <summary>
        /// Saves the configuration to the database.
        /// </summary>
        public static void SaveToDatabase()
        {
            var config = Instance;

            using var connection = DbManager.Instance.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = "DELETE FROM Config";
                _ = command.ExecuteNonQuery();

                command.CommandText = @"
                                INSERT INTO Config (
                                    RepoFolder,
                                    UseGit,
                                    GitHubRepo,
                                    UseModManager,
                                    GameFolder,
                                    ModStagingFolder,
                                    ModManagerExecutable,
                                    ModManagerParameters,
                                    IDEExecutable,
                                    LimitFileTypes,
                                    PromoteIncludeFiletypes,
                                    PackageExcludeFiletypes,
                                    TimestampFormat,
                                    MyNameSpace,
                                    MyResourcePrefix,
                                    ShowSaveMessage,
                                    ShowOverwriteMessage,
                                    NexusAPIKey,
                                    ArchiveFormatID,
                                    AutoCheckForUpdates
                                ) VALUES (
                                    @RepoFolder,
                                    @UseGit,
                                    @GitHubRepo,
                                    @UseModManager,
                                    @GameFolder,
                                    @ModStagingFolder,
                                    @ModManagerExecutable,
                                    @ModManagerParameters,
                                    @IdeExecutable,
                                    @LimitFiletypes,
                                    @PromoteIncludeFiletypes,
                                    @PackageExcludeFiletypes,
                                    @TimestampFormat,
                                    @MyNameSpace,
                                    @MyResourcePrefix,
                                    @ShowSaveMessage,
                                    @ShowOverwriteMessage,
                                    @NexusAPIKey,
                                    (SELECT ArchiveFormatID FROM ArchiveFormats WHERE FormatName = @ArchiveFormat),
                                    @AutoCheckForUpdates
                                )";

                command.Parameters.AddWithValue("@RepoFolder", config.RepoFolder ?? (object)DBNull.Value);
                _ = command.Parameters.AddWithValue("@UseGit", config.UseGit);
                command.Parameters.AddWithValue("@GitHubRepo", config.GitHubRepo ?? (object)DBNull.Value);
                _ = command.Parameters.AddWithValue("@UseModManager", config.UseModManager);
                command.Parameters.AddWithValue("@GameFolder", config.GameFolder ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ModStagingFolder", config.ModStagingFolder ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ModManagerExecutable", config.ModManagerExecutable ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ModManagerParameters", config.ModManagerParameters ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@IdeExecutable", config.IdeExecutable ?? (object)DBNull.Value);
                _ = command.Parameters.AddWithValue("@LimitFiletypes", config.LimitFiletypes);
                _ = command.Parameters.AddWithValue("@PromoteIncludeFiletypes", string.Join(",", config.PromoteIncludeFiletypes ?? Array.Empty<string>()));
                _ = command.Parameters.AddWithValue("@PackageExcludeFiletypes", string.Join(",", config.PackageExcludeFiletypes ?? Array.Empty<string>()));
                command.Parameters.AddWithValue("@TimestampFormat", config.TimestampFormat ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@MyNameSpace", config.MyNameSpace ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@MyResourcePrefix", config.MyResourcePrefix ?? (object)DBNull.Value);
                _ = command.Parameters.AddWithValue("@ShowSaveMessage", config.ShowSaveMessage);
                _ = command.Parameters.AddWithValue("@ShowOverwriteMessage", config.ShowOverwriteMessage);
                command.Parameters.AddWithValue("@NexusAPIKey", config.NexusAPIKey ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ArchiveFormat", config.ArchiveFormat ?? (object)DBNull.Value);
                _ = command.Parameters.AddWithValue("@AutoCheckForUpdates", config.AutoCheckForUpdates);

                _ = command.ExecuteNonQuery();
            }

            if (config.ModStages != null && config.ModStages.Length > 0)
            {
                using (var deleteCommand = new SQLiteCommand("DELETE FROM Stages WHERE IsReserved = 0", connection))
                {
                    _ = deleteCommand.ExecuteNonQuery();
                }

                foreach (var stage in config.ModStages)
                {
                    var stageName = stage.TrimStart('*', '#');
                    var isSource = stage.StartsWith('*');
                    var isReserved = stage.StartsWith('#');

                    using var stageCommand = new SQLiteCommand(connection);
                    stageCommand.CommandText = @"
                                        INSERT OR REPLACE INTO Stages (StageName, IsSource, IsReserved) 
                                        VALUES (@StageName, @IsSource, @IsReserved)";
                    _ = stageCommand.Parameters.AddWithValue("@StageName", stageName);
                    _ = stageCommand.Parameters.AddWithValue("@IsSource", isSource);
                    _ = stageCommand.Parameters.AddWithValue("@IsReserved", isReserved);

                    _ = stageCommand.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }
    }
}