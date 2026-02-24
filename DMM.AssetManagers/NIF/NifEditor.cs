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

        string nifRelativeToMeshes = ResolveNifRelativeToMeshes(fullNifPath, fullGameRoot);

        string nifDirRel = Path.GetDirectoryName(nifRelativeToMeshes) ?? string.Empty;
        string nifBase = Path.GetFileNameWithoutExtension(fullNifPath);
        var destinationNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var planned = new List<NifReadableMeshCopy>();
        IReadOnlyList<NifMeshStringEntry> meshes = _reader.ReadMeshStrings(fullNifPath);
        IReadOnlyList<NifStringEntry> strings = _reader.ReadStringTable(fullNifPath);

        foreach (NifMeshStringEntry entry in meshes)
        {
            string fullSourceMesh = Path.Combine(fullGameRoot, entry.NormalizedToken);

            string blockName = TryGetSpellStyleMeshName(strings, entry.Index)
                               ?? GetReadableMeshName(entry.NormalizedToken);
            string uniqueBlockName = EnsureUniqueName(blockName, destinationNameCounts);
            string destRel = Path.Combine("Data", "Geometries", nifDirRel, nifBase, uniqueBlockName + ".mesh");
            string fullDest = Path.GetFullPath(Path.Combine(fullGameRoot, destRel));
            string rewrittenMeshToken = Path.GetRelativePath(Path.Combine(fullGameRoot, "Data"), fullDest)
                .Replace('/', '\\');

            planned.Add(new NifReadableMeshCopy
            {
                NifPath = fullNifPath,
                SourceMeshPath = fullSourceMesh,
                DestinationMeshPath = fullDest,
                OriginalMeshToken = entry.RawToken,
                OriginalMeshTokenNormalized = entry.NormalizedToken,
                RewrittenMeshToken = rewrittenMeshToken
            });
        }

        return planned;
    }

    private static string? TryGetSpellStyleMeshName(IReadOnlyList<NifStringEntry> strings, int meshStringIndex)
    {
        for (int i = meshStringIndex - 1; i >= 0; i--)
        {
            string candidate = strings[i].Value.Trim();
            if (!LooksLikeGeometryObjectName(candidate))
                continue;

            string objectName = SanitizeSpellFileName(candidate);
            if (!string.IsNullOrWhiteSpace(objectName))
                return objectName;
        }

        return null;
    }

    private static bool LooksLikeGeometryObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value.Trim();
        if (trimmed.Length is < 2 or > 128)
            return false;

        if (trimmed.Contains('\\') || trimmed.Contains('/') || trimmed.Contains('.'))
            return false;

        if (IsLikelyNifTypeName(trimmed))
            return false;

        if (LooksLikeHashedName(trimmed))
            return false;

        bool hasPayloadShape = trimmed.Contains(':')
                               || trimmed.Contains('_')
                               || trimmed.Contains(' ')
                               || trimmed.Contains('-');
        if (!hasPayloadShape)
            return false;

        return trimmed.Any(char.IsLetter);
    }

    private static bool IsLikelyNifTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.StartsWith("Ni", StringComparison.Ordinal)
            || value.StartsWith("BS", StringComparison.Ordinal)
            || value.StartsWith("bhk", StringComparison.Ordinal)
            || value.StartsWith("hk", StringComparison.Ordinal))
        {
            return value.All(char.IsLetterOrDigit);
        }

        return false;
    }

    private static string SanitizeSpellFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string lowered = input.ToLowerInvariant();
        var filtered = new List<char>(lowered.Length);
        foreach (char c in lowered)
        {
            if (c == '.' || c == ' ' || c == '/' || c == '\\')
                continue;

            char value = c switch
            {
                '<' or '>' or ':' or '"' or '|' or '?' or '*' => '_',
                _ => c
            };

            if (value is >= ' ' and <= '~')
                filtered.Add(value);
        }

        return new string(filtered.ToArray());
    }

    private static string ResolveNifRelativeToMeshes(string fullNifPath, string fullGameRoot)
    {
        string[] meshesRoots =
        [
            Path.Combine(fullGameRoot, "Data", "Meshes"),
            Path.Combine(fullGameRoot, "Meshes")
        ];

        foreach (string meshesRoot in meshesRoots)
        {
            string rel = Path.GetRelativePath(meshesRoot, fullNifPath);
            if (!rel.StartsWith("..", StringComparison.Ordinal))
                return rel;
        }

        throw new InvalidOperationException($"NIF '{fullNifPath}' is not under Data\\Meshes or Meshes in '{fullGameRoot}'.");
    }

    private static string GetReadableMeshName(string normalizedMeshToken)
    {
        string fileBase = Path.GetFileNameWithoutExtension(normalizedMeshToken);
        if (!LooksLikeHashedName(fileBase))
            return fileBase;

        string? parentFolder = Path.GetFileName(Path.GetDirectoryName(normalizedMeshToken));
        return string.IsNullOrWhiteSpace(parentFolder) ? fileBase : parentFolder;
    }

    private static bool LooksLikeHashedName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 12)
            return false;

        foreach (char c in value)
        {
            bool hex = (c >= '0' && c <= '9')
                       || (c >= 'a' && c <= 'f')
                       || (c >= 'A' && c <= 'F');
            if (!hex)
                return false;
        }

        return true;
    }

    private static string EnsureUniqueName(string baseName, IDictionary<string, int> counts)
    {
        if (!counts.TryGetValue(baseName, out int count))
        {
            counts[baseName] = 1;
            return baseName;
        }

        counts[baseName] = count + 1;
        return $"{baseName}_{count}";
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
