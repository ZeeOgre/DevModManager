namespace DMM.AssetManagers.NIF;

public enum NifFieldKind { UInt8, UInt16, UInt32, Int32, Float32, Bool, SizedString, StringIndex, Ref, Struct, Array, Bytes }
public enum NifNormalizationStrategy { None, Material, Mesh, Rig, Havok, Behavior, Texture }
public abstract record NifPredicate { public abstract bool Matches(NifFamily family, uint streamVersion); }
public sealed record AnyPredicate : NifPredicate { public override bool Matches(NifFamily family, uint streamVersion) => true; }
public sealed record FamilyPredicate(NifFamily Family, uint MinimumStreamVersion = 0, uint MaximumStreamVersion = uint.MaxValue) : NifPredicate { public override bool Matches(NifFamily family, uint streamVersion) => family == Family && streamVersion >= MinimumStreamVersion && streamVersion <= MaximumStreamVersion; }
public abstract record NifLengthExpression;
public sealed record FixedLength(int Value) : NifLengthExpression;
public sealed record CountField(string FieldName) : NifLengthExpression;
public sealed record BitFlagLength(string FieldName, uint Mask) : NifLengthExpression;
public sealed record NifSchemaField(string Name, NifFieldKind Kind, NifPredicate? Predicate = null, string? Template = null, NifLengthExpression? Length = null, string? DependencyCategory = null, NifNormalizationStrategy Normalization = NifNormalizationStrategy.None)
{
    /// <summary>Serialized schema type name, retained for callers that display inventory rows.</summary>
    public string Type => Template ?? Kind.ToString();
    public bool CanContainExternalDependency => DependencyCategory is not null;
}
public sealed record NifSchemaType(string Name, bool IsStruct, string? BaseType, NifPredicate Predicate, IReadOnlyList<NifSchemaField> Fields);
public sealed record NifSchemaProfile(string Name, NifFamily Family, uint BethesdaStreamVersion, string? BaseProfile = null);
public sealed record NifSchemaProfileResolution(NifSchemaProfile? Profile, NifSchemaProfile? NearestProfile)
{ public bool IsKnown => Profile is not null; public NifFamily Family => Profile?.Family ?? NifFamily.Unknown; }
