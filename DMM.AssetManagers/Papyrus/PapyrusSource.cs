using System;
using System.Collections.Generic;
using System.Text;

namespace DMM.AssetManagers.Papyrus
{
    public sealed class PapyrusSource
    {
        // Read PSC file text
        public string Read(string pscPath)
        {
            if (pscPath == null) throw new ArgumentNullException(nameof(pscPath));
            throw new NotImplementedException();
        }

        // Extract import tokens from PSC content
        public IEnumerable<string> ExtractImports(string pscPath)
        {
            throw new NotImplementedException();
        }
        // Convenience: return PEX relative paths for included script names found in PSC text.
        public static IReadOnlyList<string> ExtractIncludedPexRelPathsFromPscText(string pscText)
        {
            var names = ExtractIncludedScriptNamesFromPscText(pscText);
            var list = new List<string>(names.Count);
            foreach (var n in names)
                list.Add(ScriptNameToPexRel(n));
            return list;
        }


        // Read PSC file and return PEX-relative paths for included scripts.
        public static IReadOnlyList<string> ExtractIncludedPexRelPathsFromPscFile(string pscPath, Encoding? encoding = null)
        {
            if (pscPath == null) throw new ArgumentNullException(nameof(pscPath));
            encoding ??= Encoding.UTF8;
            string text = File.ReadAllText(pscPath, encoding);
            return ExtractIncludedPexRelPathsFromPscText(text);
        }

    }
}