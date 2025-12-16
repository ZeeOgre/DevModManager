param(
    [switch] $Force,
    [switch] $AutoCommit,
    [string] $Tag,
    [string] $RepoRoot
)

# Determine repo root path in a way compatible with Windows PowerShell (no inline if-expression)
if ($RepoRoot) {
    $rootPath = $RepoRoot
} else {
    $rootPath = (Get-Location).ProviderPath
}

# Create a persistent debug log file in the repo root so MSBuild/IDE runs always produce diagnostics
$dbgFile = Join-Path -Path $rootPath -ChildPath ("push-tag-debug-{0}-pid{1}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $PID)
"---- push-tag debug start: $(Get-Date) ----" | Out-File -FilePath $dbgFile -Encoding utf8
"Args: Force=$Force AutoCommit=$AutoCommit Tag=$Tag RepoRoot=$RepoRoot" | Out-File -FilePath $dbgFile -Append -Encoding utf8
"User: $(whoami)" | Out-File -FilePath $dbgFile -Append -Encoding utf8
"Pwd: $(Get-Location).ProviderPath" | Out-File -FilePath $dbgFile -Append -Encoding utf8
"PowerShell: $($PSVersionTable.PSVersion)" | Out-File -FilePath $dbgFile -Append -Encoding utf8
"PATH: $env:PATH" | Out-File -FilePath $dbgFile -Append -Encoding utf8

# Exit on any error
$ErrorActionPreference = 'Stop'

# Skip entirely on CI environments (explicit checks)
if ($env:GITHUB_ACTIONS -eq 'true' -or $env:CI -eq 'true' -or -not [string]::IsNullOrEmpty($env:TF_BUILD)) {
    Write-Host "CI detected (GITHUB_ACTIONS/CI/TF_BUILD). Skipping push-tag.ps1."
    "Skipping: CI detected" | Out-File -FilePath $dbgFile -Append -Encoding utf8
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
        $msg = "git not found: $($_.Exception.Message)"
        $res = @{ ExitCode = -1; StdOut = ""; StdErr = $msg }
        $res | ConvertTo-Json | Out-File -FilePath $dbgFile -Append -Encoding utf8
        return $res
    }

    if (-not $Args) { $Args = @() }

    Write-Host "DEBUG: Run-Git args count=$($Args.Length)"
    for ($i = 0; $i -lt $Args.Length; $i++) {
        $argVal = if ($null -eq $Args[$i]) { '' } else { $Args[$i] }
        Write-Host "DEBUG: Arg[$i] = '$argVal'"
    }

    try {
        # Invoke git directly, capture combined stdout/stderr and exit code
        $output = & $gitCmd @Args 2>&1
        $exit = $LASTEXITCODE
        $stdOut = ""
        $stdErr = ""
        if ($output) { $stdOut = ($output -join "`n") }

        # Persist diagnostics
        $argStr = [string]::Join(" ", ($Args | ForEach-Object { if ($_ -eq $null) { "" } else { $_ } } ))
        "Run-Git: $gitCmd $argStr" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        "ExitCode: $exit" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        "StdOut:`n$stdOut" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        "StdErr:`n$stdErr" | Out-File -FilePath $dbgFile -Append -Encoding utf8

        return @{ ExitCode = $exit; StdOut = $stdOut; StdErr = $stdErr }
    } catch {
        $errMsg = $_.Exception.Message
        "Run-Git error: $errMsg" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        return @{ ExitCode = -1; StdOut = ""; StdErr = $errMsg }
    }
}

# Ensure git is available
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "git is not available on PATH."
    "git not available on PATH" | Out-File -FilePath $dbgFile -Append -Encoding utf8
    exit 2
}

