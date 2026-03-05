using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DMM.AssetManagers;
using DMM.AssetManagers.GameStores.BattleNet;
using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;
using DMM.AssetManagers.GameStores.EA;
using DMM.AssetManagers.GameStores.Epic;
using DMM.AssetManagers.GameStores.Gog;
using DMM.AssetManagers.GameStores.Minecraft;
using DMM.AssetManagers.GameStores.Origin;
using DMM.AssetManagers.GameStores.PSN;
using DMM.AssetManagers.GameStores.Rockstar;
using DMM.AssetManagers.GameStores.Steam;
using DMM.AssetManagers.GameStores.XBox;
using DMM.Data;
using DMM.Core;

namespace DMM.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ProgramWideSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _timedAutoSyncTimer = new();
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = MainWindowViewModel.CreateSample();
        DataContext = _viewModel;
        Opened += MainWindow_Opened;
        Closing += MainWindow_Closing;
        _timedAutoSyncTimer.Tick += TimedAutoSyncTimer_Tick;
    }


    private void ConfigureTimedAutoSync()
    {
        var settings = _settingsStore.Load();
        _timedAutoSyncTimer.Stop();

        if (!settings.TimedAutoSyncEnabled)
        {
            return;
        }

        _timedAutoSyncTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, settings.TimedAutoSyncIntervalMinutes));
        _timedAutoSyncTimer.Start();
    }

    private void TimedAutoSyncTimer_Tick(object? sender, EventArgs e)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _viewModel.StatusMessage = _viewModel.TryAutoSyncAllManagedMods(out var message)
            ? $"autocommit : {stamp} :: {message}"
            : $"autocommit : {stamp} :: sync failed ({message})";
    }

    private async Task HandleModFocusCloseSyncAsync(ModListItem mod)
    {
        var settings = _settingsStore.Load();
        if (settings.ModFocusSyncPreference == ModFocusSyncPreference.Never)
        {
            return;
        }

        ModFocusSyncPreference? chosen = settings.ModFocusSyncPreference;
        if (chosen == ModFocusSyncPreference.Prompt)
        {
            chosen = await PromptModFocusSyncPreferenceAsync(mod).ConfigureAwait(true);
            if (chosen is null)
            {
                return;
            }
        }

        if (chosen == ModFocusSyncPreference.Always)
        {
            var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _viewModel.StatusMessage = _viewModel.TryAutoSyncManagedMod(mod, out var syncMessage)
                ? $"autocommit : {stamp} :: {syncMessage}"
                : $"autocommit : {stamp} :: sync failed ({syncMessage})";
        }
    }

    private async Task<ModFocusSyncPreference?> PromptModFocusSyncPreferenceAsync(ModListItem mod)
    {
        var remember = new CheckBox { Content = "Remember my choice", IsChecked = false };
        var syncNow = new Button { Content = "Sync this mod", MinWidth = 120 };
        var skipSync = new Button { Content = "Close without Sync", MinWidth = 140 };
        var cancel = new Button { Content = "Cancel", MinWidth = 88 };
        ModFocusSyncPreference? chosenPreference = null;

        var dialog = new Window
        {
            Title = "Focus Window Sync",
            Width = 560,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(12),
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Sync '{mod.Name}' before leaving Focus?",
                            TextWrapping = TextWrapping.Wrap
                        },
                        remember,
                        new StackPanel
                        {
                            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                            Spacing = 8,
                            Children = { syncNow, skipSync, cancel }
                        }
                    }
                }
            }
        };

        syncNow.Click += (_, _) =>
        {
            chosenPreference = ModFocusSyncPreference.Always;
            dialog.Close();
        };
        skipSync.Click += (_, _) =>
        {
            chosenPreference = ModFocusSyncPreference.Never;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);

        if (chosenPreference is null)
        {
            return null;
        }

        if (remember.IsChecked == true)
        {
            var updatedSettings = _settingsStore.Load();
            updatedSettings.ModFocusSyncPreference = chosenPreference.Value;
            _settingsStore.Save(updatedSettings);
            ConfigureTimedAutoSync();
        }

        return chosenPreference;
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        var settings = _settingsStore.Load();
        if (settings.ExitSyncPreference == ExitSyncPreference.Never)
        {
            return;
        }

        e.Cancel = true;

        if (settings.ExitSyncPreference == ExitSyncPreference.Always)
        {
            _viewModel.TrySyncManagedRepoRoot(out _);
            _allowClose = true;
            Close();
            return;
        }

        var remember = new CheckBox { Content = "Remember my choice", IsChecked = false };
        var syncAndExit = new Button { Content = "Sync and Exit", MinWidth = 120 };
        var exitNoSync = new Button { Content = "Exit without Sync", MinWidth = 120 };
        var cancel = new Button { Content = "Cancel", MinWidth = 88 };
        ExitSyncPreference? chosenPreference = null;

        var dialog = new Window
        {
            Title = "Exit Sync",
            Width = 620,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(12),
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Sync all repos before exiting? This will run pull/submodule sync on your Mod Repo Root.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        remember,
                        new StackPanel
                        {
                            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                            Spacing = 8,
                            Children = { syncAndExit, exitNoSync, cancel }
                        }
                    }
                }
            }
        };

        syncAndExit.Click += (_, _) =>
        {
            chosenPreference = ExitSyncPreference.Always;
            dialog.Close();
        };
        exitNoSync.Click += (_, _) =>
        {
            chosenPreference = ExitSyncPreference.Never;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);

        if (chosenPreference is null)
        {
            return;
        }

        if (remember.IsChecked == true)
        {
            settings.ExitSyncPreference = chosenPreference.Value;
            _settingsStore.Save(settings);
        }

        if (chosenPreference == ExitSyncPreference.Always)
        {
            _viewModel.TrySyncManagedRepoRoot(out _);
        }

        _allowClose = true;
        Close();
    }

    private async void MainWindow_Opened(object? sender, System.EventArgs e)
    {
        if (_viewModel.GameInstalls.Count > 0)
        {
            ConfigureTimedAutoSync();
            return;
        }

        var wizard = new GameInstallWizardWindow(_viewModel, isFirstRun: true);
        await wizard.ShowDialog(this);
        _viewModel.SyncGameFoldersFromInstalls();
        _viewModel.StatusMessage = _viewModel.GameInstalls.Count > 0
            ? $"First-run game setup completed. Added {_viewModel.GameInstalls.Count} game install(s)."
            : "First-run setup closed without selecting game installs.";

        ConfigureTimedAutoSync();
    }

    private async void RescanGameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryValidateGitHubOnboardingSettings(out var settingsMessage))
        {
            _viewModel.StatusMessage = settingsMessage;
            await ShowInfoDialogAsync("Scan Apply Blocked", settingsMessage);
            return;
        }

        var selections = _viewModel.GetCurrentManagedSelectionsForRescan();
        if (selections.Count == 0)
        {
            _viewModel.StatusMessage = "Rescan skipped: no managed mods are currently listed for the selected folder.";
            return;
        }

        await RunScanApplyWithProgressDialogAsync(selections);
    }

    private async void ScanGameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryValidateGitHubOnboardingSettings(out var settingsMessage))
        {
            _viewModel.StatusMessage = settingsMessage;
            await ShowInfoDialogAsync("Scan Apply Blocked", settingsMessage);
            return;
        }

        var scan = _viewModel.ScanSelectedGameFolderForMods();
        if (!scan.Success)
        {
            return;
        }

        if (scan.DiscoveredCandidates.Count == 0)
        {
            _viewModel.StatusMessage = "Scan complete. No non-base plugin candidates were found in the selected game data folder.";
            return;
        }

        var window = new GameFolderScanWindow(scan.DiscoveredCandidates, _viewModel.StageOptions);
        var result = await window.ShowDialog<GameFolderScanApplyResult?>(this);
        if (result is null)
        {
            _viewModel.StatusMessage =
                $"Scan complete. Found {scan.DiscoveredCandidates.Count} non-base plugin candidate(s). No import actions were applied.";
            return;
        }

        await RunScanApplyWithProgressDialogAsync(result.SelectedMods);

        if (_viewModel.StatusMessage.StartsWith("Scan apply blocked:", StringComparison.OrdinalIgnoreCase))
        {
            await ShowInfoDialogAsync("Scan Apply Blocked", _viewModel.StatusMessage);
        }
        else if (_viewModel.StatusMessage.Contains("bootstrap needed:", StringComparison.OrdinalIgnoreCase)
                 && !_viewModel.StatusMessage.Contains("bootstrap needed: 0", StringComparison.OrdinalIgnoreCase))
        {
            await ShowInfoDialogAsync(
                "Local Repo Bootstrap Needed",
                "PAT is configured, but onboarding still needs local per-mod git repos to exist under your Mod Repo Root. " +
                "Please create/bootstrap those repos (or use the upcoming automated bootstrap flow), then run Scan Apply again.");
        }
    }

    private async Task RunScanApplyWithProgressDialogAsync(IReadOnlyList<GameFolderStageSelection> selections)
    {
        var reviewSelections = await DependencyReviewCoordinator.BuildSelectionsAsync(this, _viewModel, _viewModel.SelectedGameFolder, selections);
        if (reviewSelections is null)
        {
            _viewModel.StatusMessage = "Scan apply canceled during dependency review.";
            return;
        }

        var progressWindow = new Window
        {
            Title = "Applying Scan Selection",
            Width = 520,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(12),
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Applying selected mods. This can take a moment while DMM bootstraps repositories, syncs submodules, and commits onboarding files.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new ProgressBar
                        {
                            IsIndeterminate = true,
                            Height = 12,
                            MinWidth = 420
                        },
                        new TextBlock
                        {
                            Text = "Working... please wait.",
                            FontStyle = FontStyle.Italic
                        }
                    }
                }
            }
        };

        var progressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Height = 12,
            MinWidth = 420,
            Minimum = 0,
            Maximum = Math.Max(1, selections.Count),
            Value = 0
        };
        var progressText = new TextBlock
        {
            Text = "Starting...",
            FontStyle = FontStyle.Italic
        };

        if (progressWindow.Content is Border { Child: StackPanel panel })
        {
            panel.Children[1] = progressBar;
            panel.Children[2] = progressText;
        }

        _ = progressWindow.ShowDialog(this);
        await Task.Delay(50);

        var progress = new Progress<ScanApplyProgress>(update =>
        {
            progressBar.Maximum = Math.Max(1, update.TotalMods);
            progressBar.Value = Math.Min(update.CompletedMods, progressBar.Maximum);
            progressText.Text = $"{update.Message} ({update.CompletedMods}/{update.TotalMods})";
            _viewModel.StatusMessage = update.Message;
        });

        try
        {
            await Task.Run(async () => await _viewModel.ApplyScanSelections(selections, progress, reviewSelections));
        }
        finally
        {
            progressWindow.Close();
        }
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var ok = new Button { Content = "OK", MinWidth = 88 };
        var dialog = new Window
        {
            Title = title,
            Width = 560,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(12),
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                        ok
                    }
                }
            }
        };

        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }


    private async void OpenHelp_Click(object? sender, RoutedEventArgs e)
    {
        var helpWindow = HelpWindow.ForSection("Main");
        await helpWindow.ShowDialog(this);
        _viewModel.StatusMessage = "Help viewed.";
    }

    private async void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new CoreProgramSettingsWindow();
        var manageInstalls = await settingsWindow.ShowDialog<bool>(this);
        if (!manageInstalls)
        {
            ConfigureTimedAutoSync();
            _viewModel.StatusMessage = "Settings closed.";
            return;
        }

        var wizard = new GameInstallWizardWindow(_viewModel, isFirstRun: false);
        await wizard.ShowDialog(this);
        _viewModel.SyncGameFoldersFromInstalls();
        ConfigureTimedAutoSync();
        _viewModel.StatusMessage = "Settings: game install management completed.";
    }

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

    private void OpenGameFolder_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _viewModel.SelectedGameFolder;
        if (string.IsNullOrWhiteSpace(selected) || !Directory.Exists(selected))
        {
            _viewModel.StatusMessage = "Open game folder failed: selected folder is missing.";
            return;
        }

        var folderToOpen = selected;
        var contentRoot = Path.Combine(selected, "Content");
        if (Directory.Exists(Path.Combine(contentRoot, "Data")))
        {
            folderToOpen = contentRoot;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folderToOpen,
                UseShellExecute = true
            });
            _viewModel.StatusMessage = $"Opened game folder: {folderToOpen}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Open game folder failed: {ex.Message}";
        }
    }

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
        _viewModel.StatusMessage = _viewModel.TrySyncManagedRepoRoot(out var syncMessage)
            ? $"Git control: sync completed. {syncMessage}"
            : $"Git control: sync failed. {syncMessage}";

    private void GitDown_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage = "Git control: pull/down requested.";

    private void SortByMod_Click(object? sender, RoutedEventArgs e) => _viewModel.ToggleSortByModName();

    private void SortByPrimaryPlugin_Click(object? sender, RoutedEventArgs e) => _viewModel.ToggleSortByPrimaryPlugin();

    private void SortByStage_Click(object? sender, RoutedEventArgs e) => _viewModel.ToggleSortByCurrentStage();

    private async void OpenModWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ModListItem mod })
        {
            var matchingFolders = _viewModel.GetGameFoldersForGame(mod.GameName);
            var activeStages = _viewModel.GetAvailableStagesForMod(mod);
            var modWindow = new ModWindow(mod, matchingFolders, activeStages, _viewModel.SelectedGameFolder, RunSingleModGatherDependenciesAsync);
            await modWindow.ShowDialog(this);
            await HandleModFocusCloseSyncAsync(mod);
            if (!_viewModel.StatusMessage.StartsWith("autocommit :", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.StatusMessage = $"Closed focus window for {mod.Name}.";
            }
        }
    }


    private async Task<string> RunSingleModGatherDependenciesAsync(ModDependencyGatherRequest request)
    {
        var stage = string.IsNullOrWhiteSpace(request.Stage) ? "DEV" : request.Stage;
        var selection = new GameFolderStageSelection(request.ModName, request.PrimaryPlugin, stage);

        var reviewSelections = await DependencyReviewCoordinator.BuildSelectionsAsync(this, _viewModel, request.GameFolder, new[] { selection });
        if (reviewSelections is null)
        {
            _viewModel.StatusMessage = "Single-mod gather canceled during dependency review.";
            return _viewModel.StatusMessage;
        }

        await _viewModel.ApplyScanSelectionsForGameFolder(request.GameFolder, new[] { selection }, null, reviewSelections);
        return _viewModel.StatusMessage;
    }
}

