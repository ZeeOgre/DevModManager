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
        byte[] nifBytes = File.ReadAllBytes(fullNifPath);
        IReadOnlyList<NifSerializedString> serialized = NifReader.ReadSerializedStrings(nifBytes);
        IReadOnlyList<NifMeshStringEntry> meshes = _reader.ReadMeshStrings(fullNifPath);
        string?[]? namesByMeshStringIndex = null;
        if (NifReader.TryReadBethesdaStructure(nifBytes, out NifStructureScan structure))
            namesByMeshStringIndex = ResolveMeshNamesFromBlockStructure(nifBytes, serialized, structure, meshes);

        foreach (NifMeshStringEntry entry in meshes)
        {
            string fullSourceMesh = Path.Combine(fullGameRoot, entry.NormalizedToken);

            string blockName = TryGetResolvedName(namesByMeshStringIndex, entry.Index)
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

    private static string?[] ResolveMeshNamesFromBlockStructure(
        byte[] bytes,
        IReadOnlyList<NifSerializedString> serialized,
        NifStructureScan structure,
        IReadOnlyList<NifMeshStringEntry> meshes)
    {
        var names = new string?[serialized.Count];
        if (structure.HeaderStrings.Count == 0 || structure.Blocks.Count == 0 || meshes.Count == 0)
            return names;

        var meshOffsets = new Dictionary<int, int>();
        foreach (NifMeshStringEntry mesh in meshes)
        {
            if (mesh.Index < 0 || mesh.Index >= serialized.Count)
                continue;

            meshOffsets[serialized[mesh.Index].Offset] = mesh.Index;
        }

        if (meshOffsets.Count == 0)
            return names;

        foreach (NifBlockSpan block in structure.Blocks)
        {
            int? blockNameStringIndex = FindFirstStringRefInBlock(bytes, block.StartOffset, block.EndOffsetExclusive, serialized, structure.HeaderStrings.Count);
            string? sanitizedBlockName = null;
            if (blockNameStringIndex.HasValue)
            {
                string raw = structure.HeaderStrings[blockNameStringIndex.Value].Trim();
                sanitizedBlockName = SanitizeSpellFileName(raw);
                if (string.IsNullOrWhiteSpace(sanitizedBlockName))
                    sanitizedBlockName = null;
            }

            if (sanitizedBlockName == null)
                continue;

            foreach (KeyValuePair<int, int> pair in meshOffsets)
            {
                int meshOffset = pair.Key;
                int meshStringIndex = pair.Value;
                if (meshOffset >= block.StartOffset && meshOffset < block.EndOffsetExclusive)
                    names[meshStringIndex] = sanitizedBlockName;
            }
        }

        return names;
    }

    private static int? FindFirstStringRefInBlock(
        byte[] bytes,
        int blockStart,
        int blockEndExclusive,
        IReadOnlyList<NifSerializedString> serialized,
        int headerStringCount)
    {
        for (int pos = Align4(blockStart); pos <= blockEndExclusive - 4; pos += 4)
        {
            if (IsInsideSerializedRecord(serialized, pos))
                continue;

            int candidateIndex = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pos, 4));
            if (candidateIndex < 0 || candidateIndex >= headerStringCount)
                continue;

            return candidateIndex;
        }

        return null;
    }

    private static bool IsInsideSerializedRecord(IReadOnlyList<NifSerializedString> serialized, int position)
    {
        foreach (NifSerializedString entry in serialized)
        {
            int recordStart = entry.Offset;
            int recordEnd = entry.Offset + entry.PrefixSize + entry.Length;
            if (position >= recordStart && position < recordEnd)
                return true;
        }

        return false;
    }

    private static int Align4(int value)
    {
        int remainder = value & 0x3;
        return remainder == 0 ? value : value + (4 - remainder);
    }

    private static string? TryGetResolvedName(string?[]? namesByMeshStringIndex, int meshStringIndex)
    {
        if (namesByMeshStringIndex == null || meshStringIndex < 0 || meshStringIndex >= namesByMeshStringIndex.Length)
            return null;

        return namesByMeshStringIndex[meshStringIndex];
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