# If RepoRoot was passed in, use it; otherwise resolve repo root
if ($RepoRoot) {
    if (-not (Test-Path $RepoRoot)) {
        Write-Error "Provided RepoRoot path does not exist: $RepoRoot"
        "RepoRoot path not found: $RepoRoot" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        exit 2
    }
    Set-Location $RepoRoot
    $repoRoot = (Get-Location).ProviderPath
} else {
    $result = Run-Git "rev-parse" "--show-toplevel"
    if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.StdOut)) {
        Write-Error "Not a git repository (cannot find repo root). git error: $($result.StdErr.Trim())"
        "Not a git repo: $($result.StdErr)" | Out-File -FilePath $dbgFile -Append -Encoding utf8
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
    "Generated tag: $Tag" | Out-File -FilePath $dbgFile -Append -Encoding utf8
}

# Echo which source was used
switch ($tagSource) {
    "argument"       { Write-Host "Using tag '$Tag' (source: command-line argument)" }
    "version-file"   {
        Write-Host "Using tag '$Tag' (source: version file)"
        if ($versionFilePath) {
            Write-Host "Version file used: $versionFilePath"
            Write-Host "Version file contents: '$((Get-Content $versionFilePath -Raw).Trim())'"
            "Version file used: $versionFilePath" | Out-File -FilePath $dbgFile -Append -Encoding utf8
            "Version file contents: '$((Get-Content $versionFilePath -Raw).Trim())'" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        }
    }
    "generated"      { Write-Host "Using tag '$Tag' (source: generated)" }
    default          { Write-Host "Using tag '$Tag' (source: unknown)" }
}

# Ensure working tree is clean unless forced
$statusRes = Run-Git "status" "--porcelain"

if ($null -eq $statusRes) {
    Write-Error "Internal error: Run-Git returned no result for 'git status'."
    "Run-Git returned null for status" | Out-File -FilePath $dbgFile -Append -Encoding utf8
    exit 4
}

# Normalize fields safely (handle unexpected shapes)
$stdOut = ""
if ($statusRes -and $statusRes.ContainsKey('StdOut') -and $statusRes.StdOut) {
    $stdOut = $statusRes.StdOut
}

$stdErr = ""
if ($statusRes -and $statusRes.ContainsKey('StdErr') -and $statusRes.StdErr) {
    $stdErr = $statusRes.StdErr
}

$exitCode = -1
if ($statusRes -and $statusRes.ContainsKey('ExitCode') -and $statusRes.ExitCode -ne $null) {
    $exitCode = $statusRes.ExitCode
}

if ($exitCode -ne 0) {
    $out = ""
    if ($stdOut) { $out = $stdOut.Trim() }
    $err = ""
    if ($stdErr) { $err = $stdErr.Trim() }
    Write-Error "git status failed (exit $exitCode). StdOut:`n$out`nStdErr:`n$err"
    "git status failed (exit $exitCode). StdOut:`n$out`nStdErr:`n$err" | Out-File -FilePath $dbgFile -Append -Encoding utf8
    exit 4
}

