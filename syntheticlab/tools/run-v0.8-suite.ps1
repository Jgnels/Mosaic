$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

if (Get-Command py -ErrorAction SilentlyContinue) {
    & py -3 "$Root\run_v0_8_suite.py" --seeds 64 --output "$Root\artifacts"
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    & python "$Root\run_v0_8_suite.py" --seeds 64 --output "$Root\artifacts"
} else {
    throw "Python 3 was not found."
}

if ($LASTEXITCODE -ne 0) {
    throw "SyntheticLab v0.8 failed with exit code $LASTEXITCODE"
}

Write-Host "PASS SyntheticLab v0.8" -ForegroundColor Green
Write-Host "$Root\artifacts\syntheticlab-v0.8-report-latest.md"
