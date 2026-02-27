using System.Linq;
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
        _viewModel = new GameInstallWizardViewModel(_mainViewModel.DiscoverInstallCandidates(), _mainViewModel.ManagedGames, isFirstRun);
        DataContext = _viewModel;
    }

    private void NextPage_Click(object? sender, RoutedEventArgs e) => _viewModel.NextPage();

    private void PreviousPage_Click(object? sender, RoutedEventArgs e) => _viewModel.PreviousPage();

    private void ScanAgain_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.DiscoveredInstalls.Clear();
        foreach (var install in _mainViewModel.DiscoverInstallCandidates())
        {
            _viewModel.DiscoveredInstalls.Add(install);
        }

        _viewModel.RefreshPaging();
    }

    private async void AddUnlistedGame_Click(object? sender, RoutedEventArgs e)
    {
        var install = new GameInstallRecord { Manage = true, GameStore = "Custom" };
        var installWindow = new GameInstallWindow(install, _mainViewModel.ManagedGames, showNavigation: false);
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
        var installWindow = new GameInstallWindow(editable, _mainViewModel.ManagedGames, showNavigation: true);
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
        _mainViewModel.GameInstalls.Clear();
        foreach (var install in _viewModel.SelectedInstalls().Where(x => !string.IsNullOrWhiteSpace(x.InstallPath)))
        {
            _mainViewModel.GameInstalls.Add(install.Clone());
        }

        Close();
    }
}