$status = ""
if ($stdOut) { $status = $stdOut.Trim() }
if ($status -and -not $Force) {
    # If AutoCommit is requested, only allow if changed files are within an approved whitelist
    if ($AutoCommit) {
        # Extract changed paths from porcelain output
        $changed = @()
        foreach ($line in ($stdOut -split "`n")) {
            $l = $line.Trim()
            if (-not $l) { continue }
            if ($l -match '^[\s\S]{0,}\s{1,}(.+)$') { $p = $matches[1].Trim() } else { $p = $l }
            $changed += $p
        }

        # allowed patterns (relative paths). Modify as needed.
        $allowed = @(
            'DMM.Standalone.DependencyChecker/*',
            'DMM.Installer/*',
            'scripts/*',
            'Properties/*',
            'TriggerRelease.targets'  # allow committing the targets file when we auto-bump
        )

        $unsafe = @()
        foreach ($f in $changed) {
            $ok = $false
            foreach ($pat in $allowed) {
                if ($f -like $pat) { $ok = $true; break }
            }
            if (-not $ok) { $unsafe += $f }
        }

        if ($unsafe.Count -gt 0) {
            Write-Error "AutoCommit refused: changed files outside allowed paths:`n$($unsafe -join "`n")"
            "AutoCommit refused: $($unsafe -join ', ')" | Out-File -FilePath $dbgFile -Append -Encoding utf8
            exit 3
        }

        # Stage and commit
        "AutoCommit: staging changed files: $($changed -join ', ')" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        $addRes = Run-Git "add" "--all"
        if ($addRes.ExitCode -ne 0) {
            Write-Error "git add failed: $($addRes.StdErr)"
            "git add failed: $($addRes.StdErr)" | Out-File -FilePath $dbgFile -Append -Encoding utf8
            exit 3
        }
        $commitMsg = "Auto-commit changes for FullRelease by push-tag.ps1"
        $commitRes = Run-Git "commit" "-m" $commitMsg
        if ($commitRes.ExitCode -ne 0) {
            Write-Error "git commit failed: $($commitRes.StdErr)"
            "git commit failed: $($commitRes.StdErr)" | Out-File -FilePath $dbgFile -Append -Encoding utf8
            exit 3
        }
        "AutoCommit: commit created" | Out-File -FilePath $dbgFile -Append -Encoding utf8

    } else {
        Write-Error "Working tree not clean. Commit or stash changes, or rerun with -Force.`nChanges:`n$status"
        "Working tree not clean. Changes:`n$status" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        exit 3
    }
}

# Check if tag already exists (local)
$revRes = Run-Git "rev-parse" "--verify" "refs/tags/$Tag"
if ($revRes.ExitCode -eq 0) {
    Write-Host "Tag $Tag already exists locally."
    "Tag $Tag already exists locally" | Out-File -FilePath $dbgFile -Append -Encoding utf8

    # If tag exists, try to bump patch and create a new tag if allowed
    if ($AutoCommit -or $Force) {
        if (-not $versionFilePath) {
            Write-Error "Cannot auto-bump version: no version.txt found to update."
            "Auto-bump refused: no version file found" | Out-File -FilePath $dbgFile -Append -Encoding utf8
            exit 5
        }

        # Read and parse current version
        $curText = (Get-Content $versionFilePath -Raw).Trim()
        $m = [System.Text.RegularExpressions.Regex]::Match($curText, "^v?(\d+)\.(\d+)\.(\d+)$")
        if (-not $m.Success) {
            Write-Error "version.txt contains unexpected format: '$curText' (expected MAJOR.MINOR.PATCH)"
            "Auto-bump refused: version file format unexpected: $curText" | Out-File -FilePath $dbgFile -Append -Encoding utf8
            exit 5
        }
        $major = [int]$m.Groups[1].Value
        $minor = [int]$m.Groups[2].Value
        $patch = [int]$m.Groups[3].Value
        $patch++
        $newVer = "{0}.{1}.{2}" -f $major, $minor, $patch

        # Write new version (no leading v in file)
        Set-Content -Path $versionFilePath -Value $newVer -Encoding UTF8
        "Auto-bump: version.txt updated to $newVer" | Out-File -FilePath $dbgFile -Append -Encoding utf8

        # Stage and commit the updated version file
        $addRes = Run-Git "add" "$versionFilePath"
        if ($addRes.ExitCode -ne 0) {
            Write-Error "git add failed for version file: $($addRes.StdErr)"
            "git add failed for version file: $($addRes.StdErr)" | Out-File -FilePath $dbgFile -Append -Encoding utf8
            exit 5
        }
        $commitMsg = "Auto-bump version to $newVer for release"
        $commitRes = Run-Git "commit" "-m" $commitMsg
        if ($commitRes.ExitCode -ne 0) {
            Write-Error "git commit failed for version bump: $($commitRes.StdErr)"
            "git commit failed for version bump: $($commitRes.StdErr)" | Out-File -FilePath $dbgFile -Append -Encoding utf8
            exit 5
        }
        "Auto-bump: committed version bump to $newVer" | Out-File -FilePath $dbgFile -Append -Encoding utf8

        # record commit id
        $shaRes = Run-Git "rev-parse" "HEAD"
        $commitSha = $null
        if ($shaRes -and $shaRes.ContainsKey('StdOut')) { $commitSha = $shaRes.StdOut.Trim() }
        "Auto-bump commit SHA: $commitSha" | Out-File -FilePath $dbgFile -Append -Encoding utf8

        # Update tag name and continue
        if ($newVer -match '^v') { $Tag = $newVer } else { $Tag = "v$newVer" }
        Write-Host "Bumped tag to $Tag and will create new tag"
        "Bumped tag to $Tag and will create new tag" | Out-File -FilePath $dbgFile -Append -Encoding utf8
    } else {
        Write-Host "Tag $Tag exists and AutoCommit is not enabled; skipping tag creation."
        "Tag exists and no AutoCommit: skipping" | Out-File -FilePath $dbgFile -Append -Encoding utf8
        exit 0
    }
} else {
    # tag does not exist; proceed as before
}

# Create annotated tag
"Attempting to create annotated tag: $Tag" | Out-File -FilePath $dbgFile -Append -Encoding utf8
$tagRes = Run-Git "tag" "-a" $Tag "-m" "Release $Tag"
if ($tagRes.ExitCode -ne 0) {
    Write-Error (("Failed to create tag {0}: {1}" -f $Tag, $tagRes.StdErr.Trim()))
    "Failed to create tag ${Tag}: $($tagRes.StdErr)" | Out-File -FilePath $dbgFile -Append -Encoding utf8
    exit 5
}
Write-Host "Created tag $Tag"
"Created tag $Tag" | Out-File -FilePath $dbgFile -Append -Encoding utf8

# Push tag to origin (robust handling)
"Attempting to push tag $Tag to origin" | Out-File -FilePath $dbgFile -Append -Encoding utf8
$pushRes = Run-Git "push" "origin" "refs/tags/$Tag"

# Consider 'Everything up-to-date' a success (sometimes git prints it without zero exit code)
$alreadyUpToDate = $false
if ($pushRes -and $pushRes.ContainsKey('StdOut') -and $pushRes.StdOut) {
    if ($pushRes.StdOut -match 'Everything up-to-date') { $alreadyUpToDate = $true }
}
if ($pushRes -and $pushRes.ContainsKey('StdErr') -and $pushRes.StdErr) {
    if ($pushRes.StdErr -match 'Everything up-to-date') { $alreadyUpToDate = $true }
}

if ($pushRes.ExitCode -ne 0 -and -not $alreadyUpToDate) {
    "Push failed for tag $Tag. ExitCode: $($pushRes.ExitCode). StdErr: $($pushRes.StdErr)" | Out-File -FilePath $dbgFile -Append -Encoding utf8

    # Defensive check: sometimes remote reports messages (redirects) but the tag still exists on the remote.
    "Checking remote for tag refs/tags/$Tag to verify push result" | Out-File -FilePath $dbgFile -Append -Encoding utf8
    $lsRes = Run-Git "ls-remote" "origin" "refs/tags/$Tag"
    $remoteHasTag = $false
    if ($lsRes -and $lsRes.ContainsKey('StdOut') -and -not [string]::IsNullOrWhiteSpace($lsRes.StdOut)) {
        if ($lsRes.StdOut -match 'refs/tags/') { $remoteHasTag = $true }
    }

    if ($remoteHasTag) {
        Write-Host "Push reported error but tag refs/tags/$Tag exists on remote — treating as success."
        "Push reported error but tag exists on remote; treating as success" | Out-File -FilePath $dbgFile -Append -Encoding utf8
    } else {
        "Push did not succeed and tag not found on remote; failing." | Out-File -FilePath $dbgFile -Append -Encoding utf8
        Write-Error (("Failed to push tag {0} to origin (exit {1}): {2}" -f $Tag, $pushRes.ExitCode, $pushRes.StdErr.Trim()))
        exit 6
    }
}

if ($alreadyUpToDate) {
    Write-Host "Tag $Tag already on remote (no-op)."
    "Tag $Tag already on remote (no-op)." | Out-File -FilePath $dbgFile -Append -Encoding utf8
} else {
    Write-Host "Pushed tag $Tag to origin"
    "Pushed tag $Tag to origin" | Out-File -FilePath $dbgFile -Append -Encoding utf8
}

"---- push-tag debug end: $(Get-Date) ----" | Out-File -FilePath $dbgFile -Append -Encoding utf8

exit 0