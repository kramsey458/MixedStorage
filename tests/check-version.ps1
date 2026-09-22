# The mod's version is set in one place, Directory.Build.props, and both DLLs are built with it. This checks that the
# manifest the game reads says the same and that neither mod project sets its own <Version> (which would win over
# Directory.Build.props), then prints the version. It needs no game files: build.ps1 runs it before building, and CI
# runs it on every pull request. build.ps1 also checks the version the built DLLs carry.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$versionNodes = @(([xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)).SelectNodes('/Project/PropertyGroup/Version'))
if ($versionNodes.Count -ne 1) { throw 'Directory.Build.props must set <Version> exactly once.' }
$version = $versionNodes[0].InnerText.Trim()
$manifestVersion = (Get-Content -LiteralPath (Join-Path $root 'packaging\MixedStorage\version-1.1\manifest.json') -Raw | ConvertFrom-Json).Version
if ($manifestVersion -cne $version) { throw "manifest.json has Version '$manifestVersion' but Directory.Build.props has '$version'. Set both to the release's version." }
foreach ($project in @('source\MixedStorage.csproj', 'multiplayer\MixedStorage.BeaverBuddies.csproj')) {
    if (([xml](Get-Content -LiteralPath (Join-Path $root $project) -Raw)).SelectNodes('/Project/PropertyGroup/Version').Count -ne 0) {
        throw "$project sets <Version>, which would win over Directory.Build.props. Set the version only there."
    }
}
$version
