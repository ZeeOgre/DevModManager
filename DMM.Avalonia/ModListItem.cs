using Avalonia.Media;

namespace DMM.Avalonia;

public sealed class ModListItem
{
    public ModListItem(string name, string primaryPlugin, string currentStage, string gameName, string bethesdaId, string nexusId, IBrush rowBackground)
    {
        Name = name;
        PrimaryPlugin = primaryPlugin;
        CurrentStage = currentStage;
        GameName = gameName;
        BethesdaId = bethesdaId;
        NexusId = nexusId;
        RowBackground = rowBackground;
    }

    public string Name { get; }
    public string PrimaryPlugin { get; }
    public string CurrentStage { get; }
    public string GameName { get; }
    public string BethesdaId { get; }
    public string NexusId { get; }
    public IBrush RowBackground { get; }
}
