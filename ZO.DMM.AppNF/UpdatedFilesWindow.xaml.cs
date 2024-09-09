using System.Collections.Generic;
using System.Windows;

namespace ZO.DMM.AppNF
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
