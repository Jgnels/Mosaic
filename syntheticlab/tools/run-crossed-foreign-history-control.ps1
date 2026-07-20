$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

if (-not $env:DAGMAY_GEMINI_API_KEY -and -not $env:GEMINI_API_KEY) {
    throw "Set DAGMAY_GEMINI_API_KEY or GEMINI_API_KEY in this PowerShell session first."
}

$Model = if ($env:DAGMAY_GEMINI_MODEL) {
    $env:DAGMAY_GEMINI_MODEL
} else {
    "gemini-3.1-flash-lite"
}

py -3 (Join-Path $Root "run_crossed_foreign_history_control.py") `
    --execute-real `
    --confirmation "I_APPROVE_NINETY_CROSSED_FOREIGN_HISTORY_CONTROL_CALLS" `
    --model $Model
