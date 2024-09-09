param (
    [string]$SettingsFile,
    [string]$CsprojFilePath,
    [string]$AppConfigFilePath,
    [string]$VersionTxtFilePath,
    [string]$AipFilePath,
    [string]$XmlOutputPath = "./Properties/AutoUpdater.xml"
)

Write-Output "SettingsFile: $SettingsFile"
Write-Output "CsprojFilePath: $CsprojFilePath"
Write-Output "AppConfigFilePath: $AppConfigFilePath"
Write-Output "VersionTxtFilePath: $VersionTxtFilePath"
Write-Output "AipFilePath: $AipFilePath"

# Resolve paths to absolute paths
$ResolvedSettingsFile = (Resolve-Path -Path $SettingsFile).Path
$ResolvedCsprojFilePath = (Resolve-Path -Path $CsprojFilePath).Path
$ResolvedAppConfigFilePath = (Resolve-Path -Path $AppConfigFilePath).Path
$ResolvedVersionTxtFilePath = (Resolve-Path -Path $VersionTxtFilePath).Path
$ResolvedAipFilePath = (Resolve-Path -Path $AipFilePath).Path
$ResolvedXmlFilePath = (Resolve-Path -Path $XmlOutputPath).Path

Write-Host "Resolved SettingsFile: $ResolvedSettingsFile"
Write-Host "Resolved VersionTxtFilePath: $ResolvedVersionTxtFilePath"
Write-Host "Resolved CsprojFilePath: $ResolvedCsprojFilePath"
Write-Host "Resolved AppConfigFilePath: $ResolvedAppConfigFilePath"
Write-Host "Resolved AipFilePath: $ResolvedAipFilePath"
Write-Host "Resolved XmlFilePath: $ResolvedXmlFilePath"

# Function to load XML file
function Get-CurrentVersion {
    param (
        [string]$filePath
    )

    [xml]$xml = Get-Content -Path $filePath -Raw -Encoding UTF8

    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $namespaceManager.AddNamespace("ns", "http://schemas.microsoft.com/VisualStudio/2004/01/settings")

    $currentVersionNode = $xml.SelectSingleNode("//ns:Setting[@Name='version']/ns:Value", $namespaceManager)
    $currentVersion = $currentVersionNode.InnerText

    $result = [PSCustomObject]@{
        Version = $currentVersion
        Node = $currentVersionNode
        Xml = $xml
    }

    return $result
}

# Function to increment the version
function Increment-Version {
    param (
        [string]$currentVersion
    )
    $versionParts = $currentVersion -split '\.'
    if ($versionParts.Length -eq 3) {
        $newVersion = "$($versionParts[0]).$($versionParts[1]).$([int]$versionParts[2] + 1)"
        return $newVersion
    } else {
        Write-Output "Error: Version format is incorrect. Expected format: X.X.X"
        exit 1
    }
}

# Function to update the version in the settings file
function Update-SettingsVersion {
    param (
        [xml]$xml,
        [System.Xml.XmlNode]$currentVersionNode,
        [string]$newVersion,
        [string]$settingsFile,
        [switch]$WhatIf
    )
    
    Write-Output "Updating settings file..."
    Write-Output "Current Version Node: $currentVersionNode"
    Write-Output "New Version: $newVersion"
    Write-Output "Settings File: $settingsFile"
    
    $currentVersionNode.InnerText = $newVersion
    
    if ($WhatIf) {
        Write-Output "WhatIf: $settingsFile would be updated with new version $newVersion"
    } else {
        if (![string]::IsNullOrEmpty($settingsFile)) {
            $xml.Save($settingsFile)
            Write-Output "Settings file updated successfully."
        } else {
            throw "Settings file path is empty or null."
        }
    }
}

# Function to update the version in the .csproj file
function Update-CsprojVersion {
    param (
        [string]$newVersion,
        [string]$csprojFilePath,
        [switch]$WhatIf
    )
    if ([string]::IsNullOrEmpty($csprojFilePath)) {
        throw "Csproj file path is empty or null."
    }
    
    # Read the .csproj file with UTF-8 encoding
    [xml]$csprojXml = [xml](Get-Content -Path $csprojFilePath -Raw -Encoding UTF8)
    
    $versionNode = $csprojXml.SelectSingleNode("//Version")
    if ($versionNode -ne $null) {
        $versionNode.InnerText = $newVersion
        Write-Output "Updated Version in csproj: $newVersion"
    } else {
        $propertyGroupNode = $csprojXml.SelectSingleNode("//PropertyGroup")
        if ($propertyGroupNode -ne $null) {
            $newVersionNode = $csprojXml.CreateElement("Version")
            $newVersionNode.InnerText = $newVersion
            $propertyGroupNode.AppendChild($newVersionNode)
            Write-Output "Created and updated Version node in csproj: $newVersion"
        } else {
            Write-Output "Error: PropertyGroup node not found in csproj file."
            return
        }
    }
    
    if ($WhatIf) {
        Write-Output "WhatIf: $csprojFilePath would be updated with new version $newVersion"
    } else {
        $maxRetries = 5
        $retryCount = 0
        $success = $false

        while (-not $success -and $retryCount -lt $maxRetries) {
            try {
                # Write the .csproj file with UTF-8 encoding
                $csprojXml.Save($csprojFilePath)
                [System.IO.File]::WriteAllText($csprojFilePath, [System.IO.File]::ReadAllText($csprojFilePath), [System.Text.Encoding]::UTF8)
                $success = $true
            } catch {
                $retryCount++
                Write-Output "Attempt ${retryCount}: Failed to write to $csprojFilePath. Retrying in 1 second..."
                Start-Sleep -Seconds 1
            }
        }

        if (-not $success) {
            throw "Failed to write to $csprojFilePath after $maxRetries attempts."
        }
    }
}

