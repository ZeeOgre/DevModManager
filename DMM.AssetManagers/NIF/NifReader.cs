using System.Buffers.Binary;

namespace DMM.AssetManagers.NIF;

public sealed class NifReader
{
    private const int MaxSizedStringLength = 0x8000;
    private const uint StarfieldBethesdaStreamVersion = 170;

    public NifReadResult Read(string nifPath)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

        var result = new NifReadResult { Path = nifPath };
        result.Meshes.AddRange(ReadMeshStrings(nifPath).Select(entry => entry.NormalizedToken));

        foreach (NifStringEntry entry in ReadStringTable(nifPath))
        {
            string normalized = entry.Value.Replace('/', '\\').Trim();
            if (string.IsNullOrWhiteSpace(normalized) || ContainsInvalidPathCharacters(normalized))
                continue;

            if (TryNormalizeMatToken(normalized, out string mat))
            {
                result.Mats.Add(mat);
                continue;
            }

            if (TryNormalizeRigToken(normalized, out string rig))
            {
                result.Rigs.Add(rig);
                continue;
            }

            if (TryNormalizeHavokToken(normalized, out string havok))
            {
                result.Havoks.Add(havok);
                continue;
            }

            if (LooksLikeAssetToken(normalized))
                result.OtherAssets.Add(normalized.TrimStart('\\'));
        }

        DeduplicateSort(result.Mats);
        DeduplicateSort(result.Meshes);
        DeduplicateSort(result.Rigs);
        DeduplicateSort(result.Havoks);
        DeduplicateSort(result.OtherAssets);

