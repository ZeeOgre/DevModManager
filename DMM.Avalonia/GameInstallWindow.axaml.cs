using System.Collections.ObjectModel;
using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using System.Linq;

namespace DMM.Avalonia;

public partial class GameInstallWindow : Window
{
    public ObservableCollection<ManagedGame> ManagedGames { get; }
    public bool ShowNavigation { get; }
    private readonly Action<ManagedGame>? _onManagedGameAdded;

    public GameInstallWindow()
    {
        ManagedGames = new ObservableCollection<ManagedGame>();
        ShowNavigation = false;
        _onManagedGameAdded = null;
        DataContext = new GameInstallRecord();
        InitializeComponent();
    }

    public GameInstallWindow(GameInstallRecord install, ObservableCollection<ManagedGame> managedGames, bool showNavigation, Action<ManagedGame>? onManagedGameAdded = null)    
    {
        ManagedGames = managedGames;
        ShowNavigation = showNavigation;
        _onManagedGameAdded = onManagedGameAdded;

        if (install.ManagedGame is not null)
        {
            install.ManagedGame = ResolveManagedGameReference(install.ManagedGame);
        }
        else if (!string.IsNullOrWhiteSpace(install.StoreAppId))
        {
            install.ManagedGame = ManagedGames.FirstOrDefault(x =>
                string.Equals(x.StoreId, install.StoreAppId, StringComparison.OrdinalIgnoreCase));
        }

        DataContext = install;
        InitializeComponent();
    }

    private ManagedGame? ResolveManagedGameReference(ManagedGame? candidate)
    {
        if (candidate is null)
        {
            return null;
        }

        return ManagedGames.FirstOrDefault(x =>
                   (!string.IsNullOrWhiteSpace(candidate.StoreId) &&
                    string.Equals(x.StoreId, candidate.StoreId, StringComparison.OrdinalIgnoreCase)) ||
                   string.Equals(x.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
               ?? candidate;
    }

    private void MainGame_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GameInstallRecord install || sender is not ComboBox combo)
        {
            return;
        }

        var selected = combo.SelectedItem as ManagedGame
            ?? e.AddedItems?.OfType<ManagedGame>().FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        install.ManagedGame = ResolveManagedGameReference(selected);
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
        if (DataContext is GameInstallRecord install && MainGameComboBox.SelectedItem is ManagedGame selected)
        {
            install.ManagedGame = ResolveManagedGameReference(selected);
        }

        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}