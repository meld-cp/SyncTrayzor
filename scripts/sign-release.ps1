param(
    [string]$Variant
)

$ErrorActionPreference = "Stop"

$releaseDir = ".\release"

Write-Host "Signing release files"

$files = Get-ChildItem -Path $releaseDir -File | ForEach-Object { $_.FullName }

Write-Host "Files to sign: $($files -join ', ')"

dotnet run -c Release --project src/ChecksumUtil/ChecksumUtil.csproj -- create "$releaseDir\sha512sum.txt.asc" "SHA-512" "$Env:SYNCTRAYZOR_PRIVATE_KEY" "$Env:SYNCTRAYZOR_PRIVATE_KEY_PASSPHRASE" @($files)
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to sign release files. Exiting."
    exit $LASTEXITCODE
}