        return result;
    }

    public IReadOnlyList<NifStringEntry> ReadStringTable(string nifPath)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

        byte[] bytes = File.ReadAllBytes(nifPath);

        // Mesh paths are serialized in NIF blocks, not necessarily in the Bethesda
        // header string table.  Always scan serialized strings across the whole file;
        // this also gives callers stable byte-order indexes needed for in-place rewrites.
        var serialized = ReadSerializedStrings(bytes);
        var entries = new List<NifStringEntry>(serialized.Count);
        for (int i = 0; i < serialized.Count; i++)
        {
            entries.Add(new NifStringEntry { Index = i, Value = serialized[i].Value });
        }

        return entries;
    }

    public static IReadOnlyList<NifSerializedString> ReadSerializedStrings(byte[] bytes)
    {
        var results = new List<NifSerializedString>();

        for (int i = 0; i <= bytes.Length - 4; i++)
        {
            int len32 = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i, 4));
            if (len32 > 0 && len32 <= MaxSizedStringLength && i + 4 + len32 <= bytes.Length)
            {
                string s = ReadAscii(bytes, i + 4, len32);
                if (IsLikelyNifString(s))
                {
                    results.Add(new NifSerializedString(i, 4, len32, s));
                    i += 3 + len32;
                    continue;
                }
            }

            if (i <= bytes.Length - 2)
            {
                ushort len16 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i, 2));
                if (len16 > 0 && len16 <= MaxSizedStringLength && i + 2 + len16 <= bytes.Length)
                {
                    string s = ReadAscii(bytes, i + 2, len16);
                    if (IsLikelyNifString(s))
                    {
                        results.Add(new NifSerializedString(i, 2, len16, s));
                        i += 1 + len16;
                    }
                }
            }
        }

        return results;
    }

    private static string ReadAscii(byte[] bytes, int start, int length)
    {
        return System.Text.Encoding.ASCII.GetString(bytes, start, length);
    }

    private static bool IsLikelyNifString(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 3)
            return false;

        int printable = 0;
        foreach (char c in s)
        {
            if (c >= 32 && c <= 126)
                printable++;
        }

        if (printable != s.Length)
            return false;

        if (s.Contains('\\') || s.Contains('/') || s.Contains('.') || s.Contains("Data", StringComparison.OrdinalIgnoreCase))
            return true;

        return LooksLikeNamePayload(s);
    }

    private static bool LooksLikeNamePayload(string s)
    {
        if (s.Length is < 3 or > 128)
            return false;

        bool hasLetter = false;
        bool hasStructure = false;
        foreach (char c in s)
        {
            if (char.IsLetter(c))
                hasLetter = true;

            if (char.IsDigit(c) || c == ':' || c == '_' || c == '-' || c == ' ')
                hasStructure = true;
        }

        return hasLetter && hasStructure;
    }

    public IEnumerable<string> ExtractAll(string nifPath)
    {
        var read = Read(nifPath);
        return read.Mats.Concat(read.Meshes).Concat(read.Rigs).Concat(read.Havoks).Concat(read.OtherAssets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<string> ExtractMat(string nifPath) => Read(nifPath).Mats;

    public IEnumerable<string> ExtractMesh(string nifPath) => Read(nifPath).Meshes;

    public IEnumerable<string> ExtractRig(string nifPath) => Read(nifPath).Rigs;

    public IEnumerable<string> ExtractHavok(string nifPath) => Read(nifPath).Havoks;

    public IReadOnlyList<NifMeshStringEntry> ReadMeshStrings(string nifPath)
    {
        byte[] bytes = File.ReadAllBytes(nifPath);
        return ReadMeshStrings(bytes);
    }

    /// <summary>
    /// Deserializes mesh references from NIF bytes already loaded by the caller.
    /// This avoids rereading every NIF when a dependency walker also needs its bytes
    /// for material and rig processing.
    /// </summary>
    public IReadOnlyList<NifMeshStringEntry> ReadMeshStrings(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var entries = new List<NifMeshStringEntry>();
        IReadOnlyList<NifSerializedString> serialized = ReadSerializedStrings(bytes);

        if (TryReadBethesdaStructure(bytes, out NifStructureScan structure))
            return ReadStarfieldGeometryMeshPaths(bytes, serialized, structure);

        // Non-Bethesda/legacy NIFs do not have BSGeometry mesh arrays. Retain the
        // serialized-string fallback for those formats only.
        for (int index = 0; index < serialized.Count; index++)
        {
            NifSerializedString value = serialized[index];
            string rawToken = value.Value.Replace('/', '\\').Trim();
            if (!TryNormalizeMeshToken(rawToken, out string normalizedToken))
                continue;

            entries.Add(new NifMeshStringEntry
            {
                Index = index,
                RawToken = rawToken,
                NormalizedToken = normalizedToken
            });
        }

        return entries;
    }

    private static IReadOnlyList<NifMeshStringEntry> ReadStarfieldGeometryMeshPaths(
        byte[] bytes,
        IReadOnlyList<NifSerializedString> serialized,
        NifStructureScan structure)
    {
        var entries = new List<NifMeshStringEntry>();
        if (structure.BethesdaStreamVersion < StarfieldBethesdaStreamVersion)
            return entries;

        var stringIndexByOffset = serialized
            .Select((value, index) => (value.Offset, Index: index))
            .ToDictionary(value => value.Offset, value => value.Index);

        foreach (NifBlockSpan block in structure.Blocks.Where(block =>
                     string.Equals(block.TypeName, "BSGeometry", StringComparison.Ordinal)))
        {
            if (!TryReadStarfieldBsGeometryMeshArrayStart(bytes, block, out int position, out uint geometryFlags))
                continue;

            for (int lodIndex = 0; lodIndex < 4; lodIndex++)
            {
                if (!TryReadByte(bytes, ref position, out byte hasMesh))
                    break;
                if (hasMesh == 0)
                    continue;
                if (hasMesh != 1 || !TrySkip(bytes, ref position, 8) ||
                    !TryReadUInt32(bytes, ref position, out _))
                    break;

                // Per the NIF schema, external meshes have a SizedString Mesh Path;
                // internal meshes have BSMeshData instead and no path dependency.
                if ((geometryFlags & 0x200) != 0)
                    break;

                int stringOffset = position;
                if (!TryReadSizedString4(bytes, ref position, out string rawToken) ||
                    position > block.EndOffsetExclusive ||
                    !TryNormalizeMeshToken(rawToken.Replace('/', '\\').Trim(), out string normalizedToken) ||
                    !stringIndexByOffset.TryGetValue(stringOffset, out int stringIndex))
                    break;

                entries.Add(new NifMeshStringEntry
                {
                    Index = stringIndex,
                    RawToken = rawToken.Replace('/', '\\').Trim(),
                    NormalizedToken = normalizedToken
                });
            }
        }

        return entries;
    }

    private static bool TryReadStarfieldBsGeometryMeshArrayStart(
        byte[] bytes,
        NifBlockSpan block,
        out int position,
        out uint geometryFlags)
    {
        position = block.StartOffset;
        geometryFlags = 0;

        // NiObjectNET: Name, Num Extra Data List, Extra Data List, Controller.
        if (!TrySkip(bytes, ref position, 4) || !TryReadUInt32(bytes, ref position, out uint extraDataCount) ||
            extraDataCount > (uint)((block.EndOffsetExclusive - position) / 4) ||
            !TrySkip(bytes, ref position, checked((int)extraDataCount * 4)) || !TrySkip(bytes, ref position, 4))
            return false;

        // NiAVObject: Flags, transform (12 + 36 + 4), and Collision Object reference.
        if (!TryReadUInt32(bytes, ref position, out geometryFlags) || !TrySkip(bytes, ref position, 56))
            return false;

        // BSGeometry: NiBound (16), BSBoundingBox (24), and Skin/Shader/Alpha refs (12).
        return TrySkip(bytes, ref position, 52) && position <= block.EndOffsetExclusive;
    }

    public bool TryReadBethesdaStructure(string nifPath, out NifStructureScan structure)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

        return TryReadBethesdaStructure(File.ReadAllBytes(nifPath), out structure);
    }

    internal static bool TryReadBethesdaStructure(byte[] bytes, out NifStructureScan structure)
    {
        structure = new NifStructureScan();
        try
        {
            int pos = 0;
            if (!TryReadLine(bytes, ref pos, out _))
                return false;

            if (!TryReadUInt32(bytes, ref pos, out _))
                return false;

            if (!TryReadByte(bytes, ref pos, out _))
                return false;

            if (!TryReadUInt32(bytes, ref pos, out _))
                return false;

            if (!TryReadInt32(bytes, ref pos, out int blockCount) || blockCount <= 0)
                return false;

            if (!TryReadUInt32(bytes, ref pos, out uint streamVersion))
                return false;

            if (!TryReadSizedString1(bytes, ref pos, out _))
                return false;

            if (streamVersion > 130 && !TryReadUInt32(bytes, ref pos, out _))
                return false;

            if (!TryReadSizedString1(bytes, ref pos, out _))
                return false;
            if (!TryReadSizedString1(bytes, ref pos, out _))
                return false;
            if (streamVersion == 130 && !TryReadSizedString1(bytes, ref pos, out _))
                return false;

            if (!TryReadUInt16(bytes, ref pos, out ushort blockTypeCount))
                return false;

            var blockTypes = new List<string>(blockTypeCount);
            for (int i = 0; i < blockTypeCount; i++)
            {
                if (!TryReadSizedString4(bytes, ref pos, out string blockType))
                    return false;

                blockTypes.Add(blockType);
            }

            var blockTypeIndices = new ushort[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                if (!TryReadUInt16(bytes, ref pos, out ushort blockTypeIndex) || blockTypeIndex >= blockTypes.Count)
                    return false;

                blockTypeIndices[i] = blockTypeIndex;
            }

            var blockSizes = new List<int>(blockCount);
            for (int i = 0; i < blockCount; i++)
            {
                if (!TryReadInt32(bytes, ref pos, out int blockSize) || blockSize < 0)
                    return false;

                blockSizes.Add(blockSize);
            }

            if (!TryReadUInt32(bytes, ref pos, out uint stringCountValue))
                return false;
            if (!TryReadUInt32(bytes, ref pos, out _))
                return false;

            int stringCount = checked((int)stringCountValue);
            var headerStrings = new List<string>(stringCount);
            for (int i = 0; i < stringCount; i++)
            {
                if (!TryReadSizedString4(bytes, ref pos, out string value))
                    return false;

                headerStrings.Add(value);
            }

            if (!TryReadInt32(bytes, ref pos, out int groupCount) || groupCount < 0)
                return false;

            if (!TrySkip(bytes, ref pos, groupCount * sizeof(int)))
                return false;

            int blocksStart = pos;
            var blocks = new List<NifBlockSpan>(blockCount);
            int runningOffset = blocksStart;
            for (int i = 0; i < blockCount; i++)
            {
                int size = blockSizes[i];
                int endOffset = runningOffset + size;
                if (endOffset < runningOffset || endOffset > bytes.Length)
                    return false;

                blocks.Add(new NifBlockSpan
                {
                    Index = i,
                    TypeName = blockTypes[blockTypeIndices[i]],
                    StartOffset = runningOffset,
                    EndOffsetExclusive = endOffset
                });

                runningOffset = endOffset;
            }

            structure = new NifStructureScan
            {
                BethesdaStreamVersion = streamVersion,
                BlocksStartOffset = blocksStart,
                HeaderStrings = headerStrings,
                Blocks = blocks
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadLine(byte[] bytes, ref int pos, out string value)
    {
        int start = pos;
        while (pos < bytes.Length && bytes[pos] != (byte)'\n')
            pos++;

        if (pos >= bytes.Length)
        {
            value = string.Empty;
            return false;
        }

        value = System.Text.Encoding.ASCII.GetString(bytes, start, pos - start);
        pos++;
        return true;
    }

    private static bool TrySkip(byte[] bytes, ref int pos, int count)
    {
        if (count < 0 || pos + count > bytes.Length)
            return false;

        pos += count;
        return true;
    }

    private static bool TryReadByte(byte[] bytes, ref int pos, out byte value)
    {
        if (pos + 1 > bytes.Length)
        {
            value = 0;
            return false;
        }

        value = bytes[pos++];
        return true;
    }

    private static bool TryReadUInt16(byte[] bytes, ref int pos, out ushort value)
    {
        if (pos + 2 > bytes.Length)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pos, 2));
        pos += 2;
        return true;
    }

    private static bool TryReadUInt32(byte[] bytes, ref int pos, out uint value)
    {
        if (pos + 4 > bytes.Length)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(pos, 4));
        pos += 4;
        return true;
    }

    private static bool TryReadInt32(byte[] bytes, ref int pos, out int value)
    {
        if (pos + 4 > bytes.Length)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pos, 4));
        pos += 4;
        return true;
    }

    private static bool TryReadSizedString1(byte[] bytes, ref int pos, out string value)
    {
        value = string.Empty;
        if (!TryReadByte(bytes, ref pos, out byte length))
            return false;

        if (pos + length > bytes.Length)
            return false;

        value = System.Text.Encoding.ASCII.GetString(bytes, pos, length);
        pos += length;
        return true;
    }

    private static bool TryReadSizedString4(byte[] bytes, ref int pos, out string value)
    {
        value = string.Empty;
        if (!TryReadInt32(bytes, ref pos, out int length) || length < 0)
            return false;

        if (pos + length > bytes.Length)
            return false;

        value = System.Text.Encoding.ASCII.GetString(bytes, pos, length);
        pos += length;
        return true;
    }

    internal static bool TryNormalizeMatToken(string token, out string normalized)
    {
        normalized = string.Empty;
        int matIndex = token.IndexOf(".mat", StringComparison.OrdinalIgnoreCase);
        if (matIndex < 0)
            return false;

        int end = matIndex + 4;
        if (end < token.Length && char.IsLetterOrDigit(token[end]))
            return false;

        string path = token.Substring(0, end).TrimStart('\\');
        if (ContainsInvalidPathCharacters(path))
            return false;
        if (!path.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
        {
            path = path.StartsWith("Materials\\", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine("Data", path)
                : Path.Combine("Data", "Materials", path);
        }

        normalized = NormalizePath(path);
        return true;
    }

    internal static bool TryNormalizeMeshToken(string token, out string normalized)
    {
        normalized = string.Empty;
        string trimmed = token.TrimStart('\\');
        if (ContainsInvalidPathCharacters(trimmed))
            return false;

        if (trimmed.EndsWith(".mesh", StringComparison.OrdinalIgnoreCase))
        {
            string path = trimmed.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : trimmed.StartsWith("Geometries\\", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine("Data", trimmed)
                    : Path.Combine("Data", "Geometries", trimmed);

            normalized = NormalizePath(path);
            return true;
        }

        if (trimmed.Contains('.') || !trimmed.Contains('\\'))
            return false;

        string stem = trimmed.TrimEnd();
        string rel = stem.StartsWith("geometries\\", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine("Data", stem + ".mesh")
            : Path.Combine("Data", "Geometries", stem + ".mesh");

        normalized = NormalizePath(rel);
        return true;
    }

    internal static bool TryNormalizeRigToken(string token, out string normalized)
    {
        return TryNormalizeDataTokenWithKnownRoots(token, ".rig", out normalized,
            "Meshes", "Animations", "Actors", "Data");
    }

    internal static bool TryNormalizeHavokToken(string token, out string normalized)
    {
        return TryNormalizeDataTokenWithKnownRoots(token, ".hvk", out normalized,
            "Meshes", "Animations", "Actors", "Data");
    }

    private static bool TryNormalizeDataTokenWithKnownRoots(
        string token,
        string extension,
        out string normalized,
        params string[] knownRootFolders)
    {
        normalized = string.Empty;

        int index = token.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        int end = index + extension.Length;
        if (end < token.Length && char.IsLetterOrDigit(token[end]))
            return false;

        string path = token.Substring(0, end).TrimStart('\\');
        if (ContainsInvalidPathCharacters(path))
            return false;
        if (!path.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string knownRoot in knownRootFolders)
            {
                if (!path.StartsWith(knownRoot + "\\", StringComparison.OrdinalIgnoreCase))
                    continue;

                path = Path.Combine("Data", path);
                normalized = NormalizePath(path);
                return true;
            }

            path = Path.Combine("Data", path);
        }

        normalized = NormalizePath(path);
        return true;
    }

    internal static string NormalizePath(string path) => path.Replace('/', '\\').TrimStart('\\');

    private static bool ContainsInvalidPathCharacters(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || value.Contains(':');
    }

    private static bool LooksLikeAssetToken(string token) => token.Contains('\\') || token.Contains('.') || token.Contains('/');

    private static void DeduplicateSort(List<string> values)
    {
        var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        values.Clear();
        values.AddRange(distinct);
    }
}
