using System.Text;
using DMM.AssetManagers.BSA;
using DMM.AssetManagers.Common;

namespace DMM.Tests.Unit;

public sealed class BsaArchiveTests
{
    [Fact]
    public void CreateReadExtractAndAppend_Workflow_Works()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "dmm-bsa-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            string archivePath = Path.Combine(tempRoot, "test.bsa");

            BsaArchive.Create(
                archivePath,
                new[]
                {
                    new BsaBuildFile
                    {
                        ArchivePath = "meshes/a.nif",
                        Data = Encoding.UTF8.GetBytes("mesh-data"),
                        Compression = BsaCompressionMode.Uncompressed
                    },
                    new BsaBuildFile
                    {
                        ArchivePath = "textures/a.dds",
                        Data = Encoding.UTF8.GetBytes("dds-data"),
                        Compression = BsaCompressionMode.Smart
                    },
                    new BsaBuildFile
                    {
                        ArchivePath = "sound/voice/sample.wem",
                        Data = Encoding.UTF8.GetBytes("wem-data"),
                        Compression = BsaCompressionMode.Smart
                    }
                },
                new BsaCreateOptions { ArchiveCompressedByDefault = true, TargetPlatform = BsaTargetPlatform.Pc });

            var index = BsaArchive.ReadIndex(archivePath);
            Assert.Equal(3, index.Count);

            var wem = index.Single(x => x.ArchiveInnerPath == "sound/voice/sample.wem");
            Assert.False(wem.IsCompressed);

            var ddsBytes = BsaArchive.ExtractFile(archivePath, "textures/a.dds");
            Assert.Equal("dds-data", Encoding.UTF8.GetString(ddsBytes));

            BsaArchive.AddOrReplaceFiles(
                archivePath,
                new[]
                {
                    new BsaBuildFile
                    {
                        ArchivePath = "textures/a.dds",
                        Data = Encoding.UTF8.GetBytes("dds-data-2"),
                        Compression = BsaCompressionMode.Compressed
                    },
                    new BsaBuildFile
                    {
                        ArchivePath = "textures/b.dds",
                        Data = Encoding.UTF8.GetBytes("dds-data-b"),
                        Compression = BsaCompressionMode.Compressed
                    }
                },
                new BsaCreateOptions { ArchiveCompressedByDefault = true, TargetPlatform = BsaTargetPlatform.Pc });

            string dds2 = Encoding.UTF8.GetString(BsaArchive.ExtractFile(archivePath, "textures/a.dds"));
            Assert.Equal("dds-data-2", dds2);

            var updatedIndex = BsaArchive.ReadIndex(archivePath);
            Assert.Equal(4, updatedIndex.Count);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Achlist_Parse_WithCompressionField_Works()
    {
        string content = """
            # comment
            meshes/a.nif|uncompressed
            textures/a.dds|smart
            sound/voice/sample.wem
            """;

        var entries = Achlist.Parse(content);
        Assert.Equal(3, entries.Count);
        Assert.Equal(BsaCompressionMode.Uncompressed, entries[0].Compression);
        Assert.Equal(BsaCompressionMode.Smart, entries[1].Compression);
        Assert.Equal(BsaCompressionMode.InheritArchive, entries[2].Compression);
    }
}
