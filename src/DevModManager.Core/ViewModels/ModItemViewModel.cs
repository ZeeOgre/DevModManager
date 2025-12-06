using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using DevModManager.Core.Utility;

namespace DevModManager.Core.ViewModels
{
    public class ModItemViewModel : INotifyPropertyChanged
    {
        private readonly Func<string?> _getActiveFolder;

        public string Name { get; }
        public string? BethesdaId { get; set; }
        public string? NexusId { get; set; }

        private string _stage = "DEV";
        public string Stage
        {
            get => _stage;
            set
            {
                if (_stage == value) return;
                _stage = value;
                OnPropertyChanged();
                _ = OnStageChangedAsync(value);
            }
        }

        private bool _isMonitored;
        public bool IsMonitored
        {
            get => _isMonitored;
            set { if (_isMonitored == value) return; _isMonitored = value; OnPropertyChanged(); }
        }

        public ICommand OpenFolderCommand { get; }
        public ICommand OpenBackupCommand { get; }
        public ICommand PromoteCommand { get; }
        public ICommand PackageCommand { get; }

        public ModItemViewModel(string name, Func<string?> getActiveFolder)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _getActiveFolder = getActiveFolder ?? throw new ArgumentNullException(nameof(getActiveFolder));

            OpenFolderCommand = new RelayCommand(_ => OpenFolder());
            OpenBackupCommand = new RelayCommand(_ => OpenBackup());
            PromoteCommand = new RelayCommand(_ => Promote());
            PackageCommand = new RelayCommand(_ => Package());
        }

        private void OpenFolder()
        {
            try
            {
                var folder = _getActiveFolder()?.Trim();
                if (string.IsNullOrWhiteSpace(folder))
                {
                    Debug.WriteLine($"OpenFolder: no active folder for {Name}");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenFolder error for {Name}: {ex.Message}");
            }
        }

        private void OpenBackup()
        {
            Debug.WriteLine($"Open backup for {Name} (placeholder)");
        }

        private void Promote()
        {
            Debug.WriteLine($"Promote {Name} (placeholder)");
        }

        private void Package()
        {
            Debug.WriteLine($"Package {Name} (placeholder)");
        }

        private Task OnStageChangedAsync(string newStage)
        {
            Debug.WriteLine($"Stage for {Name} changed to {newStage}");
            return Task.CompletedTask;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}