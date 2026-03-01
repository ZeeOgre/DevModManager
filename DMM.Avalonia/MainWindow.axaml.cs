using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.IO;
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
    private readonly GameSetupRepository _repository = new();

    public ObservableCollection<string> GameFolders { get; } = new();
    public ObservableCollection<string> StageOptions { get; } = new();
    public ObservableCollection<ModListItem> Mods { get; } = new();
    public ObservableCollection<ManagedGame> ManagedGames { get; } = new();
    public ObservableCollection<GameInstallRecord> GameInstalls { get; } = new();

    private string? _selectedGameFolder;
    public string? SelectedGameFolder
    {
        get => _selectedGameFolder;
        set
        {
            if (SetField(ref _selectedGameFolder, value))
            {
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

    private void RebuildMods()
    {
        Mods.Clear();

        var baseInstalls = GameInstalls
            .Where(x => !x.IsDlc)
            .Where(x => x.ManagedGame is not null)
            .Where(x => string.IsNullOrWhiteSpace(SelectedGameFolder) || string.Equals(x.InstallPath, SelectedGameFolder, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var row = 0;
        foreach (var install in baseInstalls)
        {
            foreach (var builtin in _repository.LoadKnownPluginsForGame(install.ManagedGame!.Name))
            {
                if (!PluginExists(install.InstallPath, builtin.PluginName))
                {
                    continue;
                }

                Mods.Add(new ModListItem(
                    builtin.DisplayName,
                    builtin.PluginName,
                    "BASE",
                    string.Empty,
                    string.Empty,
                    new SolidColorBrush(Color.Parse(row++ % 2 == 0 ? "#2B2B2B" : "#343434"))));
            }
        }

        if (Mods.Count > 0)
        {
            StatusMessage = $"Loaded {Mods.Count} built-in Bethesda mod entries from managed installs.";
            return;
        }

        Mods.Add(new ModListItem("ZO_AIOGamePlayTweaks", "ZO_AIOGamePlayTweaks.esp", "DEV", "f7dc7ac6...", "345221", new SolidColorBrush(Color.Parse("#2B2B2B"))));
        Mods.Add(new ModListItem("ZO_DenserOutposts", "ZO_DenserOutposts.esm", "RELEASE", "c5bbdd20...", "817744", new SolidColorBrush(Color.Parse("#343434"))));
        Mods.Add(new ModListItem("ZO_HandScannerTweaks", "ZO_HandScannerTweaks.esl", "TEST", "3f102ab3...", "593188", new SolidColorBrush(Color.Parse("#2B2B2B"))));
        Mods.Add(new ModListItem("WT_SmartDoc", "WT_SmartDoc.esp", "DEV", "2120ee1a...", "772843", new SolidColorBrush(Color.Parse("#343434"))));
        Mods.Add(new ModListItem("ZO_StarUIFix", "ZO_StarUIFix.esp", "DEV", "a220ce51...", "300194", new SolidColorBrush(Color.Parse("#2B2B2B"))));
    }

    private static bool PluginExists(string installPath, string pluginFileName)
    {
        var inRoot = Path.Combine(installPath, pluginFileName);
        if (File.Exists(inRoot))
        {
            return true;
        }

        var inData = Path.Combine(installPath, "Data", pluginFileName);
        return File.Exists(inData);
    }

    public void SyncGameFoldersFromInstalls()
    {
        var selected = SelectedGameFolder;
        GameFolders.Clear();
        foreach (var path in GameInstalls
                     .Where(x => !x.IsDlc)
                     .Select(x => x.InstallPath)
                     .Distinct())
        {
            GameFolders.Add(path);
        }

        SelectedGameFolder = selected is not null && GameFolders.Contains(selected)
            ? selected
            : GameFolders.FirstOrDefault();
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

                    var managedGame = MatchManagedGame(app);
                    discovered.Add(new GameInstallRecord
                    {
                        Manage = managedGame is not null,
                        GameStore = ToStoreLabel(app.Id.StoreKey),
                        StoreAppId = app.Id.StoreAppId,
                        ManagedGame = managedGame,
                        InstallPath = installPath
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

        ManagedGame? MatchManagedGame(AppInstallSnapshot app)
        {
            var normalizedDisplayName = NormalizeGameName(app.DisplayName);

            var knownByStoreId = managedGamesSnapshot.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.StoreId) &&
                string.Equals(x.StoreId, app.Id.StoreAppId, System.StringComparison.OrdinalIgnoreCase));
            if (knownByStoreId is not null)
            {
                return knownByStoreId;
            }

            var knownByExe = managedGamesSnapshot.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Executable) &&
                string.Equals(x.Executable, app.ExecutableName, System.StringComparison.OrdinalIgnoreCase));
            if (knownByExe is not null)
            {
                return knownByExe;
            }

            return managedGamesSnapshot.FirstOrDefault(x =>
                string.Equals(NormalizeGameName(x.Name), normalizedDisplayName, System.StringComparison.OrdinalIgnoreCase));
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
