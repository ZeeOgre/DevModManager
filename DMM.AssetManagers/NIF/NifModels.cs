namespace DMM.AssetManagers.NIF;

public sealed class NifReadResult
{
    public string Path { get; init; } = "";
    public List<string> Mats { get; } = new();
    public List<string> Meshes { get; } = new();
    public List<string> Rigs { get; } = new();
    public List<string> Havoks { get; } = new();
    public List<string> OtherAssets { get; } = new();
    public NifDependencyDiagnostics Diagnostics { get; init; } = new();
}

/// <summary>Outcome of the schema-selected dependency reader (never a token-search outcome).</summary>
public sealed class NifDependencyDiagnostics
{
    public NifFamily Family { get; set; }
    public bool IsKnown { get; set; }
    public string? SchemaProfileName { get; init; }
    public string? NearestSchemaProfileName { get; init; }
    public string? CompatibilityProfileName { get; init; }
    public bool IsExactProfileMatch { get; init; }
    public uint ObservedBethesdaStreamVersion { get; init; }
    public List<string> UnsupportedFields { get; } = new();
    public List<string> UnconsumedBlockData { get; } = new();
    public bool IsComplete { get; set; }
    public uint BethesdaStreamVersion { get; init; }
    public List<NifDependencyRecord> Records { get; } = new();
    public List<string> UnhandledBlockTypes { get; } = new();
}

public enum NifFamily
{
    Unknown,
    Other,
    Skyrim,
    Fallout4,
    Starfield
}

public sealed class NifDependencyRecord
{
    public int BlockIndex { get; init; }
    public string BlockType { get; init; } = "";
    public string Field { get; init; } = "";
    public string Category { get; init; } = "";
    public int Offset { get; init; } = -1;
    public string Value { get; init; } = "";
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
    public string OriginalMeshTokenNormalized { get; init; } = "";
    public string RewrittenMeshToken { get; init; } = "";
}

public sealed class NifMeshStringEntry
{
    public int Index { get; init; }
    public int Offset { get; init; } = -1;
    public string RawToken { get; init; } = "";
    public string NormalizedToken { get; init; } = "";
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

public readonly record struct NifSerializedString(int Offset, int PrefixSize, int Length, string Value);

public sealed class NifBlockSpan
{
    public int Index { get; init; }
    public string TypeName { get; init; } = "";
    public int StartOffset { get; init; }
    public int EndOffsetExclusive { get; init; }
}

public sealed class NifStructureScan
{
    public uint BethesdaStreamVersion { get; init; }
    public int BlocksStartOffset { get; init; }
    public IReadOnlyList<string> HeaderStrings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<NifBlockSpan> Blocks { get; init; } = Array.Empty<NifBlockSpan>();
}
