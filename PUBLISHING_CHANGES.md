# Publishing Changes Summary

## Issues Fixed

### 1. Self-Contained vs Framework-Dependent Build Errors (NETSDK1151)

**Problem**: When publishing with `PublishSingleFile=true` for framework-dependent builds with project references, .NET was incorrectly treating referenced library projects (DMM.AssetManagers, DMM.Core) as self-contained executables, causing the NETSDK1151 error.

**Root Cause**: Framework-dependent single-file publishing with project references is not supported in .NET when using the standard project reference mechanism. When `PublishSingleFile=true` is specified without `SelfContained=true`, the build system still infers a runtime identifier and attempts to build all referenced projects as if they were executables.

**Solution**: 
- **Framework-dependent build** (`dmmdeps.exe`): Now publishes as a multi-file deployment (exe + DLLs) instead of a single file. This is the correct approach for framework-dependent builds with project references.
- **Self-contained build** (`dmmdeps-fat.exe`): Continues to use single-file publishing with all dependencies embedded.
- Added `IsPublishable=false` to library projects (DMM.Core, DMM.AssetManagers, DMM.Data) to prevent them from being treated as publishable executables.

### 2. GitHub Actions Workflow Fixes

**Problem**: The workflow was looking for incorrectly named executables and wasn't handling the multi-file framework-dependent output properly.

**Solution**:
- Updated framework-dependent build to NOT use `PublishSingleFile`
- Updated the "Normalize names and prepare zips" step to:
  - Rename `DMM.Standalone.DependencyChecker.exe` to `dmmdeps.exe` for framework-dependent
  - Rename `DMM.Standalone.DependencyChecker.exe` to `dmmdeps-fat.exe` for self-contained
  - Zip all files (exe + DLLs) for `dmmdeps.zip`
  - Zip only the single executable for `dmmdeps-fat.zip`

## Build Outputs

### Framework-Dependent (`dmmdeps.zip` - ~2 MB)
- **Contains**: `dmmdeps.exe` + all required DLLs
- **Requires**: .NET 9 Runtime installed on the target machine
- **Use case**: Development, CI/CD, or users who already have .NET 9 installed

### Self-Contained (`dmmdeps-fat.zip` - ~13 MB)
- **Contains**: Single `dmmdeps-fat.exe` file with embedded .NET runtime
- **Requires**: Nothing - completely standalone
- **Use case**: End users, distribution where .NET may not be installed

## Local Development

### Building Locally

**Standard Release Build** (no publishing):
```powershell
dotnet build DMM.Standalone.DependencyChecker\DMM.Standalone.DependencyChecker.csproj -c Release
```

**Publish Both Versions** (triggers on FullRelease configuration):
```powershell
dotnet build DMM.Standalone.DependencyChecker\DMM.Standalone.DependencyChecker.csproj -c FullRelease /p:EnableLocalReleaseTrigger=false
```

Outputs will be in: `DMM.Standalone.DependencyChecker\dist\`
- `dist\dmmdeps\` - framework-dependent (multiple files)
- `dist\dmmdeps_fat.exe` - self-contained (single file)

### Testing the Builds

**Framework-Dependent**:
```powershell
.\DMM.Standalone.DependencyChecker\dist\dmmdeps\dmmdeps.exe "path\to\your\mod.esm"
```

**Self-Contained**:
```powershell
.\DMM.Standalone.DependencyChecker\dist\dmmdeps_fat.exe "path\to\your\mod.esm"
```

## CI/CD (GitHub Actions)

### Trigger
The workflow runs on:
- Tag push matching `v*` pattern (e.g., `v0.2.90`)
- Manual workflow dispatch

### Process
1. **Restore** dependencies
2. **Publish** both versions:
   - Framework-dependent to `./out/dmmdeps`
   - Self-contained to `./out/dmmdeps-fat`
3. **Normalize** executable names
4. **Create** zip archives:
   - `dmmdeps.zip` - all framework-dependent files
   - `dmmdeps-fat.zip` - single self-contained executable
5. **Upload** as workflow artifacts
6. **Create/Update** GitHub Release with both zip files attached

### Testing Workflow Locally

You can test the workflow commands locally:

```powershell
# Framework-dependent
dotnet publish DMM.Standalone.DependencyChecker\DMM.Standalone.DependencyChecker.csproj -c Release /p:SelfContained=false /p:DebugType=None /p:IncludeSymbols=false /p:OutputType=Exe /p:SkipNestedPublish=true /p:EnableLocalReleaseTrigger=false -o .\out\dmmdeps

# Self-contained  
dotnet publish DMM.Standalone.DependencyChecker\DMM.Standalone.DependencyChecker.csproj -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=true /p:DebugType=None /p:IncludeSymbols=false /p:OutputType=Exe /p:EnableCompressionInSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:IncludeAllContentForSelfExtract=true /p:SkipNestedPublish=true /p:EnableLocalReleaseTrigger=false -o .\out\dmmdeps-fat
```

## Key Configuration Changes

### DMM.Standalone.DependencyChecker.csproj
- Removed `PublishSingleFile=true` from framework-dependent publish command
- Added explicit `SelfContained=false` to framework-dependent configuration
- **Removed `/p:OutputType=Exe` parameter from publish commands** - this was overriding the library projects' `OutputType=Library` setting
- Updated copy task to copy all files for framework-dependent build
- Updated size estimates in messages

### Library Projects (DMM.Core, DMM.AssetManagers, DMM.Data)
- Added `<IsPublishable>false</IsPublishable>` to prevent them from being treated as executables
- **Added `<OutputType>Library</OutputType>` to explicitly mark them as libraries** - critical for preventing CS5001 errors during publish

### .github/workflows/publish.yml
- **Removed `/p:OutputType=Exe` parameter from both publish commands** - this was causing library projects to be built as executables
- Removed `--no-self-contained` and `-r` flags from framework-dependent build
- Updated normalization step to handle multi-file framework-dependent output
- Updated release notes to reflect actual sizes

## Future Improvements

### Option 1: DLL Auto-Installation for dmmdeps.exe
To make `dmmdeps.exe` install missing DLLs automatically, you could:
1. Create a launcher stub that checks for .NET 9 runtime
2. If not found, download and install it (requires admin rights)
3. Then launch the actual application

### Option 2: ILMerge for True Single-File Framework-Dependent
Use ILMerge or similar tool post-publish to merge all DLLs into a single executable. However, this is complex and may have compatibility issues with SQLite native libraries.

### Option 3: Keep Current Approach
The current approach is the recommended Microsoft pattern:
- Lightweight version requires .NET installed (common for developers)
- Fat version is completely standalone (best for end users)

## Verification Steps

1. ✅ Build compiles without NETSDK1151 errors
2. ✅ Framework-dependent publish creates multi-file output
3. ✅ Self-contained publish creates single-file output (~13 MB)
4. ✅ Both versions run correctly
5. ⏳ GitHub Actions workflow produces correct artifacts (needs testing)
6. ⏳ Release notes accurately reflect download sizes (needs verification)

## Next Steps

1. Test a complete FullRelease build locally
2. Push a test tag to verify the GitHub Actions workflow
3. Verify the generated zip files contain the correct contents
4. Test downloads and execution on a clean machine (without .NET installed for fat version)
