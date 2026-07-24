param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('start', 'stop')]
    [string]$Event
)

$ErrorActionPreference = 'Stop'
$settingsPath = Join-Path $PSScriptRoot 'settings.json'
$commandTimeoutSeconds = 15
$startRetryCount = 5

function Invoke-GameCommand {
    param([string]$ApiBase, [string]$Id, [object]$Minutes = $null)

    $body = @{ id = $Id }
    if ($null -ne $Minutes) { $body.minutes = [int]$Minutes }
    try {
        Invoke-RestMethod -Uri "$ApiBase/v1/commands" -Method Post -ContentType 'application/json; charset=utf-8' -Body ($body | ConvertTo-Json -Compress) -TimeoutSec $commandTimeoutSeconds | Out-Null
    } catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }

        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        try { $payload = $reader.ReadToEnd() } finally { $reader.Dispose() }
        throw "Game command '$Id' failed: $payload"
    }
}

function Get-TimerState {
    param([string]$ApiBase)

    try { return (Invoke-RestMethod -Uri "$ApiBase/v1/timer-status" -Method Get -TimeoutSec 2).data.currentState } catch { return '' }
}

function Start-PomodoroTimer {
    param([string]$ApiBase)

    $lastError = $null
    for ($attempt = 1; $attempt -le $startRetryCount; $attempt++) {
        if ((Get-TimerState -ApiBase $ApiBase) -eq 'Work') { return }
        try {
            Invoke-GameCommand -ApiBase $ApiBase -Id 'pomodoro.ui-start'
            return
        } catch {
            $lastError = $_
            if ($attempt -lt $startRetryCount) { Start-Sleep -Seconds 1 }
        }
    }

    throw $lastError
}

try {
    $settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $apiBase = ([string]$settings.gameApiUrl).TrimEnd('/')
    $timerState = Get-TimerState -ApiBase $apiBase
    # 游戏或本地 API 未运行时不再发送后续命令，避免影响普通 Codex 会话。
    if ([string]::IsNullOrWhiteSpace($timerState)) { exit 0 }

    if ($Event -eq 'start') {
        if ($timerState -eq 'Work') { exit 0 }
        $minutes = 0
        if (![int]::TryParse([string]$settings.workMinutes, [ref]$minutes) -or $minutes -le 0) { throw 'workMinutes must be a positive integer.' }
        Invoke-GameCommand -ApiBase $apiBase -Id 'pomodoro.set-work-minutes' -Minutes $minutes
        Start-PomodoroTimer -ApiBase $apiBase
        exit 0
    }

    if ($timerState -ne 'Default') { Invoke-GameCommand -ApiBase $apiBase -Id 'pomodoro.ui-stop' }
} catch {
    Write-Warning "Memory of Memorie activity bridge ignored failure: $($_.Exception.Message)"
}

exit 0
