$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

function Find-Python {
    if (Get-Command py -ErrorAction SilentlyContinue) {
        return @("py", "-3")
    }
    if (Get-Command python -ErrorAction SilentlyContinue) {
        return @("python")
    }
    throw "Python 3 was not found. Install Python 3, then run this script again."
}

$Python = Find-Python
Write-Host "Running Dagmay SyntheticLab v0.1..." -ForegroundColor Cyan

if ($Python.Count -eq 2) {
    & $Python[0] $Python[1] "$Root\run_lab.py" --self-test --seeds 64 --output "$Root\artifacts"
} else {
    & $Python[0] "$Root\run_lab.py" --self-test --seeds 64 --output "$Root\artifacts"
}

if ($LASTEXITCODE -ne 0) {
    throw "SyntheticLab failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "PASS SyntheticLab. Results:" -ForegroundColor Green
Write-Host "  $Root\artifacts\syntheticlab-report-latest.md"
Write-Host "  $Root\artifacts\syntheticlab-results-latest.json"
Write-Host "  $Root\artifacts\syntheticlab-summary-latest.csv"
