# NIF dependency schema map

This map is the implementation contract for deterministic dependency extraction. It is derived from the vendored NifSkope schema at `_references/nifskope/build/nif.xml` and NifSkope's resource extraction implementation in `_references/nifskope/src/spells/fileextract.cpp`.

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

## Starfield priority map

| Block / field | Dependency kind | NifSkope schema location | Status |
| --- | --- | --- | --- |
| `BSGeometry` → `Meshes[4]` → `Mesh Path` | Mesh | `BSMeshArray` / `BSMesh` | Implemented: read four optional external `SizedString` paths. |
| `BSLightingShaderProperty` → `Material` | Material | `BSLayeredMaterial`, Starfield conditional | Pending typed decoder. |
| `BSEffectShaderProperty` → `Material` | Material | `BSLayeredMaterial`, Starfield conditional | Pending typed decoder. |
| Starfield shader/property texture fields | Texture | Starfield shader/material structs | Pending typed decoder. |
| `SkinAttach` → `Bones` | Rig / skeleton metadata | `SkinAttach` | Pending typed decoder. |
| `BoneTranslations` → `Bone Name` | Rig / skeleton metadata | `BoneTranslation` | Pending typed decoder. |
| `BSBehaviorGraphExtraData` → `Behaviour Graph File` | Animation / behavior | NifSkope schema | Pending typed decoder. |
| Havok and collision path-bearing blocks | Havok / collision | NifSkope Havok schema | Pending inventory and decoder. |
| Morph path-bearing blocks | Morph | NifSkope morph schema | Pending inventory and decoder. |

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
