param(
    [switch] $Force,
    [string] $Tag
)

# Exit on any error
$ErrorActionPreference = 'Stop'

# Ensure git is available
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "git is not available on PATH."
    exit 2
}

# Resolve repo root
$repoRoot = (& git rev-parse --show-toplevel) 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    Write-Error "Not a git repository (cannot find repo root)."
    exit 2
}
$repoRoot = $repoRoot.Trim()
Set-Location $repoRoot

# If tag not supplied, try to find a Properties\version.txt anywhere in the repo (prefer project-level)
if (-not $Tag) {
    $versionFile = $null

    try {
        $versionFile = Get-ChildItem -Path $repoRoot -Filter version.txt -Recurse -ErrorAction SilentlyContinue |
                       Where-Object { $_.FullName -imatch "\\Properties\\version\.txt$" } |
                       Select-Object -First 1
    } catch {
        # ignore
    }

    if ($versionFile -and (Test-Path $versionFile.FullName)) {
        $v = (Get-Content $versionFile.FullName -Raw).Trim()
        if ($v) {
            # ensure tag begins with 'v'
            if ($v -match '^v') { $Tag = $v } else { $Tag = "v$v" }
        }
    }
}

# Fallback tag if still empty
if (-not $Tag) {
    $Tag = ("v" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    Write-Host "No version file found; using generated tag: $Tag"
}

# Ensure working tree is clean unless forced
$status = (& git status --porcelain) 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error "git status failed."
    exit 4
}
if ($status -and -not $Force) {
    Write-Error "Working tree not clean. Commit or stash changes, or rerun with -Force."
    exit 3
}

# Check if tag already exists (local)
$null = (& git rev-parse --verify "refs/tags/$Tag") 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Tag $Tag already exists locally."
} else {
    # Create annotated tag
    $createOutput = & git tag -a $Tag -m "Release $Tag" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error ("Failed to create tag {0}: {1}" -f $Tag, $createOutput)
        exit 5
    }
    Write-Host "Created tag $Tag"
}

# Push tag to origin
$pushOutput = & git push origin "refs/tags/$Tag" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error ("Failed to push tag {0} to origin: {1}" -f $Tag, $pushOutput)
    exit 6
}
Write-Host "Pushed tag $Tag to origin"

exit 0