param (
    [string]$configuration,
    [string]$msiFile,
    [string]$versionFile,
    [bool]$manual = $false
)

function Get-NewestTag {
    $tags = git tag --sort=-v:refname
    return $tags[0]
}

function Increment-Tag {
    param (
        [string]$tag
    )
    if ($tag -match "-m(\d+)$") {
        $number = [int]$matches[1] + 1
        return $tag -replace "-m\d+$", "-m$number"
    } else {
        return "$tag-m1"
    }
}

# Ensure correct directory
Set-Location $PSScriptRoot

# Debugging output
Write-Output "Current Directory: $(Get-Location)"
Write-Output "Configuration: $configuration"
Write-Output "MSI File: $msiFile"
Write-Output "Version File: $versionFile"
Write-Output "Manual Mode: $manual"

# Read version or set manual test tag
if (-not $manual) {
    $version = Get-Content $versionFile | Out-String
    $version = $version.Trim()
    $tagName = "v$version"
} else {
    $newestTag = Get-NewestTag
    $tagName = Increment-Tag -tag $newestTag
}

Write-Output "Tag Name: $tagName"

# Ensure on correct branch
$currentBranch = git rev-parse --abbrev-ref HEAD
Write-Output "Current Branch: $currentBranch"

if ($currentBranch -eq 'master') {
    # Clobber down to dev
    git checkout dev
    git merge -X theirs master
    if ($?) {
        Write-Output "Merged master into dev with conflicts resolved in favor of master."
    } else {
        Write-Error "Failed to merge master into dev."
        exit 1
    }
} elseif ($currentBranch -eq 'dev') {
    # Friendly merge up to master
    git checkout master
    git merge dev
    if ($?) {
        Write-Output "Merged dev into master."
    } else {
        Write-Error "Failed to merge dev into master."
        exit 1
    }
}

# Commit and push changes
git add .
git commit -m "Automated commit for $configuration configuration"
if ($?) {
    Write-Output "Committed changes."
} else {
    Write-Error "Failed to commit changes."
    exit 1
}

git push origin $currentBranch
if ($?) {
    Write-Output "Pushed changes to $currentBranch."
} else {
    Write-Error "Failed to push changes to $currentBranch."
    exit 1
}

# Handle release
if ($configuration -eq 'Release') {
    git tag $tagName
    git push origin $tagName
    Write-Output "Tagged and pushed release: $tagName"

    # Create GitHub release
    if (Get-Command gh -ErrorAction SilentlyContinue) {
        gh release create $tagName $msiFile -t $tagName -n "Release $tagName"
        Write-Output "Created GitHub release: $tagName"
    } else {
        Write-Error "GitHub CLI (gh) not found."
        exit 1
    }

    # Apply stashed changes
    git stash pop

    # Check if GitHub CLI is available
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Host "GitHub CLI (gh) is not installed or not in PATH. Skipping release creation."
        exit 0
}

# Switch back to dev if needed
if ($currentBranch -eq 'dev') {
    git checkout dev
    Write-Output "Switched back to dev branch."
}
