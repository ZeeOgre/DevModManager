using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DMM.AssetManagers;

namespace DMM.Avalonia;

public sealed record DependencyReviewDecision(IReadOnlyCollection<string> KeepRelativePaths);

public sealed class DependencyReviewWindow : Window
{
    private readonly ObservableCollection<DependencyReviewItem> _definiteKeep;
    private readonly ObservableCollection<DependencyReviewItem> _maybeKeep;
    private readonly ObservableCollection<DependencyReviewItem> _errors;

    public DependencyReviewWindow(
        string modName,
        string pluginName,
        IReadOnlyList<string> pluginFiles,
        IReadOnlyList<string> archiveFiles,
        IReadOnlyList<ModDependencyEntry> entries,
        IReadOnlyCollection<string> missingReferences,
        IReadOnlyCollection<string> undefinedDiscard)
    {
        Title = $"Dependency Review - {modName}";
        Width = 1300;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _definiteKeep = new ObservableCollection<DependencyReviewItem>(entries
            .Where(x => !x.ParentArchiveMatch)
            .OrderBy(x => x.RelativeDataPath, StringComparer.OrdinalIgnoreCase)
            .Select(x => new DependencyReviewItem(x.RelativeDataPath, x.SourcePath, true, Brushes.LightGreen)));

        _maybeKeep = new ObservableCollection<DependencyReviewItem>(entries
            .Where(x => x.ParentArchiveMatch)
            .OrderBy(x => x.RelativeDataPath, StringComparer.OrdinalIgnoreCase)
            .Select(x => new DependencyReviewItem(x.RelativeDataPath, x.SourcePath, false, Brushes.Khaki)));

        _errors = new ObservableCollection<DependencyReviewItem>(
            missingReferences.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => new DependencyReviewItem(x, "Referenced by plugin but not found on disk/in archive.", false, Brushes.IndianRed))
            .Concat(
                undefinedDiscard
                    .Where(x => !missingReferences.Contains(x, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new DependencyReviewItem(x, "Found in parent/base archive but not present on disk.", false, Brushes.Goldenrod))));

        var modSummary = new TextBlock
        {
            Text = $"Mod: {modName}   Plugin: {pluginName}",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var pluginList = string.Join(", ", pluginFiles.Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var ba2List = string.Join(", ", archiveFiles.Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        var metadataText = new TextBlock
        {
            Text = $"Plugins: {pluginList}\nBA2: {ba2List}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var addNewButton = new Button { Content = "Add New", HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 100 };
        addNewButton.Click += async (_, _) => await AddNewFromPickerAsync();

        var okButton = new Button { Content = "OK", MinWidth = 90 };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 90 };

        okButton.Click += (_, _) => Close(new DependencyReviewDecision(BuildKeepList()));
        cancelButton.Click += (_, _) => Close(null);

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,*,*")
        };

        body.Children.Add(modSummary);
        Grid.SetRow(modSummary, 0);
        Grid.SetColumnSpan(modSummary, 3);

        body.Children.Add(metadataText);
        Grid.SetRow(metadataText, 1);
        Grid.SetColumnSpan(metadataText, 3);

        body.Children.Add(addNewButton);
        Grid.SetRow(addNewButton, 2);
        Grid.SetColumn(addNewButton, 0);

        var definiteColumn = BuildColumn("Definitely Keep (file exists, no parent match)", _definiteKeep, allowDrop: true);
        var maybeColumn = BuildColumn("Maybe Keep (file exists, matches parent)", _maybeKeep, allowDrop: false);
        var errorColumn = BuildColumn("Error / Discard (missing on disk)", _errors, allowDrop: false, checkable: false);

        body.Children.Add(definiteColumn);
        Grid.SetRow(definiteColumn, 3);
        Grid.SetColumn(definiteColumn, 0);

        body.Children.Add(maybeColumn);
        Grid.SetRow(maybeColumn, 3);
        Grid.SetColumn(maybeColumn, 1);

        body.Children.Add(errorColumn);
        Grid.SetRow(errorColumn, 3);
        Grid.SetColumn(errorColumn, 2);

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { okButton, cancelButton }
        };
        body.Children.Add(actionRow);
        Grid.SetRow(actionRow, 4);
        Grid.SetColumnSpan(actionRow, 3);

        Content = new Border
        {
            Margin = new Thickness(12),
            Padding = new Thickness(12),
            Child = body
        };
    }

    private Control BuildColumn(string title, ObservableCollection<DependencyReviewItem> items, bool allowDrop, bool checkable = true)
    {
        var header = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var stack = new StackPanel { Spacing = 4 };
        foreach (var item in items)
        {
            stack.Children.Add(BuildRow(item, checkable));
        }

        items.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
            {
                foreach (var entry in args.NewItems.Cast<DependencyReviewItem>())
                {
                    stack.Children.Add(BuildRow(entry, checkable));
                }
            }
        };

        var scroller = new ScrollViewer
        {
            Content = stack,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Visible
        };

        var columnLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        columnLayout.Children.Add(header);
        Grid.SetRow(header, 0);
        columnLayout.Children.Add(scroller);
        Grid.SetRow(scroller, 1);

        var border = new Border
        {
            BorderBrush = Brushes.DimGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Margin = new Thickness(4, 0, 4, 0),
            Child = columnLayout
        };

        if (allowDrop)
        {
            DragDrop.SetAllowDrop(border, true);
            border.AddHandler(DragDrop.DropEvent, async (_, e) => await HandleDropAsync(e));
        }

        return border;
    }

