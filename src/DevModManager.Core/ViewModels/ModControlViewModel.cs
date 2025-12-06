using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DevModManager.Core.ViewModels
{
    public class ModControlViewModel : INotifyPropertyChanged
    {
        private readonly ModItemViewModel _item;

        public string Name => _item.Name;

        public string Stage
        {
            get => _item.Stage;
            set
            {
                if (_item.Stage == value) return;
                _item.Stage = value;
                OnPropertyChanged();
            }
        }

        // Forward existing commands from the ModItemViewModel
        public ICommand OpenFolderCommand => _item.OpenFolderCommand;
        public ICommand OpenBackupCommand => _item.OpenBackupCommand;
        public ICommand PromoteCommand => _item.PromoteCommand;
        public ICommand PackageCommand => _item.PackageCommand;

        // New commands exposed by the control (stubs / forwarders)
        public ICommand OpenStageFolderCommand { get; }
        public ICommand FixAudioPathsCommand { get; }
        public ICommand MakeLinkedAfCommand { get; }
        public ICommand GitStatusCommand { get; }
        public ICommand GitCommitCommand { get; }
        public ICommand GitPushCommand { get; }
        public ICommand InferAchListCommand { get; }
        public ICommand ConvertTesCommand { get; }
        public ICommand ImportFromProdCommand { get; }

        public ModControlViewModel(ModItemViewModel item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _item.PropertyChanged += Item_PropertyChanged;

            // Try to reuse existing OpenFolderCommand when asked to open stage folder
            OpenStageFolderCommand = new DelegateCommand(_ =>
            {
                if (OpenFolderCommand?.CanExecute(null) ?? false) OpenFolderCommand.Execute(null);
            });

            FixAudioPathsCommand = new DelegateCommand(async _ =>
            {
                Debug.WriteLine($"FixAudioPaths for {_item.Name} (stub)");
                await Task.CompletedTask;
            });

            MakeLinkedAfCommand = new DelegateCommand(_ =>
            {
                Debug.WriteLine($"MakeLinkedAf for {_item.Name} (stub)");
            });

            GitStatusCommand = new DelegateCommand(_ =>
            {
                Debug.WriteLine($"Git status for {_item.Name} (stub)");
            });

            GitCommitCommand = new DelegateCommand(_ =>
            {
                Debug.WriteLine($"Git commit for {_item.Name} (stub)");
            });

            GitPushCommand = new DelegateCommand(_ =>
            {
                Debug.WriteLine($"Git push for {_item.Name} (stub)");
            });

            InferAchListCommand = new DelegateCommand(async _ =>
            {
                Debug.WriteLine($"InferAchList for {_item.Name} (stub)");
                await Task.CompletedTask;
            });

            ConvertTesCommand = new DelegateCommand(async _ =>
            {
                Debug.WriteLine($"ConvertTES for {_item.Name} (stub)");
                await Task.CompletedTask;
            });

            ImportFromProdCommand = new DelegateCommand(async _ =>
            {
                Debug.WriteLine($"ImportFromProd for {_item.Name} (stub)");
                await Task.CompletedTask;
            });
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Mirror source changes to the control VM (Stage frequently)
            if (e.PropertyName == nameof(ModItemViewModel.Stage))
                OnPropertyChanged(nameof(Stage));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Lightweight command implementation used only in this file to avoid dependencies
        private sealed class DelegateCommand : ICommand
        {
            private readonly Func<object?, Task>? _executeAsync;
            private readonly Action<object?>? _execute;
            private readonly Func<object?, bool>? _canExecute;

            public DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public DelegateCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
            {
                _executeAsync = executeAsync;
                _canExecute = canExecute;
            }

            public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

            public void Execute(object? parameter)
            {
                if (_execute != null)
                    _execute(parameter);
                else
                    _ = _executeAsync?.Invoke(parameter);
            }

            public event EventHandler? CanExecuteChanged;
            public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}