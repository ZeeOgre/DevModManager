using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
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
using Microsoft.Data.Sqlite;

namespace DMM.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = MainWindowViewModel.CreateSample();
        DataContext = _viewModel;
        Opened += MainWindow_Opened;
    }

    private async void MainWindow_Opened(object? sender, System.EventArgs e)
    {
        if (_viewModel.GameInstalls.Count > 0)
        {
            return;
        }

        var wizard = new GameInstallWizardWindow(_viewModel, isFirstRun: true);
        await wizard.ShowDialog(this);
        _viewModel.SyncGameFoldersFromInstalls();
        _viewModel.StatusMessage = _viewModel.GameInstalls.Count > 0
            ? $"First-run game setup completed. Added {_viewModel.GameInstalls.Count} game install(s)."
            : "First-run setup closed without selecting game installs.";
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

        _viewModel.ApplyScanSelections(result.SelectedMods);
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
            _viewModel.StatusMessage = "Settings closed.";
            return;
        }

        var wizard = new GameInstallWizardWindow(_viewModel, isFirstRun: false);
        await wizard.ShowDialog(this);
        _viewModel.SyncGameFoldersFromInstalls();
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
        _viewModel.StatusMessage = "Git control: sync requested.";

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
            _viewModel.StatusMessage = $"Closed focus window for {mod.Name}.";
        }
    }
}

public sealed class MainWindowViewModel : NotifyBase
{
    private readonly GameSetupRepository _repository = new();
    private readonly ProgramWideSettingsStore _settingsStore = new();

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

    public GameFolderScanResult ScanSelectedGameFolderForMods()
    {
        if (string.IsNullOrWhiteSpace(SelectedGameFolder))
        {
            StatusMessage = "Scan failed: no game folder selected.";
            return GameFolderScanResult.Failed();
        }

        var install = GameInstalls.FirstOrDefault(x =>
            !x.IsDlc &&
            x.ManagedGame is not null &&
            string.Equals(x.InstallPath, SelectedGameFolder, StringComparison.OrdinalIgnoreCase));

        if (install?.ManagedGame is null)
        {
            StatusMessage = "Scan failed: selected game folder is not mapped to a managed base game install.";
            return GameFolderScanResult.Failed();
        }

        var dataFolder = Path.Combine(SelectedGameFolder, "Data");
        var scanRoot = Directory.Exists(dataFolder) ? dataFolder : SelectedGameFolder;
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

        var install = GameInstalls.FirstOrDefault(x =>
            !x.IsDlc &&
            x.ManagedGame is not null &&
            string.Equals(x.InstallPath, SelectedGameFolder, StringComparison.OrdinalIgnoreCase));

        if (install?.ManagedGame is null)
        {
            StatusMessage = "Scan apply failed: selected game folder is not mapped to a managed base game install.";
            return;
        }

        var dataFolder = Path.Combine(SelectedGameFolder, "Data");
        var scanRoot = Directory.Exists(dataFolder) ? dataFolder : SelectedGameFolder;
        if (!Directory.Exists(scanRoot))
        {
            StatusMessage = $"Scan apply failed: game data folder not found at '{scanRoot}'.";
            return;
        }

        var settings = _settingsStore.Load();
        var repoRoot = string.IsNullOrWhiteSpace(settings.RepoRootPath)
            ? ProgramWideSettings.GetDefaultRepoRoot()
            : settings.RepoRootPath;

        var created = 0;
        var copiedFiles = 0;
        var skipped = 0;
        var failed = 0;
        var row = 0;

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

        StatusMessage =
            $"Scan apply complete. Added {created} mod(s); copied {copiedFiles} file(s); skipped {skipped}; failed {failed}. Repo root: {repoRoot}";
    }

