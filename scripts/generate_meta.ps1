# Regenerate Unity .meta files for Task 17 with proper 32-hex GUIDs (no dashes)
$ErrorActionPreference = "Stop"

$root = "D:\AI-Worktrees\Xingyuan\ui-tools\Assets\Starfall\Unity\Input"

function New-GuidHex {
    param([int]$N = 1)
    $bytes = New-Object byte[] 16
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    $g = New-Object System.Guid (,$bytes)
    return $g.ToString("N")
}

function Write-CsMeta($path, $guid) {
    $content = "fileFormatVersion: 2`nguid: $guid`nMonoImporter:`n  externalObjects: {}`n  serializedVersion: 2`n  defaultReferences: []`n  executionOrder: 0`n  icon: {instanceID: 0}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n"
    Set-Content -Path $path -Value $content -NoNewline -Encoding UTF8
}

function Write-FolderMeta($path, $guid) {
    $content = "fileFormatVersion: 2`nguid: $guid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n"
    Set-Content -Path $path -Value $content -NoNewline -Encoding UTF8
}

# 1. Folder meta
$folderGuid = New-GuidHex
Write-FolderMeta "$root.meta" $folderGuid
Write-Output "Folder: $folderGuid"

# 2. Scripts metas
$files = @(
    "InputMode.cs",
    "InputAction.cs",
    "InputState.cs",
    "InputStateMachine.cs",
    "CommandBuilder.cs",
    "InputController.cs"
)
foreach ($f in $files) {
    $g = New-GuidHex
    Write-CsMeta "$root\$f.meta" $g
    Write-Output "$f -> $g"
}

# 3. EditMode test meta
$testRoot = "D:\AI-Worktrees\Xingyuan\ui-tools\Assets\Starfall\Tests\EditMode"
$tg = New-GuidHex
Write-CsMeta "$testRoot\InputStateMachineTests.cs.meta" $tg
Write-Output "InputStateMachineTests.cs -> $tg"

Write-Output "Done."