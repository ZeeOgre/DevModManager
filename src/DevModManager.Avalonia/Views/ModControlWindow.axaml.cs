using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;

namespace DevModManager.Avalonia.Views
{
    public partial class ModControlWindow : Window
    {
        public ModControlWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        // Click handler for the Mod name button:
        // - If the VM exposes an `OpenStageFolderCommand` it will be executed.
        // - Otherwise falls back to `OpenFolderCommand` if available.
        private void ModNameButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var dc = this.DataContext;
                if (dc == null) return;

                var type = dc.GetType();

                // Try stage-specific command first
                var prop = type.GetProperty("OpenStageFolderCommand");
                if (prop?.GetValue(dc) is ICommand stageCmd && stageCmd.CanExecute(null))
                {
                    stageCmd.Execute(null);
                    return;
                }

                // Fallback to existing folder command
                prop = type.GetProperty("OpenFolderCommand");
                if (prop?.GetValue(dc) is ICommand folderCmd && folderCmd.CanExecute(null))
                {
                    folderCmd.Execute(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModNameButton_Click error: {ex.Message}");
            }
        }
    }
}