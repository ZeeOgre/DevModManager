# dmmdeps parity check (legacy vs modular)

This document compares:
- `DMM.Standalone.DependencyChecker` (legacy `dmmdeps` behavior)
- `DMM.Standalone.DependencyChecker.Modular` (current modular CLI)
- `DMM.AssetManagers` extraction libraries

## Current state

### Already in modular and should be kept
- `nif-readablemesh` pipeline (NIF scan -> copy readable `.mesh` targets -> in-place token rewrite).
- `nif-dedupestrings` analysis mode.
- Shared NIF model/reader/editor/writer APIs in `DMM.AssetManagers.NIF`.

### Missing for feature parity with legacy `dmmdeps`
The modular CLI currently exposes only NIF-focused commands. Legacy `dmmdeps` still contains the full dependency graph build and packaging logic:

1. Plugin (`.esp/.esm`) printable-string scan for NIF/MAT/DDS/audio/terrain/misc tokens.
2. NIF expansion to MAT + mesh + rig + havok dependencies.
3. MAT expansion to texture tokens and filtering to existing custom assets.
4. Script expansion (`.pex` + `.psc`, including Papyrus import closure).
5. Voice/audio path normalization and Xbox mirror mapping.
6. TIF root discovery and texture source lookup.
7. Manifest creation and achlist generation.
8. `--smartclobber`, `--quiet`, `--silent`, root override handling.

## MAT decoder status

- `DMM.AssetManagers.MAT.MAT` was previously a stub.
- It now supports:
  - JSON parse when MAT content is valid.
  - recursive string-value traversal in JSON payloads.
  - fallback regex extraction for malformed/non-JSON MAT text.
  - extraction of `.dds`, `.png`, `.jpg`, `.jpeg` texture tokens.

No additional external MAT library is currently required to match legacy behavior, because legacy `dmmdeps` also primarily uses JSON + regex token discovery.

## Finishing touches to complete modularization

1. **Create a modular "dependency-scan" command** that reuses `DMM.AssetManagers` APIs and reproduces legacy outputs (`.dependencies.json` + `.achlist`).
2. **Move remaining logic out of legacy `Program.cs`** into library services:
   - path normalization / root inference
   - manifest assembly
   - xbox path mapping
   - file kind classification
3. **Wire existing library components**:
   - `TESFile` for plugin token extraction,
   - `NifReader` for NIF expansion,
   - `MAT` for texture extraction,
   - `Papyrus` for import traversal,
   - `Achlist` for output formatting.
4. **Preserve NIF editing commands as-is** in modular CLI (`nif-readablemesh`, `nif-dedupestrings`).
5. **Add parity tests**: golden-fixture comparison between legacy and modular outputs for a sample plugin tree.
6. **Keep legacy CLI temporarily** as fallback until output parity is validated (line-by-line diff of generated manifests).

## Suggested rollout order

1. Implement `dependency-scan` in modular CLI behind a dedicated subcommand.
2. Add fixture-based tests that compare generated `.dependencies.json` + `.achlist` against legacy output.
3. Once parity is stable, deprecate legacy entry point and keep modular as the canonical executable.
