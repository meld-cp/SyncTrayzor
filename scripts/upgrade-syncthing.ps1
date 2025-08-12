$ErrorActionPreference = "Stop"

$exePath = ".\syncthing\syncthing.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "Syncthing binary not found at path: $exePath"
    exit 1
}

Write-Host "Running Syncthing upgrade..."

& $exePath upgrade
$exitCode = $LASTEXITCODE

switch ($exitCode) {
    0 { Write-Host "Generic success." ; exit 0 }
    2 { Write-Host "No upgrade available." ; exit 0 }
    4 { Write-Host "Syncthing upgraded successfully."; exit 0 }
    default {
        Write-Host "Syncthing upgrade failed with exit code $exitCode."
        exit $exitCode
    }
}