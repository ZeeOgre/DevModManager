# Define the path to the version.txt file
$versionTxtFilePath = "$(git rev-parse --show-toplevel)\App\Properties\version.txt"

# Read the version number from the version.txt file
if (Test-Path -Path $versionTxtFilePath) {
    $newVersion = Get-Content -Path $versionTxtFilePath -Raw
    $newVersion = $newVersion.Trim()
} else {
    Write-Error "version.txt file not found at path: $versionTxtFilePath"
    exit 1
}

# Log the newVersion parameter
Write-Output "Read newVersion from version.txt: $newVersion"

# Function to validate version string
function Validate-Version {
    param (
        [string]$version
    )
    if ($version -match '^\d+\.\d+\.\d+(-[A-Za-z0-9\-]+)?(\+[A-Za-z0-9\-]+)?$') {
        return $true
    } else {
        return $false
    }
}

if (-not (Validate-Version -version $newVersion)) {
    Write-Error "Invalid version format: $newVersion"
    exit 1
}

# Ensure we are in the correct directory
cd "$(git rev-parse --show-toplevel)"

# Commit all changes
git add .
git commit -m "Auto-commit for version $newVersion"

# Merge changes with master
git checkout master
git merge -

# Ensure output directory exists
$outputDir = "$(git rev-parse --show-toplevel)\App\output"
if (-not (Test-Path -Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir
}

# Fetch all tags from GitHub
git fetch --tags

# Get all tags
$tags = git tag

# Function to compare version strings
function Compare-Version {
    param (
        [string]$version1,
        [string]$version2
    )
    try {
        # Ensure both versions are in a comparable format
        $v1 = [Version]::Parse($version1)
        $v2 = [Version]::Parse($version2)
        return $v1.CompareTo($v2)
    } catch {
        Write-Error "Invalid version format: $version1 or $version2"
        return $null
    }
}

# Delete higher version tags
foreach ($tag in $tags) {
    # Trim leading 'v' from tag if present
    $normalizedTag = $tag.TrimStart('v')
    $comparisonResult = Compare-Version -version1 $normalizedTag -version2 $newVersion
    if ($comparisonResult -gt 0) {
        Write-Output "Deleting higher version tag: $tag"
        git tag -d $tag
        git push origin :refs/tags/$tag
    }
}

# Create a tag and push the release
$tagName = "v$newVersion"
git tag $tagName
git push origin master --tags

# Create a GitHub release and upload assets
$releaseName = $tagName
$releaseNotes = "Release for version $newVersion"
$msiPath = "$(git rev-parse --show-toplevel)\ZO.DMM.AIP\ZO.DMM.AIP-AnyCPU_GitRelease-SetupFiles\DevModManager.msi"
$xmlPath = "$(git rev-parse --show-toplevel)\App\Properties\AutoUpdater.xml"

# Create the release
gh release create $releaseName $msiPath $xmlPath --title $releaseName --notes $releaseNotes --latest

# Sync changes and switch back to the dev branch
git checkout -
git pull origin master
git checkout dev
git merge master

Write-Output "Post-build script executed successfully."
