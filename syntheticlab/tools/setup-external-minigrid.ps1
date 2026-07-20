$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Write-Host "Installing optional MiniGrid external-pilot dependencies..."
py -3 -m pip install -r (Join-Path $Root "requirements-external-minigrid.txt")
Write-Host "Running external-environment pilot..."
py -3 (Join-Path $Root "run_external_minigrid_pilot.py")
