using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DevModManager.Avalonia.Views;
using DevModManager.Core.ViewModels;

namespace DevModManager.Avalonia
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        // Opens a ModControlWindow positioned to the right of the main window.
        // This handler is safe to call from either the DataTemplate Button.Click or a ContextMenu MenuItem.Click.
        private void ModName_Popup_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                object? itemVm = null;

                // invoked from MenuItem inside ContextMenu
                if (sender is MenuItem mi && mi.DataContext != null)
                    itemVm = mi.DataContext;
                // invoked from Button inside DataTemplate
                else if (sender is Button btn && btn.DataContext != null)
                    itemVm = btn.DataContext;

                if (itemVm == null)
                    return;

                // Wrap the ModItemViewModel with ModControlViewModel for the window
                var modItem = itemVm as ModItemViewModel;
                var sub = new ModControlWindow
                {
                    DataContext = modItem is not null ? new ModControlViewModel(modItem) : itemVm
                };

                // Set size: 300px width, same height as main window
                sub.Width = 300;
                sub.Height = (int)this.Bounds.Height;

                // Position to the right of the main window (with small gap)
                var ownerPos = this.Position;
                sub.Position = new PixelPoint(ownerPos.X + (int)this.Bounds.Width + 8, ownerPos.Y);

                sub.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModName_Popup_Click error: {ex.Message}");
            }
        }
    }
}
