<#
.SYNOPSIS
    Publishes NetIPConfig as a single executable.

.EXAMPLE
    .\publish.ps1
    Self-contained x64 exe in .\publish — runs without .NET installed.

.EXAMPLE
    .\publish.ps1 -Runtime win-arm64 -SelfContained:$false
    Small exe for Arm64 that needs the .NET 8 Desktop Runtime.
#>
[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$SelfContained = $true,
    [string]$OutputPath = (Join-Path $PSScriptRoot 'publish')
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src/NetIPConfig/NetIPConfig.csproj'

dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $SelfContained.ToString().ToLowerInvariant() `
    --output $OutputPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host ''
Write-Host "Published to $(Join-Path $OutputPath 'NetIPConfig.exe')" -ForegroundColor Green
Write-Host 'Launch it directly; Windows will prompt for administrator rights.'
