using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DMM.Avalonia;

public partial class ManagedGameWindow : Window
{
    public ManagedGameWindow()
    {
        InitializeComponent();
        DataContext = new ManagedGame();
    }

    public ManagedGameWindow(ManagedGame game)
    {
        InitializeComponent();
        DataContext = game;
    }

    private void Save_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
