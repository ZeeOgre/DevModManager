using DMM.AssetManagers.MAT;

namespace DMM.Tests.Unit;

public sealed class MatTests
{
    [Fact]
    public void Read_Parses_Json_Texture_Tokens()
    {
        string root = CreateTempRoot();
        try
        {
            string matPath = Path.Combine(root, "sample.mat");
            File.WriteAllText(matPath,
                """
                {
                  "Layers": [
                    { "File": "textures/darkstar/a.dds" },
                    { "FileName": "textures\\darkstar\\b.png" },
                    { "Nested": { "Diffuse": "textures\\darkstar\\c.jpg" } }
                  ]
                }
                """);

            var mat = new MAT();
            var result = mat.Read(matPath);

            Assert.NotNull(result.RawJson);
            Assert.Contains("textures\\darkstar\\a.dds", result.TextureTokens, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("textures\\darkstar\\b.png", result.TextureTokens, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("textures\\darkstar\\c.jpg", result.TextureTokens, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_Falls_Back_To_Text_Extraction_For_Invalid_Json()
    {
        string root = CreateTempRoot();
        try
        {
            string matPath = Path.Combine(root, "broken.mat");
            File.WriteAllText(matPath, "file=textures/darkstar/fallback.dds; bogus={");

            var mat = new MAT();
            var result = mat.Read(matPath);

            Assert.Null(result.RawJson);
            Assert.Contains("textures\\darkstar\\fallback.dds", result.TextureTokens, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "dmm-mat-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
