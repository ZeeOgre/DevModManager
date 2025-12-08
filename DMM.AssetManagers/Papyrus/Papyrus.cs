using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DMM.AssetManagers.Papyrus
{
    public sealed class Papyrus
    {
        // Read PEX (binary) metadata or dependency tokens
        public byte[] Read(string pexPath)
        {
            if (pexPath == null) throw new ArgumentNullException(nameof(pexPath));
            throw new NotImplementedException();
        }

        // Convert a Bethesda Script Name (colon-separated) to PSC relative path
        // Example: "Foo:Bar:Myscript" -> "Data\Scripts\Source\Foo\Bar\Myscript.psc"
        public static string ScriptNameToPscRel(string scriptName)
        {
            if (scriptName == null) throw new ArgumentNullException(nameof(scriptName));
            string path = scriptName.Replace(':', '\\');
            return NormalizeRel(PathCombine("Data", "Scripts", "Source", path + ".psc"));
        }

        // Convert a Bethesda Script Name to PEX relative path
        // Example: "Foo:Bar:Myscript" -> "Data\Scripts\Foo\Bar\Myscript.pex"
        public static string ScriptNameToPexRel(string scriptName)
        {
            if (scriptName == null) throw new ArgumentNullException(nameof(scriptName));
            string path = scriptName.Replace(':', '\\');
            return NormalizeRel(PathCombine("Data", "Scripts", path + ".pex"));
        }

        // Extract script-names referenced by import/include statements from PSC content.
        // Returns distinct script-names (e.g. "Foo:Bar:Myscript").
        public static IReadOnlyList<string> ExtractIncludedScriptNamesFromPscText(string pscText)
        {
            if (pscText == null) throw new ArgumentNullException(nameof(pscText));

            // Matches lines like:
            //   import Foo:Bar    OR    include Foo:Bar
            // Also tolerates trailing comments/whitespace.
            var importRx = new Regex(@"^\s*(?:import|include)\s+([A-Za-z0-9_:.]+)",
                                     RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match m in importRx.Matches(pscText))
            {
                string candidate = m.Groups[1].Value.Trim();
                if (candidate.Length == 0) continue;
                if (seen.Add(candidate))
                    names.Add(candidate);
            }

            return names;
        }

        // Read a PSC file and extract included/imported script names.
        public static IReadOnlyList<string> ExtractIncludedScriptNamesFromPscFile(string pscPath, Encoding? encoding = null)
        {
            if (pscPath == null) throw new ArgumentNullException(nameof(pscPath));
            if (!File.Exists(pscPath)) throw new FileNotFoundException("PSC file not found", pscPath);

            encoding ??= Encoding.UTF8;
            string text = File.ReadAllText(pscPath, encoding);
            return ExtractIncludedScriptNamesFromPscText(text);
        }

        // Given PSC text and an optional primary script-name, return PSC+PEX lists simultaneously.
        // Primary may be null if analyzing an orphan PSC fragment; primary will be included if provided.
        public static PapyrusAnalysisResult AnalyzePscTextWithPairs(string pscText, string? primaryScriptName = null)
        {
            if (pscText == null) throw new ArgumentNullException(nameof(pscText));

            var includedNames = ExtractIncludedScriptNamesFromPscText(pscText);
            var includedPairs = includedNames.Select(GetScriptPairForName).ToArray();

            ScriptPair? primary = null;
            if (!string.IsNullOrEmpty(primaryScriptName))
                primary = GetScriptPairForName(primaryScriptName);

            return new PapyrusAnalysisResult
            {
                Primary = primary,
                Included = includedPairs
            };
        }

        // Convenience: analyze a PSC file and return paired PSC/PEX lists. Optionally provide the primary script-name.
        public static PapyrusAnalysisResult AnalyzePscFileWithPairs(string pscPath, string? primaryScriptName = null, Encoding? encoding = null)
        {
            if (pscPath == null) throw new ArgumentNullException(nameof(pscPath));
            if (!File.Exists(pscPath)) throw new FileNotFoundException("PSC file not found", pscPath);

            encoding ??= Encoding.UTF8;
            string text = File.ReadAllText(pscPath, encoding);
            return AnalyzePscTextWithPairs(text, primaryScriptName);
        }

        // Verify that both PSC and PEX exist on disk given a gameRoot (returns tuple: pscExists, pexExists)
        public static (bool pscExists, bool pexExists) VerifyScriptPairExists(string gameRoot, ScriptPair pair)
        {
            if (gameRoot == null) throw new ArgumentNullException(nameof(gameRoot));
            if (pair == null) throw new ArgumentNullException(nameof(pair));
            string fullPsc = Path.Combine(gameRoot, pair.PscRel);
            string fullPex = Path.Combine(gameRoot, pair.PexRel);
            return (File.Exists(fullPsc), File.Exists(fullPex));
        }

        // Helper: normalize rel path (backslashes, drop leading ".\")
        private static string NormalizeRel(string rel)
        {
            if (rel == null) return "";
            rel = rel.Replace('/', '\\');
            if (rel.StartsWith(".\\", StringComparison.Ordinal))
                rel = rel.Substring(2);
            return rel;
        }

        // Simple Path.Combine that tolerates null/empty segments and preserves backslashes
        private static string PathCombine(params string[] parts)
        {
            if (parts == null || parts.Length == 0) return string.Empty;
            string result = parts[0] ?? string.Empty;
            for (int i = 1; i < parts.Length; i++)
            {
                string p = parts[i] ?? string.Empty;
                if (string.IsNullOrEmpty(p)) continue;
                if (result.EndsWith("\\") || result.EndsWith("/"))
                    result = result.TrimEnd('\\', '/');
                result = result + "\\" + p.TrimStart('\\', '/');
            }
            return result;
        }
    }
}   