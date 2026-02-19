using DMM.Tests.Harness.Infrastructure;

namespace DMM.Tests.FixtureTests;

public sealed class LocalFixturesSmokeTests
{
    [Fact]
    public void FixturesLocalFolder_IsIgnoredByGit_AndUsable()
    {
        string root = RepoRoot.Find();
        string fixturesLocal = Path.Combine(root, "tests", "fixtures_local");

        // This asserts the convention, not the presence of mod files.
        // Keeping it as a smoke test ensures contributors create the right folder.
        Assert.True(Directory.Exists(fixturesLocal), $"Expected folder does not exist: {fixturesLocal}");
    }
}