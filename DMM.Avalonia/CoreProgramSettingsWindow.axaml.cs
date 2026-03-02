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
        var existing = _settingsStore.Load();
        existing.RepoRootPath = _viewModel.RepoRootPath;
        existing.RepoOrganization = _viewModel.RepoOrganization;
        existing.GitHubAccount = _viewModel.GitHubAccount;
        existing.GitHubToken = _viewModel.GitHubToken;
        existing.GitHubModRootRepo = _viewModel.GitHubModRootRepo;
        _settingsStore.Save(existing);
    }
}

public sealed class CoreProgramSettingsViewModel : NotifyBase
{
    private string _repoRootPath;
    private RepoOrganizationStrategy _repoOrganization;
    private string _gitHubAccount;
    private string _gitHubToken;
    private string _gitHubModRootRepo;

    public CoreProgramSettingsViewModel(ProgramWideSettings settings)
    {
        _repoRootPath = string.IsNullOrWhiteSpace(settings.RepoRootPath)
            ? ProgramWideSettings.GetDefaultRepoRoot()
            : settings.RepoRootPath;
        _repoOrganization = settings.RepoOrganization;
        _gitHubAccount = settings.GitHubAccount;
        _gitHubToken = settings.GitHubToken;
        _gitHubModRootRepo = settings.GitHubModRootRepo;
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

    public string GitHubAccount
    {
        get => _gitHubAccount;
        set => SetField(ref _gitHubAccount, value);
    }

    public string GitHubToken
    {
        get => _gitHubToken;
        set => SetField(ref _gitHubToken, value);
    }

    public string GitHubModRootRepo
    {
        get => _gitHubModRootRepo;
        set => SetField(ref _gitHubModRootRepo, value);
    }
}
