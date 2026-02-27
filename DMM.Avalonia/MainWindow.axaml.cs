using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DMM.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = MainWindowViewModel.CreateSample();
        DataContext = _viewModel;
    }

    private void ScanGameFolder_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage =
            "Scan requested: discovering mods from game folder and inferring default branch initialization rules.";

    private async void OpenHelp_Click(object? sender, RoutedEventArgs e)
    {
        var helpWindow = HelpWindow.ForSection("Main");
        await helpWindow.ShowDialog(this);
        _viewModel.StatusMessage = "Help viewed.";
    }

    private void OpenSettings_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Settings window launch is currently a stub.";

    private void OpenBackups_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string modName })
        {
            _viewModel.StatusMessage = $"Open backup archive requested for {modName}.";
        }
    }

    private void OpenBethesda_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string modName })
        {
            _viewModel.StatusMessage = $"Open Bethesda link requested for {modName}.";
        }
    }

    private void OpenNexus_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string modName })
        {
            _viewModel.StatusMessage = $"Open Nexus link requested for {modName}.";
        }
    }

    private void DeployToGameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string modName })
        {
            _viewModel.StatusMessage = $"Deploy requested for {modName} to {_viewModel.SelectedGameFolder}.";
        }
    }

    private void OpenGameFolder_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = $"Open game folder requested: {_viewModel.SelectedGameFolder}.";

    private void LaunchCreationKit_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage =
            $"Launch Creation Kit requested for {_viewModel.SelectedGameFolder}; if missing, prompt install.";

    private void LaunchXEdit_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Launch xEdit requested from central tools folder.";

    private void LaunchNifSkope_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Launch NifSkope requested from central tools folder.";

    private void LaunchAssetWatcher_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage =
            $"Launch AssetWatcher requested from per-game tool folder for {_viewModel.SelectedGameFolder}.";

    private void LaunchIde_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Launch preferred IDE requested (typically VS Code).";

    private void OpenLoadOrderManager_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Open Load Order manager requested.";


    private void GitUp_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Git control: push/up requested.";

    private void GitSync_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Git control: sync requested.";

    private void GitDown_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Git control: pull/down requested.";

    private async void OpenModWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ModListItem mod })
        {
            var modWindow = new ModWindow(mod, _viewModel.GameFolders, _viewModel.StageOptions);
            await modWindow.ShowDialog(this);
            _viewModel.StatusMessage = $"Closed focus window for {mod.Name}.";
        }
    }
}

public sealed class MainWindowViewModel : NotifyBase
{
    public ObservableCollection<string> GameFolders { get; } = new();
    public ObservableCollection<string> StageOptions { get; } = new();
    public ObservableCollection<ModListItem> Mods { get; } = new();

    private string? _selectedGameFolder;
    public string? SelectedGameFolder
    {
        get => _selectedGameFolder;
        set => SetField(ref _selectedGameFolder, value);
    }

    private string _statusMessage = "Ready. Choose a mod and open Focus for per-mod operations.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public static MainWindowViewModel CreateSample()
    {
        var vm = new MainWindowViewModel();

        vm.GameFolders.Add("Primary Game Folder");
        vm.GameFolders.Add("Alternate Test Folder");
        vm.GameFolders.Add("XBOX Sandbox Folder");
        vm.SelectedGameFolder = vm.GameFolders[0];

        vm.StageOptions.Add("DEV");
        vm.StageOptions.Add("TEST");
        vm.StageOptions.Add("PREFLIGHT");
        vm.StageOptions.Add("PRERELEASE");
        vm.StageOptions.Add("RELEASE");

        vm.Mods.Add(new ModListItem("ZO_AIOGamePlayTweaks", "ZO_AIOGamePlayTweaks.esp", "DEV", "f7dc7ac6...", "345221", new SolidColorBrush(Color.Parse("#2B2B2B"))));
        vm.Mods.Add(new ModListItem("ZO_DenserOutposts", "ZO_DenserOutposts.esm", "RELEASE", "c5bbdd20...", "817744", new SolidColorBrush(Color.Parse("#343434"))));
        vm.Mods.Add(new ModListItem("ZO_HandScannerTweaks", "ZO_HandScannerTweaks.esl", "TEST", "3f102ab3...", "593188", new SolidColorBrush(Color.Parse("#2B2B2B"))));
        vm.Mods.Add(new ModListItem("WT_SmartDoc", "WT_SmartDoc.esp", "DEV", "2120ee1a...", "772843", new SolidColorBrush(Color.Parse("#343434"))));
        vm.Mods.Add(new ModListItem("ZO_StarUIFix", "ZO_StarUIFix.esp", "DEV", "a220ce51...", "300194", new SolidColorBrush(Color.Parse("#2B2B2B"))));

        return vm;
    }
}

public sealed class ModListItem
{
    public ModListItem(string name, string primaryPlugin, string currentStage, string bethesdaId, string nexusId, IBrush rowBackground)
    {
        Name = name;
        PrimaryPlugin = primaryPlugin;
        CurrentStage = currentStage;
        BethesdaId = bethesdaId;
        NexusId = nexusId;
        RowBackground = rowBackground;
    }

    public string Name { get; }
    public string PrimaryPlugin { get; }
    public string CurrentStage { get; }
    public string BethesdaId { get; }
    public string NexusId { get; }
    public IBrush RowBackground { get; }
}

public abstract class NotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
