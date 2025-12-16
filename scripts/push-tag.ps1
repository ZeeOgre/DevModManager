param(
    [switch] $Force,
    [string] $Tag
)

# Exit on any error
$ErrorActionPreference = 'Stop'

# Resolve repo root
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    Write-Error "Not a git repository (cannot find repo root)."
    exit 2
}
Set-Location $repoRoot

# If tag not supplied, try to read Properties/version.txt
if (-not $Tag) {
    $versionFile = Join-Path $repoRoot "Properties\version.txt"
    if (Test-Path $versionFile) {
        $v = (Get-Content $versionFile -Raw).Trim()
        if ($v) {
            $Tag = "v$v"
        }
    }
}

# Fallback tag if still empty
if (-not $Tag) {
    $Tag = ("v" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    Write-Host "No version file found; using generated tag: $Tag"
}

# Ensure working tree is clean unless forced
$status = git status --porcelain
if ($status -and -not $Force) {
    Write-Error "Working tree not clean. Commit or stash changes, or rerun with -Force."
    exit 3
}

# Check if tag already exists locally or remote
if (git rev-parse --verify "refs/tags/$Tag" 2>$null) {
    Write-Host "Tag $Tag already exists locally."
    # don't fail; you may want to skip or push if not on remote
}

# Create annotated tag if not exists
if (-not (git rev-parse --verify "refs/tags/$Tag" 2>$null)) {
    git tag -a $Tag -m "Release $Tag"
    Write-Host "Created tag $Tag"
} else {
    Write-Host "Using existing tag $Tag"
}

# Push tag to origin
git push origin "refs/tags/$Tag"
Write-Host "Pushed tag $Tag to origin"

exit 0