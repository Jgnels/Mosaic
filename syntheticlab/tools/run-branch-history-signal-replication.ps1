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

py -3 (Join-Path $Root "run_branch_history_signal_replication.py") `
    --execute-real `
    --confirmation "I_APPROVE_SIXTY_SIX_BRANCH_SIGNAL_REPLICATION_CALLS" `
    --model $Model
