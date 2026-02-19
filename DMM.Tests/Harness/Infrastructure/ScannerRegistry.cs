using System.Collections.Generic;

using DMM.AssetManagers.GameStores.Common;

// Add store scanner namespaces here
using DMM.AssetManagers.GameStores.XBox;

namespace DMM.Tests.Harness.Infrastructure;

public static class ScannerRegistry
{
    public static IReadOnlyList<IStoreInstallScanner> CreateAllScanners()
    {
        var scanners = new List<IStoreInstallScanner>
        {
            new XboxInstallScanner(),
        };

        // When ready:
        // scanners.Add(new SteamInstallScanner());
        // scanners.Add(new GogInstallScanner());
        // scanners.Add(new EpicInstallScanner());
        // scanners.Add(new EaInstallScanner());

        return scanners;
    }
}
