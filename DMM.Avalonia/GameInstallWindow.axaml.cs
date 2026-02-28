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
    private ManagedGame? _lastMainGameSelection;

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

        install.ManagedGame = CanonicalizeManagedGame(install.ManagedGame);
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

        var resolved = ResolveManagedGameReference(selected);
        install.ManagedGame = resolved;
        _lastMainGameSelection = resolved;
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

    private void MainGameComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo)
        {
            return;
        }

        var selectedGame = combo.SelectedItem as ManagedGame
            ?? e.AddedItems?.OfType<ManagedGame>().FirstOrDefault();
        if (selectedGame is not null)
        {
            _lastMainGameSelection = selectedGame;
        }
    }


    private ManagedGame? CanonicalizeManagedGame(ManagedGame? candidate)
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


    private ManagedGame? CanonicalizeManagedGame(ManagedGame? candidate)
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


    private void MainGameComboBox_SelectionChanged_V2(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GameInstallRecord install || sender is not ComboBox combo)
        {
            return;
        }

        ApplySelectedMainGame_V2(install, combo);
    }

    private void ApplySelectedMainGame_V2(GameInstallRecord install, ComboBox combo)
    {
        var selectedGame = combo.SelectedItem as ManagedGame;
        if (selectedGame is null && combo.SelectedIndex >= 0 && combo.SelectedIndex < ManagedGames.Count)
        {
            selectedGame = ManagedGames[combo.SelectedIndex];
        }

        if (selectedGame is null && !string.IsNullOrWhiteSpace(combo.Text))
        {
            selectedGame = ManagedGames.FirstOrDefault(x =>
                string.Equals(x.Name, combo.Text, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedGame is not null)
        {
            install.ManagedGame = CanonicalizeManagedGame(selectedGame);
        }
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
            _lastMainGameSelection = game;
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameInstallRecord install)
        {
            ApplySelectedMainGame_V2(install, MainGameComboBox);
        }

        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}