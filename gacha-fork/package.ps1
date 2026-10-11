# GachaOnly fork — package the source tree and the portable release.
#
#   .\package.ps1 -Version 0.1.0                     # both archives
#   .\package.ps1 -Version 0.1.0 -DataFolder 'F:\Mihoyo\Starward'
#   .\package.ps1 -Version 0.1.0 -SkipBuild -SkipSource
#
# The release archive keeps the layout of Starward's own portable package:
#
#   StarwardGacha/
#       Starward.exe          launcher (gacha-fork/launcher, the C# equivalent of upstream's C++ one)
#       version.ini           version=<version>
#       app-<version>/        the published app (AssemblyName forced to Starward, as upstream does)
#       gacha-data.ini        only when -DataFolder is given (shared data folder, see README §3)
#
# Output: gacha-fork/dist/
#   StarwardGacha_Portable_<version>_x64.zip
#   StarwardGacha_Source_<version>.zip
#
# Note: this machine's execution policy refuses to run .ps1 directly, so invoke it as
#   powershell.exe -ExecutionPolicy Bypass -File gacha-fork\package.ps1 -Version 0.1.0

param(
    [Parameter(Mandatory = $true)][string] $Version,
    [string] $Architecture = 'x64',
    [string] $DataFolder = '',
    [switch] $SkipBuild,
    [switch] $SkipSource
)

$ErrorActionPreference = 'Stop'
$forkRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $forkRoot
$dist = Join-Path $forkRoot 'dist'
$stage = Join-Path $dist 'stage'
$packageName = 'StarwardGacha'
$packageRoot = Join-Path $stage $packageName
$appFolder = Join-Path $packageRoot "app-$Version"
$dotnet = if ($env:DOTNET_ROOT) { Join-Path $env:DOTNET_ROOT 'dotnet.exe' } else { 'dotnet' }

if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

Write-Output "[1/5] cleaning stage"
if (-not $SkipBuild) {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

    Write-Output "[2/5] launcher -> $packageRoot\Starward.exe"
    & $dotnet publish (Join-Path $forkRoot 'launcher\StarwardGacha.Launcher.csproj') `
        -c Release -r "win-$Architecture" -o (Join-Path $stage 'launcher') `
        -p:Version=$Version -v m
    if ($LASTEXITCODE -ne 0) { throw 'launcher publish failed' }
    Move-Item (Join-Path $stage 'launcher\Starward.exe') (Join-Path $packageRoot 'Starward.exe') -Force
    Remove-Item (Join-Path $stage 'launcher') -Recurse -Force

    Write-Output "[3/5] app -> $appFolder"
    # PortableLayout=true makes the app project publish as app-<version>\Starward.exe, which is what
    # the launcher (and upstream's own launcher) starts. Development builds stay Starward.GachaOnly.
    & $dotnet publish (Join-Path $repoRoot 'src\Starward\Starward.csproj') `
        -c Release -r "win-$Architecture" -p:Platform=$Architecture -p:Version=$Version `
        -p:GachaOnly=true -p:PortableLayout=true -p:PublishTrimmed=false -p:PublishReadyToRun=false `
        -o $appFolder -v m
    if ($LASTEXITCODE -ne 0) { throw 'app publish failed' }

    Write-Output "[4/5] version.ini"
    Set-Content -Path (Join-Path $packageRoot 'version.ini') -Value "version=$Version" -Encoding ascii
    if ($DataFolder) {
        Set-Content -Path (Join-Path $packageRoot 'gacha-data.ini') -Value "UserDataFolder=$DataFolder" -Encoding ascii
    }
}
else {
    Write-Output "[1/5..4/5] build skipped"
}

Write-Output "[5/5] archives"
New-Item -ItemType Directory -Path $dist -Force | Out-Null

# --- portable package -------------------------------------------------------------------------
if (Test-Path (Join-Path $packageRoot 'Starward.exe')) {
    $portableArchive = Join-Path $dist "StarwardGacha_Portable_${Version}_$Architecture.zip"
    Remove-Item $portableArchive -Force -ErrorAction SilentlyContinue
    Push-Location $stage
    try { tar -c -a -f $portableArchive $packageName } finally { Pop-Location }
}
else {
    Write-Output '  portable package skipped (no stage; run without -SkipBuild)'
}

# --- source package ---------------------------------------------------------------------------
# The tracked tree (upstream + the fork's code changes) plus the fork folder itself, which is kept
# out of git on purpose. Build output (dist/out/backup/bin/obj) never goes in.
if (-not $SkipSource) {
    $sourceArchive = Join-Path $dist "StarwardGacha_Source_$Version.zip"
    Remove-Item $sourceArchive -Force -ErrorAction SilentlyContinue
    $sourceStage = Join-Path $dist 'srcstage'
    if (Test-Path $sourceStage) { Remove-Item $sourceStage -Recurse -Force }
    New-Item -ItemType Directory -Path $sourceStage -Force | Out-Null

    git -C $repoRoot archive --format=zip -o (Join-Path $dist 'tracked.zip') HEAD
    if ($LASTEXITCODE -ne 0) { throw 'git archive failed' }
    Expand-Archive -Path (Join-Path $dist 'tracked.zip') -DestinationPath $sourceStage -Force
    Remove-Item (Join-Path $dist 'tracked.zip') -Force

    $forkTarget = Join-Path $sourceStage 'gacha-fork'
    New-Item -ItemType Directory -Path $forkTarget -Force | Out-Null
    foreach ($item in 'README.md', 'build.ps1', 'package.ps1', 'sync-upstream.ps1', 'tools', 'gacha-only.patch', 'gacha-only.diff') {
        $source = Join-Path $forkRoot $item
        if (Test-Path $source) { Copy-Item $source -Destination $forkTarget -Recurse -Force }
    }
    # the launcher project: sources only, never its build output
    $launcherSource = Join-Path $forkRoot 'launcher'
    if (Test-Path $launcherSource) {
        $launcherTarget = Join-Path $forkTarget 'launcher'
        New-Item -ItemType Directory -Path $launcherTarget -Force | Out-Null
        Copy-Item (Join-Path $launcherSource '*.*') -Destination $launcherTarget -Force
    }
    # belt and braces: nothing built ever ships in the source package
    Get-ChildItem $sourceStage -Recurse -Directory | Where-Object { $_.Name -in 'bin', 'obj' } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    # The archive should read as "Starward Gacha", not as a stock Starward drop: the package README
    # becomes the entry point and upstream's own README is kept beside it.
    $upstreamReadme = Join-Path $sourceStage 'README.md'
    if (Test-Path $upstreamReadme) {
        Move-Item $upstreamReadme (Join-Path $sourceStage 'README.upstream.md') -Force
    }
    $packageReadme = Join-Path $forkRoot 'PACKAGE-README.md'
    if (Test-Path $packageReadme) {
        Copy-Item $packageReadme (Join-Path $sourceStage 'README.md') -Force
    }

    Push-Location $sourceStage
    try { tar -c -a -f $sourceArchive (Get-ChildItem $sourceStage | Select-Object -ExpandProperty Name) } finally { Pop-Location }
    Remove-Item $sourceStage -Recurse -Force
}

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue

Get-ChildItem $dist -File | ForEach-Object {
    Write-Output ("  {0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB))
}
