<#
.SYNOPSIS
  Install or uninstall Bimwright Revit plugin(s) for every installed Revit year.

.DESCRIPTION
  Detects installed Revit years via HKLM:\SOFTWARE\Autodesk\Revit\<year>\ and, for
  each year that has a matching build/plugin-zip/Bimwright.Rvt.Plugin.R<nn>.zip, extracts
  the zip to %APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\ and copies the .addin
  manifest up to %APPDATA%\Autodesk\Revit\Addins\<year>\.

  With -Uninstall, removes both the Bimwright\ folder and the Bimwright.R<nn>.addin
  file for every detected year.

  The script ships inside the release ZIP alongside the per-version plugin zips, so
  end-users run it directly without needing the repo checked out.

.PARAMETER SourceDir
  Directory containing Bimwright.Rvt.Plugin.R<nn>.zip files. Default: build/plugin-zip/
  relative to the repo root (parent of scripts/). Release bundles override this.

.PARAMETER Uninstall
  Remove the plugin from every detected Revit year.

.PARAMETER Years
  Optional explicit list of years (e.g. 2023,2025). Default: auto-detect via registry.

.EXAMPLE
  pwsh scripts/install.ps1
  pwsh scripts/install.ps1 -Uninstall
  pwsh scripts/install.ps1 -Years 2023,2025 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourceDir,
    [switch]$Uninstall,
    [int[]]$Years,
    [ValidateSet('opencode', 'codex')]
    [string]$WireClient
)

$ErrorActionPreference = 'Stop'

if (-not $SourceDir) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $SourceDir = Join-Path $repoRoot 'build\plugin-zip'
}

function Get-InstalledRevitYears {
    $detected = @()
    $root = 'HKLM:\SOFTWARE\Autodesk\Revit'
    if (-not (Test-Path $root)) { return $detected }
    foreach ($year in 2022..2027) {
        $yearKey = Join-Path $root "$year"
        if (Test-Path $yearKey) { $detected += $year }
    }
    return $detected
}

function Get-AddinsRoot([int]$year) {
    return Join-Path $env:APPDATA ("Autodesk\Revit\Addins\{0}" -f $year)
}

function Get-BimwrightYearTargets([int[]]$years) {
    # Returns array of PSCustomObjects for plugin-supported years only (2022-2027).
    # Years outside this range are silently skipped — callers may pass raw $Years
    # which can include unsupported values (e.g. synthetic 2099 in edge tests).
    $targets = @()
    foreach ($y in $years) {
        if ($y -lt 2022 -or $y -gt 2027) { continue }
        $yt = "{0:D2}" -f ($y - 2000)
        $targets += [pscustomobject]@{
            Year      = $y
            YearTwo   = $yt
            Target    = "R$yt"
            ServerCmd = 'bimwright-rvt'
        }
    }
    return $targets
}

