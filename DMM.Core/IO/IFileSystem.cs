using System.Text;

namespace DMM.Core.IO;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);

    byte[] ReadAllBytes(string path);
    string ReadAllText(string path, Encoding? encoding = null);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption);

    void WriteAllText(string path, string content, Encoding? encoding = null);
    void WriteAllBytes(string path, byte[] bytes);

    Stream OpenRead(string path);
    Stream OpenWrite(string path);

    void CreateDirectory(string path);

    string GetDirectoryName(string path);
    string GetFileName(string path);
    string Combine(params string[] parts);
    string GetRelativePath(string relativeTo, string path);

    // Reparse point / junction / hardlink support
    bool IsReparsePoint(string path);
    bool IsJunction(string path);
    bool CreateHardLink(string existingFilePath, string linkPath);
    bool CreateDirectoryJunction(string junctionPoint, string targetDir);

    // Move + replace helpers
    bool MoveFileAndReplaceWithJunction(string sourceFilePath, string destFilePath, bool overwrite = false);
    bool MoveDirectoryAndReplaceWithJunction(string sourceDir, string targetDir, bool overwrite = false);

    bool RestoreFileFromJunction(string junctionPath, string? expectedTargetPath = null, bool overwrite = false);
    bool RestoreDirectoryFromJunction(string junctionPath, string? expectedTargetDir = null, bool overwrite = false);

    // Bulk operations: mapping of source -> destination
    bool MovePaths(IDictionary<string, string> pathMap, bool overwrite = false, bool createJunctionAtSource = false, int maxDegreeOfParallelism = 4);
    bool CopyPaths(IDictionary<string, string> pathMap, bool overwrite = false, int maxDegreeOfParallelism = 4, bool recursive = true);
}