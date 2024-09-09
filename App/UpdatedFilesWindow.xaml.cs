using System.Collections.Generic;
using System.Windows;

namespace DevModManager.App
{
    public partial class UpdatedFilesWindow : Window
    {
        public UpdatedFilesWindow(ModItem modItem, List<string> updatedFiles)
        {
            InitializeComponent();
            FilesListBox.ItemsSource = updatedFiles;
        }
    }
}
