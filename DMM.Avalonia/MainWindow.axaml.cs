using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

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

    private void LoadOrder_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Load Order manager launch is currently a stub.";

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

        vm.Mods.Add(new ModListItem("ZO_AIOGamePlayTweaks", "ZO_AIOGamePlayTweaks.esp", "DEV", "f7dc7ac6...", "345221"));
        vm.Mods.Add(new ModListItem("ZO_DenserOutposts", "ZO_DenserOutposts.esm", "RELEASE", "c5bbdd20...", "817744"));
        vm.Mods.Add(new ModListItem("ZO_HandScannerTweaks", "ZO_HandScannerTweaks.esl", "TEST", "3f102ab3...", "593188"));
        vm.Mods.Add(new ModListItem("WT_SmartDoc", "WT_SmartDoc.esp", "DEV", "2120ee1a...", "772843"));
        vm.Mods.Add(new ModListItem("ZO_StarUIFix", "ZO_StarUIFix.esp", "DEV", "a220ce51...", "300194"));

        return vm;
    }
}

public sealed class ModListItem
{
    public ModListItem(string name, string primaryPlugin, string currentStage, string bethesdaId, string nexusId)
    {
        Name = name;
        PrimaryPlugin = primaryPlugin;
        CurrentStage = currentStage;
        BethesdaId = bethesdaId;
        NexusId = nexusId;
    }

    public string Name { get; }
    public string PrimaryPlugin { get; }
    public string CurrentStage { get; }
    public string BethesdaId { get; }
    public string NexusId { get; }
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
