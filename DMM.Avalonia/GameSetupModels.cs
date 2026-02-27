using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DMM.Avalonia;

public sealed class ManagedGame : NotifyBase
{
    private string _name = string.Empty;
    private string _executable = string.Empty;
    private string _storeId = string.Empty;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Executable
    {
        get => _executable;
        set => SetField(ref _executable, value);
    }

    public string StoreId
    {
        get => _storeId;
        set => SetField(ref _storeId, value);
    }

    public override string ToString() => Name;
}

public sealed class GameInstallRecord : NotifyBase
{
    private bool _manage;
    private string _gameStore = string.Empty;
    private string _installPath = string.Empty;
    private ManagedGame? _managedGame;

    public bool Manage
    {
        get => _manage;
        set => SetField(ref _manage, value);
    }

    public string GameStore
    {
        get => _gameStore;
        set => SetField(ref _gameStore, value);
    }

    public string InstallPath
    {
        get => _installPath;
        set => SetField(ref _installPath, value);
    }

    public ManagedGame? ManagedGame
    {
        get => _managedGame;
        set
        {
            if (SetField(ref _managedGame, value))
            {
                OnPropertyChanged(nameof(GameName));
            }
        }
    }

    public string GameName => ManagedGame?.Name ?? "(Unmapped)";

    public GameInstallRecord Clone() => new()
    {
        Manage = Manage,
        GameStore = GameStore,
        InstallPath = InstallPath,
        ManagedGame = ManagedGame
    };
}

public sealed class GameInstallWizardViewModel : NotifyBase
{
    private const int PageSize = 20;
    private int _currentPageIndex;

    public ObservableCollection<GameInstallRecord> DiscoveredInstalls { get; }
    public ObservableCollection<ManagedGame> ManagedGames { get; }

    public bool IsFirstRun { get; }

    public GameInstallWizardViewModel(IEnumerable<GameInstallRecord> discoveredInstalls, ObservableCollection<ManagedGame> managedGames, bool isFirstRun)
    {
        DiscoveredInstalls = new ObservableCollection<GameInstallRecord>(discoveredInstalls);
        ManagedGames = managedGames;
        IsFirstRun = isFirstRun;
    }

    public IEnumerable<GameInstallRecord> CurrentPageItems =>
        DiscoveredInstalls.Skip(_currentPageIndex * PageSize).Take(PageSize);

    public int CurrentPage => _currentPageIndex + 1;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(DiscoveredInstalls.Count / (double)PageSize));
    public bool CanGoPrevious => _currentPageIndex > 0;
    public bool CanGoNext => _currentPageIndex + 1 < TotalPages;

    public string PageSummary => $"Page {CurrentPage} / {TotalPages} · {DiscoveredInstalls.Count} discovered installs";

    public void NextPage()
    {
        if (!CanGoNext)
        {
            return;
        }

        _currentPageIndex++;
        RefreshPaging();
    }

    public void PreviousPage()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        _currentPageIndex--;
        RefreshPaging();
    }

    public IReadOnlyList<GameInstallRecord> SelectedInstalls() => DiscoveredInstalls.Where(x => x.Manage).ToList();

    public void RefreshPaging()
    {
        OnPropertyChanged(nameof(CurrentPageItems));
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
    }
}

public abstract class NotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
