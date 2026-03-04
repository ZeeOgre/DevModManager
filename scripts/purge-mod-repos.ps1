param(
    [Parameter(Mandatory = $true)]
    [string]$ModRepoRoot,

    [switch]$IncludeHidden,
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath($ModRepoRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Mod repo root not found: $root"
}

Write-Host "[purge-mod-repos] Root: $root"

if (-not $Force -and -not $DryRun) {
    throw "Refusing destructive run without -Force. Use -DryRun to preview."
}

$entries = Get-ChildItem -LiteralPath $root -Force | Where-Object {
    if ($_.Name -in @('.git', '.gitignore')) { return $false }
    if (-not $IncludeHidden -and $_.Name.StartsWith('.')) { return $false }
    return $true
}

if ($entries.Count -eq 0) {
    Write-Host "[purge-mod-repos] Nothing to remove."
    exit 0
}

foreach ($entry in $entries) {
    $path = $entry.FullName
    if ($DryRun) {
        Write-Host "[dry-run] Would remove: $path"
        continue
    }

    try {
        Remove-Item -LiteralPath $path -Recurse -Force
        Write-Host "[purge-mod-repos] Removed: $path"
    }
    catch {
        Write-Warning "Failed to remove '$path': $($_.Exception.Message)"
    }
}

Write-Host "[purge-mod-repos] Done."
