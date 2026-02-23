using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using DMM.AssetManagers.GameStores.Common.Models;

namespace DMM.AssetManagers.GameStores.Common;

internal static class WindowsLauncherDiscovery
{
    private static readonly string[] UninstallRegistryRoots =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public static string? TryFindInstallLocationByDisplayNameContains(string displayNameContains)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var root in UninstallRegistryRoots)
            {
                using var uninstall = hive.OpenSubKey(root);
                if (uninstall is null) continue;

                foreach (var subkeyName in uninstall.GetSubKeyNames())
                {
                    using var subkey = uninstall.OpenSubKey(subkeyName);
                    if (subkey is null) continue;

                    var displayName = subkey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName)
                        || displayName.IndexOf(displayNameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var installLocation = subkey.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                        return installLocation;

                    var installSource = subkey.GetValue("InstallSource") as string;
                    if (!string.IsNullOrWhiteSpace(installSource) && Directory.Exists(installSource))
                        return installSource;

                    var icon = subkey.GetValue("DisplayIcon") as string;
                    var fromIcon = TryDeriveFolderFromPath(icon);
                    if (fromIcon is not null)
                        return fromIcon;
                }
            }
        }

        return null;
    }

    public static string? TryDeriveFolderFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var trimmed = path!.Trim().Trim('"');
        var comma = trimmed.IndexOf(',');
        if (comma > 0) trimmed = trimmed[..comma];

        try
        {
            if (File.Exists(trimmed))
            {
                var dir = Path.GetDirectoryName(trimmed);
                return !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) ? dir : null;
            }

            if (Directory.Exists(trimmed))
                return trimmed;
        }
        catch
        {
            // best effort only
        }

        return null;
    }

    public static InstallFoldersSnapshot CreateInstallFolders(string path)
        => new()
        {
            InstallFolder = new FolderRef { Path = path },
            ContentFolder = null,
            DataFolder = null,
            AdditionalFolders = Array.Empty<NamedFolderRef>()
        };

    public static ExceptionInfo ToExceptionInfo(Exception ex)
        => new()
        {
            Type = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            HResult = ex.HResult.ToString("X")
        };
}
