$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

function Find-Python {
    if (Get-Command py -ErrorAction SilentlyContinue) {
        return @("py", "-3")
    }
    if (Get-Command python -ErrorAction SilentlyContinue) {
        return @("python")
    }
    throw "Python 3 was not found."
}

$Python = Find-Python
Write-Host "Running Dagmay SyntheticLab v0.6 research suite..." -ForegroundColor Cyan

if ($Python.Count -eq 2) {
    & $Python[0] $Python[1] "$Root\run_research_suite.py" --seeds 64 --output "$Root\artifacts"
} else {
    & $Python[0] "$Root\run_research_suite.py" --seeds 64 --output "$Root\artifacts"
}

if ($LASTEXITCODE -ne 0) {
    throw "SyntheticLab v0.6 failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "PASS SyntheticLab v0.6." -ForegroundColor Green
Write-Host "Report: $Root\artifacts\syntheticlab-v0.6-report-latest.md"
