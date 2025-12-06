// Target: .NET 8 console app
// Project file should have: <TargetFramework>net8.0</TargetFramework>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DmmDep
{
    internal enum FileKind
    {
        Pex,
        Psc,
        Nif,
        Mat,
        Texture,
        Mesh,
        Voice,
        Terrain,
        Icon,
        Tif,
        BackupOnly,
        Other
    }

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
        public string? TifRootOverride { get; set; }
        public string? ScriptsRootOverride { get; set; }
    }

    internal static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            try
            {
                var options = ParseArgs(args);
                if (options == null)
                {
                    PrintUsage();
                    return 1;
                }

                // ---- Root detection ----

                string pluginPath = Path.GetFullPath(options.PluginPath);
                if (!File.Exists(pluginPath))
                    throw new FileNotFoundException("Plugin file not found", pluginPath);

                string dataRoot = Path.GetDirectoryName(pluginPath)
                                  ?? throw new InvalidOperationException("Unable to get Data root from plugin path");
                string gameRoot = options.GameRootOverride ??
                                  Directory.GetParent(dataRoot)?.FullName ??
                                  throw new InvalidOperationException("Unable to infer game root (parent of Data)");

                Console.WriteLine($"Plugin   : {pluginPath}");
                Console.WriteLine($"DataRoot : {dataRoot}");
                Console.WriteLine($"GameRoot : {gameRoot}");

                // CreationKit.ini / CreationKitCustom.ini (for XB path detection)
                string iniPath = FindCreationKitIni(gameRoot);
                if (iniPath == null && options.XboxDataOverride == null)
                    throw new InvalidOperationException("CreationKit.ini / CreationKitCustom.ini not found. Use --xboxdata to specify XBOX Data root.");

                string xboxDataRoot = options.XboxDataOverride ?? InferXboxDataRoot(gameRoot, iniPath);
                Console.WriteLine($"XboxData : {xboxDataRoot}");

                // TIF root: ..\..\Source\TGATextures from game root, unless overridden
                string tifRoot = options.TifRootOverride ??
                                 Path.GetFullPath(Path.Combine(gameRoot, "..", "..", "Source", "TGATextures"));
                Console.WriteLine($"TifRoot  : {tifRoot}");

                // Scripts root
                string scriptsRoot = options.ScriptsRootOverride ?? Path.Combine(dataRoot, "Scripts");
                string scriptsSourceRoot = Path.Combine(scriptsRoot, "Source");
                Console.WriteLine($"Scripts  : {scriptsRoot}");

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

                // ---- 1. Scan plugin for NIF / terrain / script-name strings ----

                Console.WriteLine("[1] Scanning plugin for NIF / terrain strings...");
                var pluginBytes = File.ReadAllBytes(pluginPath);
                var pluginStrings = ExtractPrintableStrings(pluginBytes, 6).ToList();

                var nifRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var terrainNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var btdNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var s in pluginStrings)
                {
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
                            terrainNames.Add(Path.GetFileNameWithoutExtension(rel));
                        }
                    }
                    else if (s.EndsWith(".btd", StringComparison.OrdinalIgnoreCase))
                    {
                        string name = Path.GetFileNameWithoutExtension(s);
                        if (!string.IsNullOrEmpty(name))
                            btdNames.Add(name);
                    }
                }

                // Terrain backup-only folder Data\terrain\<modname>\**
                string modTerrainFolder = Path.Combine(gameRoot, "Data", "terrain", pluginName);
                if (Directory.Exists(modTerrainFolder))
                {
                    Console.WriteLine("[1b] Adding terrain backup folder...");
                    foreach (var f in Directory.EnumerateFiles(modTerrainFolder, "*.*", SearchOption.AllDirectories))
                    {
                        string rel = "Data\\" + GetRelativePath(Path.Combine(gameRoot, "Data"), f);
                        rel = NormalizeRel(rel);
                        AddBackupOnlyFile(manifest, rel, "terrain-folder");
                    }
                }

                // OverlayMasks from .btd -> Textures\Terrain\OverlayMasks\<name>.dds
                Console.WriteLine("[1c] Checking terrain overlay dds from .btd...");
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

                // ---- 2. NIF -> MAT + MeshPath ----

                Console.WriteLine("[2] Scanning NIFs for MAT and MeshPath...");
                var matRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var nifRel in nifRelPaths)
                {
                    string full = Path.Combine(gameRoot, nifRel);
                    if (!File.Exists(full)) continue;

                    var nifBytes = File.ReadAllBytes(full);
                    foreach (var s in ExtractPrintableStrings(nifBytes, 6))
                    {
                        if (s.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                        {
                            string rel = s.Replace('/', '\\').TrimStart('\\');
                            if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                                rel = Path.Combine("Data\\Materials", rel);
                            rel = NormalizeRel(rel);
                            string fullMat = Path.Combine(gameRoot, rel);
                            if (File.Exists(fullMat) && matRelPaths.Add(rel))
                            {
                                AddFile(manifest, achlistPaths, rel, FileKind.Mat, $"nif:{nifRel}", gameRoot, xboxDataRoot);
                            }
                        }
                        else if (!s.Contains('.') && s.Contains("\\"))
                        {
                            // MeshPath stem "zeeogre\eazybutton\eazybutton_1_lod1"
                            string stem = s.Replace('/', '\\').TrimStart('\\');
                            string meshRel = NormalizeRel(Path.Combine("Data\\geometries", stem + ".mesh"));
                            string fullMesh = Path.Combine(gameRoot, meshRel);
                            if (File.Exists(fullMesh))
                            {
                                AddFile(manifest, achlistPaths, meshRel, FileKind.Mesh, $"nif:{nifRel}", gameRoot, xboxDataRoot);
                            }
                        }
                    }
                }

                // ---- 3. MAT -> DDS (FileName entries) ----

                Console.WriteLine("[3] Scanning MATs for DDS FileName entries...");
                var fileNameRegex = new Regex("\"FileName\"\\s*:\\s*\"([^\"]+\\.dds)\"", RegexOptions.IgnoreCase);

                foreach (var matRel in matRelPaths)
                {
                    string fullMat = Path.Combine(gameRoot, matRel);
                    if (!File.Exists(fullMat)) continue;

                    string text = File.ReadAllText(fullMat);
                    foreach (Match m in fileNameRegex.Matches(text))
                    {
                        string raw = m.Groups[1].Value;
                        string rel = raw.Replace('/', '\\').TrimStart('\\');
                        if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                            rel = Path.Combine("Data", rel);
                        rel = NormalizeRel(rel);
                        string fullTex = Path.Combine(gameRoot, rel);
                        if (File.Exists(fullTex))
                        {
                            AddFile(manifest, achlistPaths, rel, FileKind.Texture, $"mat:{matRel}", gameRoot, xboxDataRoot);
                            // interface/terrain tif source
                            TryAddInterfaceTifForTexture(manifest, rel, tifRoot);
                        }
                    }
                }

                // ---- 4. Interface icons + shipbuilder + workshop ----

                Console.WriteLine("[4] Collecting interface icons / previews...");
                CollectIconsAndPreviews(manifest, achlistPaths, pluginName, gameRoot, xboxDataRoot, tifRoot);

                // ---- 5. Voice assets (PC dev + runtime + XB) ----

                Console.WriteLine("[5] Collecting voice assets...");
                CollectVoiceAssets(manifest, achlistPaths, pluginName, gameRoot, xboxDataRoot);

                // ---- 6. Scripts (only those referenced by plugin + their imports) ----

                Console.WriteLine("[6] Collecting scripts referenced by plugin...");
                CollectScripts(manifest, achlistPaths, scriptsRoot, scriptsSourceRoot, gameRoot, xboxDataRoot, pluginStrings);

                // ---- Outputs ----

                string achlistPath = Path.Combine(outputRoot, pluginName + ".achlist");
                File.WriteAllLines(achlistPath, achlistPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
                Console.WriteLine($"Wrote achlist : {achlistPath}");

                string jsonPath = Path.Combine(outputRoot, pluginName + "_deps.json");
                var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(manifest, jsonOpts));
                Console.WriteLine($"Wrote deps    : {jsonPath}");

                Console.WriteLine("Done.");
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
        }

        private static Options? ParseArgs(string[] args)
        {
            var opts = new Options();
            int i = 0;

            // first non-switch argument is plugin
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
                    default:
                        Console.Error.WriteLine($"Unknown option: {arg}");
                        return null;
                }
            }

            if (string.IsNullOrWhiteSpace(opts.PluginPath))
                return null;

            return opts;
        }

        // -------- Root helpers --------

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

        // -------- Common small helpers --------

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
            if (!manifest.Files.Any(f => f.PcPath.Equals(relPcPath, StringComparison.OrdinalIgnoreCase)))
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

        // -------- TIF mapping for interface / terrain textures --------

        private static void TryAddInterfaceTifForTexture(DependencyManifest manifest, string ddsRel, string tifRoot)
        {
            if (!ddsRel.StartsWith("Data\\Textures", StringComparison.OrdinalIgnoreCase))
                return;

            string relUnderTextures = ddsRel.Substring("Data\\Textures\\".Length);
            string subPath = Path.ChangeExtension(relUnderTextures, ".tif");

            if (relUnderTextures.StartsWith("Interface\\", StringComparison.OrdinalIgnoreCase))
            {
                string tifFull = Path.Combine(tifRoot, subPath);
                if (File.Exists(tifFull))
                {
                    string relTifPc = "Source\\TGATextures\\" + subPath;
                    manifest.Files.Add(new FileEntry
                    {
                        PcPath = NormalizeRel(relTifPc),
                        XboxPath = null,
                        Kind = FileKind.Tif.ToString().ToLowerInvariant(),
                        Source = "interface-tif-from-dds:" + ddsRel
                    });
                }
            }
            else if (relUnderTextures.StartsWith("Terrain\\OverlayMasks\\", StringComparison.OrdinalIgnoreCase))
            {
                string tifFull = Path.Combine(tifRoot, "Terrain", "OverlayMasks", Path.GetFileName(subPath));
                if (File.Exists(tifFull))
                {
                    string relTifPc = NormalizeRel(Path.Combine("Source\\TGATextures\\Terrain\\OverlayMasks", Path.GetFileName(subPath)));
                    manifest.Files.Add(new FileEntry
                    {
                        PcPath = relTifPc,
                        XboxPath = null,
                        Kind = FileKind.Tif.ToString().ToLowerInvariant(),
                        Source = "terrain-tif-from-dds:" + ddsRel
                    });
                }
            }
        }

        // -------- Icons & previews --------

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

        // -------- Voice assets --------

        private static void CollectVoiceAssets(
            DependencyManifest manifest,
            HashSet<string> achlist,
            string pluginName,
            string gameRoot,
            string xboxDataRoot)
        {
            string dataRoot = Path.Combine(gameRoot, "Data");

            // PC dev WAVs in Data\Sound\Voice\<modname>.esp\**
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

            // Runtime PC WEM/FFXANIM in Data\Sound\Voice\<modname>.esm\**
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

            // XBOX WEMs in XBOX\Data\Sound\Voice\<modname>.esm\** (no FFXANIM expected)
            if (Directory.Exists(xboxDataRoot))
            {
                string xbVoiceRoot = Path.Combine(xboxDataRoot, "Sound", "Voice", pluginName + ".esm");
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
                                Source = "xbox-voice"
                            });
                        }
                    }
                }
            }
        }

        // -------- Scripts (PEX/PSC + imports), but only if used by plugin --------

        private static void CollectScripts(
            DependencyManifest manifest,
            HashSet<string> achlist,
            string scriptsRoot,
            string scriptsSourceRoot,
            string gameRoot,
            string xboxDataRoot,
            IReadOnlyCollection<string> pluginStrings)
        {
            if (!Directory.Exists(scriptsRoot))
                return;

            string dataRoot = Path.Combine(gameRoot, "Data");

            var pexFiles = Directory.EnumerateFiles(scriptsRoot, "*.pex", SearchOption.AllDirectories).ToList();
            var pscFiles = Directory.Exists(scriptsSourceRoot)
                ? Directory.EnumerateFiles(scriptsSourceRoot, "*.psc", SearchOption.AllDirectories).ToList()
                : new List<string>();

            var nameToPex = pexFiles.ToDictionary(
                p => Path.GetFileNameWithoutExtension(p),
                p => p,
                StringComparer.OrdinalIgnoreCase);

            var nameToPsc = pscFiles.ToDictionary(
                p => Path.GetFileNameWithoutExtension(p),
                p => p,
                StringComparer.OrdinalIgnoreCase);

            var includedPex = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var includedPsc = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // helper: does script base name appear in any plugin string?
            bool ScriptNameAppearsInPlugin(string baseName)
            {
                foreach (var s in pluginStrings)
                {
                    if (s.IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false;
            }

            // First pass: only scripts whose base name shows up in plugin strings
            foreach (var pex in pexFiles)
            {
                string baseName = Path.GetFileNameWithoutExtension(pex);
                if (!ScriptNameAppearsInPlugin(baseName))
                    continue;

                string relUnderData = GetRelativePath(dataRoot, pex);
                string relPc = NormalizeRel(Path.Combine("Data", relUnderData));
                includedPex.Add(relPc);
                AddFile(manifest, achlist, relPc, FileKind.Pex, "pex-from-plugin", gameRoot, xboxDataRoot);
            }

            foreach (var psc in pscFiles)
            {
                string baseName = Path.GetFileNameWithoutExtension(psc);

                // always include PSC if:
                //  - its PEX counterpart was included, OR
                //  - it appears in plugin strings
                if (!ScriptNameAppearsInPlugin(baseName) &&
                    !nameToPex.ContainsKey(baseName))
                    continue;

                string relUnderData = GetRelativePath(dataRoot, psc);
                string relPc = NormalizeRel(Path.Combine("Data", relUnderData));
                includedPsc.Add(relPc);
                AddBackupOnlyFile(manifest, relPc, "psc-from-plugin");
            }

            // PSC import scan to pull related dependencies
            var importRegex = new Regex(@"^\s*import\s+([A-Za-z0-9_:.]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (var pscRel in includedPsc.ToList())
            {
                string fullPsc = Path.Combine(gameRoot, pscRel);
                if (!File.Exists(fullPsc)) continue;

                string text = File.ReadAllText(fullPsc);
                foreach (Match m in importRegex.Matches(text))
                {
                    string importName = m.Groups[1].Value.Trim();
                    string baseName = importName.Split(new[] { '.', ':' }).Last();

                    if (nameToPex.TryGetValue(baseName, out var depPex))
                    {
                        string relUnderData = GetRelativePath(dataRoot, depPex);
                        string relPc = NormalizeRel(Path.Combine("Data", relUnderData));
                        if (includedPex.Add(relPc))
                        {
                            AddFile(manifest, achlist, relPc, FileKind.Pex, "pex-from-psc-import", gameRoot, xboxDataRoot);
                        }
                    }

                    if (nameToPsc.TryGetValue(baseName, out var depPsc))
                    {
                        string relUnderData = GetRelativePath(dataRoot, depPsc);
                        string relPc = NormalizeRel(Path.Combine("Data", relUnderData));
                        if (includedPsc.Add(relPc))
                        {
                            AddBackupOnlyFile(manifest, relPc, "psc-from-psc-import");
                        }
                    }
                }
            }
        }
    }
}
