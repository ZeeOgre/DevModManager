using System.Text;
namespace DMM.AssetManagers.NIF;

/// <summary>Human-editable authoritative schema registry. Family profiles append or override small definitions.</summary>
public static class NifSchemaCatalog
{
    private static readonly Dictionary<string, NifSchemaType> Types = Build();
    private static readonly NifSchemaProfile[] Profiles =
    [
        new("Skyrim83", NifFamily.Skyrim, 83), new("Fallout4_130", NifFamily.Fallout4, 130),
        new("Fallout4_132", NifFamily.Fallout4, 132, "Fallout4_130"), new("Starfield170", NifFamily.Starfield, 170),
        new("Starfield172", NifFamily.Starfield, 172, "Starfield170"), new("Starfield173", NifFamily.Starfield, 173, "Starfield172"), new("Starfield175", NifFamily.Starfield, 175, "Starfield173")
    ];
    public static IReadOnlyList<NifSchemaType> AllTypes => Types.Values.OrderBy(x => x.Name).ToArray();
    public static bool TryGet(string name, out NifSchemaType? type) => Types.TryGetValue(name, out type);
    public static NifSchemaProfileResolution ResolveProfile(uint nifVersion, uint userVersion, uint bethesdaStreamVersion)
    {
        NifSchemaProfile? exact = Profiles.FirstOrDefault(x => x.BethesdaStreamVersion == bethesdaStreamVersion);
        NifSchemaProfile? nearest = Profiles.Where(x => x.BethesdaStreamVersion <= bethesdaStreamVersion).OrderByDescending(x => x.BethesdaStreamVersion).FirstOrDefault();
        return new(exact, nearest);
    }
    public static string FamilyIndex(string family) => string.Join("\n", AllTypes.Where(x => string.Equals(family, "Other", StringComparison.OrdinalIgnoreCase) || x.Predicate.Matches(Enum.Parse<NifFamily>(family, true), 0)).Select(x => $"{(x.IsStruct ? "Struct" : "Block")}|{x.Name}|{x.BaseType}"));
    public static string InventoryReport() { var b = new StringBuilder("# Internal NIF schema inventory\n\n"); foreach (var t in AllTypes) { b.Append("## ").Append(t.Name).Append("\n"); b.Append("Base: ").Append(t.BaseType ?? "—").Append("\n"); foreach (var f in t.Fields) b.Append("- ").Append(f.Name).Append(": ").Append(f.Kind).Append('\n'); } return b.ToString(); }
    private static Dictionary<string, NifSchemaType> Build()
    {
        var types = new List<NifSchemaType>();
        NifSchemaCommon.Add(types); NifSchemaBethesda.Add(types); NifSchemaSkyrim.Add(types); NifSchemaFallout4.Add(types); NifSchemaStarfield.Add(types); NifSchemaLegacy.Add(types);
        return types.ToDictionary(x => x.Name, StringComparer.Ordinal);
    }
}
