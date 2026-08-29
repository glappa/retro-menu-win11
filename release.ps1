<#
    Builds the two release files and their checksums.

      RetroMenu-Setup-x64.exe    self contained, needs nothing installed. Run it and
                                 it offers to install itself; the copy it leaves
                                 behind is the program.
      RetroMenu-portable-x64.zip framework dependent, needs the .NET 8 Desktop
                                 Runtime, for people who would rather not install.

    -Publish additionally creates the GitHub release and uploads both, with the
    checksums in the notes.
#>
param(
    [switch]$Publish,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$project = Join-Path $root 'src\RetroMenu\RetroMenu.csproj'
$out = Join-Path $root 'release'
$work = Join-Path $out 'work'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out, $work | Out-Null

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ }
if (-not $Tag) { $Tag = "v$version" }
Write-Host "Version $version, Tag $Tag"

function Publish-Variant([string]$name, [bool]$selfContained) {
    $dir = Join-Path $work $name
    $args = @(
        'publish', $project, '-c', 'Release', '-r', 'win-x64', '-o', $dir,
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:DebugType=none',
        '--self-contained', $(if ($selfContained) { 'true' } else { 'false' })
    )
    # compression only applies to a self contained bundle
    if ($selfContained) { $args += '-p:EnableCompressionInSingleFile=true' }
    Write-Host "  dotnet publish ($name)"
    & dotnet @args | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $name" }
    return (Join-Path $dir 'RetroMenu.exe')
}

# --- the installer: one file, nothing to install first ---
$setupSource = Publish-Variant 'setup' $true
$setup = Join-Path $out 'RetroMenu-Setup-x64.exe'
Copy-Item $setupSource $setup -Force

# --- portable: small, wants the runtime ---
$portableSource = Publish-Variant 'portable' $false
$portableDir = Join-Path $work 'portable-zip'
New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
Copy-Item $portableSource (Join-Path $portableDir 'RetroMenu.exe') -Force
Copy-Item (Join-Path $root 'README.md') $portableDir -Force
Copy-Item (Join-Path $root 'LICENSE') $portableDir -Force
$portable = Join-Path $out 'RetroMenu-portable-x64.zip'
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $portable -Force

Remove-Item $work -Recurse -Force

# --- checksums ---
$lines = @()
foreach ($file in @($setup, $portable)) {
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLower()
    $size = [math]::Round((Get-Item $file).Length / 1MB, 1)
    $lines += "$hash  $(Split-Path $file -Leaf)"
    Write-Host ("  {0,-30} {1,6} MB  {2}" -f (Split-Path $file -Leaf), $size, $hash)
}
$sums = Join-Path $out 'SHA256SUMS.txt'
Set-Content -Path $sums -Value $lines -Encoding ASCII

if (-not $Publish) {
    Write-Host ''
    Write-Host "Fertig in $out. Mit -Publish wird daraus eine GitHub-Ausgabe."
    return
}

$notes = @"
Retro Menu $version

Ein Startmenue im Stil aelterer Windows-Versionen fuer Windows 11, als Gegenstueck
zu [RetroBar](https://github.com/dremin/RetroBar).

**Welche Datei?**

| Datei | Fuer wen |
| --- | --- |
| ``RetroMenu-Setup-x64.exe`` | Der uebliche Weg. Bringt alles mit, setzt nichts voraus, richtet auf Wunsch auch RetroBar mit ein. |
| ``RetroMenu-portable-x64.zip`` | Wer nichts installieren moechte. Braucht das [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). |

Installiert wird in den eigenen Benutzerordner. Keine Administratorrechte, keine
Aenderung am System; Entfernen geht ueber die Programmliste von Windows.

**Pruefsummen (SHA-256)**

``````
$($lines -join "`n")
``````

Nachrechnen mit:

``````
Get-FileHash .\RetroMenu-Setup-x64.exe -Algorithm SHA256
``````
"@

$notesFile = Join-Path $out 'notes.md'
Set-Content -Path $notesFile -Value $notes -Encoding UTF8

Write-Host "Lege GitHub-Ausgabe $Tag an..."
& gh release create $Tag $setup $portable $sums --title "Retro Menu $version" --notes-file $notesFile
if ($LASTEXITCODE -ne 0) { throw 'gh release create failed' }
Write-Host 'Veroeffentlicht.'
