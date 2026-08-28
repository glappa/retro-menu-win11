<#
    Baut eine einzelne RetroMenu.exe nach publish\.

    Ohne Schalter entsteht eine schlanke EXE, die das .NET 8 Desktop Runtime auf dem
    Zielrechner voraussetzt. Mit -SelfContained bringt sie alles mit und laeuft auch
    ohne installiertes Runtime, ist dafuer deutlich groesser.
#>
param(
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$project = Join-Path $root 'src\RetroMenu\RetroMenu.csproj'
$output = Join-Path $root 'publish'

$args = @(
    'publish', $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $output,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=none'
)

if ($SelfContained) {
    $args += '--self-contained'
    $args += 'true'
} else {
    $args += '--self-contained'
    $args += 'false'
}

Write-Host "dotnet $($args -join ' ')"
& dotnet @args

Write-Host ''
Write-Host "Fertig: $(Join-Path $output 'RetroMenu.exe')"
