using System;
using System.Collections.Generic;
using System.Linq;

using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;
using DMM.AssetManagers.GameStores.XBox;


using DMM.Tests.Harness.Infrastructure;

namespace DMM.Tests.Harness;

internal static class Program
{
    private static int Main(string[] args)
    {
        var parsed = CliArgs.Parse(args);

        if (parsed.ShowHelp || parsed.Positionals.Count == 0)
        {
            CliHelp.Print();
            return parsed.Positionals.Count == 0 ? 1 : 0;
        }

        try
        {
            // Register all available scanners here
            var scanners = ScannerRegistry.CreateAllScanners();

            // Common context values
            var context = new StoreScanContext
            {
                IncludeVisualAssets = !parsed.NoVisuals,
                Roots = parsed.ResolveRoots()
            };

            // Commands
            var cmd = parsed.Positionals[0].ToLowerInvariant();

            switch (cmd)
            {
                case "help":
                case "--help":
                case "-h":
                    CliHelp.Print();
                    return 0;

                case "stores":
                    return RunStores(scanners);

                case "scan":
                    return RunScan(scanners, context, parsed);

                default:
                    Console.WriteLine($"Unknown command: {cmd}");
                    CliHelp.Print();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fatal error:");
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static int RunStores(IReadOnlyList<IStoreInstallScanner> scanners)
    {
        if (scanners.Count == 0)
        {
            Console.WriteLine("No store scanners are registered.");
            return 1;
        }

        Console.WriteLine("Registered store scanners:");
        foreach (var s in scanners.OrderBy(s => s.StoreKey))
            Console.WriteLine($"- {s.StoreKey} ({s.GetType().Name})");

        return 0;
    }

    private static int RunScan(IReadOnlyList<IStoreInstallScanner> scanners, StoreScanContext context, CliArgs parsed)
    {
        if (scanners.Count == 0)
        {
            Console.WriteLine("No store scanners are registered.");
            return 1;
        }

        // Syntax:
        //   scan all
        //   scan store <storeKey>
        if (parsed.Positionals.Count < 2)
        {
            Console.WriteLine("scan requires: all | store <storeKey>");
            return 1;
        }

        var mode = parsed.Positionals[1].ToLowerInvariant();

        // Build orchestrator
        var orchestrator = new StoreScanner(scanners);

        InstallSnapshot snapshot;

        if (mode == "all")
        {
            snapshot = orchestrator.ScanAll(context);
        }
        else if (mode == "store")
        {
            if (parsed.Positionals.Count < 3)
            {
                Console.WriteLine("scan store requires <storeKey>.");
                return 1;
            }

            var storeKey = parsed.Positionals[2];
            snapshot = orchestrator.ScanStore(storeKey, context);
        }
        else
        {
            Console.WriteLine("Invalid scan syntax. Use: scan all | scan store <storeKey>");
            return 1;
        }

        // Output
        OutputWriter.Write(snapshot, parsed);

        // Summary + exit codes
        Console.WriteLine();
        Console.WriteLine($"Apps: {snapshot.Apps.Count}");
        Console.WriteLine($"Issues: {snapshot.Issues.Count}");

        // Helpful per-store summary
        var byStore = snapshot.Apps
            .GroupBy(a => a.Id.StoreKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key);

        foreach (var g in byStore)
            Console.WriteLine($"  {g.Key}: {g.Count()} apps");

        return snapshot.Issues.Count > 0 ? 2 : 0;
    }
}
