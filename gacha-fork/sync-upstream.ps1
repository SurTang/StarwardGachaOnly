# Sync this fork with upstream Starward.
#
#   .\sync-upstream.ps1              # fetch + rebase the fork commit(s) onto upstream/main
#   .\sync-upstream.ps1 -Merge       # same, but merge instead of rebase
#   .\sync-upstream.ps1 -Check       # only show what would conflict (no history rewrite)
#
# The whole fork is meant to stay a thin patch on top of upstream:
#   6 upstream files with one hook line each, everything else in new files
#   (src/Starward/Features/GachaOnly/**, src/Starward.Core/HoYoPlay/HoYoPlayClientLegacyOverloads.cs).
# If upstream touches those hook lines, the conflict is one or two lines wide.

param(
    [string] $UpstreamUrl = 'https://github.com/Scighost/Starward.git',
    [string] $Branch = 'main',
    [switch] $Merge,
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $remotes = git remote
    if ($remotes -notcontains 'upstream') {
        Write-Host "adding remote upstream -> $UpstreamUrl"
        git remote add upstream $UpstreamUrl
    }

    Write-Host 'fetching upstream...'
    git fetch upstream $Branch --tags
    if ($LASTEXITCODE -ne 0) { throw 'git fetch failed (no network access to GitHub?)' }

    $ahead = (git rev-list --count "upstream/$Branch..HEAD")
    $behind = (git rev-list --count "HEAD..upstream/$Branch")
    Write-Host "fork is $ahead commit(s) ahead, $behind commit(s) behind upstream/$Branch"
    if ($ahead -eq 0) { Write-Warning 'fork has no own commits: nothing to sync' }

    Write-Host ''
    Write-Host 'files this fork patches (the only places a conflict can appear):'
    git diff --name-status "upstream/$Branch...HEAD" -- src | ForEach-Object { "  $_" }

    if ($Check) {
        Write-Host ''
        Write-Host 'dry run (git merge-tree):'
        git merge-tree --write-tree "upstream/$Branch" HEAD | Select-Object -First 40
        return
    }

    Write-Host ''
    if ($Merge) {
        Write-Host "merging upstream/$Branch into the fork..."
        git merge "upstream/$Branch"
    }
    else {
        Write-Host "rebasing the fork commit(s) onto upstream/$Branch..."
        git rebase "upstream/$Branch"
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'conflicts need resolving, then run:  git rebase --continue   (or git merge --continue)'
        git diff --name-only --diff-filter=U | ForEach-Object { "  conflict: $_" }
        Write-Host ''
        Write-Host 'reminder: never rename namespaces/files or move Features/Gacha/** — that is what keeps this cheap.'
        return
    }

    Write-Host 'synced. now rebuild:  .\build.ps1 -DataFolder <your data folder>'
}
finally {
    Pop-Location
}
