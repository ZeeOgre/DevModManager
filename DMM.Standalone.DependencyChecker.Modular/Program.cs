using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DMM.Core.IO;

namespace DmmDep.Modular
{
#nullable enable

    internal static class Program
    {
        private static bool s_quiet;
        private static bool s_silent;

        private static class Log
        {
            public static void Info(string message, bool isSkipped = false)
            {
                if (s_silent) return;
                if (isSkipped && s_quiet) return;
                Console.WriteLine(message);
            }

            public static void Warn(string message)
            {
                if (s_silent) return;
                Console.WriteLine(message);
            }

            public static void Always(string message) => Console.WriteLine(message);
        }

        private sealed class Options
        {
            public string PluginPath { get; set; } = "";
            public string? GameRootOverride { get; set; }
            public string? XboxDataOverride { get; set; }
            public string? TifRootOverride { get; set; } = "..\\..\\Source\\TGATextures";
            public string? ScriptsRootOverride { get; set; } = "Scripts";
            public bool TestMode { get; set; }
            public bool Quiet { get; set; }
            public bool Silent { get; set; }
            public bool SmartClobber { get; set; }
        }

        internal static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            var swTotal = Stopwatch.StartNew();

            try
            {
                var options = ParseArgs(args);
                if (options == null)
                {
                    PrintUsage();
                    return 1;
                }

                s_quiet = options.Quiet;
                s_silent = options.Silent;

                using IHost host = Host.CreateDefaultBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddFileSystem();
                        // TODO: services.AddDependencyChecker(); (once we move logic into DMM.Core)
                    })
                    .Build();

                var fs = host.Services.GetRequiredService<IFileSystem>();

                string pluginPath = Path.GetFullPath(options.PluginPath);
                if (!fs.FileExists(pluginPath))
                    throw new FileNotFoundException("Plugin file not found", pluginPath);

                Log.Always($"Starting dependency check (MODULAR): {Path.GetFileName(pluginPath)}");

                // NOTE: For now this is intentionally a stub wrapper. Next step is to:
                //  - move the dependency scanning to DMM.Core
                //  - call that engine here
                //
                // This wrapper already guarantees:
                //  - separate executable
                //  - same CLI surface area
                //  - DI + IFileSystem available for the modular implementation

                Log.Info("ProgramModular is wired up. Next step: call DMM.Core dependency engine.");
                swTotal.Stop();
                Log.Always($"Done in {swTotal.Elapsed}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("dmmdep_modular.exe <pluginPath> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --gameroot <path>     Override inferred game root (parent of Data).");
            Console.WriteLine("  --xboxdata <path>     Override XBOX Data root (default from CreationKit.ini).");
            Console.WriteLine("  --tifroot <path>      Override TIF root (default ..\\..\\Source\\TGATextures).");
            Console.WriteLine("  --scriptsroot <path>  Override Data\\Scripts root.");
            Console.WriteLine("  --test                Write .achlist.test instead of .achlist.");
            Console.WriteLine("  --quiet               Suppress output about skipped files (e.g. 'Skipping MAT').");
            Console.WriteLine("  --silent              Suppress all informational output except starting and completion.");
            Console.WriteLine("  --smartclobber        Seed candidates from existing .achlist.");
        }

        private static Options? ParseArgs(string[] args)
        {
            var opts = new Options();
            int i = 0;

            if (!args[0].StartsWith("-", StringComparison.Ordinal))
            {
                opts.PluginPath = args[0];
                i = 1;
            }

            for (; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg.ToLowerInvariant())
                {
                    case "--gameroot":
                        if (++i >= args.Length) return null;
                        opts.GameRootOverride = args[i];
                        break;
                    case "--xboxdata":
                        if (++i >= args.Length) return null;
                        opts.XboxDataOverride = args[i];
                        break;
                    case "--tifroot":
                        if (++i >= args.Length) return null;
                        opts.TifRootOverride = args[i];
                        break;
                    case "--scriptsroot":
                        if (++i >= args.Length) return null;
                        opts.ScriptsRootOverride = args[i];
                        break;
                    case "--test":
                        opts.TestMode = true;
                        break;
                    case "--quiet":
                        opts.Quiet = true;
                        break;
                    case "--silent":
                        opts.Silent = true;
                        break;
                    case "--smartclobber":
                        opts.SmartClobber = true;
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown option: {arg}");
                        return null;
                }
            }

            if (string.IsNullOrWhiteSpace(opts.PluginPath))
                return null;

            if (opts.Silent)
                opts.Quiet = true;

            return opts;
        }
    }
}