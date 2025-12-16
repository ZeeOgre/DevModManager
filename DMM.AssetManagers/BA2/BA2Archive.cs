using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DMM.AssetManagers
{
    public sealed class Ba2Entry
    {
        public string ArchivePath { get; init; } = "";
        public string RelativePath { get; init; } = "";
        public string ArchiveInnerPath { get; init; } = "";
        public long FileSize { get; init; } = -1;

        public override string ToString()
            => $"{RelativePath} (size={FileSize}, from={Path.GetFileName(ArchivePath)})";
    }

    public static class BA2Archive
    {
        // Reflection cache
        private static Type? s_ba2FileType;
        private static Func<string, object>? s_ba2FileCtor;
        private static Func<object, IEnumerable<object>>? s_getAssets;
        private static Func<object, string?>? s_getAssetNameOrPath;
        private static Func<object, long>? s_getAssetUncompressedSize;
        private static Action<object, Stream>? s_assetExtractToStream;
        private static Action<object>? s_ba2Dispose;

        public static IReadOnlyList<Ba2Entry> ReadIndex(string ba2Path)
        {
            if (ba2Path is null) throw new ArgumentNullException(nameof(ba2Path));
            if (!File.Exists(ba2Path))
                throw new FileNotFoundException("BA2 archive not found", ba2Path);

            EnsureBindings();

            var entries = new List<Ba2Entry>();
            string archiveFull = Path.GetFullPath(ba2Path);

            object ba2 = s_ba2FileCtor!(archiveFull);
            try
            {
                foreach (var asset in s_getAssets!(ba2))
                {
                    string? rawInner = s_getAssetNameOrPath!(asset);
                    if (string.IsNullOrWhiteSpace(rawInner))
                        continue;

                    string inner = NormalizeInnerPath(rawInner);
                    if (string.IsNullOrWhiteSpace(inner))
                        continue;

                    string rel = NormalizeRel(inner);
                    long size = SafeGetSize(asset);

                    entries.Add(new Ba2Entry
                    {
                        ArchivePath = archiveFull,
                        ArchiveInnerPath = inner,
                        RelativePath = rel,
                        FileSize = size
                    });
                }
            }
            finally
            {
                s_ba2Dispose?.Invoke(ba2);
                (ba2 as IDisposable)?.Dispose();
            }

            return entries;
        }

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

            if (!mergedIndex.TryGetValue(normRel, out var entry))
                return false;

            matchedEntry = entry;

            string fullLoose = Path.Combine(gameRoot, normRel);
            if (!File.Exists(fullLoose))
                return false;

            var looseInfo = new FileInfo(fullLoose);
            long looseSize = looseInfo.Length;

            if (entry.FileSize >= 0 && entry.FileSize != looseSize)
                return false;

            using var looseStream = File.OpenRead(fullLoose);
            using var archiveStream = OpenArchiveEntryStream(entry);

            if (!archiveStream.CanRead || !archiveStream.CanSeek)
                return FullCompareSIMD(looseStream, archiveStream);

            bool approxEqual = FastApproxEqual(looseStream, archiveStream);
            return approxEqual;
        }

        private static Stream OpenArchiveEntryStream(Ba2Entry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            EnsureBindings();

            object ba2 = s_ba2FileCtor!(entry.ArchivePath);
            try
            {
                var asset = s_getAssets!(ba2).FirstOrDefault(a =>
                {
                    string? raw = s_getAssetNameOrPath!(a);
                    string inner = NormalizeInnerPath(raw);
                    return inner.Equals(entry.ArchiveInnerPath, StringComparison.OrdinalIgnoreCase);
                });

                if (asset == null)
                    throw new FileNotFoundException("Asset not found in BA2: " + entry.ArchiveInnerPath, entry.ArchivePath);

                var ms = new MemoryStream();
                s_assetExtractToStream!(asset, ms);
                ms.Position = 0;
                return ms;
            }
            finally
            {
                s_ba2Dispose?.Invoke(ba2);
                (ba2 as IDisposable)?.Dispose();
            }
        }

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

            if (!ReadAndCompareBlock(a, b, 0, bufA, bufB))
                return false;

            if (lenA > BlockSize)
            {
                long tailPos = lenA - BlockSize;
                if (!ReadAndCompareBlock(a, b, tailPos, bufA, bufB))
                    return false;
            }

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
                    break;

                if (!bufA.AsSpan(0, readA).SequenceEqual(bufB.AsSpan(0, readB)))
                    return false;
            }

            return true;
        }

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

        private static long SafeGetSize(object asset)
        {
            try
            {
                return s_getAssetUncompressedSize != null ? s_getAssetUncompressedSize(asset) : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static void EnsureBindings()
        {
            if (s_ba2FileType != null) return;

            // Try common types/namespaces for BA2 readers.
            // Adjust if your library uses different names.

            // Candidate fully qualified names
            string[] candidates =
            {
                "SharpBSA.BA2.BA2File",
                "SharpBSA.BA2.Ba2File",
                "Bethesda.Archives.BA2File",
                "BA2Lib.BA2File"
            };

            Type? ba2Type = candidates.Select(Type.GetType).FirstOrDefault(t => t != null);
            if (ba2Type == null)
                throw new InvalidOperationException("BA2 reader type not found. Ensure a BA2 library (e.g., SharpBSA) is referenced and available.");

            s_ba2FileType = ba2Type;

            // Constructor: BA2File(string path)
            var ctor = ba2Type.GetConstructor(new[] { typeof(string) })
                       ?? throw new InvalidOperationException("BA2File(string) constructor not found.");
            s_ba2FileCtor = (string path) => ctor.Invoke(new object[] { path });

            // Assets enumerable property or method
            // Try property "Assets" or "Files"
            var assetsProp = ba2Type.GetProperty("Assets") ?? ba2Type.GetProperty("Files");
            if (assetsProp == null)
                throw new InvalidOperationException("BA2File.Assets (or Files) property not found.");

            s_getAssets = (object ba2) =>
            {
                var obj = assetsProp.GetValue(ba2);
                return (obj as IEnumerable<object>) ?? ((obj as System.Collections.IEnumerable)?.Cast<object>
                        ?? throw new InvalidOperationException("BA2 Assets enumeration not supported."));
            };

            // Asset type discovery (first item)
            var probeBa2 = s_ba2FileCtor!(Path.GetTempFileName()); // we won't enumerate; just dispose
            try
            {
                s_ba2Dispose = (probeBa2 as IDisposable != null)
                    ? (Action<object>)(o => ((IDisposable)o).Dispose())
                    : null;
            }
            catch { s_ba2Dispose = null; }
            finally
            {
                s_ba2Dispose?.Invoke(probeBa2);
                (probeBa2 as IDisposable)?.Dispose();
            }

            // Asset string path getters — try FileName, Name, Path
            s_getAssetNameOrPath = BindStringGetterFromAsset("FileName")
                                   ?? BindStringGetterFromAsset("Name")
                                   ?? BindStringGetterFromAsset("Path")
                                   ?? throw new InvalidOperationException("Asset path/name getter not found (FileName/Name/Path).");

            // Asset uncompressed size — try UncompressedSize, Size
            s_getAssetUncompressedSize = BindLongGetterFromAsset("UncompressedSize")
                                         ?? BindLongGetterFromAsset("Size");

            // Asset extract — try Extract(Stream) or CopyTo(Stream)
            s_assetExtractToStream = BindExtractToStream("Extract")
                                     ?? BindExtractToStream("CopyTo")
                                     ?? throw new InvalidOperationException("Asset extract method not found (Extract/CopyTo).");
        }

        private static Func<object, string?>? BindStringGetterFromAsset(string propertyName)
        {
            return (object asset) =>
            {
                var prop = asset.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return prop != null ? (string?)prop.GetValue(asset) : null;
            };
        }

        private static Func<object, long>? BindLongGetterFromAsset(string propertyName)
        {
            return (object asset) =>
            {
                var prop = asset.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) return -1;
                var val = prop.GetValue(asset);
                if (val == null) return -1;
                if (val is long l) return l;
                if (val is int i) return i;
                if (val is uint ui) return unchecked((long)ui);
                if (val is ulong ul) return unchecked((long)ul);
                if (long.TryParse(val.ToString(), out var parsed)) return parsed;
                return -1;
            };
        }

        private static Action<object, Stream>? BindExtractToStream(string methodName)
        {
            return (object asset, Stream output) =>
            {
                var m = asset.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, new[] { typeof(Stream) });
                if (m == null) return;
                m.Invoke(asset, new object[] { output });
            };
        }
    }
}
