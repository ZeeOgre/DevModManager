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
    public ObservableCollection<string> GameStoreOptions { get; }
    public bool ShowNavigation { get; }
    private readonly Action<ManagedGame>? _onManagedGameAdded;

    public GameInstallWindow()
    {
        ManagedGames = new ObservableCollection<ManagedGame>();
        GameStoreOptions = BuildGameStoreOptions();
        ShowNavigation = false;
        _onManagedGameAdded = null;
        var install = new GameInstallRecord();
        EnsureStoreOptionExists(install.GameStore);
        DataContext = install;
        InitializeComponent();
        SelectGameStoreValue(install.GameStore);
    }

    public GameInstallWindow(GameInstallRecord install, ObservableCollection<ManagedGame> managedGames, bool showNavigation, Action<ManagedGame>? onManagedGameAdded = null)
    {
        ManagedGames = managedGames;
        GameStoreOptions = BuildGameStoreOptions();
        ShowNavigation = showNavigation;
        _onManagedGameAdded = onManagedGameAdded;

        install.ManagedGame = CanonicalizeManagedGame(install.ManagedGame);
        EnsureStoreOptionExists(install.GameStore);
        DataContext = install;
        InitializeComponent();
        SelectGameStoreValue(install.GameStore);
    }

    private static ObservableCollection<string> BuildGameStoreOptions()
    {
        return new ObservableCollection<string>(new[]
        {
            "Steam",
            "Game Pass",
            "Epic",
            "GOG",
            "EA",
            "Origin",
            "Battle.net",
            "Rockstar",
            "Minecraft",
            "PSN",
            "Custom"
        });
    }

    private void EnsureStoreOptionExists(string? gameStore)
    {
        if (string.IsNullOrWhiteSpace(gameStore))
        {
            return;
        }

        if (!GameStoreOptions.Any(x => string.Equals(x, gameStore, StringComparison.OrdinalIgnoreCase)))
        {
            GameStoreOptions.Add(gameStore);
        }
    }

    private void SelectGameStoreValue(string? gameStore)
    {
        if (string.IsNullOrWhiteSpace(gameStore))
        {
            return;
        }

        var matched = GameStoreOptions.FirstOrDefault(x =>
            string.Equals(x, gameStore, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matched))
        {
            GameStoreComboBox.SelectedItem = matched;
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

    private void MainGameComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GameInstallRecord install || sender is not ComboBox combo)
        {
            return;
        }

        var selectedGame = combo.SelectedItem as ManagedGame
            ?? e.AddedItems?.OfType<ManagedGame>().FirstOrDefault();

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
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameInstallRecord install)
        {
            var selectedStore = GameStoreComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedStore) && !string.IsNullOrWhiteSpace(GameStoreComboBox.Text))
            {
                selectedStore = GameStoreOptions.FirstOrDefault(x =>
                    string.Equals(x, GameStoreComboBox.Text, StringComparison.OrdinalIgnoreCase))
                    ?? GameStoreComboBox.Text.Trim();
            }

            if (string.IsNullOrWhiteSpace(selectedStore))
            {
                selectedStore = install.GameStore;
            }

            install.GameStore = string.IsNullOrWhiteSpace(selectedStore)
                ? "Custom"
                : selectedStore.Trim();

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
                install.ManagedGame = CanonicalizeManagedGame(selectedGame);
            }
        }

        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
