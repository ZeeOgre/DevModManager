using Microsoft.Win32;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ZO.DMM.AppNF
{
    public class Config
    {
        private static Config _instance;
        private static readonly object _lock = new object();
        private static readonly string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeeOgre", "DevModManager");
        public static readonly string configFilePath = Path.Combine(localAppDataPath, "config.yaml");
        public static readonly string dbFilePath = Path.Combine(localAppDataPath, "DevModManager.db");
        private static bool _isVerificationInProgress = false;

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

        public string RepoFolder { get; set; } = string.Empty;
        public bool UseGit { get; set; }
        public string GitHubRepo { get; set; } = string.Empty;
        public bool UseModManager { get; set; }
        public string ModStagingFolder { get; set; } = string.Empty;
        public string GameFolder { get; set; } = string.Empty;
        public string ModManagerExecutable { get; set; } = string.Empty;
        public string ModManagerParameters { get; set; } = string.Empty;
        public string IdeExecutable { get; set; } = string.Empty;
        public string[] ModStages { get; set; } = new string[0];
        public bool LimitFiletypes { get; set; }
        public string[] PromoteIncludeFiletypes { get; set; } = new string[0];
        public string[] PackageExcludeFiletypes { get; set; } = new string[0];
        public string TimestampFormat { get; set; } = string.Empty;
        public string ArchiveFormat { get; set; } = string.Empty;
        public string MyNameSpace { get; set; } = string.Empty;
        public string MyResourcePrefix { get; set; } = string.Empty;
        public bool ShowSaveMessage { get; set; }
        public bool ShowOverwriteMessage { get; set; }
        public string NexusAPIKey { get; set; } = string.Empty;
        public bool AutoCheckForUpdates { get; set; }

        public static void Initialize()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        VerifyLocalAppDataFiles();
                        if (File.Exists(dbFilePath))
                        {
                            _instance = LoadFromDatabase();
                        }
                        else
                        {
                            _instance = LoadFromYaml(configFilePath);
                        }
                    }
                }
            }
        }

        public static void InitializeNewInstance()
        {
            _instance = new Config();
        }

        public static void VerifyLocalAppDataFiles()
        {
            if (_isVerificationInProgress)
            {
                return;
            }

            _isVerificationInProgress = true;

            try
            {
                if (!Directory.Exists(localAppDataPath))
                {
                    _ = Directory.CreateDirectory(localAppDataPath);
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
                            _ = MessageBox.Show("Sample data copied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            _ = MessageBox.Show("Database file is missing. Please reinstall the application and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
                            return;
                        }
                    }
                    else
                    {
                        _ = MessageBox.Show("The database file is missing and no sample data is available. Please reinstall the application and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        _ = MessageBox.Show("Sample config copied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        _ = MessageBox.Show("The config file is missing and no sample data is available. Please reinstall the application and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public static Config LoadFromYaml()
        {
            return LoadFromYaml(configFilePath);
        }

        public static void SaveToYaml()
        {
            SaveToYaml(configFilePath);
        }

        public static Config LoadFromYaml(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Configuration file not found", filePath);
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            using (var reader = new StreamReader(filePath))
            {
                var config = deserializer.Deserialize<Config>(reader);

                lock (_lock)
                {
                    _instance = config;
                }

                return _instance;
            }
        }

        public static void SaveToYaml(string filePath)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(Instance);

            File.WriteAllText(filePath, yaml);
        }

        public static Config LoadFromDatabase()
        {
            using (var connection = DbManager.Instance.GetConnection())
            {
                connection.Open();
                using (var command = new SQLiteCommand("SELECT * FROM vwConfig", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            _instance = new Config
                            {
                                RepoFolder = reader["RepoFolder"] != DBNull.Value ? reader["RepoFolder"].ToString() : string.Empty,
                                UseGit = Convert.ToBoolean(reader["UseGit"]),
                                GitHubRepo = reader["GitHubRepo"] != DBNull.Value ? reader["GitHubRepo"].ToString() : string.Empty,
                                UseModManager = Convert.ToBoolean(reader["UseModManager"]),
                                GameFolder = reader["GameFolder"] != DBNull.Value ? reader["GameFolder"].ToString() : string.Empty,
                                ModStagingFolder = reader["ModStagingFolder"] != DBNull.Value ? reader["ModStagingFolder"].ToString() : string.Empty,
                                ModManagerExecutable = reader["ModManagerExecutable"] != DBNull.Value ? reader["ModManagerExecutable"].ToString() : string.Empty,
                                ModManagerParameters = reader["ModManagerParameters"] != DBNull.Value ? reader["ModManagerParameters"].ToString() : string.Empty,
                                IdeExecutable = reader["IDEExecutable"] != DBNull.Value ? reader["IDEExecutable"].ToString() : string.Empty,
                                LimitFiletypes = Convert.ToBoolean(reader["LimitFileTypes"]),
                                PromoteIncludeFiletypes = reader["PromoteIncludeFiletypes"] != DBNull.Value ? reader["PromoteIncludeFiletypes"].ToString().Split(new char[] { ',' }) : new string[0],
                                PackageExcludeFiletypes = reader["PackageExcludeFiletypes"] != DBNull.Value ? reader["PackageExcludeFiletypes"].ToString().Split(new char[] { ',' }) : new string[0],
                                TimestampFormat = reader["TimestampFormat"] != DBNull.Value ? reader["TimestampFormat"].ToString() : string.Empty,
                                MyNameSpace = reader["MyNameSpace"] != DBNull.Value ? reader["MyNameSpace"].ToString() : string.Empty,
                                MyResourcePrefix = reader["MyResourcePrefix"] != DBNull.Value ? reader["MyResourcePrefix"].ToString() : string.Empty,
                                ShowSaveMessage = Convert.ToBoolean(reader["ShowSaveMessage"]),
                                ShowOverwriteMessage = Convert.ToBoolean(reader["ShowOverwriteMessage"]),
                                NexusAPIKey = reader["NexusAPIKey"] != DBNull.Value ? reader["NexusAPIKey"].ToString() : string.Empty,
                                ModStages = reader["ModStages"] != DBNull.Value ? reader["ModStages"].ToString().Split(new char[] { ',' }) : new string[0],
                                ArchiveFormat = reader["ArchiveFormat"] != DBNull.Value ? reader["ArchiveFormat"].ToString() : string.Empty,
                                AutoCheckForUpdates = Convert.ToBoolean(reader["AutoCheckForUpdates"])
                            };
                        }
                    }
                }
            }
            return _instance;
        }


        public static void SaveToDatabase()
        {
            var config = Instance;

            using (var connection = DbManager.Instance.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
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

                        _ = command.Parameters.AddWithValue("@RepoFolder", config.RepoFolder ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@UseGit", config.UseGit);
                        _ = command.Parameters.AddWithValue("@GitHubRepo", config.GitHubRepo ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@UseModManager", config.UseModManager);
                        _ = command.Parameters.AddWithValue("@GameFolder", config.GameFolder ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@ModStagingFolder", config.ModStagingFolder ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@ModManagerExecutable", config.ModManagerExecutable ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@ModManagerParameters", config.ModManagerParameters ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@IdeExecutable", config.IdeExecutable ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@LimitFiletypes", config.LimitFiletypes);
                        _ = command.Parameters.AddWithValue("@PromoteIncludeFiletypes", string.Join(",", config.PromoteIncludeFiletypes ?? Array.Empty<string>()));
                        _ = command.Parameters.AddWithValue("@PackageExcludeFiletypes", string.Join(",", config.PackageExcludeFiletypes ?? Array.Empty<string>()));
                        _ = command.Parameters.AddWithValue("@TimestampFormat", config.TimestampFormat ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@MyNameSpace", config.MyNameSpace ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@MyResourcePrefix", config.MyResourcePrefix ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@ShowSaveMessage", config.ShowSaveMessage);
                        _ = command.Parameters.AddWithValue("@ShowOverwriteMessage", config.ShowOverwriteMessage);
                        _ = command.Parameters.AddWithValue("@NexusAPIKey", config.NexusAPIKey ?? (object)DBNull.Value);
                        _ = command.Parameters.AddWithValue("@ArchiveFormat", config.ArchiveFormat ?? (object)DBNull.Value);
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
                            var isSource = stage.StartsWith("*");
                            var isReserved = stage.StartsWith("#");

                            using (var stageCommand = new SQLiteCommand(connection))
                            {
                                stageCommand.CommandText = @"
                                                        INSERT OR REPLACE INTO Stages (StageName, IsSource, IsReserved) 
                                                        VALUES (@StageName, @IsSource, @IsReserved)";
                                _ = stageCommand.Parameters.AddWithValue("@StageName", stageName);
                                _ = stageCommand.Parameters.AddWithValue("@IsSource", isSource);
                                _ = stageCommand.Parameters.AddWithValue("@IsReserved", isReserved);

                                _ = stageCommand.ExecuteNonQuery();
                            }
                        }
                    }

                    transaction.Commit();
                }
            }
        }
    }
}