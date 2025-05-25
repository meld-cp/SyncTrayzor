param(
    [string]$Variant
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\helpers\get-arch.ps1"
$arch = Get-Arch
$installerArch = switch ($arch) {
    "amd64" { "x64" }
    "arm64" { "arm64" }
    default { throw "Unknown architecture: $arch" }
}
# Try to find ISCC.exe in PATH
$isccPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue

# Fallback to default location if not found
if (-not $isccPath) {
    $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $isccPath)) {
        throw "ISCC.exe not found in PATH or at '$isccPath'"
    }
}

$innoScript = Join-Path $PSScriptRoot "..\installer\installer-$installerArch.iss"

& $isccPath $innoScript
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE"
}