using System.Text;
using DMM.AssetManagers;
using DMM.AssetManagers.Common;

namespace DMM.Tests.Unit;

public sealed class Ba2ArchiveTests
{
    [Fact]
    public void CreateReadExtractAndAppend_Workflow_Works()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "dmm-ba2-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            string archivePath = Path.Combine(tempRoot, "test.ba2");

            BA2Archive.Create(
                archivePath,
                new[]
                {
                    new BA2BuildFile
                    {
                        ArchivePath = "meshes/a.nif",
                        Data = Encoding.UTF8.GetBytes("mesh-data"),
                        Compression = BA2CompressionMode.Uncompressed
                    },
                    new BA2BuildFile
                    {
                        ArchivePath = "textures/a.dds",
                        Data = Encoding.UTF8.GetBytes("dds-data"),
                        Compression = BA2CompressionMode.Smart
                    },
                    new BA2BuildFile
                    {
                        ArchivePath = "sound/voice/sample.wem",
                        Data = Encoding.UTF8.GetBytes("wem-data"),
                        Compression = BA2CompressionMode.Smart
                    }
                },
                new BA2CreateOptions { ArchiveCompressedByDefault = true, TargetPlatform = BA2TargetPlatform.Pc });

            var index = BA2Archive.ReadBuildIndex(archivePath);
            Assert.Equal(3, index.Count);

            var wem = index.Single(x => x.ArchiveInnerPath == "sound/voice/sample.wem");
            Assert.False(wem.IsCompressed);

            var ddsBytes = BA2Archive.ExtractBuiltFile(archivePath, "textures/a.dds");
            Assert.Equal("dds-data", Encoding.UTF8.GetString(ddsBytes));

            BA2Archive.AddOrReplaceFiles(
                archivePath,
                new[]
                {
                    new BA2BuildFile
                    {
                        ArchivePath = "textures/a.dds",
                        Data = Encoding.UTF8.GetBytes("dds-data-2"),
                        Compression = BA2CompressionMode.Compressed
                    },
                    new BA2BuildFile
                    {
                        ArchivePath = "textures/b.dds",
                        Data = Encoding.UTF8.GetBytes("dds-data-b"),
                        Compression = BA2CompressionMode.Compressed
                    }
                },
                new BA2CreateOptions { ArchiveCompressedByDefault = true, TargetPlatform = BA2TargetPlatform.Pc });

            string dds2 = Encoding.UTF8.GetString(BA2Archive.ExtractBuiltFile(archivePath, "textures/a.dds"));
            Assert.Equal("dds-data-2", dds2);

            var updatedIndex = BA2Archive.ReadBuildIndex(archivePath);
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
        Assert.Equal(BA2CompressionMode.Uncompressed, entries[0].Compression);
        Assert.Equal(BA2CompressionMode.Smart, entries[1].Compression);
        Assert.Equal(BA2CompressionMode.InheritArchive, entries[2].Compression);
    }
}
