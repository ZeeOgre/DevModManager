using nifly;

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

/// <summary>Traverses niflysharp's deserialized object graph; it never decodes NIF bytes itself.</summary>
public sealed class NifWalker
{
    public IReadOnlyList<DependencyReference> Walk(string nifPath, NifDependencyDiagnostics diagnostics)
    {
        NifFile? file = null;
        try
        {
            file = new NifFile();
            if (file.Load(nifPath) != 0)
            {
                file.Dispose();
                diagnostics.IsKnown = false;
                diagnostics.IsComplete = false;
                diagnostics.UnhandledBlockTypes.Add("niflysharp could not parse the NIF.");
                return Array.Empty<DependencyReference>();
            }
        }
        catch (Exception exception)
        {
            file?.Dispose();
            diagnostics.IsKnown = false;
            diagnostics.IsComplete = false;
            diagnostics.UnhandledBlockTypes.Add($"niflysharp parse failure: {exception.GetType().Name}");
            return Array.Empty<DependencyReference>();
        }

        diagnostics.IsKnown = file.IsValid();
        diagnostics.IsComplete = file.IsValid() && !file.HasUnknown();
        diagnostics.Family = NifFamily.Starfield;
        if (!file.IsValid()) diagnostics.UnhandledBlockTypes.Add("niflysharp could not parse the NIF.");
        if (file.HasUnknown()) diagnostics.UnhandledBlockTypes.Add("niflysharp preserved unknown block(s).");

        var references = new List<DependencyReference>();
        NiHeader header = file.GetHeader();
        for (uint index = 0; index < header.GetNumBlocks(); index++)
        {
            NiObject block = header.GetBlockById(index);
            string type = block.GetBlockName();
            if (block is BSGeometry geometry)
                AddGeometryMeshes(geometry, (int)index, type, references);
            if (block is BSLightingShaderProperty lighting)
                Add(lighting.name.get(), DependencyKind.Material, (int)index, type, "NiObjectNET.Name", references);
            if (block is BSEffectShaderProperty effect)
                Add(effect.name.get(), DependencyKind.Material, (int)index, type, "NiObjectNET.Name", references);
            if (block is BSBehaviorGraphExtraData behavior)
                Add(behavior.behaviorGraphFile.get(), DependencyKind.Havok, (int)index, type, "Behaviour Graph File", references);
            if (block is BSShaderTextureSet textureSet)
                foreach (NiString texture in textureSet.textures.items())
                    Add(texture.get(), DependencyKind.Texture, (int)index, type, "Textures[]", references);
        }
        file.Dispose();
        return references;
    }

    private static void AddGeometryMeshes(BSGeometry geometry, int blockIndex, string blockType, List<DependencyReference> output)
    {
        // Consume every mesh niflysharp exposes. The structural recovery in NifReader
        // independently unions all four serialized BSGeometry paths because 2.0.4 can
        // stop exposing entries even when the serialized positions are not sparse.
        byte meshCount = geometry.MeshCount();
        for (byte meshIndex = 0; meshIndex < meshCount; meshIndex++)
        {
            BSGeometryMesh? mesh = geometry.SelectMesh(meshIndex);
            if (mesh is null)
                continue;

            try
            {
                Add(mesh.meshName.get(), DependencyKind.Mesh, blockIndex, blockType, $"Meshes[{meshIndex}].Mesh Path", output);
            }
            finally
            {
                geometry.ReleaseMesh();
            }
        }
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
