$ErrorActionPreference = 'Stop'
Get-Content (Join-Path $PSScriptRoot '../.env') | ForEach-Object {
    if ($_ -match '^([A-Z_][A-Z0-9_]*)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1], $matches[2], 'Process') }
}
$env:POSTGRES_HOST = 'localhost'
$env:POSTGRES_PORT = '5434'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Logging__LogLevel__Default = 'Warning'
dotnet ef database update --project (Join-Path $PSScriptRoot '../apps/api') --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project (Join-Path $PSScriptRoot '../apps/api') --no-build --no-launch-profile -- --seed-clinic
exit $LASTEXITCODE
