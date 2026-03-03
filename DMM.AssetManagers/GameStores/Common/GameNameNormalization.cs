using System;

namespace DMM.AssetManagers.GameStores.Common;

public static class GameNameNormalization
{
    public static string NormalizeGameName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        const string pcSuffix = " (PC)";
        return name.EndsWith(pcSuffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^pcSuffix.Length].TrimEnd()
            : name.Trim();
    }

    public static string ToStoreLabel(string storeKey) => storeKey.ToLowerInvariant() switch
    {
        StoreKeys.BattleNet => "Battle.net",
        StoreKeys.Ea => "EA",
        StoreKeys.Gog => "GOG",
        StoreKeys.Psn => "PSN",
        StoreKeys.Xbox => "Game Pass",
        _ => string.IsNullOrWhiteSpace(storeKey) ? "Unknown" : char.ToUpperInvariant(storeKey[0]) + storeKey[1..]
    };
}
