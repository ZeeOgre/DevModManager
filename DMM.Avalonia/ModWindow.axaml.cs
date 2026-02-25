using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DMM.Avalonia;

public partial class ModWindow : Window
{
    private readonly ModWindowViewModel _viewModel;

    public ModWindow(ModListItem mod, ObservableCollection<string> gameFolders, ObservableCollection<string> stages)
    {
        InitializeComponent();
        _viewModel = new ModWindowViewModel(mod, gameFolders, stages);
        DataContext = _viewModel;
    }

    private void Action_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string action })
        {
            _viewModel.StatusMessage = $"{action} requested for {_viewModel.ModName} ({_viewModel.SelectedStage} @ {_viewModel.SelectedGameFolder}).";
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}

public sealed class ModWindowViewModel : NotifyBase
{
    public ModWindowViewModel(ModListItem mod, ObservableCollection<string> gameFolders, ObservableCollection<string> stages)
    {
        ModName = mod.Name;
        PluginInfo = $"Primary plugin: {mod.PrimaryPlugin}";

        foreach (var folder in gameFolders)
        {
            GameFolders.Add(folder);
        }

        foreach (var stage in stages)
        {
            StageOptions.Add(stage);
        }

        SelectedGameFolder = GameFolders.Count > 0 ? GameFolders[0] : "Primary Game Folder";
        SelectedStage = StageOptions.Contains(mod.CurrentStage) ? mod.CurrentStage : "DEV";
        StatusMessage = "Select an action to run a workflow stub.";
    }

    public string ModName { get; }
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
