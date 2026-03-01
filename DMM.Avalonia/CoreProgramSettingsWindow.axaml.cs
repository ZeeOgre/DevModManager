using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DMM.Avalonia;

public partial class CoreProgramSettingsWindow : Window
{
    public CoreProgramSettingsWindow()
    {
        InitializeComponent();
    }

    private void ManageGameInstalls_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Close_Click(object? sender, RoutedEventArgs e) => Close(false);
}