# Function to update the version in the App.config file
function Update-AppConfigVersion {
    param (
        [string]$newVersion,
        [string]$appConfigFilePath,
        [switch]$WhatIf
    )
    if ([string]::IsNullOrEmpty($appConfigFilePath)) {
        throw "App.config file path is empty or null."
    }
    [xml]$appConfigXml = Get-Content $appConfigFilePath
    $versionNode = $appConfigXml.SelectSingleNode("//ZO.DMM.AppNF.Properties.Settings/setting[@name='version']/value")
    if ($versionNode -ne $null) {
        $versionNode.InnerText = $newVersion
        Write-Output "Updated Version in App.config: $newVersion"
        if ($WhatIf) {
            Write-Output "WhatIf: $appConfigFilePath would be updated with new version $newVersion"
        } else {
            $appConfigXml.Save($appConfigFilePath)
        }
    } else {
        Write-Output "Error: Version node not found in App.config file."
    }
}

# Function to update the version in the version.txt file
function Update-VersionTxt {
    param (
        [string]$newVersion,
        [string]$versionTxtFilePath,
        [switch]$WhatIf
    )
    if ([string]::IsNullOrEmpty($versionTxtFilePath)) {
        throw "Version.txt file path is empty or null."
    }
    Write-Output "Updating version.txt file..."
    if ($WhatIf) {
        Write-Output "WhatIf: $versionTxtFilePath would be updated with new version $newVersion"
    } else {
        Set-Content -Path $versionTxtFilePath -Value $newVersion
        Write-Output "version.txt file updated successfully."
    }
}

# Function to update the version in the .aip file
function Update-AipVersion {
    param (
        [string]$newVersion,
        [string]$aipFilePath,
        [switch]$WhatIf
    )
    if ([string]::IsNullOrEmpty($aipFilePath)) {
        throw "AIP file path is empty or null."
    }
    
    # Load the AIP file as XML
    [xml]$aipXml = [xml](Get-Content -Path $aipFilePath -Raw -Encoding UTF8)
    
    # Find the ROW element with Property="ProductVersion"
    $productVersionRow = $aipXml.SelectSingleNode("//ROW[@Property='ProductVersion']")
    if ($productVersionRow -ne $null) {
        $productVersionRow.Value = $newVersion
        Write-Output "Updated ProductVersion in AIP: $newVersion"
    } else {
        Write-Output "Error: ProductVersion row not found in AIP file."
        return
    }

    # Generate a new GUID
    $newGuid = [guid]::NewGuid().ToString().ToUpper()
    
    # Find the ROW element with Property="ProductCode"
    $productCodeRow = $aipXml.SelectSingleNode("//ROW[@Property='ProductCode']")
    if ($productCodeRow -ne $null) {
        $productCodeRow.Value = "1033:{$newGuid}"
        Write-Output "Updated ProductCode in AIP: 1033:{$newGuid}"
    } else {
        Write-Output "Error: ProductCode row not found in AIP file."
        return
    }
    
    if ($WhatIf) {
        Write-Output "WhatIf: $aipFilePath would be updated with new version $newVersion and new ProductCode 1033:{$newGuid}"
    } else {
        $aipXml.Save($aipFilePath)
        Write-Output "AIP file updated successfully."
    }
}

# Function to create the AutoUpdater XML file
function Create-AutoUpdaterXml {
    param (
        [string]$version,
        [string]$xmlOutputPath
    )
    $url = "https://github.com/ZeeOgre/ZO.DevModManager/releases/latest/download/DevModManager.msi"
    $changelog = "https://github.com/ZeeOgre/ZO.DevModManager/releases/latest"

    $xmlContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<item>
  <version>$version</version>
  <url>$url</url>
  <changelog>$changelog</changelog>
</item>
"@

    Set-Content -Path $xmlOutputPath -Value $xmlContent
    Write-Output "AutoUpdater XML file created successfully at $xmlOutputPath."
}

try {
    # Main script execution
    $result = Get-CurrentVersion -filePath $ResolvedSettingsFile
    $currentVersion = $result.Version
    $currentVersionNode = $result.Node
    $xml = $result.Xml
    $newVersion = Increment-Version -currentVersion $currentVersion

    Update-SettingsVersion -xml $xml -currentVersionNode $currentVersionNode -newVersion $newVersion -settingsFile $ResolvedSettingsFile
    Update-CsprojVersion -newVersion $newVersion -csprojFilePath $ResolvedCsprojFilePath
    Update-AppConfigVersion -newVersion $newVersion -appConfigFilePath $ResolvedAppConfigFilePath
    Update-VersionTxt -newVersion $newVersion -versionTxtFilePath $ResolvedVersionTxtFilePath
    Update-AipVersion -newVersion $newVersion -aipFilePath $ResolvedAipFilePath

    # Create the AutoUpdater XML file
    Create-AutoUpdaterXml -version $newVersion -xmlOutputPath $XmlOutputPath

    Write-Output "Version incremented successfully to $newVersion"
} catch {
    Write-Output "Error: $_"
    exit 1
}
