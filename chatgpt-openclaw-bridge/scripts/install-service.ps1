param(
    [ValidateSet("Preview", "Install", "Uninstall")]
    [string]$Action = "Preview",
    [string]$TaskName = "XingyuanOpenClawCodexBridge",
    [int]$Iterations = 288,
    [int]$IntervalSeconds = 300,
    [int]$Limit = 5,
    [switch]$Execute,
    [switch]$Approved
)

$ErrorActionPreference = "Stop"
$BridgeRoot = Split-Path -Parent $PSScriptRoot
$StartScript = Join-Path $PSScriptRoot "start-bridge.ps1"
$StopScript = Join-Path $PSScriptRoot "stop-bridge.ps1"
$StartupDir = [Environment]::GetFolderPath("Startup")
$StartupScript = Join-Path $StartupDir "$TaskName.cmd"

if (-not (Test-Path -LiteralPath $StartScript)) {
    throw "Bridge start script not found: $StartScript"
}

$scheduledAction = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$StartScript`" -Iterations $Iterations -IntervalSeconds $IntervalSeconds -Limit $Limit"

$plan = [ordered]@{
    action = $Action
    taskName = $TaskName
    execute = [bool]$Execute
    approved = [bool]$Approved
    bridgeRoot = $BridgeRoot
    startCommand = $scheduledAction
    stopCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$StopScript`""
    startupFallback = $StartupScript
    policy = "V2.0 requires explicit approval before installing or removing persistent startup behavior."
}

if (-not $Execute -or $Action -eq "Preview") {
    $plan.status = "preview"
    $plan | ConvertTo-Json -Depth 5
    exit 0
}

if (-not $Approved) {
    $plan.status = "blocked"
    $plan.reason = "Use -Approved only after a matching approval-decision has been recorded."
    $plan | ConvertTo-Json -Depth 5
    exit 2
}

if ($Action -eq "Install") {
    $taskAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$StartScript`" -Iterations $Iterations -IntervalSeconds $IntervalSeconds -Limit $Limit"
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew
    try {
        Register-ScheduledTask -TaskName $TaskName -Action $taskAction -Trigger $trigger -Settings $settings -Description "Xingyuan OpenClaw/Codex bridge supervisor" -Force | Out-Null
        $plan.status = "installed_scheduled_task"
        $plan.installedPath = $TaskName
    }
    catch {
        $fallbackCommand = "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$StartScript`" -Iterations $Iterations -IntervalSeconds $IntervalSeconds -Limit $Limit`r`n"
        Set-Content -LiteralPath $StartupScript -Value $fallbackCommand -Encoding ASCII
        $plan.status = "installed_startup_folder"
        $plan.installedPath = $StartupScript
        $plan.scheduledTaskError = $_.Exception.Message
    }
    $plan | ConvertTo-Json -Depth 5
    exit 0
}

if ($Action -eq "Uninstall") {
    $removed = @()
    try {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction Stop
        $removed += "scheduled_task"
    }
    catch {
        $plan.scheduledTaskRemoveError = $_.Exception.Message
    }
    if (Test-Path -LiteralPath $StartupScript) {
        Remove-Item -LiteralPath $StartupScript -Force
        $removed += "startup_folder"
    }
    $plan.status = "uninstalled"
    $plan.removed = $removed
    $plan | ConvertTo-Json -Depth 5
    exit 0
}
