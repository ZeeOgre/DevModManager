param(
    [string]$InputFile
)

[System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms') | Out-Null
[System.Windows.Forms.Application]::EnableVisualStyles()

# -------------------------------------------------------------------
#  Config helpers (single unified .ini)
# -------------------------------------------------------------------
$scriptName = [System.IO.Path]::GetFileNameWithoutExtension($MyInvocation.MyCommand.Definition)
if (-not $scriptName) { $scriptName = "mod_archiver_unified" }
$configPath = Join-Path $PSScriptRoot "$scriptName.ini"

function Load-Config {
    param([string]$Path)

    $cfg = @{}
    if (Test-Path $Path) {
        Get-Content $Path | ForEach-Object {
            if ($_ -match '^\s*([^=]+)=(.*)$') {
                $key = $matches[1].Trim()
                $val = $matches[2].Trim()
                $cfg[$key] = $val
            }
        }
    }
    return $cfg
}

function Save-Config {
    param(
        [string]$Path,
        [hashtable]$Data
    )
    $lines = @()
    foreach ($k in $Data.Keys) {
        $lines += ("{0}={1}" -f $k, $Data[$k])
    }
    $lines | Set-Content -Path $Path -Encoding UTF8
}

function Get-Bool {
    param(
        [hashtable]$Config,
        [string]$Key,
        [bool]$Default = $false
    )
    if (-not $Config.ContainsKey($Key)) { return $Default }
    $v = $Config[$Key]
    if ($null -eq $v) { return $Default }
    $s = $v.ToString().Trim().ToLowerInvariant()
    return @('1','true','yes','on') -contains $s
}

$config = Load-Config -Path $configPath

Set-Location $PSScriptRoot

# -------------------------------------------------------------------
#  Voice achlist rebuild helper from SoundFileFix.ps1
# -------------------------------------------------------------------
function Rebuild-AchlistFromEsmVoice {
    param(
        [string]$AchlistPath,
        [string]$DataRoot,
        [string]$ModName,
        [string]$PcEsmVoiceFolder,
        [System.Windows.Forms.TextBox]$LogBox
    )

    if (-not (Test-Path $AchlistPath)) {
        $LogBox.AppendText("achlist not found, skipping rebuild:`r`n  $AchlistPath`r`n")
        return
    }

    if (-not (Test-Path $PcEsmVoiceFolder)) {
        $LogBox.AppendText("PC ESM voice folder not found, skipping rebuild:`r`n  $PcEsmVoiceFolder`r`n")
        return
    }

    try {
        $LogBox.AppendText("Rebuilding achlist for mod '$ModName'`r`n")
        $LogBox.AppendText("  achlist: $AchlistPath`r`n")
        $LogBox.AppendText("  source : $PcEsmVoiceFolder`r`n")

        # Backup original achlist (YYMMDDhhmm)
        $timestamp = Get-Date -Format "yyMMddHHmm"
        $backupPath = "$AchlistPath.$timestamp.bak"
        Copy-Item -Path $AchlistPath -Destination $backupPath -Force
        $LogBox.AppendText("Backup created:`r`n  $backupPath`r`n")

        # Load JSON (array of strings)
        $rawJson = Get-Content -Path $AchlistPath -Raw
        $assets = $rawJson | ConvertFrom-Json

        # Build replacement list for voice entries
        $voiceRoot = Join-Path $DataRoot ("sound\voice\{0}.esm" -f $ModName)
        if (-not (Test-Path $voiceRoot)) {
            $LogBox.AppendText("ESM voice root not found for rebuild:`r`n  $voiceRoot`r`n")
            return
        }

        $newVoiceAssets = Get-ChildItem -Path $voiceRoot -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in '.wem', '.ffxanim' } |
            ForEach-Object {
                $rel = $_.FullName.Substring($DataRoot.Length + 1).Replace('/', '\').Replace('\\', '\')
                "Data\$rel"
            }

        $LogBox.AppendText("Found $($newVoiceAssets.Count) new ESM voice assets.`r`n")

        # Remove any existing .wem or .ffxanim entries (both ESP and ESM will be replaced)
        $assets = $assets | Where-Object { 
            $_ -notlike "*.wem" -and $_ -notlike "*.ffxanim" 
        }

        # Add new ESM entries
        $assets = @($assets + $newVoiceAssets) 

        # Sort the assets before writing
        $assets = $assets | Sort-Object

        # Write back, compact
        Write-AchlistProper -Items $assets -OutPath $AchlistPath

        $LogBox.AppendText("achlist updated with new ESM voice entries.`r`n")
    }   
    catch {
        $LogBox.AppendText("ERROR rebuilding achlist: $($_.Exception.Message)`r`n")
    }
}

# -------------------------------------------------------------------
#  Form + Controls (mod_archiver as the base layout)
# -------------------------------------------------------------------
$form = New-Object System.Windows.Forms.Form
$form.Text = "Mod Archiver (Unified)"
$form.Size = New-Object System.Drawing.Size(640, 800)
$form.StartPosition = "CenterScreen"
$form.AllowDrop = $true

# Drag & Drop for input file
$form.Add_DragEnter({
    if ($_.Data.GetDataPresent([Windows.Forms.DataFormats]::FileDrop)) {
        $_.Effect = 'Copy'
    }
})

$form.Add_DragDrop({
    $file = $_.Data.GetData([Windows.Forms.DataFormats]::FileDrop)[0]
    if ($file -match '\\([^\\]+)\.(achlist|esm|esp|ba2|txt)$') {
        $inputBox.Text = $file
    }
})

# Input File
$inputLabel = New-Object System.Windows.Forms.Label
$inputLabel.Text = "Input File (.achlist / .esm / .esp / .ba2):"
$inputLabel.Location = New-Object System.Drawing.Point(10, 10)
$inputLabel.AutoSize = $true
$form.Controls.Add($inputLabel)

$inputBox = New-Object System.Windows.Forms.TextBox
$inputBox.Location = New-Object System.Drawing.Point(260, 10)
$inputBox.Width = 300
if ($InputFile) {
    $inputBox.Text = $InputFile
} elseif ($config.ContainsKey('InputFile')) {
    $inputBox.Text = $config['InputFile']
}
$form.Controls.Add($inputBox)

$inputButton = New-Object System.Windows.Forms.Button
$inputButton.Text = "..."
$inputButton.Location = New-Object System.Drawing.Point(570, 8)
$inputButton.Width = 40
$inputButton.Add_Click({
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "Supported Files (*.achlist;*.esm;*.esp;*.ba2)|*.achlist;*.esm;*.esp;*.ba2|All files (*.*)|*.*"
    if ($dialog.ShowDialog() -eq 'OK') {
        $inputBox.Text = $dialog.FileName
    }
})
$form.Controls.Add($inputButton)

# Backup Destination Folder
$destLabel = New-Object System.Windows.Forms.Label
$destLabel.Text = "Backup Destination Folder:"
$destLabel.Location = New-Object System.Drawing.Point(10, 40)
$destLabel.AutoSize = $true
$form.Controls.Add($destLabel)

$destBox = New-Object System.Windows.Forms.TextBox
$destBox.Location = New-Object System.Drawing.Point(260, 40)
$destBox.Width = 300
$destBox.Text = if ($config.ContainsKey('BackupFolder')) { $config['BackupFolder'] } else { '' }
$form.Controls.Add($destBox)

$destButton = New-Object System.Windows.Forms.Button
$destButton.Text = "..."
$destButton.Location = New-Object System.Drawing.Point(570, 38)
$destButton.Width = 40
$destButton.Add_Click({
    $folderDialog = New-Object System.Windows.Forms.FolderBrowserDialog
    if ($folderDialog.ShowDialog() -eq 'OK') {
        $destBox.Text = $folderDialog.SelectedPath
    }
})
$form.Controls.Add($destButton)

# Game Data Folder
$dataLabel = New-Object System.Windows.Forms.Label
$dataLabel.Text = "Game Data Folder:"
$dataLabel.Location = New-Object System.Drawing.Point(10, 70)
$dataLabel.AutoSize = $true
$form.Controls.Add($dataLabel)

$dataBox = New-Object System.Windows.Forms.TextBox
$dataBox.Location = New-Object System.Drawing.Point(260, 70)
$dataBox.Width = 300
$dataBox.Text = if ($config.ContainsKey('DataFolder')) { $config['DataFolder'] } else { '' }
$form.Controls.Add($dataBox)

$dataButton = New-Object System.Windows.Forms.Button
$dataButton.Text = "..."
$dataButton.Location = New-Object System.Drawing.Point(570, 68)
$dataButton.Width = 40
$dataButton.Add_Click({
    $folderDialog = New-Object System.Windows.Forms.FolderBrowserDialog
    if ($folderDialog.ShowDialog() -eq 'OK') {
        $dataBox.Text = $folderDialog.SelectedPath
    }
})
$form.Controls.Add($dataButton)

# Xbox Data Folder
$xboxLabel = New-Object System.Windows.Forms.Label
$xboxLabel.Text = "Xbox Data Folder:"
$xboxLabel.Location = New-Object System.Drawing.Point(10, 100)
$xboxLabel.AutoSize = $true
$form.Controls.Add($xboxLabel)

$xboxBox = New-Object System.Windows.Forms.TextBox
$xboxBox.Location = New-Object System.Drawing.Point(260, 100)
$xboxBox.Width = 300
$xboxBox.Text = if ($config.ContainsKey('XboxFolder')) { $config['XboxFolder'] } else { '' }
$form.Controls.Add($xboxBox)

$xboxButton = New-Object System.Windows.Forms.Button
$xboxButton.Text = "..."
$xboxButton.Location = New-Object System.Drawing.Point(570, 98)
$xboxButton.Width = 40
$xboxButton.Add_Click({
    $folderDialog = New-Object System.Windows.Forms.FolderBrowserDialog
    if ($folderDialog.ShowDialog() -eq 'OK') {
        $xboxBox.Text = $folderDialog.SelectedPath
    }
})
$form.Controls.Add($xboxButton)

# Archiver2.exe Path
$archiverLabel = New-Object System.Windows.Forms.Label
$archiverLabel.Text = "Archiver2.exe Path:"
$archiverLabel.Location = New-Object System.Drawing.Point(10, 130)
$archiverLabel.AutoSize = $true
$form.Controls.Add($archiverLabel)

$archiverBox = New-Object System.Windows.Forms.TextBox
$archiverBox.Location = New-Object System.Drawing.Point(260, 130)
$archiverBox.Width = 300
$archiverBox.Text = if ($config.ContainsKey('ArchiverPath')) { $config['ArchiverPath'] } else { '' }
$form.Controls.Add($archiverBox)

$archiverButton = New-Object System.Windows.Forms.Button
$archiverButton.Text = "..."
$archiverButton.Location = New-Object System.Drawing.Point(570, 128)
$archiverButton.Width = 40
$archiverButton.Add_Click({
    $fileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $fileDialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
    if ($archiverBox.Text) {
        $fileDialog.InitialDirectory = [System.IO.Path]::GetDirectoryName($archiverBox.Text)
    }
    if ($fileDialog.ShowDialog() -eq 'OK') {
        $archiverBox.Text = $fileDialog.FileName
    }
})
$form.Controls.Add($archiverButton)

# Dmmdeps.exe Path
$dmmdepsLabel = New-Object System.Windows.Forms.Label
$dmmdepsLabel.Text = "Dmmdeps.exe Path:"
$dmmdepsLabel.Location = New-Object System.Drawing.Point(10, 160)
$dmmdepsLabel.AutoSize = $true
$form.Controls.Add($dmmdepsLabel)

$dmmdepsBox = New-Object System.Windows.Forms.TextBox
$dmmdepsBox.Location = New-Object System.Drawing.Point(260, 160)
$dmmdepsBox.Width = 300
$dmmdepsBox.Text = if ($config.ContainsKey('DmmdepsPath')) { $config['DmmdepsPath'] } else { '' }
$form.Controls.Add($dmmdepsBox)

$dmmdepsButton = New-Object System.Windows.Forms.Button
$dmmdepsButton.Text = "..."
$dmmdepsButton.Location = New-Object System.Drawing.Point(570, 158)
$dmmdepsButton.Width = 40
$dmmdepsButton.Add_Click({
    $fileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $fileDialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
    if ($dmmdepsBox.Text) {
        $fileDialog.InitialDirectory = [System.IO.Path]::GetDirectoryName($dmmdepsBox.Text)
    }
    if ($fileDialog.ShowDialog() -eq 'OK') {
        $dmmdepsBox.Text = $fileDialog.FileName
    }
})
$form.Controls.Add($dmmdepsButton)

# -------------------------------------------------------------------
#  Feature checkbox rows (one row per area)
# -------------------------------------------------------------------

# Voice File Management
$voiceLabel = New-Object System.Windows.Forms.Label
$voiceLabel.Text = "Voice File Management:"
$voiceLabel.Location = New-Object System.Drawing.Point(10, 200)
$voiceLabel.AutoSize = $true
$form.Controls.Add($voiceLabel)

$voiceUpdateCheckbox = New-Object System.Windows.Forms.CheckBox
$voiceUpdateCheckbox.Text = "Voice folder update (ESP to ESM)"
$voiceUpdateCheckbox.Location = New-Object System.Drawing.Point(30, 220)
$voiceUpdateCheckbox.AutoSize = $true
$voiceUpdateCheckbox.Checked = Get-Bool -Config $config -Key 'VoiceFolderUpdate' -Default:$false
$form.Controls.Add($voiceUpdateCheckbox)

$rebuildAchlistCheckbox = New-Object System.Windows.Forms.CheckBox
$rebuildAchlistCheckbox.Text = "Rebuild .achlist voice entries"
$rebuildAchlistCheckbox.Location = New-Object System.Drawing.Point(330, 220)
$rebuildAchlistCheckbox.AutoSize = $true
$rebuildAchlistCheckbox.Checked = Get-Bool -Config $config -Key 'RebuildAchlist' -Default:$false
$form.Controls.Add($rebuildAchlistCheckbox)

# Archive Management
$archiveLabel = New-Object System.Windows.Forms.Label
$archiveLabel.Text = "Archive Management:"
$archiveLabel.Location = New-Object System.Drawing.Point(10, 250)
$archiveLabel.AutoSize = $true
$form.Controls.Add($archiveLabel)

$useDmmdepsCheckbox = New-Object System.Windows.Forms.CheckBox
$useDmmdepsCheckbox.Text = "Use dmmdeps"
$useDmmdepsCheckbox.Location = New-Object System.Drawing.Point(30, 270)
$useDmmdepsCheckbox.AutoSize = $true
$useDmmdepsCheckbox.Checked = Get-Bool -Config $config -Key 'UseDmmdeps' -Default:$false
$form.Controls.Add($useDmmdepsCheckbox)

# Move Include Source Scripts checkbox up to same row as Use dmmdeps
$includeSourceScriptsCheckbox = New-Object System.Windows.Forms.CheckBox
$includeSourceScriptsCheckbox.Text = "Include Source Scripts (.psc) in PC Archive"
$includeSourceScriptsCheckbox.Location = New-Object System.Drawing.Point(180, 270)  # <-- Same Y as useDmmdepsCheckbox
$includeSourceScriptsCheckbox.AutoSize = $true
$includeSourceScriptsCheckbox.Checked = Get-Bool -Config $config -Key 'IncludeSourceScripts' -Default:$false
$form.Controls.Add($includeSourceScriptsCheckbox)

# SmartClobber option for dmmdeps
$smartClobberCheckbox = New-Object System.Windows.Forms.CheckBox
$smartClobberCheckbox.Text = "SmartClobber"
$smartClobberCheckbox.Location = New-Object System.Drawing.Point(480, 270)
$smartClobberCheckbox.AutoSize = $true
$smartClobberCheckbox.Checked = Get-Bool -Config $config -Key 'SmartClobber' -Default:$false
$form.Controls.Add($smartClobberCheckbox)

# BA2 Archive Management
$ba2Label = New-Object System.Windows.Forms.Label
$ba2Label.Text = "BA2 Archive Management:"
$ba2Label.Location = New-Object System.Drawing.Point(10, 300)
$ba2Label.AutoSize = $true
$form.Controls.Add($ba2Label)

$xboxArchiveCheckbox = New-Object System.Windows.Forms.CheckBox
$xboxArchiveCheckbox.Text = "Xbox Archive"
$xboxArchiveCheckbox.Location = New-Object System.Drawing.Point(30, 320)
$xboxArchiveCheckbox.AutoSize = $true
$xboxArchiveCheckbox.Checked = Get-Bool -Config $config -Key 'XboxArchive' -Default:$true
$form.Controls.Add($xboxArchiveCheckbox)

$windowsArchiveCheckbox = New-Object System.Windows.Forms.CheckBox
$windowsArchiveCheckbox.Text = "Windows Archive"
$windowsArchiveCheckbox.Location = New-Object System.Drawing.Point(180, 320)
$windowsArchiveCheckbox.AutoSize = $true
$windowsArchiveCheckbox.Checked = Get-Bool -Config $config -Key 'WindowsArchive' -Default:$true
$form.Controls.Add($windowsArchiveCheckbox)

$sortAchlistCheckbox = New-Object System.Windows.Forms.CheckBox
$sortAchlistCheckbox.Text = "Sort .achlist before saving"
$sortAchlistCheckbox.Location = New-Object System.Drawing.Point(340, 320)
$sortAchlistCheckbox.AutoSize = $true
$sortAchlistCheckbox.Checked = Get-Bool -Config $config -Key 'SortAchlist' -Default:$false
$form.Controls.Add($sortAchlistCheckbox)

# Backup Management
$backupLabel = New-Object System.Windows.Forms.Label
$backupLabel.Text = "Backup Management:"
$backupLabel.Location = New-Object System.Drawing.Point(10, 350)
$backupLabel.AutoSize = $true
$form.Controls.Add($backupLabel)

$copyCheckbox = New-Object System.Windows.Forms.CheckBox
$copyCheckbox.Text = "Copy Files to Backup Structure"
$copyCheckbox.Location = New-Object System.Drawing.Point(30, 370)
$copyCheckbox.AutoSize = $true
$copyCheckbox.Checked = Get-Bool -Config $config -Key 'Copy' -Default:$true
$form.Controls.Add($copyCheckbox)

$zipCheckbox = New-Object System.Windows.Forms.CheckBox
$zipCheckbox.Text = "Create Dated Zip"
$zipCheckbox.Location = New-Object System.Drawing.Point(260, 370)
$zipCheckbox.AutoSize = $true
$zipCheckbox.Checked = Get-Bool -Config $config -Key 'Zip' -Default:$true
$form.Controls.Add($zipCheckbox)

# NEW: Clean Copy
$cleanCopyCheckbox = New-Object System.Windows.Forms.CheckBox
$cleanCopyCheckbox.Text = "Clean copy (remove loose_pc/loose_xbox)"
$cleanCopyCheckbox.Location = New-Object System.Drawing.Point(430, 370)
$cleanCopyCheckbox.AutoSize = $true
$cleanCopyCheckbox.Checked = Get-Bool -Config $config -Key 'CleanCopy' -Default:$false
$form.Controls.Add($cleanCopyCheckbox)

# "Select All" – turn on all feature checkboxes (no run)
$doAllButton = New-Object System.Windows.Forms.Button
$doAllButton.Text = "Select All"
$doAllButton.Location = New-Object System.Drawing.Point(30, 400)
$doAllButton.Width = 100
$form.Controls.Add($doAllButton)

# Log label + box
$logLabel = New-Object System.Windows.Forms.Label
$logLabel.Text = "Log:"
$logLabel.Location = New-Object System.Drawing.Point(10, 430)
$logLabel.AutoSize = $true
$form.Controls.Add($logLabel)

$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Multiline = $true
$logBox.ScrollBars = "Vertical"
$logBox.ReadOnly = $true
$logBox.Location = New-Object System.Drawing.Point(10, 450)
$logBox.Size = New-Object System.Drawing.Size(600, 170)
$form.Controls.Add($logBox)

# Status + progress
$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Location = New-Object System.Drawing.Point(10, 630)
$statusLabel.Size = New-Object System.Drawing.Size(600, 20)
$statusLabel.Text = ""
$form.Controls.Add($statusLabel)

$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object System.Drawing.Point(10, 655)
$progressBar.Size = New-Object System.Drawing.Size(600, 20)
$form.Controls.Add($progressBar)

# -------------------------------------------------------------------
#  Buttons (Run, Make AF, Close)
# -------------------------------------------------------------------
$runButton = New-Object System.Windows.Forms.Button
$runButton.Text = "Run"
$runButton.Location = New-Object System.Drawing.Point(200, 690)
$runButton.Width = 80
$form.Controls.Add($runButton)

$afButton = New-Object System.Windows.Forms.Button
$afButton.Text = "Make AF Version"
$afButton.Location = New-Object System.Drawing.Point(290, 690)
$afButton.Width = 110
$afButton.Enabled = $false
$form.Controls.Add($afButton)

$closeButton = New-Object System.Windows.Forms.Button
$closeButton.Text = "Close"
$closeButton.Location = New-Object System.Drawing.Point(410, 690)
$closeButton.Width = 80
$closeButton.Add_Click({ $form.Close() })
$form.Controls.Add($closeButton)

# -------------------------------------------------------------------
#  Shared helpers – path guessing / archiver path
# -------------------------------------------------------------------
$inputBox.Add_TextChanged({
    $path = $inputBox.Text
    if (-not (Test-Path $path)) { return }

    # Guess Data folder on first run if empty
    if (-not $dataBox.Text) {
        $dir = Split-Path $path
        $candidate = $dir
        while ($candidate -and (Split-Path $candidate -Leaf) -ne 'Data') {
            $parent = Split-Path $candidate -Parent
            if ($parent -eq $candidate) { $candidate = $null } else { $candidate = $parent }
        }
        if ($candidate -and (Split-Path $candidate -Leaf) -eq 'Data') {
            $dataBox.Text = $candidate
        }
    }

    # Guess Xbox folder from Data on first run if empty
    if (-not $xboxBox.Text -and $dataBox.Text) {
        $root = Split-Path $dataBox.Text -Parent
        $guess = Join-Path $root "XBOX\Data"
        $xboxBox.Text = $guess
    }
})

$dataBox.Add_TextChanged({
    # Guess Archiver2.exe from Data folder if empty
    if (-not $archiverBox.Text -and $dataBox.Text -like "*\Data") {
        $root = Split-Path $dataBox.Text -Parent
        $guess = Join-Path (Join-Path $root "Tools\Archive2") "Archive2.exe"
        $archiverBox.Text = $guess
    }
})

# -------------------------------------------------------------------
#  Core operations: Voice, BA2, Backup
# -------------------------------------------------------------------
function Invoke-VoiceManagement {
    param(
        [string]$InputPath,
        [string]$DataRoot,
        [string]$XboxRoot,
        [bool]$DoVoiceUpdate,
        [bool]$DoRebuildAchlist
    )
    if (-not $DoVoiceUpdate -and -not $DoRebuildAchlist) { return }

    if (-not (Test-Path $InputPath)) {
        [System.Windows.Forms.MessageBox]::Show("Input file not found:`r`n$InputPath", "Error", 'OK', 'Error') | Out-Null
        return
    }
    if (-not (Test-Path $DataRoot)) {
        [System.Windows.Forms.MessageBox]::Show("Game Data Folder does not exist:`r`n$DataRoot", "Error", 'OK', 'Error') | Out-Null
        return
    }
    if (-not (Test-Path $XboxRoot)) {
        [System.Windows.Forms.MessageBox]::Show("Xbox Data Folder does not exist:`r`n$XboxRoot", "Error", 'OK', 'Error') | Out-Null
        return
    }

    $modName = [System.IO.Path]::GetFileNameWithoutExtension($InputPath)

    $logBox.AppendText("=== Voice File Management ===`r`n")
    $logBox.AppendText("Input : $InputPath`r`n")
    $logBox.AppendText("Data  : $DataRoot`r`n")
    $logBox.AppendText("Xbox  : $XboxRoot`r`n")
    $logBox.AppendText("DoVoiceUpdate   : $DoVoiceUpdate`r`n")
    $logBox.AppendText("DoRebuildAchlist: $DoRebuildAchlist`r`n")
    $logBox.AppendText("-------------------------------------`r`n")

    try {
        $pcEsmVoiceFolder  = Join-Path $DataRoot ("sound\voice\{0}.esm" -f $modName)
        $pcEspVoiceFolder  = Join-Path $DataRoot ("sound\voice\{0}.esp" -f $modName)
        $xboxEspVoiceFolder = Join-Path $XboxRoot ("sound\voice\{0}.esp" -f $modName)
        $xboxEsmVoiceFolder = Join-Path $XboxRoot ("sound\voice\{0}.esm" -f $modName)

        if ($DoVoiceUpdate) {
            # 1) Delete existing PC ESM voice folder
            if (Test-Path $pcEsmVoiceFolder) {
                $logBox.AppendText("Deleting existing PC ESM voice folder:`r`n  $pcEsmVoiceFolder`r`n")
                Remove-Item -Path $pcEsmVoiceFolder -Recurse -Force
            } else {
                $logBox.AppendText("No existing PC ESM voice folder found (OK):`r`n  $pcEsmVoiceFolder`r`n")
            }

            # 2) Copy ESP → ESM tree
            if (Test-Path $pcEspVoiceFolder) {
                $logBox.AppendText("Copying PC ESP voice folder:`r`n  $pcEspVoiceFolder`r`n  =>  $pcEsmVoiceFolder`r`n")
                New-Item -ItemType Directory -Force -Path (Split-Path $pcEsmVoiceFolder) | Out-Null
                Copy-Item -Path $pcEspVoiceFolder -Destination $pcEsmVoiceFolder -Recurse -Force
            } else {
                $logBox.AppendText("WARNING: PC ESP voice folder not found:`r`n  $pcEspVoiceFolder`r`n")
            }

            # 3) Remove wav/dat from new ESM tree
            if (Test-Path $pcEsmVoiceFolder) {
                $logBox.AppendText("Removing .wav and .dat from PC ESM voice tree...`r`n")
                Get-ChildItem -Path $pcEsmVoiceFolder -Recurse -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.Extension -in '.wav', '.dat' } |
                    ForEach-Object {
                        $logBox.AppendText("  DEL: $($_.FullName)`r`n")
                        Remove-Item -Path $_.FullName -Force
                    }
            }

            # 4) Xbox: rename ESP tree to ESM tree if present
            if (Test-Path $xboxEspVoiceFolder) {
                if (Test-Path $xboxEsmVoiceFolder) {
                    $logBox.AppendText("Deleting old Xbox ESM voice folder:`r`n  $xboxEsmVoiceFolder`r`n")
                    Remove-Item -Path $xboxEsmVoiceFolder -Recurse -Force
                }

                $logBox.AppendText("Renaming Xbox ESP voice folder to ESM:`r`n  $xboxEspVoiceFolder`r`n")
                $parent = Split-Path $xboxEspVoiceFolder -Parent
                $newName = ("{0}.esm" -f $modName)
                Rename-Item -Path $xboxEspVoiceFolder -NewName $newName
                $logBox.AppendText("  =>  $xboxEsmVoiceFolder`r`n")
            } else {
                $logBox.AppendText("WARNING: Xbox ESP folder NOT found!`r`n")
                $logBox.AppendText("  Expected: $xboxEspVoiceFolder`r`n")
                if (Test-Path $xboxEsmVoiceFolder) {
                    $logBox.AppendText("  Existing Xbox ESM left untouched:`r`n    $xboxEsmVoiceFolder`r`n")
                } else {
                    $logBox.AppendText("  No Xbox ESM exists either; nothing to change.`r`n")
                }
            }
        }

        $logBox.AppendText("=====================================`r`n")

        if ($DoRebuildAchlist) {
            $achlistPath = [System.IO.Path]::ChangeExtension($InputPath, ".achlist")
            $logBox.AppendText("Attempting achlist rebuild using:`r`n  $achlistPath`r`n")
            Rebuild-AchlistFromEsmVoice -AchlistPath $achlistPath -DataRoot $DataRoot -ModName $modName -PcEsmVoiceFolder $pcEsmVoiceFolder -LogBox $logBox
            $logBox.AppendText("=====================================`r`n")
        } else {
            $logBox.AppendText("achlist rebuild not selected; skipping.`r`n")
        }
    }
    catch {
        $logBox.AppendText("ERROR (Voice Management): $($_.Exception.Message)`r`n")
        [System.Windows.Forms.MessageBox]::Show("Voice management error:`r`n$($_.Exception.Message)", "Error", 'OK', 'Error') | Out-Null
    }
}

