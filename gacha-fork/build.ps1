# GachaOnly fork — build the standalone gacha app from this source tree.
#
#   .\build.ps1                                   # Debug-free Release publish, x64
#   .\build.ps1 -Architecture arm64 -Version 0.2.0
#   .\build.ps1 -DataFolder 'D:\Starward'         # point the app at a specific data folder
#   .\build.ps1 -Stock                             # build plain upstream Starward instead
#
# Output: <repo>\gacha-fork\out\StarwardGacha-<Version>-<Architecture>\
#         Starward.GachaOnly.exe + gacha-data.ini (shared user data folder)

param(
    [string] $Configuration = 'Release',
    [ValidateSet('x64', 'x86', 'arm64')]
    [string] $Architecture = 'x64',
    [string] $Version = '0.1.0',
    [string] $Output,
    [string] $DataFolder,
    [switch] $Stock,
    [switch] $UseWindowsSdk,
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

# The .NET SDK lives outside PATH on this machine (dotnet.exe found no SDK by default).
$dotnet = 'E:\A-Dev\deepseek-harness\.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

if (-not $Output) { $Output = Join-Path $PSScriptRoot "out\StarwardGacha-$Version-$Architecture" }
$runtime = "win-$Architecture"

$props = @("-p:Platform=$Architecture", "-p:Version=$Version", "-p:GachaOnly=$((-not $Stock).ToString().ToLower())")
if ($UseWindowsSdk) {
    $props += '-p:WindowsSdkAvailable=true'
}
else {
    # No Windows SDK on this machine: the CsWinRT projection / AOT optimizer steps cannot run, and a
    # trimmed Release build then dies at XAML startup (0xC000027B). Turn trimming and R2R off.
    # Command line beats the publish profile, which is what sets PublishTrimmed/PublishReadyToRun.
    $props += '-p:PublishTrimmed=false'
    $props += '-p:PublishReadyToRun=false'
}
if ($NoRestore) { $props += '--no-restore' }

Write-Host "dotnet      : $dotnet"
Write-Host "runtime     : $runtime   configuration: $Configuration"
Write-Host "variant     : $(if ($Stock) { 'stock Starward (GachaOnly=false)' } else { 'GachaOnly (GachaOnly=true)' })"
Write-Host "output      : $Output"
Write-Host ''

& $dotnet publish (Join-Path $repo 'src\Starward\Starward.csproj') `
    -c $Configuration -r $runtime -o $Output @props
if ($LASTEXITCODE -ne 0) { throw "publish failed with exit code $LASTEXITCODE" }

if (-not $Stock) {
    $ini = Join-Path $Output 'gacha-data.ini'
    if ($DataFolder) {
        $lines = @(
            '; Starward Gacha - shared user data folder (holds StarwardDatabase.db).'
            '; resolution order: --data-folder > STARWARD_GACHA_DATA_FOLDER env var > this file > auto probe.'
            "UserDataFolder=$DataFolder"
        )
        Write-Host "data folder : $DataFolder   (written to $ini)"
    }
    else {
        $lines = @(
            '; Starward Gacha - shared user data folder (holds StarwardDatabase.db).'
            '; leave the value empty to auto-detect a Starward-family data folder, or set it explicitly,'
            '; for example:  UserDataFolder=D:\Starward'
            'UserDataFolder='
        )
        Write-Host 'data folder : auto-detect'
    }
    # ASCII + no BOM: keeps the file readable whatever writes/reads it next
    [System.IO.File]::WriteAllLines($ini, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

$exe = Get-ChildItem $Output -Filter '*.exe' | Where-Object { $_.Name -like 'Starward*' } | Select-Object -First 1
if ($exe) {
    Write-Host ''
    Write-Host "built: $($exe.FullName)  ($([math]::Round($exe.Length / 1MB, 2)) MB)"
}
