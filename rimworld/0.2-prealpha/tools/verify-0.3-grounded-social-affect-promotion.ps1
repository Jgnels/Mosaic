[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    throw "MOSAIC 0.3 GROUNDED SOCIAL AFFECT SOURCE GATE FAIL: $Message"
}

$Root = [IO.Path]::GetFullPath($Root)
$required = @(
    "Dagmay.Core\Appraisal\GroundedSocialAffectContracts.cs",
    "Dagmay.Core\Affect\BoundedSocialAffectContracts.cs",
    "Dagmay.RimWorld\Perception\GroundedSocialSemanticProjectionAdapter.cs",
    "Dagmay.Tests\Mosaic03GroundedSocialAffectContractTests.cs",
    "docs\MOSAIC_0.3_GROUNDED_SOCIAL_AFFECT_PROMOTION_BOUNDARY.md",
    "fixtures\Mosaic_v37_Grounded_Social_Affect_Test_Vectors.json"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) {
        Fail "Missing required promotion file: $relative"
    }
}

$production = @(
    Get-Content -LiteralPath (Join-Path $Root "Dagmay.Core\Appraisal\GroundedSocialAffectContracts.cs") -Raw
    Get-Content -LiteralPath (Join-Path $Root "Dagmay.Core\Affect\BoundedSocialAffectContracts.cs") -Raw
    Get-Content -LiteralPath (Join-Path $Root "Dagmay.RimWorld\Perception\GroundedSocialSemanticProjectionAdapter.cs") -Raw
) -join "`n"

foreach ($forbidden in @(
    "Verse.Pawn",
    "Verse.Map",
    "HarmonyPatch",
    "JobMaker",
    "StartJob",
    "TryTakeOrderedJob",
    "GenerateStructuredAsync",
    "HttpClient",
    "Scribe_",
    "DateTime.UtcNow",
    "DateTimeOffset.UtcNow",
    "Guid.NewGuid(",
    "new Random(",
    "File.Write"
)) {
    if ($production.Contains($forbidden)) {
        Fail "Forbidden authority, I/O, or nondeterminism token: $forbidden"
    }
}

$affect = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Core\Affect\BoundedSocialAffectContracts.cs") -Raw
if (-not $affect.Contains("internal AffectTransitionProposal(")) {
    Fail "AffectTransitionProposal construction must remain internal."
}

$testProject = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Dagmay.Tests.csproj") -Raw
$requiredTestLink = '<Compile Include="..\Dagmay.RimWorld\Perception\GroundedSocialSemanticProjectionAdapter.cs" Link="RimWorldPure\GroundedSocialSemanticProjectionAdapter.cs" />'
if (-not $testProject.Contains($requiredTestLink)) {
    Fail "Dagmay.Tests.csproj does not link the pure grounded-social projection adapter."
}

$tests = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Mosaic03GroundedSocialAffectContractTests.cs") -Raw
if ($tests -match '\.Contains\s*\([^\r\n,]+,\s*StringComparison\.') {
    Fail "Unsupported netstandard2.0 string.Contains overload found."
}
if (-not $tests.Contains("BindingFlags.Instance | BindingFlags.NonPublic")) {
    Fail "Adversarial proposal test must preserve the internal production constructor."
}

$fixture = Get-Content -LiteralPath (Join-Path $Root "fixtures\Mosaic_v37_Grounded_Social_Affect_Test_Vectors.json") -Raw |
    ConvertFrom-Json
if ($null -eq $fixture.schema) {
    Fail "Shared deterministic fixture has no schema."
}

Write-Host "PASS Mosaic 0.3 grounded social affect promotion source boundary."
