using System.Text;
namespace DMM.AssetManagers.NIF;

/// <summary>Human-editable authoritative schema registry. Family profiles append or override small definitions.</summary>
public static class NifSchemaCatalog
{
    private static readonly Dictionary<string, NifSchemaType> Types = Build();
    private static readonly NifSchemaProfile[] Profiles =
    [
        // NifSkope build/nif.xml lines 216-221: explicit version/user/stream tuples.
        new("Skyrim", NifFamily.Skyrim, [83], "V20_2_0_7_SKY"),
        new("SkyrimSE", NifFamily.Skyrim, [100], "V20_2_0_7_SSE", "Skyrim"),
        new("Fallout4", NifFamily.Fallout4, [130], "V20_2_0_7_FO4"),
        new("Fallout4Special", NifFamily.Fallout4, [132, 139], "V20_2_0_7_FO4_2", "Fallout4"),
        new("Fallout76", NifFamily.Fallout4, [155], "V20_2_0_7_F76", "Fallout4"),
        new("Starfield170", NifFamily.Starfield, [170], "NifSkope #BS_GTE_STF# baseline; exact documented stream 170")
    ];
    public static IReadOnlyList<NifSchemaType> AllTypes => Types.Values.OrderBy(x => x.Name).ToArray();
    public static bool TryGet(string name, out NifSchemaType? type) => Types.TryGetValue(name, out type);
    public static NifSchemaProfileResolution ResolveProfile(uint nifVersion, uint userVersion, uint bethesdaStreamVersion)
    {
        NifSchemaProfile? exact = Profiles.FirstOrDefault(x => x.BethesdaStreamVersions.Contains(bethesdaStreamVersion));
        NifSchemaProfile? nearest = Profiles.Where(x => x.BethesdaStreamVersions.Min() <= bethesdaStreamVersion).OrderByDescending(x => x.BethesdaStreamVersions.Max()).FirstOrDefault();
        return new(exact, nearest);
    }
    public static string FamilyIndex(string family) => string.Join("\n", AllTypes.Where(x => string.Equals(family, "Other", StringComparison.OrdinalIgnoreCase) || x.Predicate.Matches(Enum.Parse<NifFamily>(family, true), 0)).Select(x => $"{(x.IsStruct ? "Struct" : "Block")}|{x.Name}|{x.BaseType}"));
    public static string InventoryReport() { var b = new StringBuilder("# Internal NIF schema inventory\n\n"); foreach (var t in AllTypes) { b.Append("## ").Append(t.Name).Append("\n"); b.Append("Base: ").Append(t.BaseType ?? "—").Append("\n"); foreach (var f in t.Fields) b.Append("- ").Append(f.Name).Append(": ").Append(f.Kind).Append('\n'); } return b.ToString(); }
    private static Dictionary<string, NifSchemaType> Build()
    {
        var types = new List<NifSchemaType>();
        NifSchemaCommon.Add(types); NifSchemaBethesda.Add(types); NifSchemaSkyrim.Add(types); NifSchemaFallout4.Add(types); NifSchemaStarfield.Add(types); NifSchemaLegacy.Add(types);
        // A block name can have additive family variants (for example shader
        // properties in Fallout 4 and Starfield). Keep the registry key stable;
        // profile-aware field selection resolves the applicable variant.
        return types.GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
    }
}
