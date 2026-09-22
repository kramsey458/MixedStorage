param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Timberborn',
    [Parameter(Mandatory = $true)][string]$HarmonyPath,
    [Parameter(Mandatory = $true)][string]$BeaverBuddiesPath
)
$ErrorActionPreference = 'Stop'
foreach ($file in @((Join-Path $GameDir 'Timberborn_Data\Managed\Timberborn.InventorySystem.dll'), $HarmonyPath, $BeaverBuddiesPath)) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Required dependency not found: $file" }
}
dotnet build (Join-Path $PSScriptRoot 'multiplayer\MixedStorage.BeaverBuddies.csproj') -p:BootstrapBuild=true -c Release "-p:GameDir=$GameDir" "-p:HarmonyPath=$HarmonyPath" "-p:BeaverBuddiesPath=$BeaverBuddiesPath"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
dotnet build (Join-Path $PSScriptRoot 'source\MixedStorage.csproj') -c Release "-p:GameDir=$GameDir" "-p:HarmonyPath=$HarmonyPath"
if ($LASTEXITCODE -ne 0) { throw 'Bundled build failed.' }
dotnet build (Join-Path $PSScriptRoot 'loading-tests\StubBeaverBuddies\StubBeaverBuddies.csproj') -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Stub BeaverBuddies build failed.' }
$stubBeaverBuddies = Join-Path $PSScriptRoot 'loading-tests\StubBeaverBuddies\bin\Release\netstandard2.1\BeaverBuddies.dll'
foreach ($mode in @('without', 'with', 'legacy', 'incompatible')) {
    $beaverBuddies = if ($mode -eq 'incompatible') { $stubBeaverBuddies } else { $BeaverBuddiesPath }
    dotnet run --project (Join-Path $PSScriptRoot 'loading-tests\LoadingTests.csproj') -c Release -- $mode (Join-Path $PSScriptRoot 'source\bin\Release\netstandard2.1\MixedStorage.dll') $GameDir $HarmonyPath $beaverBuddies
    if ($LASTEXITCODE -ne 0) { throw "Optional loading test failed: $mode" }
}
dotnet run --project (Join-Path $PSScriptRoot 'tests\AllocationTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Allocation tests failed.' }
dotnet run --project (Join-Path $PSScriptRoot 'visual-tests\VisualTests.csproj') -c Release "-p:GameDir=$GameDir" "-p:HarmonyPath=$HarmonyPath" -- $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Visual geometry tests failed.' }
$dist = Join-Path $PSScriptRoot 'dist'
# Start from an empty staging folder so files from earlier builds are never packed.
$staging = Join-Path $dist 'MixedStorage'
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
$versionDirectory = Join-Path $staging 'version-1.1'
New-Item -ItemType Directory -Force $versionDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\MixedStorage\version-1.1\manifest.json') -Destination $versionDirectory
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'source\bin\Release\netstandard2.1\MixedStorage.dll') -Destination $versionDirectory
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $staging
$manifest = Get-Content -LiteralPath (Join-Path $versionDirectory 'manifest.json') -Raw | ConvertFrom-Json
$zip = Join-Path $dist "MixedStorage-v$($manifest.Version).zip"
$partial = Join-Path $dist "MixedStorage-v$($manifest.Version).partial.zip"
foreach ($file in @($zip, $partial)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    # Windows' tar writes the '/' separators the zip format requires. Compress-Archive in Windows
    # PowerShell 5.1 writes '\', which some macOS and Linux tools extract as flat file names.
    & (Join-Path $env:SystemRoot 'System32\tar.exe') -a -c -f $partial -C $dist 'MixedStorage'
    if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($partial)
    try { $files = @($archive.Entries | Where-Object { !$_.FullName.EndsWith('/') } | ForEach-Object { $_.FullName } | Sort-Object) }
    finally { $archive.Dispose() }
    $expected = @('MixedStorage/README.md', 'MixedStorage/version-1.1/manifest.json', 'MixedStorage/version-1.1/MixedStorage.dll') | Sort-Object
    if (($files -join '|') -cne ($expected -join '|')) { throw "Unexpected zip contents: $($files -join ', ')" }
    # Only a checked zip gets the release name.
    Move-Item -LiteralPath $partial -Destination $zip
}
finally {
    if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial }
}
Write-Output "Built and packaged the unified mod in $zip"
