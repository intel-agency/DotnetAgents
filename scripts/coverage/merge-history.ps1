param(
    [Parameter(Mandatory = $true)][string]$Branch,
    [Parameter(Mandatory = $true)][string]$NewRunPath,
    [Parameter(Mandatory = $false)][string]$ExistingHistoryPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [int]$MaxEntries = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $NewRunPath)) {
    throw "New run coverage JSON not found at '$NewRunPath'."
}

$newRun = Get-Content -LiteralPath $NewRunPath -Raw | ConvertFrom-Json

if (-not $newRun.version) {
    throw "Coverage JSON must contain a 'version' property."
}

if (-not $newRun.timestamp) {
    $newRun | Add-Member -NotePropertyName timestamp -NotePropertyValue ([DateTime]::UtcNow.ToString('o'))
}

$existing = $null
if ($ExistingHistoryPath -and (Test-Path -LiteralPath $ExistingHistoryPath)) {
    $existing = Get-Content -LiteralPath $ExistingHistoryPath -Raw | ConvertFrom-Json
}

if (-not $existing) {
    $existing = [ordered]@{
        branch = $Branch
        runs   = @()
    }
}

$existing.branch = $Branch

# Remove any prior run with the same version identifier
$filteredRuns = @()
foreach ($run in $existing.runs) {
    if ($run.version -ne $newRun.version) {
        $filteredRuns += $run
    }
}

$allRuns = @($newRun) + $filteredRuns
$sorted = $allRuns | Sort-Object { [DateTime]$_.timestamp } -Descending | Select-Object -First $MaxEntries

$result = [ordered]@{
    branch = $Branch
    runs   = $sorted
    stats  = [ordered]@{
        latestLine    = $sorted[0].line
        latestBranch  = $sorted[0].branch
        bestLine      = ($sorted | Measure-Object -Property line -Maximum).Maximum
        bestBranch    = ($sorted | Measure-Object -Property branch -Maximum).Maximum
        averageLine   = [math]::Round(($sorted | Measure-Object -Property line -Average).Average, 2)
        averageBranch = [math]::Round(($sorted | Measure-Object -Property branch -Average).Average, 2)
    }
}

$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
