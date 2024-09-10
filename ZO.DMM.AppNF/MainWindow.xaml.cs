using AutoUpdaterDotNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using MessageBox = System.Windows.MessageBox;

namespace ZO.DMM.AppNF
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;

        public MainWindow()
        {
            this.Title = $"ZeeOgre's DevModManager ({App.Version})";
            InitializeComponent();
            Debug.WriteLine("MainWindow Initialize Complete");
            _viewModel = new MainWindowViewModel();
            Debug.WriteLine("MainWindow ViewModel Loaded");

            DataContext = _viewModel;
            Debug.WriteLine("MainWindow DataContext Bound");

            Loaded += _viewModel.MainWindowLoaded;
            Debug.WriteLine("ViewModel_MainWindowLoaded");

            this.Closing += MainWindow_Closing;
            Debug.WriteLine("MainWindow Closing Event Set");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainWindow LoadStages()");
            _viewModel.LoadStages();
            Debug.WriteLine("MainWindow LoadStages() complete");

            Debug.WriteLine("MainWindow LoadModItems()");
            _viewModel.LoadModItems();
            Debug.WriteLine("MainWindow LoadModItems() complete");

            if (Config.Instance.AutoCheckForUpdates)
            {
                App.CheckForUpdates(this);
            }

        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine("MainWindowClose called by " + sender);
            Debug.WriteLine("MainWindowClose: Flushing database before closing.");
            DbManager.FlushDB();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var uriString = e.Uri.IsAbsoluteUri ? e.Uri.AbsoluteUri : e.Uri.ToString();

            if (uriString.StartsWith("file://"))
            {
                var localPath = new Uri(uriString).LocalPath;
                OpenFolder(localPath);
            }
            else if (Directory.Exists(uriString))
            {
                OpenFolder(uriString);
            }
            else
            {
                HandleUrl(sender, uriString);
            }

            e.Handled = true;
        }

        private void HandleUrl(object sender, string uriString)
        {
            if (uriString.StartsWith("https://"))
            {
                uriString = ModItem.DB.ExtractID(uriString);
            }
            else
            {
                if (Guid.TryParse(uriString, out _))
                {
                    uriString = $"https://creations.bethesda.net/en/starfield/details/{uriString}";
                }
                else if (int.TryParse(uriString, out _))
                {
                    uriString = $"https://www.nexusmods.com/starfield/mods/{uriString}";
                }
            }

            if (!uriString.Contains("bethesda.net") && !uriString.Contains("nexusmods.com"))
            {
                if (sender is Hyperlink hyperlink && hyperlink.DataContext is ModItem modItem)
                {
                    var urlInputDialog = new UrlInputDialog(modItem, uriString);
                    if (urlInputDialog.ShowDialog() == true)
                    {
                        _viewModel.LoadModItems();
                    }
                }
            }
            else
            {
                _ = Process.Start(new ProcessStartInfo(uriString) { UseShellExecute = true });
            }
        }

        private void OpenFolder(string path)
        {
            if (Directory.Exists(path))
            {
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true
                });
            }
            else
            {
                _ = MessageBox.Show($"The folder '{path}' does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenBackupFolder_ButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem modItem)
            {
                OpenFolder(PathBuilder.GetBackupFolder(modItem.ModName));
            }
        }

        private void Gather_ButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem modItem)
            {

                var updatedFilesWindow = new UpdatedFilesWindow(modItem);
                _ = updatedFilesWindow.ShowDialog();
            }
            e.Handled = true;
        }

        private void OpenModFolder_ButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem modItem)
            {
                OpenFolder(PathBuilder.GetModStagingFolder(modItem.ModName));
            }
            e.Handled = true;
        }

        private void Promote_ButtonClicked(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var modItem = button?.Tag as ModItem;
            if (modItem != null)
            {
                var modActionWindow = new ModActionWindow(modItem, "Promote");
                _ = modActionWindow.ShowDialog();
            }
            e.Handled = true;
        }

        private void Package_ButtonClicked(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var modItem = button?.Tag as ModItem;
            if (modItem != null)
            {
                var modActionWindow = new ModActionWindow(modItem, "Package");
                _ = modActionWindow.ShowDialog();
            }
            e.Handled = true;
        }

        private void OnCurrentStageButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem modItem)
            {
                var modActionWindow = new ModActionWindow(modItem, "Deploy");
                if (modActionWindow.ShowDialog() == true)
                {
                    var selectedStage = modActionWindow.SelectedStage;
                    if (selectedStage != null)
                    {
                        _viewModel.LoadModItems();
                    }
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow(SettingsLaunchSource.MainWindow);
        }

        private void OpenSettingsWindow(SettingsLaunchSource launchSource)
        {
            var settingsWindow = new SettingsWindow(launchSource);
            _ = settingsWindow.ShowDialog();
        }
    }

}

