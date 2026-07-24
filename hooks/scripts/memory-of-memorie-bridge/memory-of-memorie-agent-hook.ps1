param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('start', 'stop')]
    [string]$Event
)

$ErrorActionPreference = 'Stop'
$settingsPath = Join-Path $PSScriptRoot 'settings.json'

function Invoke-GameCommand {
    param([string]$ApiBase, [string]$Id, [Nullable[int]]$Minutes = $null)

    $body = @{ id = $Id }
    if ($Minutes.HasValue) { $body.minutes = $Minutes.Value }
    Invoke-RestMethod -Uri "$ApiBase/v1/commands" -Method Post -ContentType 'application/json; charset=utf-8' -Body ($body | ConvertTo-Json -Compress) -TimeoutSec 3 | Out-Null
}

function Get-TimerState {
    param([string]$ApiBase)

    try { return (Invoke-RestMethod -Uri "$ApiBase/v1/timer-status" -Method Get -TimeoutSec 2).data.currentState } catch { return '' }
}

try {
    $settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $apiBase = ([string]$settings.gameApiUrl).TrimEnd('/')
    $timerState = Get-TimerState -ApiBase $apiBase

    if ($Event -eq 'start') {
        if ($timerState -eq 'Work') { exit 0 }
        $minutes = 0
        if (![int]::TryParse([string]$settings.workMinutes, [ref]$minutes) -or $minutes -le 0) { throw 'workMinutes must be a positive integer.' }
        Invoke-GameCommand -ApiBase $apiBase -Id 'pomodoro.set-work-minutes' -Minutes $minutes
        Invoke-GameCommand -ApiBase $apiBase -Id 'pomodoro.ui-start'
        exit 0
    }

    if ($timerState -ne 'Default') { Invoke-GameCommand -ApiBase $apiBase -Id 'pomodoro.ui-stop' }
} catch {
    Write-Warning "Memory of Memorie activity bridge ignored failure: $($_.Exception.Message)"
}

exit 0
