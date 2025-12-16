param(
  [switch] $IncrementPatch,   # true -> increment patch number in found version.txt
  [string] $Version,          # explicit version to set (e.g. 0.2.1). If provided, takes precedence.
  [switch] $NoTag,            # don't call push-tag.ps1 (just commit the version bump)
  [switch] $Force             # bypass checks (use with care)
)

$ErrorActionPreference = 'Stop'

function Find-VersionFile {
  $root = (git rev-parse --show-toplevel 2>$null).Trim()
  if (-not $root) { throw "Not inside a git repository." }
  $vf = Get-ChildItem -Path $root -Filter version.txt -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -imatch "\\Properties\\version\.txt$" } |
        Select-Object -First 1
  if (-not $vf) { throw "version.txt not found under a Properties folder." }
  return $vf.FullName
}

function Parse-Version($v) {
  if ($v -match '^\s*v?(\d+)\.(\d+)\.(\d+)\s*$') {
    return @{ Major=[int]$Matches[1]; Minor=[int]$Matches[2]; Patch=[int]$Matches[3] }
  }
  throw "version.txt contains unexpected format: '$v' (expected: MAJOR.MINOR.PATCH)"
}

# Find file
$versionFile = Find-VersionFile
Write-Host "Using version file: $versionFile"

# Read current
$current = (Get-Content $versionFile -Raw).Trim()
Write-Host "Current version: $current"

# Determine new version
if ($Version) {
  $new = $Version
} elseif ($IncrementPatch) {
  $parts = Parse-Version $current
  $parts.Patch = $parts.Patch + 1
  $new = "$($parts.Major).$($parts.Minor).$($parts.Patch)"
} else {
  throw "Provide -IncrementPatch or -Version <x.y.z>."
}

# Normalize to not include leading 'v'
$newNormalized = $new.TrimStart('v')
Write-Host "New version: $newNormalized"

# Ensure working tree clean unless forced
$status = git status --porcelain
if ($status -and -not $Force) {
  Write-Error "Working tree not clean. Commit or stash changes, or pass -Force. Changes:`n$status"
  exit 3
}

# Write file
Set-Content -Path $versionFile -Value $newNormalized -Encoding UTF8
git add $versionFile
git commit -m "Bump dmmdeps to $newNormalized"
git push origin HEAD

# Tag & push (reuse push-tag.ps1)
if (-not $NoTag) {
  $tagName = "v$newNormalized"
  Write-Host "Invoking push-tag.ps1 to create & push tag $tagName"
  powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\push-tag.ps1" -Tag $tagName
}

Write-Host "Done. New version: $newNormalized"