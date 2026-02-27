using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
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

    private void ScanGameFolder_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.StatusMessage =
            "Scan requested: discovering mods from game folder and inferring default branch initialization rules.";

    private async void OpenHelp_Click(object? sender, RoutedEventArgs e)
    {
        var helpWindow = HelpWindow.ForSection("Main");
        await helpWindow.ShowDialog(this);
        _viewModel.StatusMessage = "Help viewed.";
    }

    private async void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        var wizard = new GameInstallWizardWindow(_viewModel, isFirstRun: false);
        await wizard.ShowDialog(this);
        _viewModel.SyncGameFoldersFromInstalls();
        _viewModel.StatusMessage = "Settings: scan for new games completed.";
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
    public ObservableCollection<ManagedGame> ManagedGames { get; } = new();
    public ObservableCollection<GameInstallRecord> GameInstalls { get; } = new();

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

        vm.ManagedGames.Add(new ManagedGame { Name = "Starfield", Executable = "Starfield.exe", StoreId = "1716740" });
        vm.ManagedGames.Add(new ManagedGame { Name = "Fallout 4", Executable = "Fallout4.exe", StoreId = "377160" });
        vm.ManagedGames.Add(new ManagedGame { Name = "Skyrim Special Edition", Executable = "SkyrimSE.exe", StoreId = "489830" });

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

    public void SyncGameFoldersFromInstalls()
    {
        var selected = SelectedGameFolder;
        GameFolders.Clear();
        foreach (var path in GameInstalls.Select(x => x.InstallPath).Distinct())
        {
            GameFolders.Add(path);
        }

        SelectedGameFolder = selected is not null && GameFolders.Contains(selected)
            ? selected
            : GameFolders.FirstOrDefault();
    }

    public async Task<IReadOnlyList<GameInstallRecord>> DiscoverInstallCandidatesAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var scanners = CreateAvailableScanners();
        if (scanners.Count == 0)
        {
            progress?.Report("Store scanners are only available on Windows.");
            return Array.Empty<GameInstallRecord>();
        }

        var discovered = new List<GameInstallRecord>();
        var seen = new HashSet<(string Store, string Path)>(StringComparer.OrdinalIgnoreCase);
        var storesByKey = scanners.ToDictionary(x => x.StoreKey, x => x, StringComparer.OrdinalIgnoreCase);

        progress?.Report($"Scanning {scanners.Count} game stores for installs...");

        foreach (var scanner in scanners)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Scanning {ToStoreLabel(scanner.StoreKey)}...");

            StoreScanResult result;
            try
            {
                result = await scanner.ScanAsync(new StoreScanContext(), ct);
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

                var key = (ToStoreLabel(app.Id.StoreKey), installPath);
                if (!seen.Add(key))
                {
                    continue;
                }

                var managedGame = MatchManagedGame(app);
                discovered.Add(new GameInstallRecord
                {
                    Manage = managedGame is not null,
                    GameStore = ToStoreLabel(app.Id.StoreKey),
                    ManagedGame = managedGame,
                    InstallPath = installPath
                });
            }
        }

        progress?.Report($"Scan complete. Found {discovered.Count} installs across {storesByKey.Count} stores.");

        return discovered
            .OrderByDescending(x => x.Manage)
            .ThenBy(x => x.ManagedGame?.Name ?? x.InstallPath)
            .ThenBy(x => x.GameStore)
            .ToList();

        ManagedGame? MatchManagedGame(AppInstallSnapshot app)
        {
            var knownByStoreId = ManagedGames.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.StoreId) &&
                string.Equals(x.StoreId, app.Id.StoreAppId, System.StringComparison.OrdinalIgnoreCase));
            if (knownByStoreId is not null)
            {
                return knownByStoreId;
            }

            var knownByExe = ManagedGames.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Executable) &&
                string.Equals(x.Executable, app.ExecutableName, System.StringComparison.OrdinalIgnoreCase));
            if (knownByExe is not null)
            {
                return knownByExe;
            }

            return ManagedGames.FirstOrDefault(x =>
                string.Equals(x.Name, app.DisplayName, System.StringComparison.OrdinalIgnoreCase));
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
