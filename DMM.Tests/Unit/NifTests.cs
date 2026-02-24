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
    public void Reader_ReadStringTable_Includes_SizedString2_Block_Name_Payloads()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "sample.nif");
            File.WriteAllBytes(nifPath, BuildMixedSizedStringBytes(
                (2, "GenMachinery_CentralModuleD002:0"),
                (4, "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh")));

            var reader = new NifReader();
            var strings = reader.ReadStringTable(nifPath).Select(x => x.Value).ToList();

            Assert.Contains("GenMachinery_CentralModuleD002:0", strings, StringComparer.Ordinal);
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
            Assert.Equal(Path.Combine("Geometries", "DarkStar", "folder", "sample", "hash_123"), plan[0].RewrittenMeshToken);

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
            Assert.Equal(Path.Combine("Geometries", "Weapons", "Laser", "rifle", "hash_987"), plan[0].RewrittenMeshToken);
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
                Path.Combine("Geometries", "DarkStar", "terminal", "DarkStar_jmpz11_Terminal_WallAttach"),
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
            Assert.EndsWith(Path.Combine("DarkStar", "terminal", "WallAttach"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "terminal", "WallAttach_1"), plan[1].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "terminal", "WallAttach_2"), plan[2].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "terminal", "WallAttach_3"), plan[3].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
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
                "0c80fbd66e324f86581e",
                "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh",
                "L2_Museum Button:1",
                "geometries\\darkstar\\src\\bbbbbbbbbbbbbbbbbbbb.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Equal(2, plan.Count);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "invis_museumbutton01", "l2_museumbutton_1"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "invis_museumbutton01", "l2_museumbutton_1_1"), plan[1].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
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
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "invis_museumbutton01", "l2_museumbutton_1"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(plan[0].SourceMeshPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Prefers_Block_Name_Over_Nif_Class_Name()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "architecture", "landingpad", "slab80m.nif");
            string meshPath = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "aaaaaaaaaaaaaaaaaaaa.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath)!);
            File.WriteAllBytes(meshPath, [1]);

            File.WriteAllBytes(nifPath, BuildSizedStringBytes(
                "NiIntegerExtraData",
                "PavGenLandingStrMid023:8",
                "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("DarkStar", "architecture", "landingpad", "slab80m", "pavgenlandingstrmid023_8"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("niintegerextradata", plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Uses_SizedString2_Name_For_Target_File_Basename()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "activators", "activator_tall.nif");
            string meshPath = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "0c80fbd66e324f86581e.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath)!);
            File.WriteAllBytes(meshPath, [1]);

            File.WriteAllBytes(nifPath, BuildMixedSizedStringBytes(
                (2, "GenMachinery_CentralModuleD002:0"),
                (4, "geometries\\darkstar\\src\\0c80fbd66e324f86581e.mesh"),
                (2, "GenMachinery_CentralModuleD002:0"),
                (4, "geometries\\darkstar\\src\\1c40c7e82300b9b3e9e5.mesh")));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Equal(2, plan.Count);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "genmachinery_centralmoduled002_0"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "genmachinery_centralmoduled002_0_1"), plan[1].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Without_Reference_Falls_Back_To_Mesh_Derived_Name()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "activators", "activator_tall.nif");
            string meshPath = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "aaaaaaaaaaaaaaaaaaaa.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath)!);
            File.WriteAllBytes(meshPath, [1]);

            File.WriteAllBytes(nifPath, BuildMixedSizedStringBytes(
                (2, "GenMachinery_CentralModuleD002:0"),
                (2, "_t"),
                (4, "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh")));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "src"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Without_Block_Structure_Falls_Back_To_Mesh_Derived_Name()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "activators", "activator_tall.nif");
            string meshPath = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "aaaaaaaaaaaaaaaaaaaa.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath)!);
            File.WriteAllBytes(meshPath, [1]);

            File.WriteAllBytes(nifPath, BuildMixedSizedStringBytesWithIntRefs(
                (4, "GenMachinery_CentralModuleD002:0"),
                (4, "_t"),
                (4, "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh"),
                0));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "src"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Without_Block_Structure_Does_Not_Use_Short_Tag_Reference()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "activators", "activator_tall.nif");
            string meshPath = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "aaaaaaaaaaaaaaaaaaaa.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath)!);
            File.WriteAllBytes(meshPath, [1]);

            File.WriteAllBytes(nifPath, BuildMixedSizedStringBytesWithIntRefs(
                (4, "GenMachinery_CentralModuleD002:0"),
                (4, "_t"),
                (4, "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh"),
                1));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "src"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Uses_Block_Name_StringId_From_Bethesda_Header_Table()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "architecture", "landingpad", "slab60m.nif");
            string meshPath = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "aaaaaaaaaaaaaaaaaaaa.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath)!);
            File.WriteAllBytes(meshPath, [1]);

            File.WriteAllBytes(nifPath, BuildBethesdaLikeSingleBlockNif(
                "PavGenLandingStrMid018:8",
                "geometries\\darkstar\\src\\aaaaaaaaaaaaaaaaaaaa.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("DarkStar", "architecture", "landingpad", "slab60m", "pavgenlandingstrmid018_8"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Uses_Block_Name_Per_Block_And_Per_Mesh_Index()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "activators", "activator_tall.nif");
            string[] meshPaths =
            [
                Path.Combine(root, "Data", "Geometries", "darkstar", "src", "a1111111111111111111.mesh"),
                Path.Combine(root, "Data", "Geometries", "darkstar", "src", "a2222222222222222222.mesh"),
                Path.Combine(root, "Data", "Geometries", "darkstar", "src", "b1111111111111111111.mesh"),
                Path.Combine(root, "Data", "Geometries", "darkstar", "src", "b2222222222222222222.mesh")
            ];

            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            foreach (string meshPath in meshPaths)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(meshPath)!);
                File.WriteAllBytes(meshPath, [1]);
            }

            File.WriteAllBytes(nifPath, BuildBethesdaLikeTwoBlocksNif(
                "Box365:14",
                "FullDisplay008:0",
                "geometries\\darkstar\\src\\a1111111111111111111.mesh",
                "geometries\\darkstar\\src\\a2222222222222222222.mesh",
                "geometries\\darkstar\\src\\b1111111111111111111.mesh",
                "geometries\\darkstar\\src\\b2222222222222222222.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Equal(4, plan.Count);
            Assert.Contains(plan, x => x.RewrittenMeshToken.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "box365_14"), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(plan, x => x.RewrittenMeshToken.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "box365_14_1"), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(plan, x => x.RewrittenMeshToken.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "fulldisplay008_0"), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(plan, x => x.RewrittenMeshToken.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "fulldisplay008_0_1"), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_BuildReadableMeshCopyPlan_Prefers_Block_Name_When_First_Block_Int_Is_SuperParent_Name()
    {
        string root = CreateTempRoot();
        try
        {
            string nifPath = Path.Combine(root, "Data", "Meshes", "DarkStar", "activators", "activator_tall.nif");
            string meshPath = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "c1111111111111111111.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(nifPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath)!);
            File.WriteAllBytes(meshPath, [1]);

            File.WriteAllBytes(nifPath, BuildBethesdaLikeSingleBlockNifWithLeadingSuperParentNameRef(
                "OutpostMenuActivator01",
                "Box362:3",
                "geometries\\darkstar\\src\\c1111111111111111111.mesh"));

            var editor = new NifEditor(new NifReader());
            var plan = editor.BuildReadableMeshCopyPlan(nifPath, root).ToList();

            Assert.Single(plan);
            Assert.EndsWith(Path.Combine("DarkStar", "activators", "activator_tall", "box362_3"), plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("outpostmenuactivator01", plan[0].RewrittenMeshToken, StringComparison.OrdinalIgnoreCase);
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
    public void Writer_ExecuteReadableMeshCopyPlan_Moves_Source_To_Parsed_And_Copies_To_Target()
    {
        string root = CreateTempRoot();
        try
        {
            string source = Path.Combine(root, "Data", "Geometries", "darkstar", "src", "aaaaaaaaaaaaaaaaaaaa.mesh");
            string destination = Path.Combine(root, "Data", "Geometries", "DarkStar", "activators", "sample", "block.mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllBytes(source, [7, 8, 9]);

            var writer = new NifWriter();
            int copied = writer.ExecuteReadableMeshCopyPlan([
                new NifReadableMeshCopy
                {
                    SourceMeshPath = source,
                    DestinationMeshPath = destination
                }
            ]);

            Assert.Equal(1, copied);
            Assert.True(File.Exists(destination));

            string parsedSource = Path.Combine(root, "Data", "Geometries", "Parsed", "darkstar", "src", "aaaaaaaaaaaaaaaaaaaa.mesh");
            Assert.False(File.Exists(source));
            Assert.True(File.Exists(parsedSource));
            Assert.Equal(File.ReadAllBytes(parsedSource), File.ReadAllBytes(destination));
            Assert.False(Directory.Exists(Path.Combine(root, "Data", "Geometries", "darkstar", "src")));
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

    private static byte[] BuildMixedSizedStringBytes(params (int PrefixSize, string Value)[] tokens)
    {
        using var ms = new MemoryStream();
        foreach ((int prefixSize, string value) in tokens)
        {
            byte[] b = Encoding.ASCII.GetBytes(value);
            if (prefixSize == 2)
            {
                Span<byte> len = stackalloc byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(len, checked((ushort)b.Length));
                ms.Write(len);
            }
            else
            {
                Span<byte> len = stackalloc byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(len, b.Length);
                ms.Write(len);
            }

            ms.Write(b);
            ms.WriteByte(0);
        }

        return ms.ToArray();
    }

    private static byte[] BuildMixedSizedStringBytesWithIntRefs(
        (int PrefixSize, string Value) first,
        (int PrefixSize, string Value) second,
        (int PrefixSize, string Value) mesh,
        int referencedStringIndex)
    {
        using var ms = new MemoryStream();

        WriteSized(ms, first.PrefixSize, first.Value);
        WriteSized(ms, second.PrefixSize, second.Value);

        Span<byte> idx = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(idx, referencedStringIndex);
        ms.Write(idx);

        WriteSized(ms, mesh.PrefixSize, mesh.Value);
        return ms.ToArray();
    }

    private static byte[] BuildBethesdaLikeSingleBlockNif(string blockName, string meshToken)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        bw.Write(Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        bw.Write(0x14020007u);
        bw.Write((byte)1);
        bw.Write(12u);
        bw.Write(1);
        bw.Write(172u);
        WriteSized1(bw, "");
        bw.Write(0u);
        WriteSized1(bw, "");
        WriteSized1(bw, "");
        bw.Write((ushort)1);
        WriteSized4(bw, "BSGeometry");
        bw.Write((ushort)0);

        long blockSizePosition = ms.Position;
        bw.Write(0);

        bw.Write(2u);
        bw.Write((uint)Math.Max(blockName.Length, 1));
        WriteSized4(bw, blockName);
        WriteSized4(bw, "unused");

        bw.Write(0);

        long blockStart = ms.Position;
        bw.Write(0);
        bw.Write(0);
        WriteSized4(bw, meshToken);
        long blockEnd = ms.Position;

        ms.Position = blockSizePosition;
        bw.Write(checked((int)(blockEnd - blockStart)));
        bw.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildBethesdaLikeTwoBlocksNif(
        string blockNameA,
        string blockNameB,
        string meshA0,
        string meshA1,
        string meshB0,
        string meshB1)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        bw.Write(Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        bw.Write(0x14020007u);
        bw.Write((byte)1);
        bw.Write(12u);
        bw.Write(2);
        bw.Write(172u);
        WriteSized1(bw, "");
        bw.Write(0u);
        WriteSized1(bw, "");
        WriteSized1(bw, "");
        bw.Write((ushort)1);
        WriteSized4(bw, "BSGeometry");
        bw.Write((ushort)0);
        bw.Write((ushort)0);

        long blockSizePosA = ms.Position;
        bw.Write(0);
        long blockSizePosB = ms.Position;
        bw.Write(0);

        bw.Write(3u);
        bw.Write((uint)Math.Max(Math.Max(blockNameA.Length, blockNameB.Length), 1));
        WriteSized4(bw, blockNameA);
        WriteSized4(bw, blockNameB);
        WriteSized4(bw, "unused");

        bw.Write(0);

        long blockStartA = ms.Position;
        bw.Write(0);
        WriteSized4(bw, meshA0);
        WriteSized4(bw, meshA1);
        long blockEndA = ms.Position;

        long blockStartB = ms.Position;
        bw.Write(1);
        WriteSized4(bw, meshB0);
        WriteSized4(bw, meshB1);
        long blockEndB = ms.Position;

        ms.Position = blockSizePosA;
        bw.Write(checked((int)(blockEndA - blockStartA)));
        ms.Position = blockSizePosB;
        bw.Write(checked((int)(blockEndB - blockStartB)));

        bw.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildBethesdaLikeSingleBlockNifWithLeadingSuperParentNameRef(
        string superParentName,
        string blockName,
        string meshToken)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        bw.Write(Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        bw.Write(0x14020007u);
        bw.Write((byte)1);
        bw.Write(12u);
        bw.Write(1);
        bw.Write(172u);
        WriteSized1(bw, "");
        bw.Write(0u);
        WriteSized1(bw, "");
        WriteSized1(bw, "");
        bw.Write((ushort)1);
        WriteSized4(bw, "BSGeometry");
        bw.Write((ushort)0);

        long blockSizePosition = ms.Position;
        bw.Write(0);

        bw.Write(3u);
        bw.Write((uint)Math.Max(Math.Max(superParentName.Length, blockName.Length), 1));
        WriteSized4(bw, superParentName);
        WriteSized4(bw, blockName);
        WriteSized4(bw, "unused");

        bw.Write(0);

        long blockStart = ms.Position;
        bw.Write(0);
        bw.Write(1);
        WriteSized4(bw, meshToken);
        long blockEnd = ms.Position;

        ms.Position = blockSizePosition;
        bw.Write(checked((int)(blockEnd - blockStart)));
        bw.Flush();
        return ms.ToArray();
    }

    private static void WriteSized1(BinaryWriter bw, string value)
    {
        byte[] b = Encoding.ASCII.GetBytes(value);
        bw.Write(checked((byte)b.Length));
        bw.Write(b);
    }

    private static void WriteSized4(BinaryWriter bw, string value)
    {
        byte[] b = Encoding.ASCII.GetBytes(value);
        bw.Write(b.Length);
        bw.Write(b);
    }

    private static void WriteSized(MemoryStream ms, int prefixSize, string value)
    {
        byte[] b = Encoding.ASCII.GetBytes(value);
        if (prefixSize == 2)
        {
            Span<byte> len = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(len, checked((ushort)b.Length));
            ms.Write(len);
        }
        else
        {
            Span<byte> len = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(len, b.Length);
            ms.Write(len);
        }

        ms.Write(b);
        ms.WriteByte(0);
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "dmm-nif-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
