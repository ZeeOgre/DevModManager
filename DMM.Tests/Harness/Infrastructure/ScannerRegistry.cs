using System;
using System.Collections.Generic;

using DMM.AssetManagers.GameStores.Common;
using System.Runtime.Versioning;

// Add store scanner namespaces here
using DMM.AssetManagers.GameStores.XBox;
using DMM.AssetManagers.GameStores.Steam;
using DMM.AssetManagers.GameStores.Epic;
using DMM.AssetManagers.GameStores.Gog;
using DMM.AssetManagers.GameStores.PSN;
using DMM.AssetManagers.GameStores.BattleNet;
using DMM.AssetManagers.GameStores.Minecraft;
using DMM.AssetManagers.GameStores.EA;
using DMM.AssetManagers.GameStores.Origin;
using DMM.AssetManagers.GameStores.Rockstar;


namespace DMM.Tests.Harness.Infrastructure;

public static class ScannerRegistry
{
    public static IReadOnlyList<IStoreInstallScanner> CreateAllScanners()
    {
        var scanners = new List<IStoreInstallScanner>();

        if (OperatingSystem.IsWindows())
        {
            #if WINDOWS
            new XboxInstallScanner(),
            new SteamInstallScanner(),
            new EpicInstallScanner(),
            new GogInstallScanner(),
            new PsnInstallScanner(),
            new BattleNetInstallScanner(),
            new MinecraftInstallScanner(),
            new EaInstallScanner(),
            new OriginInstallScanner(),
            new RockstarInstallScanner(),
            #endif
        };

        // When ready:
        // scanners.Add(new SteamInstallScanner());
        // scanners.Add(new GogInstallScanner());
        // scanners.Add(new EpicInstallScanner());
        // scanners.Add(new EaInstallScanner());

        return scanners;
    }
}
