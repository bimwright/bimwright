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

function Invoke-Step4-Discovery {
    $root = Join-Path $env:LOCALAPPDATA 'Bimwright'
    if (-not (Test-Path $root)) {
        Write-Host "[step4] $root not present — nothing to remove"
        $script:skipped += 'step4-discovery'
        return
    }

    $logsPath = Join-Path $root 'logs'
    $toolBakerPath = Join-Path $root 'ToolBaker'
    $hasToolBaker = Test-Path $toolBakerPath

    if ($KeepLogs -and (Test-Path $logsPath)) {
        # Remove everything inside $root EXCEPT logs\
        $entries = Get-ChildItem -Path $root -Force
        foreach ($e in $entries) {
            if ($e.Name -eq 'logs') { continue }
            if ($PSCmdlet.ShouldProcess($e.FullName, 'Remove-Item -Recurse')) {
                Remove-Item -Path $e.FullName -Recurse -Force
            }
        }
        Write-Host ("[step4] cleaned {0} (kept logs\)" -f $root)
    } else {
        if ($PSCmdlet.ShouldProcess($root, 'Remove-Item -Recurse')) {
            Remove-Item -Path $root -Recurse -Force
        }
        Write-Host ("[step4] removed {0}" -f $root)
    }

    $script:handled += 'step4-discovery'
    if ($hasToolBaker) {
        $script:handled += 'step5-toolbaker (contained)'
    }
}

function Remove-ClaudeCodeGlobalEntries {
    $candidates = @(
        (Join-Path $env:USERPROFILE '.claude.json'),
        (Join-Path $env:USERPROFILE '.claude\mcp.json')
    )

    $touched = $false
    foreach ($cfgPath in $candidates) {
        if (-not (Test-Path $cfgPath)) { continue }

        try {
            $cfg = (Get-Content -Raw -Path $cfgPath) | ConvertFrom-Json -AsHashtable -Depth 50
        } catch {
            Write-Warning ("[step3.claude] parse failed at {0} — skipping this file" -f $cfgPath)
            continue
        }

        # Claude Code uses 'mcpServers' (plural camelCase) per its docs
        if (-not $cfg.ContainsKey('mcpServers') -or $cfg['mcpServers'].Count -eq 0) { continue }

        $bimKeys = @($cfg['mcpServers'].Keys | Where-Object { $_ -like 'bimwright-rvt-*' })
        if ($bimKeys.Count -eq 0) { continue }

        foreach ($k in $bimKeys) { $cfg['mcpServers'].Remove($k) | Out-Null }

        if ($PSCmdlet.ShouldProcess($cfgPath, ("Remove {0} bimwright-rvt-* entries" -f $bimKeys.Count))) {
            $content = $cfg | ConvertTo-Json -Depth 50
            $bak = Write-ConfigAtomic -Path $cfgPath -Content $content
            Write-Host ("[step3.claude] removed {0} entries -> {1} (backup: {2})" -f $bimKeys.Count, $cfgPath, $bak)
        }
        $touched = $true
    }

    if ($touched) { $script:handled += 'step3-claude-global' }
    else          { $script:skipped += 'step3-claude-global' }

    Write-Host ""
    Write-Host "[step3.claude] NOTE: project-level .mcp.json files are not auto-scanned."
    Write-Host "               If you added bimwright-rvt-* to any project's .mcp.json manually,"
    Write-Host "               remove those entries by hand."
}

