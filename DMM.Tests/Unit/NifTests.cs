using DMM.AssetManagers.NIF;

namespace DMM.Tests.Unit;

public sealed class NifTests
{
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
}
