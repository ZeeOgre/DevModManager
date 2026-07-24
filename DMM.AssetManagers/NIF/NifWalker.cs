using NiflySharp;
using NiflySharp.Blocks;

namespace DMM.AssetManagers.NIF;

public enum DependencyKind { Material, Mesh, Rig, Havok, Texture, Other }

public sealed class DependencyReference
{
    public string RawValue { get; init; } = "";
    public string NormalizedPath { get; init; } = "";
    public DependencyKind Kind { get; init; }
    public int? BlockIndex { get; init; }
    public string BlockType { get; init; } = "";
    public string FieldPath { get; init; } = "";
}

/// <summary>Traverses NiflySharp's deserialized object graph; it never decodes NIF bytes itself.</summary>
public sealed class NifWalker
{
    public IReadOnlyList<DependencyReference> Walk(string nifPath, NifDependencyDiagnostics diagnostics)
    {
        var file = new NifFile();
        try { file.Load(nifPath); }
        catch (Exception exception)
        {
            diagnostics.IsKnown = false;
            diagnostics.IsComplete = false;
            diagnostics.UnhandledBlockTypes.Add($"NiflySharp parse failure: {exception.GetType().Name}");
            return Array.Empty<DependencyReference>();
        }
        diagnostics.IsKnown = file.Valid;
        diagnostics.IsComplete = file.Valid && !file.HasUnknownBlocks;
        diagnostics.Family = NifFamily.Starfield;
        if (!file.Valid) diagnostics.UnhandledBlockTypes.Add("NiflySharp could not parse the NIF.");
        if (file.HasUnknownBlocks) diagnostics.UnhandledBlockTypes.Add("NiflySharp preserved unknown block(s).");

        var references = new List<DependencyReference>();
        for (int index = 0; index < file.Blocks.Count; index++)
        {
            INiObject block = file.Blocks[index];
            string type = block.GetType().Name;
            if (block is BSGeometry geometry)
            {
                foreach (var mesh in geometry.Meshes ?? [])
                    foreach (var value in mesh.StringRefs.Select(reference => reference.String))
                        Add(value, DependencyKind.Mesh, index, type, "Meshes[].Mesh Path", references);
            }
            if (block is BSLightingShaderProperty lighting)
                Add(lighting.Name.String, DependencyKind.Material, index, type, "NiObjectNET.Name", references);
            if (block is BSEffectShaderProperty effect)
                Add(effect.Name.String, DependencyKind.Material, index, type, "NiObjectNET.Name", references);
            if (block is BSBehaviorGraphExtraData behavior)
                Add(behavior.BehaviourGraphFile.String, DependencyKind.Havok, index, type, "Behaviour Graph File", references);
            if (block is BSShaderTextureSet textureSet)
                foreach (var texture in textureSet.Textures ?? []) Add(texture.Content, DependencyKind.Texture, index, type, "Textures[]", references);
        }
        return references;
    }

    private static void Add(string? raw, DependencyKind kind, int blockIndex, string blockType, string fieldPath, List<DependencyReference> output)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        string token = raw.Replace('/', '\\').Trim();
        bool valid = kind switch
        {
            DependencyKind.Material => NifReader.TryNormalizeMatToken(token, out _),
            DependencyKind.Mesh => NifReader.TryNormalizeMeshToken(token, out _),
            DependencyKind.Rig => NifReader.TryNormalizeRigToken(token, out _),
            DependencyKind.Havok => NifReader.TryNormalizeHkxToken(token, out _),
            DependencyKind.Texture => TryNormalizeTexture(token, out _),
            _ => false
        };
        if (!valid) return;
        string normalized = kind switch
        {
            DependencyKind.Material => NormalizeMat(token), DependencyKind.Mesh => NormalizeMesh(token),
            DependencyKind.Rig => NormalizeRig(token), DependencyKind.Havok => NormalizeHavok(token),
            DependencyKind.Texture => NormalizeTexture(token), _ => token
        };
        output.Add(new DependencyReference { RawValue = raw, NormalizedPath = normalized, Kind = kind, BlockIndex = blockIndex, BlockType = blockType, FieldPath = fieldPath });
    }
    private static string NormalizeMat(string x) { NifReader.TryNormalizeMatToken(x, out var v); return v; }
    private static string NormalizeMesh(string x) { NifReader.TryNormalizeMeshToken(x, out var v); return v; }
    private static string NormalizeRig(string x) { NifReader.TryNormalizeRigToken(x, out var v); return v; }
    private static string NormalizeHavok(string x) { NifReader.TryNormalizeHkxToken(x, out var v); return v; }
    private static bool TryNormalizeTexture(string x, out string value) => NifReader.TryNormalizeDataToken(x, ".dds", out value);
    private static string NormalizeTexture(string x) { TryNormalizeTexture(x, out var v); return v; }
}