function Invoke-DmmdepsGeneration {
    param(
        [string]$DmmdepsPath,
        [string]$InputPath,
        [string]$DataFolder,
        [bool]$SmartClobber
    )

    if (-not (Test-Path $DmmdepsPath)) {
        [System.Windows.Forms.MessageBox]::Show("Dmmdeps.exe path is invalid:`r`n$DmmdepsPath", "Error", 'OK', 'Error') | Out-Null
        return $false
    }
    if (-not (Test-Path $DataFolder)) {
        [System.Windows.Forms.MessageBox]::Show("Data Folder does not exist:`r`n$DataFolder", "Error", 'OK', 'Error') | Out-Null
        return $false
    }

    # Determine the mod file to run dmmdeps against
    $modFile = $null
    $modName = [System.IO.Path]::GetFileNameWithoutExtension($InputPath)
    
    # If input is an achlist, we need to find the corresponding mod file
    if ($InputPath.ToLower().EndsWith(".achlist")) {
        $logBox.AppendText("Input is achlist, looking for corresponding mod file...`r`n")   
        
        # Look for .esp first, then .esm
        $espPath = Join-Path $DataFolder "$modName.esp"
        $esmPath = Join-Path $DataFolder "$modName.esm"
        
        if (Test-Path $espPath) {
            $modFile = $espPath
            $logBox.AppendText("Found ESP file: $espPath`r`n")
        } elseif (Test-Path $esmPath) {
            $modFile = $esmPath
            $logBox.AppendText("Found ESM file: $esmPath`r`n")
        } else {
            [System.Windows.Forms.MessageBox]::Show("Cannot run dmmdeps: No corresponding .esp or .esm file found for achlist`r`n$InputPath`r`n`r`nExpected:`r`n$espPath`r`nor`r`n$esmPath", "Missing Mod File", 'OK', 'Error') | Out-Null
            return $false
        }
    }
    # If input is .esp or .esm, use it directly
    elseif ($InputPath.ToLower().EndsWith(".esp") -or $InputPath.ToLower().EndsWith(".esm")) {
        if (-not (Test-Path $InputPath)) {
            [System.Windows.Forms.MessageBox]::Show("Input file not found:`r`n$InputPath", "Error", 'OK', 'Error') | Out-Null
            return $false
        }
        $modFile = $InputPath
        $logBox.AppendText("Using mod file directly: $modFile`r`n")
    }
    else {
        [System.Windows.Forms.MessageBox]::Show("Cannot run dmmdeps: Input file must be .achlist, .esp, or .esm`r`n$InputPath", "Invalid Input", 'OK', 'Error') | Out-Null
        return $false
    }

    $logBox.AppendText("=== Dmmdeps Generation ===`r`n")
    $logBox.AppendText("Dmmdeps : $DmmdepsPath`r`n")
    $logBox.AppendText("ModFile : $modFile`r`n")
    $logBox.AppendText("Data    : $DataFolder`r`n")
    $logBox.AppendText("-------------------------------------`r`n")

    $achlistPath = Join-Path $DataFolder "$modName.achlist"
    $csvPath = Join-Path $DataFolder "$($modName)_deps.csv"

    try {
        # Run dmmdeps to generate achlist and CSV
        $logBox.AppendText("Running dmmdeps to generate achlist and CSV...`r`n")
        
        # Change to Data folder before running dmmdeps
        $originalLocation = Get-Location
        Push-Location -Path $DataFolder
        
        $dmmdepsArgs = @(
            "`"$modFile`"",
            "--quiet"
        )

        if ($SmartClobber) {
            $dmmdepsArgs += "--Smartclobber"
        }
        
        $logBox.AppendText("Command: `"$DmmdepsPath`" $($dmmdepsArgs -join ' ')`r`n")
        
        $process = Start-Process -FilePath $DmmdepsPath -ArgumentList $dmmdepsArgs -Wait -PassThru -NoNewWindow
        
        Pop-Location
        Set-Location $originalLocation
        
        if ($process.ExitCode -ne 0) {
            $logBox.AppendText("ERROR: dmmdeps exited with code $($process.ExitCode)`r`n")
            return $false
        }
        
        # Check if files were generated
        if (-not (Test-Path $achlistPath)) {
            $logBox.AppendText("ERROR: achlist not generated at $achlistPath`r`n")
            return $false
        }
        if (-not (Test-Path $csvPath)) {
            $logBox.AppendText("ERROR: CSV not generated at $csvPath`r`n")
            return $false
        }
        
        $logBox.AppendText("Successfully generated:`r`n")
        $logBox.AppendText("  achlist: $achlistPath`r`n")
        $logBox.AppendText("  CSV    : $csvPath`r`n")
        
        return $true
    }
    catch {
        Pop-Location
        Set-Location $originalLocation
        $logBox.AppendText("ERROR running dmmdeps: $($_.Exception.Message)`r`n")
        return $false
    }
}


function Invoke-Ba2Archives {
    param(
        [string]$AchlistPath,
        [string]$DataFolder,
        [string]$XboxDataPath,
        [string]$ArchiverPath,
        [bool]$DoXbox,
        [bool]$DoWindows,
        [bool]$DoSort
    )

    if (-not $DoXbox -and -not $DoWindows) { return }

    if (-not (Test-Path $AchlistPath)) {
        [System.Windows.Forms.MessageBox]::Show("Achlist file not found:`r`n$AchlistPath", "Error", 'OK', 'Error') | Out-Null
        return
    }
    if (-not (Test-Path $DataFolder)) {
        [System.Windows.Forms.MessageBox]::Show("Data Folder does not exist:`r`n$DataFolder", "Error", 'OK', 'Error') | Out-Null
        return
    }
    if (-not (Test-Path $XboxDataPath)) {
        [System.Windows.Forms.MessageBox]::Show("Xbox Data Path does not exist:`r`n$XboxDataPath", "Error", 'OK', 'Error') | Out-Null
        return
    }
    if (-not (Test-Path $ArchiverPath)) {
        [System.Windows.Forms.MessageBox]::Show("Archiver2.exe path is invalid:`r`n$ArchiverPath", "Error", 'OK', 'Error') | Out-Null
        return
    }

    $logBox.AppendText("=== BA2 Archive Management ===`r`n")
    $logBox.AppendText("Achlist : $AchlistPath`r`n")
    $logBox.AppendText("Data    : $DataFolder`r`n")
    $logBox.AppendText("Xbox    : $XboxDataPath`r`n")
    $logBox.AppendText("Archiver: $ArchiverPath`r`n")
    $logBox.AppendText("XboxArchive   : $DoXbox`r`n")
    $logBox.AppendText("WindowsArchive: $DoWindows`r`n")
    $logBox.AppendText("SortAchlist   : $DoSort`r`n")
    $logBox.AppendText("-------------------------------------`r`n")

    # Load achlist JSON (always use achlist for BA2 creation)
    $rawJson  = Get-Content $AchlistPath -Raw
    $jsonData = $rawJson | ConvertFrom-Json
    if ($null -eq $jsonData) {
        $jsonData = @()
    } elseif ($jsonData -isnot [System.Collections.IEnumerable] -or $jsonData -is [string]) {
        $jsonData = @($jsonData)
    }

    $achlistModified = $false

    # Add .psc entries to achlist if checkbox is checked
    if ($includeSourceScriptsCheckbox.Checked) {
        $pscToAdd = @()

        foreach ($item in $jsonData) {
            # Match: Data\Scripts\...\Whatever.pex
            if ($item -match '^Data\\Scripts\\(.+)\.pex$') {
                $subPath    = $Matches[1]                         # CommunityShare\DebugMenuFramework\dmfMagicEffect
                $pscRel     = "Scripts\Source\$subPath.psc"       # Scripts\Source\CommunityShare\...\dmfMagicEffect.psc
                $pscFull    = Join-Path $DataFolder $pscRel
                $pscAchlist = "Data\$pscRel"

                if ((Test-Path $pscFull) -and
                    ($jsonData -notcontains $pscAchlist) -and
                    ($pscToAdd -notcontains $pscAchlist)) {

                    $pscToAdd += $pscAchlist
                    $logBox.AppendText("Include PSC in achlist: $pscAchlist`r`n")
                }
            }
        }

        if ($pscToAdd.Count -gt 0) {
            $jsonData       += $pscToAdd
            $achlistModified = $true
        }
    }

    # Optional sort
    if ($DoSort -and $jsonData.Count -gt 0) {
        $jsonData        = $jsonData | Sort-Object
        $achlistModified = $true
    }

    # Persist any changes (psc additions and/or sort)
    if ($achlistModified) {
        Write-AchlistProper -Items $jsonData -OutPath $AchlistPath
        $logBox.AppendText("Achlist JSON updated and saved.`r`n")
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($AchlistPath)

    # Text list paths
    $xboxMainFile       = "$DataFolder\$baseName`_xboxMain.txt"
    $xboxTextureFile    = "$DataFolder\$baseName`_xboxTextures.txt"
    $windowsMainFile    = "$DataFolder\$baseName`_windowsMain.txt"
    $windowsTextureFile = "$DataFolder\$baseName`_windowsTextures.txt"

    $xboxMainContent       = @()
    $xboxTextureContent    = @()
    $windowsMainContent    = @()
    $windowsTextureContent = @()

    $hasWemFiles     = $false
    $hasTextureFiles = $false
    $hasBtdFiles     = $false
    $hasBk2Files     = $false

    foreach ($item in $jsonData) {
        $p   = $item -replace '/', '\\'
        $ext = [System.IO.Path]::GetExtension($p).ToLowerInvariant()

        $isTexture = ($p -match '^DATA\\Textures') -and ($ext -eq '.dds')
        $isWem     = ($ext -eq '.wem')
        $isPsc     = ($ext -eq '.psc')
        $isBtd     = ($ext -eq '.btd')
        $isBk2     = ($ext -eq '.bk2')

        if ($isBtd) {
            $hasBtdFiles = $true
        }
        elseif ($isBk2) {
            $hasBk2Files = $true
        }

        if ($isTexture) {
            $hasTextureFiles      = $true
            $windowsTextureContent += $p
            $xboxTextureContent    += ($p -replace '^DATA\\Textures', "$XboxDataPath\Textures")
        }
        elseif ($isWem) {
            $hasWemFiles          = $true
            $windowsMainContent   += $p
            $xboxMainContent      += ($p -replace '^DATA', $XboxDataPath)
        }
        elseif ($isPsc) {
            # Only include .psc in Windows (PC) archive and only if checkbox is on
            if ($includeSourceScriptsCheckbox.Checked) {
                $windowsMainContent += $p
            }
        }
        else {
            $windowsMainContent += $p
            $xboxMainContent    += $p
        }
    }

    # Write the text lists
    $xboxMainContent    | Set-Content -Path $xboxMainFile -Encoding ASCII
    $windowsMainContent | Set-Content -Path $windowsMainFile -Encoding ASCII

    if ($hasTextureFiles) {
        $xboxTextureContent    | Set-Content -Path $xboxTextureFile -Encoding ASCII
        $windowsTextureContent | Set-Content -Path $windowsTextureFile -Encoding ASCII
    }

    $logBox.AppendText("Windows and Xbox archive list files written.`r`n")

    # Compression: if we have any wem, btd, or bk2 at all, use None, else Default
    $compressionType = if ($hasWemFiles -or $hasBtdFiles -or $hasBk2Files) { "None" } else { "Default" }

    # BA2 output names
    $xboxMainBa2        = "$DataFolder\$baseName - Main_xbox.ba2"
    $xboxTexturesBa2    = "$DataFolder\$baseName - Textures_xbox.ba2"
    $windowsMainBa2     = "$DataFolder\$baseName - Main.ba2"
    $windowsTexturesBa2 = "$DataFolder\$baseName - Textures.ba2"

    # Run Archive2 from Starfield root so "Data\..." resolves
    $originalLocation = Get-Location
    try {
        $starfieldRoot = Split-Path $DataFolder   # parent of ...\Data
        $logBox.AppendText("Setting working folder for Archive2 to: $starfieldRoot`r`n")
        Push-Location -Path $starfieldRoot

        if ($DoXbox) {
            $logBox.AppendText("Running Archiver2 for Xbox main archive...`r`n")
            Invoke-Expression "& '$ArchiverPath' -create='$xboxMainBa2' -sourceFile='$xboxMainFile' -format=General -compression=$compressionType"

            if ($hasTextureFiles) {
                $logBox.AppendText("Running Archiver2 for Xbox textures archive...`r`n")
                Invoke-Expression "& '$ArchiverPath' -create='$xboxTexturesBa2' -sourceFile='$xboxTextureFile' -format=XBoxDDS -compression=XBox"
            }
        }

        if ($DoWindows) {
            $logBox.AppendText("Running Archiver2 for Windows main archive...`r`n")
            Invoke-Expression "& '$ArchiverPath' -create='$windowsMainBa2' -sourceFile='$windowsMainFile' -format=General -compression=$compressionType"

            if ($hasTextureFiles) {
                $logBox.AppendText("Running Archiver2 for Windows textures archive...`r`n")
                Invoke-Expression "& '$ArchiverPath' -create='$windowsTexturesBa2' -sourceFile='$windowsTextureFile' -format=DDS -compression=Default"
            }
        }
    }
    finally {
        Pop-Location | Out-Null
        Set-Location $originalLocation
    }

    $logBox.AppendText("BA2 archive processing complete.`r`n")
}

# -------------------------------------------------------------------
#  UI pump helper for long operations
# -------------------------------------------------------------------
function Invoke-UiPump {
    [System.Windows.Forms.Application]::DoEvents()
}

function New-ZipFromFolder {
    param(
        [string]$SourceFolder,
        [string]$ZipPath,
        [System.Windows.Forms.TextBox]$LogBox,
        [System.Windows.Forms.ProgressBar]$ProgressBar,
        [string]$ExcludeFolder  # <= NEW (optional)
    )

    # Make sure both compression assemblies are loaded
    Add-Type -AssemblyName 'System.IO.Compression'
    Add-Type -AssemblyName 'System.IO.Compression.FileSystem'

    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }

    # Normalize exclude path (if provided)
    if ($ExcludeFolder) {
        $ExcludeFolder = [System.IO.Path]::GetFullPath($ExcludeFolder).TrimEnd('\','/')
    }

    # Collect all files once so we can show real progress, excluding backup folder if requested
    $files = Get-ChildItem -Path $SourceFolder -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            if (-not $ExcludeFolder) { return $true }
            $full = [System.IO.Path]::GetFullPath($_.FullName)
            # Skip anything under the backup folder
            return (-not $full.StartsWith($ExcludeFolder, [System.StringComparison]::OrdinalIgnoreCase))
        }

    $total = $files.Count
    if ($total -lt 1) {
        $LogBox.AppendText("ZIP: No files found under $SourceFolder (after exclusions).`r`n")
        return
    }

    $ProgressBar.Style   = 'Blocks'
    $ProgressBar.Minimum = 0
    $ProgressBar.Maximum = $total
    $ProgressBar.Value   = 0

    $LogBox.AppendText("ZIP: $total files to compress.`r`n")
    Invoke-UiPump   # let UI breathe before the heavy loop

    $zipFileStream = [System.IO.File]::Open($ZipPath, [System.IO.FileMode]::Create)
    try {
        $zipMode    = [System.IO.Compression.ZipArchiveMode]::Create
        $zipArchive = New-Object System.IO.Compression.ZipArchive($zipFileStream, $zipMode, $false)

        $i = 0
        foreach ($file in $files) {
            # Relative path inside the zip
            $relPath = $file.FullName.Substring($SourceFolder.Length).TrimStart('\','/')
            # Use Optimal compression
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zipArchive,
                $file.FullName,
                $relPath,
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null

            $i++
            if ($i -le $ProgressBar.Maximum) {
                $ProgressBar.Value = $i
            }

            if (($i % 10) -eq 0) {
                Invoke-UiPump
            }
        }

        $LogBox.AppendText("ZIP: Completed compression of $i files.`r`n")
    }
    finally {
        if ($zipArchive) { $zipArchive.Dispose() }
        $zipFileStream.Dispose()
        Invoke-UiPump
    }
}

function Write-AchlistProper {
    param(
        [string[]]$Items,
        [string]$OutPath
    )

    # ConvertTo-Json pretty format produces newline-separated entries.
    $json = $Items | ConvertTo-Json -Depth 5

    # CK requires a CRLF after each line except the final bracket.
    # ConvertTo-Json already includes newlines but Set-Content sometimes normalizes them.
    # Use Out-File a la your working scripts.
    $json | Out-File -FilePath $OutPath -Encoding ascii
}


# Junction targets collected during backup for AF
$script:junctionTargets = @{}
$script:lastRunInputPath   = $null
$script:lastRunModName     = $null
$script:lastRunAchlistPath = $null
$script:stopwatch = [System.Diagnostics.Stopwatch]::new()

function Invoke-Backup {
    param(
        [string]$InputPath,
        [string]$BackupRoot,
        [string]$DataRoot,
        [string]$XboxRoot,
        [bool]$DoCopy,
        [bool]$DoZip,
        [bool]$DoClean
    )

    if (-not $DoCopy -and -not $DoZip -and -not $DoClean) { return }

    if (-not ($DataRoot -like "*\Data")) {
        [System.Windows.Forms.MessageBox]::Show("Game Data Folder must end with 'Data'", "Invalid Path", 'OK', 'Error')
        return
    }
    if (-not ($XboxRoot -like "*\Data")) {
        [System.Windows.Forms.MessageBox]::Show("Xbox Data Folder must end with 'Data'", "Invalid Path", 'OK', 'Error')
        return
    }

    $modName = [System.IO.Path]::GetFileNameWithoutExtension($InputPath)
    $achlistPath = Join-Path -Path $DataRoot -ChildPath "$modName.achlist"
    if (-not (Test-Path $achlistPath)) {
        [System.Windows.Forms.MessageBox]::Show("Achlist file not found at: $achlistPath", "Missing File", 'OK', 'Error')
        return
    }

    $logBox.AppendText("=== Backup Management ===`r`n")
    $logBox.AppendText("Mod     : $modName`r`n")
    $logBox.AppendText("Input   : $InputPath`r`n")
    $logBox.AppendText("Backup  : $BackupRoot`r`n")
    $logBox.AppendText("Data    : $DataRoot`r`n")
    $logBox.AppendText("Xbox    : $XboxRoot`r`n")
    $logBox.AppendText("DoCopy  : $DoCopy`r`n")
    $logBox.AppendText("DoZip   : $DoZip`r`n")
    $logBox.AppendText("DoClean : $DoClean`r`n")
    $logBox.AppendText("-------------------------------------`r`n")

    $progressBar.Value = 0

    $modBackupRoot = Join-Path $BackupRoot $modName

    # Clean everything except "backup" folder
    if ($DoClean -and (Test-Path $modBackupRoot)) {
        $logBox.AppendText("Clean copy enabled - removing all folders except 'backup' and all files in root under:`r`n  $modBackupRoot`r`n")

        # Remove all directories except "backup"
        Get-ChildItem -Path $modBackupRoot -Directory | Where-Object { $_.Name -ne "backup" } | ForEach-Object {
            $logBox.AppendText("  REMOVE DIR: $($_.FullName)`r`n")
            Remove-Item -Path $_.FullName -Recurse -Force
        }
        
        # Remove all files in the root directory
        Get-ChildItem -Path $modBackupRoot -File | ForEach-Object {
            $logBox.AppendText("  REMOVE FILE: $($_.FullName)`r`n")
            Remove-Item -Path $_.FullName -Force
        }
    }

    if ($DoCopy) {
        # base mod files (.esm/.esp/.ba2/.txt/.achlist)
        $basePath = $DataRoot
        Get-ChildItem -Path $basePath -Filter "$modName*" -Recurse -File |
            Where-Object { $_.Extension -in '.esm', '.esp', '.ba2', '.txt', '.achlist' } |
            ForEach-Object {
                $src = $_.FullName
                $dst = Join-Path $modBackupRoot $_.Name
                New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
                Copy-Item -Path $src -Destination $dst -Force
                $logBox.AppendText("COPY: $src => $dst`r`n")
            }

        $jsonData = Get-Content $achlistPath | ConvertFrom-Json
        $total = $jsonData.Count
        $progressBar.Maximum = [math]::Max(1, $total)
        $progressBar.Value = 0
        $script:junctionTargets = @{}
        $terrainChunks = @{}
        $count = 0
        foreach ($item in $jsonData) {
            $item     = $item -replace '/', '\'
            $relPath  = $item -replace '^DATA\\', ''
            $relLower = $relPath.ToLowerInvariant()

            $srcPath = Join-Path -Path $DataRoot -ChildPath $relPath
            $dstPC   = Join-Path -Path (Join-Path $modBackupRoot "LOOSEFILES\Data") -ChildPath $relPath

            $logBox.AppendText("CHECK [Main Copy]: $srcPath`r`n")

            # Track ESM folder for AF junctions
            if ($item -match "\\$modName\.esm\\") {
                $fullFolder = Split-Path (Join-Path $DataRoot ($item -replace '^DATA\\', '')) -Parent
                if (-not $script:junctionTargets.ContainsKey($fullFolder)) {
                    $script:junctionTargets[$fullFolder] = $true
                }
            }

            # Always copy PC version into loose_pc
            if (Test-Path $srcPath) {
                New-Item -ItemType Directory -Force -Path (Split-Path $dstPC) | Out-Null
                Copy-Item -Path $srcPath -Destination $dstPC -Force
                $logBox.AppendText("COPY: $srcPath => $dstPC`r`n")
                
                # Track terrain chunks for TIF overlay copying
                if ($relLower.EndsWith('.btd') -and $relPath -like "terrain\*") {
                    $chunkName = [System.IO.Path]::GetFileNameWithoutExtension($srcPath)
                    $terrainChunks[$chunkName] = $true
                    $logBox.AppendText("TRACK [Terrain Chunk]: $chunkName`r`n")
                }
            }

            # --- Xbox extras ---

            # 1) For .wem: also copy Xbox version into loose_xbox
            if ($relLower.EndsWith('.wem')) {
                $dstXbox = Join-Path (Join-Path $modBackupRoot "LOOSEFILES\XBOX\Data") $relPath
                $srcXbox = Join-Path $XboxRoot $relPath
                $logBox.AppendText("CHECK [Xbox WEM]: $srcXbox`r`n")
                if (Test-Path $srcXbox) {
                    New-Item -ItemType Directory -Force -Path (Split-Path $dstXbox) | Out-Null
                    Copy-Item -Path $srcXbox -Destination $dstXbox -Force
                    $logBox.AppendText("COPY: $srcXbox => $dstXbox`r`n")
                }
            }

            # 2) For .dds textures: also copy Xbox version into loose_xbox
            if ($relLower.EndsWith('.dds') -and $relLower.StartsWith('textures\')) {
                $dstXboxTex = Join-Path (Join-Path $modBackupRoot "LOOSEFILES\XBOX\Data") $relPath
                $srcXboxTex = Join-Path $XboxRoot $relPath   # XboxRoot is the Data folder
                $logBox.AppendText("CHECK [Xbox DDS]: $srcXboxTex`r`n")
                if (Test-Path $srcXboxTex) {
                    New-Item -ItemType Directory -Force -Path (Split-Path $dstXboxTex) | Out-Null
                    Copy-Item -Path $srcXboxTex -Destination $dstXboxTex -Force
                    $logBox.AppendText("COPY: $srcXboxTex => $dstXboxTex`r`n")
                } else {
                    $logBox.AppendText("SKIP [Xbox DDS missing]: $srcXboxTex`r`n")
                }
            }

            # PSC sources for any PEX
            if ($relLower.EndsWith('.pex') -and $relPath -like "Scripts\*") {
                $relSubPath = $relPath -replace '^Scripts\\', ''
                $pscRel     = "Scripts\Source\" + $relSubPath.Replace('.pex', '.psc')
                $srcPSC     = Join-Path $DataRoot $pscRel
                $dstPSC     = Join-Path (Join-Path $modBackupRoot "LOOSEFILES\Data") $pscRel
                $logBox.AppendText("CHECK [PSC Source]: $srcPSC`r`n")
                if (Test-Path $srcPSC) {
                    New-Item -ItemType Directory -Force -Path (Split-Path $dstPSC) | Out-Null
                    Copy-Item -Path $srcPSC -Destination $dstPSC -Force 
                    $logBox.AppendText("COPY: $srcPSC => $dstPSC`r`n")
                }
            }



            $count++
            if ($count -le $progressBar.Maximum) {
                $progressBar.Value = $count
            }
        }
        # --- Also capture PC ESP voice tree (source WAVs), excluding WEM/FFX ---
        $pcEspVoicePath = Join-Path $DataRoot ("sound\voice\{0}.esp" -f $modName)
        if (Test-Path $pcEspVoicePath) {
            $dstPcEspVoice = Join-Path (Join-Path $modBackupRoot "LOOSEFILES\Data") ("sound\voice\{0}.esp" -f $modName)
            
            $logBox.AppendText(
                "COPY [PC ESP Voice Tree, excluding WEM/FFX] (thinking, GUI may freeze briefly):`r`n" +
                "  $pcEspVoicePath`r`n" +
                "  =>  $dstPcEspVoice`r`n"
            )

            # Optional: update status line so it’s obvious what’s happening
            $statusLabel.Text = "Copying PC ESP voice tree (GUI may freeze briefly)..."
            
            Invoke-UiPump

            New-Item -ItemType Directory -Force -Path (Split-Path $dstPcEspVoice) | Out-Null
            Copy-Item -Path $pcEspVoicePath -Destination $dstPcEspVoice -Recurse -Force -Exclude '*.wem','*.ffxanim'

            $logBox.AppendText("Finished ESP voice copy.`r`n")
        }
        else {
            $logBox.AppendText("SKIP: No PC ESP voice folder found:`r`n  $pcEspVoicePath`r`n")
        }
        # --- EXTRA: Terrain\<modname> folder from Data ---
        $terrainModFolder = Join-Path $DataRoot ("terrain\{0}" -f $modName)
        if (Test-Path $terrainModFolder) {
            $dstTerrainParent = Join-Path (Join-Path $modBackupRoot "LOOSEFILES\Data") "terrain"
            New-Item -ItemType Directory -Force -Path $dstTerrainParent | Out-Null
            $logBox.AppendText("COPY [Terrain Mod Folder]: $terrainModFolder => $dstTerrainParent`r`n")
            Copy-Item -Path $terrainModFolder -Destination $dstTerrainParent -Recurse -Force
        } else {
            $logBox.AppendText("SKIP: No Data\terrain\$modName folder found.`r`n")
        }

        # --- EXTRA: Terrain overlay TIFs from Source\TGATextures\Terrain\OverlayMasks ---
        if ($terrainChunks.Count -gt 0) {
            
            $overlayRoot = Join-Path $DataRoot "Source\TGATextures\Terrain\OverlayMasks"
            foreach ($chunkName in $terrainChunks.Keys) {
                $srcTif = Join-Path $overlayRoot ("{0}.tif" -f $chunkName)
                if (Test-Path $srcTif) {
                    $dstOverlayRoot = Join-Path $modBackupRoot "LOOSEFILES\Data\Source\TGATextures\Terrain\OverlayMasks"
                    New-Item -ItemType Directory -Force -Path $dstOverlayRoot | Out-Null
                    $dstTif = Join-Path $dstOverlayRoot ([System.IO.Path]::GetFileName($srcTif))
                    $logBox.AppendText("COPY [Terrain Overlay TIF]: $srcTif => $dstTif`r`n")
                    Copy-Item -Path $srcTif -Destination $dstTif -Force
                }
                else {
                    $logBox.AppendText("SKIP [Overlay TIF missing]: $srcTif`r`n")
                }
            }
        }
        else {
            $logBox.AppendText("No terrain BTD chunks tracked; skipping overlay TIF copy.`r`n")
        }



    }

    if ($DoZip) {
        try {
            $zipTime = Get-Date -Format "yyyyMMdd_HHmmss"
            $zipTarget = Join-Path -Path $modBackupRoot -ChildPath "backup"
            if (-not (Test-Path $zipTarget)) {
                New-Item -ItemType Directory -Force -Path $zipTarget | Out-Null
            }
            $zipName = "$modName-$zipTime.zip"
            $zipPath = Join-Path $zipTarget $zipName
            $sourceFolder = $modBackupRoot

            $logBox.AppendText("Creating zip:`r`n  Source: $sourceFolder`r`n  Dest  : $zipPath`r`n")

            $excludeFolder = $zipTarget   # <- do NOT zip previous backups
            New-ZipFromFolder -SourceFolder $sourceFolder `
                              -ZipPath $zipPath `
                              -LogBox $logBox `
                              -ProgressBar $progressBar `
                              -ExcludeFolder $excludeFolder

            $logBox.AppendText("Zip created.`r`n")
        }
        catch {
            $logBox.AppendText("ZIP ERROR: $($_.Exception.Message)`r`n")
        }
    }
    # Open the backup destination folder in Explorer
    if (Test-Path $modbackupRoot) {
        $logBox.AppendText("Opening backup folder in Explorer:`r`n  $modBackupRoot`r`n")
        Start-Process "explorer.exe" -ArgumentList $modBackupRoot
    }
}  # <-- closes Invoke-Backup

function Invoke-BackupFromCsv {
    param(
        [string]$InputPath,
        [string]$BackupRoot,
        [string]$DataRoot,
        [string]$XboxRoot,
        [string]$CsvPath,
        [bool]$DoCopy,
        [bool]$DoZip,
        [bool]$DoClean
    )

    if (-not $DoCopy -and -not $DoZip -and -not $DoClean) { return }

    if (-not (Test-Path $CsvPath)) {
        $logBox.AppendText("CSV file not found: $CsvPath`r`n")
        return
    }

    $modName = [System.IO.Path]::GetFileNameWithoutExtension($InputPath)
    
    $logBox.AppendText("=== Backup Management (CSV-based) ===`r`n")
    $logBox.AppendText("Mod     : $modName`r`n")
    $logBox.AppendText("CSV     : $CsvPath`r`n")
    $logBox.AppendText("Backup  : $BackupRoot`r`n")
    $logBox.AppendText("Data    : $DataRoot`r`n")
    $logBox.AppendText("Xbox    : $XboxRoot`r`n")
    $logBox.AppendText("-------------------------------------`r`n")

    $modBackupRoot = Join-Path $BackupRoot $modName

    # Clean everything except "backup" folder
    if ($DoClean -and (Test-Path $modBackupRoot)) {
        $logBox.AppendText("Clean copy enabled - removing all folders except 'backup' and all files in root under:`r`n  $modBackupRoot`r`n")
        
        # Remove all directories except "backup"
        Get-ChildItem -Path $modBackupRoot -Directory -Force | Where-Object { $_.Name -ne "backup" } | ForEach-Object {
            $logBox.AppendText("  REMOVE DIR: $($_.FullName)`r`n")
            try {
                Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction Stop
                $logBox.AppendText("    SUCCESS: Directory removed`r`n")
            } catch {
                $logBox.AppendText("    ERROR: Failed to remove directory - $($_.Exception.Message)`r`n")
            }
        }
        
        # Remove all files in the root directory (including hidden/system files)
        Get-ChildItem -Path $modBackupRoot -File -Force | ForEach-Object {
            $logBox.AppendText("  REMOVE FILE: $($_.FullName)`r`n")
            try {
                # Remove read-only attribute if present
                if ($_.IsReadOnly) {
                    $_.IsReadOnly = $false
                }
                Remove-Item -Path $_.FullName -Force -ErrorAction Stop
                $logBox.AppendText("    SUCCESS: File removed`r`n")
            } catch {
                $logBox.AppendText("    ERROR: Failed to remove file - $($_.Exception.Message)`r`n")
            }
        }
        
        $logBox.AppendText("Cleanup completed.`r`n")
    }

    if ($DoCopy) {
        # Reset junction targets for this backup run so AF creation
        # uses only folders discovered from the current CSV processing.
        $script:junctionTargets = @{}
        # Copy base mod files (.esm/.esp/.ba2/.txt/.achlist)
        $basePath = $DataRoot
        Get-ChildItem -Path $basePath -Filter "$modName*" -File |
            Where-Object { $_.Extension -in '.esm', '.esp', '.ba2', '.txt', '.achlist' } |
            ForEach-Object {
                $src = $_.FullName
                $dst = Join-Path $modBackupRoot $_.Name
                New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
                Copy-Item -Path $src -Destination $dst -Force
                $logBox.AppendText("COPY: $src => $dst`r`n")
            }

        # Process CSV file
        try {
            $csvData = Import-Csv -Path $CsvPath
            $total = $csvData.Count
            $progressBar.Maximum = [math]::Max(1, $total)
            $progressBar.Value = 0
            $count = 0
            $copiedPC = 0
            $copiedXbox = 0
            $GameRoot = Split-Path $DataRoot -Parent
            foreach ($row in $csvData) {
                # PC path copy
                if ($row.pcpath -and $row.pcpath.Trim()) {
                    $pcRaw = $row.pcpath.Trim()

                    $srcPath = $null
                    $dstPC   = $null

                    # If dmmdeps provided a full absolute path (e.g. Source\TGATextures)
                    if ($pcRaw -match '^[a-zA-Z]:\\' -or $pcRaw.StartsWith("\\")) {
                        $srcPath = $pcRaw

                        # Special handling for TGATextures assets: place under LOOSEFILES\TGATextures
                        $lower  = $pcRaw.ToLowerInvariant()
                        $marker = "\tgatextures\"
                        $idx    = $lower.IndexOf($marker)
                        if ($idx -ge 0) {
                            $relUnderTga = $pcRaw.Substring($idx + $marker.Length)
                            $dstRoot     = Join-Path $modBackupRoot "LOOSEFILES\TGATextures"
                            $dstPC       = Join-Path $dstRoot $relUnderTga
                        }
                        else {
                            # Absolute path without a TGATextures segment – log and skip to avoid bad copies
                            $logBox.AppendText("SKIP [PC]: Absolute pcpath without TGATextures marker: $pcRaw`r`n")
                        }
                    }
                    else {
                        # Original behavior for Data-relative or other relative paths
                        $relPath = $pcRaw
                        if ($relPath.StartsWith("Data\", [System.StringComparison]::OrdinalIgnoreCase)) {
                            $relPath = $relPath.Substring(5)
                        }
                        $srcPath = Join-Path $DataRoot $relPath
                        $dstPC   = Join-Path (Join-Path $modBackupRoot "LOOSEFILES\Data") $relPath
                    }

                    if ($srcPath -and $dstPC) {
                        $logBox.AppendText("COPY [PC]: $srcPath => $dstPC`r`n")
                        New-Item -ItemType Directory -Force -Path (Split-Path $dstPC) | Out-Null
                        Copy-Item -Path $srcPath -Destination $dstPC -Force
                        $copiedPC++

                        # Track ESM folders for AF junction creation, mirroring
                        # the achlist-based backup logic. We normalize the CSV
                        # pcpath to a Data-relative form so we can detect
                        # "\\<modName>.esm\\" segments.
                        $achItem = $pcRaw.Replace('/', '\\')
                        if (-not $achItem.StartsWith("Data\\", [System.StringComparison]::OrdinalIgnoreCase)) {
                            $achItem = "Data\\" + $achItem.TrimStart('\\')
                        }

                        if ($achItem -match "\\$modName\.esm\\") {
                            $relFromData = $achItem -replace '^DATA\\', ''
                            $fullFolder  = Split-Path (Join-Path $DataRoot $relFromData) -Parent
                            if (-not $script:junctionTargets.ContainsKey($fullFolder)) {
                                $script:junctionTargets[$fullFolder] = $true
                            }
                        }
                    }
                }
                # Xbox path copy
                if ($row.xboxpath -and $row.xboxpath.Trim()) {
                    $xboxPathRaw = $row.xboxpath.Trim()
                    $srcXbox = Join-Path $GameRoot $xboxPathRaw
                    $dstXbox = Join-Path (Join-Path $modBackupRoot "LOOSEFILES") $xboxPathRaw
                    $logBox.AppendText("COPY [Xbox]: $srcXbox => $dstXbox`r`n")
                    New-Item -ItemType Directory -Force -Path (Split-Path $dstXbox) | Out-Null
                    Copy-Item -Path $srcXbox -Destination $dstXbox -Force
                    $copiedXbox++
                }
                $count++
                if ($count -le $progressBar.Maximum) {
                    $progressBar.Value = $count
                }
            }
            $logBox.AppendText("CSV processing complete. Processed $count items, copied $copiedPC PC files, $copiedXbox Xbox files.`r`n")
        }
        catch {
            $logBox.AppendText("ERROR processing CSV: $($_.Exception.Message)`r`n")
        }
    }

    # Zip creation (same as original function)
    if ($DoZip) {
        try {
            $zipTime = Get-Date -Format "yyyyMMdd_HHmmss"
            $zipTarget = Join-Path -Path $modBackupRoot -ChildPath "backup"
            if (-not (Test-Path $zipTarget)) {
                New-Item -ItemType Directory -Force -Path $zipTarget | Out-Null
            }
            $zipName = "$modName-$zipTime.zip"
            $zipPath = Join-Path $zipTarget $zipName
            $sourceFolder = $modBackupRoot

            $logBox.AppendText("Creating zip:`r`n  Source: $sourceFolder`r`n  Dest  : $zipPath`r`n")

            $excludeFolder = $zipTarget
            New-ZipFromFolder -SourceFolder $sourceFolder `
                              -ZipPath $zipPath `
                              -LogBox $logBox `
                              -ProgressBar $progressBar `
                              -ExcludeFolder $excludeFolder

            $logBox.AppendText("Zip created.`r`n")
        }
        catch {
            $logBox.AppendText("ZIP ERROR: $($_.Exception.Message)`r`n")
        }
    }
    
    # Open the backup destination folder in Explorer
    if (Test-Path $modBackupRoot) {
        $logBox.AppendText("Opening backup folder in Explorer:`r`n  $modBackupRoot`r`n")
        Start-Process "explorer.exe" -ArgumentList $modBackupRoot
    }
}

# -------------------------------------------------------------------
#  Make AF Version button (always normalize to use the achlist)
# -------------------------------------------------------------------
$afButton.Add_Click({
    # Input can be .achlist, .esm, .esp, or .ba2. For AF generation we
    # *always* want to operate on the JSON achlist, not on the plugin
    # bytes themselves.
    $inputPath = if ($script:lastRunInputPath) { $script:lastRunInputPath } else { $inputBox.Text }
    if (-not $inputPath) {
        $logBox.AppendText("ERROR: Input file is empty.`r`n")
        return
    }

    $modName = if ($script:lastRunModName) { $script:lastRunModName } else { [System.IO.Path]::GetFileNameWithoutExtension($inputPath) }
    if ($modName -match '_AF$') {
        $logBox.AppendText("Already AF version, skipping AF generation.`r`n")
        return
    }

    # Resolve the *source* achlist path. If the selected input is
    # already an achlist, use it directly; otherwise look for
    # <modName>.achlist under the configured Data folder.
    $originalAchlistPath = $null
    if ($script:lastRunAchlistPath -and (Test-Path $script:lastRunAchlistPath)) {
        $originalAchlistPath = $script:lastRunAchlistPath
    }
    elseif ($inputPath.ToLower().EndsWith('.achlist')) {
        $originalAchlistPath = $inputPath
    }
    else {
        if (-not $dataBox.Text) {
            $logBox.AppendText("ERROR: Data folder is not set; cannot locate original achlist for AF generation.`r`n")
            [System.Windows.Forms.MessageBox]::Show(
                "Data folder is not set; cannot locate <mod>.achlist for AF generation.",
                "Missing Data Folder", "OK", "Error"
            ) | Out-Null
            return
        }

        $candidateAch = Join-Path $dataBox.Text ("{0}.achlist" -f $modName)
        if (Test-Path $candidateAch) {
            $originalAchlistPath = $candidateAch
        }
    }

    if (-not $originalAchlistPath -or -not (Test-Path $originalAchlistPath)) {
        $logBox.AppendText("ERROR: Could not find original achlist for AF generation.`r`n")
        [System.Windows.Forms.MessageBox]::Show(
            "Could not find the original .achlist for this mod. Select the .achlist as the input file or ensure it exists in the Data folder.",
            "Missing Achlist", "OK", "Error"
        ) | Out-Null
        return
    }

    $outputFolder = Split-Path $originalAchlistPath
    $afModName = "${modName}_AF"
    $afAchlistPath = Join-Path $outputFolder "$afModName.achlist"

    # 1. Write AF achlist (modname.esm → modname_AF.esm)
    $escapedModName = [Regex]::Escape($modName)

    $rawAchlist = Get-Content -Path $originalAchlistPath -Raw

    # Production rule: NO .esp references allowed in a production achlist
    if ($rawAchlist -match '(?i)\.esp\b') {
        $logBox.AppendText("ERROR: Achlist contains .esp references; production BA2 must be built from .esm only.`r`n")
        [System.Windows.Forms.MessageBox]::Show(
            "This achlist contains .esp references. Production BA2 builds must be based on .esm only.`r`n`r`nFix the source achlist, then re-run AF.",
            "Invalid Achlist (.esp found)", "OK", "Error"
        ) | Out-Null
        return
    }

    # Replace ONLY .esm references for this mod
    $rawAchlist = $rawAchlist -replace "${escapedModName}\.esm", "$afModName.esm"

    # Write JSON safely (don’t use ASCII)
    Set-Content -Path $afAchlistPath -Value $rawAchlist -Encoding UTF8

    $logBox.AppendText("Wrote new AF achlist to $afAchlistPath`r`n")

    # Copy ESM/ESP to AF equivalents in same folder
    $originalEsm = Join-Path $outputFolder "$modName.esm"
    $afEsm = Join-Path $outputFolder "$afModName.esm"
    if (Test-Path $originalEsm) {
        try {
            if (Test-Path $afEsm) {
                Remove-Item -Path $afEsm -Force
                $logBox.AppendText("Removed existing AF ESM: $afEsm`r`n")
            }
            #Copy-Item -Path $originalEsm -Destination $afEsm -Force
            New-Item -Path $afEsm -ItemType HardLink -Value $originalEsm | Out-Null
            $logBox.AppendText("Hardlinked ESM: $originalEsm => $afEsm`r`n")
        }
        catch {
            $logBox.AppendText("ERROR: Failed to create AF ESM hardlink: $afEsm`r`n  $($_.Exception.Message)`r`n")
            [System.Windows.Forms.MessageBox]::Show(
                "Failed to create AF ESM hardlink:`r`n$afEsm`r`n`r`n$($_.Exception.Message)",
                "AF Hardlink Failure", "OK", "Error"
            ) | Out-Null
            return
        }
    }

    $originalEsp = Join-Path $outputFolder "$modName.esp"
    $afEsp = Join-Path $outputFolder "$afModName.esp"
    if (Test-Path $originalEsp) {
        try {
            if (Test-Path $afEsp) {
                Remove-Item -Path $afEsp -Force
                $logBox.AppendText("Removed existing AF ESP: $afEsp`r`n")
            }
            #Copy-Item -Path $originalEsp -Destination $afEsp -Force
            New-Item -Path $afEsp -ItemType HardLink -Value $originalEsp | Out-Null
            $logBox.AppendText("Hardlinked ESP: $originalEsp => $afEsp`r`n")
        }
        catch {
            $logBox.AppendText("ERROR: Failed to create AF ESP hardlink: $afEsp`r`n  $($_.Exception.Message)`r`n")
            [System.Windows.Forms.MessageBox]::Show(
                "Failed to create AF ESP hardlink:`r`n$afEsp`r`n`r`n$($_.Exception.Message)",
                "AF Hardlink Failure", "OK", "Error"
            ) | Out-Null
            return
        }
    }

    # 2. Create junctions for any tracked esm folders (optional).
    # If no junction targets were recorded during backup, that's OK –
    # we still keep the AF hardlink master files, we just skip
    # directory junction creation.
    if ($script:junctionTargets.Count -eq 0) {
        $logBox.AppendText("No junction targets were found; skipping AF directory junction creation.`r`n")
    }
    else {
        foreach ($target in $script:junctionTargets.Keys) {
            $logBox.AppendText("CANDIDATE: $target`r`n")
            if ($target.ToLower() -match "\\$($modName.ToLower())\.esm\\") {
                $junctionName = $target -creplace "\\$([Regex]::Escape($modName)).esm\\", "\\${modName}_AF.esm\\"
                $junctionName = $junctionName -replace '\\\\+', '\\'
                $target       = $target       -replace '\\\\+', '\\'

                $jpParent = Split-Path $junctionName -Parent
                if (-not (Test-Path $jpParent)) {
                    New-Item -ItemType Directory -Force -Path $jpParent | Out-Null
                    $logBox.AppendText("CREATED PARENT DIR: $jpParent`r`n")
                }

                if (-not (Test-Path $junctionName)) {
                    $logBox.AppendText("RUN: mklink /J `"$junctionName`" `"$target`"`r`n")
                    cmd /c "mklink /J `"$junctionName`" `"$target`""
                    if ($LASTEXITCODE -ne 0) {
                        $logBox.AppendText(" ERROR: Failed to create junction: $junctionName => $target`r`n")
                        [System.Windows.Forms.MessageBox]::Show(
                            "Failed to create junction:`r`n$junctionName`r`n`r`nTarget:`r`n$target",
                            "mklink Failure", "OK", "Error"
                        ) | Out-Null
                        return
                    }
                    $logBox.AppendText("JUNCTION CREATED: $junctionName => $target`r`n")
                } else {
                    $logBox.AppendText("SKIP: Junction already exists: $junctionName`r`n")
                }
            }
        }
    }

    # 3. Run the normal pipeline using the AF achlist as the input
    $logBox.AppendText("Running normal pipeline using AF achlist...`r`n")
    $inputBox.Text = $afAchlistPath
    $runButton.PerformClick()
})

# -------------------------------------------------------------------
#  Run + Do All handlers
# -------------------------------------------------------------------
$runButton.Add_Click({
    # Start stopwatch
    $script:stopwatch.Restart()
    
    $statusLabel.Text = ""
    $logBox.AppendText("=====================================`r`n")
    $logBox.AppendText("RUN started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`r`n")
    $progressBar.Value = 0

    # Save config on run
    $cfgOut = @{
        InputFile        = $inputBox.Text
        BackupFolder     = $destBox.Text
        DataFolder       = $dataBox.Text
        XboxFolder       = $xboxBox.Text
        ArchiverPath     = $archiverBox.Text
        DmmdepsPath      = $dmmdepsBox.Text
        VoiceFolderUpdate= $voiceUpdateCheckbox.Checked
        RebuildAchlist   = $rebuildAchlistCheckbox.Checked
        UseDmmdeps       = $useDmmdepsCheckbox.Checked
        SmartClobber     = $smartClobberCheckbox.Checked
        XboxArchive      = $xboxArchiveCheckbox.Checked
        WindowsArchive   = $windowsArchiveCheckbox.Checked
        SortAchlist      = $sortAchlistCheckbox.Checked
        Copy             = $copyCheckbox.Checked
        Zip              = $zipCheckbox.Checked
        CleanCopy        = $cleanCopyCheckbox.Checked
    }

    Save-Config -Path $configPath -Data $cfgOut

    $inputPath   = $inputBox.Text
    if (-not $inputPath) {
        [System.Windows.Forms.MessageBox]::Show("Please select an input file.", "Missing Input", 'OK', 'Warning') | Out-Null
        return
    }

    $dataRoot     = $dataBox.Text
    $xboxRoot     = $xboxBox.Text
    $backupRoot   = $destBox.Text
    $archiverPath = $archiverBox.Text
    $dmmdepsPath  = $dmmdepsBox.Text

    $modName = [System.IO.Path]::GetFileNameWithoutExtension($inputPath)
    $achlistPath = if ($inputPath.ToLower().EndsWith(".achlist")) {
        $inputPath
    } else {
        Join-Path $dataRoot "$modName.achlist"
    }

    # Cache the last successful run inputs so AF creation can reuse them
    $script:lastRunInputPath   = $inputPath
    $script:lastRunModName     = $modName
    $script:lastRunAchlistPath = $achlistPath
    
    $csvPath = Join-Path $dataRoot "$($modName)_deps.csv"

    $doVoice  = $voiceUpdateCheckbox.Checked -or $rebuildAchlistCheckbox.Checked
    $doDmmdeps = $useDmmdepsCheckbox.Checked
    $doBa2    = $xboxArchiveCheckbox.Checked -or $windowsArchiveCheckbox.Checked -or $sortAchlistCheckbox.Checked
    $doBackup = $copyCheckbox.Checked -or $zipCheckbox.Checked -or $cleanCopyCheckbox.Checked

    if (-not ($doVoice -or $doDmmdeps -or $doBa2 -or $doBackup)) {
        [System.Windows.Forms.MessageBox]::Show("No operations are selected. Tick at least one checkbox.", "Nothing to do", 'OK', 'Information') | Out-Null
        return
    }
    
    # Run dmmdeps first if selected
    if ($doDmmdeps) {
        $statusLabel.Text = "Running: Dmmdeps Generation..."
        $dmmdepsSuccess = Invoke-DmmdepsGeneration -DmmdepsPath $dmmdepsPath -InputPath $inputPath -DataFolder $dataRoot -SmartClobber:$smartClobberCheckbox.Checked
        if (-not $dmmdepsSuccess) {
            $logBox.AppendText("Dmmdeps generation failed. Stopping execution.`r`n")
            return
        }
    }

    if ($doVoice) {
        $statusLabel.Text = "Running: Voice File Management..."
        Invoke-VoiceManagement -InputPath $inputPath -DataRoot $dataRoot -XboxRoot $xboxRoot `
            -DoVoiceUpdate:$voiceUpdateCheckbox.Checked -DoRebuildAchlist:$rebuildAchlistCheckbox.Checked
    }

    if ($doBa2) {
        $statusLabel.Text = "Running: BA2 Archive Management..."
        Invoke-Ba2Archives -AchlistPath $achlistPath -DataFolder $dataRoot -XboxDataPath $xboxRoot `
            -ArchiverPath $archiverPath -DoXbox:$xboxArchiveCheckbox.Checked `
            -DoWindows:$windowsArchiveCheckbox.Checked -DoSort:$sortAchlistCheckbox.Checked
    }

    if ($doBackup) {
        $statusLabel.Text = "Running: Backup Management..."
        
        # Handle CSV-based backup if dmmdeps was used
        if ($useDmmdepsCheckbox.Checked -and (Test-Path $csvPath)) {
            $logBox.AppendText("Using CSV-based backup. CSV path: $csvPath`r`n")
            Invoke-BackupFromCsv -InputPath $inputPath -BackupRoot $backupRoot -DataRoot $dataRoot -XboxRoot $xboxRoot `
                -CsvPath $csvPath -DoCopy:$copyCheckbox.Checked -DoZip:$zipCheckbox.Checked -DoClean:$cleanCopyCheckbox.Checked
        } else {
            if ($useDmmdepsCheckbox.Checked) {
                $logBox.AppendText("Dmmdeps enabled but CSV not found at: $csvPath. Using traditional backup.`r`n")
            } else {
                $logBox.AppendText("Using traditional achlist-based backup.`r`n")
            }
            Invoke-Backup -InputPath $inputPath -BackupRoot $backupRoot -DataRoot $dataRoot -XboxRoot $xboxRoot `
                -DoCopy:$copyCheckbox.Checked -DoZip:$zipCheckbox.Checked -DoClean:$cleanCopyCheckbox.Checked
        }

        # Only after a backup in this session do we allow AF
        $afButton.Enabled = $true
    }

    # Stop stopwatch and show total runtime
    $script:stopwatch.Stop()
    $runtime = $script:stopwatch.Elapsed
    $runtimeText = if ($runtime.TotalMinutes -ge 1) {
        "{0:mm\:ss\.ff}" -f $runtime
    } else {
        "{0:ss\.ff}" -f $runtime
    }
    
    $statusLabel.Text = "Run completed. Total runtime: $runtimeText"
    $logBox.AppendText("RUN completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Total runtime: $runtimeText`r`n")
})

$doAllButton.Add_Click({
    # Just turn everything on; Run button actually executes
    $voiceUpdateCheckbox.Checked    = $true
    $rebuildAchlistCheckbox.Checked = $true
    $useDmmdepsCheckbox.Checked     = $true
    $smartClobberCheckbox.Checked   = $true
    $xboxArchiveCheckbox.Checked    = $true
    $windowsArchiveCheckbox.Checked = $true
    $sortAchlistCheckbox.Checked    = $true
    $copyCheckbox.Checked           = $true
    $zipCheckbox.Checked            = $true
    $cleanCopyCheckbox.Checked      = $true
})

# -------------------------------------------------------------------
#  Show the form
# -------------------------------------------------------------------
[void]$form.ShowDialog()
