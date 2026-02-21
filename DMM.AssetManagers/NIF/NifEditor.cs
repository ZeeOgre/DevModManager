namespace DMM.AssetManagers.NIF;

public sealed class NifEditor
{
    private readonly NifReader _reader;

    public NifEditor() : this(new NifReader())
    {
    }

    public NifEditor(NifReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public IReadOnlyList<NifReadableMeshCopy> BuildReadableMeshCopyPlan(string nifPath, string gameRoot)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (gameRoot == null) throw new ArgumentNullException(nameof(gameRoot));

        string fullNifPath = Path.GetFullPath(nifPath);
        string fullGameRoot = Path.GetFullPath(gameRoot);

        string dataMeshesRoot = Path.Combine(fullGameRoot, "Data", "Meshes");
        string nifRelativeToMeshes = Path.GetRelativePath(dataMeshesRoot, fullNifPath);
        if (nifRelativeToMeshes.StartsWith("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"NIF '{nifPath}' is not under '{dataMeshesRoot}'.");

        string nifDirRel = Path.GetDirectoryName(nifRelativeToMeshes) ?? string.Empty;
        string nifBase = Path.GetFileNameWithoutExtension(fullNifPath);

        var read = _reader.Read(fullNifPath);
        var planned = new List<NifReadableMeshCopy>();

        foreach (string meshToken in read.Meshes)
        {
            string fullSourceMesh = Path.Combine(fullGameRoot, meshToken);
            if (!File.Exists(fullSourceMesh))
                continue;

            string blockName = Path.GetFileNameWithoutExtension(meshToken);
            string destRel = Path.Combine("Data", "Geometries", nifDirRel, nifBase, blockName + ".mesh");
            string fullDest = Path.GetFullPath(Path.Combine(fullGameRoot, destRel));
            string rewrittenMeshToken = Path.GetRelativePath(Path.Combine(fullGameRoot, "Data"), fullDest)
                .Replace('/', '\\');

            planned.Add(new NifReadableMeshCopy
            {
                NifPath = fullNifPath,
                SourceMeshPath = fullSourceMesh,
                DestinationMeshPath = fullDest,
                OriginalMeshToken = meshToken,
                RewrittenMeshToken = rewrittenMeshToken
            });
        }

        return planned;
    }

    public NifStringRewritePlan BuildDeduplicateStringPlan(string nifPath)
    {
        var strings = _reader.ReadStringTable(nifPath);
        var plan = new NifStringRewritePlan();

        var firstByValue = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (NifStringEntry entry in strings.OrderBy(x => x.Index))
        {
            if (firstByValue.TryGetValue(entry.Value, out int lowest))
            {
                plan.Remap[entry.Index] = lowest;
            }
            else
            {
                firstByValue[entry.Value] = entry.Index;
            }
        }

        return plan;
    }

    public IReadOnlyList<NifInvalidMatReference> FindInvalidMatReferences(string nifPath, string gameRoot, ISet<string>? removedMats = null)
    {
        string fullGameRoot = Path.GetFullPath(gameRoot);
        var strings = _reader.ReadStringTable(nifPath);
        var invalid = new List<NifInvalidMatReference>();

        foreach (NifStringEntry entry in strings)
        {
            if (!NifReader.TryNormalizeMatToken(entry.Value, out string matPath))
                continue;

            bool isRemoved = removedMats?.Contains(matPath) == true;
            bool exists = File.Exists(Path.Combine(fullGameRoot, matPath));
            if (exists && !isRemoved)
                continue;

            invalid.Add(new NifInvalidMatReference
            {
                NifPath = Path.GetFullPath(nifPath),
                MatPath = matPath,
                StringIndex = entry.Index
            });
        }

        return invalid;
    }
}
