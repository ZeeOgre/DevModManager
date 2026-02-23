using System.Text;

namespace DMM.AssetManagers.NIF;

public sealed class NifWriter
{
    public int ExecuteReadableMeshCopyPlan(IEnumerable<NifReadableMeshCopy> copies, bool overwrite = true)
    {
        if (copies == null) throw new ArgumentNullException(nameof(copies));

        int copied = 0;
        foreach (NifReadableMeshCopy copy in copies)
        {
            if (!File.Exists(copy.SourceMeshPath))
                continue;

            string? dir = Path.GetDirectoryName(copy.DestinationMeshPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.Copy(copy.SourceMeshPath, copy.DestinationMeshPath, overwrite);
            copied++;
        }

        return copied;
    }

    // Best-effort binary patcher: rewrites null-terminated string tokens in-place.
    // This intentionally avoids broad binary reserialization until a dedicated block/index parser lands.
    public int RewriteStringsInPlace(string nifPath, IReadOnlyDictionary<string, string> replacements)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (replacements == null) throw new ArgumentNullException(nameof(replacements));
        if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

        byte[] bytes = File.ReadAllBytes(nifPath);
        int rewritten = 0;

        foreach ((int start, int length) in EnumerateAsciiSlices(bytes, 4))
        {
            string token = Encoding.ASCII.GetString(bytes, start, length);
            if (!replacements.TryGetValue(token, out string? replacement))
                continue;

            byte[] repl = Encoding.ASCII.GetBytes(replacement);
            if (repl.Length > length)
                throw new InvalidOperationException($"Replacement '{replacement}' is longer than source token '{token}'.");

            Array.Clear(bytes, start, length);
            Array.Copy(repl, 0, bytes, start, repl.Length);
            rewritten++;
        }

        if (rewritten > 0)
            File.WriteAllBytes(nifPath, bytes);

        return rewritten;
    }

    private static IEnumerable<(int start, int length)> EnumerateAsciiSlices(byte[] bytes, int minLen)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            if (bytes[i] < 32 || bytes[i] > 126)
            {
                i++;
                continue;
            }

            int start = i;
            while (i < bytes.Length && bytes[i] >= 32 && bytes[i] <= 126)
                i++;

            int len = i - start;
            if (len >= minLen)
                yield return (start, len);
        }
    }
}
