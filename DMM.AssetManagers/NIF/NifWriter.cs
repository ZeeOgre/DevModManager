using System.Buffers.Binary;
using System.Text;

namespace DMM.AssetManagers.NIF;

public sealed class NifWriter
{
    private readonly record struct PendingRewrite(NifSerializedString Entry, byte[] ReplacementBytes);

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

    // Rewrites typed sized-string payloads (length-prefixed), allowing arbitrary replacement lengths.
    public int RewriteStringsInPlace(string nifPath, IReadOnlyDictionary<string, string> replacements)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (replacements == null) throw new ArgumentNullException(nameof(replacements));
        if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

        byte[] bytes = File.ReadAllBytes(nifPath);
        IReadOnlyList<NifSerializedString> strings = NifReader.ReadSerializedStrings(bytes);

        var rewrites = new List<PendingRewrite>();

        foreach (NifSerializedString entry in strings)
        {
            if (!replacements.TryGetValue(entry.Value, out string? replacement))
                continue;

            byte[] repl = Encoding.ASCII.GetBytes(replacement);
            if (entry.PrefixSize == 2 && repl.Length > ushort.MaxValue)
                throw new InvalidOperationException($"Replacement '{replacement}' exceeds 16-bit sized-string capacity.");

            rewrites.Add(new PendingRewrite(entry, repl));
        }

        if (rewrites.Count == 0)
            return 0;

        byte[] rewrittenBytes = RewriteAllSerializedStrings(bytes, rewrites);
        File.WriteAllBytes(nifPath, rewrittenBytes);

        return rewrites.Count;
    }

    private static byte[] RewriteAllSerializedStrings(byte[] source, IReadOnlyList<PendingRewrite> rewrites)
    {
        List<PendingRewrite> ordered = rewrites
            .OrderBy(x => x.Entry.Offset)
            .ToList();

        int newLength = source.Length;
        foreach (PendingRewrite rewrite in ordered)
        {
            newLength += rewrite.ReplacementBytes.Length - rewrite.Entry.Length;
        }

        byte[] output = new byte[newLength];
        int sourceCursor = 0;
        int outputCursor = 0;

        foreach (PendingRewrite rewrite in ordered)
        {
            NifSerializedString entry = rewrite.Entry;
            int prefixStart = entry.Offset;
            int payloadStart = prefixStart + entry.PrefixSize;
            int payloadEnd = payloadStart + entry.Length;

            int copyLen = prefixStart - sourceCursor;
            if (copyLen < 0)
                throw new InvalidOperationException("Detected overlapping or out-of-order NIF string rewrites.");

            Buffer.BlockCopy(source, sourceCursor, output, outputCursor, copyLen);
            outputCursor += copyLen;

            if (entry.PrefixSize == 4)
                BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(outputCursor, 4), rewrite.ReplacementBytes.Length);
            else
                BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(outputCursor, 2), checked((ushort)rewrite.ReplacementBytes.Length));

            outputCursor += entry.PrefixSize;

            Buffer.BlockCopy(rewrite.ReplacementBytes, 0, output, outputCursor, rewrite.ReplacementBytes.Length);
            outputCursor += rewrite.ReplacementBytes.Length;

            sourceCursor = payloadEnd;
        }

        int tailLen = source.Length - sourceCursor;
        Buffer.BlockCopy(source, sourceCursor, output, outputCursor, tailLen);

        return output;
    }
}
