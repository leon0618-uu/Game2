param(
    [switch]$Execute,
    [switch]$Approved,
    [int]$RetryFeishu = 1,
    [int]$RetryOpenAI = 1,
    [string]$Model = "gpt-5.5",
    [string]$Message = "Xingyuan OpenClaw/Codex bridge v2.0 real-send verification. This is an approval-gated audit message."
)

$ErrorActionPreference = "Stop"
$BridgeRoot = Split-Path -Parent $PSScriptRoot
$RepoRoot = Split-Path -Parent $BridgeRoot
$Python = "C:\Users\Leon\AppData\Roaming\uv\python\cpython-3.11.15-windows-x86_64-none\python.exe"

function Get-LatestFile([string]$Root, [string]$Pattern) {
    if (-not (Test-Path -LiteralPath $Root)) {
        return $null
    }
    return Get-ChildItem -LiteralPath $Root -Filter $Pattern |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Get-TrustedFeishuTarget() {
    $configPath = Join-Path $env:USERPROFILE ".openclaw\openclaw.json"
    if (-not (Test-Path -LiteralPath $configPath)) {
        return ""
    }
    $text = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8
    if ($text -match '"ownerAllowFrom"\s*:\s*\[\s*"([^"]+)"') {
        return "user:$($Matches[1])"
    }
    if ($text -match '"allowFrom"\s*:\s*\[\s*"([^"]+)"') {
        return "user:$($Matches[1])"
    }
    return ""
}

function Invoke-Bridge([string[]]$BridgeArgs) {
    Push-Location $BridgeRoot
    try {
        $script:LastBridgeOutput = & $Python -m src.main --repo-root $RepoRoot @BridgeArgs
        $exitCode = $LASTEXITCODE
        return $exitCode
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path -LiteralPath $Python)) {
    throw "Python runtime not found: $Python"
}

$approvalDir = Join-Path $BridgeRoot "data\approvals"
$incidentDir = Join-Path $BridgeRoot "data\incidents"
$auditDir = Join-Path $BridgeRoot "data\audit"
$openAiDecision = Get-LatestFile $approvalDir "*approval-decision-*Execute-real-OpenAI-intervention-call-wi.json"
$feishuDecision = Get-LatestFile $approvalDir "*approval-decision-*Execute-real-Feishu-verification-send.json"
$openAiIncident = Get-LatestFile $incidentDir "*-blocked-task-V2-REAL-OPENAI-001.json"
$approvalBundle = Get-LatestFile $auditDir "*-approval-bundle-APPROVALBUNDLE-*.json"
$target = Get-TrustedFeishuTarget

$preview = [ordered]@{
    status = if ($Execute) { "execute_requested" } else { "preview" }
    execute = [bool]$Execute
    approved = [bool]$Approved
    retry_feishu = [bool]$RetryFeishu
    retry_openai = [bool]$RetryOpenAI
    model = $Model
    required_files = [ordered]@{
        openai_decision = if ($openAiDecision) { $openAiDecision.FullName } else { "" }
        openai_incident = if ($openAiIncident) { $openAiIncident.FullName } else { "" }
        feishu_decision = if ($feishuDecision) { $feishuDecision.FullName } else { "" }
        approval_bundle = if ($approvalBundle) { $approvalBundle.FullName } else { "" }
    }
    required_environment = [ordered]@{
        OPENAI_API_KEY = [bool][Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "User")
        OPENCLAW_GATEWAY_TOKEN = [bool][Environment]::GetEnvironmentVariable("OPENCLAW_GATEWAY_TOKEN", "User")
        FEISHU_DEFAULT_APP_SECRET = [bool][Environment]::GetEnvironmentVariable("FEISHU_DEFAULT_APP_SECRET", "User")
        FEISHU_XINGYUAN_APP_SECRET = [bool][Environment]::GetEnvironmentVariable("FEISHU_XINGYUAN_APP_SECRET", "User")
    }
    target_preview = if ($target) { "user:[REDACTED]" } else { "" }
    commands = @(
        "python -m src.main openai-intervention --package <incident> --model $Model --execute --approval-decision <decision> --write",
        "python -m src.main feishu-send --target user:[REDACTED] --message <message> --account xingyuan --execute --approval-decision <decision> --write",
        "python -m src.main v2-unblock-check --write",
        "python -m src.main goal-completion-audit --manifest <approval-bundle> --write"
    )
}

if (-not $Execute) {
    $preview | ConvertTo-Json -Depth 8
    exit 0
}

if (-not $Approved) {
    $preview.status = "blocked"
    $preview.reason = "Retrying external Feishu/OpenAI actions requires -Approved."
    $preview | ConvertTo-Json -Depth 8
    exit 2
}

$results = @()
if ($RetryOpenAI) {
    if (-not $openAiDecision -or -not $openAiIncident) {
        $results += [ordered]@{ action = "openai"; skipped = $true; reason = "Missing OpenAI incident or approval decision." }
    }
    else {
        $env:OPENAI_API_KEY = [Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "User")
        $code = Invoke-Bridge @(
            "openai-intervention",
            "--package", $openAiIncident.FullName,
            "--model", $Model,
            "--execute",
            "--approval-decision", $openAiDecision.FullName,
            "--timeout-seconds", "60",
            "--write"
        )
        $results += [ordered]@{ action = "openai"; exit_code = $code }
    }
}

if ($RetryFeishu) {
    if (-not $feishuDecision -or -not $target) {
        $results += [ordered]@{ action = "feishu"; skipped = $true; reason = "Missing Feishu target or approval decision." }
    }
    else {
        $env:OPENCLAW_GATEWAY_TOKEN = [Environment]::GetEnvironmentVariable("OPENCLAW_GATEWAY_TOKEN", "User")
        $env:FEISHU_DEFAULT_APP_SECRET = [Environment]::GetEnvironmentVariable("FEISHU_DEFAULT_APP_SECRET", "User")
        $env:FEISHU_XINGYUAN_APP_SECRET = [Environment]::GetEnvironmentVariable("FEISHU_XINGYUAN_APP_SECRET", "User")
        $code = Invoke-Bridge @(
            "feishu-send",
            "--target", $target,
            "--message", $Message,
            "--account", "xingyuan",
            "--execute",
            "--approval-decision", $feishuDecision.FullName,
            "--timeout-seconds", "60",
            "--write"
        )
        $results += [ordered]@{ action = "feishu"; exit_code = $code }
    }
}

$checkCode = Invoke-Bridge @("v2-unblock-check", "--write")
$unblockOutput = $script:LastBridgeOutput
$completionCode = 2
$completionOutput = @()
if ($approvalBundle) {
    $completionCode = Invoke-Bridge @("goal-completion-audit", "--manifest", $approvalBundle.FullName, "--write")
    $completionOutput = $script:LastBridgeOutput
}
$payload = [ordered]@{
    status = if ($checkCode -eq 0 -and $completionCode -eq 0) { "complete" } else { "blocked_or_incomplete" }
    execute = $true
    approved = $true
    results = $results
    unblock_check_exit_code = $checkCode
    goal_completion_exit_code = $completionCode
    unblock_check_output_lines = @($unblockOutput).Count
    goal_completion_output_lines = @($completionOutput).Count
    approval_bundle = if ($approvalBundle) { $approvalBundle.FullName } else { "" }
}
$payload | ConvertTo-Json -Depth 8
exit $(if ($checkCode -ne 0) { $checkCode } else { $completionCode })
