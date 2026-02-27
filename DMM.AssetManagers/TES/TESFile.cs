using System.Text;
using System.Text.RegularExpressions;

namespace DMM.AssetManagers.TES
{
    public sealed class TesFileResult
    {
        public string FileName { get; init; } = "";

        // Bethesda ScriptName tokens, e.g. "CommunityShare:DebugMenuFramework:dmfMasterQuest"
        public List<string> ReferencedScripts { get; } = new();

        // Data-relative NIF paths, e.g. "Data\\Meshes\\ZeeOgre\\OTT\\DarkStar_jmpz11_Terminal_WallAttach_ext.nif"
        public List<string> ReferencedNifs { get; } = new();

        // Data-relative MAT paths, e.g. "Data\\Materials\\darkstar\\Darkstar_Circle_Border_Logo.mat"
        public List<string> ReferencedMats { get; } = new();

        // Data-relative DDS textures, e.g. "Data\\Textures\\DarkStar\\darkstar-blackborder-512x512.dds"
        public List<string> ReferencedTextures { get; } = new();

        // Data-relative audio-ish assets discovered in the plugin:
        // voice WAV/WEM, FFXANIM, and legacy LIP (for older games).
        // e.g. "Data\\Sound\\Voice\\ZeeOgresEnhancedOutposts.esp\\NPCMKeltonFrush\\000C8CB6.wav"
        public List<string> ReferencedAudio { get; } = new();
    }

    public sealed class TESFile
    {
        /// <summary>
        /// Read a TES plugin (esp/esm) and extract top-level references as strings.
        /// All paths returned are normalized to backslashes and Data-relative,
        /// without a leading ".\".
        /// </summary>
        public TesFileResult Read(string pluginPath)
        {
            if (pluginPath == null) throw new ArgumentNullException(nameof(pluginPath));
            if (!File.Exists(pluginPath)) throw new FileNotFoundException("TES plugin not found", pluginPath);

            var bytes = File.ReadAllBytes(pluginPath);
            var result = new TesFileResult
            {
                FileName = Path.GetFileName(pluginPath)
            };

            var seenScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNifs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenMats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenDds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenAudio = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) Scan printable strings from the binary. This is intentionally "dumb":
            //    we just pull out contiguous ASCII and then pattern-match on the strings.
            foreach (var s in ExtractPrintableStrings(bytes, minLen: 6))
            {
                // Normalize slashes and trim a single leading backslash.
                string token = s.Replace('/', '\\').TrimStart('\\');

                // --- NIFs ---------------------------------------------------------
                if (token.EndsWith(".nif", StringComparison.OrdinalIgnoreCase))
                {
                    string rel = token;
                    if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                    {
                        // Default assumption: this is a mesh under Data\Meshes
                        rel = Path.Combine("Data\\Meshes", rel);
                    }
                    rel = NormalizeRel(rel);
                    if (seenNifs.Add(rel))
                        result.ReferencedNifs.Add(rel);
                    continue;
                }

                // --- MATs ---------------------------------------------------------
                if (token.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    string rel = token;
                    if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                    {
                        // Two patterns we commonly see:
                        //   "Materials\\foo\\bar.mat"   -> Data\\Materials\\foo\\bar.mat
                        //   "Data\\Materials\\foo.mat"  -> already correct
                        rel = token.StartsWith("Materials\\", StringComparison.OrdinalIgnoreCase)
                            ? Path.Combine("Data", token)
                            : Path.Combine("Data\\Materials", token);
                    }
                    rel = NormalizeRel(rel);
                    if (seenMats.Add(rel))
                        result.ReferencedMats.Add(rel);
                    continue;
                }

                // --- DDS Textures -------------------------------------------------
                if (token.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                {
                    string rel = token;
                    if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                    {
                        // Default assumption: textures under Data\Textures
                        rel = Path.Combine("Data\\Textures", token);
                    }
                    rel = NormalizeRel(rel);
                    if (seenDds.Add(rel))
                        result.ReferencedTextures.Add(rel);
                    continue;
                }

                // --- Audio-ish assets (WAV/WEM/FFXANIM/LIP) ----------------------
                //
                // Starfield:
                //   - .wav   : source voice lines under Data\Sound\Voice\<mod>.esp\...
                //   - .wem   : converted Wwise soundbanks (PC + XB) under Sound\Voice or Sound\Soundbanks
                //   - .ffxanim : face animation data
                //
                // Older titles (Skyrim / FO):
                //   - .lip   : lip sync files alongside voice WAVs
                //
                // We normalize to a Data-relative path and let callers decide
                // whether to treat them as "PC", "XB" or backup-only.
                if (token.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                    token.EndsWith(".wem", StringComparison.OrdinalIgnoreCase) ||
                    token.EndsWith(".ffxanim", StringComparison.OrdinalIgnoreCase))
                //token.EndsWith(".lip", StringComparison.OrdinalIgnoreCase))
                {
                    string rel = token;
                    if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                    {
                        // If the plugin only stored "Sound\\Voice\\...", prepend Data\.
                        rel = Path.Combine("Data", token);
                    }
                    rel = NormalizeRel(rel);
                    if (seenAudio.Add(rel))
                        result.ReferencedAudio.Add(rel);
                    continue;
                }
            }

            CollectMatPathsFromLmswRecords(bytes, seenMats, result);

            // 2) Script name tokens (colon-delimited, e.g. Foo:Bar or Foo:Bar:Baz).
            //    These are the Bethesda ScriptName forms we can map to PSC/PEX later:
            //       Data\Scripts\  + name.Replace(':','\\') + ".pex"
            //       Data\Scripts\Source\ + name.Replace(':','\\') + ".psc"
            //
            // We deliberately do NOT touch the filesystem here; TESFile just reports
            // the logical ScriptName tokens, and higher-level code can decide how
            // to resolve and expand imports.
            var scriptRx = new Regex(@"\b([A-Za-z0-9_]+(?::[A-Za-z0-9_]+)+)\b",
                                      RegexOptions.Compiled);
            string asciiView = Encoding.ASCII.GetString(bytes);

            foreach (Match m in scriptRx.Matches(asciiView))
            {
                string candidate = m.Groups[1].Value.Trim();
                if (candidate.Length == 0)
                    continue;

                if (seenScripts.Add(candidate))
                    result.ReferencedScripts.Add(candidate);
            }

            return result;
        }

