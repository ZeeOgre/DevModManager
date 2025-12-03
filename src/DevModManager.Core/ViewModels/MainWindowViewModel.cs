using DevModManager.Core.Models;
using DevModManager.Core.Utility;
using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;

namespace DevModManager.Core.ViewModels
{
    public class MainWindowViewModel
    {
        public string AppTitle { get; } = "DevModManager v <2.0>";
        public ObservableCollection<string> GameFolders { get; } = new() { @"M:\Starfield_1.14", @"D:\Games\Starfield" };
        private string _selectedGameFolder = @"M:\Starfield_1.14";
        public string SelectedGameFolder
        {
            get => _selectedGameFolder;
            set => _selectedGameFolder = value;
        }

        public ObservableCollection<string> States { get; } = new()
        {
            "DEV", "TEST", "STAGING", "PROD", "<NONE>"
        };

        public ObservableCollection<ModDatum> Mods { get; } = new();

        public ICommand OpenGameDataCommand { get; }
        public ICommand LaunchLomCommand { get; }
        public ICommand EditPluginsCommand { get; }
        public ICommand LaunchGameCommand { get; }
        public ICommand OpenModDetailsCommand { get; }

        public MainWindowViewModel()
        {
            // Commands (no-ops for now)
            OpenGameDataCommand = new RelayCommand(_ => { });
            LaunchLomCommand = new RelayCommand(_ => { });
            EditPluginsCommand = new RelayCommand(_ => { });
            LaunchGameCommand = new RelayCommand(_ => { });
            OpenModDetailsCommand = new RelayCommand(m =>
            {
                // open mod details or navigate; placeholder
                // m will be ModDatum
            });

            // Dummy data seeded from the mockup
            Mods.Add(new ModDatum { Name = "ZeeOgresEnhancedOutposts", State = "DEV", LastModified = DateTime.Parse("2025-11-12 08:57"), IsActive = false, OnCreations = false, OnNexus = false, HasGitRepo = true });
            Mods.Add(new ModDatum { Name = "ZeeOgresOutpostTutorial_Part1_Leveling", State = "TEST", LastModified = DateTime.Parse("2025-10-23 14:23"), IsActive = false, OnCreations = true, OnNexus = false, HasGitRepo = true });
            Mods.Add(new ModDatum { Name = "zeeogresoutposttutorial_part2_chem-empire", State = "<NONE>", LastModified = DateTime.Parse("2025-11-12 08:57"), IsActive = false, OnCreations = false, OnNexus = false, HasGitRepo = false });
            Mods.Add(new ModDatum { Name = "zeeogresoutposttutorial_part3_vyitnium-tycoon", State = "<NONE>", LastModified = DateTime.Parse("2025-11-16 08:57"), IsActive = false, OnCreations = false, OnNexus = false, HasGitRepo = false });
            Mods.Add(new ModDatum { Name = "zeeogresoutposttutorial_part4_unique-inorganics", State = "STAGING", LastModified = DateTime.Parse("2025-11-16 08:57"), IsActive = false, OnCreations = false, OnNexus = false, HasGitRepo = false });
            Mods.Add(new ModDatum { Name = "zeeogresoutposttutorial_part5_unique-organics", State = "<NONE>", LastModified = DateTime.Parse("2025-11-12 08:57"), IsActive = false, OnCreations = false, OnNexus = false, HasGitRepo = false });
            Mods.Add(new ModDatum { Name = "zeeogresoutposttutorial_part6_all-resources", State = "DEV", LastModified = DateTime.Parse("2021-09-05 12:34"), IsActive = false, OnCreations = false, OnNexus = false, HasGitRepo = false });
            Mods.Add(new ModDatum { Name = "ZeeOgresConstellationDropCrate", State = "PROD", LastModified = DateTime.Parse("2024-11-30 18:30"), IsActive = true, OnCreations = true, OnNexus = false, HasGitRepo = false });
        }
    }

    // Minimal RelayCommand
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
