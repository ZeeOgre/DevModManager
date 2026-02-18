using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DMM.Core.IO;

public sealed class DefaultFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public string ReadAllText(string path, Encoding? encoding = null) =>
        encoding == null ? File.ReadAllText(path) : File.ReadAllText(path, encoding);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);

    public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(path, searchPattern, searchOption);

    public void WriteAllText(string path, string content, Encoding? encoding = null)
    {
        if (encoding == null) File.WriteAllText(path, content);
        else File.WriteAllText(path, content, encoding);
    }

    public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);

    public Stream OpenRead(string path) => File.OpenRead(path);

    public Stream OpenWrite(string path) => File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string GetDirectoryName(string path) => Path.GetDirectoryName(path) ?? string.Empty;

    public string GetFileName(string path) => Path.GetFileName(path);

    public string Combine(params string[] parts) => Path.Combine(parts);

    public string GetRelativePath(string relativeTo, string path)
    {
#if NET8_0_OR_GREATER
        return Path.GetRelativePath(relativeTo, path);
#else
        var u1 = new Uri(relativeTo.EndsWith("\\") ? relativeTo : relativeTo + "\\");
        var u2 = new Uri(path);
        return Uri.UnescapeDataString(u1.MakeRelativeUri(u2).ToString()).Replace('/', '\\');
#endif
    }

    // --- Reparse point / junction / hardlink support ---

    public bool IsReparsePoint(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var attr = File.GetAttributes(path);
            return (attr & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    public bool IsJunction(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            if (!DirectoryExists(path) && !FileExists(path)) return false;
            var attr = File.GetAttributes(path);
            if ((attr & FileAttributes.ReparsePoint) == 0) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool CreateHardLink(string existingFilePath, string linkPath)
    {
        if (string.IsNullOrWhiteSpace(existingFilePath) || string.IsNullOrWhiteSpace(linkPath))
            throw new ArgumentException("Paths must be non-empty.");

        if (!FileExists(existingFilePath))
            throw new FileNotFoundException("Source file not found", existingFilePath);

        var dir = GetDirectoryName(linkPath);
        if (!string.IsNullOrEmpty(dir)) CreateDirectory(dir);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            bool ok = CreateHardLinkWin(linkPath, existingFilePath, IntPtr.Zero);
            return ok;
        }
        else
        {
            try
            {
                int rc = link(existingFilePath, linkPath);
                return rc == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CreateDirectoryJunction(string junctionPoint, string targetDir)
    {
        if (string.IsNullOrWhiteSpace(junctionPoint) || string.IsNullOrWhiteSpace(targetDir))
            throw new ArgumentException("Paths must be non-empty.");

        if (!DirectoryExists(targetDir))
            throw new DirectoryNotFoundException(targetDir);

        if (DirectoryExists(junctionPoint) || FileExists(junctionPoint))
            return false; // already exists

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPoint}\" \"{targetDir}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
        else
        {
            try
            {
#if NET8_0_OR_GREATER
                Directory.CreateSymbolicLink(junctionPoint, targetDir);
                return DirectoryExists(junctionPoint);
#else
                var psi = new ProcessStartInfo("ln", $"-s \"{targetDir}\" \"{junctionPoint}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit();
                return p.ExitCode == 0;
#endif
            }
            catch
            {
                return false;
            }
        }
    }

    // --- Move + replace helpers ---

    public bool MoveFileAndReplaceWithJunction(string sourceFilePath, string destFilePath, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || string.IsNullOrWhiteSpace(destFilePath))
            throw new ArgumentException("Paths must be non-empty.");

        if (!FileExists(sourceFilePath))
            throw new FileNotFoundException("Source file not found", sourceFilePath);

        var destDir = GetDirectoryName(destFilePath);
        if (!string.IsNullOrEmpty(destDir) && !DirectoryExists(destDir))
            CreateDirectory(destDir);

        if (FileExists(destFilePath))
        {
            if (!overwrite) return false;
            try { File.Delete(destFilePath); } catch { return false; }
        }

        try
        {
            File.Move(sourceFilePath, destFilePath);
        }
        catch
        {
            return false;
        }

        // Try hardlink first
        bool linkOk = false;
        try
        {
            linkOk = CreateHardLink(destFilePath, sourceFilePath);
        }
        catch { linkOk = false; }

        if (linkOk) return true;

        // Fallback: try to create file symbolic link (may require privileges on Windows)
        try
        {
#if NET8_0_OR_GREATER
            File.CreateSymbolicLink(sourceFilePath, destFilePath);
            return FileExists(sourceFilePath);
#else
            // On older runtimes we do not attempt symlink; rollback
            try
            {
                File.Move(destFilePath, sourceFilePath);
            }
            catch
            {
                throw new IOException("Failed to create link and failed to rollback; state may be inconsistent.");
            }
            return false;
#endif
        }
        catch
        {
            // rollback: move dest back to source if possible
            try
            {
                if (FileExists(destFilePath))
                    File.Move(destFilePath, sourceFilePath);
            }
            catch
            {
                throw new IOException("Failed to create link and failed to rollback; state may be inconsistent.");
            }
            return false;
        }
    }

    public bool MoveDirectoryAndReplaceWithJunction(string sourceDir, string targetDir, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir))
            throw new ArgumentException("Paths must be non-empty.");

        if (!DirectoryExists(sourceDir))
            throw new DirectoryNotFoundException(sourceDir);

        var parent = GetDirectoryName(targetDir);
        if (!string.IsNullOrEmpty(parent) && !DirectoryExists(parent))
            CreateDirectory(parent);

        if (DirectoryExists(targetDir))
        {
            if (!overwrite) return false;
            try { Directory.Delete(targetDir, recursive: true); } catch { return false; }
        }

        bool moved = false;
        try
        {
            // Try fast move
            Directory.Move(sourceDir, targetDir);
            moved = true;
        }
        catch
        {
            // Move failed (likely cross-volume). Fall back to copy + delete.
            try
            {
                CopyDirectoryRecursive(sourceDir, targetDir);
                // Copy succeeded — attempt delete source
                Directory.Delete(sourceDir, recursive: true);
                moved = true;
            }
            catch
            {
                // fallback failed
                return false;
            }
        }

        if (!moved) return false;

        // Create a junction at original source path pointing at targetDir
        bool jOk = CreateDirectoryJunction(sourceDir, targetDir);
        if (jOk) return true;

        // If junction creation failed try to rollback (move back if possible)
        try
        {
            if (DirectoryExists(targetDir))
            {
                // Attempt to move back if same-volume; Directory.Move may fail cross-volume
                Directory.Move(targetDir, sourceDir);
            }
        }
        catch
        {
            throw new IOException("Failed to create junction and failed to rollback; state may be inconsistent.");
        }

        return false;
    }

    // --- Bulk operations (new) ---

    public bool MovePaths(IDictionary<string, string> pathMap, bool overwrite = false, bool createJunctionAtSource = false, int maxDegreeOfParallelism = 4)
    {
        if (pathMap == null) throw new ArgumentNullException(nameof(pathMap));
        if (pathMap.Count == 0) return true;

        var successes = new ConcurrentBag<KeyValuePair<string, string>>();
        var failures = new ConcurrentBag<(KeyValuePair<string, string> Item, Exception Ex)>();

        var po = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism) };

        Parallel.ForEach(pathMap, po, kvp =>
        {
            var src = kvp.Key;
            var dst = kvp.Value;
            try
            {
                if (FileExists(src))
                {
                    var destDir = GetDirectoryName(dst);
                    if (!string.IsNullOrEmpty(destDir) && !DirectoryExists(destDir)) CreateDirectory(destDir);

                    if (FileExists(dst))
                    {
                        if (!overwrite) throw new IOException($"Destination exists: {dst}");
                        File.Delete(dst);
                    }

                    try
                    {
                        File.Move(src, dst);
                    }
                    catch
                    {
                        // cross-volume fallback
                        File.Copy(src, dst, overwrite: true);
                        File.Delete(src);
                    }

                    successes.Add(new KeyValuePair<string, string>(src, dst));
                }
                else if (DirectoryExists(src))
                {
                    var parent = GetDirectoryName(dst);
                    if (!string.IsNullOrEmpty(parent) && !DirectoryExists(parent)) CreateDirectory(parent);

                    try
                    {
                        Directory.Move(src, dst);
                    }
                    catch
                    {
                        // cross-volume fallback: copy then delete
                        CopyDirectoryRecursive(src, dst);
                        Directory.Delete(src, recursive: true);
                    }

                    // Optionally create junction at original source (now missing)
                    if (createJunctionAtSource)
                    {
                        CreateDirectoryJunction(src, dst);
                    }

                    successes.Add(new KeyValuePair<string, string>(src, dst));
                }
                else
                {
                    throw new FileNotFoundException("Source not found", src);
                }
            }
            catch (Exception ex)
            {
                failures.Add((kvp, ex));
            }
        });

        if (failures.IsEmpty) return true;

        // Attempt rollback of successful moves (best-effort)
        var rollbackErrors = new List<Exception>();
        foreach (var ok in successes)
        {
            try
            {
                var src = ok.Key;
                var dst = ok.Value;
                if (FileExists(dst) && !FileExists(src))
                {
                    var dir = GetDirectoryName(src);
                    if (!string.IsNullOrEmpty(dir) && !DirectoryExists(dir)) CreateDirectory(dir);
                    try { File.Move(dst, src); }
                    catch
                    {
                        // attempt copy back
                        File.Copy(dst, src, overwrite: true);
                        File.Delete(dst);
                    }
                }
                else if (DirectoryExists(dst) && !DirectoryExists(src))
                {
                    try { Directory.Move(dst, src); }
                    catch
                    {
                        CopyDirectoryRecursive(dst, src);
                        Directory.Delete(dst, recursive: true);
                    }
                }
            }
            catch (Exception ex)
            {
                rollbackErrors.Add(ex);
            }
        }

        if (rollbackErrors.Count > 0)
            throw new IOException("MovePaths failed and rollback partially failed; filesystem may be inconsistent.", rollbackErrors[0]);

        // Return false to indicate original requested operation failed (but rollback succeeded)
        return false;
    }

    public bool CopyPaths(IDictionary<string, string> pathMap, bool overwrite = false, int maxDegreeOfParallelism = 4, bool recursive = true)
    {
        if (pathMap == null) throw new ArgumentNullException(nameof(pathMap));
        if (pathMap.Count == 0) return true;

        var failures = new ConcurrentBag<(KeyValuePair<string, string> Item, Exception Ex)>();
        var po = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism) };

        Parallel.ForEach(pathMap, po, kvp =>
        {
            var src = kvp.Key;
            var dst = kvp.Value;
            try
            {
                if (FileExists(src))
                {
                    var destDir = GetDirectoryName(dst);
                    if (!string.IsNullOrEmpty(destDir) && !DirectoryExists(destDir)) CreateDirectory(destDir);

                    if (FileExists(dst) && !overwrite) throw new IOException($"Destination exists: {dst}");
                    File.Copy(src, dst, overwrite: overwrite);
                }
                else if (DirectoryExists(src))
                {
                    if (!recursive)
                    {
                        // copy only top-level files
                        if (!DirectoryExists(dst)) CreateDirectory(dst);
                        foreach (var file in Directory.GetFiles(src, "*", SearchOption.TopDirectoryOnly))
                        {
                            var destFile = Path.Combine(dst, Path.GetFileName(file));
                            if (FileExists(destFile) && !overwrite) throw new IOException($"Destination exists: {destFile}");
                            File.Copy(file, destFile, overwrite: overwrite);
                        }
                    }
                    else
                    {
                        // recursive copy
                        CopyDirectoryRecursive(src, dst);
                    }
                }
                else
                {
                    throw new FileNotFoundException("Source not found", src);
                }
            }
            catch (Exception ex)
            {
                failures.Add((kvp, ex));
            }
        });

        return failures.IsEmpty;
    }

    public bool RestoreFileFromJunction(string junctionPath, string? expectedTargetPath = null, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(junctionPath))
            throw new ArgumentException("junctionPath must be non-empty.", nameof(junctionPath));

        // Determine target
        string? target = expectedTargetPath;
        if (string.IsNullOrWhiteSpace(target))
        {
            // If junctionPath is a symlink, try to resolve its target
            try
            {
#if NET8_0_OR_GREATER
                var fi = new FileInfo(junctionPath);
                var link = fi.Exists ? fi.ResolveLinkTarget(true) : null;
                target = link?.FullName;
#else
                target = null;
#endif
            }
            catch
            {
                target = null;
            }
        }

        if (string.IsNullOrWhiteSpace(target))
            return false; // cannot determine target; caller should supply expectedTargetPath

        if (!FileExists(target))
            return false; // nothing to restore

        // If junction path exists, remove it (or handle overwrite)
        if (FileExists(junctionPath))
        {
            if (IsReparsePoint(junctionPath))
            {
                // delete the link/junction file
                try { File.Delete(junctionPath); }
                catch { return false; }
            }
            else
            {
                if (!overwrite) return false;
                try { File.Delete(junctionPath); }
                catch { return false; }
            }
        }

        try
        {
            // Move target back to junctionPath
            File.Move(target, junctionPath);
            return true;
        }
        catch
        {
            // Attempt to rollback: try to recreate the original link at junctionPath pointing to target
            try
            {
                // Prefer hardlink recreation if possible
                if (CreateHardLink(target, junctionPath)) return false;
#if NET8_0_OR_GREATER
                // fallback to symbolic link
                File.CreateSymbolicLink(junctionPath, target);
                return false;
#endif
            }
            catch
            {
                // ignore
            }
            throw new IOException("Failed to restore file and rollback creation of junction failed; repository may be inconsistent.");
        }
    }

    public bool RestoreDirectoryFromJunction(string junctionPath, string? expectedTargetDir = null, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(junctionPath))
            throw new ArgumentException("junctionPath must be non-empty.", nameof(junctionPath));

        string? target = expectedTargetDir;
        if (string.IsNullOrWhiteSpace(target))
        {
            // Try to resolve link target for directory junction/symlink
            try
            {
#if NET8_0_OR_GREATER
                var di = new DirectoryInfo(junctionPath);
                var link = di.Exists ? di.ResolveLinkTarget(true) : null;
                target = link?.FullName;
#else
                target = null;
#endif
            }
            catch
            {
                target = null;
            }
        }

        if (string.IsNullOrWhiteSpace(target))
            return false;

        if (!DirectoryExists(target))
            return false;

        // If junction exists at junctionPath, remove it (junction deletion should not delete target)
        if (DirectoryExists(junctionPath))
        {
            if (IsJunction(junctionPath))
            {
                try { Directory.Delete(junctionPath); }
                catch { return false; }
            }
            else
            {
                if (!overwrite) return false;
                try { Directory.Delete(junctionPath, recursive: true); }
                catch { return false; }
            }
        }

        try
        {
            Directory.Move(target, junctionPath);
            return true;
        }
        catch
        {
            // rollback: try to re-create a junction from junctionPath to target
            try
            {
                CreateDirectoryJunction(junctionPath, target);
            }
            catch
            {
                // swallow - will throw below
            }
            throw new IOException("Failed to restore directory and rollback creation of junction failed; filesystem may be inconsistent.");
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        var dirs = Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories);
        Directory.CreateDirectory(targetDir);

        // Copy top-level files
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        // Copy subdirectories and files
        foreach (var dir in dirs)
        {
            var rel = Path.GetRelativePath(sourceDir, dir);
            var destSub = Path.Combine(targetDir, rel);
            Directory.CreateDirectory(destSub);
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var destFile = Path.Combine(destSub, Path.GetFileName(f));
                File.Copy(f, destFile, overwrite: true);
            }
        }
    }

    // P/Invoke for Windows CreateHardLink
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkWin(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    // Unix link(2)
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int link(string oldpath, string newpath);
}