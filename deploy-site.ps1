param(
    [string]$Remote = 'origin',
    [switch]$DryRun
)
# Publishes the site/ folder to the gh-pages branch, which GitHub Pages serves.
# Run it after committing your site/ changes. Add -DryRun to preview without pushing.

# Windows PowerShell 5.1 turns git's normal stderr progress text into errors under 'Stop',
# so git is run through a helper that checks its exit code instead.
function Invoke-Git {
    $output = & git.exe @args 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) { throw ("git $($args -join ' ') failed:`n" + ($output -join "`n")) }
    $output
}

$site = Join-Path $PSScriptRoot 'site'
if (!(Test-Path -LiteralPath (Join-Path $site 'index.html'))) { throw "site\index.html not found in $PSScriptRoot" }

$work = Join-Path ([IO.Path]::GetTempPath()) ('mixedstorage-pages-' + [guid]::NewGuid().ToString('N'))
$null = Invoke-Git -C $PSScriptRoot fetch $Remote gh-pages
$source = (Invoke-Git -C $PSScriptRoot rev-parse --short HEAD | Select-Object -First 1)
if (Invoke-Git -C $PSScriptRoot status --porcelain -- site) { Write-Warning 'site/ has uncommitted changes; they will be published anyway.' }

$null = Invoke-Git -C $PSScriptRoot worktree add --detach $work "$Remote/gh-pages"
try {
    # Replace the published files with the current contents of site/ (the worktree's .git file stays).
    Get-ChildItem -LiteralPath $work -Force | Where-Object { $_.Name -ne '.git' } | Remove-Item -Recurse -Force -ErrorAction Stop
    Copy-Item -Path (Join-Path $site '*') -Destination $work -Recurse -Force -ErrorAction Stop
    Copy-Item -LiteralPath (Join-Path $site '.nojekyll') -Destination $work -Force -ErrorAction Stop

    $null = Invoke-Git -C $work add -A
    $changes = @(Invoke-Git -C $work status --porcelain)
    if ($changes.Count -eq 0) { Write-Output 'The website is already up to date; nothing to publish.'; return }
    Write-Output 'Changes to publish:'
    $changes | ForEach-Object { Write-Output "  $_" }
    if ($DryRun) { Write-Output 'Dry run: nothing was pushed.'; return }

    $null = Invoke-Git -C $work commit -q -m "Deploy website from main@$source"
    Invoke-Git -C $work push $Remote HEAD:gh-pages | ForEach-Object { Write-Output $_ }
    Write-Output 'Published. GitHub Pages usually updates within a minute: https://kramsey458.github.io/MixedStorage/'
}
finally {
    try { $null = Invoke-Git -C $PSScriptRoot worktree remove --force $work } catch { }
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
}
