using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace ZO.DMM.AppNF
{
    public partial class ModItem
    {
        public class DB
        {
            //write moditems into the database
            public static void WriteMod(ModItem modItem)
            {
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // Insert or replace ModItem
                        string query = @"
                            INSERT OR REPLACE INTO ModItems (ModName, ModFolderPath, CurrentStageID)
                            VALUES (@ModName, @ModFolderPath, 
                                    (SELECT StageId FROM Stages WHERE StageName = @DeployedStage))";
                        using (var command = new SQLiteCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@ModName", modItem.ModName);
                            command.Parameters.AddWithValue("@ModFolderPath", modItem.ModFolderPath);

                            if (modItem.DeployedStage != null)
                            {
                                command.Parameters.AddWithValue("@DeployedStage", modItem.DeployedStage);
                            }
                            else
                            {
                                command.Parameters.AddWithValue("@DeployedStage", DBNull.Value);
                            }

                            command.ExecuteNonQuery();
                        }

                        // Insert or replace files
                        foreach (var fileEntry in modItem.ModFiles)
                        {
                            query = @"
                                INSERT OR REPLACE INTO FileInfo (ModID, StageID, Filename, RelativePath, DTStamp, HASH, isArchive)
                                VALUES ((SELECT ModID FROM ModItems WHERE ModName = @ModName),
                                        (SELECT StageID FROM Stages WHERE StageName = @Stage),
                                        @FullPath, @RelativePath, @DTStamp, @HASH, @isArchive)";

                            using (var command = new SQLiteCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@ModName", modItem.ModName);
                                command.Parameters.AddWithValue("@Stage", fileEntry.Value.Stage);
                                command.Parameters.AddWithValue("@FullPath", Path.Combine(modItem.ModFolderPath, fileEntry.Value.RelativePath));
                                command.Parameters.AddWithValue("@RelativePath", fileEntry.Value.RelativePath);
                                command.Parameters.AddWithValue("@DTStamp", fileEntry.Value.Timestamp);
                                command.Parameters.AddWithValue("@HASH", fileEntry.Value.Hash);
                                command.Parameters.AddWithValue("@isArchive", 0); // Assuming these are not archive files

                                command.ExecuteNonQuery();
                            }
                        }

                        foreach (var archiveEntry in modItem.CurrentArchiveFiles)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(archiveEntry.Value.Path);
                            if (fileName == null)
                            {
                                continue;
                            }

                            string timestampString = fileName.Substring(fileName.LastIndexOf('_') + 1);
                            DateTime dtStamp;

                            if (!DateTime.TryParseExact(timestampString, Config.Instance.TimestampFormat, null, System.Globalization.DateTimeStyles.None, out dtStamp))
                            {
                                dtStamp = DateTime.Now; // Fallback to current time if parsing fails
                            }

                            query = @"
                                INSERT OR REPLACE INTO FileInfo (ModID, StageID, Filename, RelativePath, DTStamp, HASH, isArchive)
                                VALUES ((SELECT ModID FROM ModItems WHERE ModName = @ModName),
                                        (SELECT StageID FROM Stages WHERE StageName = @Stage),
                                        @FullPath, @RelativePath, @DTStamp, @HASH, @isArchive)";

                            using (var command = new SQLiteCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@ModName", modItem.ModName);
                                command.Parameters.AddWithValue("@Stage", archiveEntry.Value.Stage);
                                command.Parameters.AddWithValue("@FullPath", archiveEntry.Value.Path);
                                command.Parameters.AddWithValue("@RelativePath", DBNull.Value);
                                command.Parameters.AddWithValue("@DTStamp", dtStamp);
                                command.Parameters.AddWithValue("@HASH", DBNull.Value); // Assuming no hash for archives
                                command.Parameters.AddWithValue("@isArchive", 1); // These are archive files

                                command.ExecuteNonQuery();
                            }
                        }

                        // Insert or replace available stages
                        if (modItem.AvailableStages != null)
                        {
                            foreach (var stage in modItem.AvailableStages)
                            {
                                query = @"
                                    INSERT OR REPLACE INTO ModStages (ModID, StageID)
                                    VALUES ((SELECT ModID FROM ModItems WHERE ModName = @ModName), 
                                            (SELECT StageID FROM Stages WHERE StageName = @StageName))";
                                using (var command = new SQLiteCommand(query, connection))
                                {
                                    command.Parameters.AddWithValue("@ModName", modItem.ModName);
                                    command.Parameters.AddWithValue("@StageName", stage);
                                    command.ExecuteNonQuery();
                                }
                            }
                        }

                        // Insert or replace external IDs
                        if (!string.IsNullOrEmpty(modItem.NexusUrl) || !string.IsNullOrEmpty(modItem.BethesdaUrl))
                        {
                            query = @"
                                INSERT OR REPLACE INTO ExternalIDs (ModID, NexusID, BethesdaID)
                                VALUES ((SELECT ModID FROM ModItems WHERE ModName = @ModName), @NexusID, @BethesdaID)";
                            using (var command = new SQLiteCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@ModName", modItem.ModName);
                                command.Parameters.AddWithValue("@NexusID", string.IsNullOrEmpty(modItem.NexusUrl) ? (object)DBNull.Value : ExtractID(modItem.NexusUrl));
                                command.Parameters.AddWithValue("@BethesdaID", string.IsNullOrEmpty(modItem.BethesdaUrl) ? (object)DBNull.Value : ExtractID(modItem.BethesdaUrl));
                                command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                }
            }

            // Substitute Path.GetRelativePath with PathBuilder.GetRelativePath
            public static void LoadModFiles(ModItem modItem)
            {
                if (Directory.Exists(modItem.ModFolderPath))
                {
                    var files = Directory.GetFiles(modItem.ModFolderPath, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var relativePath = PathBuilder.GetRelativePath(modItem.ModFolderPath, file);
                        var timestamp = File.GetLastWriteTime(file);
                        var hash = ModItem.Files.ComputeHash(file);

                        modItem.ModFiles[relativePath] = (ModItem.DB.GetSourceStage(), relativePath, timestamp, hash);
                    }
                }
            }

            public static string ExtractID(string url)
            {
                var uri = new Uri(url);
                return uri.Segments.Last().TrimEnd('/');
            }

            public static void SaveToDatabase(ObservableCollection<ModItem> modItems)
            {
                foreach (var modItem in modItems)
                {
                    WriteMod(modItem);
                }
            }

            // Load ModItems from the database
            public static ObservableCollection<ModItem> LoadFromDatabase()
            {
                var modItems = new ObservableCollection<ModItem>();
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT * FROM vwModItems";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var modItem = new ModItem
                            {
                                ModName = reader.IsDBNull(reader.GetOrdinal("ModName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ModName")),
                                ModFolderPath = reader.IsDBNull(reader.GetOrdinal("ModFolderPath")) ? string.Empty : reader.GetString(reader.GetOrdinal("ModFolderPath")),
                                DeployedStage = reader.IsDBNull(reader.GetOrdinal("CurrentStage")) ? string.Empty : reader.GetString(reader.GetOrdinal("CurrentStage")),
                                NexusUrl = reader.IsDBNull(reader.GetOrdinal("NexusID")) ? string.Empty : $"https://www.nexusmods.com/starfield/mods/{reader.GetString(reader.GetOrdinal("NexusID"))}",
                                BethesdaUrl = reader.IsDBNull(reader.GetOrdinal("BethesdaID")) ? string.Empty : $"https://creations.bethesda.net/en/starfield/details/{reader.GetString(reader.GetOrdinal("BethesdaID"))}"
                            };

                            // Populate the CurrentArchiveFiles dictionary
                            var latestModArchives = GetLatestModArchives(modItem);
                            foreach (var archive in latestModArchives)
                            {
                                modItem.CurrentArchiveFiles[archive.Key] = archive.Value;
                            }

                            GetModFiles(modItem);
                            modItem.AvailableStages = GetAvailableStages(modItem.ModName);
                            modItems.Add(modItem);
                        }
                    }
                }

                return modItems;
            }

            private static List<string> GetAvailableStages(string modName)
            {
                var stages = new List<string>();
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT StageName FROM vwModStages WHERE ModName = @ModName";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ModName", modName);
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

            public static List<string> GetStages()
            {
                var stages = new List<string>();
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT StageName FROM Stages";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stages.Add(reader.GetString(0));
                        }
                    }
                }

                return stages;
            }

            public static string GetSourceStage()
            {
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT StageName FROM Stages WHERE isSource = 1";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        return command.ExecuteScalar() as string ?? string.Empty;
                    }
                }
            }

            public static List<string> GetDeployableStages()
            {
                var deployableStages = new List<string>();
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT StageName FROM Stages WHERE isSource = 0 AND isReserved = 0";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            deployableStages.Add(reader.GetString(0));
                        }
                    }
                }

                return deployableStages;
            }

            public static void UpdateModItemStage(string modName, string newStage)
            {
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = @"
                        UPDATE ModItems
                        SET CurrentStageID = (SELECT StageID FROM Stages WHERE StageName = @NewStage)
                        WHERE ModName = @ModName";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ModName", modName);
                        command.Parameters.AddWithValue("@NewStage", newStage);
                        command.ExecuteNonQuery();
                    }
                }
            }

            public static ModItem GetModItemByName(string modName)
            {
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT ModName, DeployedStage, NexusID, BethesdaID FROM vwModItems WHERE ModName = @ModName";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ModName", modName);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var modItem = new ModItem
                                {
                                    ModName = reader.IsDBNull(reader.GetOrdinal("ModName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ModName")),
                                    DeployedStage = reader.IsDBNull(reader.GetOrdinal("DeployedStage")) ? string.Empty : reader.GetString(reader.GetOrdinal("DeployedStage")),
                                    NexusUrl = reader.IsDBNull(reader.GetOrdinal("NexusID")) ? string.Empty : $"https://www.nexusmods.com/starfield/mods/{reader.GetString(reader.GetOrdinal("NexusID"))}",
                                    BethesdaUrl = reader.IsDBNull(reader.GetOrdinal("BethesdaID")) ? string.Empty : $"https://creations.bethesda.net/en/starfield/details/{reader.GetString(reader.GetOrdinal("BethesdaID"))}"
                                };

                                LoadModFiles(modItem);
                                return modItem;
                            }
                        }
                    }
                }

                return null;
            }

            public static int GetModID(ModItem modItem)
            {
                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT ModID FROM ModItems WHERE ModName = @ModName";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ModName", modItem.ModName);
                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }

            public static Dictionary<string, (string RelativePath, DateTime Timestamp, string Hash)> GetModFiles(ModItem modItem)
            {
                var modFiles = new Dictionary<string, (string RelativePath, DateTime Timestamp, string Hash)>();
                int modID = GetModID(modItem);

                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT RelativePath, DTStamp, HASH FROM FileInfo WHERE ModID = @ModID AND isArchive = 0";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ModID", modID);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var relativePath = reader.GetString(0);
                                var timestamp = reader.GetDateTime(1);
                                var hash = reader.GetString(2);

                                modFiles[relativePath] = (relativePath, timestamp, hash);
                            }
                        }
                    }
                }

                return modFiles;
            }

            public static Dictionary<string, (string Stage, string Path)> GetModArchives(ModItem modItem)
            {
                var modArchives = new Dictionary<string, (string Stage, string Path)>();
                int modID = GetModID(modItem);

                using (var connection = DbManager.Instance.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT StageName, FileName FROM vwModArchives WHERE ModId = @ModID";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ModID", modID);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var stage = reader.GetString(0);
                                var path = reader.GetString(1);

                                modArchives[stage] = (stage, path);
                            }
                        }
                    }
                }

                return modArchives;
            }

            public static Dictionary<string, (string Stage, string Path)> GetLatestModArchives(ModItem modItem)
            {
                var latestModArchives = new Dictionary<string, (string Stage, string Path)>();
                var modArchives = GetModArchives(modItem);

                foreach (var stage in modArchives.Keys)
                {
                    var latestArchive = modArchives
                        .Where(archive => archive.Key == stage)
                        .OrderByDescending(archive => archive.Value.Path)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(latestArchive.Value.Path))
                    {
                        latestModArchives[stage] = latestArchive.Value;
                    }
                }

                return latestModArchives;
            }
        }
    }
}
