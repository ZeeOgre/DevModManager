using System.Collections.Generic;

using DMM.AssetManagers.GameStores.Common;
using System.Runtime.Versioning;

// Add store scanner namespaces here
using DMM.AssetManagers.GameStores.XBox;
using DMM.AssetManagers.GameStores.Steam;
using DMM.AssetManagers.GameStores.Epic;
using DMM.AssetManagers.GameStores.Gog;


namespace DMM.Tests.Harness.Infrastructure;

public static class ScannerRegistry
{
    public static IReadOnlyList<IStoreInstallScanner> CreateAllScanners()
    {
        var scanners = new List<IStoreInstallScanner>
        {
            new XboxInstallScanner(),
            new SteamInstallScanner(),
            new EpicInstallScanner(),
            new GogInstallScanner(),
        };

        // When ready:
        // scanners.Add(new SteamInstallScanner());
        // scanners.Add(new GogInstallScanner());
        // scanners.Add(new EpicInstallScanner());
        // scanners.Add(new EaInstallScanner());

        return scanners;
    }
}
