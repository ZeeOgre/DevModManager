using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevModManager.Core.Services
{
#nullable enable

    public enum FileKind { Pex, Psc, Nif, Mat, Texture, Mesh, Voice, Terrain, Icon, Tif, BackupOnly, Particle, Anim, Morph, Rig, Warn, Other }

    public sealed class FileEntry
    {
        public string PcPath { get; set; } = "";
        public string? XboxPath { get; set; }
        public string Kind { get; set; } = "";
        public string Source { get; set; } = "";
    }

    public sealed class DependencyManifest
    {
        public string Plugin { get; set; } = "";
        public string GameRoot { get; set; } = "";
        public string DataRoot { get; set; } = "";
        public string XboxDataRoot { get; set; } = "";
        public string TifRoot { get; set; } = "";
        public List<FileEntry> Files { get; set; } = new();
    }

    public sealed class DependencyScanOptions
    {
        public string PluginPath { get; set; } = "";
        public string? GameRootOverride { get; set; }
        public string? XboxDataOverride { get; set; }
        public string? TifRootOverride { get; set; }
        public string? ScriptsRootOverride { get; set; }
    }

    public sealed class DependencyScanResult
    {
        public DependencyManifest Manifest { get; init; } = new();
        public IReadOnlyList<string> Achlist { get; init; } = Array.Empty<string>();
        public TimeSpan Elapsed { get; init; }
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    }

    public sealed class DependencyScanner
    {
        public DependencyScanResult Scan(DependencyScanOptions options)
        {
            var start = DateTime.UtcNow;
            var diagnostics = new List<string>();
            var achlistPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(options.PluginPath))
                throw new ArgumentException("PluginPath is required.", nameof(options.PluginPath));

            string pluginPath = Path.GetFullPath(options.PluginPath);
            if (!File.Exists(pluginPath))
                throw new FileNotFoundException("Plugin file not found", pluginPath);

            string dataRoot = Path.GetDirectoryName(pluginPath)
                              ?? throw new InvalidOperationException("Unable to get Data root from plugin path");
            string gameRoot = options.GameRootOverride ??
                              Directory.GetParent(dataRoot)?.FullName ??
                              throw new InvalidOperationException("Unable to infer game root (parent of Data)");

            string? iniPath = FindCreationKitIni(gameRoot);
            if (iniPath == null && options.XboxDataOverride == null)
                throw new InvalidOperationException("CreationKit.ini / CreationKitCustom.ini not found. Use XboxDataOverride to specify XBOX Data root.");

            string xboxDataRoot = options.XboxDataOverride ?? InferXboxDataRoot(gameRoot, iniPath);
            string tifRoot = options.TifRootOverride ??
                             Path.GetFullPath(Path.Combine(gameRoot, "..", "..", "Source", "TGATextures"));

            string pluginName = Path.GetFileNameWithoutExtension(pluginPath);

            var manifest = new DependencyManifest
            {
                Plugin = Path.GetFileName(pluginPath),
                GameRoot = gameRoot,
                DataRoot = dataRoot,
                XboxDataRoot = xboxDataRoot,
                TifRoot = tifRoot
            };

            // 1) Scan plugin strings
            var pluginBytes = File.ReadAllBytes(pluginPath);
            var pluginStrings = ExtractPrintableStrings(pluginBytes, 6).ToList();

            var nifRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var btdNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                            AddFile(manifest, achlistPaths, rel, FileKind.Nif, "plugin-nif", gameRoot, xboxDataRoot);
                    }
                }
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
                        matRelPaths.Add(rel);
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
                        string baseName = Path.GetFileNameWithoutExtension(rel);
                        if (!string.IsNullOrEmpty(baseName)) btdNames.Add(baseName);
                    }
                    else
                    {
                        string baseName = Path.GetFileNameWithoutExtension(rel);
                        if (!string.IsNullOrEmpty(baseName)) btdNames.Add(baseName);
                    }
                }
                else if (s.EndsWith(".psfx", StringComparison.OrdinalIgnoreCase))
                {
                    string rel = s.Replace('/', '\\').TrimStart('\\');
                    if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                        rel = Path.Combine("Data", rel);
                    rel = NormalizeRel(rel);
                    if (File.Exists(Path.Combine(gameRoot, rel)))
                        AddFile(manifest, achlistPaths, rel, FileKind.Particle, "plugin-psfx", gameRoot, xboxDataRoot);
                }
                else if (s.EndsWith(".af", StringComparison.OrdinalIgnoreCase) ||
                         s.EndsWith(".afx", StringComparison.OrdinalIgnoreCase) ||
                         s.EndsWith(".agx", StringComparison.OrdinalIgnoreCase))
                {
                    string rel = s.Replace('/', '\\').TrimStart('\\');
                    if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                        rel = Path.Combine("Data", rel);
                    rel = NormalizeRel(rel);
                    if (File.Exists(Path.Combine(gameRoot, rel)))
                        AddFile(manifest, achlistPaths, rel, FileKind.Anim, "plugin-anim", gameRoot, xboxDataRoot);
                }
                else if (s.EndsWith(".morph", StringComparison.OrdinalIgnoreCase))
                {
                    string rel = s.Replace('/', '\\').TrimStart('\\');
                    if (!rel.StartsWith("Data\\", StringComparison.OrdinalIgnoreCase))
                        rel = Path.Combine("Data", rel);
                    rel = NormalizeRel(rel);
                    if (File.Exists(Path.Combine(gameRoot, rel)))
                        AddFile(manifest, achlistPaths, rel, FileKind.Morph, "plugin-morph", gameRoot, xboxDataRoot);
                }
                else
                {
                    // Generic path-like tokens -> WARN
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
                                AddFile(manifest, achlistPaths, rel, FileKind.Warn, "plugin-warn", gameRoot, xboxDataRoot);
                        }
                    }
                }
            }

            // Terrain backup-only folder Data\terrain\<modname>\**
            string modTerrainFolder = Path.Combine(gameRoot, "Data", "terrain", pluginName);
            if (Directory.Exists(modTerrainFolder))
            {
                foreach (var f in Directory.EnumerateFiles(modTerrainFolder, "*.*", SearchOption.AllDirectories))
                {
                    string rel = "Data\\" + GetRelativePath(Path.Combine(gameRoot, "Data"), f);
                    rel = NormalizeRel(rel);
                    AddBackupOnlyFile(manifest, rel, "terrain-folder");
                }
            }

            // OverlayMasks from .btd -> Textures\Terrain\OverlayMasks\<name>.dds
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

            // 2) NIF -> MAT + MeshPath stems
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
                        if (File.Exists(fullMat)) matRelPaths.Add(rel);
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
                            AddFile(manifest, achlistPaths, rel, FileKind.Rig, $"nif:{nifRel}", gameRoot, xboxDataRoot);
                    }
                    else if (!token.Contains('.') && token.Contains("\\"))
                    {
                        string stem = token.TrimStart('\\');
                        string meshRel = stem.StartsWith("geometries\\", StringComparison.OrdinalIgnoreCase)
                            ? NormalizeRel(Path.Combine("Data", stem + ".mesh"))
                            : NormalizeRel(Path.Combine("Data\\geometries", stem + ".mesh"));

                        if (File.Exists(Path.Combine(gameRoot, meshRel)))
                            AddFile(manifest, achlistPaths, meshRel, FileKind.Mesh, $"nif:{nifRel}", gameRoot, xboxDataRoot);
                    }
                }
            }

            // 3) MATs -> DDS tokens
            foreach (var matRel in matRelPaths)
            {
                string fullMat = Path.Combine(gameRoot, matRel);
                if (!File.Exists(fullMat)) continue;

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
                    if (ddsRel == null) continue;

                    string fullTexPc = Path.Combine(gameRoot, ddsRel);
                    bool pcExists = File.Exists(fullTexPc);

                    string? xbCandidate = null;
                    bool xbExists = false;
                    if (ddsRel.StartsWith("Data\\Textures", StringComparison.OrdinalIgnoreCase))
                    {
                        xbCandidate = Path.Combine(xboxDataRoot, GetRelativePath(Path.Combine(gameRoot, "Data"), fullTexPc));
                        xbExists = File.Exists(xbCandidate);
                    }

                    // Policy: both missing -> silent skip; one missing -> warn; both present -> add; add only if PC exists
                    if (!pcExists && !xbExists)
                        continue;

                    if (pcExists && found.Add(ddsRel))
                    {
                        hasCustomTextures = true;
                        AddFile(manifest, achlistPaths, ddsRel, FileKind.Texture, $"mat:{matRel}", gameRoot, xboxDataRoot);
                        TryAddInterfaceTifForTexture(manifest, ddsRel, tifRoot);
                    }
                }

                if (hasCustomTextures)
                    AddFile(manifest, achlistPaths, matRel, FileKind.Mat, "mat-with-custom-dds", gameRoot, xboxDataRoot);
            }

            // 4) Interface icons / previews
            CollectIconsAndPreviews(manifest, achlistPaths, pluginName, gameRoot, xboxDataRoot, tifRoot);

            // 5) Voice assets
            CollectVoiceAssets(manifest, achlistPaths, pluginName, gameRoot, xboxDataRoot);

            // 6) Scripts (VMAD names + PSC imports)
            static string ToPscRel(string name) =>
                NormalizeRel(Path.Combine("Data", "Scripts", "Source", name.Replace(':', '\\') + ".psc"));
            static string ToPexRel(string name) =>
                NormalizeRel(Path.Combine("Data", "Scripts", name.Replace(':', '\\') + ".pex"));

            var vmadNames = ExtractVmadPexNames(pluginBytes);
            var pscSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pexSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var vm in vmadNames)
            {
                string pscRel = ToPscRel(vm);
                string pexRel = ToPexRel(vm);

                if (File.Exists(Path.Combine(gameRoot, pscRel)) && pscSet.Add(pscRel))
                    AddBackupOnlyFile(manifest, pscRel, "vmad-psc");

                if (File.Exists(Path.Combine(gameRoot, pexRel)) && pexSet.Add(pexRel))
                    AddFile(manifest, achlistPaths, pexRel, FileKind.Pex, "vmad-pex", gameRoot, xboxDataRoot);
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

            var elapsed = DateTime.UtcNow - start;
            return new DependencyScanResult
            {
                Manifest = manifest,
                Achlist = achlistPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(),
                Elapsed = elapsed,
                Diagnostics = diagnostics.ToArray()
            };
        }

        // Helpers (logging suppressed)
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
                throw new InvalidOperationException("sPathToVoiceOutputXB not found in CreationKit.ini; use XboxDataOverride.");
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

        private static void TryAddInterfaceTifForTexture(
            DependencyManifest manifest,
            string relTexturePath,
            string tifRoot)
        {
            if (!relTexturePath.StartsWith("Data\\Textures", StringComparison.OrdinalIgnoreCase))
                return;

            string relUnderTextures = relTexturePath.Substring("Data\\Textures\\".Length);

            if (relUnderTextures.StartsWith("Interface\\", StringComparison.OrdinalIgnoreCase))
            {
                string tifSubPath = Path.ChangeExtension(relUnderTextures, ".tif");
                string tifFull = Path.Combine(tifRoot, tifSubPath);
                if (File.Exists(tifFull))
                {
                    string relTifPc = NormalizeRel(Path.Combine("Source", "TGATextures", tifSubPath));
                    if (!manifest.Files.Any(f => f.PcPath.Equals(relTifPc, StringComparison.OrdinalIgnoreCase)))
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
                    if (!manifest.Files.Any(f => f.PcPath.Equals(relTifPc, StringComparison.OrdinalIgnoreCase)))
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

        private static HashSet<string> ExtractVmadPexNames(byte[] pluginBytes)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string text = Encoding.ASCII.GetString(pluginBytes);

            var scriptNameRegex = new Regex(@"""ScriptName""\s*:\s*""([A-Za-z0-9_:.]+)""", RegexOptions.IgnoreCase);
            foreach (Match m in scriptNameRegex.Matches(text))
            {
                string name = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }

            var pexRegex = new Regex(@"\b([A-Za-z0-9_:.]+)\.pex\b", RegexOptions.IgnoreCase);
            foreach (Match m in pexRegex.Matches(text))
            {
                string name = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }

            return names;
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