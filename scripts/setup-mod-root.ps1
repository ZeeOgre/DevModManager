param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,

    [Parameter(Mandatory = $true)]
    [string]$RemoteUrl,

    [switch]$SkipLfs,
    [switch]$ForceReclone
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$repoParent = Split-Path -Parent $repoRoot
$repoName = Split-Path -Leaf $repoRoot

if ([string]::IsNullOrWhiteSpace($repoParent) -or -not (Test-Path -LiteralPath $repoParent)) {
    throw "Parent folder does not exist: $repoParent"
}

function Invoke-Git([string]$workingDir, [string[]]$gitArgs) {
    $commandPreview = $gitArgs -join ' '
    Write-Host "[git] ($workingDir) git $commandPreview"
    pushd $workingDir
    try {
        & git @gitArgs
        if ($LASTEXITCODE -ne 0) {
            throw "git $commandPreview failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        popd
    }
}

if ($ForceReclone -and (Test-Path -LiteralPath $repoRoot)) {
    Write-Host "[setup-mod-root] Removing existing repo due to -ForceReclone: $repoRoot"
    Remove-Item -LiteralPath $repoRoot -Recurse -Force
}

if (-not (Test-Path -LiteralPath $repoRoot)) {
    Write-Host "[setup-mod-root] Cloning root repo..."
    Invoke-Git $repoParent @('clone', $RemoteUrl, $repoName)
}

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot '.git'))) {
    throw "Repo root is not a git working tree: $repoRoot"
}

Invoke-Git $repoRoot @('fetch', '--all', '--prune')
Invoke-Git $repoRoot @('pull', '--recurse-submodules')
Invoke-Git $repoRoot @('submodule', 'sync', '--recursive')
Invoke-Git $repoRoot @('submodule', 'update', '--init', '--recursive')

if (-not $SkipLfs) {
    Invoke-Git $repoRoot @('lfs', 'install')
    Invoke-Git $repoRoot @('lfs', 'pull')

    # Also pull LFS content in each initialized submodule where possible.
    $submodulePaths = git -C $repoRoot config --file .gitmodules --get-regexp path 2>$null |
        ForEach-Object { ($_ -split '\s+', 2)[1] } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($rel in $submodulePaths) {
        $full = Join-Path $repoRoot $rel
        if (Test-Path -LiteralPath $full) {
            try {
                Invoke-Git $full @('lfs', 'install')
                Invoke-Git $full @('lfs', 'pull')
            }
            catch {
                Write-Warning "LFS pull skipped for submodule '$rel': $($_.Exception.Message)"
            }
        }
    }
}

Write-Host "[setup-mod-root] Ready: $repoRoot"
Write-Host "[setup-mod-root] Next in DMM: set Program Settings -> Mod Repo Root to this path, set GitHub account/token/repo, then run Sync." 