    private static IEnumerable<string> CollectInitialModFiles(string scanRoot, string modName, string primaryPlugin)
    {
        var normalizedModName = NormalizePluginBaseName(modName);
        var primaryPath = Path.Combine(scanRoot, primaryPlugin);

        var pluginFiles = Directory.EnumerateFiles(scanRoot, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var ext = Path.GetExtension(path);
                return string.Equals(ext, ".esm", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ext, ".esp", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ext, ".esl", StringComparison.OrdinalIgnoreCase);
            })
            .Where(path => NormalizePluginBaseName(Path.GetFileNameWithoutExtension(path)).Contains(normalizedModName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (File.Exists(primaryPath) && !pluginFiles.Contains(primaryPath, StringComparer.OrdinalIgnoreCase))
        {
            pluginFiles.Add(primaryPath);
        }

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

        StatusMessage = string.IsNullOrWhiteSpace(SelectedGameFolder)
            ? "Ready. Select a game folder, scan for mods, and onboard only the mods you edit."
            : "Ready. Scan the selected game folder to onboard mods under management.";
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

internal sealed class GameSetupRepository
{
    private readonly DatabaseManager _database = new();

    public IReadOnlyList<ManagedGame> LoadManagedGames()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.Name,
                   COALESCE(g.Executable, ''),
                   COALESCE((
                       SELECT gsa.StoreAppId
                       FROM GameStoreApp gsa
                       JOIN GameSource gs ON gs.id = gsa.GameSourceId
                       WHERE gsa.GameId = g.id
                         AND gs.Name = 'Steam'
                       ORDER BY gsa.id
                       LIMIT 1
                   ), '')
            FROM Game g
            WHERE g.IsDlc = 0
            ORDER BY g.Name
            """;

        var games = new List<ManagedGame>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            games.Add(new ManagedGame
            {
                Name = name,
                Executable = reader.GetString(1),
                StoreId = reader.GetString(2)
            });
        }

        return games;
    }

    public IReadOnlyList<KnownGameCatalogRecord> LoadKnownGameCatalog()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.Name,
                   g.IsDlc,
                   parent.Name,
                   COALESCE(gsa.StoreAppId, '')
            FROM Game g
            LEFT JOIN Game parent ON parent.id = g.ParentGameId
            LEFT JOIN GameStoreApp gsa ON gsa.GameId = g.id
            """;

        var records = new List<KnownGameCatalogRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new KnownGameCatalogRecord(
                reader.GetString(0),
                reader.GetInt64(1) == 1,
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3)));
        }

