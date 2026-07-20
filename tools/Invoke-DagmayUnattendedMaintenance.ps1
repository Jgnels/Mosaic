[CmdletBinding()]
param(
    [ValidateRange(1, 256)]
    [int] $Seeds = 16,

    [string] $Python,

    [string] $OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$labRoot = Join-Path $repoRoot "syntheticlab"

if ([string]::IsNullOrWhiteSpace($Python)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) {
        $pythonCommand = Get-Command py -ErrorAction SilentlyContinue
    }
    if ($null -eq $pythonCommand) {
        throw "Python 3 was not found. Pass -Python with an explicit executable path."
    }
    $Python = $pythonCommand.Source
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $env:LOCALAPPDATA "Dagmay\unattended"
}

$runId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$runRoot = Join-Path $OutputRoot $runId
$artifactRoot = Join-Path $runRoot "artifacts"
$cacheRoot = Join-Path ([IO.Path]::GetTempPath()) ("MosaicPyCache-" + $runId)
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null

# Fail closed: maintenance never inherits provider credentials and therefore
# cannot accidentally become a real-provider experiment.
$priorDagmay = $env:DAGMAY_GEMINI_API_KEY
$priorGemini = $env:GEMINI_API_KEY
$priorCache = $env:PYTHONPYCACHEPREFIX
$env:DAGMAY_GEMINI_API_KEY = $null
$env:GEMINI_API_KEY = $null
$env:PYTHONPYCACHEPREFIX = $cacheRoot

$startedUtc = (Get-Date).ToUniversalTime()
$status = "FAILED"
$failure = $null

try {
    $priorPythonPath = $env:PYTHONPATH
    $env:PYTHONPATH = Join-Path $labRoot "src"
    & $Python -m dagmay_synthetic_lab.persistent_character_boundary_offline_tests
    if ($LASTEXITCODE -ne 0) {
        throw "Mosaic persistent-character boundary tests failed with exit code $LASTEXITCODE."
    }

    & $Python -m dagmay_synthetic_lab.character_quality_suite_offline_tests
    if ($LASTEXITCODE -ne 0) {
        throw "Mosaic character-quality tests failed with exit code $LASTEXITCODE."
    }

    & $Python -m dagmay_synthetic_lab.objective_surface_audit_offline_tests
    if ($LASTEXITCODE -ne 0) {
        throw "Mosaic objective-surface audit failed with exit code $LASTEXITCODE."
    }

    & $Python -m compileall -q $labRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Python compilation failed with exit code $LASTEXITCODE."
    }

    & $Python (Join-Path $labRoot "run_research_suite.py") --seeds $Seeds --output $artifactRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Offline research suite failed with exit code $LASTEXITCODE."
    }

    & $Python -m dagmay_synthetic_lab.character_quality_suite --seeds ([Math]::Max($Seeds, 64)) --output (Join-Path $artifactRoot "mosaic-memory-relevance-001.json")
    if ($LASTEXITCODE -ne 0) {
        throw "Mosaic character-quality suite failed with exit code $LASTEXITCODE."
    }

    & $Python -m dagmay_synthetic_lab.objective_surface_audit --repo-root $repoRoot --output (Join-Path $artifactRoot "mosaic-objective-surface-audit-001.json")
    if ($LASTEXITCODE -ne 0) {
        throw "Mosaic objective-surface artifact generation failed with exit code $LASTEXITCODE."
    }

    $status = "PASSED"
}
catch {
    $failure = $_.Exception.Message
    throw
}
finally {
    $env:DAGMAY_GEMINI_API_KEY = $priorDagmay
    $env:GEMINI_API_KEY = $priorGemini
    $env:PYTHONPYCACHEPREFIX = $priorCache
    $env:PYTHONPATH = $priorPythonPath

    $finishedUtc = (Get-Date).ToUniversalTime()
    $report = [ordered]@{
        schema_version = 1
        run_id = $runId
        mode = "MOSAIC_OFFLINE_PERSISTENT_CHARACTER_MAINTENANCE"
        active_objective = "persistent_character_quality"
        provider_calls_authorized = $false
        provider_credentials_exposed = $false
        status = $status
        seeds = $Seeds
        started_utc = $startedUtc.ToString("o")
        finished_utc = $finishedUtc.ToString("o")
        duration_seconds = [Math]::Round(($finishedUtc - $startedUtc).TotalSeconds, 3)
        artifacts = $artifactRoot
        failure = $failure
    }

    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot "run-report.json") -Encoding UTF8
}

Write-Host "PASS: unattended offline maintenance completed." -ForegroundColor Green
Write-Host "Report: $(Join-Path $runRoot 'run-report.json')"
Write-Host "Artifacts: $artifactRoot"