    private Control BuildRow(DependencyReviewItem item, bool checkable)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        if (checkable)
        {
            var cb = new CheckBox { IsChecked = item.Selected, VerticalAlignment = VerticalAlignment.Top };
            cb.IsCheckedChanged += (_, _) => item.Selected = cb.IsChecked == true;
            panel.Children.Add(cb);
        }

        var text = new TextBlock
        {
            Text = item.RelativePath,
            Foreground = item.Foreground ?? Brushes.White,
            TextWrapping = TextWrapping.Wrap
        };
        ToolTip.SetTip(text, item.Source);
        panel.Children.Add(text);

        return panel;
    }

    private IReadOnlyCollection<string> BuildKeepList()
    {
        return _definiteKeep.Where(x => x.Selected)
            .Concat(_maybeKeep.Where(x => x.Selected))
            .Select(x => x.RelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task AddNewFromPickerAsync()
    {
        var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select files under Data",
            AllowMultiple = true
        });

        foreach (var file in selected)
        {
            if (file.TryGetLocalPath() is { } local)
            {
                TryAddDroppedFile(local);
            }
        }
    }

    private Task HandleDropAsync(DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files) && e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
        {
            foreach (var file in files)
            {
                var local = file.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(local))
                {
                    TryAddDroppedFile(local);
                }
            }
        }

        if (e.Data.Contains(DataFormats.Text) && e.Data.Get(DataFormats.Text) is string text && !string.IsNullOrWhiteSpace(text))
        {
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                TryAddDroppedFile(line.Trim('"', ' '));
            }
        }

        return Task.CompletedTask;
    }

    private void TryAddDroppedFile(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return;
        }

        var marker = "\\Data\\";
        var idx = fullPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return;
        }

        var rel = "Data\\" + fullPath[(idx + marker.Length)..].Replace('/', '\\');
        if (_definiteKeep.Any(x => string.Equals(x.RelativePath, rel, StringComparison.OrdinalIgnoreCase)) ||
            _maybeKeep.Any(x => string.Equals(x.RelativePath, rel, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _definiteKeep.Add(new DependencyReviewItem(rel, fullPath, true));
    }

    private sealed class DependencyReviewItem
    {
        public DependencyReviewItem(string relativePath, string source, bool selected, IBrush? foreground = null)
        {
            RelativePath = relativePath;
            Source = source;
            Selected = selected;
            Foreground = foreground;
        }

        public string RelativePath { get; }
        public string Source { get; }
        public bool Selected { get; set; }
        public IBrush? Foreground { get; }
    }
}
