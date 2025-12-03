using System;
using System.Collections.ObjectModel;

namespace DevModManager.Avalonia;

public sealed class MainWindowDesignViewModel
{
    public string AppTitle { get; } = "DevModManager v <2.0>";
    public ObservableCollection<string> GameFolders { get; } = new() { @"M:\Starfield_1.14", @"D:\Games\Starfield" };
    public string SelectedGameFolder { get; } = @"M:\Starfield_1.14";
    public ObservableCollection<string> States { get; } = new() { "DEV", "TEST", "STAGING", "PROD", "<NONE>" };
    public ObservableCollection<ModRow> Mods { get; } = new()
    {
        new ModRow { Name="ZeeOgresEnhancedOutposts", State="DEV", LastModified=new DateTime(2025,11,12,8,57,0), IsActive=false, OnCreations=false, OnNexus=false, HasGitRepo=true },
        new ModRow { Name="ZeeOgresConstellationDropCrate", State="PROD", LastModified=new DateTime(2024,11,30,18,30,0), IsActive=true, OnCreations=true, OnNexus=false, HasGitRepo=false },
    };
}

public sealed class ModRow
{
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime LastModified { get; set; }
    public bool IsActive { get; set; }
    public bool OnCreations { get; set; }
    public bool OnNexus { get; set; }
    public bool HasGitRepo { get; set; }
}