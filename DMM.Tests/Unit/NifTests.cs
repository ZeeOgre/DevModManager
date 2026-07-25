using DMM.AssetManagers.NIF;
using System.Text;

namespace DMM.Tests.Unit;

public sealed class NifTests
{
    [Fact]
    public void Reader_Unions_All_Four_Serialized_Starfield_Geometry_Meshes()
    {
        string path = Path.GetTempFileName();
        try
        {
            string[] meshTokens = Enumerable.Range(1, 4)
                .Select(lod => $"zeeogre\\zo_combinedstoragecontainer\\outpoststorageliquid01sm_mesh_1_lod{lod}.mesh")
                .ToArray();
            File.WriteAllBytes(path, BuildStarfieldGeometryNif(meshTokens));

            var reader = new NifReader();
            IReadOnlyList<NifMeshStringEntry> structuralMeshes = reader.ReadMeshStrings(path);
            NifReadResult result = reader.Read(path);

            Assert.Equal(4, structuralMeshes.Count);
            Assert.Equal(
                meshTokens.Select(token => "Data\\Geometries\\" + token),
                structuralMeshes.Select(mesh => mesh.NormalizedToken));
            Assert.Equal(4, result.Meshes.Count);
            Assert.Equal(4, result.Meshes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains(result.Meshes, mesh => mesh.EndsWith("_lod4.mesh", StringComparison.OrdinalIgnoreCase));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Reader_Recovers_SeventyNine_Complete_NonSparse_Lod_Groups()
    {
        string path = Path.GetTempFileName();
        try
        {
            string[][] groups = Enumerable.Range(0, 79)
                .Select(group => Enumerable.Range(1, 4)
                    .Select(lod => $"zeeogre\\zo_combinedstoragecontainer\\outpoststoragesolid01sm_mesh_{group}_lod{lod}.mesh")
                    .ToArray())
                .ToArray();
            File.WriteAllBytes(path, BuildStarfieldGeometryNifGroups(groups));

            NifReadResult result = new NifReader().Read(path);

            Assert.Equal(316, result.Meshes.Count);
            Assert.Equal(316, result.Meshes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            for (int lod = 1; lod <= 4; lod++)
                Assert.Equal(79, result.Meshes.Count(mesh => mesh.EndsWith($"_lod{lod}.mesh", StringComparison.OrdinalIgnoreCase)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Reader_Does_Not_Use_Printable_String_Scanning_When_Nifly_Cannot_Parse_A_File()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "materials\\not-a-nif.mat\nanimations\\not-a-nif.hkx");
            NifReadResult result = new NifReader().Read(path);

            Assert.False(result.Diagnostics.IsComplete);
            Assert.Empty(result.Mats);
            Assert.Empty(result.Havoks);
            Assert.Empty(result.Meshes);
        }
        finally { File.Delete(path); }
    }

    private static byte[] BuildStarfieldGeometryNif(IReadOnlyList<string?> meshTokens)
        => BuildStarfieldGeometryNifGroups([meshTokens]);

    private static byte[] BuildStarfieldGeometryNifGroups(IReadOnlyList<IReadOnlyList<string?>> groups)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        writer.Write(0x14020007u);
        writer.Write((byte)1);
        writer.Write(12u);
        writer.Write(groups.Count);
        writer.Write(170u);
        WriteSized1(writer, string.Empty);
        writer.Write(0u);
        WriteSized1(writer, string.Empty);
        WriteSized1(writer, string.Empty);
        writer.Write((ushort)1);
        WriteSized4(writer, "BSGeometry");
        foreach (IReadOnlyList<string?> _ in groups)
            writer.Write((ushort)0);

        long blockSizesPosition = stream.Position;
        foreach (IReadOnlyList<string?> _ in groups)
            writer.Write(0);
        writer.Write(0u); // Header string count.
        writer.Write(0u); // Maximum string length.
        writer.Write(0);  // Group count.

        var blockSizes = new List<int>(groups.Count);
        foreach (IReadOnlyList<string?> meshTokens in groups)
        {
            long blockStart = stream.Position;
            writer.Write(0);            // NiObjectNET name.
            writer.Write(0u);           // Extra-data count.
            writer.Write(-1);           // Controller.
            writer.Write(0u);           // External geometry flags.
            writer.Write(new byte[56]); // NiAVObject transform and collision reference.
            writer.Write(new byte[52]); // BSGeometry bounds and three references.
            foreach (string? token in meshTokens)
            {
                if (token is null)
                {
                    writer.Write((byte)0);
                    continue;
                }
                writer.Write((byte)1);
                writer.Write(0u); // Indices size.
                writer.Write(0u); // Vertex count.
                writer.Write(0u); // Mesh flags.
                WriteSized4(writer, token);
            }
            blockSizes.Add(checked((int)(stream.Position - blockStart)));
        }

        stream.Position = blockSizesPosition;
        foreach (int blockSize in blockSizes)
            writer.Write(blockSize);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteSized1(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteSized4(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
