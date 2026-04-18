<#
.SYNOPSIS
  Remove every Bimwright artifact from this machine (plugin + server tool + host configs + discovery + logs).

.DESCRIPTION
  Runs a 5-step sweep:
    1. Plugin + .addin for every detected Revit year (delegates to install.ps1 -Uninstall).
    2. .NET global tool Bimwright.Rvt.Server.
    3. MCP host config entries matching bimwright-rvt-* in:
       - $env:USERPROFILE\.config\opencode\opencode.json
       - $env:USERPROFILE\.codex\config.toml
       - $env:USERPROFILE\.claude.json (global, if present)
       Project-level .mcp.json files are NOT scanned — emits a reminder notice.
    4. Discovery files in %LOCALAPPDATA%\Bimwright\ (except logs\ when -KeepLogs).
    5. ToolBaker cache (contained inside step 4 path; reported separately).

  Each step is independently skippable if the target does not exist. Failure mid-step
  does not abort the chain; exit code 1 is returned at the end if any step failed.

.PARAMETER WhatIf
  Print the full plan, write nothing.

.PARAMETER Yes
  Skip the interactive confirmation prompt.

.PARAMETER KeepLogs
  Preserve %LOCALAPPDATA%\Bimwright\logs\ during step 4.

.EXAMPLE
  pwsh scripts/uninstall-all.ps1 -WhatIf
  pwsh scripts/uninstall-all.ps1 -Yes
  pwsh scripts/uninstall-all.ps1 -KeepLogs
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$Yes,
    [switch]$KeepLogs
)

$ErrorActionPreference = 'Stop'

$script:handled = @()
$script:skipped = @()
$script:failed  = @()

# duplicated from install.ps1 intentionally — both scripts are standalone entry points for end-users
function Write-ConfigAtomic {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )
    $bak = "$Path.bimwright.bak"
    Copy-Item -Path $Path -Destination $bak -Force
    $temp = "$Path.bimwright.tmp"
    try {
        Set-Content -Path $temp -Value $Content -Encoding UTF8 -NoNewline
        [System.IO.File]::Replace($temp, $Path, [NullString]::Value)
    } catch {
        if (Test-Path $temp) { Remove-Item $temp -Force -ErrorAction SilentlyContinue }
        throw
    }
    return $bak
}

function Confirm-Sweep {
    param([string[]]$PlannedTargets)
    Write-Host ""
    Write-Host "=== uninstall-all.ps1 — planned targets ==="
    foreach ($t in $PlannedTargets) { Write-Host "  - $t" }
    Write-Host ""
    if ($Yes) { return $true }
    $ans = Read-Host "Proceed? (y/N)"
    return ($ans -match '^(y|yes)$')
}

function Invoke-Step1-Plugin {
    $installScript = Join-Path $PSScriptRoot 'install.ps1'
    if (-not (Test-Path $installScript)) {
        Write-Warning "[step1] install.ps1 not found at $installScript — cannot remove plugin"
        $script:failed += 'step1-plugin'
        return
    }
    try {
        if ($PSCmdlet.ShouldProcess($installScript, 'Delegate plugin uninstall')) {
            & $installScript -Uninstall
        } else {
            Write-Host "[step1] (WhatIf) would call: $installScript -Uninstall"
        }
        $script:handled += 'step1-plugin'
    } catch {
        Write-Warning ("[step1] plugin uninstall failed: {0}" -f $_.Exception.Message)
        $script:failed += 'step1-plugin'
    }
}

function Invoke-Step2-DotnetTool {
    $toolName = 'Bimwright.Rvt.Server'
    try {
        $list = & dotnet tool list -g 2>&1 | Out-String
    } catch {
        Write-Warning "[step2] 'dotnet' not on PATH — cannot check global tools"
        $script:skipped += 'step2-dotnet-tool'
        return
    }

    if ($list -notmatch [regex]::Escape($toolName.ToLower())) {
        Write-Host "[step2] $toolName not installed — nothing to remove"
        $script:skipped += 'step2-dotnet-tool'
        return
    }

    try {
        if ($PSCmdlet.ShouldProcess($toolName, 'dotnet tool uninstall -g')) {
            & dotnet tool uninstall -g $toolName
        } else {
            Write-Host "[step2] (WhatIf) would run: dotnet tool uninstall -g $toolName"
        }
        $script:handled += 'step2-dotnet-tool'
    } catch {
        Write-Warning ("[step2] uninstall failed: {0}" -f $_.Exception.Message)
        $script:failed += 'step2-dotnet-tool'
    }
}

# --- Main ---
$planned = @(
    'Step1: plugin + .addin (all detected Revit years via install.ps1 -Uninstall)'
    'Step2: .NET global tool Bimwright.Rvt.Server'
    # Steps 3-5 added in later tasks
)

if (-not (Confirm-Sweep $planned)) {
    Write-Host "Aborted by user."
    return
}

Invoke-Step1-Plugin
Invoke-Step2-DotnetTool

Write-Host ""
Write-Host "=== uninstall-all.ps1 summary ==="
Write-Host ("Handled: {0}" -f (($script:handled) -join ', '))
if ($script:skipped.Count -gt 0) { Write-Host ("Skipped: {0}" -f (($script:skipped) -join ', ')) }
if ($script:failed.Count  -gt 0) { Write-Host ("Failed : {0}" -f (($script:failed)  -join ', ')) }

if ($script:failed.Count -gt 0) { exit 1 } else { exit 0 }
