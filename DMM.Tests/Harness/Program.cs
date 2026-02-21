using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using DMM.AssetManagers.GameStores.Common;
using DMM.AssetManagers.GameStores.Common.Models;

using DMM.Tests.Harness.Infrastructure;

namespace DMM.Tests.Harness;

internal static class Program
{
    private static int Main(string[] args)
    {
        var totalSw = Stopwatch.StartNew();

        TimeSpan scanElapsed = TimeSpan.Zero;
        TimeSpan outputElapsed = TimeSpan.Zero;

        var parsed = CliArgs.Parse(args);

        if (parsed.ScanAll && parsed.Positionals.Count == 0)
        {
            parsed.Positionals.Clear();
            parsed.Positionals.Add("scan");
            parsed.Positionals.Add("all");
        }
        else if (parsed.Positionals.Count > 0
                 && string.Equals(parsed.Positionals[0], "scanall", StringComparison.OrdinalIgnoreCase))
        {
            parsed.Positionals.Clear();
            parsed.Positionals.Add("scan");
            parsed.Positionals.Add("all");
        }

        if (parsed.ShowHelp || parsed.Positionals.Count == 0)
        {
            CliHelp.Print();
            PrintTimings(totalSw, scanElapsed, outputElapsed);
            return parsed.Positionals.Count == 0 ? 1 : 0;
        }

        try
        {
            var scanners = ScannerRegistry.CreateAllScanners();

            var context = new StoreScanContext
            {
                IncludeVisualAssets = !parsed.NoVisuals,
                Roots = parsed.ResolveRoots()
            };

            var cmd = parsed.Positionals[0].ToLowerInvariant();

            switch (cmd)
            {
                case "help":
                case "--help":
                case "-h":
                    CliHelp.Print();
                    PrintTimings(totalSw, scanElapsed, outputElapsed);
                    return 0;

                case "stores":
                    {
                        var rc = RunStores(scanners);
                        PrintTimings(totalSw, scanElapsed, outputElapsed);
                        return rc;
                    }

                case "scan":
                    {
                        var rc = RunScan(scanners, context, parsed, out scanElapsed, out outputElapsed);
                        PrintTimings(totalSw, scanElapsed, outputElapsed);
                        return rc;
                    }

                default:
                    Console.WriteLine($"Unknown command: {cmd}");
                    CliHelp.Print();
                    PrintTimings(totalSw, scanElapsed, outputElapsed);
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fatal error:");
            Console.WriteLine(ex);
            PrintTimings(totalSw, scanElapsed, outputElapsed);
            return 1;
        }
    }

    private static void PrintTimings(Stopwatch totalSw, TimeSpan scan, TimeSpan output)
    {
        totalSw.Stop();

        // Print only what we actually measured
        if (scan != TimeSpan.Zero || output != TimeSpan.Zero)
            Console.WriteLine($"[Timing] scan={scan} output={output} total={totalSw.Elapsed}");
        else
            Console.WriteLine($"[Elapsed: {totalSw.Elapsed}]");
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

    private static int RunScan(
        IReadOnlyList<IStoreInstallScanner> scanners,
        StoreScanContext context,
        CliArgs parsed,
        out TimeSpan scanElapsed,
        out TimeSpan outputElapsed)
    {
        scanElapsed = TimeSpan.Zero;
        outputElapsed = TimeSpan.Zero;

        if (scanners.Count == 0)
        {
            Console.WriteLine("No store scanners are registered.");
            return 1;
        }

        if (parsed.Positionals.Count < 2)
        {
            Console.WriteLine("scan requires: all | store <storeKey>");
            return 1;
        }

        var mode = parsed.Positionals[1].ToLowerInvariant();
        var orchestrator = new StoreScanOrchestrator(scanners);

        InstallSnapshot snapshot;

        // Measure scan
        var scanSw = Stopwatch.StartNew();

        if (mode == "all")
        {
            snapshot = orchestrator.ScanStores(context);
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
            // Shorthand: scan <storeKey>
            var storeKey = parsed.Positionals[1];
            snapshot = orchestrator.ScanStore(storeKey, context);
        }

        scanSw.Stop();
        scanElapsed = scanSw.Elapsed;

        // Measure output
        var outSw = Stopwatch.StartNew();
        OutputWriter.Write(snapshot, parsed);
        outSw.Stop();
        outputElapsed = outSw.Elapsed;

        // Summary + exit codes
        Console.WriteLine();
        Console.WriteLine($"Apps: {snapshot.Apps.Count}");
        Console.WriteLine($"Issues: {snapshot.Issues.Count}");

        var byStore = snapshot.Apps
            .GroupBy(a => a.Id.StoreKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key);

        foreach (var g in byStore)
            Console.WriteLine($"  {g.Key}: {g.Count()} apps");

        return snapshot.Issues.Count > 0 ? 2 : 0;
    }
}
