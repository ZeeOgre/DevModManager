using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DmmDep
{
#nullable enable

    internal enum FileKind { Pex, Psc, Nif, Mat, Texture, Mesh, Voice, Terrain, Icon, Tif, Biom, BackupOnly, Particle, Anim, Morph, Rig, Warn, Other }

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

    // Source generator context for trim-safe JSON serialization
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(string[]))]
    internal partial class AchlistSerializerContext : JsonSerializerContext
    {
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

        public bool SmartClobber { get; set; } // --smartclobber : seed candidates from existing .achlist
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

        private static string BuildXboxRelativePath(string xboxDataRoot, string xboxFullPath)
        {
            // Build consistent XBOX relative path from xboxDataRoot
            // xboxDataRoot typically points to: <GameRoot>\XBOX\Data
            // We want to return: XBOX\Data\<relative-from-xboxDataRoot>

            string relFromXboxData = GetRelativePath(xboxDataRoot, xboxFullPath);

            // Get the parent of xboxDataRoot to find the XBOX folder name
            string? xboxRootParent = Directory.GetParent(xboxDataRoot)?.FullName;
            if (xboxRootParent == null)
                return NormalizeRel(Path.Combine("XBOX", "Data", relFromXboxData));

            string xboxFolderName = Path.GetFileName(Directory.GetParent(xboxDataRoot)?.FullName ?? "XBOX");
            string dataFolderName = Path.GetFileName(xboxDataRoot);

            return NormalizeRel(Path.Combine(xboxFolderName, dataFolderName, relFromXboxData));
        }

        private static bool IsMatExtension(string token)
        {
            // Check if token contains ".mat" (case-insensitive) but not ".mat2" or other variants
            // Look for the 4-character sequence ".mat" followed by end-of-string or non-alphanumeric
            if (token.Length < 4)
                return false;

            int matIndex = token.IndexOf(".mat", StringComparison.OrdinalIgnoreCase);
            if (matIndex < 0)
                return false;

            // Check if it's exactly ".mat" at the end or followed by non-alphanumeric
            int afterMatIndex = matIndex + 4;
            if (afterMatIndex == token.Length)
                return true; // ends with .mat

            char afterChar = token[afterMatIndex];
            // If followed by digit or letter, it's something like .mat2 or .matx
            if (char.IsLetterOrDigit(afterChar))
                return false;

            return true; // .mat followed by non-alphanumeric (like space, slash, etc.)
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

                //all of these should ALWAYS succeed.
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
                // TIF root: resolve to absolute path from game root
                string tifRoot = options.TifRootOverride != null
                    ? Path.GetFullPath(Path.Combine(gameRoot, options.TifRootOverride))
                    : Path.GetFullPath(Path.Combine(gameRoot, "..", "..", "Source", "TGATextures"));
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

                if (options.SmartClobber)
                {
                    string oldAchlistPath = Path.Combine(outputRoot, pluginName + ".achlist");
                    if (File.Exists(oldAchlistPath))
                    {
                        int seeded = SeedAchlistCandidatesFromExisting(oldAchlistPath, achlistPaths);
                        Log.Info($"[0] --smartclobber seeded {seeded} items from existing achlist: {oldAchlistPath}");
                    }
                    else
                    {
                        Log.Info($"[0] --smartclobber: no existing achlist found at {oldAchlistPath}", isSkipped: true);
                    }
                }

                // ---- 1. Scan plugin for NIF / terrain / MAT / misc strings ----
                Log.Info("[1] Scanning plugin for local NIF / terrain / MAT / misc strings...");
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
                    else if (IsMatExtension(s))
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

                // OverlayMasks from .btd -> Textures\Terrain\OverlayMasks\<name>.dds + TIF
                Log.Info("[1c] Checking terrain overlay dds from .btd...");
                foreach (var n in btdNames)
                {
                    // Add DDS
                    string ddsRel = NormalizeRel(Path.Combine("Data", "Textures", "Terrain", "OverlayMasks", n + ".dds"));
                    string ddsFull = Path.Combine(gameRoot, ddsRel);
                    if (File.Exists(ddsFull))
                    {
                        AddFile(manifest, achlistPaths, ddsRel, FileKind.Texture, "btd-overlay", gameRoot, xboxDataRoot);
                    }

                    // Add corresponding TIF: Data\Source\TGATextures\Terrain\OverlayMasks\<name>.tif
                    string tifRel = Path.Combine("Data", "Source", "TGATextures", "Terrain", "OverlayMasks", n + ".tif");
                    string tifFull = Path.Combine(gameRoot, tifRel);
                    if (File.Exists(tifFull))
                    {
                        tifRel = NormalizeRel(tifRel);
                        AddBackupOnlyFile(manifest, tifRel, "terrain-overlay-tif");
                    }
                }

                // ---- 1b. PlanetData BiomeMaps (PNDT FULL -> <FULL>.biom) ----
                Log.Info("[1d] Collecting biome maps (.biom) from PNDT FULL names... (Targeted Scan element)");
                CollectBiomeMapsFromPndtFull(manifest, achlistPaths, nifRelPaths, Path.GetFileName(pluginPath), pluginBytes, gameRoot, xboxDataRoot);


                // ---- 2. NIF -> MAT + MeshPath + RIG ----
                Log.Info("[2] Scanning NIFs for MAT, mesh stems, and RIG... meshes come from nifs (which are in meshes) but are stored in geometries... confused yet?");

                foreach (var nifRel in nifRelPaths)
                {
                    string full = Path.Combine(gameRoot, nifRel);
                    if (!File.Exists(full)) continue;

                    var nifBytes = File.ReadAllBytes(full);


                    

                    foreach (var s in ExtractPrintableStrings(nifBytes, 4))
                    {
                        string token = s.Replace('/', '\\').TrimStart('\\');

                        if (IsMatExtension(token))
                        {
                            string rel = token;

                            // Truncate at .mat extension to remove garbage like .mat+ or .mat1
                            int matIndex = rel.IndexOf(".mat", StringComparison.OrdinalIgnoreCase);
                            if (matIndex >= 0)
                            {
                                rel = rel.Substring(0, matIndex + 4); // Keep up to and including ".mat"
                            }

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
                                matRelPaths.Add(rel);
                            }
                            else
                            {
                                Log.Warn($"[2] Presumed MAT path '{rel}' from NIF '{nifRel}' does not exist");
                            }
                        }
                        else if (token.EndsWith(".rig", StringComparison.OrdinalIgnoreCase))
                        {
                            string rel = token;

                            // Truncate at .rig extension to remove garbage
                            int rigIndex = rel.IndexOf(".rig", StringComparison.OrdinalIgnoreCase);
                            if (rigIndex >= 0)
                            {
                                rel = rel.Substring(0, rigIndex + 4); // Keep up to and including ".rig"
                            }

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

                            int nullChar = stem.IndexOf('\0');
                            if (nullChar >= 0)
                            {
                                stem = stem.Substring(0, nullChar);
                            }
                            stem = stem.TrimEnd();

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
                            }
                            continue;
                        }

                        if (!pcExists && xbExists)
                        {
                            string relXb = BuildXboxRelativePath(xboxDataRoot, xbCandidate!);
                            Log.Warn($"[WARN] PC texture missing; regenerate and try again -> {relXb}");
                            continue;
                        }
                        if (pcExists && !xbExists && xbCandidate != null)
                        {
                            string relXb = BuildXboxRelativePath(xboxDataRoot, xbCandidate);
                            Log.Warn($"[WARN] XBOX texture missing; regenerate and try again -> {relXb}");
                            if (found.Add(ddsRel))
                            {
                                hasCustomTextures = true;
                                totalDdsHits++;
                                AddFile(manifest, achlistPaths, ddsRel, FileKind.Texture, $"mat:{matRel}", gameRoot, xboxDataRoot);
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
                        Log.Info($"[3] Skipping MAT (no existing custom textures): {matRel}", isSkipped: true);
                    }
                }

                Log.Info($"[3] MATs with custom DDS: {matsWithCustom}, total DDS hits: {totalDdsHits}");

                // ---- 4. Interface icons + shipbuilder + workshop ----
                Log.Info("[4] Collecting interface icons / previews...");
                CollectIconsAndPreviews(manifest, achlistPaths, pluginName, gameRoot, xboxDataRoot, tifRoot);


                // ---- 5. Voice assets (PC dev + runtime + XB) ----
                Log.Info("[5] Collecting voice files...");
                CollectVoiceAssets(manifest, achlistPaths, pluginName, gameRoot, xboxDataRoot);

                // ---- 6. Scripts (Bethesda Script Name format with imports) ----
                static string ToPscRel(string name) =>
                    NormalizeRel(Path.Combine("Data", "Scripts", "Source", name.Replace(':', '\\') + ".psc"));
                static string ToPexRel(string name) =>
                    NormalizeRel(Path.Combine("Data", "Scripts", name.Replace(':', '\\') + ".pex"));

                Log.Info("[6] Script discovery from plugin text + PSC imports...");
                Log.Warn("[NOTE] Script files (PSC/PEX) may be overrepresented; only filesystem presence is checked, not parent archives (probable future enhancement).");

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
                Log.Info($"Skipped writing JSON deps file: {jsonPath} (disabled to reduce output size)");

                swTotal.Stop();
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
            Console.WriteLine("  --smartclobber        Seed candidates from existing .achlist (captures manual overrides like Data\\Interface\\mapicons.swf).");
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
                    xboxRel = BuildXboxRelativePath(xboxDataRoot, candidate);
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

            int ddsCount = 0;
            int tifFoundCount = 0;

            foreach (var type in iconTypes)
            {
                string root = Path.Combine(dataRoot, "Textures", "Interface", type, pluginName + ".esm");
                if (!Directory.Exists(root)) continue;

                foreach (string dds in Directory.EnumerateFiles(root, "*.dds", SearchOption.AllDirectories))
                {
                    ddsCount++;
                    string relUnderData = GetRelativePath(dataRoot, dds);
                    string relPc = NormalizeRel(Path.Combine("Data", relUnderData));

                    // Add DDS icon
                    AddFile(manifest, achlist, relPc, FileKind.Icon, $"icon-{type.ToLowerInvariant()}", gameRoot, xboxDataRoot);

                    // Add corresponding TIF: Data\Textures\Interface\... → tifRoot\Interface\...
                    // Example DDS: Data\Textures\Interface\InventoryIcons\ZeeOgresEnhancedOutposts.esm\000000CCMC.dds
                    // Calculate: Interface\InventoryIcons\ZeeOgresEnhancedOutposts.esm\000000CCMC.tif
                    string relUnderTextures = relPc.Substring("Data\\Textures\\".Length);
                    string tifSubPath = Path.ChangeExtension(relUnderTextures, ".tif");
                    string tifFull = Path.Combine(tifRoot, tifSubPath);

                    // Debug log (first 3 only)
                    //if (tifFoundCount < 3 && File.Exists(tifFull))
                    //{
                    //    string relTifForLog = GetRelativePath(gameRoot, tifFull);
                    //    Log.Info($"[4-DEBUG] TIF mapping: {relPc} → {relTifForLog}");
                    //}

                    if (File.Exists(tifFull))
                    {
                        tifFoundCount++;
                        // Use FULL absolute path - let AddBackupOnlyFile normalize it
                        AddBackupOnlyFile(manifest, tifFull, "interface-icon-tif");
                    }
                }
            }

            Log.Info($"[4] Found {ddsCount} interface icons, {tifFoundCount} corresponding TIF files");
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
                        // Filter out wwise.dat files
                        if (Path.GetFileName(f).Equals("wwise.dat", StringComparison.OrdinalIgnoreCase))
                            continue;

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
                            // Filter out wwise.dat files
                            if (Path.GetFileName(f).Equals("wwise.dat", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string relXb = BuildXboxRelativePath(xboxDataRoot, f);
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
                        string relXb = BuildXboxRelativePath(xboxDataRoot, f);
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
            // Use source-generated serialization for trim safety
            var json = JsonSerializer.Serialize(arr, AchlistSerializerContext.Default.StringArray);

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
        private sealed class PndtExtract
        {
            public readonly HashSet<string> Anam = new(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> Modl = new(StringComparer.OrdinalIgnoreCase);
        }

        private static void CollectBiomeMapsFromPndtFull(
                        DependencyManifest manifest,
                        HashSet<string> achlist,
                        HashSet<string> nifRelPaths,
                        string pluginFileNameWithExt,
                        byte[] pluginBytes,
                        string gameRoot,
                        string xboxDataRoot)

        {
            // NOTE: Despite the method name, BIOM inference is now keyed off PNDT:ANAM (not FULL).
            // We want: Data\PlanetData\BiomeMaps\<pluginBaseName>.esm\<PNDT ANAM>.biom

            var pndt = ExtractPndtStrings(pluginBytes);

                if (pndt.Anam.Count == 0 && pndt.Modl.Count == 0)
                {
                    Log.Warn("[4b] No PNDT ANAM/MODL strings found while parsing plugin bytes. (No BIOM inference or PNDT model dependencies possible.)");
                    return;
                }

                if (pndt.Anam.Count > 0)
                    Log.Info($"[4b] PNDT ANAM strings found: {pndt.Anam.Count}");
                if (pndt.Modl.Count > 0)
                    Log.Info($"[4b] PNDT MODL strings found: {pndt.Modl.Count}");

                string pluginBaseName = Path.GetFileNameWithoutExtension(pluginFileNameWithExt);
                string pluginFolderName = pluginBaseName + ".esm";

                // ---- BIOM inference from ANAM ----
                int biomFound = 0;
                int biomMissing = 0;

                foreach (string anam in pndt.Anam.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    string safeName = anam.Trim();
                    if (safeName.Length == 0)
                        continue;

                    string biomRel = NormalizeRel(Path.Combine(
                        "Data",
                        "PlanetData",
                        "BiomeMaps",
                        pluginFolderName,
                        safeName + ".biom"));

                    string biomFull = Path.Combine(gameRoot, biomRel);

                    if (File.Exists(biomFull))
                    {
                        biomFound++;
                        AddFile(manifest, achlist, biomRel, FileKind.Biom, "pndt-anam-inferred", gameRoot, xboxDataRoot);
                    }
                    else
                    {
                        biomMissing++;
                        Log.Warn($"[WARN] Missing inferred BIOM: {biomRel}");
                    }
                }

                if (pndt.Anam.Count > 0)
                    Log.Info($"[4b] BIOM inference results: found {biomFound}, missing {biomMissing}");

                // ---- Model dependencies from MODL (planet_Nirn.nif etc.) ----
                int nifFound = 0;
                int nifMissing = 0;

            foreach (string modl in pndt.Modl.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                string s = modl.Trim();
                if (s.Length == 0)
                    continue;

                if (!s.EndsWith(".nif", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (TryResolveModelPath(s, gameRoot, out string rel))
                {
                    nifFound++;

                    // add to seed set so phase [2] will walk it
                    nifRelPaths.Add(rel);

                    AddFile(manifest, achlist, rel, FileKind.Nif, "pndt-modl", gameRoot, xboxDataRoot);
                    Log.Info($"[1d] PNDT MODL model resolved: {rel}");
                }
                else
                {
                    nifMissing++;

                    // Log both expected candidates for clarity
                    string raw = NormalizeRel(s.Replace('/', '\\')).TrimStart('\\');

                    string c1 = raw.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                        ? raw
                        : NormalizeRel(Path.Combine("Data", raw));

                    string rawNoData = raw.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                        ? raw.Substring("Data\\".Length)
                        : raw;

                    string c2 = (!rawNoData.StartsWith("Meshes\\", StringComparison.OrdinalIgnoreCase))
                        ? NormalizeRel(Path.Combine("Data", "Meshes", rawNoData))
                        : "(n/a)";

                    Log.Warn($"[WARN] Missing PNDT MODL NIF. Tried: {c1}" + (c2 != "(n/a)" ? $" OR {c2}" : ""));
                }
            }




            if (pndt.Modl.Count > 0)
                    Log.Info($"[4b] PNDT MODL NIF results: found {nifFound}, missing {nifMissing}");
            }

            /// <summary>
            /// Extracts PNDT strings (FULL + MODL) from the binary plugin.
            /// Handles GRUP containers and compressed record payloads.
            /// </summary>
        private static PndtExtract ExtractPndtStrings(byte[] pluginBytes)
            {
                var results = new PndtExtract();

                const int HeaderSize = 24;
                int len = pluginBytes.Length;
                int offset = 0;

                // Stack of GRUP end offsets. We "enter" a GRUP by pushing its end,
                // then continue parsing records inside it linearly until we hit the end.
                var grupEnds = new Stack<int>();

                while (offset + HeaderSize <= len)
                {
                    // If we reached the end of the current GRUP, pop and continue.
                    while (grupEnds.Count > 0 && offset >= grupEnds.Peek())
                        grupEnds.Pop();

                    // Record type (4cc)
                    string type = ReadFourCC(pluginBytes, offset);
                    uint sizeOrGroupSize = ReadU32(pluginBytes, offset + 4);

                    if (type == "GRUP")
                    {
                        // In Bethesda plugins, GRUP "size" is total group size INCLUDING header.
                        int groupTotalSize = checked((int)sizeOrGroupSize);
                        int groupEnd = offset + groupTotalSize;

                        if (groupTotalSize < HeaderSize || groupEnd > len)
                        {
                            // Corrupt or unexpected; bail safely to avoid infinite loops
                            break;
                        }

                        // Enter GRUP: push end, then move to first child record (right after header)
                        grupEnds.Push(groupEnd);
                        offset += HeaderSize;
                        continue;
                    }

                    // Normal record: header includes dataSize at +4
                    int dataSize = checked((int)sizeOrGroupSize);
                    int dataStart = offset + HeaderSize;
                    int next = dataStart + dataSize;

                    if (dataSize < 0 || next > len)
                    {
                        // Corrupt; stop
                        break;
                    }

                    if (type == "PNDT")
                    {
                        uint flags = ReadU32(pluginBytes, offset + 8);

                        byte[] payload = Slice(pluginBytes, dataStart, dataSize);
                        byte[] recordData = payload;

                        // Compressed flag: 0x00040000 (same convention as TES5/FO4/Starfield)
                        const uint CompressedFlag = 0x00040000;
                        if ((flags & CompressedFlag) != 0 && payload.Length >= 5)
                        {
                            recordData = TryDecompressRecordPayload(payload) ?? payload;
                        }

                        // BIOM name now comes from ANAM, not FULL
                        foreach (var anam in ExtractStringSubrecords(recordData, "ANAM"))
                        {
                            if (!string.IsNullOrWhiteSpace(anam))
                                results.Anam.Add(anam.Trim());
                        }

                        foreach (var modl in ExtractStringSubrecords(recordData, "MODL"))
                        {
                            if (!string.IsNullOrWhiteSpace(modl))
                                results.Modl.Add(modl.Trim());
                        }
                    }

                    offset = next;
                }

                return results;
            }
        private static int SeedAchlistCandidatesFromExisting(string achlistPath, HashSet<string> achlist)
        {
            try
            {
                // Your achlist is written as a JSON string array.
                string json = File.ReadAllText(achlistPath, Encoding.ASCII);

                string[]? items = JsonSerializer.Deserialize<string[]>(json);
                if (items == null || items.Length == 0)
                    return 0;

                int added = 0;
                foreach (string raw in items)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    string rel = NormalizeRel(raw.Trim());

                    // Keep scope tight: only accept Data\... relative paths
                    if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (achlist.Add(rel))
                        added++;
                }

                return added;
            }
            catch (Exception ex)
            {
                Log.Warn($"[WARN] --smartclobber failed to read/parse existing achlist '{achlistPath}': {ex.GetType().Name}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Extract string payloads from a specific subrecord type within a record payload.
        /// Handles XXXX extended-size subrecords.
        /// </summary>
        private static IEnumerable<string> ExtractStringSubrecords(byte[] recordData, string wantedFourCC)
            {
                const int SubHeader = 6;
                int pos = 0;
                int len = recordData.Length;

                uint? extendedSize = null;

                while (pos + SubHeader <= len)
                {
                    string sub = ReadFourCC(recordData, pos);
                    ushort sz16 = ReadU16(recordData, pos + 4);
                    pos += SubHeader;

                    uint size = extendedSize ?? sz16;
                    extendedSize = null;

                    if (sub == "XXXX")
                    {
                        // XXXX: next subrecord uses 32-bit size stored in this data
                        if (pos + sz16 <= len && sz16 == 4)
                        {
                            extendedSize = ReadU32(recordData, pos);
                        }
                        pos += sz16;
                        continue;
                    }

                    if (pos + size > len)
                        yield break; // corrupt / truncated

                    if (sub == wantedFourCC)
                    {
                        var bytes = Slice(recordData, pos, checked((int)size));
                        string str = Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim();
                        if (str.Length > 0)
                            yield return str;
                    }

                    pos += checked((int)size);
                }
            }

        private static byte[]? TryDecompressRecordPayload(byte[] payload)
        {
            try
            {
                if (payload.Length < 5)
                    return null;

                int expected = checked((int)ReadU32(payload, 0));
                if (expected <= 0 || expected > 256 * 1024 * 1024) // sanity cap
                    return null;

                using var input = new MemoryStream(payload, 4, payload.Length - 4, writable: false);
                using var z = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream(capacity: expected);

                z.CopyTo(output);
                var data = output.ToArray();

                // Some plugins may not match expected exactly; accept what we got if non-empty
                return data.Length > 0 ? data : null;
            }
            catch
            {
                return null;
            }
        }
        private static bool TryResolveModelPath(
                       string modlRaw,
                       string gameRoot,
                       out string resolvedRel)
        {
            resolvedRel = "";

            string raw = NormalizeRel(modlRaw.Replace('/', '\\')).TrimStart('\\');

            // Candidate #1: Data\<raw>
            string c1Rel = raw.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                ? raw
                : NormalizeRel(Path.Combine("Data", raw));

            string c1Full = Path.Combine(gameRoot, c1Rel);
            if (File.Exists(c1Full))
            {
                resolvedRel = c1Rel;
                return true;
            }

            // Candidate #2: Data\Meshes\<raw> (if not already Meshes-rooted)
            string rawNoData = raw.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase)
                ? raw.Substring("Data\\".Length)
                : raw;

            if (!rawNoData.StartsWith("Meshes\\", StringComparison.OrdinalIgnoreCase))
            {
                string c2Rel = NormalizeRel(Path.Combine("Data", "Meshes", rawNoData));
                string c2Full = Path.Combine(gameRoot, c2Rel);

                if (File.Exists(c2Full))
                {
                    resolvedRel = c2Rel;
                    return true;
                }
            }

            return false;
        }




        private static string ReadFourCC(byte[] bytes, int offset)
        {
            // bytes are ASCII for record/subrecord names
            return Encoding.ASCII.GetString(bytes, offset, 4);
        }

        private static ushort ReadU16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadU32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
        }

        private static byte[] Slice(byte[] bytes, int offset, int count)
        {
            var buf = new byte[count];
            Buffer.BlockCopy(bytes, offset, buf, 0, count);
            return buf;
        }

    }
}
