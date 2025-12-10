using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DMM.AssetManagers
{
    /// <summary>
    /// Represents a single asset entry inside a BA2 archive.
    /// </summary>
    public sealed class Ba2Entry
    {
        /// <summary>
        /// Full path to the .ba2 archive on disk.
        /// </summary>
        public string ArchivePath { get; init; } = "";

        /// <summary>
        /// Normalized relative path in "Data\..." form, matching dmmdeps conventions.
        /// Example: "Data\\Meshes\\ZeeOgre\\foo.nif".
        /// </summary>
        public string RelativePath { get; init; } = "";

        /// <summary>
        /// The inner path as stored inside the BA2 (normalized with backslashes, no leading slash).
        /// Example: "Meshes\\ZeeOgre\\foo.nif".
        /// </summary>
        public string ArchiveInnerPath { get; init; } = "";

        /// <summary>
        /// Uncompressed file size in bytes, if available. -1 if unknown.
        /// </summary>
        public long FileSize { get; init; } = -1;

        public override string ToString()
            => $"{RelativePath} (size={FileSize}, from={Path.GetFileName(ArchivePath)})";
    }

    /// <summary>
    /// Helper class for BA2 archive introspection and "already packed" checks.
    /// 
    /// Responsibilities:
    ///  - Read a single BA2 and return a list of Ba2Entry
    ///  - Build a merged index from multiple BA2s
    ///  - Build an index for all masters' BA2s
    ///  - Check if a loose file is already packed (size + fast block sampling)
    /// </summary>
    public static class BA2Archive
    {
        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Reads one .ba2 archive and returns all entries.
        /// 
        /// You must wire this to your SharpBSA.BA2 usage:
        ///   - Open the archive from ba2Path
        ///   - Enumerate assets
        ///   - For each asset, create Ba2Entry:
        ///       ArchivePath      = full .ba2 path
        ///       ArchiveInnerPath = inner path (normalized with backslashes)
        ///       RelativePath     = NormalizeRel(inner path) => "Data\..."
        ///       FileSize         = uncompressed size
        /// </summary>
        public static IReadOnlyList<Ba2Entry> ReadIndex(string ba2Path)
        {
            if (ba2Path is null) throw new ArgumentNullException(nameof(ba2Path));
            if (!File.Exists(ba2Path))
                throw new FileNotFoundException("BA2 archive not found", ba2Path);

            var entries = new List<Ba2Entry>();
            string archiveFull = Path.GetFullPath(ba2Path);

            // -----------------------------------------------------------------
            // TODO: Wire to SharpBSA.BA2 here.
            //
            // PSEUDO-CODE SKETCH (adjust type & property names to match your DLL):
            //
            // using SharpBSA.BA2;
            //
            // using (var ba2 = new BA2File(archiveFull))
            // {
            //     foreach (var asset in ba2.Assets)
            //     {
            //         string rawInner =
            //             asset.FileName ??
            //             asset.Name ??
            //             asset.Path;
            //
            //         if (string.IsNullOrWhiteSpace(rawInner))
            //             continue;
            //
            //         string inner = NormalizeInnerPath(rawInner);       // "Meshes\foo.nif"
            //         string rel   = NormalizeRel(inner);               // "Data\Meshes\foo.nif"
            //
            //         long size = asset.UncompressedSize;               // adjust property name if needed
            //
            //         entries.Add(new Ba2Entry
            //         {
            //             ArchivePath      = archiveFull,
            //             ArchiveInnerPath = inner,
            //             RelativePath     = rel,
            //             FileSize         = size
            //         });
            //     }
            // }
            // -----------------------------------------------------------------

            throw new NotImplementedException(
                "Wire BA2Archive.ReadIndex(...) to your SharpBSA.BA2 reader by replacing the TODO block.");

            // return entries;
        }

        /// <summary>
        /// Builds a merged index (RelativePath -> Ba2Entry) from a set of .ba2 paths.
        /// Last archive wins on collision (that's fine for a "does this exist somewhere?" index).
        /// </summary>
        public static Dictionary<string, Ba2Entry> BuildMergedIndex(IEnumerable<string> ba2Paths)
        {
            if (ba2Paths == null) throw new ArgumentNullException(nameof(ba2Paths));

            var index = new Dictionary<string, Ba2Entry>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in ba2Paths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                string full = Path.GetFullPath(path);
                if (!File.Exists(full))
                    continue;

                IReadOnlyList<Ba2Entry> entries;
                try
                {
                    entries = ReadIndex(full);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Failed to read BA2 '{full}': {ex.Message}");
                    continue;
                }

                foreach (var e in entries)
                {
                    index[e.RelativePath] = e;
                }
            }

            return index;
        }

        /// <summary>
        /// Convenience wrapper:
        /// Given a list of masters (MAST entries) and a Data root, find all relevant *.ba2
        /// and build a merged index.
        /// 
        /// For each master "Foo.esm", we look for "Foo*.ba2" in dataRoot.
        /// </summary>
        public static Dictionary<string, Ba2Entry> BuildMasterArchiveIndex(
            IEnumerable<string> masterNames,
            string dataRoot)
        {
            if (masterNames == null) throw new ArgumentNullException(nameof(masterNames));
            if (dataRoot == null) throw new ArgumentNullException(nameof(dataRoot));

            var ba2Paths = new List<string>();

            foreach (var master in masterNames)
            {
                if (string.IsNullOrWhiteSpace(master))
                    continue;

                string baseName = Path.GetFileNameWithoutExtension(master);
                if (string.IsNullOrEmpty(baseName))
                    continue;

                try
                {
                    var matches = Directory.GetFiles(
                        dataRoot,
                        baseName + "*.ba2",
                        SearchOption.TopDirectoryOnly);

                    ba2Paths.AddRange(matches);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Failed to enumerate BA2s for master '{master}': {ex.Message}");
                }
            }

            return BuildMergedIndex(ba2Paths);
        }

        /// <summary>
        /// Checks if a loose file (gameRoot + relPath) is already packed in any archive.
        /// 
        /// Algorithm:
        ///   1. Normalize relPath to "Data\..."
        ///   2. Look up in mergedIndex:
        ///        - Not found => false (keep).
        ///   3. Compare file sizes:
        ///        - Different => false (keep).
        ///   4. Open loose file stream + archive entry stream.
        ///   5. FastApproxEqual(loose, archive) using sampled head/mid/tail blocks
        ///      with SIMD SequenceEqual.
        ///   6. Return true only if FastApproxEqual() is true (very high confidence).
        /// 
        /// NOTE: You must wire OpenArchiveEntryStream() to SharpBSA.BA2 so we can get
        /// a Stream for the single asset.
        /// </summary>
        public static bool IsLooseFileAlreadyPacked(
            string gameRoot,
            string relPath,
            IReadOnlyDictionary<string, Ba2Entry> mergedIndex,
            out Ba2Entry? matchedEntry)
        {
            if (gameRoot is null) throw new ArgumentNullException(nameof(gameRoot));
            if (relPath is null) throw new ArgumentNullException(nameof(relPath));
            if (mergedIndex is null) throw new ArgumentNullException(nameof(mergedIndex));

            matchedEntry = null;

            string normRel = NormalizeRel(relPath);

            // Not present in any master BA2 => definitely new.
            if (!mergedIndex.TryGetValue(normRel, out var entry))
                return false;

            matchedEntry = entry;

            string fullLoose = Path.Combine(gameRoot, normRel);
            if (!File.Exists(fullLoose))
                return false; // nothing to compare; treat as "not packed"

            var looseInfo = new FileInfo(fullLoose);
            long looseSize = looseInfo.Length;

            if (entry.FileSize >= 0 && entry.FileSize != looseSize)
            {
                // Same path but size differs => changed or override => keep.
                return false;
            }

            // Size matches (or BA2 unknown) -> heavy check:
            // Compare actual bytes with fast block sampling.

            using var looseStream = File.OpenRead(fullLoose);
            using var archiveStream = OpenArchiveEntryStream(entry); // TODO: wire to BA2

            if (!archiveStream.CanRead || !archiveStream.CanSeek)
            {
                // If we can't seek, we fall back to full streaming compare.
                // (Still using SIMD SequenceEqual in chunks.)
                return FullCompareSIMD(looseStream, archiveStream);
            }

            // Use fast approximate comparison: head / tail / middle with SIMD.
            bool approxEqual = FastApproxEqual(looseStream, archiveStream);

            // If you want absolute certainty, you can optionally do:
            // if (approxEqual)
            //     approxEqual = FullCompareSIMD(looseStream, archiveStream);

            return approxEqual;
        }

        // =====================================================================
        // BA2 entry extraction hook
        // =====================================================================

        /// <summary>
        /// Opens a readable, seekable stream for the uncompressed contents
        /// of a BA2 entry. You must implement this using SharpBSA.BA2.
        /// 
        /// Should return a stream positioned at 0.
        /// </summary>
        private static Stream OpenArchiveEntryStream(Ba2Entry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            // -----------------------------------------------------------------
            // TODO: Wire this to your BA2 library.
            //
            // PSEUDO-CODE SKETCH:
            //
            // using SharpBSA.BA2;
            //
            // var ba2 = new BA2File(entry.ArchivePath);
            // var asset = ba2.Assets.First(a =>
            //     NormalizeInnerPath(a.FileName ?? a.Name ?? a.Path)
            //         .Equals(entry.ArchiveInnerPath, StringComparison.OrdinalIgnoreCase));
            //
            // var ms = new MemoryStream();
            // asset.Extract(ms);   // or asset.CopyTo(ms) depending on API
            // ms.Position = 0;
            // return ms;
            //
            // IMPORTANT: If BA2File is IDisposable, you may want a tiny wrapper
            // that keeps BA2File alive as long as the stream is open, or
            // fully materialize into MemoryStream as shown above.
            // -----------------------------------------------------------------

            throw new NotImplementedException(
                "Wire BA2Archive.OpenArchiveEntryStream(...) to your SharpBSA.BA2 usage.");
        }

        // =====================================================================
        // Fast comparison helpers
        // =====================================================================

        /// <summary>
        /// Fast approximate equality: compare head, tail, and middle 4KB blocks
        /// using SIMD-accelerated Span.SequenceEqual.
        /// 
        /// Streams must be readable and seekable.
        /// </summary>
        private static bool FastApproxEqual(Stream a, Stream b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (!a.CanRead || !b.CanRead || !a.CanSeek || !b.CanSeek)
                return FullCompareSIMD(a, b);

            long lenA = a.Length;
            long lenB = b.Length;
            if (lenA != lenB)
                return false;

            const int BlockSize = 4096;
            var bufA = new byte[BlockSize];
            var bufB = new byte[BlockSize];

            // HEAD
            if (!ReadAndCompareBlock(a, b, 0, bufA, bufB))
                return false;

            // TAIL
            if (lenA > BlockSize)
            {
                long tailPos = lenA - BlockSize;
                if (!ReadAndCompareBlock(a, b, tailPos, bufA, bufB))
                    return false;
            }

            // MIDDLE
            if (lenA > 2 * BlockSize)
            {
                long midPos = lenA / 2;
                if (!ReadAndCompareBlock(a, b, midPos, bufA, bufB))
                    return false;
            }

            return true;
        }

        private static bool ReadAndCompareBlock(
            Stream a,
            Stream b,
            long position,
            byte[] bufA,
            byte[] bufB)
        {
            const int BlockSize = 4096;

            a.Position = position;
            b.Position = position;

            int readA = a.Read(bufA, 0, BlockSize);
            int readB = b.Read(bufB, 0, BlockSize);

            if (readA != readB)
                return false;

            return bufA.AsSpan(0, readA).SequenceEqual(bufB.AsSpan(0, readB));
        }

        /// <summary>
        /// Exact compare: stream both files in chunks and use SIMD SequenceEqual
        /// on each chunk. This is still quite fast, but reads 100% of both streams.
        /// </summary>
        private static bool FullCompareSIMD(Stream a, Stream b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            long lenA = a.CanSeek ? a.Length : -1;
            long lenB = b.CanSeek ? b.Length : -1;

            if (lenA >= 0 && lenB >= 0 && lenA != lenB)
                return false;

            const int BufSize = 65536;
            var bufA = new byte[BufSize];
            var bufB = new byte[BufSize];

            if (a.CanSeek) a.Position = 0;
            if (b.CanSeek) b.Position = 0;

            while (true)
            {
                int readA = a.Read(bufA, 0, BufSize);
                int readB = b.Read(bufB, 0, BufSize);

                if (readA != readB)
                    return false;

                if (readA == 0)
                    break; // both ended

                if (!bufA.AsSpan(0, readA).SequenceEqual(bufB.AsSpan(0, readB)))
                    return false;
            }

            return true;
        }

        // =====================================================================
        // Path normalization helpers
        // =====================================================================

        /// <summary>
        /// Normalize something like "meshes\foo.nif" or "\Data\Meshes\foo.nif"
        /// into "Data\Meshes\foo.nif" with backslashes.
        /// </summary>
        private static string NormalizeRel(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Data\\";

            string rel = raw.Trim();
            rel = rel.Replace('/', '\\');

            while (rel.StartsWith("\\", StringComparison.Ordinal))
                rel = rel.Substring(1);

            if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                rel = Path.Combine("Data", rel);

            return rel;
        }

        /// <summary>
        /// Normalize BA2 inner paths consistently (backslashes, no leading slash).
        /// Example: "/meshes/foo.nif" -> "meshes\foo.nif".
        /// </summary>
        private static string NormalizeInnerPath(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string p = raw.Trim();
            p = p.Replace('/', '\\');

            while (p.StartsWith("\\", StringComparison.Ordinal))
                p = p.Substring(1);

            return p;
        }
    }
}
