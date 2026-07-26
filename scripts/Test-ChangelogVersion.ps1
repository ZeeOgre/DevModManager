[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$CurrentVersionPath,
    [Parameter(Mandatory)] [string]$ChangelogPath,
    [string]$BaseVersionPath,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$currentVersion = (Get-Content -LiteralPath $CurrentVersionPath -Raw).Trim()

if ($BaseVersionPath) {
    $baseVersion = (Get-Content -LiteralPath $BaseVersionPath -Raw).Trim()
    if ($currentVersion -eq $baseVersion) {
        Write-Host "DMMDeps version is unchanged at $currentVersion; no new changelog section is required."
        exit 0
    }
    Write-Host "DMMDeps version changed from $baseVersion to $currentVersion; validating release notes."
}

$extractor = Join-Path $PSScriptRoot 'Get-ReleaseNotes.ps1'
$global:LASTEXITCODE = 0
& $extractor -Version $currentVersion -ChangelogPath $ChangelogPath -OutputPath $OutputPath -ValidateOnly
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Validated non-empty CHANGELOG.md release notes for DMMDeps $currentVersion."
