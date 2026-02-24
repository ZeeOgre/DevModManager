# NIF deserialization strategy (block-aware, deterministic naming)

Current state in DMM is **partial parsing** (string extraction + token rewrite), not a full object deserializer.

## Goal
For readable mesh naming, avoid "nearby string" heuristics and prefer deterministic data:
1. Parse NIF header.
2. Read block-size table.
3. Scan each block in block boundaries.
4. Resolve block `Name` using explicit string table IDs.
5. Associate mesh paths to that same block.

This mirrors the intent used by tools like NifSkope/NiflySharp where `BSGeometry.Name` drives display naming.

## Contract used in code now
- `NifStructureScan`
  - `HeaderStrings`: header string table indexed by NIF string IDs.
  - `Blocks`: absolute byte spans for each serialized block.
- `NifBlockSpan`
  - `StartOffset` / `EndOffsetExclusive` for deterministic block boundaries.

The editor uses this contract to resolve mesh names from a block's first referenced header-string ID, rather than scanning backwards near mesh strings.

## Next step to reach full fidelity
Implement type-aware field deserialization for Bethesda blocks (e.g. `NiObjectNET -> Name`, `BSGeometry -> Meshes`), likely via generated field metadata (NifXML/NiflySharp-style), so `Name` and `Mesh Path` are read by schema instead of byte-pattern scanning.
