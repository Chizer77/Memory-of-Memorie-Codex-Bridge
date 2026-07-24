[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Version,
    [Parameter(Mandatory)]
    [string]$BepInExArchive,
    [string]$BepInExVersion = '6.0.0-be.785'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$safeVersion = $Version -replace '[^0-9A-Za-z._-]', '-'
$safeBepInExVersion = $BepInExVersion -replace '[^0-9A-Za-z._-]', '-'
$packageName = "Memory-of-Memorie-Codex-Bridge-$safeVersion-BepInEx-$safeBepInExVersion-net6.0-win-x64"
$stagingBase = Join-Path $repositoryRoot (Join-Path 'outputs\release-staging' ([Guid]::NewGuid().ToString('N')))
$packageRoot = Join-Path $stagingBase $packageName
$artifactDirectory = Join-Path $repositoryRoot 'outputs\release-artifacts'
$archivePath = Join-Path $artifactDirectory "$packageName.zip"
$pluginOutput = Join-Path $repositoryRoot 'bin\Release\net6.0\MemoryOfMemorieCodexBridge.dll'

if (-not (Test-Path -LiteralPath $pluginOutput))
{
    throw "Release DLL was not found: $pluginOutput"
}
if (-not (Test-Path -LiteralPath $BepInExArchive))
{
    throw "BepInEx runtime archive was not found: $BepInExArchive"
}

$bepInExDirectory = Join-Path $packageRoot 'BepInEx'
$bepInExConfigDirectory = Join-Path $bepInExDirectory 'config'
$pluginDirectory = Join-Path $bepInExDirectory 'plugins\MemoryOfMemorieCodexBridge'
$hooksDirectory = Join-Path $packageRoot 'hooks'
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Expand-Archive -LiteralPath $BepInExArchive -DestinationPath $packageRoot -Force
New-Item -ItemType Directory -Path $bepInExConfigDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $hooksDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

Copy-Item -LiteralPath $pluginOutput -Destination (Join-Path $pluginDirectory 'MemoryOfMemorieCodexBridge.dll')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'config.example.json') -Destination (Join-Path $pluginDirectory 'config.json')
Copy-Item -LiteralPath (Join-Path $repositoryRoot '.github\release-assets\BepInEx.cfg') -Destination (Join-Path $bepInExDirectory 'config\BepInEx.cfg')
Copy-Item -Path (Join-Path $repositoryRoot 'hooks\*') -Destination $hooksDirectory -Recurse
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination (Join-Path $packageRoot 'LICENSE-Memory-of-Memorie-Codex-Bridge.txt')
Copy-Item -LiteralPath (Join-Path $repositoryRoot '.github\release-assets\INSTALL.md') -Destination (Join-Path $packageRoot 'INSTALL.md')

Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath -Force
Write-Output "RELEASE_ARCHIVE=$archivePath"
