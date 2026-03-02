using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
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

        _ = progressWindow.ShowDialog(this);
        await Task.Delay(50);

        try
        {
            _viewModel.ApplyScanSelections(selections);
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


    private void RefreshFolders_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.RefreshCurrentFolderData();
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
        var folder = _viewModel.SelectedGameFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            _viewModel.StatusMessage = "Open game folder failed: selected folder is missing.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
            _viewModel.StatusMessage = $"Opened game folder: {folder}";
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

    private async void OpenModWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ModListItem mod })
        {
            var matchingFolders = _viewModel.GetGameFoldersForGame(mod.GameName);
            var activeStages = _viewModel.GetAvailableStagesForMod(mod);
            var modWindow = new ModWindow(mod, matchingFolders, activeStages, _viewModel.SelectedGameFolder);
            await modWindow.ShowDialog(this);
            await HandleModFocusCloseSyncAsync(mod);
            if (!_viewModel.StatusMessage.StartsWith("autocommit :", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.StatusMessage = $"Closed focus window for {mod.Name}.";
            }
        }
    }
}

public sealed class MainWindowViewModel : NotifyBase
{
    private readonly GameSetupRepository _repository = new();
    private readonly ProgramWideSettingsStore _settingsStore = new();
    private readonly ModOnboardingGitService _gitService = new();
    private readonly SharedCatalogService _catalogService = new();

    public ObservableCollection<string> GameFolders { get; } = new();
    public ObservableCollection<string> StageOptions { get; } = new();
    public ObservableCollection<ModListItem> Mods { get; } = new();
    public ObservableCollection<ManagedGame> ManagedGames { get; } = new();
    public ObservableCollection<GameInstallRecord> GameInstalls { get; } = new();

