param (
    [string]$configuration,
    [string]$msiFile = "..\Installer\ZO.DevModManager.msi",
    [string]$versionFile = "..\Properties\version.txt",
    [bool]$manual = $false
)

# Function to execute a command and handle errors
function Execute-Command {
    param (
        [string]$command
    )
    Write-Output "Executing: $command"
    $result = Invoke-Expression $command 2>&1
    if ($LASTEXITCODE -ne 0) {
        if ($result -match "nothing to commit, working tree clean") {
            Write-Output "Nothing to commit, working tree clean."
        } else {
            Write-Error "Command failed: $command"
            Write-Error ($result -join "`n")
            exit 1
        }
    }
    Write-Output $result
}

# Ensure all changes are staged
Execute-Command "git add -A"

# Commit the changes
try {
    Execute-Command "git commit -m 'Post-build commit for configuration $configuration'"
} catch {
    Write-Error "Failed to commit changes."
    exit 1
}

function Get-NewestTag {
    $tags = git tag --sort=-v:refname
    return $tags[0]
}

function Increment-Tag {
    param (
        [string]$tag
    )
    if ($tag -match "-m(\d+)$") {
        $number = [long]$matches[1] + 1
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
    Execute-Command "git checkout dev"
    Execute-Command "git merge -X ours master"
    Write-Output "Merged master INTO dev with conflicts resolved in favor of dev."
    $currentBranch = 'dev'
} elseif ($currentBranch -eq 'dev') {
    # Friendly merge up to master (locally)
    Execute-Command "git checkout master"
    Execute-Command "git merge -X theirs dev"
    Write-Output "Merged dev INTO master with conflicts resolved in favor of dev."
    $currentBranch = 'master'
}

# Always push dev branch changes
Execute-Command "git push origin dev"
Write-Output "Pushed dev branch to origin."

# Check if there are any changes before committing
$gitStatus = git status --porcelain
if (-not [string]::IsNullOrWhiteSpace($gitStatus)) {
    Execute-Command "git add ."
    Execute-Command "git commit -m 'Automated commit for $configuration configuration'"
    Write-Output "Committed changes."
} else {
    Write-Output "Nothing to commit, working tree clean."
}

# Handle release
if ($configuration -eq 'GitRelease') {
    # Push master branch changes only during GitRelease
    Execute-Command "git push origin master"
    Write-Output "Pushed master branch to origin."

    # Delete existing local tag if it exists
    $existingTag = git tag -l $tagName
    if ($existingTag) {
        Execute-Command "git tag -d $tagName"
    }

    Execute-Command "git tag $tagName"
    # Push the tag to remote, force-pushing to overwrite if necessary
    try {
        Execute-Command "git push --force origin $tagName"
    } catch {
        Write-Error "Failed to push tag $tagName to origin."
        exit 1
    }
    Write-Output "Tagged and pushed release: $tagName"

    # Create GitHub release
    if (Get-Command gh -ErrorAction SilentlyContinue) {
        $autoUpdaterFile = "$(git rev-parse --show-toplevel)/Properties/AutoUpdater.xml"
        if (Test-Path -Path $autoUpdaterFile) {
            Execute-Command "gh release create $tagName $msiFile $autoUpdaterFile -t $tagName -n 'Release $tagName'"
            Write-Output "Created GitHub release: $tagName with AutoUpdater.xml"
        } else {
            Write-Error "AutoUpdater.xml file not found at path: $autoUpdaterFile"
            exit 1
        }
    } else {
        Write-Error "GitHub CLI (gh) not found."
        exit 1
    }

    # Check if there is a stash to pop
    $stashList = git stash list
    if (-not [string]::IsNullOrWhiteSpace($stashList)) {
        Execute-Command "git stash pop"
    } else {
        Write-Output "No stash to pop."
    }
}

# Push AutoUpdater.xml
$autoUpdaterFile = "$(git rev-parse --show-toplevel)/Properties/AutoUpdater.xml"
if (Test-Path -Path $autoUpdaterFile) {
    Execute-Command "git add $autoUpdaterFile"
    Execute-Command "git commit -m 'Update AutoUpdater.xml for $tagName'"
    if ($configuration -eq 'GitRelease') {
        Execute-Command "git push origin master"
        Write-Output "Pushed AutoUpdater.xml changes to master."
    } else {
        Execute-Command "git push origin dev"
        Write-Output "Pushed AutoUpdater.xml changes to dev."
    }
} else {
    Write-Error "AutoUpdater.xml file not found at path: $autoUpdaterFile"
    exit 1
}

# Check if GitHub CLI is available
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "GitHub CLI (gh) is not installed or not in PATH. Skipping release creation."
    exit 0
}

# Switch back to dev if needed
if ($currentBranch -eq 'dev') {
    Execute-Command "git checkout dev"
    Write-Output "Switched back to dev branch."
}
