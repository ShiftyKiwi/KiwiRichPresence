param(
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectDir = Join-Path $repoRoot "KiwiRichPresence"
$releaseRoot = Join-Path $repoRoot "artifacts\\release"
$buildRoot = Join-Path $projectDir "bin\\x64\\$Configuration"
$manifestPath = Join-Path $buildRoot "KiwiRichPresence.json"

if (Test-Path -LiteralPath $buildRoot) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}

dotnet clean (Join-Path $repoRoot "KiwiRichPresence.sln") -c $Configuration
dotnet build (Join-Path $repoRoot "KiwiRichPresence.sln") -c $Configuration

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Built manifest not found at $manifestPath"
}

$manifest = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
$version = $manifest.AssemblyVersion

$packageZip = Join-Path $buildRoot "KiwiRichPresence\\latest.zip"
if (-not (Test-Path -LiteralPath $packageZip)) {
    throw "Packaged plugin zip not found at $packageZip"
}

$extractDir = Join-Path $releaseRoot "KiwiRichPresence-$version"
$versionedZip = Join-Path $releaseRoot "KiwiRichPresence-$version.zip"
$latestZip = Join-Path $releaseRoot "KiwiRichPresence-latest.zip"

if (Test-Path -LiteralPath $extractDir) {
    Remove-Item -LiteralPath $extractDir -Recurse -Force
}

if (Test-Path -LiteralPath $versionedZip) {
    Remove-Item -LiteralPath $versionedZip -Force
}

if (Test-Path -LiteralPath $latestZip) {
    Remove-Item -LiteralPath $latestZip -Force
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
Expand-Archive -LiteralPath $packageZip -DestinationPath $extractDir
Compress-Archive -Path (Join-Path $extractDir "*") -DestinationPath $versionedZip -CompressionLevel Optimal
Copy-Item -LiteralPath $versionedZip -Destination $latestZip

Write-Output "Prepared release directory: $extractDir"
Write-Output "Prepared versioned zip: $versionedZip"
Write-Output "Prepared latest zip: $latestZip"
