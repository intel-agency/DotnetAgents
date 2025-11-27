param(
    [Parameter(Mandatory = $true)][string]$BaselinePath,
    [Parameter(Mandatory = $true)][string]$CurrentPath,
    [double]$MaxDropPercent = 2.0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $BaselinePath)) {
    throw "Baseline coverage file not found at '$BaselinePath'."
}

if (-not (Test-Path -LiteralPath $CurrentPath)) {
    throw "Current coverage file not found at '$CurrentPath'."
}

$baseline = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
$current = Get-Content -LiteralPath $CurrentPath -Raw | ConvertFrom-Json

function Test-Drop([string]$Metric, [double]$BaselineValue, [double]$CurrentValue) {
    $drop = [math]::Round($BaselineValue - $CurrentValue, 2)
    if ($drop -gt $MaxDropPercent) {
        throw "Coverage regression detected for $Metric: baseline=$BaselineValue, current=$CurrentValue, drop=$drop (> $MaxDropPercent)."
    }
}

Test-Drop -Metric 'line' -BaselineValue $baseline.line -CurrentValue $current.line
Test-Drop -Metric 'branch' -BaselineValue $baseline.branch -CurrentValue $current.branch

foreach ($assemblyName in $baseline.assemblies.PSObject.Properties.Name) {
    if (-not $current.assemblies.PSObject.Properties.Name.Contains($assemblyName)) {
        Write-Warning "Assembly '$assemblyName' missing from current coverage."
        continue
    }

    $baselineAssembly = $baseline.assemblies.$assemblyName
    $currentAssembly = $current.assemblies.$assemblyName

    Test-Drop -Metric "$assemblyName.line" -BaselineValue $baselineAssembly.line -CurrentValue $currentAssembly.line
    Test-Drop -Metric "$assemblyName.branch" -BaselineValue $baselineAssembly.branch -CurrentValue $currentAssembly.branch
}
