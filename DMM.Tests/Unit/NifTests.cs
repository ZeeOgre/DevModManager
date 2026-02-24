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
                "actors\\humanoid\\skeleton.rig",
                "animations\\behavior\\human.hvk",
                "textures\\random.dds");
            File.WriteAllBytes(nifPath, bytes);

            var reader = new NifReader();
            var result = reader.Read(nifPath);

            Assert.Contains("Data\\Materials\\darkstar\\foo.mat", result.Mats, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Data\\Geometries\\weapons\\hash_123.mesh", result.Meshes, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Data\\Actors\\humanoid\\skeleton.rig", result.Rigs, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Data\\Animations\\behavior\\human.hvk", result.Havoks, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }


    [Fact]
    public void Reader_ReadMeshStrings_Returns_Only_Mesh_Tokens()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "sample.nif");
            File.WriteAllBytes(nifPath, BuildSizedStringBytes(
                "materials\\darkstar\\foo.mat",
                "geometries\\darkstar\\block\\hash.mesh",
                "meshes\\other.rig"));

            var reader = new NifReader();
            var meshEntries = reader.ReadMeshStrings(nifPath);

            Assert.Single(meshEntries);
            Assert.Equal("geometries\\darkstar\\block\\hash.mesh", meshEntries[0].RawToken);
            Assert.Equal("Data\\Geometries\\darkstar\\block\\hash.mesh", meshEntries[0].NormalizedToken);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Reader_ExtractAll_Includes_Rig_And_Havok_Tokens()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "sample.nif");
            File.WriteAllBytes(nifPath, BuildSizedStringBytes(
                "actors\\humanoid\\skeleton.rig",
                "animations\\behavior\\human.hvk"));

            var reader = new NifReader();
            var all = reader.ExtractAll(nifPath).ToList();

            Assert.Contains("Data\\Actors\\humanoid\\skeleton.rig", all, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Data\\Animations\\behavior\\human.hvk", all, StringComparer.OrdinalIgnoreCase);
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
    public void Editor_BuildReadableMeshCopyPlan_Supports_Meshes_Root_Without_Data_Folder()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Meshes", "Weapons", "Laser", "rifle.nif");
            string sourceMesh = Path.Combine(root, "Data", "Geometries", "weapons", "laser", "hash_987.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(sourceMesh)!);

            File.WriteAllBytes(sourceMesh, [1, 2, 3]);
            File.WriteAllBytes(nifPath, BuildSizedStringBytes("geometries\\weapons\\laser\\hash_987"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("Data", "Geometries", "Weapons", "Laser", "rifle", "hash_987.mesh"), plan[0].DestinationMeshPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Path.Combine("Geometries", "Weapons", "Laser", "rifle", "hash_987.mesh"), plan[0].RewrittenMeshToken);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Uses_ParentFolder_Name_For_Hashed_Mesh_File()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "terminal.nif");
            string sourceMesh = Path.Combine(root, "Data", "Geometries", "darkstar", "DarkStar_jmpz11_Terminal_WallAttach", "fe729b8c345d07f78938.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(sourceMesh)!);

            File.WriteAllBytes(sourceMesh, [1, 2, 3]);
            File.WriteAllBytes(nifPath, BuildSizedStringBytes("geometries\\darkstar\\DarkStar_jmpz11_Terminal_WallAttach\\fe729b8c345d07f78938.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.Equal(
                Path.Combine("Geometries", "DarkStar", "terminal", "DarkStar_jmpz11_Terminal_WallAttach.mesh"),
                plan[0].RewrittenMeshToken,
                ignoreCase: true);
            Assert.EndsWith(
                Path.Combine("Data", "Geometries", "DarkStar", "terminal", "DarkStar_jmpz11_Terminal_WallAttach.mesh"),
                plan[0].DestinationMeshPath,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }


    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Collision_Suffixes_Start_At_One()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "terminal.nif");
            string mesh1 = Path.Combine(root, "Data", "Geometries", "darkstar", "WallAttach", "aaaaaaaaaaaaaaaaaaaa.mesh");
            string mesh2 = Path.Combine(root, "Data", "Geometries", "darkstar", "WallAttach", "bbbbbbbbbbbbbbbbbbbb.mesh");
            string mesh3 = Path.Combine(root, "Data", "Geometries", "darkstar", "WallAttach", "cccccccccccccccccccc.mesh");
            string mesh4 = Path.Combine(root, "Data", "Geometries", "darkstar", "WallAttach", "dddddddddddddddddddd.mesh");

            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(mesh1)!);
            File.WriteAllBytes(mesh1, [1]);
            File.WriteAllBytes(mesh2, [1]);
            File.WriteAllBytes(mesh3, [1]);
            File.WriteAllBytes(mesh4, [1]);

            File.WriteAllBytes(nifPath, BuildSizedStringBytes(
                "geometries\\darkstar\\WallAttach\\aaaaaaaaaaaaaaaaaaaa.mesh",
                "geometries\\darkstar\\WallAttach\\bbbbbbbbbbbbbbbbbbbb.mesh",
                "geometries\\darkstar\\WallAttach\\cccccccccccccccccccc.mesh",
                "geometries\\darkstar\\WallAttach\\dddddddddddddddddddd.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Equal(4, plan.Count);
            Assert.EndsWith(Path.Combine("DarkStar", "terminal", "WallAttach.mesh"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "terminal", "WallAttach_1.mesh"), plan[1].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "terminal", "WallAttach_2.mesh"), plan[2].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "terminal", "WallAttach_3.mesh"), plan[3].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Uses_SpellStyle_ObjectName_Lod_Naming_When_Available()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "activators", "invis_museumbutton01.nif");
            string mesh1 = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "aaaaaaaaaaaaaaaaaaaa.mesh");
            string mesh2 = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "bbbbbbbbbbbbbbbbbbbb.mesh");

            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(mesh1)!);
            File.WriteAllBytes(mesh1, [1]);
            File.WriteAllBytes(mesh2, [1]);

            File.WriteAllBytes(nifPath, BuildSizedStringBytes(
                "L2_Museum Button:1",
                "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh",
                "L2_Museum Button:1",
                "geometries\\darkstar\\src\\bbbbbbbbbbbbbbbbbbbb.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Equal(2, plan.Count);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "invis_museumbutton01", "l2_museumbutton_1_lod1.mesh"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "invis_museumbutton01", "l2_museumbutton_1_lod2.mesh"), plan[1].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Includes_Entries_When_Source_Mesh_Is_Missing()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "activators", "invis_museumbutton01.nif");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);

            File.WriteAllBytes(nifPath, BuildSizedStringBytes(
                "L2_Museum Button:1",
                "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "invis_museumbutton01", "l2_museumbutton_1_lod1.mesh"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(plan[0].SourceMeshPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Writer_RewriteStringsInPlace_Rewrites_Multiple_Tokens_In_One_Pass()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "sample.nif");
            File.WriteAllBytes(nifPath, BuildSizedStringBytes("a\\b", "c\\d", "keep"));

            var writer = new NifWriter();
            int rewritten = writer.RewriteStringsInPlace(nifPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["a\\b"] = "first\\replacement.mesh",
                ["c\\d"] = "second\\replacement.mesh"
            });

            Assert.Equal(2, rewritten);

            byte[] bytes = File.ReadAllBytes(nifPath);
            var strings = NifReader.ReadSerializedStrings(bytes).Select(x => x.Value).ToList();
            Assert.Contains("first\\replacement.mesh", strings, StringComparer.Ordinal);
            Assert.Contains("second\\replacement.mesh", strings, StringComparer.Ordinal);
            Assert.Contains("keep", strings, StringComparer.Ordinal);
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
