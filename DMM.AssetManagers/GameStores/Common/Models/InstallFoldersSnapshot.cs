namespace DMM.AssetManagers.GameStores.Common.Models;

public sealed record InstallFoldersSnapshot
{
    public FolderRef? InstallFolder { get; init; }
    public FolderRef? ContentFolder { get; init; }
    public FolderRef? DataFolder { get; init; }

    /// <summary>Any additional roots the store reports (compatibility, shader cache, etc.).</summary>
    public IReadOnlyList<NamedFolderRef> AdditionalFolders { get; init; } = Array.Empty<NamedFolderRef>();
}

public sealed record FolderRef
{
    /// <summary>Future DB foreign key (or other stable internal identifier), if already known.</summary>
    public long? FolderId { get; init; }

    /// <summary>Resolved absolute path on disk.</summary>
    public required string Path { get; init; }
}

public sealed record NamedFolderRef
{
    public required string Role { get; init; }   // e.g. "Mutable", "Packages", "ShaderCache"
    public required FolderRef Folder { get; init; }
}
