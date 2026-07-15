param(
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$BridgeRoot = Split-Path -Parent $PSScriptRoot
$RepoRoot = Split-Path -Parent $BridgeRoot
$Python = "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe"

if (-not (Test-Path -LiteralPath $Python)) {
    throw "Python runtime not found: $Python"
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $BridgeRoot "data\handoff"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Push-Location $BridgeRoot
try {
    $jsonText = & $Python -m src.main --repo-root $RepoRoot v2-unblock-check --write --write-report --output-dir $OutputDir
    $checkCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

$payload = $jsonText | ConvertFrom-Json
$handoffPath = Join-Path $OutputDir "v2-external-blocker-handoff.md"
$handoffJsonPath = Join-Path $OutputDir "v2-external-blocker-handoff.json"

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# V2.0 External Blocker Handoff")
$lines.Add("")
$lines.Add(("- Overall status: {0}" -f $payload.overall_status))
$lines.Add(("- Complete: {0}" -f $payload.complete))
$lines.Add(("- External blocker count: {0}" -f $payload.external_blocker_count))
$lines.Add(("- Generated unblock report: {0}" -f $payload.report_path))
$lines.Add(("- Runbook: {0}" -f $payload.remediation.runbook))
$lines.Add("")
$lines.Add("## Required External Actions")
$lines.Add("")
foreach ($blocker in @($payload.external_blockers)) {
    $lines.Add("### $($blocker.requirement_id)")
    $lines.Add("")
    $lines.Add(("- Status: {0}" -f $blocker.status))
    $lines.Add("- Requirement: $($blocker.requirement)")
    $lines.Add("- Next action: $($blocker.next_action)")
    $lines.Add("- Evidence:")
    foreach ($evidence in @($blocker.evidence)) {
        $lines.Add(("  - {0}" -f $evidence))
    }
    $lines.Add("")
}
$lines.Add("## Verification Commands")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add($payload.remediation.preview_retry_command)
$lines.Add($payload.remediation.approved_retry_command)
$lines.Add($payload.remediation.completion_gate_command)
$lines.Add('```')
$lines.Add("")
$lines.Add("Do not paste secret values into this handoff. All required secret checks must stay in the local user environment.")

$lines | Set-Content -LiteralPath $handoffPath -Encoding UTF8
@{
    status = $payload.overall_status
    complete = $payload.complete
    external_blocker_count = $payload.external_blocker_count
    external_blockers = $payload.external_blockers
    remediation = $payload.remediation
    unblock_audit = $payload.audit_path
    unblock_report = $payload.report_path
    handoff_markdown = $handoffPath
    runbook = $payload.remediation.runbook
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $handoffJsonPath -Encoding UTF8

@{
    status = $payload.overall_status
    complete = $payload.complete
    external_blocker_count = $payload.external_blocker_count
    unblock_check_exit_code = $checkCode
    handoff_markdown = $handoffPath
    handoff_json = $handoffJsonPath
    unblock_report = $payload.report_path
} | ConvertTo-Json -Depth 6

exit 0