function Remove-CodexEntries {
    $cfgPath = Join-Path $env:USERPROFILE '.codex\config.toml'
    if (-not (Test-Path $cfgPath)) {
        Write-Host "[step3.codex] config not present — nothing to unwire"
        $script:skipped += 'step3-codex'
        return
    }

    $raw = Get-Content -Raw -Path $cfgPath -Encoding UTF8
    if ($null -eq $raw) { $raw = '' }

    $pattern = '(?ms)^\[mcp_servers\.bimwright-rvt-r\d{2}\].*?(?=^\[|\z)'
    $blockMatches = [regex]::Matches($raw, $pattern)
    if ($blockMatches.Count -eq 0) {
        Write-Host "[step3.codex] no bimwright-rvt-* blocks — skipping"
        $script:skipped += 'step3-codex'
        return
    }

    $new = [regex]::Replace($raw, $pattern, '')
    # Collapse triple+ blank lines left behind by removal
    $new = [regex]::Replace($new, "(\r?\n){3,}", "`n`n")

    if ($PSCmdlet.ShouldProcess($cfgPath, ("Remove {0} [mcp_servers.bimwright-rvt-*] blocks" -f $blockMatches.Count))) {
        $bak = Write-ConfigAtomic -Path $cfgPath -Content $new
        Write-Host ("[step3.codex] removed {0} blocks -> {1} (backup: {2})" -f $blockMatches.Count, $cfgPath, $bak)
    }
    $script:handled += 'step3-codex'
}

function Remove-OpencodeEntries {
    $cfgPath = Join-Path $env:USERPROFILE '.config\opencode\opencode.json'
    if (-not (Test-Path $cfgPath)) {
        Write-Host "[step3.opencode] config not present — nothing to unwire"
        $script:skipped += 'step3-opencode'
        return
    }

    try {
        $cfg = (Get-Content -Raw -Path $cfgPath) | ConvertFrom-Json -AsHashtable -Depth 50
    } catch {
        Write-Warning ("[step3.opencode] parse failed at {0}: {1} — skipping" -f $cfgPath, $_.Exception.Message)
        $script:failed += 'step3-opencode'
        return
    }

    if (-not $cfg.ContainsKey('mcp') -or $cfg['mcp'].Count -eq 0) {
        Write-Host "[step3.opencode] no mcp entries — skipping"
        $script:skipped += 'step3-opencode'
        return
    }

    $bimKeys = @($cfg['mcp'].Keys | Where-Object { $_ -like 'bimwright-rvt-*' })
    if ($bimKeys.Count -eq 0) {
        Write-Host "[step3.opencode] no bimwright-rvt-* entries — skipping"
        $script:skipped += 'step3-opencode'
        return
    }

    foreach ($k in $bimKeys) { $cfg['mcp'].Remove($k) | Out-Null }
    if ($cfg['mcp'].Count -eq 0) { $cfg.Remove('mcp') | Out-Null }

    if ($PSCmdlet.ShouldProcess($cfgPath, ("Remove {0} bimwright-rvt-* entries" -f $bimKeys.Count))) {
        $content = $cfg | ConvertTo-Json -Depth 50
        $bak = Write-ConfigAtomic -Path $cfgPath -Content $content
        Write-Host ("[step3.opencode] removed {0} entries -> {1} (backup: {2})" -f $bimKeys.Count, $cfgPath, $bak)
    }
    $script:handled += 'step3-opencode'
}

# --- Main ---
$planned = @(
    'Step1: plugin + .addin (all detected Revit years via install.ps1 -Uninstall)'
    'Step2: .NET global tool Bimwright.Rvt.Server'
    'Step3.opencode: bimwright-rvt-* keys in .config\opencode\opencode.json'
    'Step3.codex: [mcp_servers.bimwright-rvt-*] blocks in .codex\config.toml'
    'Step3.claude: bimwright-rvt-* in ~/.claude.json and ~/.claude/mcp.json (global only)'
    'Step4: %LOCALAPPDATA%\Bimwright\ (discovery + ToolBaker)'
)

if (-not (Confirm-Sweep $planned)) {
    Write-Host "Aborted by user."
    return
}

Invoke-Step1-Plugin
Invoke-Step2-DotnetTool
Remove-OpencodeEntries
Remove-CodexEntries
Remove-ClaudeCodeGlobalEntries
Invoke-Step4-Discovery

Write-Host ""
Write-Host "=== uninstall-all.ps1 summary ==="
Write-Host ("Handled: {0}" -f (($script:handled) -join ', '))
if ($script:skipped.Count -gt 0) { Write-Host ("Skipped: {0}" -f (($script:skipped) -join ', ')) }
if ($script:failed.Count  -gt 0) { Write-Host ("Failed : {0}" -f (($script:failed)  -join ', ')) }

if ($script:failed.Count -gt 0) { exit 1 } else { exit 0 }
