using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DevModManager.App
{
    public partial class ModGroupEditorWindow : Window
    {
        private ModGroup _modGroup;
        private List<Plugin> _allPlugins;

        public ModGroupEditorWindow(ModGroup modGroup, List<Plugin> allPlugins)
        {
            InitializeComponent();
            _modGroup = modGroup;
            _allPlugins = allPlugins;

            // Bind data to UI elements
            DescriptionTextBox.Text = _modGroup.Description;
            PluginIDsTextBox.Text = string.Join(", ", _modGroup.PluginIDs);
            UpdatePluginsListBox();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update ModGroup properties
            _modGroup.Description = DescriptionTextBox.Text;

            // Parse PluginIDs from the TextBox
            var pluginIDs = PluginIDsTextBox.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(id => int.Parse(id.Trim()))
                                                  .ToList();

            // Update PluginIDs and Plugins collections
            _modGroup.PluginIDs.Clear();
            _modGroup.Plugins.Clear();
            foreach (var id in pluginIDs)
            {
                _modGroup.PluginIDs.Add(id);
                var plugin = _allPlugins.FirstOrDefault(p => p.ModID == id);
                if (plugin != null)
                {
                    _modGroup.Plugins.Add(plugin);
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PluginIDsTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Parse PluginIDs from the TextBox
            var pluginIDs = PluginIDsTextBox.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(id => int.Parse(id.Trim()))
                                                  .ToList();

            // Update Plugins collection
            _modGroup.Plugins.Clear();
            foreach (var id in pluginIDs)
            {
                var plugin = _allPlugins.FirstOrDefault(p => p.ModID == id);
                if (plugin != null)
                {
                    _modGroup.Plugins.Add(plugin);
                }
            }

            // Update the display of PluginIDsTextBox
            PluginIDsTextBox.Text = string.Join(", ", _modGroup.Plugins.Select(p => p.ModID));

            // Update the PluginsListBox
            UpdatePluginsListBox();
        }

        private void UpdatePluginsListBox()
        {
            PluginsListBox.ItemsSource = _modGroup.Plugins.Select(p => p.PluginName).ToList();
        }
    }
}
