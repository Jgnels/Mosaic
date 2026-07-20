$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (Get-Command py -ErrorAction SilentlyContinue) {
  & py -3 "$Root\run_v1_suite.py" --seeds 64 --output "$Root\artifacts"
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
  & python "$Root\run_v1_suite.py" --seeds 64 --output "$Root\artifacts"
} else { throw "Python 3 was not found." }
if ($LASTEXITCODE -ne 0) { throw "SyntheticLab v1.0 failed with exit code $LASTEXITCODE" }
Write-Host "PASS SyntheticLab v1.0" -ForegroundColor Green
