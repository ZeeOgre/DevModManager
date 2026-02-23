using System.Buffers.Binary;
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

    // Rewrites typed sized-string payloads (length-prefixed), allowing arbitrary replacement lengths.
    public int RewriteStringsInPlace(string nifPath, IReadOnlyDictionary<string, string> replacements)
    {
        if (nifPath == null) throw new ArgumentNullException(nameof(nifPath));
        if (replacements == null) throw new ArgumentNullException(nameof(replacements));
        if (!File.Exists(nifPath)) throw new FileNotFoundException("NIF not found", nifPath);

        byte[] bytes = File.ReadAllBytes(nifPath);
        IReadOnlyList<NifSerializedString> strings = NifReader.ReadSerializedStrings(bytes);

        int rewritten = 0;
        byte[] current = bytes;

        foreach (NifSerializedString entry in strings.OrderByDescending(x => x.Offset))
        {
            if (!replacements.TryGetValue(entry.Value, out string? replacement))
                continue;

            byte[] repl = Encoding.ASCII.GetBytes(replacement);
            if (entry.PrefixSize == 2 && repl.Length > ushort.MaxValue)
                throw new InvalidOperationException($"Replacement '{replacement}' exceeds 16-bit sized-string capacity.");

            current = ReplaceSerializedString(current, entry, repl);
            rewritten++;
        }

        if (rewritten > 0)
            File.WriteAllBytes(nifPath, current);

        return rewritten;
    }

    private static byte[] ReplaceSerializedString(byte[] source, NifSerializedString entry, byte[] replacement)
    {
        int payloadStart = entry.Offset + entry.PrefixSize;
        int payloadEnd = payloadStart + entry.Length;

        byte[] output = new byte[source.Length - entry.Length + replacement.Length];

        Buffer.BlockCopy(source, 0, output, 0, entry.Offset);

        if (entry.PrefixSize == 4)
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(entry.Offset, 4), replacement.Length);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(entry.Offset, 2), checked((ushort)replacement.Length));

        Buffer.BlockCopy(replacement, 0, output, payloadStart, replacement.Length);

        int tailLen = source.Length - payloadEnd;
        Buffer.BlockCopy(source, payloadEnd, output, payloadStart + replacement.Length, tailLen);

        return output;
    }
}
