using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DMM.Avalonia;

public partial class GameInstallWizardWindow : Window
{
    private readonly MainWindowViewModel _mainViewModel;
    private readonly GameInstallWizardViewModel _viewModel;

    public GameInstallWizardWindow()
    {
        InitializeComponent();
        _mainViewModel = MainWindowViewModel.CreateSample();
        _viewModel = new GameInstallWizardViewModel(_mainViewModel.DiscoverInstallCandidates(), _mainViewModel.ManagedGames, isFirstRun: false);
        DataContext = _viewModel;
    }

    public GameInstallWizardWindow(MainWindowViewModel mainViewModel, bool isFirstRun)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        _viewModel = new GameInstallWizardViewModel(Array.Empty<GameInstallRecord>(), _mainViewModel.ManagedGames, isFirstRun);
        DataContext = _viewModel;
        Opened += async (_, _) => await RunStoreScanAsync();
    }

    private void NextPage_Click(object? sender, RoutedEventArgs e) => _viewModel.NextPage();

    private void PreviousPage_Click(object? sender, RoutedEventArgs e) => _viewModel.PreviousPage();

    private async void ScanAgain_Click(object? sender, RoutedEventArgs e) => await RunStoreScanAsync();

    private async Task RunStoreScanAsync()
    {
        if (_viewModel.IsScanning)
        {
            return;
        }

        _viewModel.IsScanning = true;
        _viewModel.ScanStatus = "Preparing store scan...";

        try
        {
            var progress = new Progress<string>(status => _viewModel.ScanStatus = status);
            var discovered = await _mainViewModel.DiscoverInstallCandidatesAsync(progress);

            _viewModel.DiscoveredInstalls.Clear();
            foreach (var install in discovered)
            {
                _viewModel.DiscoveredInstalls.Add(install);
            }

            _viewModel.ScanStatus = discovered.Count == 0
                ? "No installs found. Add an unlisted game or scan again."
                : $"Scan complete. Found {discovered.Count} install(s).";
        }
        catch (Exception ex)
        {
            _viewModel.ScanStatus = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _viewModel.IsScanning = false;
            _viewModel.RefreshPaging();
        }
    }

    private async void AddUnlistedGame_Click(object? sender, RoutedEventArgs e)
    {
        var install = new GameInstallRecord { Manage = true, GameStore = "Custom" };
        var installWindow = new GameInstallWindow(install, _mainViewModel.ManagedGames, showNavigation: false, _mainViewModel.PersistManagedGame);
        var result = await installWindow.ShowDialog<bool>(this);
        if (!result)
        {
            return;
        }

        _viewModel.DiscoveredInstalls.Add(install);
        _viewModel.RefreshPaging();
    }

    private async void OpenInstall_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: GameInstallRecord install })
        {
            return;
        }

        var editable = install.Clone();
        var installWindow = new GameInstallWindow(editable, _mainViewModel.ManagedGames, showNavigation: true, _mainViewModel.PersistManagedGame);
        var result = await installWindow.ShowDialog<bool>(this);
        if (!result)
        {
            return;
        }

        install.Manage = editable.Manage;
        install.GameStore = editable.GameStore;
        install.ManagedGame = editable.ManagedGame;
        install.InstallPath = editable.InstallPath;
    }

    private void SaveSelection_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _viewModel.SelectedInstalls().Where(x => !string.IsNullOrWhiteSpace(x.InstallPath)).ToList();
        _mainViewModel.PersistSelectedInstalls(selected);

        Close();
    }
}
