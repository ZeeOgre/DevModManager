# NIF dependency schema map

This map is the implementation contract for deterministic dependency extraction. Its authoritative source is the checked-in C# `NifSchemaCatalog` in `DMM.AssetManagers.NIF`; NifSkope is reference material only.

The schema catalog owns every known block, struct, field, predicate, and dependency classification. It has no runtime dependency on vendored XML or generated external documentation.

## Dispatch contract

1. Parse the NIF header once: NIF version, user version, Bethesda stream version, block type table, block type indexes, block sizes, and block spans.
2. Select a family from the header; no path probing is used for family selection.
3. Dispatch every path-bearing block through the family schema.
4. A recognized-family parse is complete only when every encountered path-bearing block and applicable conditional field is decoded.
5. Unknown non-path-bearing blocks are recorded and skipped. Unknown blocks that can carry external references make the parse incomplete and require an explicitly logged fallback.
6. Heuristic token scans are a fallback only for unknown families, malformed structures, or incomplete schema coverage.

## Family selection

| Family | Header discriminator | External resource conventions |
| --- | --- | --- |
| Skyrim / Skyrim SE | Bethesda stream before Fallout 4 / Starfield ranges | `.nif`, `.dds`, `.hkx`, `.bsa`-style paths; shader blocks can reference texture sets and legacy material forms. |
| Fallout 4 | Bethesda stream in the Fallout 4 range | `BSTriShape`, `BSLightingShaderProperty`/`BSEffectShaderProperty`, `.bgsm`/`.bgem`, texture sets, behavior/Havok fields. |
| Starfield | Bethesda stream version `>= 170` | `BSGeometry` four-slot external `.mesh` array, `materials/*.mat`, Starfield material and animation/collision fields. |

The exact numeric version predicates must be generated from NifSkope's `#BS_*#` schema predicates rather than duplicated as ad-hoc offsets.

## Starfield complete NIF inventory

NifSkope's Starfield NIF schema has three external-file fields. `BSLayeredMaterial`,
`BSTextureSet`, and the texture/decal structures are the decoded **external `.mat`
resource**, exposed by NifSkope as abstract fields; they are not serialized NIF
blocks and therefore are deliberately not scanned in a NIF. `SkinAttach` and
`BoneTranslations` contain bone labels, not external asset paths. `BSResourceID`
and the weak-reference hashes are identifiers, not reversible paths.

| Block / field | Dependency kind | NifSkope schema location | Status |
| --- | --- | --- | --- |
| `BSGeometry` → `Meshes[4]` → `Mesh Path` | Mesh | `BSMeshArray` / `BSMesh` | Implemented: read four optional external `SizedString` paths. |
| `BSLightingShaderProperty` → inherited `NiObjectNET.Name` | Material | `#BS_GTE_STF#`, nonempty Name; header string index | Typed: `Data\\Materials\\*.mat` |
| `BSEffectShaderProperty` → inherited `NiObjectNET.Name` | Material | `#BS_GTE_STF#`, nonempty Name; header string index | Typed: `Data\\Materials\\*.mat` |
| `BSBehaviorGraphExtraData` → `Behaviour Graph File` | Behavior | unconditional; header string index | Typed: `Data\\…\\*.hkx` |
| `BSGeometry` → `Meshes[4]` → `Mesh Path` | Mesh | `Has Mesh == 1` and `Flags & 512 == 0`; `SizedString` | Typed: `Data\\Geometries\\*.mesh` |
| `BSWeakReferenceNode` → `Water Refs[].Material` | None | unconditional `SizedString`, but NifSkope does not classify it as an extractable external resource | Not a dependency field |

There are no Starfield NIF texture, rig, collision, or morph path fields in the
vendored NifSkope schema. Texture paths mentioned by the Starfield schema belong
to externally loaded `.mat` files, and collision resource IDs are hashes.

## Required diagnostics per NIF

* detected family, NIF version, user version, and Bethesda stream version;
* block count and encountered block-type counts;
* decoded dependency records with block index, field name, and byte offset;
* unsupported path-bearing block/field records;
* structured completeness and fallback reason;
* structured parse time, fallback time, and total time.

## Regression fixture matrix

| Family | Required fixture coverage |
| --- | --- |
| Starfield | four external LOD mesh paths; material-backed lighting and effect shader blocks; texture-bearing paths; skeleton/behavior/collision examples; malformed block span and unsupported path-bearing block diagnostics. |
| Fallout 4 | `BSTriShape`; `.bgsm`/`.bgem`; texture set; behavior/Havok references; malformed and unsupported diagnostics. |
| Skyrim / SE | geometry; shader texture set; texture and animation/Havok references; malformed and unsupported diagnostics. |

A fixture is accepted only when its extracted references match the corresponding fields displayed by NifSkope.
