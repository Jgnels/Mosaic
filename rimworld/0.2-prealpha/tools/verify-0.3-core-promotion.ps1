[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$RimWorldPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) { throw "MOSAIC 0.3 CORE PROMOTION FAIL: $Message" }

$CoreSources = @(
    (Join-Path $Root "Dagmay.Core\Appraisal\CompoundAppraisalContracts.cs"),
    (Join-Path $Root "Dagmay.Core\Relationships\SocialMeaningContracts.cs"),
    (Join-Path $Root "Dagmay.Core\Diagnostics\CharacterWhyProjection.cs"),
    (Join-Path $Root "Dagmay.Core\Development\DevelopmentalCharacterContracts.cs"),
    (Join-Path $Root "Dagmay.Core\Decisions\ContextualDecisionContracts.cs")
)
foreach ($Path in $CoreSources) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "missing candidate source: $Path"
    }
}

$Forbidden = @(
    'UnityEngine\.',
    'Verse\.',
    'GenerateStructuredAsync\s*\(',
    'StartJob\s*\(',
    'TryTakeOrderedJob\s*\(',
    '\.jobs\b',
    '\.pather\b',
    'File\.Write',
    'Directory\.Create',
    'HttpClient',
    'Google'
)
foreach ($Path in $CoreSources) {
    $Text = [IO.File]::ReadAllText($Path)
    foreach ($Pattern in $Forbidden) {
        if ([Regex]::IsMatch($Text, $Pattern)) {
            Fail "$Path matched forbidden authority surface: $Pattern"
        }
    }
}
Write-Host "PASS provider/environment/persistence/pawn-authority source firewall."

$BuildScript = Join-Path $Root "tools\build.ps1"
if (-not (Test-Path -LiteralPath $BuildScript -PathType Leaf)) {
    Fail "repository build script is missing."
}
$ChildPowerShellCommand = Get-Command powershell.exe -ErrorAction SilentlyContinue
if ($null -eq $ChildPowerShellCommand) {
    $ChildPowerShellCommand = Get-Command pwsh -ErrorAction SilentlyContinue
}
if ($null -eq $ChildPowerShellCommand) {
    Fail "No child PowerShell executable was found for isolated repository build."
}

$BuildArguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $BuildScript
)
if (-not [string]::IsNullOrWhiteSpace($RimWorldPath)) {
    $BuildArguments += @("-RimWorldPath", $RimWorldPath)
}

$PreviousBuildErrorActionPreference = $ErrorActionPreference
$HasNativeBuildPreference = Test-Path -LiteralPath "variable:PSNativeCommandUseErrorActionPreference"
if ($HasNativeBuildPreference) {
    $PreviousNativeBuildPreference = $PSNativeCommandUseErrorActionPreference
}
try {
    $ErrorActionPreference = "Continue"
    if ($HasNativeBuildPreference) {
        $PSNativeCommandUseErrorActionPreference = $false
    }
    $BuildOutput = @(
        & $ChildPowerShellCommand.Source @BuildArguments 2>&1
    )
    $BuildExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $PreviousBuildErrorActionPreference
    if ($HasNativeBuildPreference) {
        $PSNativeCommandUseErrorActionPreference = $PreviousNativeBuildPreference
    }
}
$BuildOutput | ForEach-Object { Write-Host $_ }
if ($BuildExitCode -ne 0) {
    Fail "repository build failed with exit code $BuildExitCode."
}

$Dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$Tests = Join-Path $Root "Dagmay.Tests\Dagmay.Tests.csproj"
$Outputs = @()
for ($Run = 1; $Run -le 2; $Run++) {
    $Output = & $Dotnet run --project $Tests -c Release --no-restore 2>&1
    if ($LASTEXITCODE -ne 0) {
        Fail "contract process $Run failed.`n$($Output -join "`n")"
    }
    $Normalized = (($Output | Where-Object { $_ -match '^(PASS|Executed)' }) -join "`n")
    $Outputs += $Normalized
}
if ($Outputs.Count -ne 2 -or $Outputs[0] -cne $Outputs[1]) {
    Fail "contract output was not identical across processes."
}

Write-Host "PASS repeated contracts."
Write-Host "No RimWorld runtime test is required until these unused Core contracts are wired."