        /// <summary>
        /// Normalize a relative path:
        ///  - Converts '/' to '\'
        ///  - Strips leading ".\"
        /// </summary>
        private static string NormalizeRel(string rel)
        {
            rel = rel.Replace('/', '\\');
            if (rel.StartsWith(".\\", StringComparison.Ordinal))
                rel = rel.Substring(2);
            return rel;
        }

        /// <summary>
        /// Extract contiguous "printable" ASCII runs from a binary blob.
        /// Used to pull JSON-ish fragments, asset paths, and ScriptName tokens
        /// from TES plugin records without doing full record-type decoding.
        /// </summary>
        private static IEnumerable<string> ExtractPrintableStrings(byte[] data, int minLen)
        {
            var sb = new StringBuilder();

            foreach (byte b in data)
            {
                if (b >= 32 && b <= 126) // printable ASCII
                {
                    sb.Append((char)b);
                }
                else
                {
                    if (sb.Length >= minLen)
                        yield return sb.ToString();
                    sb.Clear();
                }
            }

            if (sb.Length >= minLen)
                yield return sb.ToString();
        }

        private static void CollectMatPathsFromLmswRecords(byte[] pluginBytes, HashSet<string> seenMats, TesFileResult result)
        {
            const int recordHeaderSize = 24;
            const int groupHeaderSize = 24;
            int pos = 0;
            int len = pluginBytes.Length;
            var groupEnds = new Stack<int>();

            while (pos + 4 <= len)
            {
                while (groupEnds.Count > 0 && pos >= groupEnds.Peek())
                    groupEnds.Pop();

                string sig = ReadAscii4(pluginBytes, pos);
                if (sig == "GRUP")
                {
                    if (pos + groupHeaderSize > len) break;
                    int groupSize = ReadInt32LE(pluginBytes, pos + 4);
                    if (groupSize < groupHeaderSize) break;

                    int groupEnd = pos + groupSize;
                    if (groupEnd > len) break;

                    groupEnds.Push(groupEnd);
                    pos += groupHeaderSize;
                    continue;
                }

                if (pos + recordHeaderSize > len) break;
                int dataSize = ReadInt32LE(pluginBytes, pos + 4);
                if (dataSize < 0) break;

                int payloadStart = pos + recordHeaderSize;
                int payloadEnd = payloadStart + dataSize;
                if (payloadEnd > len) break;

                if (sig == "LMSW")
                {
                    ReadOnlySpan<byte> payload = new(pluginBytes, payloadStart, dataSize);
                    foreach (var sub in EnumerateSubrecords(payload))
                    {
                        if (!string.Equals(sub.Type, "REFL", StringComparison.Ordinal))
                            continue;

                        foreach (string token in ScrapeMatTokensFromBlob(payload.Slice(sub.Offset, sub.Length)))
                        {
                            string rel = NormalizeMatTokenToRelPath(token);
                            if (string.IsNullOrEmpty(rel))
                                continue;

                            if (seenMats.Add(rel))
                                result.ReferencedMats.Add(rel);
                        }
                    }
                }

                pos = payloadEnd;
            }
        }

