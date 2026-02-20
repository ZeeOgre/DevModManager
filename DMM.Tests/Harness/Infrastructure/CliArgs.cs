using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DMM.Tests.Harness.Infrastructure;

public sealed class CliArgs
{
    public List<string> Positionals { get; } = new();

    public bool ShowHelp { get; private set; }

    public string? OutPath { get; private set; }
    public string Format { get; private set; } = "yaml";

    public bool NoVisuals { get; private set; }
    public bool ScanAll { get; private set; }

    // Roots can be provided via:
    // --roots "a;b;c"  (semicolon-separated)
    // --root "a" --root "b"
    // --roots-file roots.txt (one per line)
    private readonly List<string> _roots = new();
    private string? _rootsFile;

    public static CliArgs Parse(string[] args)
    {
        var a = new CliArgs();

        for (int i = 0; i < args.Length; i++)
        {
            var token = args[i];

            if (token is "--help" or "-h" or "help")
            {
                a.ShowHelp = true;
                continue;
            }

            if (token.Equals("--out", StringComparison.OrdinalIgnoreCase))
            {
                a.OutPath = NextValue(args, ref i, "--out");
                continue;
            }

            if (token.Equals("--format", StringComparison.OrdinalIgnoreCase))
            {
                a.Format = (NextValue(args, ref i, "--format") ?? "yaml").Trim().ToLowerInvariant();
                continue;
            }

            if (token.Equals("--no-visuals", StringComparison.OrdinalIgnoreCase))
            {
                a.NoVisuals = true;
                continue;
            }

            if (token.Equals("--scanall", StringComparison.OrdinalIgnoreCase))
            {
                a.ScanAll = true;
                continue;
            }

            if (token.Equals("--roots", StringComparison.OrdinalIgnoreCase))
            {
                var v = NextValue(args, ref i, "--roots");
                if (!string.IsNullOrWhiteSpace(v))
                {
                    foreach (var p in v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        a._roots.Add(p.Trim());
                }
                continue;
            }

            if (token.Equals("--root", StringComparison.OrdinalIgnoreCase))
            {
                var v = NextValue(args, ref i, "--root");
                if (!string.IsNullOrWhiteSpace(v))
                    a._roots.Add(v.Trim());
                continue;
            }

            if (token.Equals("--roots-file", StringComparison.OrdinalIgnoreCase))
            {
                a._rootsFile = NextValue(args, ref i, "--roots-file");
                continue;
            }

            // Positional
            a.Positionals.Add(token);
        }

        return a;
    }

    public IReadOnlyList<string> ResolveRoots()
    {
        var roots = new List<string>();

        roots.AddRange(_roots.Where(r => !string.IsNullOrWhiteSpace(r)));

        if (!string.IsNullOrWhiteSpace(_rootsFile))
        {
            var path = _rootsFile!;
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0) continue;
                    if (trimmed.StartsWith("#")) continue;
                    roots.Add(trimmed);
                }
            }
        }

        // Normalize: remove empties, trim, distinct (case-insensitive)
        return roots
            .Select(r => r.Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NextValue(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {name}");

        i++;
        return args[i];
    }
}
