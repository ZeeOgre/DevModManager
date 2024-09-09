using System.Collections.ObjectModel;
using System.Windows;

namespace DevModManager.App
{
    public partial class PluginEditorWindow : Window
    {
        public Plugin Plugin { get; set; }
        public ObservableCollection<ModGroup> Groups { get; set; }

        public PluginEditorWindow(Plugin plugin, ObservableCollection<ModGroup> groups)
        {
            InitializeComponent();
            Plugin = plugin;
            Groups = groups;
            DataContext = this;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
