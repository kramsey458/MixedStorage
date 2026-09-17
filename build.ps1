param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Timberborn',
    [Parameter(Mandatory = $true)][string]$HarmonyPath,
    [Parameter(Mandatory = $true)][string]$BeaverBuddiesPath
)
$ErrorActionPreference = 'Stop'
foreach ($file in @((Join-Path $GameDir 'Timberborn_Data\Managed\Timberborn.InventorySystem.dll'), $HarmonyPath, $BeaverBuddiesPath)) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Required dependency not found: $file" }
}
dotnet build (Join-Path $PSScriptRoot 'multiplayer\MixedStorage.BeaverBuddies.csproj') -c Release "-p:GameDir=$GameDir" "-p:HarmonyPath=$HarmonyPath" "-p:BeaverBuddiesPath=$BeaverBuddiesPath"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
dotnet run --project (Join-Path $PSScriptRoot 'tests\AllocationTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Allocation tests failed.' }
$dist = Join-Path $PSScriptRoot 'dist'
foreach ($name in @('MixedStorage', 'MixedStorage-BeaverBuddies')) {
    $versionDirectory = Join-Path $dist "$name\version-1.1"
    New-Item -ItemType Directory -Force $versionDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "packaging\$name\version-1.1\manifest.json") -Destination $versionDirectory
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'source\bin\Release\netstandard2.1\MixedStorage.dll') -Destination (Join-Path $dist 'MixedStorage\version-1.1')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $dist 'MixedStorage')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'multiplayer\bin\Release\netstandard2.1\MixedStorage.BeaverBuddies.dll') -Destination (Join-Path $dist 'MixedStorage-BeaverBuddies\version-1.1')
foreach ($name in @('MixedStorage', 'MixedStorage-BeaverBuddies')) {
    $manifest = Get-Content -LiteralPath (Join-Path $dist "$name\version-1.1\manifest.json") -Raw | ConvertFrom-Json
    Compress-Archive -LiteralPath (Join-Path $dist $name) -DestinationPath (Join-Path $dist "$name-v$($manifest.Version).zip") -Force
}
Write-Output "Built and packaged both mods in $dist"
