param(
    [switch] $Force,
    [string] $Tag
)

# Exit on any error
$ErrorActionPreference = 'Stop'

function Run-Git {
    param([string[]] $Args)

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "git"
    $psi.Arguments = [string]::Join(" ", $Args)
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdOut = $proc.StandardOutput.ReadToEnd()
    $stdErr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    return @{ ExitCode = $proc.ExitCode; StdOut = $stdOut; StdErr = $stdErr }
}

# Ensure git is available
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "git is not available on PATH."
    exit 2
}

# Resolve repo root
$result = Run-Git @("rev-parse", "--show-toplevel")
if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.StdOut)) {
    Write-Error "Not a git repository (cannot find repo root). git error: $($result.StdErr.Trim())"
    exit 2
}
$repoRoot = $result.StdOut.Trim()
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
$statusRes = Run-Git @("status", "--porcelain")
if ($statusRes.ExitCode -ne 0) {
    Write-Error "git status failed: $($statusRes.StdErr.Trim())"
    exit 4
}
$status = $statusRes.StdOut.Trim()
if ($status -and -not $Force) {
    Write-Error "Working tree not clean. Commit or stash changes, or rerun with -Force.`nChanges:`n$status"
    exit 3
}

# Check if tag already exists (local)
$revRes = Run-Git @("rev-parse", "--verify", "refs/tags/$Tag")
if ($revRes.ExitCode -eq 0) {
    Write-Host "Tag $Tag already exists locally."
} else {
    # Create annotated tag
    $tagRes = Run-Git @("tag", "-a", $Tag, "-m", "Release $Tag")
    if ($tagRes.ExitCode -ne 0) {
        Write-Error ("Failed to create tag {0}: {1}" -f $Tag, $tagRes.StdErr.Trim())
        exit 5
    }
    Write-Host "Created tag $Tag"
}

# Push tag to origin (robust handling)
$pushRes = Run-Git @("push", "origin", "refs/tags/$Tag")
if ($pushRes.ExitCode -ne 0) {
    Write-Error ("Failed to push tag {0} to origin (exit {1}): {2}" -f $Tag, $pushRes.ExitCode, $pushRes.StdErr.Trim())
    exit 6
}

if ($pushRes.StdOut -match 'Everything up-to-date' -or $pushRes.StdErr -match 'Everything up-to-date') {
    Write-Host "Tag $Tag already on remote (no-op)."
} else {
    Write-Host "Pushed tag $Tag to origin"
}

exit 0