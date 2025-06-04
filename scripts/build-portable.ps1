$ErrorActionPreference = "Stop"

. "$PSScriptRoot\helpers\get-arch.ps1"
$arch = Get-Arch
$portableArch = switch ($arch) {
    "amd64" { "x64" }
    "arm64" { "arm64" }
    default { throw "Unknown architecture: $arch" }
}
$releaseDir = ".\release"
$distName = "SyncTrayzorPortable-$portableArch"

dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false -p:SelfContained=true -o ./dist/ src/PortableInstaller/PortableInstaller.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build PortableInstaller. Exiting."
    exit $LASTEXITCODE
}

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}
Write-Host "Creating $distName.zip"
Move-Item -Path ./dist -Destination $distName -Force
Compress-Archive -Path $distName -DestinationPath "$releaseDir/$distName.zip" -Force
Move-Item -Path $distName -Destination ./dist -Force