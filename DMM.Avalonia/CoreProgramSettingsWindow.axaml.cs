using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace DMM.Avalonia;

public partial class CoreProgramSettingsWindow : Window
{
    private readonly ProgramWideSettingsStore _settingsStore = new();
    private readonly CoreProgramSettingsViewModel _viewModel;

    public CoreProgramSettingsWindow()
    {
        InitializeComponent();
        _viewModel = new CoreProgramSettingsViewModel(_settingsStore.Load());
        DataContext = _viewModel;
    }

    private async void BrowseRepoRoot_Click(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select mod repository root",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            _viewModel.RepoRootPath = result[0].Path.LocalPath;
        }
    }

    private void ManageGameInstalls_Click(object? sender, RoutedEventArgs e)
    {
        SaveCurrentSettings();
        Close(true);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        SaveCurrentSettings();
        Close(false);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        SaveCurrentSettings();
        Close(false);
    }

    private void SaveCurrentSettings()
    {
        _settingsStore.Save(new ProgramWideSettings
        {
            RepoRootPath = _viewModel.RepoRootPath,
            RepoOrganization = _viewModel.RepoOrganization
        });
    }
}

public sealed class CoreProgramSettingsViewModel : NotifyBase
{
    private string _repoRootPath;
    private RepoOrganizationStrategy _repoOrganization;

    public CoreProgramSettingsViewModel(ProgramWideSettings settings)
    {
        _repoRootPath = string.IsNullOrWhiteSpace(settings.RepoRootPath)
            ? ProgramWideSettings.GetDefaultRepoRoot()
            : settings.RepoRootPath;
        _repoOrganization = settings.RepoOrganization;
    }

    public ObservableCollection<RepoOrganizationStrategy> RepoOrganizationChoices { get; } =
    [
        RepoOrganizationStrategy.GameRepoWithPerModFolders,
        RepoOrganizationStrategy.RepoPerMod
    ];

    public string RepoRootPath
    {
        get => _repoRootPath;
        set => SetField(ref _repoRootPath, value);
    }

    public RepoOrganizationStrategy RepoOrganization
    {
        get => _repoOrganization;
        set => SetField(ref _repoOrganization, value);
    }
}