function Add-OpencodeEntry {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][object[]]$Targets
    )

    if (-not (Test-Path $ConfigPath)) {
        Write-Warning ("[opencode] config not found at {0} — skipping wire" -f $ConfigPath)
        return $false
    }

    try {
        $raw = Get-Content -Raw -Path $ConfigPath
        $cfg = $raw | ConvertFrom-Json -AsHashtable -Depth 50
    } catch {
        Write-Warning ("[opencode] parse failed at {0}: {1} — skipping" -f $ConfigPath, $_.Exception.Message)
        return $false
    }

    if (-not $cfg.ContainsKey('mcp')) { $cfg['mcp'] = @{} }

    $desired = @{}
    foreach ($t in $Targets) {
        $name = "bimwright-rvt-r$($t.YearTwo)"
        $desired[$name] = [ordered]@{
            type    = 'local'
            command = @($t.ServerCmd, '--target', $t.Target)
            enabled = $true
        }
    }

    $changed = $false
    foreach ($k in $desired.Keys) {
        $existingJson = if ($cfg['mcp'].ContainsKey($k)) { ($cfg['mcp'][$k] | ConvertTo-Json -Depth 20 -Compress) } else { $null }
        $newJson = $desired[$k] | ConvertTo-Json -Depth 20 -Compress
        if ($existingJson -ne $newJson) {
            $cfg['mcp'][$k] = $desired[$k]
            $changed = $true
        }
    }

    if (-not $changed) {
        Write-Host ("[opencode] no changes needed at {0}" -f $ConfigPath)
        return $true
    }

    if ($PSCmdlet.ShouldProcess($ConfigPath, 'Upsert bimwright-rvt-* entries')) {
        $bak = "$ConfigPath.bimwright.bak"
        Copy-Item -Path $ConfigPath -Destination $bak -Force
        $temp = "$ConfigPath.bimwright.tmp"
        ($cfg | ConvertTo-Json -Depth 50) | Set-Content -Path $temp -Encoding UTF8 -NoNewline
        # [NullString]::Value is required because PowerShell marshals bare $null to "" for
        # string parameters, which File.Replace rejects with "The path is empty".
        [System.IO.File]::Replace($temp, $ConfigPath, [NullString]::Value)
        Write-Host ("[opencode] wired {0} entries -> {1} (backup: {2})" -f $desired.Count, $ConfigPath, $bak)
    }
    return $true
}

function Add-CodexEntry {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][object[]]$Targets
    )

    if (-not (Test-Path $ConfigPath)) {
        Write-Warning ("[codex] config not found at {0} — skipping wire" -f $ConfigPath)
        return $false
    }

    $raw = Get-Content -Raw -Path $ConfigPath -Encoding UTF8
    if ($null -eq $raw) { $raw = '' }

    $changed = $false

    foreach ($t in $Targets) {
        $name = "bimwright-rvt-r$($t.YearTwo)"
        $headerLiteral = "[mcp_servers.$name]"
        $desiredBlock = @"
$headerLiteral
command = "$($t.ServerCmd)"
args = ["--target", "$($t.Target)"]
enabled = true
"@

        $pattern = '(?ms)^\[mcp_servers\.' + [regex]::Escape($name) + '\].*?(?=^\[|\z)'
        $existingMatch = [regex]::Match($raw, $pattern)
        if ($existingMatch.Success) {
            $existingTrim = ($existingMatch.Value -replace '\s+$', '')
            $desiredTrim = ($desiredBlock -replace '\s+$', '')
            if ($existingTrim -ne $desiredTrim) {
                $raw = [regex]::Replace($raw, $pattern, ($desiredBlock + "`n`n"), 1)
                $changed = $true
            }
        } else {
            $sep = if ($raw.EndsWith("`n")) { "`n" } else { "`n`n" }
            $raw = $raw + $sep + $desiredBlock + "`n"
            $changed = $true
        }
    }

    if (-not $changed) {
        Write-Host ("[codex] no changes needed at {0}" -f $ConfigPath)
        return $true
    }

    if ($PSCmdlet.ShouldProcess($ConfigPath, 'Upsert [mcp_servers.bimwright-rvt-*] blocks')) {
        $bak = "$ConfigPath.bimwright.bak"
        Copy-Item -Path $ConfigPath -Destination $bak -Force
        $temp = "$ConfigPath.bimwright.tmp"
        Set-Content -Path $temp -Value $raw -Encoding UTF8 -NoNewline
        # [NullString]::Value — PowerShell otherwise sends "" which File.Replace rejects.
        [System.IO.File]::Replace($temp, $ConfigPath, [NullString]::Value)
        Write-Host ("[codex] wired bimwright-rvt-* blocks -> {0} (backup: {1})" -f $ConfigPath, $bak)
    }
    return $true
}

if (-not $Years -or $Years.Count -eq 0) {
    $Years = Get-InstalledRevitYears
    if ($Years.Count -eq 0) {
        Write-Warning "No Revit installations detected under HKLM:\SOFTWARE\Autodesk\Revit\. Use -Years to force explicit list."
        return
    }
    Write-Host ("Detected Revit years: {0}" -f ($Years -join ', '))
}

