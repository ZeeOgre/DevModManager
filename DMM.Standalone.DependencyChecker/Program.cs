using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace DmmDep
{
#nullable enable

    internal enum FileKind { Pex, Psc, Nif, Mat, Texture, Mesh, Voice, Terrain, Icon, Tif, BackupOnly, Particle, Anim, Morph, Rig, Warn, Other }

    internal sealed class FileEntry
    {
        public string PcPath { get; set; } = "";
        public string? XboxPath { get; set; }
        public string Kind { get; set; } = "";
        public string Source { get; set; } = "";
    }

    internal sealed class DependencyManifest
    {
        public string Plugin { get; set; } = "";
        public string GameRoot { get; set; } = "";
        public string DataRoot { get; set; } = "";
        public string XboxDataRoot { get; set; } = "";
        public string TifRoot { get; set; } = "";
        public List<FileEntry> Files { get; set; } = new();
    }

    internal sealed class Options
    {
        public string PluginPath { get; set; } = "";
        public string? GameRootOverride { get; set; }
        public string? XboxDataOverride { get; set; }
        public string? TifRootOverride { get; set; } = "..\\..\\Source\\TGATextures";
        public string? ScriptsRootOverride { get; set; } = "Scripts";
        public bool TestMode { get; set; } // --test switch

        // New switches
        public bool Quiet { get; set; }   // --quiet : suppress skipped-file messages
        public bool Silent { get; set; }  // --silent: only show start and completion
    }

    internal static class Program
    {
        // runtime flags populated from options
        private static bool s_quiet = false;
        private static bool s_silent = false;

        // logger helpers honoring --quiet / --silent
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

            // Always printed regardless of --silent
            public static void Always(string message)
            {
                Console.WriteLine(message);
            }
        }

        static int Main(string[] args)
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

                // ---- Root detection ----

                string pluginPath = Path.GetFullPath(options.PluginPath);
                if (!File.Exists(pluginPath))
                    throw new FileNotFoundException("Plugin file not found", pluginPath);

                // Print a concise starting message regardless of --silent
                Log.Always($"Starting dependency check: {Path.GetFileName(pluginPath)}");

                string dataRoot = Path.GetDirectoryName(pluginPath)
                                  ?? throw new InvalidOperationException("Unable to get Data root from plugin path");
                string gameRoot = options.GameRootOverride ??
                                  Directory.GetParent(dataRoot)?.FullName ??
                                  throw new InvalidOperationException("Unable to infer game root (parent of Data)");

                Log.Info($"Plugin   : {pluginPath}");
                Log.Info($"DataRoot : {dataRoot}");
                Log.Info($"GameRoot : {gameRoot}");

                // CreationKit.ini / CreationKitCustom.ini (for XB path detection)
                string? iniPath = FindCreationKitIni(gameRoot);
                if (iniPath == null && options.XboxDataOverride == null)
                    throw new InvalidOperationException("CreationKit.ini / CreationKitCustom.ini not found. Use --xboxdata to specify XBOX Data root.");

                string xboxDataRoot = options.XboxDataOverride ?? InferXboxDataRoot(gameRoot, iniPath);
                Log.Info($"XboxData : {xboxDataRoot}");

                // TIF root: ..\..\Source\TGATextures from game root, unless overridden
                string tifRoot = options.TifRootOverride ??
                                 Path.GetFullPath(Path.Combine(gameRoot, "..", "..", "Source", "TGATextures"));
                Log.Info($"TifRoot  : {tifRoot}");

                // Scripts root
                string scriptsRoot = options.ScriptsRootOverride ?? Path.Combine(dataRoot, "Scripts");
                string scriptsSourceRoot = Path.Combine(scriptsRoot, "Source");
                Log.Info($"Scripts  : {scriptsRoot}");

                string pluginName = Path.GetFileNameWithoutExtension(pluginPath);
                string outputRoot = Path.GetDirectoryName(pluginPath)!;

                // ---- Manifest + achlist containers ----

                var manifest = new DependencyManifest
                {
                    Plugin = Path.GetFileName(pluginPath),
                    GameRoot = gameRoot,
                    DataRoot = dataRoot,
                    XboxDataRoot = xboxDataRoot,
                    TifRoot = tifRoot
                };

                var achlistPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // ---- 1. Scan plugin for NIF / terrain / MAT / misc strings ----

                Log.Info("[1] Scanning plugin for NIF / terrain / MAT / misc strings...");
                var pluginBytes = File.ReadAllBytes(pluginPath);
                var pluginStrings = ExtractPrintableStrings(pluginBytes, 6).ToList();
                Log.Info($"[1] Extracted {pluginStrings.Count} printable strings (len >= 6)");

                var nifRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var btdNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var matRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var s in pluginStrings)
                {
                    // --- NIFs ---
                    if (s.EndsWith(".nif", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = s.Replace('/', '\\').TrimStart('\\');
                        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                            rel = Path.Combine("Data\\Meshes", rel);
                        rel = NormalizeRel(rel);
                        if (File.Exists(Path.Combine(gameRoot, rel)))
                        {
                            if (nifRelPaths.Add(rel))
                            {
                                AddFile(manifest, achlistPaths, rel, FileKind.Nif, "plugin-nif", gameRoot, xboxDataRoot);
                            }
                        }
                    }
                    // --- .mat from plugin (e.g. MTPT / material path records) ---
                    else if (s.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = s.Replace('/', '\\').TrimStart('\\');
                        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                        {
                            rel = rel.StartsWith("Materials\\", StringComparison.OrdinalIgnoreCase)
                                ? Path.Combine("Data", rel)
                                : Path.Combine("Data\\Materials", rel);
                        }
                        rel = NormalizeRel(rel);
                        if (File.Exists(Path.Combine(gameRoot, rel)))
                        {
                            matRelPaths.Add(rel);
                        }
                    }
                    // --- Terrain .btd ---
                    else if (s.EndsWith(".btd", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = s.Replace('/', '\\').TrimStart('\\');
                        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                            rel = Path.Combine("Data\\terrain", rel);
                        rel = NormalizeRel(rel);
                        string full = Path.Combine(gameRoot, rel);
                        if (File.Exists(full))
                        {
                            AddFile(manifest, achlistPaths, rel, FileKind.Terrain, "plugin-btd", gameRoot, xboxDataRoot);
                            string baseName = Path.GetFileNameWithoutExtension(rel);
                            if (!string.IsNullOrEmpty(baseName))
                            {
                                btdNames.Add(baseName);
                            }
                        }
                        else
                        {
                            string baseName = Path.GetFileNameWithoutExtension(rel);
                            if (!string.IsNullOrEmpty(baseName))
                            {
                                btdNames.Add(baseName);
                            }
                        }
                    }
                    // --- Particles (.psfx) ---
                    else if (s.EndsWith(".psfx", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = s.Replace('/', '\\').TrimStart('\\');
                        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                            rel = Path.Combine("Data", rel);
                        rel = NormalizeRel(rel);
                        if (File.Exists(Path.Combine(gameRoot, rel)))
                        {
                            AddFile(manifest, achlistPaths, rel, FileKind.Particle, "plugin-psfx", gameRoot, xboxDataRoot);
                        }
                    }
                    // --- Animation / behavior assets (af, afx, agx) ---
                    else if (s.EndsWith(".af", StringComparison.OrdinalIgnoreCase) ||
                             s.EndsWith(".afx", StringComparison.OrdinalIgnoreCase) ||
                             s.EndsWith(".agx", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = s.Replace('/', '\\').TrimStart('\\');
                        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                            rel = Path.Combine("Data", rel);
                        rel = NormalizeRel(rel);
                        if (File.Exists(Path.Combine(gameRoot, rel)))
                        {
                            AddFile(manifest, achlistPaths, rel, FileKind.Anim, "plugin-anim", gameRoot, xboxDataRoot);
                        }
                    }
                    // --- Morph targets (.morph) ---
                    else if (s.EndsWith(".morph", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = s.Replace('/', '\\').TrimStart('\\');
                        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                            rel = Path.Combine("Data", rel);
                        rel = NormalizeRel(rel);
                        if (File.Exists(Path.Combine(gameRoot, rel)))
                        {
                            AddFile(manifest, achlistPaths, rel, FileKind.Morph, "plugin-morph", gameRoot, xboxDataRoot);
                        }
                    }
                    // --- Fallback: other extensions that look like paths -> WARN ---
                    else
                    {
                        string candidate = s.Replace('/', '\\');
                        int dot = candidate.LastIndexOf('.');
                        int slash = candidate.LastIndexOf('\\');
                        if (dot > 0 && slash >= 0 && dot > slash)
                        {
                            string ext = candidate.Substring(dot);
                            if (ext.Length > 1 && ext.Length <= 8)
                            {
                                string rel = candidate.TrimStart('\\');
                                if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                                    rel = Path.Combine("Data", rel);
                                rel = NormalizeRel(rel);
                                string full = Path.Combine(gameRoot, rel);
                                if (File.Exists(full))
                                {
                                    Log.Warn($"[WARN] Unclassified asset extension '{ext}' from plugin: {rel}");
                                    AddFile(manifest, achlistPaths, rel, FileKind.Warn, "plugin-warn", gameRoot, xboxDataRoot);
                                }
                            }
                        }
                    }
                }

                // Terrain backup-only folder Data\terrain\<modname>\**
                string modTerrainFolder = Path.Combine(gameRoot, "Data", "terrain", pluginName);
                if (Directory.Exists(modTerrainFolder))
                {
                    Log.Info("[1b] Adding terrain backup folder...");
                    foreach (var f in Directory.EnumerateFiles(modTerrainFolder, "*.*", SearchOption.AllDirectories))
                    {
                        string rel = "Data\\" + GetRelativePath(Path.Combine(gameRoot, "Data"), f);
                        rel = NormalizeRel(rel);
                        AddBackupOnlyFile(manifest, rel, "terrain-folder");
                    }
                }

                // OverlayMasks from .btd -> Textures\Terrain\OverlayMasks\<name>.dds
                Log.Info("[1c] Checking terrain overlay dds from .btd...");
                foreach (var n in btdNames)
                {
                    string rel = NormalizeRel(Path.Combine("Data", "Textures", "Terrain", "OverlayMasks", n + ".dds"));
                    string full = Path.Combine(gameRoot, rel);
                    if (File.Exists(full))
                    {
                        AddFile(manifest, achlistPaths, rel, FileKind.Texture, "btd-overlay", gameRoot, xboxDataRoot);
                        TryAddInterfaceTifForTexture(manifest, rel, tifRoot);
                    }
                }

                // ---- 2. NIF -> MAT + MeshPath + RIG ----
                Log.Info("[2] Scanning NIFs for MAT, mesh stems, and RIG...");

                foreach (var nifRel in nifRelPaths)
                {
                    string full = Path.Combine(gameRoot, nifRel);
                    if (!File.Exists(full)) continue;

                    var nifBytes = File.ReadAllBytes(full);

                    foreach (var s in ExtractPrintableStrings(nifBytes, 4))
                    {
                        string token = s.Replace('/', '\\').TrimStart('\\');

                        if (token.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                        {
                            string rel = token;

                            if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                            {
                                rel = rel.StartsWith("Materials\\", StringComparison.OrdinalIgnoreCase)
                                    ? Path.Combine("Data", rel)
                                    : Path.Combine("Data\\Materials", rel);
                            }
                            rel = NormalizeRel(rel);
                            string fullMat = Path.Combine(gameRoot, rel);

                            if (File.Exists(fullMat))
                            {
                                if (matRelPaths.Add(rel))
                                {
                                    //Console.WriteLine($"[2] Found MAT '{rel}' in NIF '{nifRel}'");
                                }
                            }
                            else
                            {
                                Log.Warn($"[2] Presumed MAT path '{rel}' from NIF '{nifRel}' does not exist");
                            }
                        }
                        else if (token.EndsWith(".rig", StringComparison.OrdinalIgnoreCase))
                        {
                            string rel = token;
                            if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                            {
                                rel = rel.StartsWith("meshes\\", StringComparison.OrdinalIgnoreCase)
                                    ? NormalizeRel(Path.Combine("Data", rel))
                                    : NormalizeRel(Path.Combine("Data\\Meshes", rel));
                            }
                            else
                            {
                                rel = NormalizeRel(rel);
                            }

                            if (File.Exists(Path.Combine(gameRoot, rel)))
                            {
                                AddFile(manifest, achlistPaths, rel, FileKind.Rig, $"nif:{nifRel}", gameRoot, xboxDataRoot);
                            }
                        }
                        else if (!token.Contains('.') && token.Contains("\\"))
                        {
                            string stem = token.TrimStart('\\');
                            string meshRel = stem.StartsWith("geometries\\", StringComparison.OrdinalIgnoreCase)
                                ? NormalizeRel(Path.Combine("Data", stem + ".mesh"))
                                : NormalizeRel(Path.Combine("Data\\geometries", stem + ".mesh"));

                            if (File.Exists(Path.Combine(gameRoot, meshRel)))
                            {
                                AddFile(manifest, achlistPaths, meshRel, FileKind.Mesh, $"nif:{nifRel}", gameRoot, xboxDataRoot);
                            }
                        }
                    }
                }

                // ---- 3. MATs -> DDS ----
                Log.Info("[3] Scanning MATs for DDS tokens (via JSON File/FileName)...");
                int matsWithCustom = 0;
                int totalDdsHits = 0;

                foreach (var matRel in matRelPaths)
                {
                    string fullMat = Path.Combine(gameRoot, matRel);
                    if (!File.Exists(fullMat))
                        continue;

                    string matText = File.ReadAllText(fullMat);
                    var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool hasCustomTextures = false;

                    List<string> ddsTokens = new();
                    try
                    {
                        using var doc = JsonDocument.Parse(matText);
                        CollectMatDdsTokensFromJson(doc.RootElement, ddsTokens);
                    }
                    catch
                    {
                        var ddsAnyRegex = new Regex(@"([A-Za-z0-9_\\/\-\.]+\.dds)\b", RegexOptions.IgnoreCase);
                        foreach (Match m in ddsAnyRegex.Matches(matText))
                            ddsTokens.Add(m.Groups[1].Value);
                    }

                    foreach (var raw in ddsTokens)
                    {
                        string? ddsRel = NormalizeDdsPathFromMat(raw);
                        if (ddsRel == null)
                            continue;

                        string fullTexPc = Path.Combine(gameRoot, ddsRel);
                        bool pcExists = File.Exists(fullTexPc);

                        string? xbCandidate = null;
                        bool xbExists = false;
                        if (ddsRel.StartsWith("Data\\Textures", StringComparison.OrdinalIgnoreCase))
                        {
                            xbCandidate = Path.Combine(xboxDataRoot, GetRelativePath(Path.Combine(gameRoot, "Data"), fullTexPc));
                            xbExists = File.Exists(xbCandidate);
                        }

                        if (!pcExists && !xbExists)
                            continue;

                        if (pcExists && xbExists)
                        {
                            if (found.Add(ddsRel))
                            {
                                hasCustomTextures = true;
                                totalDdsHits++;
                                AddFile(manifest, achlistPaths, ddsRel, FileKind.Texture, $"mat:{matRel}", gameRoot, xboxDataRoot);
                                TryAddInterfaceTifForTexture(manifest, ddsRel, tifRoot);
                            }
                            continue;
                        }

                        if (!pcExists && xbExists)
                        {
                            string relXb = "XBOX\\Data\\" + GetRelativePath(xboxDataRoot, xbCandidate!);
                            Log.Warn($"[WARN] PC texture missing; regenerate and try again -> {relXb}");
                            continue;
                        }
                        if (pcExists && !xbExists && xbCandidate != null)
                        {
                            string relXb = "XBOX\\Data\\" + GetRelativePath(xboxDataRoot, xbCandidate);
                            Log.Warn($"[WARN] XBOX texture missing; regenerate and try again -> {relXb}");
                            if (found.Add(ddsRel))
                            {
                                hasCustomTextures = true;
                                totalDdsHits++;
                                AddFile(manifest, achlistPaths, ddsRel, FileKind.Texture, $"mat:{matRel}", gameRoot, xboxDataRoot);
                                TryAddInterfaceTifForTexture(manifest, ddsRel, tifRoot);
                            }
                        }
                    }

                    if (hasCustomTextures)
                    {
                        matsWithCustom++;
                        AddFile(manifest, achlistPaths, matRel, FileKind.Mat, "mat-with-custom-dds", gameRoot, xboxDataRoot);
                    }
                    else
                    {
                        // Mark this as a "skipped files" informational message (suppressed by --quiet)
                        Log.Info($"[3] Skipping MAT (no existing custom textures): {matRel}", isSkipped: true);
                    }
                }

                Log.Info($"[3] MATs with custom DDS: {matsWithCustom}, total DDS hits: {totalDdsHits}");

                // ---- 4. Interface icons + shipbuilder + workshop ----

                Log.Info("[4] Collecting interface icons / previews...");
                CollectIconsAndPreviews(manifest, achlistPaths, pluginName, gameRoot, xboxDataRoot, tifRoot);

                // ---- 5. Voice assets (PC dev + runtime + XB) ----

                Log.Info("[5] Collecting voice assets...");
                CollectVoiceAssets(manifest, achlistPaths, pluginName, gameRoot, xboxDataRoot);

                // ---- 6. Scripts (Bethesda Script Name format with imports) ----
                static string ToPscRel(string name) =>
                    NormalizeRel(Path.Combine("Data", "Scripts", "Source", name.Replace(':', '\\') + ".psc"));
                static string ToPexRel(string name) =>
                    NormalizeRel(Path.Combine("Data", "Scripts", name.Replace(':', '\\') + ".pex"));
                // ---- 6. Scripts (Bethesda script names + PSC imports) ----

                Log.Info("[6] Script discovery from plugin text + PSC imports...");
                // NOTE: Script files (PSC/PEX) may be overrepresented here because we only
                // check for their presence on disk. We do not inspect parent archives that
                // might contain scripts, so some scripts may appear present when they are
                // actually provided by archives or other distribution mechanisms.
                Log.Warn("[NOTE] Script files (PSC/PEX) may be overrepresented; only filesystem presence is checked, not parent archives (potential future enhancement).");

                var rootScriptNames = ExtractScriptNamesFromPlugin(pluginBytes, gameRoot);
                Log.Info($"[6] Root script names from plugin: {rootScriptNames.Count}");

                var allScriptNames = ExpandScriptImports(rootScriptNames, gameRoot);
                Log.Info($"[6] After PSC imports: {allScriptNames.Count} total script names");

                var pscSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pexSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var name in allScriptNames)
                {
                    string pscRel = ScriptNameToPscRel(name);
                    string pexRel = ScriptNameToPexRel(name);

                    string fullPsc = Path.Combine(gameRoot, pscRel);
                    string fullPex = Path.Combine(gameRoot, pexRel);

                    if (File.Exists(fullPsc) && pscSet.Add(pscRel))
                        AddBackupOnlyFile(manifest, pscRel, "script-psc-or-import");

                    if (File.Exists(fullPex) && pexSet.Add(pexRel))
                        AddFile(manifest, achlistPaths, pexRel, FileKind.Pex, "script-pex-or-import", gameRoot, xboxDataRoot);
                }

                // Second pass: imports from PSC
                var pscSetSnapshot = new HashSet<string>(pscSet, StringComparer.OrdinalIgnoreCase);
                var importRegex = new Regex(@"^\s*import\s+([A-Za-z0-9_:.]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

                foreach (var pscRel in pscSetSnapshot)
                {
                    string fullPsc = Path.Combine(gameRoot, pscRel);
                    if (!File.Exists(fullPsc)) continue;

                    string text = File.ReadAllText(fullPsc);
                    foreach (Match m in importRegex.Matches(text))
                    {
                        string importName = m.Groups[1].Value.Trim();

                        string impPscRel = ToPscRel(importName);
                        string impPexRel = ToPexRel(importName);

                        if (File.Exists(Path.Combine(gameRoot, impPscRel)) && pscSet.Add(impPscRel))
                            AddBackupOnlyFile(manifest, impPscRel, "psc-import");

                        if (File.Exists(Path.Combine(gameRoot, impPexRel)) && pexSet.Add(impPexRel))
                            AddFile(manifest, achlistPaths, impPexRel, FileKind.Pex, "pex-from-psc-import", gameRoot, xboxDataRoot);
                    }
                }

                Log.Info($"[6] After PSC imports: {pscSet.Count} PSC, {pexSet.Count} PEX");


                // ---- Outputs ----

                string achlistFileName = pluginName + (options.TestMode ? ".achlist.test" : ".achlist");
                string achlistPath = Path.Combine(outputRoot, achlistFileName);
                WriteAchlistJsonAsciiCrLf(achlistPath, achlistPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
                Log.Info($"Wrote achlist : {achlistPath} (total {achlistPaths.Count})");

                string csvPath = Path.Combine(outputRoot, pluginName + "_deps.csv");
                WriteDepsCsv(csvPath, manifest.Files);
                Log.Info($"Wrote deps (csv) : {csvPath} (total {manifest.Files.Count})");

                string jsonPath = Path.Combine(outputRoot, pluginName + "_deps.json");
                // JSON output is currently not used and can be very large. Skip writing it.
                // If needed in future re-enable serialization below.
                // var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
                // File.WriteAllText(jsonPath, JsonSerializer.Serialize(manifest, jsonOpts));
                Log.Info($"Skipped writing JSON deps file: {jsonPath} (disabled to reduce output size)");

                swTotal.Stop();
                // Completion message always shown regardless of --silent
                Log.Always($"Done in {swTotal.Elapsed} | achlist={achlistPaths.Count}, deps={manifest.Files.Count}");
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
            Console.WriteLine("dmmdep.exe <pluginPath> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --gameroot <path>     Override inferred game root (parent of Data).");
            Console.WriteLine("  --xboxdata <path>     Override XBOX Data root (default from CreationKit.ini).");
            Console.WriteLine("  --tifroot <path>      Override TIF root (default ..\\..\\Source\\TGATextures).");
            Console.WriteLine("  --scriptsroot <path>  Override Data\\Scripts root.");
            Console.WriteLine("  --test                Write .achlist.test instead of .achlist.");
            Console.WriteLine("  --quiet               Suppress output about skipped files (e.g. 'Skipping MAT').");
            Console.WriteLine("  --silent              Suppress all informational output except starting and completion.");
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
                    default:
                        Console.Error.WriteLine($"Unknown option: {arg}");
                        return null;
                }
            }

            if (string.IsNullOrWhiteSpace(opts.PluginPath))
                return null;

            // if --silent is set, it implies --quiet
            if (opts.Silent)
                opts.Quiet = true;

            return opts;
        }

        private static string? FindCreationKitIni(string gameRoot)
        {
            string custom = Path.Combine(gameRoot, "CreationKitCustom.ini");
            if (File.Exists(custom)) return custom;
            string ini = Path.Combine(gameRoot, "CreationKit.ini");
            return File.Exists(ini) ? ini : null;
        }

        private static string InferXboxDataRoot(string gameRoot, string? iniPath)
        {
            if (iniPath == null)
            {
                string fallback = Path.Combine(gameRoot, "XBOX", "Data");
                if (Directory.Exists(fallback)) return fallback;
                throw new InvalidOperationException("Unable to infer XBOX Data root and CreationKit.ini not found.");
            }

            string xbPath = "";
            string currentSection = "";
            foreach (var lineRaw in File.ReadAllLines(iniPath))
            {
                string line = lineRaw.Trim();
                if (line.StartsWith(";") || line.Length == 0) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                if (!string.Equals(currentSection, "Audio", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (line.StartsWith("sPathToVoiceOutputXB", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        xbPath = parts[1].Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(xbPath))
            {
                string fallback = Path.Combine(gameRoot, "XBOX", "Data");
                if (Directory.Exists(fallback)) return fallback;
                throw new InvalidOperationException("sPathToVoiceOutputXB not found in CreationKit.ini; use --xboxdata.");
            }

            string[] segs = xbPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length < 2)
                throw new InvalidOperationException("sPathToVoiceOutputXB value is unexpected: " + xbPath);

            string xbRoot = Path.Combine(gameRoot, segs[0]); // XBOX
            string xbData = Path.Combine(xbRoot, segs[1]);   // XBOX\Data

            if (!Directory.Exists(xbData))
                throw new DirectoryNotFoundException("Inferred XBOX Data root not found: " + xbData);

            return xbData;
        }

        private static string NormalizeRel(string rel)
        {
            rel = rel.Replace('/', '\\');
            if (rel.StartsWith(".\\", StringComparison.Ordinal))
                rel = rel.Substring(2);
            return rel;
        }

        private static IEnumerable<string> ExtractPrintableStrings(byte[] data, int minLen)
        {
            var sb = new StringBuilder();
            foreach (byte b in data)
            {
                if (b >= 32 && b <= 126)
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

        private static string GetRelativePath(string root, string fullPath)
        {
#if NET8_0_OR_GREATER
            return Path.GetRelativePath(root, fullPath);
#else
            var u1 = new Uri(root.EndsWith("\\") ? root : root + "\\");
            var u2 = new Uri(fullPath);
            return Uri.UnescapeDataString(u1.MakeRelativeUri(u2).ToString()).Replace('/', '\\');
#endif
        }

        private static void AddFile(
            DependencyManifest manifest,
            HashSet<string> achlist,
            string relPcPath,
            FileKind kind,
            string source,
            string gameRoot,
            string xboxDataRoot)
        {
            relPcPath = NormalizeRel(relPcPath);
            string fullPc = Path.Combine(gameRoot, relPcPath);
            if (!File.Exists(fullPc))
                return;

            string? xboxRel = null;

            if (relPcPath.StartsWith("Data\\Sound", StringComparison.OrdinalIgnoreCase) ||
                relPcPath.StartsWith("Data\\Textures", StringComparison.OrdinalIgnoreCase))
            {
                string candidate = Path.Combine(
                    xboxDataRoot,
                    GetRelativePath(Path.Combine(gameRoot, "Data"), fullPc));

                if (File.Exists(candidate))
                {
                    string relXb = "XBOX\\Data\\" + GetRelativePath(xboxDataRoot, candidate);
                    xboxRel = NormalizeRel(relXb);
                }
            }

            achlist.Add(relPcPath);

            if (!manifest.Files.Any(f => f.PcPath.Equals(relPcPath, StringComparison.OrdinalIgnoreCase)))
            {
                manifest.Files.Add(new FileEntry
                {
                    PcPath = relPcPath,
                    XboxPath = xboxRel,
                    Kind = kind.ToString().ToLowerInvariant(),
                    Source = source
                });
            }
        }

        private static void AddBackupOnlyFile(DependencyManifest manifest, string relPcPath, string source)
        {
            relPcPath = NormalizeRel(relPcPath);
            if (!manifest.Files.Any(f => string.Equals(f.PcPath, relPcPath, StringComparison.OrdinalIgnoreCase)))
            {
                manifest.Files.Add(new FileEntry
                {
                    PcPath = relPcPath,
                    XboxPath = null,
                    Kind = FileKind.BackupOnly.ToString().ToLowerInvariant(),
                    Source = source
                });
            }
        }

        private static void CollectIconsAndPreviews(
            DependencyManifest manifest,
            HashSet<string> achlist,
            string pluginName,
            string gameRoot,
            string xboxDataRoot,
            string tifRoot)
        {
            string dataRoot = Path.Combine(gameRoot, "Data");
            string[] iconTypes =
            {
                "InventoryIcons",
                "ShipBuilderIcons",
                "WorkshopIcons"
            };

            foreach (var type in iconTypes)
            {
                string root = Path.Combine(dataRoot, "Textures", "Interface", type, pluginName + ".esm");
                if (!Directory.Exists(root)) continue;

                foreach (string dds in Directory.EnumerateFiles(root, "*.dds", SearchOption.AllDirectories))
                {
                    string relUnderData = GetRelativePath(dataRoot, dds);
                    string relPc = NormalizeRel(Path.Combine("Data", relUnderData));
                    AddFile(manifest, achlist, relPc, FileKind.Icon, $"icon-{type.ToLowerInvariant()}", gameRoot, xboxDataRoot);
                    TryAddInterfaceTifForTexture(manifest, relPc, tifRoot);
                }
            }
        }

        private static void CollectVoiceAssets(
            DependencyManifest manifest,
            HashSet<string> achlist,
            string pluginName,
            string gameRoot,
            string xboxDataRoot)
        {
            string dataRoot = Path.Combine(gameRoot, "Data");

            string devVoiceRoot = Path.Combine(dataRoot, "Sound", "Voice", pluginName + ".esp");
            if (Directory.Exists(devVoiceRoot))
            {
                foreach (var f in Directory.EnumerateFiles(devVoiceRoot, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".wav" or ".lip" or ".txt" or ".dat")
                    {
                        string relUnderData = GetRelativePath(dataRoot, f);
                        string relPc = NormalizeRel(Path.Combine("Data", relUnderData));
                        AddBackupOnlyFile(manifest, relPc, "pc-voice-dev");
                    }
                }
            }

            string pcVoiceRoot = Path.Combine(dataRoot, "Sound", "Voice", pluginName + ".esm");
            if (Directory.Exists(pcVoiceRoot))
            {
                foreach (var f in Directory.EnumerateFiles(pcVoiceRoot, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".wem" or ".ffxanim")
                    {
                        string relUnderData = GetRelativePath(dataRoot, f);
                        string relPc = NormalizeRel(Path.Combine("Data", relUnderData));
                        AddFile(manifest, achlist, relPc, FileKind.Voice, "pc-voice-runtime", gameRoot, xboxDataRoot);
                    }
                }
            }

            if (Directory.Exists(xboxDataRoot))
            {
                // XBOX dev voice (esp)
                string xbVoiceRoot = Path.Combine(xboxDataRoot, "Sound", "Voice", pluginName + ".esp");
                if (Directory.Exists(xbVoiceRoot))
                {
                    foreach (var f in Directory.EnumerateFiles(xbVoiceRoot, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext is ".wav" or ".lip" or ".txt" or ".dat")
                        {
                            string relXb = "XBOX\\Data\\" + GetRelativePath(xboxDataRoot, f);
                            relXb = NormalizeRel(relXb);
                            if (!manifest.Files.Any(fe => fe.XboxPath == relXb))
                            {
                                manifest.Files.Add(new FileEntry
                                {
                                    PcPath = "",
                                    XboxPath = relXb,
                                    Kind = FileKind.Voice.ToString().ToLowerInvariant(),
                                    Source = "xbox-voice-dev"
                                });
                            }
                        }
                    }
                }

                // XBOX runtime voice (esm) – only WEM is expected to differ
                xbVoiceRoot = Path.Combine(xboxDataRoot, "Sound", "Voice", pluginName + ".esm");
                if (Directory.Exists(xbVoiceRoot))
                {
                    foreach (var f in Directory.EnumerateFiles(xbVoiceRoot, "*.wem", SearchOption.AllDirectories))
                    {
                        string relXb = "XBOX\\Data\\" + GetRelativePath(xboxDataRoot, f);
                        relXb = NormalizeRel(relXb);
                        if (!manifest.Files.Any(fe => fe.XboxPath == relXb))
                        {
                            manifest.Files.Add(new FileEntry
                            {
                                PcPath = "",
                                XboxPath = relXb,
                                Kind = FileKind.Voice.ToString().ToLowerInvariant(),
                                Source = "xbox-voice-runtime"
                            });
                        }
                    }
                }
            }
        }

        // Achlist writer (JSON array, ASCII, CRLF, no BOM)
        private static void WriteAchlistJsonAsciiCrLf(string path, IEnumerable<string> items)
        {
            var arr = items.ToArray();
            var json = JsonSerializer.Serialize(arr, new JsonSerializerOptions { WriteIndented = true });

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(fs, Encoding.ASCII);
            sw.NewLine = "\r\n";
            foreach (var line in json.Split('\n'))
            {
                sw.Write(line.TrimEnd('\r'));
                sw.Write(sw.NewLine);
            }
            sw.Flush();
        }

        // Lightweight CSV writer for dependencies
        private static void WriteDepsCsv(string path, IEnumerable<FileEntry> files)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(fs, Encoding.UTF8);
            sw.WriteLine("kind,source,pcpath,xboxpath");
            foreach (var f in files.OrderBy(x => x.Kind).ThenBy(x => x.PcPath, StringComparer.OrdinalIgnoreCase))
            {
                string pc = (f.PcPath ?? "").Replace(',', ';');
                string xb = (f.XboxPath ?? "").Replace(',', ';');
                sw.WriteLine($"{f.Kind},{f.Source},{pc},{xb}");
            }
            sw.Flush();
        }

        // Full-path-aware TIF mapping for interface + terrain textures (mirror includes "<plugin>.esm")
        private static void TryAddInterfaceTifForTexture(
            DependencyManifest manifest,
            string relTexturePath,
            string tifRoot)
        {
            if (!relTexturePath.StartsWith("Data\\Textures", StringComparison.OrdinalIgnoreCase))
                return;

            string relUnderTextures = relTexturePath.Substring("Data\\Textures\\".Length);

            // Interface: mirror includes "<plugin>.esm" directory
            if (relUnderTextures.StartsWith("Interface\\", StringComparison.OrdinalIgnoreCase))
            {
                // Example:
                // DDS: Data\Textures\Interface\InventoryIcons\<Plugin>.esm\...\foo.dds
                // TIF: Source\TGATextures\Interface\InventoryIcons\<Plugin>.esm\...\foo.tif
                string tifSubPath = Path.ChangeExtension(relUnderTextures, ".tif");
                string tifFull = Path.Combine(tifRoot, tifSubPath);
                if (File.Exists(tifFull))
                {
                    string relTifPc = NormalizeRel(Path.Combine("Source", "TGATextures", tifSubPath));
                    if (!manifest.Files.Any(f => string.Equals(f.PcPath, relTifPc, StringComparison.OrdinalIgnoreCase)))
                    {
                        manifest.Files.Add(new FileEntry
                        {
                            PcPath = relTifPc,
                            XboxPath = null,
                            Kind = FileKind.Tif.ToString().ToLowerInvariant(),
                            Source = "interface-tif-from-dds"
                        });
                    }
                }
            }
            else if (relUnderTextures.StartsWith("Terrain\\OverlayMasks\\", StringComparison.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileNameWithoutExtension(relUnderTextures) + ".tif";
                string tifFull = Path.Combine(tifRoot, "Terrain", "OverlayMasks", fileName);
                if (File.Exists(tifFull))
                {
                    string relTifPc = NormalizeRel(Path.Combine("Source", "TGATextures", "Terrain", "OverlayMasks", fileName));
                    if (!manifest.Files.Any(f => string.Equals(f.PcPath, relTifPc, StringComparison.OrdinalIgnoreCase)))
                    {
                        manifest.Files.Add(new FileEntry
                        {
                            PcPath = relTifPc,
                            XboxPath = null,
                            Kind = FileKind.Tif.ToString().ToLowerInvariant(),
                            Source = "terrain-tif-from-dds"
                        });
                    }
                }
            }
        }

        // =====================================================================
        // Script discovery
        // =====================================================================

        // Convert Bethesda Script Name to PSC relative path
        private static string ScriptNameToPscRel(string name)
        {
            string path = name.Replace(':', '\\');
            return NormalizeRel(Path.Combine("Data", "Scripts", "Source", path + ".psc"));
        }

        // Convert Bethesda Script Name to PEX relative path
        private static string ScriptNameToPexRel(string name)
        {
            string path = name.Replace(':', '\\');
            return NormalizeRel(Path.Combine("Data", "Scripts", path + ".pex"));
        }



        // Find colon-delimited tokens in plugin that correspond to valid scripts
        private static HashSet<string> ExtractScriptNamesFromPlugin(byte[] pluginBytes, string gameRoot)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Raw ASCII view of the whole plugin.
            // In practice Starfield script names show up as plain ASCII tokens.
            string text = Encoding.ASCII.GetString(pluginBytes);

            // Match tokens like Foo:Bar or Foo:Bar:Baz etc.
            // First segment: [A-Za-z0-9_]+
            // Then one or more ":segment" parts.
            var rx = new Regex(@"\b([A-Za-z0-9_]+(?::[A-Za-z0-9_]+)+)\b",
                               RegexOptions.Multiline);

            foreach (Match m in rx.Matches(text))
            {
                string candidate = m.Groups[1].Value.Trim();
                if (candidate.Length == 0)
                    continue;

                string pscRel = ScriptNameToPscRel(candidate);
                string pexRel = ScriptNameToPexRel(candidate);

                string fullPsc = Path.Combine(gameRoot, pscRel);
                string fullPex = Path.Combine(gameRoot, pexRel);

                // Only keep candidates that actually exist as PSC or PEX on disk.
                if (File.Exists(fullPsc) || File.Exists(fullPex))
                {
                    names.Add(candidate);
                }
            }

            return names;
        }

        // Expand PSC imports to include all dependent scripts
        private static HashSet<string> ExpandScriptImports(HashSet<string> rootNames, string gameRoot)
        {
            var all = new HashSet<string>(rootNames, StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(rootNames);

            var importRegex = new Regex(@"^\s*import\s+([A-Za-z0-9_:.]+)",
                                        RegexOptions.IgnoreCase | RegexOptions.Multiline);

            while (queue.Count > 0)
            {
                string name = queue.Dequeue();
                string pscRel = ScriptNameToPscRel(name);
                string fullPsc = Path.Combine(gameRoot, pscRel);

                if (!File.Exists(fullPsc))
                    continue;

                string text = File.ReadAllText(fullPsc);
                foreach (Match m in importRegex.Matches(text))
                {
                    string importName = m.Groups[1].Value.Trim();
                    if (importName.Length == 0)
                        continue;

                    string pscRelImp = ScriptNameToPscRel(importName);
                    string pexRelImp = ScriptNameToPexRel(importName);

                    bool exists = File.Exists(Path.Combine(gameRoot, pscRelImp)) ||
                                  File.Exists(Path.Combine(gameRoot, pexRelImp));

                    if (exists && all.Add(importName))
                    {
                        queue.Enqueue(importName);
                    }
                }
            }

            return all;
        }


        private static void CollectMatDdsTokensFromJson(JsonElement element, List<string> tokens)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        if ((prop.NameEquals("File") || prop.NameEquals("FileName")) &&
                            prop.Value.ValueKind == JsonValueKind.String)
                        {
                            string? val = prop.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(val) &&
                                val.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                            {
                                tokens.Add(val);
                            }
                        }

                        CollectMatDdsTokensFromJson(prop.Value, tokens);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        CollectMatDdsTokensFromJson(item, tokens);
                    break;
            }
        }

        private static string? NormalizeDdsPathFromMat(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Replace('/', '\\').Replace("\\\\", "\\").Trim();

            int idxDataTex = s.IndexOf("Data\\Textures\\", StringComparison.OrdinalIgnoreCase);
            if (idxDataTex >= 0) return NormalizeRel(s.Substring(idxDataTex));

            int idxTextures = s.IndexOf("Textures\\", StringComparison.OrdinalIgnoreCase);
            if (idxTextures >= 0) return NormalizeRel(Path.Combine("Data", s.Substring(idxTextures)));

            if (!s.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)) return null;
            return NormalizeRel(Path.Combine("Data", "Textures", s.TrimStart('\\')));
        }
    }
}
