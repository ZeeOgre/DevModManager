using System;
using System.Collections.Generic;

namespace DMM.AssetManagers.GameStores.BattleNet;

internal static class BattleNetKnownProducts
{
    // ProductCode examples seen historically:
    // Pro=Overwatch, WoW=World of Warcraft, D3=Diablo III, Fen=Diablo IV, etc.
    // (Keep this minimal and grow from your own product.db observations.)
    private static readonly Dictionary<string, string> ByProductCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WoW"] = "World of Warcraft",
        ["WoWC"] = "World of Warcraft Classic",
        ["WoWB"] = "World of Warcraft Beta",
        ["S1"] = "StarCraft",
        ["S2"] = "StarCraft II",
        ["W3"] = "Warcraft III",
        ["D3"] = "Diablo III",
        ["Fen"] = "Diablo IV",
        ["D2R"] = "Diablo II: Resurrected",
        ["Pro"] = "Overwatch",
        ["ODIN"] = "Call of Duty",
        ["HS"] = "Hearthstone",
        ["Hero"] = "Heroes of the Storm",
    };

    public static string? TryGetDisplayName(string? productCode, string? uid)
    {
        if (!string.IsNullOrWhiteSpace(productCode) && ByProductCode.TryGetValue(productCode!, out var name))
            return name;

        // If uid is more recognizable on your machine, you can add a uid map here later.
        return null;
    }
}