        return records;
    }

    public IReadOnlyList<KnownPluginRecord> LoadKnownPluginsForGame(string gameName)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kp.DisplayName, kp.PluginName, kp.IsBaseGame, kp.IsDlc
            FROM GameKnownPlugin kp
            JOIN Game g ON g.id = kp.GameId
            WHERE g.Name = $gameName
            ORDER BY kp.PluginName
            """;
        command.Parameters.AddWithValue("$gameName", gameName);

        var plugins = new List<KnownPluginRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            plugins.Add(new KnownPluginRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2) == 1,
                reader.GetInt64(3) == 1));
        }

        return plugins;
    }

    public IReadOnlyList<KnownPluginRecord> LoadKnownPluginsForGameIncludingDlc(string gameName)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kp.DisplayName, kp.PluginName, kp.IsBaseGame, kp.IsDlc
            FROM GameKnownPlugin kp
            JOIN Game g ON g.id = kp.GameId
            LEFT JOIN Game parent ON parent.id = g.ParentGameId
            WHERE g.Name = $gameName OR parent.Name = $gameName
            ORDER BY kp.PluginName
            """;
        command.Parameters.AddWithValue("$gameName", gameName);

        var plugins = new List<KnownPluginRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            plugins.Add(new KnownPluginRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2) == 1,
                reader.GetInt64(3) == 1));
        }

        return plugins;
    }

    public IReadOnlyList<GameInstallRecord> LoadManagedInstalls(IEnumerable<ManagedGame> managedGames)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(gs.Name, ''), COALESCE(g.Name, ''), COALESCE(gsi.StoreAppId, ''), COALESCE(f.Path, ''),
                   CASE WHEN EXISTS (
                       SELECT 1 FROM GameStoreProductLink l
                       WHERE l.ChildInstallId = gsi.id AND l.LinkType = 'DLC'
                   ) THEN 1 ELSE 0 END AS IsDlc
            FROM GameStoreInstall gsi
            LEFT JOIN GameStoreRoot gsr ON gsr.id = gsi.GameStoreRootId
            LEFT JOIN GameSource gs ON gs.id = gsr.GameSourceId
            LEFT JOIN Game g ON g.id = gsi.GameId
            LEFT JOIN Folders f ON f.id = gsi.InstallFolderId
            ORDER BY gsi.LastSeenDT DESC
            """;

        var managedByName = managedGames.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var installs = new List<GameInstallRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var gameName = reader.GetString(1);
            managedByName.TryGetValue(gameName, out var game);
            installs.Add(new GameInstallRecord
            {
                Manage = true,
                GameStore = reader.GetString(0),
                ManagedGame = game,
                StoreAppId = reader.GetString(2),
                InstallPath = reader.GetString(3),
                IsDlc = reader.GetInt64(4) == 1
            });
        }

        return installs;
    }

    public void UpsertManagedGame(ManagedGame game)
    {
        using var connection = _database.OpenConnection();

        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT id FROM Game WHERE Name = $name LIMIT 1";
        exists.Parameters.AddWithValue("$name", game.Name);
        var existingId = exists.ExecuteScalar();

        using var command = connection.CreateCommand();
        if (existingId is null)
        {
            command.CommandText = "INSERT INTO Game (Name, Executable) VALUES ($name, $exe)";
        }
        else
        {
            command.CommandText = "UPDATE Game SET Executable = $exe WHERE id = $id";
            command.Parameters.AddWithValue("$id", (long)existingId);
        }

        command.Parameters.AddWithValue("$name", game.Name);
        command.Parameters.AddWithValue("$exe", game.Executable);
        command.ExecuteNonQuery();
    }

    public void ReplaceManagedInstalls(IReadOnlyList<GameInstallRecord> installs, IReadOnlyCollection<ManagedGame> managedGames)
    {
        using var connection = _database.OpenConnection();
        using var tx = connection.BeginTransaction();

        using (var clear = connection.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM GameStoreInstall";
            clear.ExecuteNonQuery();
        }

        var gameIdLookup = LoadGameIdLookup(connection, tx);
        foreach (var game in managedGames)
        {
            EnsureGameId(connection, tx, gameIdLookup, game);
        }

        var folderTypeId = EnsureFolderType(connection, tx, "GameInstall");
        var folderRoleId = EnsureFolderRole(connection, tx, "GameInstall");
        var fileStorageKindId = EnsureFileStorageKind(connection, tx, "Primary", "Game/discovered file on disk");

        foreach (var install in installs)
        {
            var installFolderId = EnsureFolder(connection, tx, install.InstallPath, folderTypeId, folderRoleId);
            var rootPath = Path.GetPathRoot(install.InstallPath) ?? install.InstallPath;
            var rootFolderId = EnsureFolder(connection, tx, rootPath, folderTypeId, folderRoleId);
            var sourceId = EnsureGameSource(connection, tx, install.GameStore);
            var rootId = EnsureStoreRoot(connection, tx, sourceId, rootFolderId);

            var storeAppId = !string.IsNullOrWhiteSpace(install.StoreAppId)
                ? install.StoreAppId
                : !string.IsNullOrWhiteSpace(install.ManagedGame?.StoreId)
                    ? install.ManagedGame.StoreId
                    : $"custom:{install.InstallPath}";

            long? gameId = EnsureGameId(connection, tx, gameIdLookup, install.ManagedGame);

            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO GameStoreInstall (
                    GameStoreRootId, InstallFolderId, GameId, StoreAppId, DisplayName, ExecutableName, LastSeenDT)
                VALUES ($rootId, $installFolderId, $gameId, $storeAppId, $displayName, $exe, $now)
                """;
            cmd.Parameters.AddWithValue("$rootId", rootId);
            cmd.Parameters.AddWithValue("$installFolderId", installFolderId);
            cmd.Parameters.AddWithValue("$gameId", gameId.HasValue ? gameId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$storeAppId", storeAppId);
            cmd.Parameters.AddWithValue("$displayName", install.ManagedGame?.Name ?? "Unknown");
            cmd.Parameters.AddWithValue("$exe", install.ManagedGame?.Executable ?? string.Empty);
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();

            var childInstallId = ReadLastInsertRowId(connection, tx);
            PersistInstallManifestFiles(connection, tx, childInstallId, gameId, fileStorageKindId, folderTypeId, folderRoleId, install);

            if (install.IsDlc && !string.IsNullOrWhiteSpace(install.ManagedGame?.StoreId))
            {
                using var link = connection.CreateCommand();
                link.Transaction = tx;
                link.CommandText = """
                    INSERT OR IGNORE INTO GameStoreProductLink (
                        ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType)
                    VALUES ($childInstallId, $parentGameSourceId, $parentStoreAppId, 'DLC')
                    """;
                link.Parameters.AddWithValue("$childInstallId", childInstallId);
                link.Parameters.AddWithValue("$parentGameSourceId", sourceId);
                link.Parameters.AddWithValue("$parentStoreAppId", install.ManagedGame.StoreId);
                link.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    private static long EnsureFileStorageKind(SqliteConnection connection, SqliteTransaction tx, string name, string description)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM FileStorageKind WHERE Name = $name LIMIT 1";
        select.Parameters.AddWithValue("$name", name);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO FileStorageKind (Name, Description) VALUES ($name, $description)";
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$description", description);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static void PersistInstallManifestFiles(
        SqliteConnection connection,
        SqliteTransaction tx,
        long installId,
        long? gameId,
        long fileStorageKindId,
        long folderTypeId,
        long folderRoleId,
        GameInstallRecord install)
    {
        if (string.IsNullOrWhiteSpace(install.BaseGameManifestPath) || !File.Exists(install.BaseGameManifestPath))
        {
            return;
        }

        using var stream = File.OpenRead(install.BaseGameManifestPath);
        using var doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.TryGetProperty("Files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var fileEntry in files.EnumerateArray())
        {
            if (!fileEntry.TryGetProperty("RelativePath", out var relativePathElement))
            {
                continue;
            }

            var relativePath = relativePathElement.GetString();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var extension = Path.GetExtension(relativePath);
            if (!IsKnownGameDataExtension(extension))
            {
                continue;
            }

            var fileName = Path.GetFileName(relativePath);
            var size = fileEntry.TryGetProperty("SizeBytes", out var sizeElement) ? sizeElement.GetInt64() : 0L;
            var dtStamp = fileEntry.TryGetProperty("LastWriteUtc", out var lastWriteElement) &&
                          lastWriteElement.ValueKind == JsonValueKind.String &&
                          DateTimeOffset.TryParse(lastWriteElement.GetString(), out var parsed)
                ? parsed
                : DateTimeOffset.UtcNow;

            var relativeFolderPath = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
            var relativeFolderId = EnsureRelativeFolderId(connection, tx, relativeFolderPath, folderTypeId, folderRoleId);
            var fileTypeId = TryFindFileTypeId(connection, tx, extension);
            var fileInfoId = EnsureManifestFileInfo(connection, tx, fileName, size, dtStamp, gameId, fileTypeId, relativeFolderId, fileStorageKindId);

            using var insertLink = connection.CreateCommand();
            insertLink.Transaction = tx;
            insertLink.CommandText = """
                INSERT OR IGNORE INTO GameStoreInstallFile (
                    InstallId, FileInfoId, RelativePath, FileRole, IsPresentOnDisk, LastValidatedDT)
                VALUES ($installId, $fileInfoId, $relativePath, 'Reference', 1, $lastValidated)
                """;
            insertLink.Parameters.AddWithValue("$installId", installId);
            insertLink.Parameters.AddWithValue("$fileInfoId", fileInfoId);
            insertLink.Parameters.AddWithValue("$relativePath", relativePath.Replace('\\', '/'));
            insertLink.Parameters.AddWithValue("$lastValidated", dtStamp.ToString("O"));
            insertLink.ExecuteNonQuery();
        }
    }

    private static long? EnsureRelativeFolderId(
        SqliteConnection connection,
        SqliteTransaction tx,
        string? relativeFolderPath,
        long folderTypeId,
        long folderRoleId)
    {
        if (string.IsNullOrWhiteSpace(relativeFolderPath) || relativeFolderPath == ".")
        {
            return null;
        }

        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM Folders WHERE Path = $path LIMIT 1";
        select.Parameters.AddWithValue("$path", relativeFolderPath);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO Folders (Path, FolderTypeId, FolderRoleId) VALUES ($path, $folderTypeId, $folderRoleId)";
        insert.Parameters.AddWithValue("$path", relativeFolderPath);
        insert.Parameters.AddWithValue("$folderTypeId", folderTypeId);
        insert.Parameters.AddWithValue("$folderRoleId", folderRoleId);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static bool IsKnownGameDataExtension(string? extension)
        => extension is not null && (
            extension.Equals(".esm", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".esl", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".esp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ba2", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bsa", StringComparison.OrdinalIgnoreCase));

    private static long? TryFindFileTypeId(SqliteConnection connection, SqliteTransaction tx, string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM FileType WHERE LOWER(FileExtension) = $ext LIMIT 1";
        cmd.Parameters.AddWithValue("$ext", extension.ToLowerInvariant());
        var result = cmd.ExecuteScalar();
        return result is long id ? id : null;
    }

    private static long EnsureManifestFileInfo(
        SqliteConnection connection,
        SqliteTransaction tx,
        string name,
        long size,
        DateTimeOffset dtStamp,
        long? gameId,
        long? fileTypeId,
        long? relativeFolderId,
        long fileStorageKindId)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = """
            SELECT id FROM FileInfo
            WHERE Name = $name
              AND Size = $size
              AND IFNULL(GameId, 0) = IFNULL($gameId, 0)
              AND IFNULL(FileTypeId, 0) = IFNULL($fileTypeId, 0)
              AND IFNULL(RelativeFolderId, 0) = IFNULL($relativeFolderId, 0)
            LIMIT 1
            """;
        select.Parameters.AddWithValue("$name", name);
        select.Parameters.AddWithValue("$size", size);
        select.Parameters.AddWithValue("$gameId", gameId.HasValue ? gameId.Value : DBNull.Value);
        select.Parameters.AddWithValue("$fileTypeId", fileTypeId.HasValue ? fileTypeId.Value : DBNull.Value);
        select.Parameters.AddWithValue("$relativeFolderId", relativeFolderId.HasValue ? relativeFolderId.Value : DBNull.Value);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO FileInfo (Name, DTStamp, Size, GameId, FileTypeId, RelativeFolderId, FileStorageKindId)
            VALUES ($name, $dtStamp, $size, $gameId, $fileTypeId, $relativeFolderId, $fileStorageKindId)
            """;
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$dtStamp", dtStamp.ToString("O"));
        insert.Parameters.AddWithValue("$size", size);
        insert.Parameters.AddWithValue("$gameId", gameId.HasValue ? gameId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("$fileTypeId", fileTypeId.HasValue ? fileTypeId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("$relativeFolderId", relativeFolderId.HasValue ? relativeFolderId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("$fileStorageKindId", fileStorageKindId);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long? EnsureGameId(
        SqliteConnection connection,
        SqliteTransaction tx,
        IDictionary<string, long> gameIdLookup,
        ManagedGame? game)
    {
        if (game is null || string.IsNullOrWhiteSpace(game.Name))
        {
            return null;
        }

        if (gameIdLookup.TryGetValue(game.Name, out var existingId))
        {
            return existingId;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO Game (Name, Executable) VALUES ($name, $exe)";
        insert.Parameters.AddWithValue("$name", game.Name);
        insert.Parameters.AddWithValue("$exe", game.Executable ?? string.Empty);
        insert.ExecuteNonQuery();

        var createdId = ReadLastInsertRowId(connection, tx);
        gameIdLookup[game.Name] = createdId;
        return createdId;
    }

    private static Dictionary<string, long> LoadGameIdLookup(SqliteConnection connection, SqliteTransaction tx)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id, Name FROM Game";
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(1)] = reader.GetInt64(0);
        }
        return result;
    }

    private static long EnsureFolderType(SqliteConnection connection, SqliteTransaction tx, string name)
        => EnsureByName(connection, tx, "FolderType", name);

    private static long EnsureFolderRole(SqliteConnection connection, SqliteTransaction tx, string name)
        => EnsureByName(connection, tx, "FolderRole", name);

    private static long EnsureByName(SqliteConnection connection, SqliteTransaction tx, string tableName, string name)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = $"SELECT id FROM {tableName} WHERE Name = $name LIMIT 1";
        select.Parameters.AddWithValue("$name", name);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = $"INSERT INTO {tableName} (Name) VALUES ($name)";
        insert.Parameters.AddWithValue("$name", name);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long EnsureFolder(SqliteConnection connection, SqliteTransaction tx, string path, long folderTypeId, long folderRoleId)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM Folders WHERE Path = $path LIMIT 1";
        select.Parameters.AddWithValue("$path", path);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO Folders (Path, FolderTypeId, FolderRoleId) VALUES ($path, $typeId, $roleId)";
        insert.Parameters.AddWithValue("$path", path);
        insert.Parameters.AddWithValue("$typeId", folderTypeId);
        insert.Parameters.AddWithValue("$roleId", folderRoleId);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long EnsureGameSource(SqliteConnection connection, SqliteTransaction tx, string store)
    {
        var sourceName = store switch
        {
            "Game Pass" => "GamePass",
            "GOG" => "GoG",
            _ => store
        };

        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM GameSource WHERE Name = $name LIMIT 1";
        select.Parameters.AddWithValue("$name", sourceName);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO GameSource (Name) VALUES ($name)";
        insert.Parameters.AddWithValue("$name", sourceName);
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long EnsureStoreRoot(SqliteConnection connection, SqliteTransaction tx, long gameSourceId, long rootFolderId)
    {
        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT id FROM GameStoreRoot WHERE GameSourceId = $sourceId AND RootFolderId = $folderId AND RootType = 'Library' LIMIT 1";
        select.Parameters.AddWithValue("$sourceId", gameSourceId);
        select.Parameters.AddWithValue("$folderId", rootFolderId);
        var existing = select.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO GameStoreRoot (GameSourceId, RootFolderId, RootType, LastSeenDT) VALUES ($sourceId, $folderId, 'Library', $now)";
        insert.Parameters.AddWithValue("$sourceId", gameSourceId);
        insert.Parameters.AddWithValue("$folderId", rootFolderId);
        insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
        return ReadLastInsertRowId(connection, tx);
    }

    private static long ReadLastInsertRowId(SqliteConnection connection, SqliteTransaction tx)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}

internal sealed record KnownPluginRecord(string DisplayName, string PluginName, bool IsBaseGame, bool IsDlc);
internal sealed record KnownGameCatalogRecord(string GameName, bool IsDlc, string? ParentGameName, string StoreAppId);

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