public sealed class MainWindowViewModel : NotifyBase
{
    private enum ModSortColumn
    {
        ModName,
        PrimaryPlugin,
        Stage
    }

    private readonly GameSetupRepository _repository = new();
    private readonly ProgramWideSettingsStore _settingsStore = new();
    private readonly ModOnboardingGitService _gitService = new();
    private readonly SharedCatalogService _catalogService = new();
    private readonly ModDependencyDiscoveryService _dependencyDiscoveryService = new();
    private readonly ModScanRulesService _modScanRulesService = new();

    public ObservableCollection<string> GameFolders { get; } = new();
    public ObservableCollection<string> StageOptions { get; } = new();
    public ObservableCollection<ModListItem> Mods { get; } = new();
    public ObservableCollection<ManagedGame> ManagedGames { get; } = new();
    public ObservableCollection<GameInstallRecord> GameInstalls { get; } = new();

    private ModSortColumn _sortColumn = ModSortColumn.ModName;
    private bool _sortAscending = true;

    public string ModHeaderText => BuildSortHeader("Mod", ModSortColumn.ModName);
    public string PrimaryPluginHeaderText => BuildSortHeader("Primary Plugin", ModSortColumn.PrimaryPlugin);
    public string CurrentStageHeaderText => BuildSortHeader("Current Stage", ModSortColumn.Stage);

