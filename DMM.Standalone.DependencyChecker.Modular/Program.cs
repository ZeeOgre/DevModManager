using DMM.AssetManagers.NIF;

namespace DmmDep.Modular
{
#nullable enable

    internal static class Program
    {
        private static bool s_quiet;
        private static bool s_silent;
        private static bool s_trace;

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

        internal static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            if (string.Equals(args[0], "nif-readablemesh", StringComparison.OrdinalIgnoreCase))
            {
                return RunNifReadableMesh(args.Skip(1).ToArray());
            }

            if (string.Equals(args[0], "nif-dedupestrings", StringComparison.OrdinalIgnoreCase))
            {
                return RunNifDedupeStrings(args.Skip(1).ToArray());
            }

            PrintUsage();
            return 1;
        }

        private static int RunNifReadableMesh(string[] args)
        {
            string? folder = null;
            string? gameRoot = null;
            bool dryRun = false;
            bool overwrite = true;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--folder":
                        if (++i >= args.Length) return Fail("Missing value for --folder");
                        folder = args[i];
                        break;
                    case "--gameroot":
                        if (++i >= args.Length) return Fail("Missing value for --gameroot");
                        gameRoot = args[i];
                        break;
                    case "--dryrun":
                        dryRun = true;
                        break;
                    case "--no-overwrite":
                        overwrite = false;
                        break;
                    case "--quiet":
                        s_quiet = true;
                        break;
                    case "--silent":
                        s_silent = true;
                        s_quiet = true;
                        break;
                    case "--trace":
                        s_trace = true;
                        break;
                    default:
                        return Fail($"Unknown option: {args[i]}");
                }
            }

            if (string.IsNullOrWhiteSpace(folder))
                return Fail("--folder is required");

            string fullFolder = Path.GetFullPath(folder);
            if (!Directory.Exists(fullFolder))
                return Fail($"Folder does not exist: {fullFolder}");

            string resolvedGameRoot = ResolveGameRoot(gameRoot, fullFolder);
            var reader = new NifReader();
            var editor = new NifEditor(reader);
            var writer = new NifWriter();

            var nifFiles = Directory.EnumerateFiles(fullFolder, "*.nif", SearchOption.AllDirectories).ToList();
            Log.Always($"nif-readablemesh: scanning {nifFiles.Count} NIF(s)");

            int copied = 0;
            int rewritten = 0;
            int rewriteFailures = 0;
            int planEntries = 0;

            foreach (string nifPath in nifFiles)
            {
                var fileTimer = s_trace ? System.Diagnostics.Stopwatch.StartNew() : null;
                IReadOnlyList<NifReadableMeshCopy> plan;
                try
                {
                    plan = editor.BuildReadableMeshCopyPlan(nifPath, resolvedGameRoot);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Skipping '{nifPath}': {ex.Message}");
                    continue;
                }

                if (plan.Count == 0)
                {
                    Log.Info($"No mesh entries: {nifPath}", isSkipped: true);
                    continue;
                }

                planEntries += plan.Count;

                if (!dryRun)
                {
                    copied += writer.ExecuteReadableMeshCopyPlan(plan, overwrite);

                    var replacementMap = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (NifReadableMeshCopy item in plan)
                    {
                        if (!File.Exists(item.SourceMeshPath))
                            continue;

                        replacementMap[item.OriginalMeshToken] = item.RewrittenMeshToken;
                        replacementMap[item.OriginalMeshTokenNormalized] = item.RewrittenMeshToken;
                    }

                    try
                    {
                        rewritten += writer.RewriteStringsInPlace(nifPath, replacementMap);
                    }
                    catch (InvalidOperationException ex)
                    {
                        rewriteFailures++;
                        Log.Warn($"Remap failed for '{nifPath}': {ex.Message}");
                    }
                }

                if (s_trace)
                {
                    Log.Always($"TRACE nif-readablemesh | file='{nifPath}', plan={plan.Count}, elapsedMs={fileTimer?.ElapsedMilliseconds ?? 0}");
                }
            }

            Log.Always($"nif-readablemesh complete | nif={nifFiles.Count}, plan={planEntries}, copied={copied}, rewritten={rewritten}, rewriteFailures={rewriteFailures}, dryrun={dryRun}, elapsedMs={stopwatch.ElapsedMilliseconds}");
            return 0;
        }

        private static int RunNifDedupeStrings(string[] args)
        {
            string? folder = null;
            bool dryRun = true;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--folder":
                        if (++i >= args.Length) return Fail("Missing value for --folder");
                        folder = args[i];
                        break;
                    case "--apply":
                        dryRun = false;
                        break;
                    case "--quiet":
                        s_quiet = true;
                        break;
                    case "--silent":
                        s_silent = true;
                        s_quiet = true;
                        break;
                    case "--trace":
                        s_trace = true;
                        break;
                    default:
                        return Fail($"Unknown option: {args[i]}");
                }
            }

            if (string.IsNullOrWhiteSpace(folder))
                return Fail("--folder is required");

            string fullFolder = Path.GetFullPath(folder);
            if (!Directory.Exists(fullFolder))
                return Fail($"Folder does not exist: {fullFolder}");

            var reader = new NifReader();
            var editor = new NifEditor(reader);
            var nifFiles = Directory.EnumerateFiles(fullFolder, "*.nif", SearchOption.AllDirectories).ToList();

            int filesWithDupes = 0;
            int remapTotal = 0;
            foreach (string nifPath in nifFiles)
            {
                var fileTimer = s_trace ? System.Diagnostics.Stopwatch.StartNew() : null;
                NifStringRewritePlan plan = editor.BuildDeduplicateStringPlan(nifPath);
                if (plan.Remap.Count == 0)
                    continue;

                filesWithDupes++;
                remapTotal += plan.Remap.Count;

                if (!dryRun)
                {
                    Log.Warn($"nif-dedupestrings apply-mode is not yet implemented for '{nifPath}' (requires structured NIF reserialization).");
                }

                if (s_trace)
                {
                    Log.Always($"TRACE nif-dedupestrings | file='{nifPath}', remaps={plan.Remap.Count}, elapsedMs={fileTimer?.ElapsedMilliseconds ?? 0}");
                }
            }

            Log.Always($"nif-dedupestrings complete | nif={nifFiles.Count}, filesWithDupes={filesWithDupes}, remaps={remapTotal}, apply={(!dryRun)}");
            return 0;
        }

        private static string ResolveGameRoot(string? explicitGameRoot, string folder)
        {
            if (!string.IsNullOrWhiteSpace(explicitGameRoot))
                return Path.GetFullPath(explicitGameRoot);

            string fullFolder = Path.GetFullPath(folder);
            string marker = Path.DirectorySeparatorChar + Path.Combine("Data", "Meshes") + Path.DirectorySeparatorChar;
            int idx = fullFolder.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return fullFolder.Substring(0, idx);
            }

            // fallback: parent of folder's parent
            DirectoryInfo? parent = Directory.GetParent(fullFolder);
            DirectoryInfo? grand = parent?.Parent;
            return grand?.FullName ?? fullFolder;
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine($"ERROR: {message}");
            return 1;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("dmmdep_modular.exe nif-readablemesh --folder <Data\\Meshes\\...> [options]");
            Console.WriteLine("dmmdep_modular.exe nif-dedupestrings --folder <Data\\Meshes\\...> [--apply] [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --gameroot <path>     Override game root (folder that contains Data).");
            Console.WriteLine("  --dryrun              Plan only; do not copy or rewrite NIFs.");
            Console.WriteLine("  --apply               For nif-dedupestrings, attempt apply mode (currently reports unsupported per-file).");
            Console.WriteLine("  --no-overwrite        Do not overwrite destination mesh files.");
            Console.WriteLine("  --trace               Print per-file timing diagnostics.");
            Console.WriteLine("  --quiet               Suppress skipped-file informational output.");
            Console.WriteLine("  --silent              Suppress non-essential output.");
        }
    }
}
