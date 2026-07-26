# Changelog

## Unreleased

### Added
- Nothing yet.

### Changed
- Nothing yet.

### Fixed
- Nothing yet.

## Version 0.4.1

### Changed
- Added two-stage validation for the parent BA2 cache using archive metadata before content hashing.
- Cache metadata now records file length and UTC last-write timestamp ticks.
- Same-length archives with changed timestamps use XXH128 to distinguish metadata-only updates from content changes.
- Archive length changes are detected as stale immediately, without an unnecessary validation hash.
- Hashing for stale archives is deferred to the rebuild so each archive is read in a single pass.
- Cache validation now logs metadata decisions, hash activity, stored fingerprints, and rebuild timing.

### Fixed
- Parent archive entries with duplicate asset paths remain associated with each owning archive.

## Version 0.4.0

### Added
- Added schema-driven dependency parsing for Bethesda-family NIF files, with structured diagnostics and fallback handling for incomplete or unsupported files.
- Added typed extraction of Starfield `BSGeometry` external mesh paths, including extensionless and LOD mesh references that could previously be missed.
- Added support for Starfield Bethesda stream versions 170, 173, and 175.
- Added the `--preserve-parent-mat` option and a corresponding persisted Archive Manager checkbox, allowing parent-archive `.mat` files to be packaged without also copying their referenced parent textures.
- Added third-party attribution documentation and explicit package licensing metadata.
- Added release smoke tests that execute both published DMMDeps variants before packaging.

### Changed
- Migrated the active solution and release pipeline to .NET 10.
- Updated NuGet dependencies across the solution, including Avalonia 12 and current SQLite packages.
- Replaced the previous homegrown and legacy NIF handling with the maintained `niflysharp` library and its native runtime.
- Reworked NIF dependency discovery to prefer typed block and field information instead of relying primarily on printable-string scanning.
- Changed the project license to GPL-3.0-only.
- Updated framework-dependent publishing for .NET 10 and retained a self-contained release for users who do not have the required runtime installed.
- Disabled compression in self-contained single-file builds to reduce antivirus false positives.
- Normalized release artifacts and strengthened the GitHub Actions publishing process.
- Updated the Archive Manager interface and configuration handling for the new archive options.

### Fixed
- Fixed incomplete dependency manifests caused by earlier mesh-discovery logic missing extensionless Starfield `BSGeometry` and LOD mesh paths.
- Fixed publish failures caused by library projects being treated as executable publish targets.
- Fixed compatibility issues introduced by newer Avalonia drag-and-drop and placeholder APIs.
- Removed the vulnerable transitive SQLite native bundle by centrally pinning the corrected package version.
- Prevented previous generated BA2 outputs from being included in new archive manifests.
- Improved Archive2 reliability by removing existing output archives before creation and reporting locked output files clearly.