    private string? _selectedGameFolder;
    public string? SelectedGameFolder
    {
        get => _selectedGameFolder;
        set
        {
            if (SetField(ref _selectedGameFolder, value))
            {
                PersistLastSelectedGameFolder(value);
                RebuildMods();
            }
        }
    }

    private string _statusMessage = "Ready. Choose a mod and open Focus for per-mod operations.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public void ToggleSortByModName() => ToggleSort(ModSortColumn.ModName);

    public void ToggleSortByPrimaryPlugin() => ToggleSort(ModSortColumn.PrimaryPlugin);

    public void ToggleSortByCurrentStage() => ToggleSort(ModSortColumn.Stage);

    private void ToggleSort(ModSortColumn column)
    {
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        OnPropertyChanged(nameof(ModHeaderText));
        OnPropertyChanged(nameof(PrimaryPluginHeaderText));
        OnPropertyChanged(nameof(CurrentStageHeaderText));
        RebuildMods();
    }

    private string BuildSortHeader(string label, ModSortColumn column)
    {
        if (_sortColumn != column)
        {
            return label;
        }

        return _sortAscending ? $"{label} ▲" : $"{label} ▼";
    }

    private IEnumerable<ManagedModRecord> ApplyModSort(IEnumerable<ManagedModRecord> mods)
    {
        return (_sortColumn, _sortAscending) switch
        {
            (ModSortColumn.PrimaryPlugin, true) => mods.OrderBy(x => x.PrimaryPlugin, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ModName, StringComparer.OrdinalIgnoreCase),
            (ModSortColumn.PrimaryPlugin, false) => mods.OrderByDescending(x => x.PrimaryPlugin, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ModName, StringComparer.OrdinalIgnoreCase),
            (ModSortColumn.Stage, true) => mods.OrderBy(x => x.Stage, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ModName, StringComparer.OrdinalIgnoreCase),
            (ModSortColumn.Stage, false) => mods.OrderByDescending(x => x.Stage, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ModName, StringComparer.OrdinalIgnoreCase),
            (ModSortColumn.ModName, true) => mods.OrderBy(x => x.ModName, StringComparer.OrdinalIgnoreCase),
            _ => mods.OrderByDescending(x => x.ModName, StringComparer.OrdinalIgnoreCase)
        };
    }

    public static MainWindowViewModel CreateSample()
    {
        var vm = new MainWindowViewModel();
        vm.LoadManagedGames();
        vm.LoadPersistedInstalls();

        vm.StageOptions.Add("DEV");
        vm.StageOptions.Add("TEST");
        vm.StageOptions.Add("PREFLIGHT");
        vm.StageOptions.Add("PRERELEASE");
        vm.StageOptions.Add("RELEASE");

        vm.RebuildMods();

        return vm;
    }

    private void LoadManagedGames()
    {
        foreach (var game in _repository.LoadManagedGames())
        {
            ManagedGames.Add(game);
        }

        if (ManagedGames.Count > 0)
        {
            return;
        }

        ManagedGames.Add(new ManagedGame { Name = "Starfield", Executable = "Starfield.exe", StoreId = "1716740" });
        ManagedGames.Add(new ManagedGame { Name = "Fallout 4", Executable = "Fallout4.exe", StoreId = "377160" });
        ManagedGames.Add(new ManagedGame { Name = "Skyrim Special Edition", Executable = "SkyrimSE.exe", StoreId = "489830" });
    }

    private void LoadPersistedInstalls()
    {
        foreach (var install in _repository.LoadManagedInstalls(ManagedGames))
        {
            GameInstalls.Add(install);
        }

        SyncGameFoldersFromInstalls();
        RebuildMods();
    }

    public void PersistManagedGame(ManagedGame game) => _repository.UpsertManagedGame(game);

    public void PersistSelectedInstalls(IReadOnlyList<GameInstallRecord> selectedInstalls)
    {
        _repository.ReplaceManagedInstalls(selectedInstalls, ManagedGames);
        GameInstalls.Clear();
        foreach (var install in selectedInstalls)
        {
            GameInstalls.Add(install.Clone());
        }

        SyncGameFoldersFromInstalls();
        RebuildMods();
    }

