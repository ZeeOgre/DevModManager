using DmmDep;

namespace DMM.Tests.Unit;

public sealed class ParentArchiveFingerprintTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"dmmdeps-fingerprint-{Guid.NewGuid():N}");

    public ParentArchiveFingerprintTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void SameMetadataSkipsHashing()
    {
        var path = CreateFile("same.ba2", "content");
        var info = new FileInfo(path);
        var logs = new List<string>();

        var result = ParentArchiveFingerprintService.Validate(path, info.Length, info.LastWriteTimeUtc.Ticks, "CACHED", logs.Add);

        Assert.Equal(ParentArchiveValidationStatus.UnchangedByMetadata, result.Status);
        Assert.DoesNotContain(logs, line => line.Contains("hash start"));
    }

    [Fact]
    public void ChangedTimestampWithSameContentMatchesHash()
    {
        var path = CreateFile("timestamp.ba2", "content");
        var first = ParentArchiveFingerprintService.Validate(path, null, null, null);
        var firstFingerprint = ParentArchiveFingerprintService.EnsureHash(first.Fingerprint!, _ => { });
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));

        var result = ParentArchiveFingerprintService.Validate(path, firstFingerprint.FileLength,
            firstFingerprint.LastWriteTimeUtcTicks, firstFingerprint.XxHash128);

        Assert.Equal(ParentArchiveValidationStatus.UnchangedByHash, result.Status);
    }

    [Fact]
    public void ChangedLengthIsChanged()
    {
        var path = CreateFile("length.ba2", "one");
        var first = ParentArchiveFingerprintService.Validate(path, null, null, null).Fingerprint!;
        first = ParentArchiveFingerprintService.EnsureHash(first, _ => { });
        File.AppendAllText(path, "two");

        var logs = new List<string>();
        var result = ParentArchiveFingerprintService.Validate(path, first.FileLength, first.LastWriteTimeUtcTicks, first.XxHash128, logs.Add);

        Assert.Equal(ParentArchiveValidationStatus.Changed, result.Status);
        Assert.DoesNotContain(logs, line => line.Contains("hash start"));

        var rebuilt = ParentArchiveFingerprintService.EnsureHash(result.Fingerprint!, logs.Add);
        _ = ParentArchiveFingerprintService.EnsureHash(rebuilt, logs.Add);
        Assert.Single(logs, line => line.Contains("hash start"));
    }

    [Fact]
    public void ChangedContentWithSameLengthIsChanged()
    {
        var path = CreateFile("content.ba2", "aaaa");
        var first = ParentArchiveFingerprintService.Validate(path, null, null, null).Fingerprint!;
        first = ParentArchiveFingerprintService.EnsureHash(first, _ => { });
        File.WriteAllText(path, "bbbb");
        File.SetLastWriteTimeUtc(path, new DateTime(first.LastWriteTimeUtcTicks, DateTimeKind.Utc).AddSeconds(2));

        Assert.Equal(ParentArchiveValidationStatus.Changed,
            ParentArchiveFingerprintService.Validate(path, first.FileLength, first.LastWriteTimeUtcTicks, first.XxHash128).Status);
    }

    [Fact]
    public void MissingFileIsMissing()
    {
        Assert.Equal(ParentArchiveValidationStatus.Missing,
            ParentArchiveFingerprintService.Validate(Path.Combine(_directory, "missing.ba2"), 0, 0, "HASH").Status);
    }

    [Fact]
    public void FirstSeenArchiveComputesHash()
    {
        var result = ParentArchiveFingerprintService.Validate(CreateFile("new.ba2", "new"), null, null, null);
        var rebuilt = ParentArchiveFingerprintService.EnsureHash(result.Fingerprint!, _ => { });

        Assert.Equal(ParentArchiveValidationStatus.Changed, result.Status);
        Assert.Matches("^[0-9A-F]{32}$", rebuilt.XxHash128!);
    }

    [Fact]
    public void CancellationDuringHashingIsObserved()
    {
        var path = CreateFile("cancel.ba2", new string('x', 2 * 1024 * 1024));
        using var cancellation = new CancellationTokenSource();

        var fingerprint = ParentArchiveFingerprintService.Validate(path, null, null, null).Fingerprint!;
        Assert.Throws<OperationCanceledException>(() => ParentArchiveFingerprintService.EnsureHash(
            fingerprint, message => { if (message.Contains("hash start")) cancellation.Cancel(); }, cancellation.Token));
    }

    [Fact]
    public void HashIsStableAcrossRepeatedRuns()
    {
        var path = CreateFile("stable.ba2", "stable hash input");

        var first = ParentArchiveFingerprintService.EnsureHash(
            ParentArchiveFingerprintService.Validate(path, null, null, null).Fingerprint!, _ => { }).XxHash128;
        var second = ParentArchiveFingerprintService.EnsureHash(
            ParentArchiveFingerprintService.Validate(path, null, null, null).Fingerprint!, _ => { }).XxHash128;

        Assert.Equal(first, second);
    }

    private string CreateFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
