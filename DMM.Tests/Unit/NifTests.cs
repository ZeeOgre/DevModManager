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

            byte[] bytes = BuildBytes(
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
            File.WriteAllBytes(nifPath, BuildBytes("geometries\\weapons\\hash_123"));

            var editor = new NifEditor(new NifReader());
            var writer = new NifWriter();
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("Data", "Geometries", "DarkStar", "folder", "sample", "hash_123.mesh"), plan[0].DestinationMeshPath, StringComparison.OrdinalIgnoreCase);
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
    public void Editor_BuildDeduplicateStringPlan_Uses_Lowest_Index()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "sample.nif");
            File.WriteAllBytes(nifPath, BuildBytes("A", "B", "A", "C", "B"));

            var editor = new NifEditor(new NifReader());
            NifStringRewritePlan plan = editor.BuildDeduplicateStringPlan(nifPath);

            Assert.True(plan.Remap.Count >= 2);
            Assert.Contains(plan.Remap, kvp => kvp.Value < kvp.Key);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_FindInvalidMatReferences_Returns_String_Index()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "sample.nif");
            File.WriteAllBytes(nifPath, BuildBytes("materials\\darkstar\\missing.mat"));

            var editor = new NifEditor(new NifReader());
            var invalid = editor.FindInvalidMatReferences(nifPath, root);

            Assert.Single(invalid);
            Assert.Equal("Data\\Materials\\darkstar\\missing.mat", invalid[0].MatPath);
            Assert.True(invalid[0].StringIndex >= 0);
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
