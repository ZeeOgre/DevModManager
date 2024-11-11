using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ZO.DMM.AppNF
{
    public partial class UpdatedFilesWindow : Window
    {
        private readonly ModItem _modItem;
        private readonly List<FileItem> _fileItems;

        public UpdatedFilesWindow(ModItem modItem)
        {
            InitializeComponent();
            _modItem = modItem;
            List<string> updatedFiles = ModItem.Files.GetUpdatedGameFolderFiles(modItem);
            // Pre-select files and sort
            _fileItems = updatedFiles
                .Select(file => new FileItem
                {
                    FileName = PathBuilder.GetRelativePath(Path.Combine(Config.Instance.GameFolder, "data"), file),
                    FullPath = file, // Set the full path
                    IsSelected = ShouldBeSelected(file)
                })
                .OrderByDescending(item => item.IsSelected)
                .ThenBy(item => item.FileName)
                .ToList();

            PopulateFilesGrid();
        }

        private void PopulateFilesGrid()
        {
            FilesGrid.Children.Clear();

            foreach (var fileItem in _fileItems)
            {
                var checkBox = new CheckBox
                {
                    Content = fileItem.FileName,
                    IsChecked = fileItem.IsSelected,
                    Margin = new Thickness(5)
                };

                // Bind the checkbox state to the IsSelected property
                checkBox.Checked += (s, e) => fileItem.IsSelected = true;
                checkBox.Unchecked += (s, e) => fileItem.IsSelected = false;

                _ = FilesGrid.Children.Add(checkBox);
            }
        }

        private bool ShouldBeSelected(string file)
        {
            var config = Config.Instance;
            return file.Contains(_modItem.ModName) ||
                   file.StartsWith(config.MyResourcePrefix) ||
                   file.Contains(config.MyNameSpace);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = _fileItems.Where(item => item.IsSelected).Select(item => item.FullPath).ToList();
            CopyFilesToSourceFolder(selectedFiles);
            _ = MessageBox.Show("Selected files have been copied to the mod's source folder.", "Operation Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void CopyFilesToSourceFolder(List<string> files)
        {
            var dataFolder = Path.Combine(Config.Instance.GameFolder, "data");
            var modFolderPath = _modItem.ModFolderPath;

            foreach (var file in files)
            {
                var sourcePath = file; // The file already contains the full path
                var relativePath = PathBuilder.GetRelativePath(dataFolder, file);
                var destinationPath = Path.Combine(modFolderPath, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationPath);

                if (!Directory.Exists(destinationDir))
                {
                    _ = Directory.CreateDirectory(destinationDir);
                }

                try
                {
                    var sourceFileInfo = new FileInfo(sourcePath);
                    var destinationFileInfo = new FileInfo(destinationPath);

                    // Check if the destination file exists and compare properties
                    if (destinationFileInfo.Exists)
                    {
                        if (sourceFileInfo.Length == destinationFileInfo.Length &&
                            sourceFileInfo.LastWriteTime == destinationFileInfo.LastWriteTime)
                        {
                            // Skip copying if the files are the same
                            continue;
                        }
                    }

                    File.Copy(sourcePath, destinationPath, true);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Error copying file {sourcePath}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
        public class FileItem
    {
        public bool IsSelected { get; set; }
        public string FileName { get; set; }
        public string FullPath { get; set; } // Add this property
    }
}
