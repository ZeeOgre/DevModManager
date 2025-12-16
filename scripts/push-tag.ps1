param(
    [switch] $Force,
    [string] $Tag,
    [string] $RepoRoot
)

# Exit on any error
$ErrorActionPreference = 'Stop'

# Skip entirely on CI environments (explicit checks)
if ($env:GITHUB_ACTIONS -eq 'true' -or $env:CI -eq 'true' -or -not [string]::IsNullOrEmpty($env:TF_BUILD)) {
    Write-Host "CI detected (GITHUB_ACTIONS/CI/TF_BUILD). Skipping push-tag.ps1."
    exit 0
}

function Run-Git {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $Args
    )

    # Resolve git binary once
    try {
        $gitCmd = (Get-Command git -ErrorAction Stop).Source
    } catch {
        return @{ ExitCode = -1; StdOut = ""; StdErr = "git not found: $($_.Exception.Message)" }
    }

    if (-not $Args) { $Args = @() }

    Write-Host ("DEBUG: Run-Git args count={0}" -f $Args.Length)
    for ($i = 0; $i -lt $Args.Length; $i++) {
        Write-Host ("DEBUG: Arg[{0}] = '{1}'" -f $i, $Args[$i])
    }

    # Use Start-Process with redirected files to avoid ArgumentList/quoting/platform issues
    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = [System.IO.Path]::GetTempFileName()
    try {
        $proc = Start-Process -FilePath $gitCmd -ArgumentList $Args -RedirectStandardOutput $outFile -RedirectStandardError $errFile -NoNewWindow -PassThru -Wait
        $stdOut = ""
        $stdErr = ""
        try { $stdOut = Get-Content -Raw -LiteralPath $outFile -ErrorAction Stop } catch {}
        try { $stdErr = Get-Content -Raw -LiteralPath $errFile -ErrorAction Stop } catch {}

        # Parenthesize the pipeline expression so Join receives the resulting collection
        $argStr = [string]::Join(" ", ($Args | ForEach-Object { if ($_ -eq $null) { "" } else { $_ } } ))
        Write-Host "DEBUG: Running git -> `"$gitCmd`" $argStr"

        return @{ ExitCode = $proc.ExitCode; StdOut = $stdOut; StdErr = $stdErr }
    } catch {
        return @{ ExitCode = -1; StdOut = ""; StdErr = $_.Exception.Message }
    } finally {
        Remove-Item -LiteralPath $outFile -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $errFile -ErrorAction SilentlyContinue
    }
}

# Ensure git is available
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "git is not available on PATH."
    exit 2
}

# If RepoRoot was passed in, use it; otherwise resolve repo root
if ($RepoRoot) {
    if (-not (Test-Path $RepoRoot)) {
        Write-Error "Provided RepoRoot path does not exist: $RepoRoot"
        exit 2
    }
    Set-Location $RepoRoot
    $repoRoot = (Get-Location).ProviderPath
} else {
    $result = Run-Git @("rev-parse", "--show-toplevel")
    if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.StdOut)) {
        Write-Error "Not a git repository (cannot find repo root). git error: $($result.StdErr.Trim())"
        exit 2
    }
    $repoRoot = $result.StdOut.Trim()
    Set-Location $repoRoot
}

# Track tag source for diagnostics
$tagSource = "none"
$versionFilePath = $null

# If tag supplied on the command line, prefer it
if ($Tag) {
    $tagSource = "argument"
}

# If tag not supplied, try to find a Properties\version.txt anywhere in the repo (prefer project-level)
if (-not $Tag) {
    try {
        $vf = Get-ChildItem -Path $repoRoot -Filter version.txt -Recurse -ErrorAction SilentlyContinue |
              Where-Object { $_.FullName -imatch "\\Properties\\version\.txt$" } |
              Select-Object -First 1
    } catch {
        $vf = $null
    }

    if ($vf -and (Test-Path $vf.FullName)) {
        $versionFilePath = $vf.FullName
        $v = (Get-Content $versionFilePath -Raw).Trim()
        if ($v) {
            if ($v -match '^v') { $Tag = $v } else { $Tag = "v$v" }
            $tagSource = "version-file"
        }
    }
}

# Fallback tag if still empty
if (-not $Tag) {
    $Tag = ("v" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    $tagSource = "generated"
    Write-Host "No version file found; using generated tag: $Tag"
}

# Echo which source was used
switch ($tagSource) {
    "argument"       { Write-Host "Using tag '$Tag' (source: command-line argument)" }
    "version-file"   {
        Write-Host "Using tag '$Tag' (source: version file)"
        if ($versionFilePath) {
            Write-Host "Version file used: $versionFilePath"
            Write-Host "Version file contents: '$((Get-Content $versionFilePath -Raw).Trim())'"
        }
    }
    "generated"      { Write-Host "Using tag '$Tag' (source: generated)" }
    default          { Write-Host "Using tag '$Tag' (source: unknown)" }
}

# Ensure working tree is clean unless forced
$statusRes = Run-Git @("status", "--porcelain")

if ($null -eq $statusRes) {
    Write-Error "Internal error: Run-Git returned no result for 'git status'."
    exit 4
}

# Normalize fields safely (handle unexpected shapes)
$stdOut = if ($statusRes.ContainsKey('StdOut')) { $statusRes.StdOut } else { $statusRes.StdOut ?? "" }
$stdErr = if ($statusRes.ContainsKey('StdErr')) { $statusRes.StdErr } else { $statusRes.StdErr ?? "" }
$exitCode = if ($statusRes.ContainsKey('ExitCode')) { $statusRes.ExitCode } else { $statusRes.ExitCode ?? -1 }

if ($exitCode -ne 0) {
    $out = ($stdOut ?? "").Trim()
    $err = ($stdErr ?? "").Trim()
    Write-Error "git status failed (exit $exitCode). StdOut:`n$out`nStdErr:`n$err"
    exit 4
}

$status = ($stdOut ?? "").Trim()
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