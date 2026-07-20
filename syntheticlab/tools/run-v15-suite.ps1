$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
py -3 (Join-Path $Root "run_v15_suite.py")
