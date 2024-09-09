param (
    [string]$configuration,
    [string]$msiFile,
    [string]$versionFile
)

# Read the version from Properties\version.txt
$version = Get-Content -Path $versionFile -Raw
$version = $version.Trim()
$tag = "v$version"

# Ensure we are in the correct directory
cd $PSScriptRoot

# Debugging output
Write-Host "Current directory: $PSScriptRoot"
Write-Host "Configuration: $configuration"
Write-Host "MSI File: $msiFile"
Write-Host "Version File: $versionFile"

# Derive XmlOutputPath from versionFile
$xmlFile = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($versionFile), "AutoUpdater.xml")

# Check for unresolved conflicts
$conflicts = git ls-files -u
if ($conflicts) {
    Write-Host "Unresolved conflicts detected. Please resolve them before proceeding."
    exit 1
}

# Ensure we are on the 'dev' branch
$currentBranch = git rev-parse --abbrev-ref HEAD
Write-Host "Current branch: $currentBranch"
if ($currentBranch -ne 'dev') {
    Write-Host "Not on 'dev' branch. Switching to 'dev' branch."
    git checkout dev
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to switch to 'dev' branch. Aborting."
        exit 1
    }
}

# Merge 'master' into 'dev'
Write-Host "Merging 'master' into 'dev'."
git merge master
if ($LASTEXITCODE -ne 0) {
    Write-Host "Merge conflicts detected. Please resolve them before proceeding."
    exit 1
}

# Add all changes including the .msi and AutoUpdater.xml files
git add .

# Commit changes
try {
    git commit -m "Automated commit for $configuration build"
} catch {
    Write-Host "Error during commit: $_"
    exit 1
}

# Push to 'dev' branch
try {
    git push origin dev
} catch {
    Write-Host "Error during push: $_"
    exit 1
}

# If configuration is 'Release', merge to 'master' and tag
if ($configuration -eq 'Release') {
    # Stash local changes
    git stash -u

    git checkout master
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to switch to 'master' branch. Aborting."
        exit 1
    }
    git merge dev

    # Check for valid version
    if ($version -match '^[0-9]+\.[0-9]+\.[0-9]+$') {
        # Tag with the version
        git tag $tag
        git push origin master --tags
    } else {
        Write-Host "Invalid version format in $versionFile. Aborting."
        exit 1
    }

    git checkout dev
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to switch back to 'dev' branch. Aborting."
        exit 1
    }

    # Apply stashed changes
    git stash pop

    # Check if GitHub CLI is available
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Host "GitHub CLI (gh) is not installed or not in PATH. Skipping release creation."
        exit 0
    }

    # Create a release and set it as the latest
    gh release create $tag $msiFile $xmlFile --title "Release $tag" --notes "Release notes for $tag" --latest --target master
}
