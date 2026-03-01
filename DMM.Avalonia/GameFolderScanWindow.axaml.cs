using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DMM.Avalonia;

public partial class GameFolderScanWindow : Window
{
    private readonly GameFolderScanWindowViewModel _viewModel;

    public GameFolderScanWindow()
    {
        InitializeComponent();
        _viewModel = new GameFolderScanWindowViewModel(Array.Empty<GameFolderScanCandidate>(), Array.Empty<string>());
        DataContext = _viewModel;
    }

    public GameFolderScanWindow(IReadOnlyList<GameFolderScanCandidate> candidates, IReadOnlyList<string> stageOptions)
    {
        InitializeComponent();
        _viewModel = new GameFolderScanWindowViewModel(candidates, stageOptions);
        DataContext = _viewModel;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _viewModel.SelectedStages();
        Close(new GameFolderScanApplyResult(selected));
    }
}

public sealed class GameFolderScanWindowViewModel : NotifyBase
{
    public const string IgnoreOption = "Ignore";

    public GameFolderScanWindowViewModel(IReadOnlyList<GameFolderScanCandidate> candidates, IReadOnlyList<string> stageOptions)
    {
        var mergedStageOptions = stageOptions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var candidate in candidates)
        {
            Candidates.Add(new GameFolderScanCandidateSelection(candidate, mergedStageOptions));
        }

        foreach (var item in Candidates)
        {
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(GameFolderScanCandidateSelection.SelectedStage))
                {
                    OnPropertyChanged(nameof(SummaryText));
                }
            };
        }
    }

    public ObservableCollection<GameFolderScanCandidateSelection> Candidates { get; } = new();

    public string SummaryText
    {
        get
        {
            var selected = Candidates.Count(x => !string.Equals(x.SelectedStage, IgnoreOption, StringComparison.OrdinalIgnoreCase));
            return selected == 0
                ? "No stage actions selected yet."
                : $"{selected} mod(s) will be added to the selected stage queues.";
        }
    }

    public IReadOnlyList<GameFolderStageSelection> SelectedStages() => Candidates
        .Where(x => !string.Equals(x.SelectedStage, IgnoreOption, StringComparison.OrdinalIgnoreCase))
        .Select(x => new GameFolderStageSelection(x.ModName, x.PluginName, x.SelectedStage))
        .ToList();
}

public sealed class GameFolderScanCandidateSelection : NotifyBase
{
    public GameFolderScanCandidateSelection(GameFolderScanCandidate candidate, IReadOnlyList<string> stageOptions)
    {
        ModName = candidate.ModName;
        PluginName = candidate.PluginName;

        StageChoices.Add(GameFolderScanWindowViewModel.IgnoreOption);
        foreach (var stage in stageOptions)
        {
            StageChoices.Add(stage);
        }

        _selectedStage = GameFolderScanWindowViewModel.IgnoreOption;
    }

    public string ModName { get; }
    public string PluginName { get; }
    public ObservableCollection<string> StageChoices { get; } = new();

    private string _selectedStage;
    public string SelectedStage
    {
        get => _selectedStage;
        set => SetField(ref _selectedStage, value);
    }
}

public sealed record GameFolderScanCandidate(string ModName, string PluginName);
public sealed record GameFolderStageSelection(string ModName, string PluginName, string Stage);
public sealed record GameFolderScanApplyResult(IReadOnlyList<GameFolderStageSelection> SelectedMods);
public sealed record GameFolderScanResult(bool Success, IReadOnlyList<GameFolderScanCandidate> DiscoveredCandidates)
{
    public static GameFolderScanResult Failed() => new(false, Array.Empty<GameFolderScanCandidate>());

    public static GameFolderScanResult Succeeded(IReadOnlyList<GameFolderScanCandidate> discoveredCandidates)
        => new(true, discoveredCandidates);
}
