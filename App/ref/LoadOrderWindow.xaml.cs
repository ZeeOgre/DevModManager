using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using MessageBox = System.Windows.MessageBox;


namespace DevModManager.App
{
    public partial class LoadOrderWindow : Window
    {
        public ObservableCollection<ModGroup> Groups { get; set; }

        public LoadOrderWindow()
        {
            InitializeComponent();
            DataContext = this;
            Groups = new ObservableCollection<ModGroup>(); // Initialize Groups to avoid null reference
            LoadData();
            LoadOrderDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
        }

        private void LoadData()
        {
            var plugins = PluginManager.LoadPlugins();
            var groupedPlugins = plugins.GroupBy(p => p.GroupID).Select(g =>
            {
                var group = PluginManager.GetGroupById(Groups, g.Key) ?? new ModGroup { GroupID = g.Key };
                return new ModGroup
                {
                    GroupID = group.GroupID,
                    Description = group.Description,
                    Plugins = new ObservableCollection<Plugin>(g)
                };
            });

            Groups = new ObservableCollection<ModGroup>(groupedPlugins);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var hyperlink = (Hyperlink)sender;
            _ = (Plugin)hyperlink.DataContext;

            if (!string.IsNullOrEmpty(e.Uri.AbsoluteUri))
            {
                _ = Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }

            e.Handled = true;
        }

        private void UpdateHyperlink(TextBlock textBlock, string id, string type)
        {
            textBlock.Inlines.Clear();
            string url = string.Empty;

            if (type == "Bethesda")
            {
                var bethesdaUrlConverter = new BethesdaUrlConverter();
                url = bethesdaUrlConverter.Convert(id, typeof(string), null, CultureInfo.InvariantCulture) as string ?? string.Empty;
            }
            else if (type == "Nexus")
            {
                var nexusUrlConverter = new NexusUrlConverter();
                url = nexusUrlConverter.Convert(id, typeof(string), null, CultureInfo.InvariantCulture) as string ?? string.Empty;
            }

            var newHyperlink = new Hyperlink(new Run(id ?? "Unknown"))
            {
                NavigateUri = new Uri(url)
            };
            newHyperlink.RequestNavigate += Hyperlink_RequestNavigate;
            textBlock.Inlines.Add(newHyperlink);

            // Save the updated plugin data
            PluginManager.SavePluginsToJson(Groups.ToList(), Groups.SelectMany(g => g.Plugins).ToList());
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPlugin = LoadOrderDataGrid.SelectedItem as Plugin;
            if (selectedPlugin == null) return;

            var group = PluginManager.GetGroupById(Groups, selectedPlugin.GroupID);
            if (group == null) return;

            var index = group.Plugins.IndexOf(selectedPlugin);
            if (index > 0)
            {
                group.Plugins.Move(index, index - 1);
                RefreshDataGrid();
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPlugin = LoadOrderDataGrid.SelectedItem as Plugin;
            if (selectedPlugin == null) return;

            var group = PluginManager.GetGroupById(Groups, selectedPlugin.GroupID);
            if (group == null) return;

            var index = group.Plugins.IndexOf(selectedPlugin);
            if (index < group.Plugins.Count - 1)
            {
                group.Plugins.Move(index, index + 1);
                RefreshDataGrid();
            }
        }

        private void EditRowDataButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = LoadOrderDataGrid.SelectedItem;
            if (selectedItem is Plugin selectedPlugin)
            {
                var editorWindow = new PluginEditorWindow(selectedPlugin, new ObservableCollection<ModGroup>(Groups));
                if (editorWindow.ShowDialog() == true)
                {
                    PluginManager.SavePluginsToJson(Groups.ToList(), Groups.SelectMany(g => g.Plugins).ToList());
                    LoadOrderDataGrid.Items.Refresh();
                }
            }
            else if (selectedItem is ModGroup selectedGroup)
            {
                var editorWindow = new ModGroupEditorWindow(selectedGroup, Groups.SelectMany(g => g.Plugins).ToList());
                if (editorWindow.ShowDialog() == true)
                {
                    PluginManager.SavePluginsToJson(Groups.ToList(), Groups.SelectMany(g => g.Plugins).ToList());
                    LoadOrderDataGrid.Items.Refresh();
                }
            }
        }


        private void RefreshDataGrid()
        {
            LoadOrderDataGrid.Items.Refresh();
            PluginManager.SavePluginsToJson(Groups.ToList(), Groups.SelectMany(g => g.Plugins).ToList());
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save the updated plugin data
            PluginManager.SavePluginsToJson(Groups.ToList(), Groups.SelectMany(g => g.Plugins).ToList());
            _ = MessageBox.Show("Changes saved successfully.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPlugin = LoadOrderDataGrid.SelectedItem as Plugin;
            if (selectedPlugin == null) return;

            // Assuming you have a property or field named `Groups` that contains the list of ModGroup objects
            var editorWindow = new PluginEditorWindow(selectedPlugin, new ObservableCollection<ModGroup>(Groups));
            if (editorWindow.ShowDialog() == true)
            {
                // Save the updated plugin data
                PluginManager.SavePluginsToJson(Groups.ToList(), Groups.SelectMany(g => g.Plugins).ToList());
                LoadOrderDataGrid.Items.Refresh(); // Refresh the DataGrid view
            }
        }

        private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic to open the game folder
            _ = MessageBox.Show("Open Game Folder clicked.", "Open Game Folder", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenGameSettingsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic to open the game settings folder
            _ = MessageBox.Show("Open Game Settings Folder clicked.", "Open Game Settings Folder", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenGameSaveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic to open the game save folder
            _ = MessageBox.Show("Open Game Save Folder clicked.", "Open Game Save Folder", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditPluginsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic to edit plugins.txt
            _ = MessageBox.Show("Edit plugins.txt clicked.", "Edit Plugins", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditContentCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic to edit ContentCatalog.txt
            _ = MessageBox.Show("Edit ContentCatalog.txt clicked.", "Edit Content Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
