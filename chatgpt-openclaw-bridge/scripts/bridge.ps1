param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$BridgeArgs
)

$ErrorActionPreference = "Stop"
$BridgeRoot = Split-Path -Parent $PSScriptRoot
$RepoRoot = Split-Path -Parent $BridgeRoot
$Python = "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe"

if (-not (Test-Path -LiteralPath $Python)) {
    throw "Python runtime not found: $Python"
}

$argsList = @("-m", "src.main", "--repo-root", $RepoRoot) + $BridgeArgs

Push-Location $BridgeRoot
try {
    & $Python @argsList
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
