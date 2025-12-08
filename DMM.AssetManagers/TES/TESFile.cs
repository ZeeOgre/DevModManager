using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    token.EndsWith(".ffxanim", StringComparison.OrdinalIgnoreCase) )
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
    }
}