    public IReadOnlyList<GameFolderStageSelection> GetCurrentManagedSelectionsForRescan()
    {
        var selectedGameFolder = SelectedGameFolder;
        if (string.IsNullOrWhiteSpace(selectedGameFolder))
        {
            return Array.Empty<GameFolderStageSelection>();
        }

        var install = GameInstalls.FirstOrDefault(x =>
            !x.IsDlc &&
            x.ManagedGame is not null &&
            string.Equals(x.InstallPath, selectedGameFolder, StringComparison.OrdinalIgnoreCase));

        if (install?.ManagedGame is null)
        {
            return Array.Empty<GameFolderStageSelection>();
        }

        return _repository.LoadManagedModsForInstall(selectedGameFolder, install.ManagedGame.Name)
            .Select(x => new GameFolderStageSelection(x.ModName, x.PrimaryPlugin, x.Stage))
            .OrderBy(x => x.PluginName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public GameFolderScanResult ScanSelectedGameFolderForMods()
    {
        if (string.IsNullOrWhiteSpace(SelectedGameFolder))
        {
            StatusMessage = "Scan failed: no game folder selected.";
            return GameFolderScanResult.Failed();
        }

        var selectedGameFolder = SelectedGameFolder!;
        var install = GameInstalls.FirstOrDefault(x =>
            !x.IsDlc &&
            x.ManagedGame is not null &&
            string.Equals(x.InstallPath, selectedGameFolder, StringComparison.OrdinalIgnoreCase));

        if (install?.ManagedGame is null)
        {
            StatusMessage = "Scan failed: selected game folder is not mapped to a managed base game install.";
            return GameFolderScanResult.Failed();
        }

        if (!TryResolveGameDataRoot(selectedGameFolder, out var scanRoot, out _))
        {
            StatusMessage = $"Scan failed: game data folder not found under '{selectedGameFolder}'.";
            return GameFolderScanResult.Failed();
        }

        var knownPluginNames = _repository.LoadKnownPluginsForGameIncludingDlc(install.ManagedGame.Name)
            .Select(x => x.PluginName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownPluginBaseNames = knownPluginNames
            .Select(Path.GetFileNameWithoutExtension)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingManagedMods = _repository.LoadManagedModsForInstall(selectedGameFolder, install.ManagedGame.Name);
        var managedPluginNames = existingManagedMods
            .Select(x => x.PrimaryPlugin)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var managedPluginBaseNames = managedPluginNames
            .Select(Path.GetFileNameWithoutExtension)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var discovered = Directory.EnumerateFiles(scanRoot, "*.*", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name =>
            {
                var ext = Path.GetExtension(name);
                return string.Equals(ext, ".esm", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(ext, ".esp", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(ext, ".esl", StringComparison.OrdinalIgnoreCase);
            })
            .Where(name => !knownPluginNames.Contains(name))
            .Where(name => !knownPluginBaseNames.Contains(Path.GetFileNameWithoutExtension(name)))
            .Where(name => !managedPluginNames.Contains(name))
            .Where(name => !managedPluginBaseNames.Contains(Path.GetFileNameWithoutExtension(name)))
            .Where(name => !_modScanRulesService.IsOfficialPluginName(install.ManagedGame.Name, name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .GroupBy(name => Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(name => _modScanRulesService.GetPluginExtensionPriority(Path.GetExtension(name)))
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StatusMessage = discovered.Count == 0
            ? "Scan complete. No non-base plugin candidates were found in the selected game data folder."
            : $"Scan complete. Found {discovered.Count} non-base plugin candidate(s). Choose stage actions in the scan wizard.";

        var candidates = discovered
            .Select(plugin => new GameFolderScanCandidate(Path.GetFileNameWithoutExtension(plugin), plugin))
            .ToList();

        return GameFolderScanResult.Succeeded(candidates);
    }


    public bool TryCollectDependencyPreview(
        string? gameFolder,
        GameFolderStageSelection selection,
        out ModDependencyPreview preview,
        out string error)
    {
        preview = default!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(gameFolder))
        {
            error = "no game folder selected";
            return false;
        }

        if (!TryResolveGameDataRoot(gameFolder, out var scanRoot, out var resolvedGameRoot))
        {
            error = $"game data folder not found under '{gameFolder}'";
            return false;
        }

        try
        {
            var pluginFiles = CollectCanonicalPluginFiles(scanRoot, selection.ModName, selection.PluginName);
            var ba2Files = new[]
            {
                Path.Combine(scanRoot, selection.ModName + " - Main.ba2"),
                Path.Combine(scanRoot, selection.ModName + " - Main_xbox.ba2"),
                Path.Combine(scanRoot, selection.ModName + " - Textures.ba2"),
                Path.Combine(scanRoot, selection.ModName + " - Textures_xbox.ba2")
            }
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            var discovery = _dependencyDiscoveryService.CollectInitialFiles(scanRoot, resolvedGameRoot, selection.ModName, selection.PluginName);
            preview = new ModDependencyPreview(pluginFiles, ba2Files, discovery, scanRoot, resolvedGameRoot);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryValidateGitHubOnboardingSettings(out string message)
    {
        var settings = _settingsStore.Load();
        if (_gitService.HasRequiredGitHubSettings(settings.GitHubAccount, settings.GitHubToken, settings.GitHubModRootRepo, out var missingSettings))
        {
            message = string.Empty;
            return true;
        }

        message = $"Scan apply blocked: configure GitHub settings first ({missingSettings}) in Program Settings.";
        return false;
    }

    public Task ApplyScanSelections(
        IReadOnlyList<GameFolderStageSelection> selections,
        IProgress<ScanApplyProgress>? progress = null,
        IReadOnlyDictionary<string, HashSet<string>>? reviewSelections = null)
    {
        return ApplyScanSelectionsForGameFolder(SelectedGameFolder, selections, progress, reviewSelections);
    }

    public async Task ApplyScanSelectionsForGameFolder(
        string? gameFolder,
        IReadOnlyList<GameFolderStageSelection> selections,
        IProgress<ScanApplyProgress>? progress = null,
        IReadOnlyDictionary<string, HashSet<string>>? reviewSelections = null)
    {
        Mods.Clear();

        async Task SetStatusAsync(string message)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                StatusMessage = message;
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = message);
            }
        }

        if (string.IsNullOrWhiteSpace(gameFolder))
        {
            await SetStatusAsync("Scan apply failed: no game folder selected.");
            return;
        }

        var selectedGameFolder = gameFolder!;

        var install = GameInstalls.FirstOrDefault(x =>
            !x.IsDlc &&
            x.ManagedGame is not null &&
            string.Equals(x.InstallPath, selectedGameFolder, StringComparison.OrdinalIgnoreCase));

        if (install?.ManagedGame is null)
        {
            await SetStatusAsync("Scan apply failed: selected game folder is not mapped to a managed base game install.");
            return;
        }

        if (!TryResolveGameDataRoot(selectedGameFolder, out var scanRoot, out var resolvedGameRoot))
        {
            await SetStatusAsync($"Scan apply failed: game data folder not found under '{selectedGameFolder}'.");
            return;
        }

        var settings = _settingsStore.Load();
        var repoRoot = string.IsNullOrWhiteSpace(settings.RepoRootPath)
            ? ProgramWideSettings.GetDefaultRepoRoot()
            : settings.RepoRootPath;

        if (!TryValidateGitHubOnboardingSettings(out var settingsMessage))
        {
            await SetStatusAsync(settingsMessage);
            return;
        }

        var created = 0;
        var copiedFiles = 0;
        var skipped = 0;
        var bootstrapRequired = 0;
        var failed = 0;
        var bootstrapPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failureDetails = new List<string>();
        var dependencyFilesIncluded = 0;
        var dependencyCollisionCount = 0;
        var dependencyMissingCount = 0;
        var dependencyParentHitCount = 0;
        var parentMasterCountMax = 0;
        var parentArchiveCountMax = 0;
        var parentZipCountMax = 0;
        var parentIndexedFileCountMax = 0;
        long parentIndexedBytesMax = 0;
        long parentEstimatedRecordBytesMax = 0;
        var parentNonBa2CountMax = 0;
        var parentReadFailureCountMax = 0;
        var parentAttemptedArchiveCountMax = 0;
        string? parentNonBa2Sample = null;
        string? parentAttemptedArchiveSample = null;
        string? parentLastArchiveCandidate = null;
        string? parentLastArchiveOutcome = null;
        long dependencyScanMsTotal = 0;

        var orderedSelections = selections.OrderBy(x => x.PluginName, StringComparer.OrdinalIgnoreCase).ToList();
        var completedMods = 0;

        Task<(bool SourceExists, ModDependencyDiscoveryResult? Discovery, List<ModDependencyEntry> InitialEntries, string? Error)> StartDependencyScanTask(GameFolderStageSelection selection)
        {
            return Task.Run(() =>
            {
                try
                {
                    var sourcePath = Path.Combine(scanRoot, selection.PluginName);
                    if (!File.Exists(sourcePath))
                    {
                        return (false, (ModDependencyDiscoveryResult?)null, new List<ModDependencyEntry>(), (string?)null);
                    }

                    var discovery = _dependencyDiscoveryService.CollectInitialFiles(scanRoot, resolvedGameRoot, selection.ModName, selection.PluginName);
                    var initialEntries = discovery.Entries
                        .Where(x => !x.ParentArchiveMatch)
                        .OrderBy(x => x.RelativeDataPath, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (reviewSelections is not null && reviewSelections.TryGetValue(BuildSelectionReviewKey(selection), out var reviewedKeep))
                    {
                        initialEntries = discovery.Entries
                            .Where(x => IsPluginOrArchiveRelativePath(x.RelativeDataPath) || reviewedKeep.Contains(x.RelativeDataPath))
                            .OrderBy(x => x.RelativeDataPath, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }

                    return (true, discovery, initialEntries, (string?)null);
                }
                catch (Exception ex)
                {
                    return (true, (ModDependencyDiscoveryResult?)null, new List<ModDependencyEntry>(), ex.Message);
                }
            });
        }

        async Task ReportProgressAsync(string message)
        {
            await SetStatusAsync(message);
            progress?.Report(new ScanApplyProgress(completedMods, orderedSelections.Count, message));
            await Task.Yield();
        }

        Task<(bool SourceExists, ModDependencyDiscoveryResult? Discovery, List<ModDependencyEntry> InitialEntries, string? Error)>? pendingScanTask =
            orderedSelections.Count > 0 ? StartDependencyScanTask(orderedSelections[0]) : null;

        for (var selectionIndex = 0; selectionIndex < orderedSelections.Count; selectionIndex++)
        {
            var selection = orderedSelections[selectionIndex];
            await ReportProgressAsync($"Preparing {selection.ModName}");
            await ReportProgressAsync($"Scanning {selection.ModName} dependencies (verify discovered files)");

            var (sourceExists, discovery, initialEntries, scanError) = pendingScanTask is null
                ? (false, (ModDependencyDiscoveryResult?)null, new List<ModDependencyEntry>(), "internal scan task was not initialized")
                : await pendingScanTask;

            pendingScanTask = selectionIndex + 1 < orderedSelections.Count
                ? StartDependencyScanTask(orderedSelections[selectionIndex + 1])
                : null;

            if (!sourceExists)
            {
                skipped++;
                completedMods++;
                failureDetails.Add($"{selection.ModName}: source plugin file '{selection.PluginName}' was not found under {scanRoot}");
                await ReportProgressAsync($"Skipped {selection.ModName}: source plugin file was not found");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(scanError) || discovery is null)
            {
                failed++;
                completedMods++;
                failureDetails.Add($"{selection.ModName}: dependency scan failed ({scanError ?? "unknown error"})");
                await ReportProgressAsync($"Failed scanning {selection.ModName}: {scanError ?? "unknown error"}");
                continue;
            }

            try
            {
                var modRepoRoot = ModRepositoryPathService.BuildModRepoRoot(repoRoot, install.ManagedGame.Name, selection.ModName, settings.RepoOrganization == RepoOrganizationStrategy.RepoPerMod);
                
                if (initialEntries.Count == 0)
                {
                    skipped++;
                    completedMods++;
                    failureDetails.Add($"{selection.ModName}: no dependency files were selected to stage");
                    await ReportProgressAsync($"Skipped {selection.ModName}: no dependency files selected");
                    continue;
                }

                if (!_gitService.IsGitWorkingTree(modRepoRoot))
                {
                    await ReportProgressAsync($"Bootstrapping git repo for {selection.ModName}");
                    var bootstrapped = _gitService.TryBootstrapModRepository(
                        settings.GitHubAccount,
                        settings.GitHubToken,
                        settings.GitHubModRootRepo,
                        repoRoot,
                        install.ManagedGame.Name,
                        selection.ModName,
                        modRepoRoot,
                        out var bootstrapError);
                    if (!bootstrapped)
                    {
                        bootstrapRequired++;
                        bootstrapPaths.Add($"{modRepoRoot} ({bootstrapError})");
                        failureDetails.Add($"{selection.ModName}: bootstrap failed ({bootstrapError})");
                        skipped++;
                        await ReportProgressAsync($"Skipped {selection.ModName}: bootstrap failed ({bootstrapError})");
                        continue;
                    }
                }

                var targetStageBranch = _gitService.ToStageBranch(selection.Stage);
                await ReportProgressAsync($"Checking out {targetStageBranch} for {selection.ModName}");
                if (!_gitService.EnsureBranchCheckedOut(modRepoRoot, targetStageBranch, out var branchError))
                {
                    bootstrapRequired++;
                    bootstrapPaths.Add($"{modRepoRoot} ({branchError})");
                    failureDetails.Add($"{selection.ModName}: failed to checkout branch {targetStageBranch} ({branchError})");
                    skipped++;
                    await ReportProgressAsync($"Skipped {selection.ModName}: branch checkout failed ({branchError})");
                    continue;
                }

                var stageFolder = Path.Combine(modRepoRoot, "loosefiles", "Data");
                Directory.CreateDirectory(stageFolder);

                dependencyFilesIncluded += initialEntries.Count;
                dependencyCollisionCount += discovery.CollisionCount;
                dependencyMissingCount += discovery.MissingReferences.Count;
                dependencyParentHitCount += discovery.ParentArchiveReferences.Count;
                parentMasterCountMax = Math.Max(parentMasterCountMax, discovery.ParentMasterCount);
                parentArchiveCountMax = Math.Max(parentArchiveCountMax, discovery.ParentArchiveCount);
                parentZipCountMax = Math.Max(parentZipCountMax, discovery.ParentZipCount);
                parentIndexedFileCountMax = Math.Max(parentIndexedFileCountMax, discovery.ParentIndexedFileCount);
                parentIndexedBytesMax = Math.Max(parentIndexedBytesMax, discovery.ParentIndexedBytes);
                parentEstimatedRecordBytesMax = Math.Max(parentEstimatedRecordBytesMax, discovery.ParentEstimatedRecordBytes);
                parentNonBa2CountMax = Math.Max(parentNonBa2CountMax, discovery.ParentNonBa2CandidateCount);
                parentReadFailureCountMax = Math.Max(parentReadFailureCountMax, discovery.ParentReadFailureCount);
                parentAttemptedArchiveCountMax = Math.Max(parentAttemptedArchiveCountMax, discovery.ParentAttemptedArchiveCount);
                parentNonBa2Sample ??= discovery.ParentNonBa2CandidateSamples.FirstOrDefault() ?? discovery.ParentReadFailureSamples.FirstOrDefault();
                parentAttemptedArchiveSample ??= discovery.ParentAttemptedArchiveSamples.FirstOrDefault();
                parentLastArchiveCandidate = discovery.ParentLastArchiveCandidate ?? parentLastArchiveCandidate;
                parentLastArchiveOutcome = discovery.ParentLastArchiveOutcome ?? parentLastArchiveOutcome;
                dependencyScanMsTotal += discovery.ScanMs;

                await ReportProgressAsync($"Copying {selection.ModName} files");

                foreach (var entry in initialEntries)
                {
                    var relUnderData = entry.RelativeDataPath.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                        ? entry.RelativeDataPath["Data\\".Length..]
                        : entry.RelativeDataPath;

                    var isPluginOrArchive = relUnderData.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)
                        || relUnderData.EndsWith(".esp", StringComparison.OrdinalIgnoreCase)
                        || relUnderData.EndsWith(".esl", StringComparison.OrdinalIgnoreCase)
                        || relUnderData.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase);

                    var target = isPluginOrArchive
                        ? Path.Combine(modRepoRoot, "main", Path.GetFileName(relUnderData))
                        : Path.Combine(stageFolder, relUnderData);
                    Directory.CreateDirectory(Path.GetDirectoryName(target) ?? stageFolder);
                    File.Copy(entry.SourcePath, target, overwrite: true);
                    copiedFiles++;

                    if (!isPluginOrArchive && !string.IsNullOrWhiteSpace(entry.XboxRelativePath) && !string.IsNullOrWhiteSpace(entry.XboxSourcePath))
                    {
                        var xboxRel = entry.XboxRelativePath!.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                            ? entry.XboxRelativePath["Data\\".Length..]
                            : entry.XboxRelativePath;
                        var xboxFolder = Path.Combine(modRepoRoot, "loosefiles", "XBOX", "Data");
                        var xboxTarget = Path.Combine(xboxFolder, xboxRel);
                        Directory.CreateDirectory(Path.GetDirectoryName(xboxTarget) ?? xboxFolder);
                        File.Copy(entry.XboxSourcePath!, xboxTarget, overwrite: true);
                    }

                    if (!string.IsNullOrWhiteSpace(entry.TifRelativePath) && !string.IsNullOrWhiteSpace(entry.TifSourcePath))
                    {
                        var tifRel = entry.TifRelativePath!.StartsWith("TGATextures\\", StringComparison.OrdinalIgnoreCase)
                            ? entry.TifRelativePath["TGATextures\\".Length..]
                            : entry.TifRelativePath;
                        var tifFolder = Path.Combine(modRepoRoot, "loosefiles", "TGATextures");
                        var tifTarget = Path.Combine(tifFolder, tifRel);
                        Directory.CreateDirectory(Path.GetDirectoryName(tifTarget) ?? tifFolder);
                        File.Copy(entry.TifSourcePath!, tifTarget, overwrite: true);
                    }
                }

                ModMetadataService.WriteMetadataFiles(modRepoRoot, selection.ModName, selection.PluginName, initialEntries, discovery);

                var relativeSubmodulePath = Path.GetRelativePath(repoRoot, modRepoRoot)
                    .Replace('\\', '/');
                await ReportProgressAsync($"Committing and syncing {selection.ModName} ({targetStageBranch})");
                if (!_gitService.CommitAndPushOnboardingChanges(repoRoot, modRepoRoot, relativeSubmodulePath, targetStageBranch, selection.ModName, out var commitError))
                {
                    bootstrapRequired++;
                    bootstrapPaths.Add($"{modRepoRoot} ({commitError})");
                    failureDetails.Add($"{selection.ModName}: commit/push failed ({commitError})");
                    skipped++;
                    await ReportProgressAsync($"Skipped {selection.ModName}: commit/push failed ({commitError})");
                    continue;
                }

                _repository.UpsertManagedModForInstall(
                    install.ManagedGame.Name,
                    selectedGameFolder,
                    selection.ModName,
                    selection.PluginName,
                    selection.Stage,
                    modRepoRoot);

                var modRepoName = $"{_gitService.ToSlug(install.ManagedGame.Name)}-{_gitService.ToSlug(selection.ModName)}";
                var submodulePath = relativeSubmodulePath;
                _catalogService.UpsertEntry(repoRoot, new SharedCatalogEntry(
                    install.ManagedGame.Name,
                    selection.ModName,
                    selection.PluginName,
                    modRepoName,
                    submodulePath,
                    $"https://github.com/{settings.GitHubAccount}/{modRepoName}.git",
                    targetStageBranch));

                created++;
            }
            catch (Exception ex)
            {
                failed++;
                failureDetails.Add($"{selection.ModName}: unexpected exception ({ex.Message})");
                await ReportProgressAsync($"Failed {selection.ModName}; continuing with remaining mods ({ex.Message})");
            }

            completedMods++;
            await ReportProgressAsync($"Completed {selection.ModName}");
        }

        if (selections.Count == 0)
        {
            await SetStatusAsync("Scan apply complete. All discovered candidates were ignored.");
            return;
        }

        var bootstrapPreview = bootstrapPaths.Count == 0
            ? string.Empty
            : $" First missing repo: {bootstrapPaths.First()}";

        var failurePreview = failureDetails.Count == 0
            ? string.Empty
            : $" Failure details: {string.Join(" | ", failureDetails.Take(3))}{(failureDetails.Count > 3 ? $" (+{failureDetails.Count - 3} more)" : string.Empty)}";

        var finalStatus =
            $"Scan apply complete. Added {created} mod(s); copied {copiedFiles} file(s); dependency files included {dependencyFilesIncluded}; parent-archive collisions filtered {dependencyCollisionCount}; parent/base hits {dependencyParentHitCount}; missing refs {dependencyMissingCount}; parent catalog snapshot: masters={parentMasterCountMax}, ba2 archives={parentArchiveCountMax}, zips={parentZipCountMax}, indexed files={parentIndexedFileCountMax}, indexed bytes={parentIndexedBytesMax}, est record bytes={parentEstimatedRecordBytesMax}, attempted archives={parentAttemptedArchiveCountMax}, non-ba2 skipped={parentNonBa2CountMax}, read-failures={parentReadFailureCountMax} (scan {dependencyScanMsTotal} ms); skipped {skipped} (local git repo bootstrap needed: {bootstrapRequired}); failed {failed}. Repo root: {repoRoot}. Mod repos were pushed and parent submodule pointers were synced.{(string.IsNullOrWhiteSpace(parentNonBa2Sample) ? string.Empty : $" Sample skipped candidate: {parentNonBa2Sample}")}{(string.IsNullOrWhiteSpace(parentAttemptedArchiveSample) ? string.Empty : $" Sample attempted archive: {parentAttemptedArchiveSample}")}{(string.IsNullOrWhiteSpace(parentLastArchiveCandidate) ? string.Empty : $" Last archive candidate: {parentLastArchiveCandidate} ({parentLastArchiveOutcome ?? "unknown outcome"})")}{bootstrapPreview}{failurePreview}";

        await SetStatusAsync(finalStatus);
        await Dispatcher.UIThread.InvokeAsync(RebuildMods);
        await SetStatusAsync(finalStatus);
    }


    public static bool IsPluginOrArchiveRelativePath(string relativeDataPath)
    {
        if (string.IsNullOrWhiteSpace(relativeDataPath))
            return false;

        return relativeDataPath.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)
               || relativeDataPath.EndsWith(".esp", StringComparison.OrdinalIgnoreCase)
               || relativeDataPath.EndsWith(".esl", StringComparison.OrdinalIgnoreCase)
               || relativeDataPath.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> CollectCanonicalPluginFiles(string scanRoot, string modName, string primaryPlugin)
    {
        var expectedPluginName = $"{modName}{Path.GetExtension(primaryPlugin)}";
        var pluginCandidates = new[]
        {
            Path.Combine(scanRoot, primaryPlugin),
            Path.Combine(scanRoot, expectedPluginName),
            Path.Combine(scanRoot, $"{modName}.esm"),
            Path.Combine(scanRoot, $"{modName}.esp"),
            Path.Combine(scanRoot, $"{modName}.esl")
        };

        return pluginCandidates
            .Where(File.Exists)
            .Where(path =>
            {
                var ext = Path.GetExtension(path);
                return string.Equals(ext, ".esm", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(ext, ".esp", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(ext, ".esl", StringComparison.OrdinalIgnoreCase);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string BuildSelectionReviewKey(GameFolderStageSelection selection)
        => $"{selection.ModName}::{selection.PluginName}".ToLowerInvariant();

    private static bool TryResolveGameDataRoot(string selectedGameFolder, out string dataRoot, out string gameRoot)
    {
        dataRoot = string.Empty;
        gameRoot = selectedGameFolder;

        var directData = Path.Combine(selectedGameFolder, "Data");
        if (Directory.Exists(directData))
        {
            dataRoot = directData;
            return true;
        }

        var contentData = Path.Combine(selectedGameFolder, "Content", "Data");
        if (Directory.Exists(contentData))
        {
            dataRoot = contentData;
            gameRoot = Path.Combine(selectedGameFolder, "Content");
            return true;
        }

        if (Directory.Exists(selectedGameFolder))
        {
            dataRoot = selectedGameFolder;
            return true;
        }

        return false;
    }

    private void RebuildMods()
    {
        Mods.Clear();

        var selectedGameFolder = SelectedGameFolder;
        if (string.IsNullOrWhiteSpace(selectedGameFolder))
        {
            StatusMessage = "Ready. Select a game folder, scan for mods, and onboard only the mods you edit.";
            return;
        }

        var install = GameInstalls.FirstOrDefault(x =>
            !x.IsDlc &&
            x.ManagedGame is not null &&
            string.Equals(x.InstallPath, selectedGameFolder, StringComparison.OrdinalIgnoreCase));

        if (install?.ManagedGame is null)
        {
            StatusMessage = "Ready. Scan the selected game folder to onboard mods under management.";
            return;
        }

        var persistedMods = _repository.LoadManagedModsForInstall(selectedGameFolder, install.ManagedGame.Name).ToList();

        var filteredOutByFolder = 0;
        if (TryResolveGameDataRoot(selectedGameFolder, out var selectedDataRoot, out _))
        {
            var folderScopedMods = persistedMods
                .Where(mod => !string.IsNullOrWhiteSpace(mod.PrimaryPlugin))
                .Where(mod => File.Exists(Path.Combine(selectedDataRoot, mod.PrimaryPlugin)))
                .ToList();
            filteredOutByFolder = persistedMods.Count - folderScopedMods.Count;
            persistedMods = folderScopedMods;
        }

        persistedMods = ApplyModSort(persistedMods).ToList();

        var row = 0;
        foreach (var mod in persistedMods)
        {
            Mods.Add(new ModListItem(
                mod.ModName,
                mod.PrimaryPlugin,
                mod.Stage,
                mod.GameName,
                string.Empty,
                string.Empty,
                new SolidColorBrush(Color.Parse(row++ % 2 == 0 ? "#2B2B2B" : "#343434"))));
        }

        StatusMessage = persistedMods.Count == 0
            ? "Ready. Scan the selected game folder to onboard mods under management."
            : filteredOutByFolder > 0
                ? $"Loaded {persistedMods.Count} managed mod(s) present in this folder (filtered {filteredOutByFolder} from other installs)."
                : $"Loaded {persistedMods.Count} managed mod(s) for this game install.";
    }

    private int ImportManagedModsFromSharedCatalog(string repoRoot, string installPath, string gameName)
    {
        var catalog = _catalogService.Load(repoRoot);
        if (catalog.Mods.Count == 0)
        {
            return 0;
        }

        var imported = 0;
        foreach (var entry in catalog.Mods
                     .Where(x => string.Equals(x.GameId, gameName, StringComparison.OrdinalIgnoreCase)))
        {
            var modRepoPath = Path.Combine(repoRoot, entry.SubmodulePath.Replace('/', Path.DirectorySeparatorChar));
            if (!_gitService.IsGitWorkingTree(modRepoPath))
            {
                continue;
            }

            _repository.UpsertManagedModForInstall(
                gameName,
                installPath,
                entry.ModName,
                entry.PrimaryPlugin,
                SharedCatalogService.ToStageDisplayName(entry.StageBranch),
                modRepoPath);
            imported++;
        }

        return imported;
    }

    public void SyncGameFoldersFromInstalls()
    {
        var selected = SelectedGameFolder;
        var settings = _settingsStore.Load();
        var preferred = string.IsNullOrWhiteSpace(settings.LastSelectedGameFolder) ? null : settings.LastSelectedGameFolder;

        GameFolders.Clear();
        foreach (var path in GameInstalls
                     .Where(x => !x.IsDlc)
                     .Select(x => x.InstallPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            GameFolders.Add(path);
        }

        SelectedGameFolder = selected is not null && GameFolders.Contains(selected)
            ? selected
            : preferred is not null && GameFolders.Contains(preferred)
                ? preferred
                : GameFolders.FirstOrDefault();
    }

    private void PersistLastSelectedGameFolder(string? selectedFolder)
    {
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        var settings = _settingsStore.Load();
        if (string.Equals(settings.LastSelectedGameFolder, selectedFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        settings.LastSelectedGameFolder = selectedFolder;
        _settingsStore.Save(settings);
    }


    public bool TryAutoSyncManagedMod(ModListItem mod, out string message)
    {
        if (mod is null)
        {
            message = "no mod selected";
            return false;
        }

        var selectedGameFolder = SelectedGameFolder;
        if (string.IsNullOrWhiteSpace(selectedGameFolder))
        {
            message = "no game folder selected";
            return false;
        }

        var install = GameInstalls.FirstOrDefault(x =>
            !x.IsDlc &&
            x.ManagedGame is not null &&
            string.Equals(x.InstallPath, selectedGameFolder, StringComparison.OrdinalIgnoreCase));

        if (install?.ManagedGame is null)
        {
            message = "selected game folder is not mapped to a managed install";
            return false;
        }

        var record = _repository.LoadManagedModsForInstall(selectedGameFolder, install.ManagedGame.Name)
            .FirstOrDefault(x =>
                string.Equals(x.ModName, mod.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.PrimaryPlugin, mod.PrimaryPlugin, StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            message = $"managed record not found for {mod.Name}";
            return false;
        }

        var stamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var commitMessage = $"autocommit : {stamp}";
        if (!_gitService.TryAutoCommitAndSyncRepository(record.ModRepoPath, commitMessage, out var error))
        {
            message = error;
            return false;
        }

        message = $"synced {mod.Name}";
        return true;
    }

    public bool TryAutoSyncAllManagedMods(out string message)
    {
        var repoPaths = _repository.LoadAllManagedModRepoPaths();
        if (repoPaths.Count == 0)
        {
            message = "no managed mod repos to sync";
            return true;
        }

        var stamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var commitMessage = $"autocommit : {stamp}";
        var synced = 0;
        foreach (var repoPath in repoPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_gitService.TryAutoCommitAndSyncRepository(repoPath, commitMessage, out _))
            {
                synced++;
            }
        }

        message = $"synced {synced}/{repoPaths.Count} managed mod repo(s)";
        return synced == repoPaths.Count;
    }

    public bool TrySyncManagedRepoRoot(out string message)
    {
        var settings = _settingsStore.Load();
        var repoRoot = string.IsNullOrWhiteSpace(settings.RepoRootPath)
            ? ProgramWideSettings.GetDefaultRepoRoot()
            : settings.RepoRootPath;

        if (!_gitService.TrySyncRepoRoot(repoRoot, out var error))
        {
            message = error;
            return false;
        }

        RebuildMods();
        message = $"Repo root synced: {repoRoot}";
        return true;
    }

    public IReadOnlyList<string> GetGameFoldersForGame(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return GameFolders.ToList();
        }

        return GameInstalls
            .Where(x => !x.IsDlc)
            .Where(x => x.ManagedGame is not null)
            .Where(x => string.Equals(x.ManagedGame!.Name, gameName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.InstallPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetAvailableStagesForMod(ModListItem mod)
    {
        var stages = Mods
            .Where(x => string.Equals(x.Name, mod.Name, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.PrimaryPlugin, mod.PrimaryPlugin, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.GameName, mod.GameName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.CurrentStage)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (stages.Count == 0)
        {
            stages.Add(mod.CurrentStage);
        }

        return stages;
    }

    public IReadOnlyList<GameInstallRecord> DiscoverInstallCandidates() =>
        DiscoverInstallCandidatesAsync().GetAwaiter().GetResult();

    public async Task<IReadOnlyList<GameInstallRecord>> DiscoverInstallCandidatesAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var scanners = CreateAvailableScanners();
        if (scanners.Count == 0)
        {
            progress?.Report("Store scanners are only available on Windows.");
            return Array.Empty<GameInstallRecord>();
        }

        var managedGamesSnapshot = ManagedGames.ToList();
        var knownCatalog = _repository.LoadKnownGameCatalog();
        var knownByStoreAppId = knownCatalog
            .Where(x => !string.IsNullOrWhiteSpace(x.StoreAppId))
            .GroupBy(x => x.StoreAppId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var knownByName = knownCatalog
            .GroupBy(x => GameNameNormalization.NormalizeGameName(x.GameName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var lightweightContext = new StoreScanContext
        {
            IncludeVisualAssets = false
        };

        return await Task.Run(async () =>
        {
            var discovered = new List<GameInstallRecord>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var storesByKey = scanners.ToDictionary(x => x.StoreKey, x => x, StringComparer.OrdinalIgnoreCase);

            progress?.Report($"Scanning {scanners.Count} game stores for installs...");

            foreach (var scanner in scanners)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Scanning {GameNameNormalization.ToStoreLabel(scanner.StoreKey)}...");

                StoreScanResult result;
                try
                {
                    result = await scanner.ScanAsync(lightweightContext, ct).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                foreach (var app in result.Apps)
                {
                    var installPath = ResolvePreferredInstallPath(app);

                    if (string.IsNullOrWhiteSpace(installPath))
                    {
                        continue;
                    }

                    var key = $"{GameNameNormalization.ToStoreLabel(app.Id.StoreKey)}|{installPath}";
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var (managedGame, isDlc) = MatchManagedGame(app);
                    app.StoreMetadata.TryGetValue("BaseGameManifestPath", out var manifestPath);
                    discovered.Add(new GameInstallRecord
                    {
                        Manage = managedGame is not null,
                        GameStore = GameNameNormalization.ToStoreLabel(app.Id.StoreKey),
                        StoreAppId = app.Id.StoreAppId,
                        ManagedGame = managedGame,
                        InstallPath = installPath,
                        IsDlc = isDlc,
                        BaseGameManifestPath = manifestPath ?? string.Empty
                    });
                }
            }

            progress?.Report($"Scan complete. Found {discovered.Count} installs across {storesByKey.Count} stores.");

            return (IReadOnlyList<GameInstallRecord>)discovered
                .OrderByDescending(x => x.Manage)
                .ThenBy(x => x.ManagedGame?.Name ?? x.InstallPath)
                .ThenBy(x => x.GameStore)
                .ToList();
        }, ct);

        static string? ResolvePreferredInstallPath(AppInstallSnapshot app)
        {
            var install = app.InstallFolders.InstallFolder?.Path;
            var content = app.InstallFolders.ContentFolder?.Path;
            var data = app.InstallFolders.DataFolder?.Path;

            bool hasData(string? p) => !string.IsNullOrWhiteSpace(p) && Directory.Exists(Path.Combine(p, "Data"));
            bool hasExe(string? p) => !string.IsNullOrWhiteSpace(p) && !string.IsNullOrWhiteSpace(app.ExecutableName) && File.Exists(Path.Combine(p, app.ExecutableName));

            // Prefer content roots when they look like the real game root (Xbox/GamePass pattern).
            if (!string.IsNullOrWhiteSpace(content) && (hasData(content) || hasExe(content)))
                return content;

            if (!string.IsNullOrWhiteSpace(install) && (hasData(install) || hasExe(install)))
                return install;

            if (!string.IsNullOrWhiteSpace(content)) return content;
            if (!string.IsNullOrWhiteSpace(install)) return install;
            return data;
        }

        (ManagedGame? Game, bool IsDlc) MatchManagedGame(AppInstallSnapshot app)
        {
            if (!string.IsNullOrWhiteSpace(app.Id.StoreAppId) && knownByStoreAppId.TryGetValue(app.Id.StoreAppId, out var byAppId))
            {
                if (byAppId.IsDlc)
                {
                    var parent = managedGamesSnapshot.FirstOrDefault(x =>
                        string.Equals(GameNameNormalization.NormalizeGameName(x.Name), GameNameNormalization.NormalizeGameName(byAppId.ParentGameName), StringComparison.OrdinalIgnoreCase));
                    if (parent is not null)
                    {
                        return (parent, true);
                    }
                }
                else
                {
                    var game = managedGamesSnapshot.FirstOrDefault(x =>
                        string.Equals(GameNameNormalization.NormalizeGameName(x.Name), GameNameNormalization.NormalizeGameName(byAppId.GameName), StringComparison.OrdinalIgnoreCase));
                    if (game is not null)
                    {
                        return (game, false);
                    }
                }
            }

            var normalizedDisplayName = GameNameNormalization.NormalizeGameName(app.DisplayName);
            if (knownByName.TryGetValue(normalizedDisplayName, out var byName) && byName.IsDlc)
            {
                var parent = managedGamesSnapshot.FirstOrDefault(x =>
                    string.Equals(GameNameNormalization.NormalizeGameName(x.Name), GameNameNormalization.NormalizeGameName(byName.ParentGameName), StringComparison.OrdinalIgnoreCase));
                if (parent is not null)
                {
                    return (parent, true);
                }
            }

            var knownByStoreId = managedGamesSnapshot.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.StoreId) &&
                string.Equals(x.StoreId, app.Id.StoreAppId, System.StringComparison.OrdinalIgnoreCase));
            if (knownByStoreId is not null)
            {
                return (knownByStoreId, false);
            }

            var knownByExe = managedGamesSnapshot.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Executable) &&
                string.Equals(x.Executable, app.ExecutableName, System.StringComparison.OrdinalIgnoreCase));
            if (knownByExe is not null)
            {
                return (knownByExe, false);
            }

            var knownByDisplay = managedGamesSnapshot.FirstOrDefault(x =>
                string.Equals(GameNameNormalization.NormalizeGameName(x.Name), normalizedDisplayName, System.StringComparison.OrdinalIgnoreCase));
            return (knownByDisplay, false);
        }

    }

    private static IReadOnlyList<IStoreInstallScanner> CreateAvailableScanners()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<IStoreInstallScanner>();
        }

        return
        [
            new XboxInstallScanner(),
            new SteamInstallScanner(),
            new EpicInstallScanner(),
            new GogInstallScanner(),
            new PsnInstallScanner(),
            new BattleNetInstallScanner(),
            new MinecraftInstallScanner(),
            new EaInstallScanner(),
            new OriginInstallScanner(),
            new RockstarInstallScanner()
        ];
    }
}


public sealed record ModDependencyPreview(
    IReadOnlyList<string> PluginFiles,
    IReadOnlyList<string> Ba2Files,
    ModDependencyDiscoveryResult Discovery,
    string ScanRoot,
    string GameRoot);

public sealed record ScanApplyProgress(int CompletedMods, int TotalMods, string Message);
