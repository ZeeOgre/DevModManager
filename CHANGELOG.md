# Changelog

## Unreleased

### Added
- Nothing yet.

### Changed
- Nothing yet.

### Fixed
- Nothing yet.

## Version 0.4.0

### Changed
- Added two-stage validation for the parent BA2 cache using archive metadata before content hashing.
- Cache metadata now records file length and UTC last-write timestamp ticks.
- Same-length archives with changed timestamps use XXH128 to distinguish metadata-only updates from content changes.
- Archive length changes are detected as stale immediately, without an unnecessary validation hash.
- Hashing for stale archives is deferred to the rebuild so each archive is read in a single pass.
- Cache validation now logs metadata decisions, hash activity, stored fingerprints, and rebuild timing.

### Fixed
- Parent archive entries with duplicate asset paths remain associated with each owning archive.
