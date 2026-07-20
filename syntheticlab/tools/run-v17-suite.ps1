$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
py -3 (Join-Path $Root "run_v17_suite.py")
