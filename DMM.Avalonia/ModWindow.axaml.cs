using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DMM.Avalonia;

public partial class ModWindow : Window
{
    private readonly ModWindowViewModel _viewModel;
    private readonly Func<ModDependencyGatherRequest, Task<string>>? _singleModGatherDependenciesAsync;

    public ModWindow()
    {
        InitializeComponent();
        var placeholderMod = new ModListItem("Mod", "", "DEV", "", "", "", new SolidColorBrush(Colors.Transparent));
        _viewModel = new ModWindowViewModel(placeholderMod, Array.Empty<string>(), Array.Empty<string>(), null);
        _singleModGatherDependenciesAsync = null;
        DataContext = _viewModel;
        BuildStageFolderContextMenu();
    }

    public ModWindow(ModListItem mod, IReadOnlyList<string> gameFolders, IReadOnlyList<string> stages, string? selectedGameFolder, Func<ModDependencyGatherRequest, Task<string>>? singleModGatherDependenciesAsync = null)
    {
        InitializeComponent();
        _viewModel = new ModWindowViewModel(mod, gameFolders, stages, selectedGameFolder);
        _singleModGatherDependenciesAsync = singleModGatherDependenciesAsync;
        DataContext = _viewModel;
        BuildStageFolderContextMenu();
    }

    private void BuildStageFolderContextMenu()
    {
        var menu = new ContextMenu();
        foreach (var stage in _viewModel.StageOptions)
        {
            var item = new MenuItem
            {
                Header = stage,
                CommandParameter = $"Open Stage Folder: {stage}"
            };
            item.Click += ContextAction_Click;
            menu.Items.Add(item);
        }

        StageFolderButton.ContextMenu = menu;
    }

    private async void Action_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string action })
        {
            if (string.Equals(action, "Gather Dependencies", StringComparison.OrdinalIgnoreCase))
            {
                await RunSingleModGatherDependenciesAsync(action);
                return;
            }

            _viewModel.StatusMessage =
                $"{action} requested for {_viewModel.ModName} ({_viewModel.SelectedStage} @ {_viewModel.SelectedGameFolder}). Right-click for options.";
        }
    }

    private async void ContextAction_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: string action })
        {
            if (action == "Make BA2: Add Files Manually")
            {
                _viewModel.StatusMessage =
                    $"{action} selected for {_viewModel.ModName}. UI wiring is pending; this will become the manual archive-file picker.";
                return;
            }

            if (string.Equals(action, "Gather Dependencies", StringComparison.OrdinalIgnoreCase))
            {
                await RunSingleModGatherDependenciesAsync(action);
                return;
            }

            _viewModel.StatusMessage =
                $"{action} requested for {_viewModel.ModName} ({_viewModel.SelectedStage} @ {_viewModel.SelectedGameFolder}).";
        }
    }

    private async Task RunSingleModGatherDependenciesAsync(string action)
    {
        if (_singleModGatherDependenciesAsync is null)
        {
            _viewModel.StatusMessage =
                $"{action} not wired for this window instance. Open this mod from MainWindow to run scan/apply.";
            return;
        }

        _viewModel.StatusMessage =
            $"{action} started for {_viewModel.ModName} ({_viewModel.SelectedStage} @ {_viewModel.SelectedGameFolder}).";

        var result = await _singleModGatherDependenciesAsync(new ModDependencyGatherRequest(
            _viewModel.ModName,
            _viewModel.PrimaryPlugin,
            _viewModel.SelectedGameFolder,
            _viewModel.SelectedStage ?? "DEV"));

        _viewModel.StatusMessage = result;
    }

    private async void OpenHelp_Click(object? sender, RoutedEventArgs e)
    {
        var helpWindow = HelpWindow.ForSection("ModFocus");
        await helpWindow.ShowDialog(this);
        _viewModel.StatusMessage = "Help viewed (ModFocus section).";
    }

    private void GitUp_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Git control: push/up requested for current mod.";

    private void GitSync_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Git control: sync requested for current mod.";

    private void GitDown_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Git control: pull/down requested for current mod.";

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}

public sealed class ModWindowViewModel : NotifyBase
{
    public ModWindowViewModel(ModListItem mod, IReadOnlyList<string> gameFolders, IReadOnlyList<string> stages, string? selectedGameFolder)
    {
        ModName = mod.Name;
        PrimaryPlugin = mod.PrimaryPlugin;
        PluginInfo = $"Primary plugin: {mod.PrimaryPlugin}";

        foreach (var folder in gameFolders)
        {
            GameFolders.Add(folder);
        }

        foreach (var stage in stages)
        {
            StageOptions.Add(stage);
        }

        SelectedGameFolder = !string.IsNullOrWhiteSpace(selectedGameFolder) && GameFolders.Contains(selectedGameFolder)
            ? selectedGameFolder
            : GameFolders.Count > 0 ? GameFolders[0] : "Primary Game Folder";
        SelectedStage = StageOptions.Contains(mod.CurrentStage) ? mod.CurrentStage : "DEV";
        StatusMessage = "Select an action. Right-click buttons for detailed options.";
    }

    public string ModName { get; }
    public string PrimaryPlugin { get; }
    public string PluginInfo { get; }
    public ObservableCollection<string> GameFolders { get; } = new();
    public ObservableCollection<string> StageOptions { get; } = new();

    private string? _selectedGameFolder;
    public string? SelectedGameFolder
    {
        get => _selectedGameFolder;
        set => SetField(ref _selectedGameFolder, value);
    }

    private string? _selectedStage;
    public string? SelectedStage
    {
        get => _selectedStage;
        set => SetField(ref _selectedStage, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }
}

public sealed record ModDependencyGatherRequest(string ModName, string PrimaryPlugin, string? GameFolder, string Stage);
