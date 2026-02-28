using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace DMM.Avalonia;

public partial class GameInstallWindow : Window
{
    public ObservableCollection<ManagedGame> ManagedGames { get; }
    public bool ShowNavigation { get; }
    private readonly Action<ManagedGame>? _onManagedGameAdded;

    public GameInstallWindow(GameInstallRecord install, ObservableCollection<ManagedGame> managedGames, bool showNavigation, Action<ManagedGame>? onManagedGameAdded = null)
    {
        InitializeComponent();
        ManagedGames = managedGames;
        ShowNavigation = showNavigation;
        _onManagedGameAdded = onManagedGameAdded;
        DataContext = install;
    }

    private async void BrowseFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GameInstallRecord install)
        {
            return;
        }

        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select game install folder",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            install.InstallPath = result[0].Path.LocalPath;
        }
    }

    private async void AddManagedGame_Click(object? sender, RoutedEventArgs e)
    {
        var game = new ManagedGame();
        var gameWindow = new ManagedGameWindow(game);
        var result = await gameWindow.ShowDialog<bool>(this);
        if (!result)
        {
            return;
        }

        ManagedGames.Add(game);
        _onManagedGameAdded?.Invoke(game);

        if (DataContext is GameInstallRecord install)
        {
            install.ManagedGame = game;
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameInstallRecord install)
        {
            var selectedGame = MainGameComboBox.SelectedItem as ManagedGame;
            if (selectedGame is null && MainGameComboBox.SelectedIndex >= 0 && MainGameComboBox.SelectedIndex < ManagedGames.Count)
            {
                selectedGame = ManagedGames[MainGameComboBox.SelectedIndex];
            }

            if (selectedGame is null && !string.IsNullOrWhiteSpace(MainGameComboBox.Text))
            {
                selectedGame = ManagedGames.FirstOrDefault(x =>
                    string.Equals(x.Name, MainGameComboBox.Text, StringComparison.OrdinalIgnoreCase));
            }

            if (selectedGame is not null)
            {
                install.ManagedGame = selectedGame;
            }
        }

        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
