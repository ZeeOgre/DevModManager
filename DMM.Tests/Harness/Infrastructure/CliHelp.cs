using System;

namespace DMM.Tests.Harness.Infrastructure;

public static class CliHelp
{
    public static void Print()
    {
        Console.WriteLine("DMM.Tests.Harness");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  stores");
        Console.WriteLine("  scan all [options]");
        Console.WriteLine("  scan store <storeKey> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --out <path>             Write output to file (otherwise prints to console)");
        Console.WriteLine("  --format yaml|json       Output format (default: yaml)");
        Console.WriteLine("  --no-visuals             Skip visual asset paths");
        Console.WriteLine("  --roots \"a;b;c\"          Override roots (semicolon-separated)");
        Console.WriteLine("  --root <path>            Override roots (repeatable)");
        Console.WriteLine("  --roots-file <file>      Override roots from file (one per line)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dmm stores");
        Console.WriteLine("  dmm scan store xbox --out c:\\temp\\xbox.yaml");
        Console.WriteLine("  dmm scan all --format json --out c:\\temp\\all.json");
        Console.WriteLine("  dmm scan store xbox --roots \"D:\\XboxGames\" --no-visuals");
    }
}