        private static string NormalizeMatTokenToRelPath(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "";

            string t = token.Replace('/', '\\').Trim().TrimStart('\\');
            int matIndex = t.IndexOf(".mat", StringComparison.OrdinalIgnoreCase);
            if (matIndex >= 0)
                t = t[..(matIndex + 4)];

            if (!t.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                return "";

            if (t.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                return NormalizeRel(t);

            if (t.StartsWith("Materials\\", StringComparison.OrdinalIgnoreCase))
                return NormalizeRel(Path.Combine("Data", t));

            return NormalizeRel(Path.Combine("Data\\Materials", t));
        }

        private static IEnumerable<(string Type, int Offset, int Length)> EnumerateSubrecords(ReadOnlySpan<byte> payload)
        {
            int i = 0;
            while (i + 6 <= payload.Length)
            {
                string type = Encoding.ASCII.GetString(payload.Slice(i, 4));
                ushort sz = ReadUInt16LE(payload, i + 4);
                i += 6;

                if (type == "XXXX")
                {
                    if (i + 4 > payload.Length) yield break;
                    int extSize = ReadInt32LE(payload, i);
                    i += 4;

                    if (i + 6 > payload.Length) yield break;
                    type = Encoding.ASCII.GetString(payload.Slice(i, 4));
                    i += 4;
                    _ = ReadUInt16LE(payload, i);
                    i += 2;

                    if (extSize < 0 || i + extSize > payload.Length) yield break;
                    yield return (type, i, extSize);
                    i += extSize;
                    continue;
                }

                if (i + sz > payload.Length) yield break;
                yield return (type, i, sz);
                i += sz;
            }
        }

        private static IEnumerable<string> ScrapeMatTokensFromBlob(ReadOnlySpan<byte> blob)
        {
            static bool IsTokenChar(byte b)
            {
                if (b >= (byte)'a' && b <= (byte)'z') return true;
                if (b >= (byte)'A' && b <= (byte)'Z') return true;
                if (b >= (byte)'0' && b <= (byte)'9') return true;
                return b is (byte)'\\' or (byte)'/' or (byte)'_' or (byte)'-' or (byte)'.' or (byte)' ';
            }

            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i <= blob.Length - 4; i++)
            {
                if (blob[i] != (byte)'.')
                    continue;

                if (!IsAsciiCI(blob[i + 1], (byte)'m')) continue;
                if (!IsAsciiCI(blob[i + 2], (byte)'a')) continue;
                if (!IsAsciiCI(blob[i + 3], (byte)'t')) continue;

                int end = i + 4;
                int start = i - 1;
                while (start >= 0 && IsTokenChar(blob[start]))
                    start--;
                start++;

                int tokenLen = end - start;
                if (tokenLen < 6)
                    continue;

                Span<byte> tmp = tokenLen <= 512 ? stackalloc byte[tokenLen] : new byte[tokenLen];
                int w = 0;
                for (int k = start; k < end; k++)
                {
                    byte c = blob[k];
                    if (c == 0 || !IsTokenChar(c))
                        continue;
                    tmp[w++] = c;
                }

                if (w < 6)
                    continue;

                string token = Encoding.ASCII.GetString(tmp[..w]).Trim();
                if (!token.Contains('\\') && !token.Contains('/'))
                    continue;

                token = token.Replace('/', '\\');
                if (results.Add(token))
                    yield return token;
            }
        }

        private static bool IsAsciiCI(byte actual, byte expectedLower)
        {
            if (actual == expectedLower)
                return true;

            byte upper = (byte)(expectedLower - 32);
            return actual == upper;
        }

        private static string ReadAscii4(byte[] data, int offset)
        {
            if (offset + 4 > data.Length)
                return "";

            return Encoding.ASCII.GetString(data, offset, 4);
        }

        private static int ReadInt32LE(byte[] data, int offset)
        {
            return data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24);
        }

        private static int ReadInt32LE(ReadOnlySpan<byte> data, int offset)
        {
            return data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24);
        }

        private static ushort ReadUInt16LE(ReadOnlySpan<byte> data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }
    }
}
