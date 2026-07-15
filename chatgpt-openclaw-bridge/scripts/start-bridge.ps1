param(
    [switch]$Once,
    [int]$Iterations = 1,
    [int]$IntervalSeconds = 300,
    [int]$Limit = 5
)

$ErrorActionPreference = "Stop"
$BridgeRoot = Split-Path -Parent $PSScriptRoot
$RepoRoot = Split-Path -Parent $BridgeRoot
$Python = "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe"

if (-not (Test-Path -LiteralPath $Python)) {
    throw "Python runtime not found: $Python"
}

$argsList = @("-m", "src.main", "--repo-root", $RepoRoot, "supervisor-run", "--limit", "$Limit")
if ($Once) {
    $argsList += "--once"
} else {
    $argsList += @("--iterations", "$Iterations", "--interval-seconds", "$IntervalSeconds")
}

Push-Location $BridgeRoot
try {
    & $Python @argsList
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}

