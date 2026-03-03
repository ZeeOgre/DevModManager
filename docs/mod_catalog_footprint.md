# Master Archive Catalog Sizing Notes

## SQLite storage and compression

- SQLite stores row data in B-Tree pages in the database file; text is not automatically gzip/zip compressed.
- SQLite can reduce duplicate overhead through page layout and indices, but highly-compressible text remains effectively stored as raw content unless you explicitly compress payloads before insert.
- Therefore, for large preseed catalogs, shipping a compressed SQL/script artifact and importing it at install/setup time is usually much smaller on disk/in transit than committing an expanded SQL file.

## Recommended preseed strategy

1. Produce a dedicated artifact such as `game_base_files.sql.gz` (or `.zip`).
2. On first-run seed:
   - extract to temp location,
   - execute import in a transaction,
   - delete extracted SQL.
3. Optionally run `VACUUM` once after seed to compact pages.

## Runtime instrumentation in DMM

`ModDependencyDiscoveryResult` now captures parent catalog sizing fields:

- `ParentMasterCount`
- `ParentArchiveCount`
- `ParentIndexedFileCount`
- `ParentIndexedBytes`

These are surfaced in scan-apply status messages in `MainWindow` so you can observe the magnitude of the catalog while testing mods with many masters.
