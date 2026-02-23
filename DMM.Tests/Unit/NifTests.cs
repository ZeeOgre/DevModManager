using System.Buffers.Binary;
using System.Text;
using DMM.AssetManagers.NIF;

namespace DMM.Tests.Unit;

public sealed class NifTests
{
    [Fact]
    public void Reader_Read_Extracts_Mats_And_Meshes()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "sample.nif");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);

            byte[] bytes = BuildSizedStringBytes(
                "materials\\darkstar\\foo.mat",
                "geometries\\weapons\\hash_123",
                "textures\\random.dds");
            File.WriteAllBytes(nifPath, bytes);

            var reader = new NifReader();
            var result = reader.Read(nifPath);

            Assert.Contains("Data\\Materials\\darkstar\\foo.mat", result.Mats, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Data\\Geometries\\weapons\\hash_123.mesh", result.Meshes, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Creates_Deterministic_Destination()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "folder", "sample.nif");
            string sourceMesh = Path.Combine(root, "Data", "Geometries", "weapons", "hash_123.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(sourceMesh)!);

            File.WriteAllBytes(sourceMesh, [1, 2, 3]);
            File.WriteAllBytes(nifPath, BuildSizedStringBytes("geometries\\weapons\\hash_123"));

            var editor = new NifEditor(new NifReader());
            var writer = new NifWriter();
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("Data", "Geometries", "DarkStar", "folder", "sample", "hash_123.mesh"), plan[0].DestinationMeshPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("geometries\\weapons\\hash_123", plan[0].OriginalMeshToken);
            Assert.Equal("Data\\Geometries\\weapons\\hash_123.mesh", plan[0].OriginalMeshTokenNormalized);
            Assert.Equal(Path.Combine("Geometries", "DarkStar", "folder", "sample", "hash_123.mesh"), plan[0].RewrittenMeshToken);

            int copied = writer.ExecuteReadableMeshCopyPlan(plan);
            Assert.Equal(1, copied);
            Assert.True(File.Exists(plan[0].DestinationMeshPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Writer_RewriteStringsInPlace_Allows_Longer_Replacements_For_SizedStrings()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "sample.nif");
            string oldToken = "a\\b";
            string newToken = "Geometries\\darkstar\\sample\\block.mesh";
            File.WriteAllBytes(nifPath, BuildSizedStringBytes(oldToken));

            var writer = new NifWriter();
            int rewritten = writer.RewriteStringsInPlace(nifPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [oldToken] = newToken
            });

            Assert.Equal(1, rewritten);

            byte[] bytes = File.ReadAllBytes(nifPath);
            int len = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
            Assert.Equal(newToken.Length, len);
            string payload = Encoding.ASCII.GetString(bytes, 4, len);
            Assert.Equal(newToken, payload);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static byte[] BuildSizedStringBytes(params string[] tokens)
    {
        using var ms = new MemoryStream();
        foreach (var token in tokens)
        {
            byte[] b = Encoding.ASCII.GetBytes(token);
            Span<byte> len = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(len, b.Length);
            ms.Write(len);
            ms.Write(b);
            ms.WriteByte(0);
        }

        return ms.ToArray();
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "dmm-nif-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
