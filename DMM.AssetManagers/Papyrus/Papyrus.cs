using System.Text;
using System.Text.RegularExpressions;

namespace DMM.AssetManagers.Papyrus
{
    public sealed class PapyrusSource
    {
        /// <summary>
        /// Read PSC file text.
        /// </summary>
        public string Read(string pscPath)
        {
            if (pscPath == null)
                throw new ArgumentNullException(nameof(pscPath));

            // You can add encoding detection later if you need it.
            return File.ReadAllText(pscPath, Encoding.UTF8);
        }

        /// <summary>
        /// Extract import tokens from PSC content (script names in import statements).
        /// </summary>
        public IEnumerable<string> ExtractImports(string pscPath)
        {
            if (pscPath == null)
                throw new ArgumentNullException(nameof(pscPath));

            string text = Read(pscPath);
            return ExtractIncludedScriptNamesFromPscText(text);
        }

        /// <summary>
        /// Convenience: return PEX relative paths for included script names found in PSC text.
        /// </summary>
        public static IReadOnlyList<string> ExtractIncludedPexRelPathsFromPscText(string pscText)
        {
            if (pscText == null)
                throw new ArgumentNullException(nameof(pscText));

            var names = ExtractIncludedScriptNamesFromPscText(pscText);
            var list = new List<string>(names.Count);

            foreach (var n in names)
                list.Add(ScriptNameToPexRel(n));

            return list;
        }

        /// <summary>
        /// Read PSC file and return PEX-relative paths for included scripts.
        /// </summary>
        public static IReadOnlyList<string> ExtractIncludedPexRelPathsFromPscFile(string pscPath, Encoding? encoding = null)
        {
            if (pscPath == null)
                throw new ArgumentNullException(nameof(pscPath));

            encoding ??= Encoding.UTF8;
            string text = File.ReadAllText(pscPath, encoding);
            return ExtractIncludedPexRelPathsFromPscText(text);
        }

        /// <summary>
        /// Parse Papyrus source text and return script names referenced by import statements.
        /// e.g. "import ZeeOgre:ZEO:SomeUtility" → "ZeeOgre:ZEO:SomeUtility".
        /// </summary>
        public static IReadOnlyList<string> ExtractIncludedScriptNamesFromPscText(string pscText)
        {
            if (pscText == null)
                throw new ArgumentNullException(nameof(pscText));

            var result = new List<string>();

            // Simple line-based scan:
            //   import Foo:Bar:Baz   ; comment
            // We'll ignore everything after ';' and capture the token after 'import'.
            var re = new Regex(
                @"^\s*import\s+([A-Za-z0-9_:]+)",
                RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match m in re.Matches(pscText))
            {
                var name = m.Groups[1].Value.Trim();
                if (name.Length == 0)
                    continue;

                if (seen.Add(name))
                    result.Add(name);
            }

            return result;
        }

        /// <summary>
        /// Convert a Papyrus script name to a PEX-relative path
        /// (relative to Data\Scripts), handling namespace-style names.
        ///
        /// Example:
        ///   "ZeeOgre:ZEO:LimitDoorActivationToFaction"
        /// → "scripts\ZeeOgre\ZEO\LimitDoorActivationToFaction.pex"
        /// </summary>
        public static string ScriptNameToPexRel(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
                throw new ArgumentException("Script name must not be null or empty.", nameof(scriptName));

            // Split Papyrus namespace components on ':'
            var parts = scriptName.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            var sb = new StringBuilder();
            sb.Append("scripts");

            foreach (var part in parts)
            {
                sb.Append(Path.DirectorySeparatorChar);
                sb.Append(part.Trim());
            }

            sb.Append(".pex");
            return sb.ToString();
        }
    }
}
