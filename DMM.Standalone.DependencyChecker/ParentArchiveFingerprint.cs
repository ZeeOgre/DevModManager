using System.Buffers;
using System.Diagnostics;
using System.IO.Hashing;

namespace DmmDep;

internal enum ParentArchiveValidationStatus
{
    Missing,
    UnchangedByMetadata,
    UnchangedByHash,
    Changed
}

internal sealed record ParentArchiveFingerprint(string FullPath, long FileLength, long LastWriteTimeUtcTicks, string? XxHash128);

internal sealed record ParentArchiveValidationResult(ParentArchiveValidationStatus Status, ParentArchiveFingerprint? Fingerprint);

/// <summary>Performs the inexpensive metadata check before streaming an XXH128 fingerprint.</summary>
internal static class ParentArchiveFingerprintService
{
    private const int BufferSize = 1024 * 1024;

    internal static ParentArchiveValidationResult Validate(
        string path,
        long? cachedLength,
        long? cachedLastWriteTimeUtcTicks,
        string? cachedHash,
        Action<string>? logger = null,
        CancellationToken cancellationToken = default)
    {
        logger ??= _ => { };
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            logger($"[Cache]   {Path.GetFileName(fullPath)} - missing archive");
            return new(ParentArchiveValidationStatus.Missing, null);
        }

        var info = new FileInfo(fullPath);
        var length = info.Length;
        var ticks = info.LastWriteTimeUtc.Ticks;
        if (cachedHash is not null && cachedLength == length && cachedLastWriteTimeUtcTicks == ticks)
        {
            logger($"[Cache]   {info.Name} - metadata match; hash skipped");
            return new(ParentArchiveValidationStatus.UnchangedByMetadata,
                new(fullPath, length, ticks, cachedHash));
        }

        logger($"[Cache]   {info.Name} - metadata mismatch (cached size={cachedLength?.ToString() ?? "none"}, ticks={cachedLastWriteTimeUtcTicks?.ToString() ?? "none"}; disk size={length}, ticks={ticks})");
        if (cachedLength is null || cachedLength != length)
        {
            logger($"[Cache]   {info.Name} - changed (file length mismatch); hash deferred to rebuild");
            return new(ParentArchiveValidationStatus.Changed, new(fullPath, length, ticks, null));
        }

        var hash = ComputeHash(fullPath, length, logger, cancellationToken);
        var fingerprint = new ParentArchiveFingerprint(fullPath, length, ticks, hash);

        if (cachedHash is not null && string.Equals(cachedHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            logger($"[Cache]   {info.Name} - hash match; cached metadata will be refreshed");
            return new(ParentArchiveValidationStatus.UnchangedByHash, fingerprint);
        }

        logger($"[Cache]   {info.Name} - hash mismatch{(cachedHash is null ? " (no cached hash)" : string.Empty)}");
        return new(ParentArchiveValidationStatus.Changed, fingerprint);
    }

    internal static ParentArchiveFingerprint EnsureHash(
        ParentArchiveFingerprint fingerprint,
        Action<string> logger,
        CancellationToken cancellationToken = default)
    {
        if (fingerprint.XxHash128 is not null)
            return fingerprint;

        return fingerprint with
        {
            XxHash128 = ComputeHash(fingerprint.FullPath, fingerprint.FileLength, logger, cancellationToken)
        };
    }

    internal static string ComputeHash(string path, long fileLength, Action<string> logger, CancellationToken cancellationToken)
    {
        logger($"[Cache]   {Path.GetFileName(path)} - hash start (size={fileLength} bytes)");
        var timer = Stopwatch.StartNew();
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, BufferSize, FileOptions.SequentialScan);
            var hasher = new XxHash128();
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hasher.Append(buffer.AsSpan(0, bytesRead));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var hash = Convert.ToHexString(hasher.GetCurrentHash());
            logger($"[Cache]   {Path.GetFileName(path)} - hash complete in {timer.Elapsed.TotalMilliseconds:F0} ms (size={fileLength} bytes)");
            return hash;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
