[CmdletBinding()]
param(
    [string]$Root = "",
    [string]$RimWorldPath = "",
    [string]$Artifacts = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) { throw "0.2FG CLOSEOUT FAIL: $Message" }

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent $PSScriptRoot
}

$Dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$TestsProject = Join-Path $Root "Dagmay.Tests\Dagmay.Tests.csproj"
$HarnessProject = Join-Path $Root "Dagmay.IntegrationHarness\Dagmay.IntegrationHarness.csproj"
$TraceSource = Join-Path $Root "Dagmay.Core\Diagnostics\ReadOnlyDecisionTrace.cs"
$BuildScript = Join-Path $Root "tools\build.ps1"
$DeterminismScript = Join-Path $Root "tools\verify-determinism-closure.ps1"
$StaticVerify = Join-Path $Root "tools\static_verify.py"
$PackageFirewall = Join-Path $Root "tools\package-firewall.ps1"
$PackageFirewallTests = Join-Path $Root "tools\test-package-firewall.ps1"
$Preflight = Join-Path $Root "tools\gate3-preflight.ps1"
$PackagePath = Join-Path $Root "artifacts\Dagmay-RimWorld-0.2-prealpha.zip"

if ([string]::IsNullOrWhiteSpace($Artifacts)) {
    $Artifacts = Join-Path $Root "artifacts\0.2fg-closeout"
}
New-Item -ItemType Directory -Path $Artifacts -Force | Out-Null

if (-not (Test-Path -LiteralPath $TraceSource -PathType Leaf)) {
    Fail "read-only decision trace source is missing."
}

$Forbidden = @(
    'GenerateStructuredAsync\s*\(',
    '(?<!builder)\.Append\s*\(',
    '\.Save\s*\(',
    '\.Enqueue\s*\(',
    'TryCommit\s*\(',
    'WithAffect\s*\(',
    'TransitionLifecycle\s*\(',
    'StartJob\s*\(',
    '\.jobs\b',
    '\.pather\b',
    'UnityEngine\.',
    'Verse\.'
)
$TraceText = [IO.File]::ReadAllText($TraceSource)
foreach ($Pattern in $Forbidden) {
    if ([Regex]::IsMatch($TraceText, $Pattern)) {
        Fail "read-only trace source matched forbidden authority surface: $Pattern"
    }
}
Write-Host "PASS read-only decision trace authority firewall."

if (Test-Path -LiteralPath $StaticVerify -PathType Leaf) {
    $Python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -ne $Python) {
        & $Python.Source $StaticVerify
    }
    else {
        $Py = Get-Command py -ErrorAction SilentlyContinue
        if ($null -eq $Py) { Fail "neither python nor py was found for static verification." }
        & $Py.Source -3 $StaticVerify
    }
    if ($LASTEXITCODE -ne 0) { Fail "static verification failed." }
}

& $Dotnet restore (Join-Path $Root "Dagmay.sln")
if ($LASTEXITCODE -ne 0) { Fail "dotnet restore failed." }

if (Test-Path -LiteralPath $BuildScript -PathType Leaf) {
    if ([string]::IsNullOrWhiteSpace($RimWorldPath)) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $BuildScript
    }
    else {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $BuildScript `
            -RimWorldPath $RimWorldPath
    }
    if ($LASTEXITCODE -ne 0) { Fail "repository build script failed." }
}
else {
    & $Dotnet build (Join-Path $Root "Dagmay.sln") -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { Fail "solution Release build failed." }
}

$TestOutputs = @()
for ($Run = 1; $Run -le 2; $Run++) {
    $Output = & $Dotnet run --project $TestsProject -c Release --no-restore 2>&1
    if ($LASTEXITCODE -ne 0) {
        Fail "contract run $Run failed.`n$($Output -join "`n")"
    }
    $Normalized = (($Output | Where-Object { $_ -match '^(PASS|Executed)' }) -join "`n")
    $TestOutputs += $Normalized
    $Output | Set-Content -LiteralPath (Join-Path $Artifacts "contracts-$Run.txt") -Encoding UTF8
}
if ($TestOutputs.Count -ne 2 -or $TestOutputs[0] -cne $TestOutputs[1]) {
    Fail "two contract processes produced different normalized output."
}
Write-Host "PASS repeated contract processes."

