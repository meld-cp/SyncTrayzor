param (
    [string]$Version
)

$ErrorActionPreference = "Stop"
$outdir = "syncthing"

if (Test-Path "$outdir/syncthing.exe") {
    Write-Host "Syncthing already downloaded in $outdir. Skipping download."
    exit 0
}

. "$PSScriptRoot\helpers\get-arch.ps1"
$arch = Get-Arch
$filename = "syncthing-windows-$arch-$Version.zip"
$url = "https://github.com/syncthing/syncthing/releases/download/$Version/$filename"
$zipPath = Join-Path $env:TEMP $filename

Write-Host "Downloading $url..."
Invoke-WebRequest -Uri $url -OutFile $zipPath

if (!(Test-Path $outdir)) { New-Item -ItemType Directory -Path $outdir | Out-Null }

Write-Host "Extracting to $outdir..."
Expand-Archive -Path $zipPath -DestinationPath $outdir -Force

# Optionally move binaries up from the nested folder
$extractedRoot = Join-Path $outdir "syncthing-windows-$arch-$Version"
if (Test-Path $extractedRoot) {
    Move-Item -Path (Join-Path $extractedRoot '*') -Destination $outdir -Force
    Remove-Item $extractedRoot -Recurse -Force
}

Remove-Item $zipPath -Force