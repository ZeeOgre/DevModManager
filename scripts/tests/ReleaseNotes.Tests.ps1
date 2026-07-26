$ErrorActionPreference = 'Stop'
$extractor = Join-Path $PSScriptRoot '..\Get-ReleaseNotes.ps1'
$validator = Join-Path $PSScriptRoot '..\Test-ChangelogVersion.ps1'
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("dmm-release-notes-" + [guid]::NewGuid())
New-Item -ItemType Directory $temp | Out-Null

function Assert-Equal([string]$Expected, [string]$Actual, [string]$Message) {
    if ($Expected -cne $Actual) { throw "$Message`nExpected: <$Expected>`nActual: <$Actual>" }
}

function Assert-ScriptFails([string]$Path, [string[]]$Arguments, [string]$Message) {
    & (Join-Path $PSHOME 'pwsh') -NoProfile -File $Path @Arguments 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { throw $Message }
}

try {
    $changelog = Join-Path $temp 'CHANGELOG.md'
    @'
# Changelog
## Version 1.2.3
### Added
- **Preserved** `Markdown`.

Paragraph two.
## Version 1.2.2
- Older notes must not be returned.
'@ | Set-Content -LiteralPath $changelog -NoNewline

    $actual = (& $extractor -Version 1.2.3 -ChangelogPath $changelog) -join [Environment]::NewLine
    Assert-Equal "### Added$([Environment]::NewLine)- **Preserved** ``Markdown``.$([Environment]::NewLine)$([Environment]::NewLine)Paragraph two." $actual 'Extraction must preserve Markdown and stop at the next level-two heading.'

    "## [2.0.0]`n- Alternate heading works." | Set-Content $changelog -NoNewline
    Assert-Equal '- Alternate heading works.' ((& $extractor -Version 2.0.0 -ChangelogPath $changelog) -join '') 'Bracketed version heading was not recognized.'

    Assert-ScriptFails $extractor @('-Version', '9.9.9', '-ChangelogPath', $changelog) 'A missing section should fail.'
    "## Version 2.0.0`n`n## Version 1.0.0`n- Notes" | Set-Content $changelog
    Assert-ScriptFails $extractor @('-Version', '2.0.0', '-ChangelogPath', $changelog) 'An empty section should fail.'
    "## Version 2.0.0`n### Changed`n- TBD" | Set-Content $changelog
    Assert-ScriptFails $extractor @('-Version', '2.0.0', '-ChangelogPath', $changelog) 'A placeholder-only section should fail.'

    $current = Join-Path $temp 'current.txt'; $base = Join-Path $temp 'base.txt'
    '3.0.0' | Set-Content $current; '3.0.0' | Set-Content $base
    & $validator -CurrentVersionPath $current -BaseVersionPath $base -ChangelogPath $changelog
    if ($LASTEXITCODE -ne 0) { throw 'An unchanged PR version should not require notes.' }

    '2.9.0' | Set-Content $base
    Assert-ScriptFails $validator @('-CurrentVersionPath', $current, '-BaseVersionPath', $base, '-ChangelogPath', $changelog) 'A changed PR version should require matching notes.'
    "## Version 3.0.0`n- Meaningful release note." | Set-Content $changelog
    & $validator -CurrentVersionPath $current -BaseVersionPath $base -ChangelogPath $changelog
    if ($LASTEXITCODE -ne 0) { throw 'A changed PR version with matching notes should pass.' }

    Write-Host 'All release-note tests passed.'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
