[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [string]$ChangelogPath,

    [string]$OutputPath,

    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'

try {
    if (-not (Test-Path -LiteralPath $ChangelogPath -PathType Leaf)) {
        throw "Changelog file was not found: $ChangelogPath"
    }

    $markdown = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $ChangelogPath))
    $escapedVersion = [Regex]::Escape($Version)
    $headingPattern = "(?m)^##[ \t]+(?:Version[ \t]+$escapedVersion|\[$escapedVersion\])[ \t]*\r?$"
    $heading = [Regex]::Match($markdown, $headingPattern)

    if (-not $heading.Success) {
        throw "DMMDeps version.txt is $Version, but CHANGELOG.md does not contain a non-empty `"## Version $Version`" section. Add release notes before running FullRelease."
    }

    $contentStart = $heading.Index + $heading.Length
    $nextHeading = [Regex]::Match($markdown.Substring($contentStart), '(?m)^##[ \t]+.+\r?$')
    $contentLength = if ($nextHeading.Success) { $nextHeading.Index } else { $markdown.Length - $contentStart }
    $notes = $markdown.Substring($contentStart, $contentLength).Trim()

    # Ignore Markdown decoration when deciding whether notes are meaningful.
    $meaningfulLines = $notes -split '\r?\n' | Where-Object {
        $_ -notmatch '^\s{0,3}#{1,6}(?:[ \t]|$)'
    } | ForEach-Object {
        ($_ -replace '^\s{0,3}(?:#{1,6}|[-*+]|>)[ \t]*', '').Trim()
    } | Where-Object {
        $_ -and $_ -notmatch '^(?i:nothing yet|none|n/?a|tbd|todo|placeholder|coming soon)[.!]?$'
    }

    if (-not $notes -or -not $meaningfulLines) {
        throw "DMMDeps version.txt is $Version, but CHANGELOG.md does not contain a non-empty `"## Version $Version`" section. Add release notes before running FullRelease."
    }

    if ($OutputPath) {
        $parent = Split-Path -Parent $OutputPath
        if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        [System.IO.File]::WriteAllText($OutputPath, $notes + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    }

    if (-not $ValidateOnly) { $notes }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
