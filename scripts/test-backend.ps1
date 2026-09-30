$ErrorActionPreference = 'Stop'
Get-Content (Join-Path $PSScriptRoot '../.env') | ForEach-Object {
    if ($_ -match '^([A-Z_][A-Z0-9_]*)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1], $matches[2], 'Process') }
}
# La contraseña se pasa por entorno, nunca por argumentos ni por salida.
$builder = New-Object System.Data.Common.DbConnectionStringBuilder
$builder['Host'] = 'localhost'
$builder['Port'] = '5434'
$builder['Username'] = $env:POSTGRES_USER
$builder['Password'] = $env:POSTGRES_PASSWORD
$builder['GSS Encryption Mode'] = 'Disable'
$env:IDENTITY_TEST_CONNECTION = $builder.ConnectionString
dotnet test (Join-Path $PSScriptRoot '../tests/ClinicaRamirez.Api.Tests/ClinicaRamirez.Api.Tests.csproj') --no-restore --logger 'console;verbosity=minimal'
exit $LASTEXITCODE
