# Tests

This repo uses automated tests (xUnit) plus local-only plugin fixtures.

## Fixture folders

- `tests/fixtures/`  
  Committed documentation and (optionally) small synthetic test data.

- `tests/fixtures_local/`  
  Local-only binaries (ignored by git). Put real `.esm/.esp` files here for smoke/integration tests.

## Recommended local layout

- `tests/fixtures_local/tes/DebugMenuFramework.esm`
- `tests/fixtures_local/tes/DMF_ZEO.esm`

Tests that require local fixtures should skip gracefully if the files are missing.