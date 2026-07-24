param(
    [ValidateSet('Interactive', 'Codex', 'Claude', 'OpenCode', 'All')]
    [string]$Platform = 'Interactive',
    [string]$CodexHome,
    [string]$ClaudeHome,
    [string]$OpenCodeConfigHome,
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Resolve-Home {
    param([string]$RequestedPath, [string]$DefaultPath)

    if (![string]::IsNullOrWhiteSpace($RequestedPath)) { return [IO.Path]::GetFullPath($RequestedPath) }
    return $DefaultPath
}

function Test-PlatformAvailable {
    param([string]$CommandName, [string]$HomePath)

    return $null -ne (Get-Command $CommandName -ErrorAction SilentlyContinue) -or (Test-Path -LiteralPath $HomePath)
}

function Write-Utf8File {
    param([string]$Path, [string]$Content)

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    [IO.File]::WriteAllText($Path, $Content, (New-Object Text.UTF8Encoding($false)))
}

function Get-Property {
    param([object]$Object, [string]$Name)

    if ($null -eq $Object) { return $null }
    return $Object.PSObject.Properties | Where-Object { $_.Name -eq $Name } | Select-Object -First 1
}

function Ensure-Property {
    param([object]$Object, [string]$Name, $Value)

    if ($null -eq $Object) { throw 'Cannot add a property to an empty configuration object.' }
    if ($null -eq (Get-Property $Object $Name)) {
        Add-Member -InputObject $Object -MemberType NoteProperty -Name $Name -Value $Value
    }
}

function Read-JsonObject {
    param([string]$Path)

    if (!(Test-Path -LiteralPath $Path)) { return [pscustomobject]@{} }
    # Windows PowerShell 5.1 不支持 ConvertFrom-Json -Depth。
    $json = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $parsed = $json | ConvertFrom-Json
    if ($null -eq $parsed) { throw "Configuration is empty: $Path" }
    return $parsed
}

function Get-TemplateConfig {
    param([string]$TemplatePath, [string]$BridgeDirectory)

    $template = Get-Content -LiteralPath $TemplatePath -Raw -Encoding UTF8
    # 替换发生在 JSON 解析前，因此 Windows 反斜杠必须先转义。
    $jsonPath = $BridgeDirectory.Replace('\', '\\')
    return ($template.Replace('__BRIDGE_ROOT__', $jsonPath) | ConvertFrom-Json)
}

function Backup-File {
    param([string]$Path)

    if (!(Test-Path -LiteralPath $Path)) { return $null }
    $backup = "$Path.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -LiteralPath $Path -Destination $backup
    return $backup
}

function Save-Config {
    param([string]$Path, [object]$Config)

    $backup = Backup-File $Path
    Write-Utf8File $Path (($Config | ConvertTo-Json -Depth 64) + [Environment]::NewLine)
    if ($backup) { Write-Host "Backup: $backup" }
}

function Copy-BridgeRuntime {
    param([string]$HomePath)

    $source = Join-Path $RepositoryRoot 'hooks\scripts\memory-of-memorie-bridge'
    $destination = Join-Path $HomePath 'scripts\memory-of-memorie-bridge'
    $sourceScript = Join-Path $source 'memory-of-memorie-agent-hook.ps1'
    $sourceSettings = Join-Path $source 'settings.json'
    if (!(Test-Path -LiteralPath $sourceScript) -or !(Test-Path -LiteralPath $sourceSettings)) {
        throw 'The shared bridge runtime is missing from the repository.'
    }

    New-Item -ItemType Directory -Force -Path $destination | Out-Null

    Copy-Item -LiteralPath $sourceScript -Destination (Join-Path $destination 'memory-of-memorie-agent-hook.ps1') -Force
    $destinationSettings = Join-Path $destination 'settings.json'
    if (!(Test-Path -LiteralPath $destinationSettings)) { Copy-Item -LiteralPath $sourceSettings -Destination $destinationSettings }
    return $destination
}

function Add-CodexHook {
    param([object]$HooksRoot, [string]$EventName, [object]$Group, [ref]$Changed)

    $property = Get-Property $HooksRoot $EventName
    $groups = if ($property) { @($property.Value) } else { @() }
    $templateCommand = @($Group.hooks)[0].commandWindows
    $existing = $groups | Where-Object { @($_.hooks) | Where-Object { $_.commandWindows -eq $templateCommand } }
    if ($existing) { return }

    $groups = @($groups) + $Group
    if ($property) { $HooksRoot.$EventName = $groups } else { Add-Member -InputObject $HooksRoot -MemberType NoteProperty -Name $EventName -Value $groups }
    $Changed.Value = $true
}

function Add-ClaudeHook {
    param([object]$HooksRoot, [string]$EventName, [object]$Group, [ref]$Changed)

    $property = Get-Property $HooksRoot $EventName
    $groups = if ($property) { @($property.Value) } else { @() }
    $templateCommand = @($Group.hooks)[0].command
    $existing = $groups | Where-Object { @($_.hooks) | Where-Object { $_.command -eq $templateCommand } }
    if ($existing) { return }

    $groups = @($groups) + $Group
    if ($property) { $HooksRoot.$EventName = $groups } else { Add-Member -InputObject $HooksRoot -MemberType NoteProperty -Name $EventName -Value $groups }
    $Changed.Value = $true
}

function Install-Codex {
    param([string]$HomePath)

    $bridge = Copy-BridgeRuntime $HomePath
    $configPath = Join-Path $HomePath 'hooks.json'
    $config = Read-JsonObject $configPath
    Ensure-Property $config 'hooks' ([pscustomobject]@{})
    $template = Get-TemplateConfig (Join-Path $RepositoryRoot 'hooks\platforms\codex\hooks.json') $bridge
    $changed = $false
    Add-CodexHook $config.hooks 'UserPromptSubmit' (@($template.hooks.UserPromptSubmit)[0]) ([ref]$changed)
    Add-CodexHook $config.hooks 'Stop' (@($template.hooks.Stop)[0]) ([ref]$changed)
    if ($changed) { Save-Config $configPath $config }
    return $bridge
}

function Install-Claude {
    param([string]$HomePath)

    $bridge = Copy-BridgeRuntime $HomePath
    $configPath = Join-Path $HomePath 'settings.json'
    $config = Read-JsonObject $configPath
    Ensure-Property $config 'hooks' ([pscustomobject]@{})
    $template = Get-TemplateConfig (Join-Path $RepositoryRoot 'hooks\platforms\claude\settings.json') $bridge
    $changed = $false
    Add-ClaudeHook $config.hooks 'UserPromptSubmit' (@($template.hooks.UserPromptSubmit)[0]) ([ref]$changed)
    Add-ClaudeHook $config.hooks 'Stop' (@($template.hooks.Stop)[0]) ([ref]$changed)
    if ($changed) { Save-Config $configPath $config }
    return $bridge
}

function Install-OpenCode {
    param([string]$ConfigHome)

    $bridge = Copy-BridgeRuntime $ConfigHome
    $source = Join-Path $RepositoryRoot 'hooks\platforms\opencode\plugins\memory-of-memorie-activity-bridge.ts'
    $destination = Join-Path $ConfigHome 'plugins\memory-of-memorie-activity-bridge.ts'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    return $bridge
}

$resolvedCodexHome = Resolve-Home $CodexHome (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex')
$resolvedClaudeHome = Resolve-Home $ClaudeHome (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.claude')
$defaultOpenCode = Join-Path (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.config') 'opencode'
$resolvedOpenCodeHome = Resolve-Home $OpenCodeConfigHome $defaultOpenCode

$available = [ordered]@{
    Codex = Test-PlatformAvailable 'codex' $resolvedCodexHome
    Claude = Test-PlatformAvailable 'claude' $resolvedClaudeHome
    OpenCode = Test-PlatformAvailable 'opencode' $resolvedOpenCodeHome
}

$isInteractive = $Platform -eq 'Interactive'
if ($isInteractive) {
    $choices = @($available.GetEnumerator() | Where-Object Value | ForEach-Object Key)
    if ($choices.Count -eq 0) { throw 'No supported platform was detected. Install Codex, Claude Code, or OpenCode first.' }
    Write-Host 'Detected platforms:'
    for ($index = 0; $index -lt $choices.Count; $index++) { Write-Host "  $($index + 1). $($choices[$index])" }
    Write-Host "  A. All detected platforms ($($choices -join ', '))"
    $answer = Read-Host 'Choose a platform number or A'
    if ($answer -match '^[aA]$') { $Platform = 'All' } elseif ($answer -match '^\d+$' -and [int]$answer -ge 1 -and [int]$answer -le $choices.Count) { $Platform = $choices[[int]$answer - 1] } else { throw 'Invalid platform selection.' }
}

$targets = if ($Platform -eq 'All') { @($available.GetEnumerator() | Where-Object Value | ForEach-Object Key) } else { @($Platform) }
$installed = @()
foreach ($target in $targets) {
    switch ($target) {
        'Codex' { $installed += [pscustomobject]@{ Platform = 'Codex'; Directory = Install-Codex $resolvedCodexHome } }
        'Claude' { $installed += [pscustomobject]@{ Platform = 'Claude Code'; Directory = Install-Claude $resolvedClaudeHome } }
        'OpenCode' { $installed += [pscustomobject]@{ Platform = 'OpenCode'; Directory = Install-OpenCode $resolvedOpenCodeHome } }
    }
}

Write-Host ''
Write-Host 'Memory of Memorie activity bridge installed:'
foreach ($item in $installed) { Write-Host "  $($item.Platform): $($item.Directory)" }
if ($isInteractive) { Read-Host 'Installation complete. Press Enter to close' | Out-Null }