$TraceDigests = @()
$BenchmarkDigests = @()
for ($Run = 1; $Run -le 2; $Run++) {
    $TraceReport = Join-Path $Artifacts "decision-trace-$Run.json"
    & $Dotnet run --project $HarnessProject -c Release --no-restore -- `
        --scenario ReadOnlyDecisionTraceFoundation `
        --output $TraceReport
    if ($LASTEXITCODE -ne 0) { Fail "decision trace scenario run $Run failed." }
    $TraceJson = Get-Content -LiteralPath $TraceReport -Raw | ConvertFrom-Json
    $TraceScenario = @($TraceJson.Scenarios | Where-Object {
        $_.Name -eq "ReadOnlyDecisionTraceFoundation"
    })
    if ($TraceScenario.Count -ne 1 -or -not $TraceScenario[0].Passed) {
        Fail "decision trace report did not contain one passing scenario."
    }
    $TraceDigests += [string]$TraceScenario[0].Details.decisionTraceDigest

    $BenchmarkReport = Join-Path $Artifacts "long-history-benchmark-$Run.json"
    & $Dotnet run --project $HarnessProject -c Release --no-restore -- `
        --scenario LongHistoryRetrievalBenchmark `
        --output $BenchmarkReport
    if ($LASTEXITCODE -ne 0) { Fail "benchmark scenario run $Run failed." }
    $BenchmarkJson = Get-Content -LiteralPath $BenchmarkReport -Raw | ConvertFrom-Json
    $BenchmarkScenario = @($BenchmarkJson.Scenarios | Where-Object {
        $_.Name -eq "LongHistoryRetrievalBenchmark"
    })
    if ($BenchmarkScenario.Count -ne 1 -or -not $BenchmarkScenario[0].Passed) {
        Fail "benchmark report did not contain one passing scenario."
    }
    $BenchmarkDigests += [string]$BenchmarkScenario[0].Details.benchmarkDigest
    if ($Run -eq 1) {
        [IO.File]::WriteAllText(
            (Join-Path $Artifacts "long-history-benchmark.csv"),
            [string]$BenchmarkScenario[0].Details.caseCsv,
            (New-Object Text.UTF8Encoding($false)))
    }
}

if ([string]::IsNullOrWhiteSpace($TraceDigests[0]) -or
    $TraceDigests[0] -cne $TraceDigests[1]) {
    Fail "decision trace digest did not reproduce across processes."
}
if ([string]::IsNullOrWhiteSpace($BenchmarkDigests[0]) -or
    $BenchmarkDigests[0] -cne $BenchmarkDigests[1]) {
    Fail "benchmark digest did not reproduce across processes."
}

if (Test-Path -LiteralPath $DeterminismScript -PathType Leaf) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $DeterminismScript `
        -Root $Root
    if ($LASTEXITCODE -ne 0) { Fail "existing determinism closure verifier failed." }
}
if (Test-Path -LiteralPath $PackageFirewallTests -PathType Leaf) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PackageFirewallTests
    if ($LASTEXITCODE -ne 0) { Fail "package firewall fixtures failed." }
}
if (Test-Path -LiteralPath $PackageFirewall -PathType Leaf) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PackageFirewall `
        -PackagePath $PackagePath `
        -SourceRoot $Root
    if ($LASTEXITCODE -ne 0) { Fail "package firewall failed." }
}
if (Test-Path -LiteralPath $Preflight -PathType Leaf) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Preflight `
        -TestRunId "mosaic-02fg-closeout-20260727" `
        -PackagePath $PackagePath `
        -RimWorldPath $RimWorldPath
    if ($LASTEXITCODE -ne 0) { Fail "non-installing preflight failed." }
}

$Summary = [ordered]@{
    generatedUtc = [DateTimeOffset]::UtcNow.ToString("o")
    decisionTraceDigest = $TraceDigests[0]
    benchmarkDigest = $BenchmarkDigests[0]
    contractsRepeatIdentical = $true
    authorityFirewall = "PASS"
}
$Summary | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $Artifacts "0.2fg-summary.json") -Encoding UTF8

Write-Host "PASS 0.2F decision trace digest=$($TraceDigests[0])"
Write-Host "PASS 0.2G benchmark digest=$($BenchmarkDigests[0])"
Write-Host "Artifacts: $Artifacts"
