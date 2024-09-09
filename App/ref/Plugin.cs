using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DevModManager.App
{
    public class Plugin
    {
        public int ModID { get; set; }
        public bool ModEnabled { get; set; }
        public string PluginName { get; set; }
        public string Description { get; set; }
        public string Achievements { get; set; }
        public string Files { get; set; }
        public string TimeStamp { get; set; }
        public string Version { get; set; }
        public string BethesdaID { get; set; }
        public string NexusID { get; set; }
        public int GroupID { get; set; }
        public int GroupOrdinal { get; set; }
    }

    public class ModGroup
    {
        public int GroupID { get; set; }
        public int Ordinal { get; set; }
        public string Description { get; set; }
        public int ParentID { get; set; }
        public ObservableCollection<int> PluginIDs { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<Plugin> Plugins { get; set; } = new ObservableCollection<Plugin>();
    }

    public class LoadOutProfile
    {
        public string Name { get; set; }
        public int[] ActivePlugins { get; set; }
    }

    public static class PluginManager
    {
        public const string PluginsFilePath = @"%LOCALAPPDATA%\Starfield\plugins.txt";
        public const string ContentCatalogFilePath = @"%LOCALAPPDATA%\Starfield\ContentCatalog.txt";
        private static readonly string repoFolder = Config.Instance.RepoFolder;
        //private static readonly string metadataFilePath = Path.Combine(repoFolder, "METADATA", "plugin_meta.json");

        public static List<Plugin> LoadPlugins()
        {
            var plugins = new List<Plugin>();
            var groups = new List<ModGroup>();
            var loadOutProfiles = new List<LoadOutProfile>();

            // Expand environment variables in file paths
            string pluginsFilePath = Environment.ExpandEnvironmentVariables(PluginsFilePath);
            string contentCatalogFilePath = Environment.ExpandEnvironmentVariables(ContentCatalogFilePath);

            // Initialize metadata modgroup array with default values   
            groups.Add(new ModGroup
            {
                GroupID = 0,
                Ordinal = 0,
                Description = "Default",
                ParentID = -1
            });

            // Read plugins.txt
            var pluginLines = File.ReadAllLines(pluginsFilePath);

            var currentGroup = groups[0];
            var currentParent = groups[0];
            var currentDepth = 0;
            int modIDCounter = 1;

            foreach (var line in pluginLines)
            {
                if (line.StartsWith("###"))
                {
                    // Handle section lines
                    var depth = line.TakeWhile(c => c == '#').Count();
                    var description = line.Substring(depth).Trim();

                    if (depth > currentDepth)
                    {
                        // Create a new child group
                        var newGroup = new ModGroup
                        {
                            GroupID = groups.Count,
                            ParentID = currentGroup.GroupID,
                            Description = description,
                            Ordinal = currentGroup.Ordinal + 1
                        };
                        groups.Add(newGroup);
                        currentGroup = newGroup;
                    }
                    else if (depth == currentDepth)
                    {
                        // Create a new sibling group
                        var newGroup = new ModGroup
                        {
                            GroupID = groups.Count,
                            ParentID = currentParent.GroupID,
                            Description = description,
                            Ordinal = currentParent.Ordinal + 1
                        };
                        groups.Add(newGroup);
                        currentGroup = newGroup;
                    }
                    else
                    {
                        // Go back to the appropriate parent group
                        currentGroup = groups.First(g => g.GroupID == currentGroup.ParentID);
                        while (currentGroup.Ordinal >= depth)
                        {
                            currentGroup = groups.First(g => g.GroupID == currentGroup.ParentID);
                        }

                        // Create a new child group
                        var newGroup = new ModGroup
                        {
                            GroupID = groups.Count,
                            ParentID = currentGroup.GroupID,
                            Description = description,
                            Ordinal = currentGroup.Ordinal + 1
                        };
                        groups.Add(newGroup);
                        currentGroup = newGroup;
                    }

                    currentParent = groups.First(g => g.GroupID == currentGroup.ParentID);
                    currentDepth = depth;
                }
                else if (line.EndsWith(".esm") || line.EndsWith(".esp"))
                {
                    var plugin = new Plugin
                    {
                        ModID = modIDCounter++,
                        ModEnabled = line.StartsWith("*"),
                        PluginName = line.TrimStart('*').Trim(),
                        GroupID = currentGroup.GroupID,
                        GroupOrdinal = plugins.Count(p => p.GroupID == currentGroup.GroupID)
                    };
                    plugins.Add(plugin);
                    currentGroup.PluginIDs.Add(plugin.ModID);
                    currentGroup.Plugins.Add(plugin);
                }
            }

            // Read contentcatalog.txt
            var contentCatalogJson = File.ReadAllText(contentCatalogFilePath);
            var contentCatalog = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(contentCatalogJson);

            foreach (var plugin in plugins)
            {
                var entry = contentCatalog.Values.FirstOrDefault(v => v["Files"] != null && v["Files"].Any(f => f.ToString().Equals(plugin.PluginName, StringComparison.OrdinalIgnoreCase)));

                if (entry != null)
                {
                    plugin.Achievements = entry["AchievementSafe"]?.ToString();
                    plugin.Files = string.Join(", ", entry["Files"]);
                    plugin.Description = entry["Title"]?.ToString();
                    string version = entry["Version"]?.ToString();
                    if (!string.IsNullOrEmpty(version))
                    {
                        string[] versionParts = version.Split('.');
                        if (versionParts.Length > 1)
                        {
                            if (long.TryParse(versionParts[0]?.ToString(), out long unixTime))
                            {
                                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unixTime);
                                plugin.TimeStamp = dateTimeOffset.ToString(Config.Instance.TimestampFormat);
                            }
                            plugin.Version = versionParts[1];
                        }
                    }

                    // Extract BethesdaID from the key
                    var bethesdaID = contentCatalog.FirstOrDefault(x => x.Value == entry).Key;
                    plugin.BethesdaID = bethesdaID.StartsWith("TM_") ? bethesdaID.Substring(3) : bethesdaID;
                }
            }

            SavePluginsToJson(groups, plugins, loadOutProfiles);

            return plugins;
        }

        public static void SavePluginsToJson(List<ModGroup> groups, List<Plugin> plugins, List<LoadOutProfile> loadouts = null)
        {
            var jsonObject = new
            {
                Groups = groups.Select(g => new ModGroup
                {
                    GroupID = g.GroupID,
                    Ordinal = g.Ordinal,
                    Description = g.Description,
                    ParentID = g.ParentID,
                    PluginIDs = g.PluginIDs
                }).ToList(),
                LoadOuts = loadouts,
                Plugins = plugins
            };

            string repoFolder = Config.Instance.RepoFolder; // Use the Config singleton to get the RepoFolder path
            string metadataFilePath = Path.Combine(repoFolder, "METADATA", "plugin_meta.json");

            _ = Directory.CreateDirectory(Path.GetDirectoryName(metadataFilePath));
            File.WriteAllText(metadataFilePath, JsonConvert.SerializeObject(jsonObject, Formatting.Indented));
        }

        public static ModGroup GetGroupById(IEnumerable<ModGroup> groups, int groupId)
        {
            return groups.FirstOrDefault(g => g.GroupID == groupId);
        }

        public static Plugin GetPluginById(IEnumerable<Plugin> plugins, int modId)
        {
            return plugins.FirstOrDefault(p => p.ModID == modId);
        }
    }

}