    private static readonly HashSet<string> StarfieldOfficialPluginBaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "starfield",
        "constellation",
        "blueprintshipsstarfield"
    };

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

    public void RefreshCurrentFolderData()
    {
        var selectedGameFolder = SelectedGameFolder;
        if (string.IsNullOrWhiteSpace(selectedGameFolder))
        {
            StatusMessage = "Refresh skipped: no game folder selected.";
            return;
        }

        var existingCount = Mods.Count;
        RebuildMods();
        StatusMessage = $"Refreshed folder data for {selectedGameFolder}. Loaded {Mods.Count} managed mod(s) (previously {existingCount}).";
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

        var dataFolder = Path.Combine(selectedGameFolder, "Data");
        var scanRoot = Directory.Exists(dataFolder) ? dataFolder : selectedGameFolder;
        if (!Directory.Exists(scanRoot))
        {
            StatusMessage = $"Scan failed: game data folder not found at '{scanRoot}'.";
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
            .Where(name => !IsOfficialPluginName(install.ManagedGame.Name, name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .GroupBy(name => Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(name => GetPluginExtensionPriority(Path.GetExtension(name)))
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

    public void ApplyScanSelections(IReadOnlyList<GameFolderStageSelection> selections)
    {
        Mods.Clear();

        if (string.IsNullOrWhiteSpace(SelectedGameFolder))
        {
            StatusMessage = "Scan apply failed: no game folder selected.";
            return;
        }

        var selectedGameFolder = SelectedGameFolder!;

        var install = GameInstalls.FirstOrDefault(x =>
            !x.IsDlc &&
            x.ManagedGame is not null &&
            string.Equals(x.InstallPath, selectedGameFolder, StringComparison.OrdinalIgnoreCase));

        if (install?.ManagedGame is null)
        {
            StatusMessage = "Scan apply failed: selected game folder is not mapped to a managed base game install.";
            return;
        }

        var dataFolder = Path.Combine(selectedGameFolder, "Data");
        var scanRoot = Directory.Exists(dataFolder) ? dataFolder : selectedGameFolder;
        if (!Directory.Exists(scanRoot))
        {
            StatusMessage = $"Scan apply failed: game data folder not found at '{scanRoot}'.";
            return;
        }

        var settings = _settingsStore.Load();
        var repoRoot = string.IsNullOrWhiteSpace(settings.RepoRootPath)
            ? ProgramWideSettings.GetDefaultRepoRoot()
            : settings.RepoRootPath;

        if (!_gitService.HasRequiredGitHubSettings(settings, out var missingSettings))
        {
            StatusMessage = $"Scan apply blocked: configure GitHub settings first ({missingSettings}) in Program Settings.";
            return;
        }

        var created = 0;
        var copiedFiles = 0;
        var skipped = 0;
        var bootstrapRequired = 0;
        var failed = 0;
        var row = 0;
        var bootstrapPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var selection in selections.OrderBy(x => x.PluginName, StringComparer.OrdinalIgnoreCase))
        {
            var sourcePath = Path.Combine(scanRoot, selection.PluginName);
            if (!File.Exists(sourcePath))
            {
                skipped++;
                continue;
            }

            try
            {
                var modRepoRoot = BuildModRepoRoot(repoRoot, install.ManagedGame.Name, selection.ModName, settings.RepoOrganization);
                if (!_gitService.IsGitWorkingTree(modRepoRoot))
                {
                    var bootstrapped = _gitService.TryBootstrapModRepository(
                        settings,
                        repoRoot,
                        install.ManagedGame.Name,
                        selection.ModName,
                        modRepoRoot,
                        out var bootstrapError);
                    if (!bootstrapped)
                    {
                        bootstrapRequired++;
                        bootstrapPaths.Add($"{modRepoRoot} ({bootstrapError})");
                        skipped++;
                        continue;
                    }
                }

                var targetStageBranch = _gitService.ToStageBranch(selection.Stage);
                if (!_gitService.EnsureBranchCheckedOut(modRepoRoot, targetStageBranch, out var branchError))
                {
                    bootstrapRequired++;
                    bootstrapPaths.Add($"{modRepoRoot} ({branchError})");
                    skipped++;
                    continue;
                }

                var stageFolder = Path.Combine(modRepoRoot, "loosefiles", "Data");
                Directory.CreateDirectory(stageFolder);

                var initialFiles = CollectInitialModFiles(scanRoot, selection.ModName, selection.PluginName)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (initialFiles.Count == 0)
                {
                    skipped++;
                    continue;
                }

                foreach (var file in initialFiles)
                {
                    var target = Path.Combine(stageFolder, Path.GetFileName(file));
                    File.Copy(file, target, overwrite: true);
                    copiedFiles++;

                    // Intentionally copy-only for now; link-back is reserved for a later validation milestone.
                }

                var relativeSubmodulePath = Path.Combine(SanitizePathSegment(install.ManagedGame.Name), SanitizePathSegment(selection.ModName))
                    .Replace('\\', '/');
                if (!_gitService.CommitAndPushOnboardingChanges(repoRoot, modRepoRoot, relativeSubmodulePath, targetStageBranch, selection.ModName, out var commitError))
                {
                    bootstrapRequired++;
                    bootstrapPaths.Add($"{modRepoRoot} ({commitError})");
                    skipped++;
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
                var submodulePath = Path.Combine(SanitizePathSegment(install.ManagedGame.Name), SanitizePathSegment(selection.ModName))
                    .Replace('\\', '/');
                _catalogService.UpsertEntry(repoRoot, new SharedCatalogEntry(
                    install.ManagedGame.Name,
                    selection.ModName,
                    selection.PluginName,
                    modRepoName,
                    submodulePath,
                    $"https://github.com/{settings.GitHubAccount}/{modRepoName}.git",
                    targetStageBranch));

                Mods.Add(new ModListItem(
                    selection.ModName,
                    selection.PluginName,
                    selection.Stage,
                    install.ManagedGame.Name,
                    string.Empty,
                    string.Empty,
                    new SolidColorBrush(Color.Parse(row++ % 2 == 0 ? "#2B2B2B" : "#343434"))));
                created++;
            }
            catch
            {
                failed++;
            }
        }

        if (selections.Count == 0)
        {
            StatusMessage = "Scan apply complete. All discovered candidates were ignored.";
            return;
        }

        var bootstrapPreview = bootstrapPaths.Count == 0
            ? string.Empty
            : $" First missing repo: {bootstrapPaths.First()}";

        StatusMessage =
            $"Scan apply complete. Added {created} mod(s); copied {copiedFiles} file(s); skipped {skipped} (local git repo bootstrap needed: {bootstrapRequired}); failed {failed}. Repo root: {repoRoot}. Mod repos were pushed and parent submodule pointers were synced.{bootstrapPreview}";

        RebuildMods();
    }

    private static IEnumerable<string> CollectInitialModFiles(string scanRoot, string modName, string primaryPlugin)
    {
        var pluginExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".esm", ".esp", ".esl" };
        var expectedPluginName = $"{modName}{Path.GetExtension(primaryPlugin)}";

        var pluginCandidates = new[]
        {
            Path.Combine(scanRoot, expectedPluginName),
            Path.Combine(scanRoot, $"{modName}.esm"),
            Path.Combine(scanRoot, $"{modName}.esp"),
            Path.Combine(scanRoot, $"{modName}.esl")
        };

        var pluginFiles = pluginCandidates
            .Where(File.Exists)
            .Where(path => pluginExtensions.Contains(Path.GetExtension(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var archiveCandidates = new[]
        {
            $"{modName} - Main.ba2",
            $"{modName} - Main_xbox.ba2",
            $"{modName} - Textures.ba2",
            $"{modName} - Textures_xbox.ba2"
        };

        var archiveFiles = archiveCandidates
            .Select(name => Path.Combine(scanRoot, name))
            .Where(File.Exists)
            .ToList();

        return pluginFiles
            .Concat(archiveFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static int GetPluginExtensionPriority(string extension)
    {
        if (string.Equals(extension, ".esp", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(extension, ".esm", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(extension, ".esl", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private static bool IsOfficialPluginName(string gameName, string pluginName)
    {
        if (!string.Equals(gameName, "Starfield", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var baseName = NormalizePluginBaseName(Path.GetFileNameWithoutExtension(pluginName));
        return StarfieldOfficialPluginBaseNames.Contains(baseName);
    }

    private static string NormalizePluginBaseName(string? baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return string.Empty;
        }

        var chars = baseName.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static string BuildModRepoRoot(string repoRoot, string gameName, string modName, RepoOrganizationStrategy strategy)
    {
        var safeGameName = SanitizePathSegment(gameName);
        var safeModName = SanitizePathSegment(modName);
        var gameRoot = Path.Combine(repoRoot, safeGameName);

        return strategy switch
        {
            RepoOrganizationStrategy.RepoPerMod => Path.Combine(gameRoot, "mods", safeModName),
            _ => Path.Combine(gameRoot, safeModName)
        };
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Unnamed" : cleaned;
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
        if (persistedMods.Count == 0)
        {
            var settings = _settingsStore.Load();
            var repoRoot = string.IsNullOrWhiteSpace(settings.RepoRootPath)
                ? ProgramWideSettings.GetDefaultRepoRoot()
                : settings.RepoRootPath;
            var imported = ImportManagedModsFromSharedCatalog(repoRoot, selectedGameFolder, install.ManagedGame.Name);
            if (imported > 0)
            {
                persistedMods = _repository.LoadManagedModsForInstall(selectedGameFolder, install.ManagedGame.Name).ToList();
            }
        }

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
            .GroupBy(x => NormalizeGameName(x.GameName), StringComparer.OrdinalIgnoreCase)
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
                progress?.Report($"Scanning {ToStoreLabel(scanner.StoreKey)}...");

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
                    var installPath = app.InstallFolders.InstallFolder?.Path
                        ?? app.InstallFolders.ContentFolder?.Path
                        ?? app.InstallFolders.DataFolder?.Path;

                    if (string.IsNullOrWhiteSpace(installPath))
                    {
                        continue;
                    }

                    var key = $"{ToStoreLabel(app.Id.StoreKey)}|{installPath}";
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var (managedGame, isDlc) = MatchManagedGame(app);
                    app.StoreMetadata.TryGetValue("BaseGameManifestPath", out var manifestPath);
                    discovered.Add(new GameInstallRecord
                    {
                        Manage = managedGame is not null,
                        GameStore = ToStoreLabel(app.Id.StoreKey),
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

        (ManagedGame? Game, bool IsDlc) MatchManagedGame(AppInstallSnapshot app)
        {
            if (!string.IsNullOrWhiteSpace(app.Id.StoreAppId) && knownByStoreAppId.TryGetValue(app.Id.StoreAppId, out var byAppId))
            {
                if (byAppId.IsDlc)
                {
                    var parent = managedGamesSnapshot.FirstOrDefault(x =>
                        string.Equals(NormalizeGameName(x.Name), NormalizeGameName(byAppId.ParentGameName), StringComparison.OrdinalIgnoreCase));
                    if (parent is not null)
                    {
                        return (parent, true);
                    }
                }
                else
                {
                    var game = managedGamesSnapshot.FirstOrDefault(x =>
                        string.Equals(NormalizeGameName(x.Name), NormalizeGameName(byAppId.GameName), StringComparison.OrdinalIgnoreCase));
                    if (game is not null)
                    {
                        return (game, false);
                    }
                }
            }

            var normalizedDisplayName = NormalizeGameName(app.DisplayName);
            if (knownByName.TryGetValue(normalizedDisplayName, out var byName) && byName.IsDlc)
            {
                var parent = managedGamesSnapshot.FirstOrDefault(x =>
                    string.Equals(NormalizeGameName(x.Name), NormalizeGameName(byName.ParentGameName), StringComparison.OrdinalIgnoreCase));
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
                string.Equals(NormalizeGameName(x.Name), normalizedDisplayName, System.StringComparison.OrdinalIgnoreCase));
            return (knownByDisplay, false);
        }

        static string NormalizeGameName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            const string pcSuffix = " (PC)";
            return name.EndsWith(pcSuffix, StringComparison.OrdinalIgnoreCase)
                ? name[..^pcSuffix.Length].TrimEnd()
                : name.Trim();
        }

        static string ToStoreLabel(string storeKey) => storeKey.ToLowerInvariant() switch
        {
            StoreKeys.BattleNet => "Battle.net",
            StoreKeys.Ea => "EA",
            StoreKeys.Gog => "GOG",
            StoreKeys.Psn => "PSN",
            StoreKeys.Xbox => "Game Pass",
            _ => string.IsNullOrWhiteSpace(storeKey) ? "Unknown" : char.ToUpperInvariant(storeKey[0]) + storeKey[1..]
        };
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

public sealed class ModListItem
{
    public ModListItem(string name, string primaryPlugin, string currentStage, string gameName, string bethesdaId, string nexusId, IBrush rowBackground)
    {
        Name = name;
        PrimaryPlugin = primaryPlugin;
        CurrentStage = currentStage;
        GameName = gameName;
        BethesdaId = bethesdaId;
        NexusId = nexusId;
        RowBackground = rowBackground;
    }

    public string Name { get; }
    public string PrimaryPlugin { get; }
    public string CurrentStage { get; }
    public string GameName { get; }
    public string BethesdaId { get; }
    public string NexusId { get; }
    public IBrush RowBackground { get; }
}
