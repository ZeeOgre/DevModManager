using System.Text;
using DMM.AssetManagers.TES;

namespace DMM.Tests.Unit;

public sealed class TesFileTests
{
    [Fact]
    public void Read_Extracts_Mat_Path_From_Lmsw_Refl_Blob()
    {
        string root = CreateTempRoot();
        try
        {
            string pluginPath = Path.Combine(root, "sample.esp");
            File.WriteAllBytes(pluginPath, BuildLmswPlugin("Clothes\\DarkStar\\panel.mat"));

            var tes = new TESFile();
            var result = tes.Read(pluginPath);

            Assert.Contains("Data\\Materials\\Clothes\\DarkStar\\panel.mat", result.ReferencedMats, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] BuildLmswPlugin(string matToken)
    {
        byte[] reflBlob = BuildBlobWithEmbeddedNulls(matToken);

        using var payload = new MemoryStream();
        payload.Write(Encoding.ASCII.GetBytes("REFL"));
        payload.WriteByte((byte)(reflBlob.Length & 0xFF));
        payload.WriteByte((byte)((reflBlob.Length >> 8) & 0xFF));
        payload.Write(reflBlob);

        byte[] payloadBytes = payload.ToArray();

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        bw.Write(Encoding.ASCII.GetBytes("LMSW"));
        bw.Write(payloadBytes.Length); // record data size
        bw.Write(0); // flags
        bw.Write(0); // form id
        bw.Write(0); // revision
        bw.Write((ushort)0); // version
        bw.Write((ushort)0); // unknown
        bw.Write(payloadBytes);
        bw.Flush();

        return ms.ToArray();
    }

    private static byte[] BuildBlobWithEmbeddedNulls(string token)
    {
        using var ms = new MemoryStream();
        byte[] prefix = Encoding.ASCII.GetBytes("junk-");
        ms.Write(prefix);

        foreach (byte b in Encoding.ASCII.GetBytes(token))
        {
            ms.WriteByte(b);
            if (b == (byte)'\\')
            {
                ms.WriteByte(0);
            }
        }

        ms.Write(Encoding.ASCII.GetBytes("-tail"));
        return ms.ToArray();
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "dmm-tes-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
