$ErrorActionPreference = "Stop"

dotnet test src/
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed. Exiting."
    exit $LASTEXITCODE
}