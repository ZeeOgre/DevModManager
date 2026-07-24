using System.Text;
namespace DMM.AssetManagers.NIF;

/// <summary>Human-editable authoritative schema registry. Family profiles append or override small definitions.</summary>
public static class NifSchemaCatalog
{
    private static readonly Dictionary<string, NifSchemaType> Types = Build();
    public static IReadOnlyList<NifSchemaType> AllTypes => Types.Values.OrderBy(x => x.Name).ToArray();
    public static bool TryGet(string name, out NifSchemaType? type) => Types.TryGetValue(name, out type);
    public static string FamilyIndex(string family) => string.Join("\n", AllTypes.Where(x => string.Equals(family, "Other", StringComparison.OrdinalIgnoreCase) || x.Predicate.Matches(Enum.Parse<NifFamily>(family, true), 0)).Select(x => $"{(x.IsStruct ? "Struct" : "Block")}|{x.Name}|{x.BaseType}"));
    public static string InventoryReport() { var b = new StringBuilder("# Internal NIF schema inventory\n\n"); foreach (var t in AllTypes) { b.Append("## ").Append(t.Name).Append("\n"); b.Append("Base: ").Append(t.BaseType ?? "—").Append("\n"); foreach (var f in t.Fields) b.Append("- ").Append(f.Name).Append(": ").Append(f.Kind).Append('\n'); } return b.ToString(); }
    private static Dictionary<string, NifSchemaType> Build()
    {
        var types = new List<NifSchemaType>();
        NifSchemaCommon.Add(types); NifSchemaBethesda.Add(types); NifSchemaSkyrim.Add(types); NifSchemaFallout4.Add(types); NifSchemaStarfield.Add(types); NifSchemaLegacy.Add(types);
        return types.ToDictionary(x => x.Name, StringComparer.Ordinal);
    }
}
