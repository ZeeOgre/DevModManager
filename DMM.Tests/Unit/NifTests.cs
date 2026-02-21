using System.Text;
using DMM.AssetManagers.NIF;

namespace DMM.Tests.Unit;

public sealed class NifTests
{
    [Fact]
    public void Read_Extracts_Mats_And_Meshes()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "sample.nif");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);

            byte[] bytes = BuildBytes(
                "materials\\darkstar\\foo.mat",
                "geometries\\weapons\\hash_123",
                "textures\\random.dds");
            File.WriteAllBytes(nifPath, bytes);

            var nif = new NifReader();
            var result = nif.Read(nifPath);

            Assert.Contains("Data\\Materials\\darkstar\\foo.mat", result.Mats, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Data\\Geometries\\weapons\\hash_123.mesh", result.Meshes, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildReadableMeshCopyPlan_Creates_Deterministic_Destination()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "folder", "sample.nif");
            string sourceMesh = Path.Combine(root, "Data", "Geometries", "weapons", "hash_123.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(sourceMesh)!);

            File.WriteAllBytes(sourceMesh, [1, 2, 3]);
            File.WriteAllBytes(nifPath, BuildBytes("geometries\\weapons\\hash_123"));

            var nif = new NifReader();
            var plan = nif.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("Data", "Geometries", "DarkStar", "folder", "sample", "hash_123.mesh"), plan[0].DestinationMeshPath, StringComparison.OrdinalIgnoreCase);

            int copied = nif.ExecuteReadableMeshCopyPlan(plan);
            Assert.Equal(1, copied);
            Assert.True(File.Exists(plan[0].DestinationMeshPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static byte[] BuildBytes(params string[] tokens)
    {
        using var ms = new MemoryStream();
        foreach (var token in tokens)
        {
            ms.WriteByte(0);
            ms.Write(Encoding.ASCII.GetBytes(token));
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
