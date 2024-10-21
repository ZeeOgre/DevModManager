using SharpCompress.Common;
using SharpCompress.Writers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace ZO.DMM.AppNF
{
    public partial class ModItem
    {
        public partial class Files
        {
            private static readonly Config _config = Config.Instance;
            public static readonly string[] ArchiveFileTypes = { ".zip", ".7z" };

            public static void CreateArchiveFromDirectory(string sourceDirectory, string archivePath)
            {
                if (Directory.Exists(sourceDirectory))
                {
                    CreateArchiveWithRetry(sourceDirectory, archivePath);
                }
            }

            private static void CreateArchiveWithRetry(string sourceDirectory, string archivePath)
            {
                const int maxRetries = 3;
                int attempts = 0;
                while (attempts < maxRetries)
                {
                    try
                    {
                        if (_config.ArchiveFormat == "zip")
                        {
                            using (var zipArchive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                            {
                                AddDirectoryToArchive(zipArchive, sourceDirectory, sourceDirectory);
                            }
                        }
                        else if (_config.ArchiveFormat == "7z")
                        {
                            Create7zArchive(sourceDirectory, archivePath);
                        }
                        break;
                    }
                    catch (IOException ex) when (ex.Message.Contains("because it is being used by another process"))
                    {
                        attempts++;
                        if (attempts >= maxRetries)
                            throw;
                        Thread.Sleep(1000);
                    }
                }
            }

            private static void AddDirectoryToArchive(ZipArchive zipArchive, string sourceDirectory, string baseDirectory)
            {
                var files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                var addedFiles = new HashSet<string>();

                foreach (var file in files)
                {
                    var relativePath = PathBuilder.GetRelativePath(baseDirectory, file);
                    if (!addedFiles.Contains(relativePath))
                    {
                        zipArchive.CreateEntryFromFile(file, relativePath);
                        addedFiles.Add(relativePath);
                    }
                }
            }

            private static void Create7zArchive(string sourceDirectory, string archivePath)
            {
                using (var archiveStream = File.Create(archivePath))
                {
                    using (var writer = WriterFactory.Open(archiveStream, ArchiveType.SevenZip, CompressionType.LZMA))
                    {
                        var files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var relativePath = PathBuilder.GetRelativePath(sourceDirectory, file);
                            writer.Write(relativePath, file);
                        }
                    }
                }
            }

            public static void CreateJunctionPoint(string junctionPoint, string targetDir)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(junctionPoint));

                if (Directory.Exists(junctionPoint))
                {
                    Directory.Delete(junctionPoint, true);
                }

                var processInfo = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPoint}\" \"{targetDir}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(processInfo)?.WaitForExit();
            }

            public static void RemoveJunctionPoint(string path)
            {
                if (Directory.Exists(path) && IsJunctionPoint(path))
                {
                    Directory.Delete(path, true);
                }
            }

            private static bool IsJunctionPoint(string path)
            {
                var dirInfo = new DirectoryInfo(path);
                return dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }

            public static string GetNewestFile(string directory, string[] fileTypes)
            {
                var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                                     .Where(f => fileTypes == null || fileTypes.Any(ext => f.EndsWith(ext)))
                                     .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                     .FirstOrDefault();

                return files ?? string.Empty;
            }

            public static string CreateBackup(string sourcePath, string backupFolder)
            {
                string timestamp = DateTime.Now.ToString(_config.TimestampFormat);
                string archiveExtension = _config.ArchiveFormat == "7z" ? ".7z" : ".zip";
                string archivePath = Path.Combine(backupFolder, $"{Path.GetFileName(sourcePath)}_{timestamp}{archiveExtension}");

                // Ensure backupFolder exists
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                // Get the newest file in the backup folder
                var existingFiles = Directory.GetFiles(backupFolder);
                var latestBackupFile = existingFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).FirstOrDefault();
                DateTime latestBackupTime = latestBackupFile != null ? new FileInfo(latestBackupFile).LastWriteTime : DateTime.MinValue;

                // Get the newest file in the source folder
                var stagingFiles = Directory.GetFiles(sourcePath);
                var latestStagingFile = stagingFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).FirstOrDefault();
                DateTime latestStagingTime = latestStagingFile != null ? new FileInfo(latestStagingFile).LastWriteTime : DateTime.MinValue;

                // Compare the times
                if (latestStagingTime <= latestBackupTime)
                {
                    if (_config.ShowOverwriteMessage)
                    {
                        string latestBackupFileName = latestBackupFile != null ? Path.GetFileName(latestBackupFile) : "unknown";
                        MessageBoxResult result = MessageBox.Show($"{latestBackupFileName} is up to date. Do you still want to create a backup?", "Backup Not Needed", MessageBoxButton.YesNo, MessageBoxImage.Information);
                        if (result == MessageBoxResult.No)
                        {
                            return string.Empty; // No backup needed
                        }
                    }
                }

                // Create the archive file from the sourcePath without deleting the source files
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath); // Overwrite the existing file
                }
                ZipFile.CreateFromDirectory(sourcePath, archivePath);

                return archivePath;
            }

            public static string ComputeHash(string filePath)
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        var hashBytes = sha256.ComputeHash(stream);
                        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }
                }
            }

            public static List<string> GetUpdatedGameFolderFiles(ModItem modItem)
            {
                // ModItem.CheckDeploymentStatus(modItem); because of how this is called, we assume this is valid.
                string gameFolder = Config.Instance.GameFolder;
                string dataFolder = Path.Combine(gameFolder, "data");
                string deployFolder = PathBuilder.GetDeployBackupFolder(modItem.ModName);
                string latestFile = GetNewestFile(deployFolder, ArchiveFileTypes);
                DateTime referenceTimestamp = ParseFileTimestamp(latestFile);
                string[] files = Directory.GetFiles(dataFolder, "*.*", SearchOption.AllDirectories);

                List<string> excludedExtensions = new List<string> { ".dmp", ".json", ".log" };
                List<string> newerFiles = new List<string>();

                foreach (string file in files)
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime > referenceTimestamp && !excludedExtensions.Contains(fileInfo.Extension.ToLower()))
                    {
                        newerFiles.Add(file);
                    }
                }
                return newerFiles;
            }

            public static DateTime ParseFileTimestamp(string filePath)
            {
                // Extract the file name from the file path
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                // Find the last underscore in the file name
                int lastUnderscoreIndex = fileName.LastIndexOf('_');
                if (lastUnderscoreIndex == -1)
                {
                    throw new FormatException("The file name does not contain a timestamp.");
                }

                // Extract the timestamp part from the file name
                string timestampPart = fileName.Substring(lastUnderscoreIndex + 1);

                // Parse the timestamp using the configured timestamp format
                if (DateTime.TryParseExact(timestampPart, Config.Instance.TimestampFormat, null, System.Globalization.DateTimeStyles.None, out DateTime timestamp))
                {
                    return timestamp;
                }
                else
                {
                    MessageBox.Show($"Failed to parse the timestamp from the file name: {fileName}\r\nAttempting to return the file creation time", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return File.GetCreationTime(filePath);
                }
            }
        }
    }
}