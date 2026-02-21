namespace DMM.AssetManagers.NIF;

public sealed class NifReadResult
{
    public string Path { get; init; } = "";
    public List<string> Mats { get; } = new();
    public List<string> Meshes { get; } = new();
    public List<string> OtherAssets { get; } = new();
}

public sealed class NifStringEntry
{
    public int Index { get; init; }
    public string Value { get; init; } = "";
}

public sealed class NifReadableMeshCopy
{
    public string NifPath { get; init; } = "";
    public string SourceMeshPath { get; init; } = "";
    public string DestinationMeshPath { get; init; } = "";
    public string OriginalMeshToken { get; init; } = "";
    public string RewrittenMeshToken { get; init; } = "";
}

public sealed class NifInvalidMatReference
{
    public string NifPath { get; init; } = "";
    public string MatPath { get; init; } = "";
    public int StringIndex { get; init; }
}

public sealed class NifStringRewritePlan
{
    public Dictionary<int, int> Remap { get; } = new();
}
