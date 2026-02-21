using System.Text;
using System.Text.RegularExpressions;

namespace DMM.AssetManagers.NIF;

public sealed class NifReader
{
    private static readonly Regex PrintableTokenRegex = new(@"[\u0020-\u007E]{4,}", RegexOptions.Compiled);

    public NifReadResult Read(string nifPath)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

        byte[] bytes = File.ReadAllBytes(nifPath);
        var result = new NifReadResult { Path = nifPath };

        foreach (NifStringEntry entry in ReadStringTable(nifPath))
        {
            string normalized = entry.Value.Replace('/', '\\').Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (TryNormalizeMatToken(normalized, out string mat))
            {
                result.Mats.Add(mat);
                continue;
            }

            if (TryNormalizeMeshToken(normalized, out string mesh))
            {
                result.Meshes.Add(mesh);
                continue;
            }

            if (LooksLikeAssetToken(normalized))
                result.OtherAssets.Add(normalized.TrimStart('\\'));
        }

        DeduplicateSort(result.Mats);
        DeduplicateSort(result.Meshes);
        DeduplicateSort(result.OtherAssets);

        return result;
    }

    public IReadOnlyList<NifStringEntry> ReadStringTable(string nifPath)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

        byte[] bytes = File.ReadAllBytes(nifPath);
        var values = ExtractPrintableStrings(bytes, 4).Distinct(StringComparer.Ordinal).ToList();
        var entries = new List<NifStringEntry>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            entries.Add(new NifStringEntry { Index = i, Value = values[i] });
        }

        return entries;
    }

    public IEnumerable<string> ExtractAll(string nifPath)
    {
        var read = Read(nifPath);
        return read.Mats.Concat(read.Meshes).Concat(read.OtherAssets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<string> ExtractMat(string nifPath) => Read(nifPath).Mats;

    public IEnumerable<string> ExtractMesh(string nifPath) => Read(nifPath).Meshes;

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

    internal static string NormalizePath(string path) => path.Replace('/', '\\').TrimStart('\\');

    private static bool LooksLikeAssetToken(string token) => token.Contains('\\') || token.Contains('.') || token.Contains('/');

    private static void DeduplicateSort(List<string> values)
    {
        var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        values.Clear();
        values.AddRange(distinct);
    }

    private static IEnumerable<string> ExtractPrintableStrings(byte[] bytes, int minLen)
    {
        var sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            if (b >= 32 && b <= 126)
            {
                sb.Append((char)b);
                continue;
            }

            if (sb.Length >= minLen)
            {
                string candidate = sb.ToString();
                foreach (Match match in PrintableTokenRegex.Matches(candidate))
                {
                    if (match.Value.Length >= minLen)
                        yield return match.Value;
                }
            }
            sb.Clear();
        }

        if (sb.Length >= minLen)
        {
            string candidate = sb.ToString();
            foreach (Match match in PrintableTokenRegex.Matches(candidate))
            {
                if (match.Value.Length >= minLen)
                    yield return match.Value;
            }
        }
    }
}
