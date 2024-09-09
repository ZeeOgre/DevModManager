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

namespace DevModManager.App
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;
        private string _previousSelectedStage = string.Empty;

        public MainWindow()
        {
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
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine("MainWindowClose called by " + sender);
            // Add any conditions that might prevent closing
            // e.Cancel = true; // Uncomment to prevent closing for testing
            Debug.WriteLine("MainWindowClose: Flushing database before closing.");
            DbManager.FlushDB();
        }

        private void ViewModel_MainWindowLoaded()
        {
            Debug.WriteLine("ViewModel_MainWindowLoaded");
            // Additional logic for ViewModel loaded
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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var uriString = e.Uri.IsAbsoluteUri ? e.Uri.AbsoluteUri : e.Uri.ToString();

            // Check if the URI is a local path or a file URL
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
            // Handle URLs
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
                        // Assuming UrlInputDialog updates the modItem with the new URL
                        //modItem.Url = uriString; // Update the modItem with the new URL
                        //_viewModel.SaveModItems(); // Save changes to the mod items
                        _viewModel.LoadModItems(); // Refresh the mod items to reflect the changes
                    }
                }
            }
            else
            {
                // Open the URL directly
                _ = Process.Start(new ProcessStartInfo(uriString) { UseShellExecute = true });
            }
        }

        private void OpenBackupFolder_ButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem modItem)
            {
                OpenFolder(PathBuilder.GetBackupFolder(modItem.ModName));
            }
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            T? parent = parentObject as T;
            if (parent != null)
            {
                return parent;
            }
            else
            {
                return FindParent<T>(parentObject);
            }
        }

        // Helper method to find a child of a specific type and name
        private T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            T? foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                T? childType = child as T;
                if (childType != null)
                {
                    if (!string.IsNullOrEmpty(childName))
                    {
                        if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                        {
                            foundChild = (T)child;
                            break;
                        }
                    }
                    else
                    {
                        foundChild = (T)child;
                        break;
                    }
                }

                foundChild = FindChild<T>(child, childName);
                if (foundChild != null) break;
            }

            return foundChild;
        }

        private void Gather_ButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem modItem)
            {
                List<string> updatedFiles = ModItem.Files.GetUpdatedGameFolderFiles(modItem);
                var updatedFilesWindow = new UpdatedFilesWindow(modItem, updatedFiles);
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

        private void TextBlock_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                var hyperlink = textBlock.Inlines.OfType<Hyperlink>().FirstOrDefault();
                if (hyperlink != null && hyperlink.DataContext is ModItem modItem)
                {
                    var navigateUri = hyperlink.NavigateUri?.ToString();
                    if (string.IsNullOrWhiteSpace(navigateUri))
                    {
                        // Determine urlType based on the hyperlink text
                        string urlType = hyperlink.Inlines.OfType<Run>().FirstOrDefault()?.Text.Contains("Bethesda") == true ? "Bethesda" : "Nexus";
                        _viewModel.PromptForUrlIfNeeded(modItem, urlType);
                        e.Handled = true; // Mark the event as handled to prevent further processing
                    }
                }
            }
        }

        private void Promote_ButtonClicked(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var modItem = button?.Tag as ModItem;
            if (modItem != null)
            {
                var modActionWindow = new ModActionWindow(modItem, _viewModel.CurrentStages, "Promote");
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
                var modActionWindow = new ModActionWindow(modItem, _viewModel.CurrentStages, "Package");
                _ = modActionWindow.ShowDialog();
            }
            e.Handled = true;
        }

        private void OnCurrentStageButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem modItem)
            {
                var modActionWindow = new ModActionWindow(modItem, _viewModel.CurrentStages, "Deploy");
                if (modActionWindow.ShowDialog() == true)
                {
                    // Handle the result from ModActionWindow
                    var selectedStage = modActionWindow.SelectedStage;
                    if (selectedStage != null)
                    {
                        _viewModel.LoadModItems(); // Refresh the mod items
                    }
                }
            }
        }

        private void OpenSettingsWindow(SettingsLaunchSource launchSource)
        {
            var settingsWindow = new SettingsWindow(launchSource);
            _ = settingsWindow.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow(SettingsLaunchSource.MainWindow);
        }
    }

}

