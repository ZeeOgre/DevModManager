using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DMM.Avalonia;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        DataContext = new HelpWindowViewModel("Help", string.Empty);
    }

    public HelpWindow(HelpWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public static HelpWindow ForSection(string section)
    {
        var content = HelpDocumentLoader.Load(section);
        return new HelpWindow(new HelpWindowViewModel($"Help - {section}", content));
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}

public sealed class HelpWindowViewModel
{
    public HelpWindowViewModel(string title, string content)
    {
        Title = title;
        Content = content;
    }

    public string Title { get; }
    public string Content { get; }
}

public static class HelpDocumentLoader
{
    public static string Load(string section)
    {
        var helpPath = FindHelpPath();
        var raw = helpPath is null
            ? "HELP.md was not found. Add HELP.md at repo root to customize guidance."
            : File.ReadAllText(helpPath);

        var marker = $"## {section}";
        var sectionStart = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (sectionStart < 0)
        {
            return raw;
        }

        var nextHeader = raw.IndexOf("\n## ", sectionStart + marker.Length, StringComparison.OrdinalIgnoreCase);
        return nextHeader < 0
            ? raw[sectionStart..].Trim()
            : raw[sectionStart..nextHeader].Trim();
    }

    private static string? FindHelpPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "HELP.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        var cwdCandidate = Path.Combine(Environment.CurrentDirectory, "HELP.md");
        return File.Exists(cwdCandidate) ? cwdCandidate : null;
    }
}
