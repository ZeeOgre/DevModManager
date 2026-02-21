using System.Text;
using System.Text.RegularExpressions;

namespace DMM.AssetManagers.NIF
{
    public sealed class NifReadResult
    {
        public string Path { get; init; } = "";
        public List<string> Mats { get; } = new();
        public List<string> Meshes { get; } = new();
        public List<string> OtherAssets { get; } = new();
    }

    public sealed class NifReadableMeshCopy
    {
        public string NifPath { get; init; } = "";
        public string SourceMeshPath { get; init; } = "";
        public string DestinationMeshPath { get; init; } = "";
        public string OriginalMeshToken { get; init; } = "";
    }

    public sealed class NifReader
    {
        private static readonly Regex PrintableTokenRegex = new(@"[\u0020-\u007E]{4,}", RegexOptions.Compiled);

        // Parse/validate the NIF file and populate metadata
        public NifReadResult Read(string nifPath)
        {
            if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
            if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

            byte[] bytes = File.ReadAllBytes(nifPath);
            var result = new NifReadResult { Path = nifPath };

            var mats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var meshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var others = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string token in ExtractPrintableStrings(bytes, 4))
            {
                string normalized = token.Replace('/', '\\').Trim();
                if (string.IsNullOrWhiteSpace(normalized))
                    continue;

                if (TryNormalizeMatToken(normalized, out string mat))
                {
                    mats.Add(mat);
                    continue;
                }

                if (TryNormalizeMeshToken(normalized, out string mesh))
                {
                    meshes.Add(mesh);
                    continue;
                }

                if (LooksLikeAssetToken(normalized))
                    others.Add(normalized.TrimStart('\\'));
            }

            result.Mats.AddRange(mats.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            result.Meshes.AddRange(meshes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            result.OtherAssets.AddRange(others.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            return result;
        }

        // High level: extract every referenced asset token from the NIF
        public IEnumerable<string> ExtractAll(string nifPath)
        {
            var read = Read(nifPath);
            return read.Mats.Concat(read.Meshes).Concat(read.OtherAssets)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        }

        // Return material tokens referenced by this NIF
        public IEnumerable<string> ExtractMat(string nifPath)
        {
            return Read(nifPath).Mats;
        }

        // Return mesh tokens/stems referenced by this NIF
        public IEnumerable<string> ExtractMesh(string nifPath)
        {
            return Read(nifPath).Meshes;
        }

        // Build deterministic mesh-copy plan:
        // Data\Meshes\<rel>\foo.nif + Data\Geometries\source\bar.mesh
        // -> Data\Geometries\<rel>\foo\bar.mesh
        public IEnumerable<NifReadableMeshCopy> BuildReadableMeshCopyPlan(string nifPath, string gameRoot)
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

            var read = Read(fullNifPath);
            var planned = new List<NifReadableMeshCopy>();

            foreach (string meshToken in read.Meshes)
            {
                string fullSourceMesh = Path.Combine(fullGameRoot, meshToken);
                if (!File.Exists(fullSourceMesh))
                    continue;

                string blockName = Path.GetFileNameWithoutExtension(meshToken);
                string destRel = Path.Combine("Data", "Geometries", nifDirRel, nifBase, blockName + ".mesh");
                string fullDest = Path.Combine(fullGameRoot, destRel);

                planned.Add(new NifReadableMeshCopy
                {
                    NifPath = fullNifPath,
                    SourceMeshPath = fullSourceMesh,
                    DestinationMeshPath = fullDest,
                    OriginalMeshToken = meshToken
                });
            }

            return planned;
        }

        public int ExecuteReadableMeshCopyPlan(IEnumerable<NifReadableMeshCopy> copies, bool overwrite = true)
        {
            if (copies == null) throw new ArgumentNullException(nameof(copies));
            int copied = 0;

            foreach (var copy in copies)
            {
                if (copy == null) continue;
                if (!File.Exists(copy.SourceMeshPath)) continue;

                string? dir = Path.GetDirectoryName(copy.DestinationMeshPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                File.Copy(copy.SourceMeshPath, copy.DestinationMeshPath, overwrite);
                copied++;
            }

            return copied;
        }

        private static bool LooksLikeAssetToken(string token)
        {
            return token.Contains('\\') || token.Contains('.') || token.Contains('/');
        }

        private static bool TryNormalizeMatToken(string token, out string normalized)
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

        private static bool TryNormalizeMeshToken(string token, out string normalized)
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

        private static string NormalizePath(string path)
        {
            return path.Replace('/', '\\').TrimStart('\\');
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
}
