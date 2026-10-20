param([switch]$Execute)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $arguments = @((Join-Path $PSScriptRoot 'reset-test-data.py'))
    if ($Execute) { $arguments += '--execute' }
    python @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Development reset failed; inspect the reported error.' }
} finally { Pop-Location }
