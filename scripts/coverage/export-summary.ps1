param(
    [Parameter(Mandatory = $true)][string]$CoverageJsonPath,
    [Parameter(Mandatory = $true)][string]$Branch,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $CoverageJsonPath)) {
    throw "Coverage JSON not found at '$CoverageJsonPath'."
}

$raw = Get-Content -LiteralPath $CoverageJsonPath -Raw | ConvertFrom-Json

function Get-CoverageSummary {
    param([object]$Node)

    $lineTotal = 0
    $lineCovered = 0
    $branchTotal = 0
    $branchCovered = 0

    foreach ($fileProperty in $Node.PSObject.Properties) {
        $fileNode = $fileProperty.Value
        foreach ($typeProperty in $fileNode.PSObject.Properties) {
            $typeNode = $typeProperty.Value
            foreach ($methodProperty in $typeNode.PSObject.Properties) {
                $methodNode = $methodProperty.Value
                if ($methodNode.Lines) {
                    foreach ($lineProperty in $methodNode.Lines.PSObject.Properties) {
                        $lineTotal += 1
                        if ([int]$lineProperty.Value -gt 0) {
                            $lineCovered += 1
                        }
                    }
                }
                if ($methodNode.Branches) {
                    foreach ($branch in $methodNode.Branches) {
                        $branchTotal += 1
                        if ([int]$branch.Hits -gt 0) {
                            $branchCovered += 1
                        }
                    }
                }
            }
        }
    }

    return [ordered]@{
        lineTotal    = $lineTotal
        lineCovered  = $lineCovered
        branchTotal  = $branchTotal
        branchCovered = $branchCovered
        line         = if ($lineTotal -eq 0) { 100 } else { [math]::Round(($lineCovered / [double]$lineTotal) * 100, 2) }
        branch       = if ($branchTotal -eq 0) { 100 } else { [math]::Round(($branchCovered / [double]$branchTotal) * 100, 2) }
    }
}

$assemblySummaries = [ordered]@{}

$overallLineTotal = 0
$overallLineCovered = 0
$overallBranchTotal = 0
$overallBranchCovered = 0

foreach ($assemblyProperty in $raw.PSObject.Properties) {
    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($assemblyProperty.Name)
    $summary = Get-CoverageSummary -Node $assemblyProperty.Value
    $assemblySummaries[$assemblyName] = [ordered]@{
        line          = $summary.line
        branch        = $summary.branch
        lineTotal     = $summary.lineTotal
        lineCovered   = $summary.lineCovered
        branchTotal   = $summary.branchTotal
        branchCovered = $summary.branchCovered
    }

    $overallLineTotal += $summary.lineTotal
    $overallLineCovered += $summary.lineCovered
    $overallBranchTotal += $summary.branchTotal
    $overallBranchCovered += $summary.branchCovered
}

$overallLine = if ($overallLineTotal -eq 0) { 100 } else { [math]::Round(($overallLineCovered / [double]$overallLineTotal) * 100, 2) }
$overallBranch = if ($overallBranchTotal -eq 0) { 100 } else { [math]::Round(($overallBranchCovered / [double]$overallBranchTotal) * 100, 2) }

$result = [ordered]@{
    version           = $Version
    branch            = $Branch
    timestamp         = (Get-Date).ToUniversalTime().ToString('o')
    line              = $overallLine
    branch            = $overallBranch
    lineTotal         = $overallLineTotal
    lineCovered       = $overallLineCovered
    branchTotal       = $overallBranchTotal
    branchCovered     = $overallBranchCovered
    assemblies        = $assemblySummaries
}

$directory = Split-Path -Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