$handled = @()
$skipped = @()

foreach ($year in $Years) {
    $yearTwo = "{0:D2}" -f ($year - 2000)   # 2023 -> 23
    $addinFile = "Bimwright.R$yearTwo.addin"
    $addinsRoot = Get-AddinsRoot $year
    $pluginDir = Join-Path $addinsRoot 'Bimwright'
    $addinPath = Join-Path $addinsRoot $addinFile

    if ($Uninstall) {
        $didSomething = $false
        if (Test-Path $pluginDir) {
            if ($PSCmdlet.ShouldProcess($pluginDir, 'Remove plugin folder')) {
                Remove-Item $pluginDir -Recurse -Force
            }
            $didSomething = $true
        }
        if (Test-Path $addinPath) {
            if ($PSCmdlet.ShouldProcess($addinPath, 'Remove addin manifest')) {
                Remove-Item $addinPath -Force
            }
            $didSomething = $true
        }
        if ($didSomething) {
            Write-Host ("[R{0}] uninstalled from {1}" -f $yearTwo, $addinsRoot)
            $handled += "R$yearTwo"
        } else {
            Write-Host ("[R{0}] nothing to remove at {1}" -f $yearTwo, $addinsRoot)
            $skipped += "R$yearTwo"
        }
        continue
    }

    # Install path
    $zip = Join-Path $SourceDir ("Bimwright.Rvt.Plugin.R{0}.zip" -f $yearTwo)
    if (-not (Test-Path $zip)) {
        Write-Warning ("[R{0}] skipped — missing zip {1}" -f $yearTwo, $zip)
        $skipped += "R$yearTwo"
        continue
    }

    if (-not (Test-Path $addinsRoot)) {
        if ($PSCmdlet.ShouldProcess($addinsRoot, 'Create Revit addins directory')) {
            New-Item -ItemType Directory -Path $addinsRoot -Force | Out-Null
        }
    }

    if (Test-Path $pluginDir) {
        if ($PSCmdlet.ShouldProcess($pluginDir, 'Clean previous install')) {
            Remove-Item $pluginDir -Recurse -Force
        }
    }
    if ($PSCmdlet.ShouldProcess($pluginDir, 'Create plugin folder')) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }

    # Peek into zip (works under -WhatIf too) to verify the addin manifest is present
    # before we commit to the Expand/Move sequence.
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
    $zipHasAddin = $false
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $zipHasAddin = @($archive.Entries | Where-Object { $_.Name -eq $addinFile }).Count -gt 0
    } finally {
        $archive.Dispose()
    }
    if (-not $zipHasAddin) {
        Write-Warning ("[R{0}] zip {1} does not contain {2} — skipping" -f $yearTwo, $zip, $addinFile)
        $skipped += "R$yearTwo"
        continue
    }

    if ($PSCmdlet.ShouldProcess($zip, "Extract to $pluginDir")) {
        Expand-Archive -Path $zip -DestinationPath $pluginDir -Force
    }

    # .addin manifest must sit at addins root, not inside Bimwright\
    $extractedAddin = Join-Path $pluginDir $addinFile
    if ($PSCmdlet.ShouldProcess($addinPath, 'Move addin manifest to addins root')) {
        Move-Item -Path $extractedAddin -Destination $addinPath -Force
    }

    Write-Host ("[R{0}] installed -> {1}" -f $yearTwo, $pluginDir)
    $handled += "R$yearTwo"
}

Write-Host ""
Write-Host "=== install.ps1 summary ==="
Write-Host ("Mode   : {0}" -f ($(if ($Uninstall) { 'Uninstall' } else { 'Install' })))
Write-Host ("Years  : {0}" -f ($Years -join ', '))
Write-Host ("Handled: {0}" -f ($handled -join ', '))
if ($skipped.Count -gt 0) {
    Write-Host ("Skipped: {0}" -f ($skipped -join ', '))
}
